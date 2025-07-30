using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine.AddressableAssets.Initialization;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.AddressableAssets.ResourceProviders;
using UnityEngine.Networking;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.Exceptions;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.ResourceManagement.Util;
using UnityEngine.SceneManagement;

namespace UnityEngine.AddressableAssets
{
    internal class AddressablesImpl : IEqualityComparer<IResourceLocation>
    {
        ResourceManager m_ResourceManager;
        IInstanceProvider m_InstanceProvider;
        int m_CatalogRequestsTimeout;

        internal const string kCacheDataFolder = "{UnityEngine.Application.persistentDataPath}/com.unity.addressables/";

        public IInstanceProvider InstanceProvider
        {
            get { return m_InstanceProvider; }
            set
            {
                m_InstanceProvider = value;
                var rec = m_InstanceProvider as IUpdateReceiver;
                if (rec != null)
                    m_ResourceManager.AddUpdateReceiver(rec);
            }
        }

        public ISceneProvider SceneProvider;

        public ResourceManager ResourceManager
        { get => m_ResourceManager; }

        public int CatalogRequestsTimeout
        {
            get { return m_CatalogRequestsTimeout; }
            set { m_CatalogRequestsTimeout = value; }
        }

        internal List<ResourceLocatorInfo> m_ResourceLocators = new List<ResourceLocatorInfo>();
        AsyncOperationHandle<IResourceLocator> m_InitializationOperation;
        AsyncOperationHandle<List<string>> m_ActiveCheckUpdateOperation;
        internal AsyncOperationHandle<List<IResourceLocator>> m_ActiveUpdateOperation;


        Action<AsyncOperationHandle> m_OnHandleCompleteAction;
        Action<AsyncOperationHandle> m_OnSceneHandleCompleteAction;
        Action<AsyncOperationHandle> m_OnHandleDestroyedAction;
        Dictionary<object, AsyncOperationHandle> m_resultToHandle = new Dictionary<object, AsyncOperationHandle>();
        internal HashSet<AsyncOperationHandle> m_SceneInstances = new HashSet<AsyncOperationHandle>();

        AsyncOperationHandle<bool> m_ActiveCleanBundleCacheOperation;

        internal int ActiveSceneInstances
        {
            get { return m_SceneInstances.Count; }
        }

        internal int TrackedHandleCount
        {
            get { return m_resultToHandle.Count; }
        }

        internal bool hasStartedInitialization = false;

        public AddressablesImpl(IAllocationStrategy alloc)
        {
            m_ResourceManager = new ResourceManager(alloc);
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        internal void ReleaseSceneManagerOperation()
        {
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
        }

        public Func<IResourceLocation, string> InternalIdTransformFunc
        {
            get { return ResourceManager.InternalIdTransformFunc; }
            set { ResourceManager.InternalIdTransformFunc = value; }
        }

        public Action<UnityWebRequest> WebRequestOverride
        {
            get { return ResourceManager.WebRequestOverride; }
            set { ResourceManager.WebRequestOverride = value; }
        }

        public AsyncOperationHandle ChainOperation
        {
            get
            {
                if (!hasStartedInitialization)
                    return InitializeAsync();
                if (m_InitializationOperation.IsValid() && !m_InitializationOperation.IsDone)
                    return m_InitializationOperation;
                if (m_ActiveUpdateOperation.IsValid() && !m_ActiveUpdateOperation.IsDone)
                    return m_ActiveUpdateOperation;
                Debug.LogWarning($"{nameof(ChainOperation)} property should not be accessed unless {nameof(ShouldChainRequest)} is true.");
                return default;
            }
        }

        internal bool ShouldChainRequest
        {
            get
            {
                if (!hasStartedInitialization)
                    return true;

                if (m_InitializationOperation.IsValid() && !m_InitializationOperation.IsDone)
                    return true;

                return m_ActiveUpdateOperation.IsValid() && !m_ActiveUpdateOperation.IsDone;
            }
        }

        internal void OnSceneUnloaded(Scene scene)
        {
            foreach (var s in m_SceneInstances)
            {
                if (!s.IsValid())
                {
                    m_SceneInstances.Remove(s);
                    break;
                }

                var sceneHandle = s.Convert<SceneInstance>();
                if (sceneHandle.Result.Scene == scene)
                {
                    m_SceneInstances.Remove(s);
                    m_resultToHandle.Remove(s.Result);

                    if (sceneHandle.Result.ReleaseSceneOnSceneUnloaded)
                    {
                        var op = SceneProvider.ReleaseScene(m_ResourceManager, sceneHandle);
                        op.ReleaseHandleOnCompletion();
                    }
                    break;
                }
            }

            m_ResourceManager.CleanupSceneInstances(scene);
        }

        public string StreamingAssetsSubFolder
        {
            get { return "aa"; }
        }

        public string BuildPath
        {
            get { return Addressables.LibraryPath + StreamingAssetsSubFolder + "/" + PlatformMappingService.GetPlatformPathSubFolder(); }
        }

        public string PlayerBuildDataPath
        {
            get { return Application.streamingAssetsPath + "/" + StreamingAssetsSubFolder; }
        }

        public string RuntimePath
        {
            get
            {
#if UNITY_EDITOR
                return BuildPath;
#else
                return PlayerBuildDataPath;
#endif
            }
        }

        public void Log(string msg)
        {
            Debug.Log(msg);
        }

        public void LogFormat(string format, params object[] args)
        {
            Debug.LogFormat(format, args);
        }

        public void LogWarning(string msg)
        {
            Debug.LogWarning(msg);
        }

        public void LogWarningFormat(string format, params object[] args)
        {
            Debug.LogWarningFormat(format, args);
        }

        public void LogError(string msg)
        {
            Debug.LogError(msg);
        }

        public void LogException(AsyncOperationHandle op, Exception ex)
        {
            if (op.Status == AsyncOperationStatus.Failed)
            {
                Debug.LogError(ex.ToString());
                Addressables.Log($"Failed op : {op.DebugName}");
            }
            else
                Addressables.Log(ex.ToString());
        }

        public void LogException(Exception ex)
        {
            Addressables.Log(ex.ToString());
        }

        public void LogErrorFormat(string format, params object[] args)
        {
            Debug.LogErrorFormat(format, args);
        }

        public string ResolveInternalId(string id)
        {
            var path = AddressablesRuntimeProperties.EvaluateString(id);
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || UNITY_WSA || UNITY_XBOXONE || UNITY_GAMECORE || UNITY_PS5 || UNITY_PS4
            if (path.Length >= 260 && path.StartsWith(Application.dataPath, StringComparison.Ordinal))
                path = path.Substring(Application.dataPath.Length + 1);
#endif
            return path;
        }

        public IEnumerable<IResourceLocator> ResourceLocators
        {
            get { return m_ResourceLocators.Select(l => l.Locator); }
        }

        public void AddResourceLocator(IResourceLocator loc, string localCatalogHash = null, IResourceLocation remoteCatalogLocation = null)
        {
            m_ResourceLocators.Add(new ResourceLocatorInfo(loc, localCatalogHash, remoteCatalogLocation));
        }

        public void RemoveResourceLocator(IResourceLocator loc)
        {
            m_ResourceLocators.RemoveAll(l => l.Locator == loc);
        }

        public void ClearResourceLocators()
        {
            m_ResourceLocators.Clear();
        }

        internal bool GetResourceLocations(object key, Type type, out IList<IResourceLocation> locations)
        {
            if (type == null && (key is AssetReference))
                type = (key as AssetReference).SubObjectType;

            key = EvaluateKey(key);

            locations = null;
            HashSet<IResourceLocation> current = null;
            foreach (var locatorInfo in m_ResourceLocators)
            {
                var locator = locatorInfo.Locator;
                IList<IResourceLocation> locs;
                if (locator.Locate(key, type, out locs))
                {
                    if (locations == null)
                    {
                        //simple, common case, no allocations
                        locations = locs;
                    }
                    else
                    {
                        //less common, need to merge...
                        if (current == null)
                        {
                            current = new HashSet<IResourceLocation>();
                            foreach (var loc in locations)
                                current.Add(loc);
                        }

                        current.UnionWith(locs);
                    }
                }
            }

            if (current == null)
                return locations != null;

            locations = new List<IResourceLocation>(current);
            return true;
        }

        internal bool GetResourceLocations(IEnumerable keys, Type type, Addressables.MergeMode merge, out IList<IResourceLocation> locations)
        {
            locations = null;
            HashSet<IResourceLocation> current = null;
            foreach (var key in keys)
            {
                IList<IResourceLocation> locs;
                if (GetResourceLocations(key, type, out locs))
                {
                    if (locations == null)
                    {
                        locations = locs;
                        if (merge == Addressables.MergeMode.UseFirst)
                            return true;
                    }
                    else
                    {
                        if (current == null)
                        {
                            current = new HashSet<IResourceLocation>(locations, this);
                        }

                        if (merge == Addressables.MergeMode.Intersection)
                            current.IntersectWith(locs);
                        else if (merge == Addressables.MergeMode.Union)
                            current.UnionWith(locs);
                    }
                }
                else
                {
                    //if entries for a key are not found, the intersection is empty
                    if (merge == Addressables.MergeMode.Intersection)
                    {
                        locations = null;
                        return false;
                    }
                }
            }

            if (current == null)
                return locations != null;
            if (current.Count == 0)
            {
                locations = null;
                return false;
            }

            locations = new List<IResourceLocation>(current);
            return true;
        }

        public AsyncOperationHandle<IResourceLocator> InitializeAsync(string runtimeDataPath, string providerSuffix = null, bool autoReleaseHandle = true)
        {
            if (hasStartedInitialization)
            {
                if (m_InitializationOperation.IsValid())
                    return m_InitializationOperation;
                var completedOperation = ResourceManager.CreateCompletedOperation(m_ResourceLocators[0].Locator, errorMsg: null);
                if (autoReleaseHandle)
                    completedOperation.ReleaseHandleOnCompletion();
                return completedOperation;
            }

            if (ResourceManager.ExceptionHandler == null)
            {
                ResourceManager.ExceptionHandler = LogException;
            }

            hasStartedInitialization = true;
            if (m_InitializationOperation.IsValid())
                return m_InitializationOperation;
            //these need to be referenced in order to prevent stripping on IL2CPP platforms.
            GC.KeepAlive(Application.streamingAssetsPath);
#if !UNITY_SWITCH
            GC.KeepAlive(Application.persistentDataPath);
#endif
            if (string.IsNullOrEmpty(runtimeDataPath))
                return ResourceManager.CreateCompletedOperation<IResourceLocator>(null, string.Format("Invalid Key: {0}", runtimeDataPath));

            m_OnHandleCompleteAction = OnHandleCompleted;
            m_OnSceneHandleCompleteAction = OnSceneHandleCompleted;
            m_OnHandleDestroyedAction = OnHandleDestroyed;

#if UNITY_EDITOR
            Object settingsObject = null;
            string settingsPath = null;
            //this indicates that a specific addressables settings asset is being used for the runtime locations
            if (runtimeDataPath.StartsWith("GUID:", StringComparison.Ordinal))
                settingsPath = UnityEditor.AssetDatabase.GUIDToAssetPath(runtimeDataPath.Substring(runtimeDataPath.IndexOf(':') + 1));

            var assembly = Assembly.Load("Unity.Addressables.Editor");
            if (string.IsNullOrEmpty(settingsPath) && !UnityEditor.EditorApplication.isPlaying)
            {
                var rtp = runtimeDataPath.StartsWith("file://", StringComparison.Ordinal) ? runtimeDataPath.Substring("file://".Length) : runtimeDataPath;
                if (!File.Exists(rtp))
                {
                    var defaultSettingsObjectType = assembly.GetType("UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject");
                    var prop = defaultSettingsObjectType.GetProperty("DefaultAssetPath", BindingFlags.Public | BindingFlags.Static);
                    settingsPath = prop.GetValue(null) as string;
                    UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
                }
            }

            if (!string.IsNullOrEmpty(settingsPath))
            {
                var settingsType = assembly.GetType("UnityEditor.AddressableAssets.Settings.AddressableAssetSettings");
                settingsObject = UnityEditor.AssetDatabase.LoadAssetAtPath(settingsPath, settingsType);
                if (settingsObject != null)
                {
                    var settingsSetupMethod = settingsType.GetMethod("CreatePlayModeInitializationOperation", BindingFlags.Instance | BindingFlags.NonPublic);
                    m_InitializationOperation = (AsyncOperationHandle<IResourceLocator>)settingsSetupMethod.Invoke(settingsObject, new object[] {this});
                }
            }
#endif
            if (!m_InitializationOperation.IsValid())
                m_InitializationOperation = Initialization.InitializationOperation.CreateInitializationOperation(this, runtimeDataPath, providerSuffix);
            if (autoReleaseHandle)
                m_InitializationOperation.ReleaseHandleOnCompletion();

            return m_InitializationOperation;
        }

        public AsyncOperationHandle<IResourceLocator> InitializeAsync()
        {
            var settingsPath =
#if UNITY_EDITOR
                PlayerPrefs.GetString(Addressables.kAddressablesRuntimeDataPath, RuntimePath + "/settings.json");
#else
                RuntimePath + "/settings.json";
#endif

            return InitializeAsync(ResolveInternalId(settingsPath));
        }

        public AsyncOperationHandle<IResourceLocator> InitializeAsync(bool autoReleaseHandle)
        {
            var settingsPath =
#if UNITY_EDITOR
                PlayerPrefs.GetString(Addressables.kAddressablesRuntimeDataPath, RuntimePath + "/settings.json");
#else
                RuntimePath + "/settings.json";
#endif

            return InitializeAsync(ResolveInternalId(settingsPath), null, autoReleaseHandle);
        }

        public ResourceLocationBase CreateCatalogLocationWithHashDependencies<T>(IResourceLocation catalogLocation) where T : IResourceProvider
        {
            return CreateCatalogLocationWithHashDependencies<T>(catalogLocation.InternalId);
        }

        public ResourceLocationBase CreateCatalogLocationWithHashDependencies<T>(string catalogLocation) where T : IResourceProvider
        {
#if ENABLE_JSON_CATALOG
            string hashFilePath = catalogLocation.Replace(".json", ".hash");
#else
            string hashFilePath = catalogLocation.Replace(".bin", ".hash");
#endif
            return CreateCatalogLocationWithHashDependencies<T>(catalogLocation, hashFilePath);
        }

        public ResourceLocationBase CreateCatalogLocationWithHashDependencies<T>(string catalogPath, string hashFilePath) where T : IResourceProvider
        {
            var catalogLoc = new ResourceLocationBase(catalogPath, catalogPath, typeof(T).FullName, typeof(IResourceLocator))
            {
                Data = new ProviderLoadRequestOptions()
                {
                    IgnoreFailures = false,
                    WebRequestTimeout = CatalogRequestsTimeout
                }
            };

            if (!string.IsNullOrEmpty(hashFilePath))
            {
                ProviderLoadRequestOptions hashOptions = new ProviderLoadRequestOptions()
                {
                    IgnoreFailures = true,
                    WebRequestTimeout = CatalogRequestsTimeout
                };

                string tmpPath = hashFilePath;
                if (ResourceManagerConfig.IsPathRemote(hashFilePath))
                {
                    tmpPath = ResourceManagerConfig.StripQueryParameters(hashFilePath);
                }
                // remote hash file location
                var hashResourceLocation = new ResourceLocationBase(hashFilePath, hashFilePath, typeof(TextDataProvider).FullName, typeof(string))
                {
                    Data = hashOptions.Copy()
                };
                catalogLoc.Dependencies.Add(hashResourceLocation);

#if UNITY_SWITCH
                string cacheHashFilePath = hashFilePath; // ResourceLocationBase does not allow empty string id
#else
                // The file name of the local cached catalog + hash file is the hash code of the remote hash path, without query parameters (if any).
                string cacheHashFilePath = ResolveInternalId(kCacheDataFolder + tmpPath.GetHashCode() + ".hash");
#endif
                // cached hash file location
                var cacheResourceLocation = new ResourceLocationBase(cacheHashFilePath, cacheHashFilePath, typeof(TextDataProvider).FullName, typeof(string))
                {
                    Data = hashOptions.Copy()
                };
                catalogLoc.Dependencies.Add(cacheResourceLocation);

                // local hash file location
                //If you're explicitly loading a content catalog, we'll just set the "local" filepath to be the same as the "cached" one
                catalogLoc.Dependencies.Add(cacheResourceLocation);
            }

            return catalogLoc;
        }

        [Conditional("UNITY_EDITOR")]
        void QueueEditorUpdateIfNeeded()
        {
#if UNITY_EDITOR
            if (!UnityEditor.EditorApplication.isPlaying)
                UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
#endif
        }

        public AsyncOperationHandle<IResourceLocator> LoadContentCatalogAsync(string catalogPath, bool autoReleaseHandle = true, string providerSuffix = null)
        {
            var catalogLoc = CreateCatalogLocationWithHashDependencies<ContentCatalogProvider>(catalogPath);
            if (ShouldChainRequest)
                return ResourceManager.CreateChainOperation(ChainOperation, op => LoadContentCatalogAsync(catalogPath, autoReleaseHandle, providerSuffix));
            var handle = Initialization.InitializationOperation.LoadContentCatalog(this, catalogLoc, providerSuffix);
            if (autoReleaseHandle)
                handle.ReleaseHandleOnCompletion();
            QueueEditorUpdateIfNeeded();
            return handle;
        }

        AsyncOperationHandle<SceneInstance> TrackHandle(AsyncOperationHandle<SceneInstance> handle)
        {
            handle.Completed += (sceneHandle) => { m_OnSceneHandleCompleteAction(sceneHandle); };
            return handle;
        }

        AsyncOperationHandle<TObject> TrackHandle<TObject>(AsyncOperationHandle<TObject> handle)
        {
            handle.CompletedTypeless += m_OnHandleCompleteAction;
            return handle;
        }

        AsyncOperationHandle TrackHandle(AsyncOperationHandle handle)
        {
            handle.Completed += m_OnHandleCompleteAction;
            return handle;
        }

        internal void ClearTrackHandles()
        {
            m_resultToHandle.Clear();
        }

        public AsyncOperationHandle<TObject> LoadAssetAsync<TObject>(IResourceLocation location)
        {
            QueueEditorUpdateIfNeeded();
            if (ShouldChainRequest)
                return TrackHandle(LoadAssetWithChain<TObject>(ChainOperation, location));
            return TrackHandle(ResourceManager.ProvideResource<TObject>(location));
        }

        AsyncOperationHandle<TObject> LoadAssetWithChain<TObject>(AsyncOperationHandle dep, IResourceLocation loc)
        {
            return ResourceManager.CreateChainOperation(dep, op => LoadAssetAsync<TObject>(loc));
        }

        AsyncOperationHandle<TObject> LoadAssetWithChain<TObject>(AsyncOperationHandle dep, object key)
        {
            return ResourceManager.CreateChainOperation(dep, op => LoadAssetAsync<TObject>(key));
        }

        public AsyncOperationHandle<TObject> LoadAssetAsync<TObject>(object key)
        {
            QueueEditorUpdateIfNeeded();
            if (ShouldChainRequest)
                return TrackHandle(LoadAssetWithChain<TObject>(ChainOperation, key));

            key = EvaluateKey(key);

            IList<IResourceLocation> locs;
            var t = typeof(TObject);
            if (t.IsArray)
                t = t.GetElementType();
            else if (t.IsGenericType && typeof(IList<>) == t.GetGenericTypeDefinition())
                t = t.GetGenericArguments()[0];
            foreach (var locatorInfo in m_ResourceLocators)
            {
                var locator = locatorInfo.Locator;
                if (locator.Locate(key, t, out locs))
                {
                    foreach (var loc in locs)
                    {
                        var provider = ResourceManager.GetResourceProvider(typeof(TObject), loc);
                        if (provider != null)
                            return TrackHandle(ResourceManager.ProvideResource<TObject>(loc));
                    }
                }
            }

            return ResourceManager.CreateCompletedOperationWithException<TObject>(default(TObject), new InvalidKeyException(key, t, this));
        }

        class LoadResourceLocationKeyOp : AsyncOperationBase<IList<IResourceLocation>>
        {
            object m_Keys;
            IList<IResourceLocation> m_locations;
            AddressablesImpl m_Addressables;
            Type m_ResourceType;

            protected override string DebugName
            {
                get { return m_Keys.ToString(); }
            }

            public void Init(AddressablesImpl aa, Type t, object keys)
            {
                m_Keys = keys;
                m_ResourceType = t;
                m_Addressables = aa;
            }

            /// <inheritdoc />
            protected override bool InvokeWaitForCompletion()
            {
                m_RM?.Update(Time.unscaledDeltaTime);
                if (!HasExecuted)
                    InvokeExecute();
                return true;
            }

            protected override void Execute()
            {
                m_Addressables.GetResourceLocations(m_Keys, m_ResourceType, out m_locations);
                if (m_locations == null)
                    m_locations = new List<IResourceLocation>();
                Complete(m_locations, true, string.Empty);
            }
        }

        class LoadResourceLocationKeysOp : AsyncOperationBase<IList<IResourceLocation>>
        {
            IEnumerable m_Key;
            Addressables.MergeMode m_MergeMode;
            IList<IResourceLocation> m_locations;
            AddressablesImpl m_Addressables;
            Type m_ResourceType;

            protected override string DebugName
            {
                get { return "LoadResourceLocationKeysOp"; }
            }

            public void Init(AddressablesImpl aa, Type t, IEnumerable key, Addressables.MergeMode mergeMode)
            {
                m_Key = key;
                m_ResourceType = t;
                m_MergeMode = mergeMode;
                m_Addressables = aa;
            }

            protected override void Execute()
            {
                m_Addressables.GetResourceLocations(m_Key, m_ResourceType, m_MergeMode, out m_locations);
                if (m_locations == null)
                    m_locations = new List<IResourceLocation>();
                Complete(m_locations, true, string.Empty);
            }

            /// <inheritdoc />
            protected override bool InvokeWaitForCompletion()
            {
                m_RM?.Update(Time.unscaledDeltaTime);
                if (!HasExecuted)
                    InvokeExecute();
                return true;
            }
        }

        public AsyncOperationHandle<IList<IResourceLocation>> LoadResourceLocationsWithChain(AsyncOperationHandle dep, IEnumerable keys, Addressables.MergeMode mode, Type type)
        {
            return ResourceManager.CreateChainOperation(dep, op => LoadResourceLocationsAsync(keys, mode, type));
        }

        public AsyncOperationHandle<IList<IResourceLocation>> LoadResourceLocationsAsync(IEnumerable keys, Addressables.MergeMode mode, Type type = null)
        {
            QueueEditorUpdateIfNeeded();

            if (ShouldChainRequest)
                return TrackHandle(LoadResourceLocationsWithChain(ChainOperation, keys, mode, type));

            var op = new LoadResourceLocationKeysOp();
            op.Init(this, type, keys, mode);
            return TrackHandle(ResourceManager.StartOperation(op, default));
        }

        public AsyncOperationHandle<IList<IResourceLocation>> LoadResourceLocationsWithChain(AsyncOperationHandle dep, object key, Type type)
        {
            return ResourceManager.CreateChainOperation(dep, op => LoadResourceLocationsAsync(key, type));
        }

        public AsyncOperationHandle<IList<IResourceLocation>> LoadResourceLocationsAsync(object key, Type type = null)
        {
            QueueEditorUpdateIfNeeded();

            if (ShouldChainRequest)
                return TrackHandle(LoadResourceLocationsWithChain(ChainOperation, key, type));

            var op = new LoadResourceLocationKeyOp();
            op.Init(this, type, key);
            return TrackHandle(ResourceManager.StartOperation(op, default));
        }

        public AsyncOperationHandle<IList<TObject>> LoadAssetsAsync<TObject>(IList<IResourceLocation> locations, Action<TObject> callback, bool releaseDependenciesOnFailure)
        {
            QueueEditorUpdateIfNeeded();
            if (ShouldChainRequest)
                return TrackHandle(LoadAssetsWithChain(ChainOperation, locations, callback, releaseDependenciesOnFailure));
            return TrackHandle(ResourceManager.ProvideResources(locations, releaseDependenciesOnFailure, callback));
        }

        AsyncOperationHandle<IList<TObject>> LoadAssetsWithChain<TObject>(AsyncOperationHandle dep, IList<IResourceLocation> locations, Action<TObject> callback, bool releaseDependenciesOnFailure)
        {
            return ResourceManager.CreateChainOperation(dep, op => LoadAssetsAsync(locations, callback, releaseDependenciesOnFailure));
        }

        AsyncOperationHandle<IList<TObject>> LoadAssetsWithChain<TObject>(AsyncOperationHandle dep, IEnumerable keys, Action<TObject> callback, Addressables.MergeMode mode,
            bool releaseDependenciesOnFailure)
        {
            return ResourceManager.CreateChainOperation(dep, op => LoadAssetsAsync(keys, callback, mode, releaseDependenciesOnFailure));
        }

        public AsyncOperationHandle<IList<TObject>> LoadAssetsAsync<TObject>(IEnumerable keys, Action<TObject> callback, Addressables.MergeMode mode, bool releaseDependenciesOnFailure)
        {
            QueueEditorUpdateIfNeeded();

            if (keys is string keyAsString)
            {
                Addressables.Log($"The keys parameter is a single string {keyAsString}, but it is being treated as an array of characters. To avoid this, the string will be wrapped in a single element array automatically. If this is not intended, please pass an array of individual characters.");
                keys = new string[] { keyAsString };
            }

            if (ShouldChainRequest)
                return TrackHandle(LoadAssetsWithChain(ChainOperation, keys, callback, mode, releaseDependenciesOnFailure));

            IList<IResourceLocation> locations;
            if (!GetResourceLocations(keys, typeof(TObject), mode, out locations))
                return ResourceManager.CreateCompletedOperationWithException<IList<TObject>>(null, new InvalidKeyException(keys, typeof(TObject), mode, this));

            return LoadAssetsAsync(locations, callback, releaseDependenciesOnFailure);
        }

        AsyncOperationHandle<IList<TObject>> LoadAssetsWithChain<TObject>(AsyncOperationHandle dep, object key, Action<TObject> callback, bool releaseDependenciesOnFailure)
        {
            return ResourceManager.CreateChainOperation(dep, op2 => LoadAssetsAsync(key, callback, releaseDependenciesOnFailure));
        }

        public AsyncOperationHandle<IList<TObject>> LoadAssetsAsync<TObject>(object key, Action<TObject> callback, bool releaseDependenciesOnFailure)
        {
            QueueEditorUpdateIfNeeded();

            if (ShouldChainRequest)
                return TrackHandle(LoadAssetsWithChain(ChainOperation, key, callback, releaseDependenciesOnFailure));

            IList<IResourceLocation> locations;
            if (!GetResourceLocations(key, typeof(TObject), out locations))
                return ResourceManager.CreateCompletedOperationWithException<IList<TObject>>(null, new InvalidKeyException(key, typeof(TObject), this));

            return LoadAssetsAsync(locations, callback, releaseDependenciesOnFailure);
        }

        void OnHandleDestroyed(AsyncOperationHandle handle)
        {
            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                m_resultToHandle.Remove(handle.Result);
            }
        }

        void OnSceneHandleCompleted(AsyncOperationHandle handle)
        {
            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                m_SceneInstances.Add(handle);
                if (m_resultToHandle.TryAdd(handle.Result, handle))
                {
                    handle.Destroyed += m_OnHandleDestroyedAction;
                }
            }
        }

        void OnHandleCompleted(AsyncOperationHandle handle)
        {
            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                if (m_resultToHandle.TryAdd(handle.Result, handle))
                {
                    handle.Destroyed += m_OnHandleDestroyedAction;
                }
            }
        }

        public void Release<TObject>(TObject obj)
        {
            if (obj == null)
            {
                LogWarning("Addressables.Release() - trying to release null object.");
                return;
            }

            AsyncOperationHandle handle;
            if (m_resultToHandle.TryGetValue(obj, out handle))
                handle.Release();
            else
            {
                LogError("Addressables.Release was called on an object that Addressables was not previously aware of.  Thus nothing is being released");
            }
        }

        public void Release<TObject>(AsyncOperationHandle<TObject> handle)
        {
            m_ResourceManager.Release(handle);
        }

        public void Release(AsyncOperationHandle handle)
        {
            m_ResourceManager.Release(handle);
        }

        AsyncOperationHandle<long> GetDownloadSizeWithChain(AsyncOperationHandle dep, object key)
        {
            return ResourceManager.CreateChainOperation(dep, op => GetDownloadSizeAsync(key));
        }

        AsyncOperationHandle<long> ComputeCatalogSizeWithChain(IResourceLocation catalogLoc)
        {
            if (!catalogLoc.HasDependencies)
                return ResourceManager.CreateCompletedOperation<long>(0, "Attempting to get the remote header size of a content catalog, " +
                                                                         $"but no dependencies pointing to a remote location could be found for location {catalogLoc.InternalId}. Catalog location dependencies can " +
                                                                         $"be setup using CreateCatalogLocationWithHashDependencies");

            AsyncOperationHandle dep = ResourceManager.ProvideResource<string>(catalogLoc.Dependencies[(int)ContentCatalogProvider.DependencyHashIndex.Remote]);
            return ResourceManager.CreateChainOperation(dep, op =>
            {
                try
                {
                    var remoteHash = Hash128.Parse(op.Result.ToString());
                    if (!IsCatalogCached(catalogLoc, remoteHash))
                        return GetRemoteCatalogHeaderSize(catalogLoc);
                }
                catch (Exception e)
                {
                    return ResourceManager.CreateCompletedOperation<long>(0, $"Fetching the remote catalog size failed. {e}");
                }

                return ResourceManager.CreateCompletedOperation<long>(0, string.Empty);
            });
        }

        internal bool IsCatalogCached(IResourceLocation catalogLoc, Hash128 remoteHash)
        {
            //If local hash exists, check if they're equal or not
            if (!catalogLoc.HasDependencies
                || catalogLoc.Dependencies.Count != 2
                || !File.Exists(catalogLoc.Dependencies[(int)ContentCatalogProvider.DependencyHashIndex.Cache].InternalId)
                || remoteHash != Hash128.Parse(File.ReadAllText(catalogLoc.Dependencies[(int)ContentCatalogProvider.DependencyHashIndex.Cache].InternalId)))
                return false;

            return true;
        }

        internal AsyncOperationHandle<long> GetRemoteCatalogHeaderSize(IResourceLocation catalogLoc)
        {
            if (!catalogLoc.HasDependencies)
                return ResourceManager.CreateCompletedOperation<long>(0, "Attempting to get the remote header size of a content catalog, " +
                                                                         $"but no dependencies pointing to a remote location could be found for location {catalogLoc.InternalId}. Catalog location dependencies can " +
                                                                         $"be setup using CreateCatalogLocationWithHashDependencies");

            var internalIdHash = catalogLoc.Dependencies[(int)ContentCatalogProvider.DependencyHashIndex.Remote].InternalId;
#if ENABLE_JSON_CATALOG
            var internalIdCat = internalIdHash.Replace(".hash", ".json");
#else
            var internalIdCat = internalIdHash.Replace(".hash", ".bin");
#endif
            var uwr = new UnityWebRequest(internalIdCat, UnityWebRequest.kHttpVerbHEAD);
            AsyncOperationBase<UnityWebRequest> uwrAsyncOp = new UnityWebRequestOperation(uwr);

            return ResourceManager.CreateChainOperation(ResourceManager.StartOperation(uwrAsyncOp, default(AsyncOperationHandle))
                , getOp =>
                {
                    var response = (getOp.Result as UnityWebRequest)?.GetResponseHeader("Content-Length");
                    if (response != null && long.TryParse(response, out long result))
                        return ResourceManager.CreateCompletedOperation(result, "");
                    else
                        return ResourceManager.CreateCompletedOperation<long>(0, "Attempting to get the remote header of a catalog failed.");
                });
        }

        AsyncOperationHandle<long> GetDownloadSizeWithChain(AsyncOperationHandle dep, IEnumerable keys)
        {
            return ResourceManager.CreateChainOperation(dep, op => GetDownloadSizeAsync(keys));
        }

        public AsyncOperationHandle<long> GetDownloadSizeAsync(object key)
        {
            QueueEditorUpdateIfNeeded();

            return GetDownloadSizeAsync(new object[] {key});
        }

        public AsyncOperationHandle<long> GetDownloadSizeAsync(IEnumerable keys)
        {
            QueueEditorUpdateIfNeeded();

            if (ShouldChainRequest)
                return TrackHandle(GetDownloadSizeWithChain(ChainOperation, keys));

            List<IResourceLocation> allLocations = new List<IResourceLocation>();
            foreach (object key in keys)
            {
                IList<IResourceLocation> locations;
                if (key is IList<IResourceLocation>)
                    locations = key as IList<IResourceLocation>;
                else if (key is IResourceLocation)
                {
                    foreach (var locator in m_ResourceLocators)
                    {
                        if (locator.CatalogLocation == key as IResourceLocation)
                            return ComputeCatalogSizeWithChain(key as IResourceLocation);
                    }

                    locations = new List<IResourceLocation>(1)
                    {
                        key as IResourceLocation
                    };
                }
                else if (!GetResourceLocations(key, typeof(object), out locations))
                    return ResourceManager.CreateCompletedOperationWithException<long>(0, new InvalidKeyException(key, typeof(object), this));

                foreach (var loc in locations)
                {
                    if (loc.HasDependencies)
                        allLocations.AddRange(loc.Dependencies);
                }
            }

            GetDownloadSizeOperation downloadSizeOp = new GetDownloadSizeOperation();
            downloadSizeOp.Init(allLocations.Distinct(new ResourceLocationComparer()), ResourceManager);
            return ResourceManager.StartOperation(downloadSizeOp, default);
        }

        AsyncOperationHandle DownloadDependenciesAsyncWithChain(AsyncOperationHandle dep, object key, bool autoReleaseHandle)
        {
            var handle = ResourceManager.CreateChainOperation(dep, op => DownloadDependenciesAsync(key).Convert<IList<IAssetBundleResource>>());
            if (autoReleaseHandle)
                handle.ReleaseHandleOnCompletion();
            return handle;
        }

        internal static void WrapAsDownloadLocations(List<IResourceLocation> locations)
        {
            for (int i = 0; i < locations.Count; i++)
                locations[i] = new DownloadOnlyLocation(locations[i]);
        }

        static List<IResourceLocation> GatherDependenciesFromLocations(IList<IResourceLocation> locations)
        {
            var locHash = new HashSet<IResourceLocation>(new ResourceLocationComparer());
            foreach (var loc in locations)
            {
                if (loc.ResourceType == typeof(IAssetBundleResource))
                {
                    locHash.Add(loc);
                }

                if (loc.HasDependencies)
                {
                    foreach (var dep in loc.Dependencies)
                        if (dep.ResourceType == typeof(IAssetBundleResource))
                            locHash.Add(dep);
                }
            }

            return new List<IResourceLocation>(locHash);
        }

        public AsyncOperationHandle DownloadDependenciesAsync(object key, bool autoReleaseHandle = false)
        {
            QueueEditorUpdateIfNeeded();

            if (ShouldChainRequest)
                return DownloadDependenciesAsyncWithChain(ChainOperation, key, autoReleaseHandle);

            IList<IResourceLocation> locations;
            if (!GetResourceLocations(key, typeof(object), out locations))
            {
                var handle = ResourceManager.CreateCompletedOperationWithException<IList<IAssetBundleResource>>(null, new InvalidKeyException(key, typeof(object), this));
                if (autoReleaseHandle)
                    handle.ReleaseHandleOnCompletion();
                return handle;
            }
            else
            {
                List<IResourceLocation> dlLocations = GatherDependenciesFromLocations(locations);
                WrapAsDownloadLocations(dlLocations);
                var handle = LoadAssetsAsync<IAssetBundleResource>(dlLocations, null, true);
                if (autoReleaseHandle)
                    handle.ReleaseHandleOnCompletion();
                return handle;
            }
        }

        AsyncOperationHandle DownloadDependenciesAsyncWithChain(AsyncOperationHandle dep, IList<IResourceLocation> locations, bool autoReleaseHandle)
        {
            var handle = ResourceManager.CreateChainOperation(dep, op => DownloadDependenciesAsync(locations).Convert<IList<IAssetBundleResource>>());
            if (autoReleaseHandle)
                handle.ReleaseHandleOnCompletion();
            return handle;
        }

        public AsyncOperationHandle DownloadDependenciesAsync(IList<IResourceLocation> locations, bool autoReleaseHandle = false)
        {
            QueueEditorUpdateIfNeeded();
            if (ShouldChainRequest)
                return DownloadDependenciesAsyncWithChain(ChainOperation, locations, autoReleaseHandle);

            List<IResourceLocation> dlLocations = GatherDependenciesFromLocations(locations);
            WrapAsDownloadLocations(dlLocations);
            var handle = LoadAssetsAsync<IAssetBundleResource>(dlLocations, null, true);
            if (autoReleaseHandle)
                handle.ReleaseHandleOnCompletion();
            return handle;
        }

        AsyncOperationHandle DownloadDependenciesAsyncWithChain(AsyncOperationHandle dep, IEnumerable keys, Addressables.MergeMode mode, bool autoReleaseHandle)
        {
            var handle = ResourceManager.CreateChainOperation(dep, op => DownloadDependenciesAsync(keys, mode).Convert<IList<IAssetBundleResource>>());
            if (autoReleaseHandle)
                handle.ReleaseHandleOnCompletion();
            return handle;
        }

        public AsyncOperationHandle DownloadDependenciesAsync(IEnumerable keys, Addressables.MergeMode mode, bool autoReleaseHandle = false)
        {
            QueueEditorUpdateIfNeeded();
            if (ShouldChainRequest)
                return DownloadDependenciesAsyncWithChain(ChainOperation, keys, mode, autoReleaseHandle);

            IList<IResourceLocation> locations;
            if (!GetResourceLocations(keys, typeof(object), mode, out locations))
            {
                var handle = ResourceManager.CreateCompletedOperationWithException<IList<IAssetBundleResource>>(null, new InvalidKeyException(keys, typeof(object), mode, this));
                if (autoReleaseHandle)
                    handle.ReleaseHandleOnCompletion();
                return handle;
            }
            else
            {
                List<IResourceLocation> dlLocations = GatherDependenciesFromLocations(locations);
                WrapAsDownloadLocations(dlLocations);
                var handle = LoadAssetsAsync<IAssetBundleResource>(dlLocations, null, true);
                if (autoReleaseHandle)
                    handle.ReleaseHandleOnCompletion();
                return handle;
            }
        }

        internal bool ClearDependencyCacheForKey(object key)
        {
            bool result = true;
#if ENABLE_CACHING
            IList<IResourceLocation> bundleLocations = null;

            if (key is IResourceLocation && (key as IResourceLocation).HasDependencies)
            {
                bundleLocations = GatherDependenciesFromLocations((key as IResourceLocation).Dependencies);
            }
            else if (GetResourceLocations(key, typeof(object), out IList<IResourceLocation> locations))
            {
                bundleLocations = GatherDependenciesFromLocations(locations);
            }

            if (bundleLocations != null)
            {
                foreach (var dep in bundleLocations)
                {
                    AssetBundleRequestOptions bundleData = dep.Data as AssetBundleRequestOptions;
                    //This should never be false when we get here, if it is there's likely a deeper problem.
                    if (bundleData != null)
                    {
                        string bundleName = bundleData.BundleName;
                        var cachedBundleOp = m_ResourceManager.GetOperationFromCache(dep, typeof(IAssetBundleResource));
                        if (cachedBundleOp != null)
                        {
                            Debug.LogWarning($"Attempting to clear cached version including {bundleName}, while {bundleName} is currently loaded.");
                            if (!string.IsNullOrEmpty(bundleData.Hash))
                            {
                                Hash128 currentVersion = Hash128.Parse(bundleData.Hash);
                                Caching.ClearOtherCachedVersions(bundleName, currentVersion);
                            }
                        }
                        else
                            result = result && Caching.ClearAllCachedVersions(bundleName);
                    }
                }
            }
#endif
            return result;
        }

        internal void AutoReleaseHandleOnTypelessCompletion<TObject>(AsyncOperationHandle<TObject> handle)
        {
            handle.CompletedTypeless += op => op.Release();
        }

        public AsyncOperationHandle<bool> ClearDependencyCacheAsync(object key, bool autoReleaseHandle)
        {
            QueueEditorUpdateIfNeeded();
            if (ShouldChainRequest)
            {
                var chainOp = ResourceManager.CreateChainOperation(ChainOperation,
                    op => ClearDependencyCacheAsync(key, autoReleaseHandle));
                if (autoReleaseHandle)
                    chainOp.ReleaseHandleOnCompletion();
                return chainOp;
            }

            bool result = ClearDependencyCacheForKey(key);

            var completedOp = ResourceManager.CreateCompletedOperation(result, result ? String.Empty : "Unable to clear the cache.  AssetBundle's may still be loaded for the given key.");
            if (autoReleaseHandle)
                completedOp.ReleaseHandleOnCompletion();
            return completedOp;
        }

        public AsyncOperationHandle<bool> ClearDependencyCacheAsync(IList<IResourceLocation> locations, bool autoReleaseHandle)
        {
            QueueEditorUpdateIfNeeded();
            if (ShouldChainRequest)
            {
                var chainOp = ResourceManager.CreateChainOperation(ChainOperation,
                    op => ClearDependencyCacheAsync(locations, autoReleaseHandle));
                if (autoReleaseHandle)
                    chainOp.ReleaseHandleOnCompletion();
                return chainOp;
            }

            bool result = true;
            foreach (var location in locations)
                result = result && ClearDependencyCacheForKey(location);

            var completedOp = ResourceManager.CreateCompletedOperation(result, result ? String.Empty : "Unable to clear the cache.  AssetBundle's may still be loaded for the given key(s).");
            if (autoReleaseHandle)
                completedOp.ReleaseHandleOnCompletion();
            return completedOp;
        }

        public AsyncOperationHandle<bool> ClearDependencyCacheAsync(IEnumerable keys, bool autoReleaseHandle)
        {
            QueueEditorUpdateIfNeeded();
            if (ShouldChainRequest)
            {
                var chainOp = ResourceManager.CreateChainOperation(ChainOperation,
                    op => ClearDependencyCacheAsync(keys, autoReleaseHandle));
                if (autoReleaseHandle)
                    chainOp.ReleaseHandleOnCompletion();
                return chainOp;
            }

            bool result = true;
            foreach (var key in keys)
                result = result && ClearDependencyCacheForKey(key);

            var completedOp = ResourceManager.CreateCompletedOperation(result, result ? String.Empty : "Unable to clear the cache.  AssetBundle's may still be loaded for the given key(s).");
            if (autoReleaseHandle)
                completedOp.ReleaseHandleOnCompletion();
            return completedOp;
        }

        public AsyncOperationHandle<GameObject> InstantiateAsync(IResourceLocation location, Transform parent = null, bool instantiateInWorldSpace = false, bool trackHandle = true)
        {
            return InstantiateAsync(location, new InstantiationParameters(parent, instantiateInWorldSpace), trackHandle);
        }

        public AsyncOperationHandle<GameObject> InstantiateAsync(IResourceLocation location, Vector3 position, Quaternion rotation, Transform parent = null, bool trackHandle = true)
        {
            return InstantiateAsync(location, new InstantiationParameters(position, rotation, parent), trackHandle);
        }

        public AsyncOperationHandle<GameObject> InstantiateAsync(object key, Transform parent = null, bool instantiateInWorldSpace = false, bool trackHandle = true)
        {
            return InstantiateAsync(key, new InstantiationParameters(parent, instantiateInWorldSpace), trackHandle);
        }

        public AsyncOperationHandle<GameObject> InstantiateAsync(object key, Vector3 position, Quaternion rotation, Transform parent = null, bool trackHandle = true)
        {
            return InstantiateAsync(key, new InstantiationParameters(position, rotation, parent), trackHandle);
        }

        AsyncOperationHandle<GameObject> InstantiateWithChain(AsyncOperationHandle dep, object key, InstantiationParameters instantiateParameters, bool trackHandle = true)
        {
            var chainOp = ResourceManager.CreateChainOperation(dep, op => InstantiateAsync(key, instantiateParameters, false));
            if (trackHandle)
                chainOp.CompletedTypeless += m_OnHandleCompleteAction;
            return chainOp;
        }

        public AsyncOperationHandle<GameObject> InstantiateAsync(object key, InstantiationParameters instantiateParameters, bool trackHandle = true)
        {
            QueueEditorUpdateIfNeeded();
            if (ShouldChainRequest)
                return InstantiateWithChain(ChainOperation, key, instantiateParameters, trackHandle);

            key = EvaluateKey(key);
            IList<IResourceLocation> locs;
            foreach (var locatorInfo in m_ResourceLocators)
            {
                var locator = locatorInfo.Locator;
                if (locator.Locate(key, typeof(GameObject), out locs))
                    return InstantiateAsync(locs[0], instantiateParameters, trackHandle);
            }

            return ResourceManager.CreateCompletedOperationWithException<GameObject>(null, new InvalidKeyException(key, typeof(GameObject), this));
        }

        AsyncOperationHandle<GameObject> InstantiateWithChain(AsyncOperationHandle dep, IResourceLocation location, InstantiationParameters instantiateParameters, bool trackHandle = true)
        {
            var chainOp = ResourceManager.CreateChainOperation(dep, op => InstantiateAsync(location, instantiateParameters, false));
            if (trackHandle)
                chainOp.CompletedTypeless += m_OnHandleCompleteAction;
            return chainOp;
        }

        public AsyncOperationHandle<GameObject> InstantiateAsync(IResourceLocation location, InstantiationParameters instantiateParameters, bool trackHandle = true)
        {
            QueueEditorUpdateIfNeeded();
            if (ShouldChainRequest)
                return InstantiateWithChain(ChainOperation, location, instantiateParameters, trackHandle);

            var opHandle = ResourceManager.ProvideInstance(InstanceProvider, location, instantiateParameters);
            if (!trackHandle)
                return opHandle;
            opHandle.CompletedTypeless += m_OnHandleCompleteAction;
            return opHandle;
        }

        public bool ReleaseInstance(GameObject instance)
        {
            if (instance == null)
            {
                LogWarning("Addressables.ReleaseInstance() - trying to release null object.");
                return false;
            }

            AsyncOperationHandle handle;
            if (m_resultToHandle.TryGetValue(instance, out handle))
                handle.Release();
            else
                return false;

            return true;
        }

        internal AsyncOperationHandle<SceneInstance> LoadSceneWithChain(AsyncOperationHandle dep, object key, LoadSceneParameters loadSceneParameters,
            SceneReleaseMode releaseMode = SceneReleaseMode.ReleaseSceneWhenSceneUnloaded, bool activateOnLoad = true, int priority = 100)
        {
            return TrackHandle(ResourceManager.CreateChainOperation(dep, op => LoadSceneAsync(key, loadSceneParameters, releaseMode, activateOnLoad, priority, false)));
        }

        internal AsyncOperationHandle<SceneInstance> LoadSceneWithChain(AsyncOperationHandle dep, IResourceLocation key, LoadSceneParameters loadSceneParameters,
            SceneReleaseMode releaseMode = SceneReleaseMode.ReleaseSceneWhenSceneUnloaded, bool activateOnLoad = true, int priority = 100)
        {
            return TrackHandle(ResourceManager.CreateChainOperation(dep, op => LoadSceneAsync(key, loadSceneParameters, releaseMode, activateOnLoad, priority, false)));
        }

        public AsyncOperationHandle<SceneInstance> LoadSceneAsync(object key,
            LoadSceneParameters loadSceneParameters, SceneReleaseMode releaseMode = SceneReleaseMode.ReleaseSceneWhenSceneUnloaded,
            bool activateOnLoad = true, int priority = 100, bool trackHandle = true)
        {
            QueueEditorUpdateIfNeeded();
            if (ShouldChainRequest)
                return LoadSceneWithChain(ChainOperation, key, loadSceneParameters, releaseMode, activateOnLoad, priority);

            IList<IResourceLocation> locations;
            if (!GetResourceLocations(key, typeof(SceneInstance), out locations))
                return ResourceManager.CreateCompletedOperationWithException<SceneInstance>(default(SceneInstance), new InvalidKeyException(key, typeof(SceneInstance), this));

            return LoadSceneAsync(locations[0], loadSceneParameters, releaseMode, activateOnLoad, priority, trackHandle);
        }

        public AsyncOperationHandle<SceneInstance> LoadSceneAsync(IResourceLocation location,
            LoadSceneParameters loadSceneParameters, SceneReleaseMode releaseMode = SceneReleaseMode.ReleaseSceneWhenSceneUnloaded,
            bool activateOnLoad = true, int priority = 100, bool trackHandle = true)
        {
            QueueEditorUpdateIfNeeded();
            if (ShouldChainRequest)
                return LoadSceneWithChain(ChainOperation, location, loadSceneParameters, releaseMode, activateOnLoad, priority);

            var handle = ResourceManager.ProvideScene(SceneProvider, location, loadSceneParameters, releaseMode, activateOnLoad, priority);
            if (trackHandle)
                return TrackHandle(handle);

            return handle;
        }

        public AsyncOperationHandle<SceneInstance> UnloadSceneAsync(SceneInstance scene, UnloadSceneOptions unloadOptions = UnloadSceneOptions.None, bool autoReleaseHandle = true)
        {
            AsyncOperationHandle handle;
            if (!m_resultToHandle.TryGetValue(scene, out handle))
            {
                var msg = string.Format("Addressables.UnloadSceneAsync() - Cannot find handle for scene {0}", scene);
                LogWarning(msg);
                return ResourceManager.CreateCompletedOperation<SceneInstance>(scene, msg);
            }

            if (handle.m_InternalOp.IsRunning)
                return CreateUnloadSceneWithChain(handle, unloadOptions, autoReleaseHandle);

            return UnloadSceneAsync(handle, unloadOptions, autoReleaseHandle);
        }

        public AsyncOperationHandle<SceneInstance> UnloadSceneAsync(AsyncOperationHandle handle, UnloadSceneOptions unloadOptions = UnloadSceneOptions.None, bool autoReleaseHandle = true)
        {
            QueueEditorUpdateIfNeeded();
            if (handle.m_InternalOp.IsRunning)
                return CreateUnloadSceneWithChain(handle, unloadOptions, autoReleaseHandle);

            return UnloadSceneAsync(handle.Convert<SceneInstance>(), unloadOptions, autoReleaseHandle);
        }

        public AsyncOperationHandle<SceneInstance> UnloadSceneAsync(AsyncOperationHandle<SceneInstance> handle, UnloadSceneOptions unloadOptions = UnloadSceneOptions.None,
            bool autoReleaseHandle = true)
        {
            if (handle.m_InternalOp.IsRunning)
                return CreateUnloadSceneWithChain(handle, unloadOptions, autoReleaseHandle);

            return InternalUnloadScene(handle, unloadOptions, autoReleaseHandle);
        }

        internal AsyncOperationHandle<SceneInstance> CreateUnloadSceneWithChain(AsyncOperationHandle handle, UnloadSceneOptions unloadOptions, bool autoReleaseHandle)
        {
            return m_ResourceManager.CreateChainOperation(handle, (completedHandle) => InternalUnloadScene(completedHandle.Convert<SceneInstance>(), unloadOptions, autoReleaseHandle));
        }

        internal AsyncOperationHandle<SceneInstance> CreateUnloadSceneWithChain(AsyncOperationHandle<SceneInstance> handle, UnloadSceneOptions unloadOptions, bool autoReleaseHandle)
        {
            return m_ResourceManager.CreateChainOperation(handle, (completedHandle) => InternalUnloadScene(completedHandle, unloadOptions, autoReleaseHandle));
        }

        internal AsyncOperationHandle<SceneInstance> InternalUnloadScene(AsyncOperationHandle<SceneInstance> handle, UnloadSceneOptions unloadOptions, bool autoReleaseHandle)
        {
            QueueEditorUpdateIfNeeded();
            var relOp = SceneProvider.ReleaseScene(ResourceManager, handle, unloadOptions);
            if (autoReleaseHandle)
                relOp.ReleaseHandleOnCompletion();
            return relOp;
        }

        private object EvaluateKey(object obj)
        {
            if (obj is IKeyEvaluator)
                return (obj as IKeyEvaluator).RuntimeKey;
            return obj;
        }

        internal AsyncOperationHandle<List<string>> CheckForCatalogUpdates(bool autoReleaseHandle = true)
        {
            if (ShouldChainRequest)
                return CheckForCatalogUpdatesWithChain(autoReleaseHandle);

            if (m_ActiveCheckUpdateOperation.IsValid())
                m_ActiveCheckUpdateOperation.Release();

            m_ActiveCheckUpdateOperation = new CheckCatalogsOperation(this).Start(m_ResourceLocators);
            if (autoReleaseHandle)
                AutoReleaseHandleOnTypelessCompletion(m_ActiveCheckUpdateOperation);
            return m_ActiveCheckUpdateOperation;
        }

        internal AsyncOperationHandle<List<string>> CheckForCatalogUpdatesWithChain(bool autoReleaseHandle)
        {
            return ResourceManager.CreateChainOperation(ChainOperation, op => CheckForCatalogUpdates(autoReleaseHandle));
        }

        public ResourceLocatorInfo GetLocatorInfo(string c)
        {
            foreach (var l in m_ResourceLocators)
                if (l.Locator.LocatorId == c)
                    return l;
            return null;
        }

        internal IEnumerable<string> CatalogsWithAvailableUpdates => m_ResourceLocators.Where(s => s.ContentUpdateAvailable).Select(s => s.Locator.LocatorId);

        internal AsyncOperationHandle<List<IResourceLocator>> UpdateCatalogs(IEnumerable<string> catalogIds = null, bool autoReleaseHandle = true, bool autoCleanBundleCache = false)
        {
            if (m_ActiveUpdateOperation.IsValid())
                return m_ActiveUpdateOperation;
            if (catalogIds == null && !CatalogsWithAvailableUpdates.Any())
                return m_ResourceManager.CreateChainOperation(CheckForCatalogUpdates(), depOp => UpdateCatalogs(CatalogsWithAvailableUpdates, autoReleaseHandle, autoCleanBundleCache));

            var op = new UpdateCatalogsOperation(this).Start(catalogIds == null ? CatalogsWithAvailableUpdates : catalogIds, autoCleanBundleCache);
            if (autoReleaseHandle)
                AutoReleaseHandleOnTypelessCompletion(op);
            return op;
        }

        //needed for IEqualityComparer<IResourceLocation> interface
        public bool Equals(IResourceLocation x, IResourceLocation y)
        {
            return x.PrimaryKey.Equals(y.PrimaryKey) && x.ResourceType.Equals(y.ResourceType) && x.InternalId.Equals(y.InternalId);
        }

        //needed for IEqualityComparer<IResourceLocation> interface
        public int GetHashCode(IResourceLocation loc)
        {
            return loc.PrimaryKey.GetHashCode() * 31 + loc.ResourceType.GetHashCode();
        }

        internal AsyncOperationHandle<bool> CleanBundleCache(IEnumerable<string> catalogIds, bool forceSingleThreading)
        {
            if (ShouldChainRequest)
                return CleanBundleCacheWithChain(catalogIds, forceSingleThreading);

#if !ENABLE_CACHING
            return ResourceManager.CreateCompletedOperation(false, "Caching not enabled. There is no bundle cache to modify.");
#else
            if (catalogIds == null)
                catalogIds = m_ResourceLocators.Select(s => s.Locator.LocatorId);

            var locations = new List<IResourceLocation>();
            foreach (var c in catalogIds)
            {
                if (c == null)
                    continue;
                var loc = GetLocatorInfo(c);
                if (loc == null || loc.CatalogLocation == null)
                    continue;
                locations.Add(loc.CatalogLocation);
            }

            if (locations.Count == 0)
                return ResourceManager.CreateCompletedOperation(false,
                    "Provided catalogs do not load data from a catalog file. This can occur when using the \"Use Asset Database (fastest)\" playmode script. Bundle cache was not modified.");

            return CleanBundleCache(ResourceManager.CreateGroupOperation<object>(locations), forceSingleThreading);
#endif
        }

        internal AsyncOperationHandle<bool> CleanBundleCache(AsyncOperationHandle<IList<AsyncOperationHandle>> depOp, bool forceSingleThreading)
        {
            if (ShouldChainRequest)
                return CleanBundleCacheWithChain(depOp, forceSingleThreading);

#if !ENABLE_CACHING
            return ResourceManager.CreateCompletedOperation(false, "Caching not enabled. There is no bundle cache to modify.");
#else
            if (m_ActiveCleanBundleCacheOperation.IsValid() && !m_ActiveCleanBundleCacheOperation.IsDone)
                return ResourceManager.CreateCompletedOperation(false, "Bundle cache is already being cleaned.");
            m_ActiveCleanBundleCacheOperation = new CleanBundleCacheOperation(this, forceSingleThreading).Start(depOp);
            return m_ActiveCleanBundleCacheOperation;
#endif
        }

        internal AsyncOperationHandle<bool> CleanBundleCacheWithChain(AsyncOperationHandle<IList<AsyncOperationHandle>> depOp, bool forceSingleThreading)
        {
            return ResourceManager.CreateChainOperation(ChainOperation, op => CleanBundleCache(depOp, forceSingleThreading));
        }

        internal AsyncOperationHandle<bool> CleanBundleCacheWithChain(IEnumerable<string> catalogIds, bool forceSingleThreading)
        {
            return ResourceManager.CreateChainOperation(ChainOperation, op => CleanBundleCache(catalogIds, forceSingleThreading));
        }
    }
}
