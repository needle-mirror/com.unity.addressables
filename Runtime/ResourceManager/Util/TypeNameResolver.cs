using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
#if UNITY_6000_5_OR_NEWER
using UnityEngine.Assemblies;
#endif
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UnityEngine.ResourceManagement.Util
{
    /// <summary>
    /// Cross-runtime type name normalization and resolution.
    ///
    /// Why this exists: catalogs are written in the Editor (Mono today, CoreCLR later) and
    /// read by players running Mono / IL2CPP / CoreCLR. Embedding fully versioned assembly
    /// identities (mscorlib vs System.Private.CoreLib, plus Version/Culture/PublicKeyToken)
    /// makes catalogs runtime-specific and breaks under UnityLinker stripping when type
    /// forwarding facades are removed.
    ///
    /// Writers normalize types via <see cref="NormalizeTypeName"/> + <see cref="GetSimpleAssemblyName"/>;
    /// readers resolve via <see cref="Resolve"/>. The corelib assembly is written as null —
    /// both top-level and for generic arguments — so unqualified names fall back to
    /// <c>Type.GetType</c>'s default lookup, which finds core types on every runtime.
    ///
    /// Known limitation: the corelib fallback only covers types that live in corelib on the
    /// reading runtime. BCL types that moved assemblies between runtimes (e.g. System.Uri:
    /// 'System' on Mono, 'System.Private.Uri' on CoreCLR) resolve only if an assembly with
    /// the recorded simple name survives in the player — facades do not survive UnityLinker
    /// stripping at Minimal or higher. Accepted for now: catalog types are almost always
    /// Unity object or provider types, which live in stable assemblies.
    /// </summary>
    internal static class TypeNameResolver
    {
        static readonly Assembly s_CorelibAssembly = typeof(object).Assembly;

        static readonly object s_LifecycleLock = new object();
        // Null until Initialize and after Shutdown. Shutdown swaps it out, so work
        // that captured an older state can only write to that older state.
        static volatile State s_State;

        // Tests read the generation through this.
        internal static int Generation => s_State?.Generation ?? 0;

        // Tests: the load callback of the current lifecycle.
        internal static Action<Assembly> LoadCallback
        {
            get
            {
                var state = s_State;
                return asm => OnAssemblyLoaded(state, asm);
            }
        }

        // Tests: runs just before a resolve writes its cache entry.
        internal static Action BeforeResolveCacheWrite;

        internal static bool HasCachedResolve(string assemblyName, string typeName)
        {
            var state = s_State;
            return state != null && state.ResolveCache.ContainsKey(CacheKey(assemblyName, typeName));
        }

        // Returns the generation of a cached miss, or -1 if none is cached.
        internal static int GetCachedMissGeneration(string assemblyName, string typeName)
        {
            var state = s_State;
            if (state != null && state.ResolveCache.TryGetValue(CacheKey(assemblyName, typeName), out var cached) && cached.Type == null)
                return cached.Generation;
            return -1;
        }

        static (string, string) CacheKey(string assemblyName, string typeName)
        {
            return (assemblyName ?? string.Empty, typeName);
        }

        readonly struct Entry
        {
            public readonly Type Type;
            public readonly int Generation;

            public Entry(Type type, int generation)
            {
                Type = type;
                Generation = generation;
            }
        }

        // Everything one Initialize..Shutdown lifecycle caches.
        sealed class State
        {
            public readonly ConcurrentDictionary<string, Assembly> SimpleNameToAssembly = new ConcurrentDictionary<string, Assembly>(StringComparer.Ordinal);
            // Unresolved entries count only while their generation is current.
            public readonly ConcurrentDictionary<(string, string), Entry> ResolveCache = new ConcurrentDictionary<(string, string), Entry>();
            // Simple names whose Assembly.Load failed, mapped to the generation of the failure.
            public readonly ConcurrentDictionary<string, int> FailedLoads = new ConcurrentDictionary<string, int>(StringComparer.Ordinal);
#if ADDRESSABLES_LOG_ALL
            public readonly HashSet<string> WarnedCollisions = new HashSet<string>(StringComparer.Ordinal);
#endif
            public readonly AssemblyLoadEventHandler LoadHandler;
            public readonly Func<AssemblyName, Assembly> AssemblyResolver;

            // Accepts late loads; cleared by Shutdown.
            public volatile bool Accepting;
            // Bumped on loads; older unresolved entries expire.
            int m_Generation;

            public int Generation => Volatile.Read(ref m_Generation);

            public State()
            {
                LoadHandler = (sender, args) => OnAssemblyLoaded(this, args.LoadedAssembly);
                AssemblyResolver = ResolveAssembly;
            }

            public void BumpGeneration()
            {
                Interlocked.Increment(ref m_Generation);
            }

            public void Register(Assembly asm)
            {
                if (asm == null || asm.IsDynamic)
                    return;
                string name;
                try { name = asm.GetName().Name; }
                catch { return; }
                if (string.IsNullOrEmpty(name))
                    return;
                if (SimpleNameToAssembly.TryAdd(name, asm))
                    return;
#if ADDRESSABLES_LOG_ALL
                if (SimpleNameToAssembly.TryGetValue(name, out var existing) && !ReferenceEquals(existing, asm))
                {
                    lock (WarnedCollisions)
                    {
                        if (WarnedCollisions.Add(name))
                            Debug.LogWarning($"Multiple loaded assemblies share the simple name '{name}'. Type resolution will use the first registered. Avoid asmdef name collisions.");
                    }
                }
#endif
            }

            Assembly ResolveAssembly(AssemblyName name)
            {
                var simple = name.Name;
                if (string.IsNullOrEmpty(simple))
                    return s_CorelibAssembly;
                if (SimpleNameToAssembly.TryGetValue(simple, out var asm))
                    return asm;
                if (FailedLoads.TryGetValue(simple, out var failedGeneration) && failedGeneration == Generation)
                    return s_CorelibAssembly;
                var generation = Generation;
                Assembly loaded = null;
                try
                {
                    loaded = Assembly.Load(new AssemblyName(simple));
                }
                catch (Exception)
                {
                    // Falls through: the failure is cached below.
                }
                if (loaded == null)
                {
                    FailedLoads[simple] = generation;
                    return s_CorelibAssembly;
                }
                if (Accepting)
                    Register(loaded);
                return loaded;
            }
        }

#if UNITY_EDITOR
        // Drops the AppDomain hook before an editor code reload.
        [InitializeOnLoadMethod]
        static void SubscribeToReload()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Shutdown;
        }
#endif

        // Covers Enter Play Mode without a domain reload.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStaticsOnLoad()
        {
            Shutdown();
        }

        /// <summary>Warms up the resolver's loaded-assembly cache. Call once, early.</summary>
        public static void Initialize()
        {
            GetState();
        }

        static State GetState()
        {
            var state = s_State;
            if (state != null)
                return state;
            lock (s_LifecycleLock)
            {
                state = s_State;
                if (state != null)
                    return state;
                state = new State();
                // Subscribe before scanning so no load falls in the gap.
                state.Accepting = true;
                AppDomain.CurrentDomain.AssemblyLoad += state.LoadHandler;
                try
                {
                    foreach (var asm in GetLoadedAssemblies())
                        state.Register(asm);
                }
                catch
                {
                    state.Accepting = false;
                    AppDomain.CurrentDomain.AssemblyLoad -= state.LoadHandler;
                    throw;
                }
                s_State = state;
                return state;
            }
        }

        /// <summary>Removes the load hook and drops cached assemblies and types.</summary>
        internal static void Shutdown()
        {
            lock (s_LifecycleLock)
            {
                var state = s_State;
                s_State = null;
                if (state == null)
                    return;
                state.Accepting = false;
                AppDomain.CurrentDomain.AssemblyLoad -= state.LoadHandler;
            }
        }

        internal static void OnAssemblyLoaded(Assembly asm)
        {
            var state = s_State;
            if (state != null)
                OnAssemblyLoaded(state, asm);
        }

        static void OnAssemblyLoaded(State state, Assembly asm)
        {
            if (state == null || !state.Accepting || asm.IsDynamic)
                return;
            state.Register(asm);
            state.BumpGeneration();
        }

        /// <summary>
        /// Returns the simple assembly name to embed for a type, or null for the core runtime
        /// assembly (so the reader can use <c>Type.GetType(typeName)</c>).
        /// </summary>
        public static string GetSimpleAssemblyName(Type t)
        {
            if (t == null || t.Assembly == s_CorelibAssembly)
                return null;
            return t.Assembly.GetName().Name;
        }

        /// <summary>
        /// Returns a type name where every embedded assembly identity (in generic argument
        /// AQNs and array element types) has been reduced to the simple name. Non-generic,
        /// non-array types pass through to <c>t.FullName</c> unchanged.
        /// </summary>
        public static string NormalizeTypeName(Type t)
        {
            if (t == null)
                return null;
            if (t.IsArray)
            {
                var elemName = NormalizeTypeName(t.GetElementType());
                int rank = t.GetArrayRank();
                if (rank == 1)
                    return elemName + "[]";
                return elemName + "[" + new string(',', rank - 1) + "]";
            }
            if (t.IsByRef)
                return NormalizeTypeName(t.GetElementType()) + "&";
            if (t.IsPointer)
                return NormalizeTypeName(t.GetElementType()) + "*";
            if (!t.IsGenericType || t.IsGenericTypeDefinition)
                return t.FullName;

            var def = t.GetGenericTypeDefinition();
            var args = t.GetGenericArguments();
            var sb = new StringBuilder(def.FullName);
            sb.Append('[');
            for (int i = 0; i < args.Length; i++)
            {
                if (i > 0)
                    sb.Append(',');
                sb.Append('[');
                sb.Append(NormalizeTypeName(args[i]));
                var innerAsm = GetSimpleAssemblyName(args[i]);
                if (!string.IsNullOrEmpty(innerAsm))
                {
                    sb.Append(", ");
                    sb.Append(innerAsm);
                }
                sb.Append(']');
            }
            sb.Append(']');
            return sb.ToString();
        }

        /// <summary>
        /// Resolves a (possibly null assemblyName, typeName) pair into a Type, using a
        /// runtime-tolerant assembly resolver that matches by simple name and falls back
        /// to the core runtime assembly. Returns null if no match.
        /// </summary>
        public static Type Resolve(string assemblyName, string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return null;
            // Everything below touches only this state, even if Shutdown runs meanwhile.
            var state = GetState();

            var key = CacheKey(assemblyName, typeName);
            if (state.ResolveCache.TryGetValue(key, out var cached) && (cached.Type != null || cached.Generation == state.Generation))
                return cached.Type;
            int generation = state.Generation;

            Type resolved = null;
            try
            {
                if (string.IsNullOrEmpty(assemblyName))
                {
                    // The callbacks are not redundant here: generic arguments can still embed assembly names.
                    resolved = Type.GetType(typeName, state.AssemblyResolver, ResolveType, throwOnError: false);
                }
                else
                {
                    resolved = Type.GetType(typeName + ", " + assemblyName, state.AssemblyResolver, ResolveType, throwOnError: false);
                    if (resolved == null)
                        resolved = Type.GetType(typeName, throwOnError: false);
                }
            }
            catch (Exception ex)
            {
                if (!(ex is FileNotFoundException))
                    Debug.LogException(ex);
            }

            BeforeResolveCacheWrite?.Invoke();
            state.ResolveCache[key] = new Entry(resolved, generation);
            return resolved;
        }

        static Type ResolveType(Assembly asm, string typeName, bool ignoreCase)
        {
            if (asm != null)
            {
                var t = asm.GetType(typeName, throwOnError: false, ignoreCase: ignoreCase);
                if (t != null)
                    return t;
            }
            return Type.GetType(typeName, throwOnError: false, ignoreCase: ignoreCase);
        }

        static IEnumerable<Assembly> GetLoadedAssemblies()
        {
#if UNITY_6000_5_OR_NEWER
            return CurrentAssemblies.GetLoadedAssemblies();
#else
            return AppDomain.CurrentDomain.GetAssemblies();
#endif
        }
    }
}
