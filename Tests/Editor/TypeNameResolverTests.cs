using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine.ResourceManagement.Util;
using UnityEngine.TestTools;

namespace UnityEditor.AddressableAssets.Tests
{
    public class TypeNameResolverTests
    {
        // Holds a same-named assembly; unloaded in teardown.
        object m_CollisionContext;

        [TearDown]
        public void ResetTypeNameResolver()
        {
            TypeNameResolver.BeforeResolveCacheWrite = null;
            TypeNameResolver.Shutdown();
            if (m_CollisionContext == null)
                return;
            m_CollisionContext.GetType().GetMethod("Unload").Invoke(m_CollisionContext, null);
            m_CollisionContext = null;
        }

        [Test]
        public void TypeNameResolver_NullAssembly_ResolvesCorelibType()
        {
            Assert.AreEqual(typeof(string), TypeNameResolver.Resolve(null, "System.String"));
            Assert.AreEqual(typeof(int), TypeNameResolver.Resolve("", "System.Int32"));
        }

        [Test]
        public void TypeNameResolver_UnknownAssembly_ReturnsNullNoThrow()
        {
            Assert.IsNull(TypeNameResolver.Resolve("Definitely.Not.A.Real.Assembly.ZZZ", "Definitely.Not.A.Real.Type.ZZZ"));
        }

        [Test]
        public void TypeNameResolver_GetSimpleAssemblyName_NullForCorelib()
        {
            Assert.IsNull(TypeNameResolver.GetSimpleAssemblyName(typeof(string)));
            Assert.AreEqual("UnityEngine.CoreModule", TypeNameResolver.GetSimpleAssemblyName(typeof(UnityEngine.Vector3)));
        }

        [Test]
        public void TypeNameResolver_InitializeAndShutdown_ResolveStillWorks()
        {
            // Repeated calls must not throw; resolution survives a Shutdown.
            TypeNameResolver.Initialize();
            TypeNameResolver.Initialize();
            TypeNameResolver.Initialize();
            TypeNameResolver.Shutdown();
            TypeNameResolver.Shutdown();
            TypeNameResolver.Initialize();

            Assert.AreEqual(typeof(string), TypeNameResolver.Resolve(null, "System.String"));
            Assert.AreEqual(typeof(UnityEngine.Vector3), TypeNameResolver.Resolve("UnityEngine.CoreModule", "UnityEngine.Vector3"));
        }

        [Test]
        public void TypeNameResolver_CachedMiss_RetriedAfterLaterLoad()
        {
            TypeNameResolver.Shutdown();
            TypeNameResolver.Initialize();
            const string assemblyName = "UUM155913.NotLoaded";
            const string typeName = "UUM155913.NotLoaded.Missing";

            Assert.IsNull(TypeNameResolver.Resolve(assemblyName, typeName));
            var missGeneration = TypeNameResolver.GetCachedMissGeneration(assemblyName, typeName);
            Assert.AreEqual(TypeNameResolver.Generation, missGeneration, "first miss should be cached at the current generation");

            // Any load bumps the generation, which makes the cached miss stale.
            TypeNameResolver.OnAssemblyLoaded(typeof(UnityEngine.Vector3).Assembly);
            Assert.Greater(TypeNameResolver.Generation, missGeneration);

            Assert.IsNull(TypeNameResolver.Resolve(assemblyName, typeName));
            Assert.AreEqual(TypeNameResolver.Generation, TypeNameResolver.GetCachedMissGeneration(assemblyName, typeName), "stale miss should be re-resolved and re-cached");
        }

        [Test]
        public void TypeNameResolver_LoadCallbackFromOldLifecycle_DoesNotTouchNewLifecycle()
        {
            TypeNameResolver.Shutdown();
            TypeNameResolver.Initialize();
            var staleCallback = TypeNameResolver.LoadCallback;

            TypeNameResolver.Shutdown();
            TypeNameResolver.Initialize();
            var generation = TypeNameResolver.Generation;

            staleCallback(typeof(UnityEngine.Vector3).Assembly);

            Assert.AreEqual(generation, TypeNameResolver.Generation, "a late load must not bump the new generation");
        }

        [Test]
        public void TypeNameResolver_ResolveOverlappingShutdown_DoesNotCacheIntoNewLifecycle()
        {
            const string missAssembly = "UUM155913.NotLoaded";
            const string missType = "UUM155913.NotLoaded.Missing";
            const string hitAssembly = "UnityEngine.CoreModule";
            const string hitType = "UnityEngine.Vector3";
            TypeNameResolver.Shutdown();
            TypeNameResolver.Initialize();
            TypeNameResolver.BeforeResolveCacheWrite = () =>
            {
                TypeNameResolver.Shutdown();
                TypeNameResolver.Initialize();
            };

            Assert.AreEqual(typeof(UnityEngine.Vector3), TypeNameResolver.Resolve(hitAssembly, hitType));
            Assert.IsNull(TypeNameResolver.Resolve(missAssembly, missType));

            Assert.IsFalse(TypeNameResolver.HasCachedResolve(hitAssembly, hitType), "a hit must not outlive its lifecycle");
            Assert.IsFalse(TypeNameResolver.HasCachedResolve(missAssembly, missType), "a miss must not outlive its lifecycle");
        }

        [Test]
        public void TypeNameResolver_DuplicateSimpleName_WarnsOnlyWithLogAllDefine()
        {
#if (UNITY_6000_5_OR_NEWER && ENABLE_CORECLR)
            var original = typeof(UnityEngine.Vector3).Assembly;
            var path = UnityEngine.AssemblyExtension.GetLoadedAssemblyPath(original);
            var contextType = Type.GetType("System.Runtime.Loader.AssemblyLoadContext");

            TypeNameResolver.Shutdown();
            TypeNameResolver.Initialize();
            m_CollisionContext = Activator.CreateInstance(contextType, "UUM155913", true);
#if ADDRESSABLES_LOG_ALL
            LogAssert.Expect(UnityEngine.LogType.Warning, new Regex("Multiple loaded assemblies share the simple name 'UnityEngine.CoreModule'"));
#endif
            var loadMethod = contextType.GetMethod("LoadFromStream", new[] { typeof(Stream) });
            using (var stream = new MemoryStream(File.ReadAllBytes(path)))
                loadMethod.Invoke(m_CollisionContext, new object[] { stream });

#if !ADDRESSABLES_LOG_ALL
            LogAssert.NoUnexpectedReceived();
#endif
            Assert.AreEqual(typeof(UnityEngine.Vector3), TypeNameResolver.Resolve("UnityEngine.CoreModule", "UnityEngine.Vector3"));
#else
            Assert.Ignore("Assembly path lookup needs Unity 6000.5 or newer with CoreCLR.");
#endif
        }

        [Test]
        public void NormalizeTypeName_CorelibGenericArguments_OmitRuntimeSpecificAssembly()
        {
            var corelibName = typeof(object).Assembly.GetName().Name;
            foreach (var t in new[] { typeof(List<string>), typeof(Dictionary<string, int>), typeof(List<List<string>>) })
            {
                var name = TypeNameResolver.NormalizeTypeName(t);
                Assert.IsFalse(name.Contains(corelibName), $"normalized name '{name}' embeds the writer's corelib identity '{corelibName}'");
                Assert.AreEqual(t, TypeNameResolver.Resolve(TypeNameResolver.GetSimpleAssemblyName(t), name));
            }
        }

        [Test]
        [TestCase("mscorlib")]
        [TestCase("System.Private.CoreLib")]
        public void TypeNameResolver_NullAssembly_ResolvesGenericWithForeignCorelibArgs(string corelib)
        {
            // Legacy catalogs still embed foreign corelib names.
            var typeName = "System.Collections.Generic.List`1[[System.String, " + corelib + "]]";
            Assert.AreEqual(typeof(List<string>), TypeNameResolver.Resolve(null, typeName));
        }
    }
}
