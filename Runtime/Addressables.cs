using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using UnityEngine.AddressableAssets.Initialization;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.Diagnostics;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.ResourceManagement.Util;
using UnityEngine.SceneManagement;

[assembly: InternalsVisibleTo("Unity.Addressables.Tests")]

namespace UnityEngine.AddressableAssets
{
    /// <summary>
    /// Exception to encapsulate invalid key errors.
    /// </summary>
    public class InvalidKeyException : Exception
    {
        /// <summary>
        /// The key used to generate the exception.
        /// </summary>
        public object Key { get; private set; }
        /// <summary>
        /// Construct a new InvalidKeyException.
        /// </summary>
        /// <param name="key">The key that caused the exception.</param>
        public InvalidKeyException(object key)
        {
            Key = key;
        }

        ///<inheritdoc/>
        public InvalidKeyException() { }
        ///<inheritdoc/>
        public InvalidKeyException(string message) : base(message) { }
        ///<inheritdoc/>
        public InvalidKeyException(string message, Exception innerException) : base(message, innerException) { }
        ///<inheritdoc/>
        protected InvalidKeyException(SerializationInfo message, StreamingContext context) : base(message, context) { }
        ///<inheritdoc/>
        public override string Message
        {
            get
            {
                return base.Message + ", Key=" + Key;
            }
        }
    }

    /// <summary>
    /// Entry point for Addressable API, this provides a simpler interface than using ResourceManager directly as it assumes string address type.
    /// </summary>
    public static class Addressables
    {
        static ResourceManager m_ResourceManager;
        public static ResourceManager ResourceManager
        {
            get
            {
                if (m_ResourceManager == null)
                    m_ResourceManager = new ResourceManager();
                return m_ResourceManager;
            }
        }

        /// <summary>
        /// Used to resolve a string using addressables config values
        /// </summary>
        public static string ResolveInternalId(string id)
        {
            var path = AddressablesRuntimeProperties.EvaluateString(id);
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || UNITY_XBOXONE
            if (path.Length >= 260 && path.StartsWith(Application.dataPath))
                path = path.Substring(Application.dataPath.Length + 1);
#endif
            return path;
        }

        /// <summary>
        /// Enumerates the supported modes of merging the results of requests.
        /// If keys (A, B) mapped to results ([1,2,4],[3,4,5])...
        ///  - UseFirst (or None) takes the results from the first key 
        ///  -- [1,2,4]
        ///  - Union takes results of each key and collects items that matched any key.
        ///  -- [1,2,3,4,5]
        ///  - Intersection takes results of each key, and collects items that matched every key.
        ///  -- [4]
        /// </summary>
        public enum MergeMode
        {
            None = 0,
            UseFirst = 0,
            Union,
            Intersection
        }

        /// <summary>
        /// The name of the PlayerPrefs value used to set the path to load the addressables runtime data file. 
        /// </summary>
        public const string kAddressablesRuntimeDataPath = "AddressablesRuntimeDataPath";
        const string k_AddressablesLogConditional = "ADDRESSABLES_LOG_ALL";

        /// <summary>
        /// The subfolder used by the Addressables system for its initialization data.
        /// </summary>
        public static string StreamingAssetsSubFolder
        {
            get
            {
                return "aa";
            }
        }

        /// <summary>
        /// The path used by the Addressables system for its initialization data.
        /// </summary>
        public static string BuildPath
        {
            get { return "Assets/StreamingAssets/" + StreamingAssetsSubFolder; }
        }

        /// <summary>
        /// The path used by the Addressables system to load initialization data.
        /// </summary>
        public static string RuntimePath
        {
            get { return Application.streamingAssetsPath + "/" + StreamingAssetsSubFolder; }
        }

        static List<IResourceLocator> s_ResourceLocators = new List<IResourceLocator>();
        static IAsyncOperation<IResourceLocator> s_InitializationOperation;

        static Dictionary<object, KeyValuePair<IResourceLocation, int>> s_AssetToLocationMap = new Dictionary<object, KeyValuePair<IResourceLocation, int>>();
        static Dictionary<GameObject, IResourceLocation> s_InstanceToLocationMap = new Dictionary<GameObject, IResourceLocation>();
        static Dictionary<Scene, IResourceLocation> s_SceneToLocationMap = new Dictionary<Scene, IResourceLocation>();

        static Dictionary<GameObject, Scene> s_InstanceToScene = new Dictionary<GameObject, Scene>();
        static Dictionary<Scene, HashSet<GameObject>> s_SceneToInstances = new Dictionary<Scene, HashSet<GameObject>>();
        static Action<IAsyncOperation> s_RecordAssetAction;
        static Action<IAsyncOperation> s_RecordAssetListAction;
        static Action<IAsyncOperation> s_RecordInstanceAction;
        static Action<IAsyncOperation> s_RecordInstanceListAction;
        static Action<GameObject, float> s_ReleaseInstanceAction;

        static int s_CurrentFrame;
        static HashSet<Object> s_InstancesReleasedInCurrentFrame = new HashSet<Object>();
        /// <summary>
        /// Gets the list of configured <see cref="IResourceLocator"/> objects. Resource Locators are used to find <see cref="IResourceLocation"/> objects from user-defined typed keys.
        /// </summary>
        /// <value>The resource locators list.</value>
        public static IList<IResourceLocator> ResourceLocators
        {
            get
            {
                return s_ResourceLocators;
            }
        }

        /// <summary>
        /// Debug.Log wrapper method that is contional on the LOG_ADDRESSABLES symbol definition.  This can be set in the Player preferences in the 'Scripting Define Symbols'.
        /// </summary>
        /// <param name="msg">The msg to log</param>
        [Conditional(k_AddressablesLogConditional)]
        public static void Log(string msg)
        {
            Debug.Log(msg);
        }

        /// <summary>
        /// Debug.LogFormat wrapper method that is contional on the LOG_ADDRESSABLES symbol definition.  This can be set in the Player preferences in the 'Scripting Define Symbols'.
        /// </summary>
        /// <param name="format">The string with format tags.</param>
        /// <param name="args">The args used to fill in the format tags.</param>
        [Conditional(k_AddressablesLogConditional)]
        public static void LogFormat(string format, params object[] args)
        {
            Debug.LogFormat(format, args);
        }

        /// <summary>
        /// Debug.LogWarning wrapper method.
        /// </summary>
        /// <param name="msg">The msg to log</param>
        public static void LogWarning(string msg)
        {
            Debug.LogWarning(msg);
        }

        /// <summary>
        /// Debug.LogWarningFormat wrapper method.
        /// </summary>
        /// <param name="format">The string with format tags.</param>
        /// <param name="args">The args used to fill in the format tags.</param>
        public static void LogWarningFormat(string format, params object[] args)
        {
            Debug.LogWarningFormat(format, args);
        }

        /// <summary>
        /// Debug.LogError wrapper method.
        /// </summary>
        /// <param name="msg">The msg to log</param>
        public static void LogError(string msg)
        {
            Debug.LogError(msg);
        }

        /// <summary>
        /// Debug.LogException wrapper method.
        /// </summary>
        /// <param name="msg">The msg to log</param>
        public static void LogException(IAsyncOperation op, Exception ex)
        {
            Debug.LogErrorFormat("{0} encountered in operation {1}.", ex.GetType().Name, op);
            Debug.LogException(ex);
        }

        /// <summary>
        /// Debug.LogErrorFormat wrapper method.
        /// </summary>
        /// <param name="format">The string with format tags.</param>
        /// <param name="args">The args used to fill in the format tags.</param>
        public static void LogErrorFormat(string format, params object[] args)
        {
            Debug.LogErrorFormat(format, args);
        }

        internal static bool GetResourceLocations(object key, out IList<IResourceLocation> locations)
        {
            locations = null;
            HashSet<IResourceLocation> current = null;
            foreach (var l in s_ResourceLocators)
            {
                IList<IResourceLocation> locs;
                if (l.Locate(key, out locs))
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

        internal static bool GetResourceLocations(IList<object> keys, MergeMode merge, out IList<IResourceLocation> locations)
        {
            locations = null;
            HashSet<IResourceLocation> current = null;
            foreach (var key in keys)
            {
                IList<IResourceLocation> locs;
                if (GetResourceLocations(key, out locs))
                {
                    if (locations == null)
                    {
                        locations = locs;
                        if (merge == MergeMode.UseFirst)
                            return true;
                    }
                    else
                    {
                        if (current == null)
                        {
                            current = new HashSet<IResourceLocation>();
                            foreach (var loc in locations)
                                current.Add(loc);
                        }

                        if (merge == MergeMode.Intersection)
                            current.IntersectWith(locs);
                        else if (merge == MergeMode.Union)
                            current.UnionWith(locs);
                    }
                }
                else
                {
                    //if entries for a key are not found, the intersection is empty
                    if (merge == MergeMode.Intersection)
                    {
                        locations = null;
                        return false;
                    }
                }
            }

            if (current == null)
                return locations != null;

            locations = new List<IResourceLocation>(current);
            return true;
        }

        [RuntimeInitializeOnLoadMethod]
        static void RuntimeInitialization()
        {
#if !ADDRESSABLES_DISABLE_AUTO_INITIALIZATION
            Initialize();
#endif
        }

        /// <summary>
        /// Initialize Addressables system.  This method will automatically be called at startup unless ADDRESSABLES_DISABLE_AUTO_INITIALIZATION is defined as a script conditional in the player settings.
        /// If ADDRESSABLES_DISABLE_AUTO_INITIALIZATION is set and a request is made, this method will also be called then.  It is safe to call this method mutliple times as it will only initialize once.
        /// </summary>
        /// <returns>IAsync operation for initialization. The result of the operation is the IResourceLocator that was loaded.</returns>
        public static IAsyncOperation<IResourceLocator> Initialize()
        {
            if (s_InitializationOperation != null)
                return s_InitializationOperation;

            //these need to be referenced in order to prevent stripping on IL2CPP platforms.
            if (string.IsNullOrEmpty(Application.streamingAssetsPath))
                Debug.LogWarning("Application.streamingAssetsPath has been stripped!");
#if !UNITY_SWITCH
            if (string.IsNullOrEmpty(Application.persistentDataPath))
                Debug.LogWarning("Application.persistentDataPath has been stripped!");
#endif
            ResourceManager.ExceptionHandler = LogException;

            var runtimeDataPath = Addressables.ResolveInternalId(PlayerPrefs.GetString(kAddressablesRuntimeDataPath, RuntimePath + "/settings.json"));

            if (string.IsNullOrEmpty(runtimeDataPath))
                return new CompletedOperation<IResourceLocator>().Start(null, null, null, new InvalidKeyException(runtimeDataPath));

            if (!Application.isPlaying)
                LogWarning("Addressables are not available in edit mode.");

            s_ReleaseInstanceAction = ReleaseInstance;
            s_RecordAssetAction = RecordObjectLocation;
            s_RecordAssetListAction = RecordObjectListLocation;
            s_RecordInstanceAction = RecordInstanceLocation;
            s_RecordInstanceListAction = RecordInstanceListLocation;
            DiagnosticEventCollector.ResourceManagerProfilerEventsEnabled = true;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            return (s_InitializationOperation = new InitializationOperation(runtimeDataPath, null));
        }

        /// <summary>
        /// Additively load catalogs from runtime data.  The settings are not used.
        /// </summary>
        /// <param name="catalogPath">The path to the runtime data.</param>
        /// <param name="providerSuffix">This value, if not null or empty, will be appended to all provider ids loaded from this data.</param>
        /// <returns>IAsync operation for initialization.</returns>
        public static IAsyncOperation<IResourceLocator> LoadContentCatalog(string catalogPath, string providerSuffix = null)
        {
            var catalogLoc = new ResourceLocationBase(catalogPath, catalogPath, typeof(JsonAssetProvider).FullName);
            if (!InitializationOperation.IsDone)
                return AsyncOperationCache.Instance.Acquire<ChainOperation<IResourceLocator, IResourceLocator>>().Start(null, catalogLoc, InitializationOperation, op => LoadContentCatalog(catalogPath, providerSuffix)).Retain();
            return Initialization.InitializationOperation.LoadContentCatalog(catalogLoc, providerSuffix);
        }

        /// <summary>
        /// Initialization operation.  You can register a callback with this if you need to run code after Addressables is ready.  Any requests made before this operaton completes will automatically cahin to its result.
        /// </summary>
        public static IAsyncOperation<IResourceLocator> InitializationOperation
        {
            get
            {
                if (s_InitializationOperation == null)
                    Initialize();
                return s_InitializationOperation;
            }
        }

        static void RecordAsset(object asset, IResourceLocation location)
        {
            if (asset == null)
                return;

            if (location == null)
            {
                LogWarningFormat("RecordInstance() - parameter location cannot be null.");
                return;
            }

            KeyValuePair<IResourceLocation, int> info;
            if (!s_AssetToLocationMap.TryGetValue(asset, out info))
                s_AssetToLocationMap.Add(asset, new KeyValuePair<IResourceLocation, int>(location, 1));
            else
                s_AssetToLocationMap[asset] = new KeyValuePair<IResourceLocation, int>(location, info.Value + 1);

        }

        static void RecordObjectLocation(IAsyncOperation op)
        {
            RecordAsset(op.Result, op.Context as IResourceLocation);
        }

        static void RecordObjectListLocation(IAsyncOperation op)
        {
            var locations = op.Context as IList<IResourceLocation>;
            if (locations == null)
            {
                LogWarningFormat("RecordInstanceListLocation() - Context is not an IList<IResourceLocation> {0}", op.Context);
                return;
            }
            var results = op.Result as IList;
            if (results == null)
            {
                LogWarningFormat("RecordInstanceListLocation() - Result is not a IList {0}", op.Result);
                return;
            }

            for (int i = 0; i < results.Count; i++)
                RecordAsset(results[i], locations[i]);
        }

        static void RecordInstance(GameObject gameObject, IResourceLocation location)
        {
            if (gameObject == null)
                return;

            if (location == null)
            {
                LogWarningFormat("RecordInstance() - parameter location cannot be null.");
                return;
            }
            if (!gameObject.scene.IsValid() || !gameObject.scene.isLoaded)
            {
                LogWarningFormat("RecordInstance() - scene is not valid and loaded " + gameObject.scene);
                return;
            }



            s_InstanceToLocationMap.Add(gameObject, location);
            s_InstanceToScene.Add(gameObject, gameObject.scene);
            HashSet<GameObject> instances;
            if (!s_SceneToInstances.TryGetValue(gameObject.scene, out instances))
                s_SceneToInstances.Add(gameObject.scene, instances = new HashSet<GameObject>());
            instances.Add(gameObject);
        }

        static void RecordInstanceLocation(IAsyncOperation op)
        {
            RecordInstance(op.Result as GameObject, op.Context as IResourceLocation);
        }

        static void RecordInstanceListLocation(IAsyncOperation op)
        {
            var locations = op.Context as IList<IResourceLocation>;
            if (locations == null)
            {
                LogWarningFormat("RecordInstanceListLocation() - Context is not an IList<IResourceLocation> {0}", op.Context);
                return;
            }
            var results = op.Result as IList;
            if (results == null)
            {
                LogWarningFormat("RecordInstanceListLocation() - Result is not a IList {0}", op.Result);
                return;
            }
            for (int i = 0; i < results.Count; i++)
                RecordInstance(results[i] as GameObject, locations[i]);
        }

        internal class GetLocationsOperation : AsyncOperationBase<IList<IResourceLocation>>
        {
            Action<IAsyncOperation<IResourceLocation>> m_Callback;
            MergeMode m_Mode;
            public IAsyncOperation<IList<IResourceLocation>> Start(object key, Action<IAsyncOperation<IResourceLocation>> callback)
            {
                Key = key;
                m_Callback = callback;
                m_Result = null;
                DelayedActionManager.AddAction((Action)OnComplete);
                return this;
            }
            public IAsyncOperation<IList<IResourceLocation>> Start(IList<object> keys, Action<IAsyncOperation<IResourceLocation>> callback, MergeMode mode)
            {
                Key = keys;
                m_Mode = mode;
                m_Callback = callback;
                m_Result = null;
                DelayedActionManager.AddAction((Action)OnComplete);
                return this;
            }

            void OnComplete()
            {
                IList<IResourceLocation> locations;
                var keyList = Key as IList<object>;
                if (keyList != null)
                    GetResourceLocations(keyList, m_Mode, out locations);
                else
                    GetResourceLocations(Key, out locations);

                if (m_Callback != null && locations != null)
                {
                    //very wasteful, but needed to ensure expected behavior - callbacks should not be passed in for location queries but they need to be supported if the user chooses to
                    foreach (var loc in locations)
                        m_Callback(new CompletedOperation<IResourceLocation>().Start(loc, Key, loc));
                }
                SetResult(locations);
                InvokeCompletionEvent();
            }
        }

        /// <summary>
        /// Load a single asset
        /// </summary>
        /// <param name="location">The location of the asset.</param>        
        public static IAsyncOperation<TObject> LoadAsset<TObject>(IResourceLocation location) where TObject : class
        {
            var loadOp = Addressables.ResourceManager.ProvideResource<TObject>(location);
            (loadOp as IAsyncOperation).Completed += s_RecordAssetAction;
            loadOp.Key = location;
            return loadOp;
        }

        /// <summary>
        /// Load a single asset
        /// </summary>
        /// <param name="key">The key of the location of the asset.</param>        
        public static IAsyncOperation<TObject> LoadAsset<TObject>(object key) where TObject : class
        {
            if (!InitializationOperation.IsDone)
                return AsyncOperationCache.Instance.Acquire<ChainOperation<TObject, IResourceLocator>>().Start(null, key, InitializationOperation, op => LoadAsset<TObject>(key)).Retain();

            if (typeof(IResourceLocation).IsAssignableFrom(typeof(TObject)))
            {
                IResourceLocation loc = null;
                IList<IResourceLocation> locList;
                if (GetResourceLocations(key, out locList))
                    loc = locList[0];
                return new CompletedOperation<IResourceLocation>().Start(loc, key, loc) as IAsyncOperation<TObject>;
            }

            IList<IResourceLocation> locations;
            if (GetResourceLocations(key, out locations))
            {
                foreach (var loc in locations)
                {
                    var provider = ResourceManager.GetResourceProvider<TObject>(loc);
                    if (provider != null)
                    {
                        var op = ResourceManager.ProvideResource<TObject>(loc);
                        (op as IAsyncOperation).Completed += s_RecordAssetAction;
                        op.Key = key;
                        return op;
                    }
                }
                return new CompletedOperation<TObject>().Start(null, key, null, new UnknownResourceProviderException(locations[0]));
            }
            return new CompletedOperation<TObject>().Start(null, key, null, new InvalidKeyException(key));
        }

        /// <summary>
        /// Load multiple assets
        /// </summary>
        /// <param name="locations">The locations of the assets.</param>
        /// <param name="callback">Callback Action that is called per load operation.</param>        
        public static IAsyncOperation<IList<TObject>> LoadAssets<TObject>(IList<IResourceLocation> locations, Action<IAsyncOperation<TObject>> callback)
            where TObject : class
        {
            var loadOp = Addressables.ResourceManager.ProvideResources(locations, callback);
            (loadOp as IAsyncOperation).Completed += s_RecordAssetListAction;
            loadOp.Key = locations;
            return loadOp;
        }


        /// <summary>
        /// Load mutliple assets
        /// </summary>
        /// <param name="keys">List of keys for the locations.</param>
        /// <param name="callback">Callback Action that is called per load operation.</param>
        /// <param name="mode">Method for merging the results of key matches.  See <see cref="MergeMode"/> for specifics</param>
        public static IAsyncOperation<IList<TObject>> LoadAssets<TObject>(IList<object> keys, Action<IAsyncOperation<TObject>> callback, MergeMode mode)
            where TObject : class
        {
            if (!InitializationOperation.IsDone)
                return AsyncOperationCache.Instance.Acquire<ChainOperation<IList<TObject>, IResourceLocator>>().Start(null, keys, InitializationOperation, op => LoadAssets(keys, callback, mode)).Retain();

            if (typeof(IResourceLocation).IsAssignableFrom(typeof(TObject)))
                return AsyncOperationCache.Instance.Acquire<GetLocationsOperation>().Start(keys, callback as Action<IAsyncOperation<IResourceLocation>>, mode).Retain() as IAsyncOperation<IList<TObject>>;

            IList<IResourceLocation> locations;
            if (GetResourceLocations(keys, mode, out locations))
            {
                var loadOp = LoadAssets(locations, callback);
                loadOp.Key = keys;
                return loadOp;
            }

            return new CompletedOperation<IList<TObject>>().Start(locations, keys, null, new InvalidKeyException(keys));
        }


        /// <summary>
        /// Load mutliple assets
        /// </summary>
        /// <param name="key">Key for the locations.</param>
        /// <param name="callback">Callback Action that is called per load operation.</param>
        public static IAsyncOperation<IList<TObject>> LoadAssets<TObject>(object key, Action<IAsyncOperation<TObject>> callback)
            where TObject : class
        {
            if (!InitializationOperation.IsDone)
                return AsyncOperationCache.Instance.Acquire<ChainOperation<IList<TObject>, IResourceLocator>>().Start(null, key, InitializationOperation, op => LoadAssets(key, callback)).Retain();

            if (typeof(IResourceLocation).IsAssignableFrom(typeof(TObject)))
                return AsyncOperationCache.Instance.Acquire<GetLocationsOperation>().Start(key, callback as Action<IAsyncOperation<IResourceLocation>>).Retain() as IAsyncOperation<IList<TObject>>;

            IList<IResourceLocation> locations;
            if (GetResourceLocations(key, out locations))
            {
                var loadOp = LoadAssets(locations, callback);
                loadOp.Key = key;
                return loadOp;
            }

            return new CompletedOperation<IList<TObject>>().Start(locations, key, null, new InvalidKeyException(key));
        }

        /// <summary>
        /// Release asset.
        /// </summary>
        /// <param name="asset">The asset to release.</param>
        public static void ReleaseAsset<TObject>(TObject asset)
            where TObject : class
        {
            if (asset == null)
            {
                LogWarning("ResourceManager.Release() - trying to relase null asset.");
                return;
            }
            KeyValuePair<IResourceLocation, int> info;
            if (!s_AssetToLocationMap.TryGetValue(asset, out info))
            {
                LogWarningFormat("Addressables.ResourceManager.Release() - unable to find location info for asset {0}.", asset);
                return;
            }
            if (info.Value <= 1)
                s_AssetToLocationMap.Remove(asset);
            else
                s_AssetToLocationMap[asset] = new KeyValuePair<IResourceLocation, int>(info.Key, info.Value - 1);
            Addressables.ResourceManager.ReleaseResource(asset, info.Key);
        }

        private static IAsyncOperation<IList<TObject>> PreloadDependencies<TObject>(IList<object> keys, Action<IAsyncOperation<TObject>> callback, MergeMode mode) where TObject : class
        {
            if (!InitializationOperation.IsDone)
                return AsyncOperationCache.Instance.Acquire<ChainOperation<IList<TObject>, IResourceLocator>>().Start(null, keys, InitializationOperation, op => PreloadDependencies(keys, callback, mode)).Retain();

            IList<IResourceLocation> locations;
            if (!GetResourceLocations(keys, mode, out locations))
                return new CompletedOperation<IList<TObject>>().Start(locations, keys, null, new InvalidKeyException(keys));


            var locHash = new HashSet<IResourceLocation>();
            foreach (var loc in locations)
            {
                if (loc.HasDependencies)
                {
                    foreach (var dep in loc.Dependencies)
                        locHash.Add(dep);
                }
            }
            var loadOp = LoadAssets(new List<IResourceLocation>(locHash), callback);
            loadOp.Completed += op => DelayedActionManager.AddAction((Action<IList<IResourceLocation>>)InternalReleaseLocations, 0, op.Context);
            loadOp.Key = keys;
            return loadOp;
        }

        private static IAsyncOperation<IList<TObject>> PreloadDependencies<TObject>(object key, Action<IAsyncOperation<TObject>> callback) where TObject : class
        {
            if (!InitializationOperation.IsDone)
                return AsyncOperationCache.Instance.Acquire<ChainOperation<IList<TObject>, IResourceLocator>>().Start(null, key, InitializationOperation, op => PreloadDependencies(key, callback)).Retain();

            IList<IResourceLocation> locations;
            if (!GetResourceLocations(key, out locations))
                return new CompletedOperation<IList<TObject>>().Start(locations, key, null, new InvalidKeyException(key));


            var locHash = new HashSet<IResourceLocation>();
            foreach (var loc in locations)
            {
                if (loc.HasDependencies)
                {
                    foreach (var dep in loc.Dependencies)
                        locHash.Add(dep);
                }
            }
            var loadOp = LoadAssets(new List<IResourceLocation>(locHash), callback);
            loadOp.Completed += op => DelayedActionManager.AddAction((Action<IList<IResourceLocation>>)InternalReleaseLocations, 0, op.Context);
            loadOp.Key = key;
            return loadOp;
        }
        /// <summary>
        /// Asynchronously loads only the dependencies for the specified <paramref name="key"/>.
        /// </summary>
        /// <returns>An async operation that will complete when all individual async load operations are complete.</returns>
        /// <param name="key">key for which to load dependencies.</param>
        /// <param name="callback">This callback will be invoked once for each object that is loaded.</param>
        public static IAsyncOperation<long> GetDownloadSize(object key)
        {
            if (!InitializationOperation.IsDone)
                return AsyncOperationCache.Instance.Acquire<ChainOperation<long, IResourceLocator>>().Start(null, key, InitializationOperation, op => GetDownloadSize(key)).Retain();

            IList<IResourceLocation> locations;
            if (typeof(IList<IResourceLocation>).IsAssignableFrom(key.GetType()))
                locations = key as IList<IResourceLocation>;
            else if (typeof(IResourceLocation).IsAssignableFrom(key.GetType()))
            {
                locations = new List<IResourceLocation>(1);
                locations.Add(key as IResourceLocation);
            }
            else
            {
                if (!GetResourceLocations(key, out locations))
                    return new CompletedOperation<long>().Start(locations, key, 0, new InvalidKeyException(key));
            }

            var locHash = new HashSet<IResourceLocation>();
            foreach (var loc in locations)
            {
                if (loc.HasDependencies)
                {
                    foreach (var dep in loc.Dependencies)
                        locHash.Add(dep);
                }
            }

            long size = 0;
            foreach (var d in locHash)
            {
                var sizeData = d.Data as ILocationSizeData;
                if (sizeData != null)
                    size += sizeData.ComputeSize(d);
            }
            return new CompletedOperation<long>().Start(locations, key, size);
        }

        /// <summary>
        /// Downloads dependencies of assets marked with the specified labels and addresses.  
        /// </summary>
        /// <param name="keys">The keys of the assets to load dependencies for.</param>
        /// <param name="mode">Method for merging the results of key matches.  See <see cref="MergeMode"/> for specifics</param>
        /// <returns>The IAsyncOperation for the dependency load.</returns>
        public static IAsyncOperation DownloadDependencies(IList<object> keys, MergeMode mode)
        {
            return PreloadDependencies<object>(keys, null, mode);
        }
        
        /// <summary>
        /// Downloads dependencies of assets marked with the specified label or address.  
        /// </summary>
        /// <param name="key">The key of the asset(s) to load dependencies for.</param>
        /// <returns>The IAsyncOperation for the dependency load.</returns>
        public static IAsyncOperation DownloadDependencies(object key)
        {
            return PreloadDependencies<object>(key, null);
        }

        /// <summary>
        /// Release dependencies for the specified <paramref name="locations"/>.
        /// </summary>
        /// <param name="locations">Location for which to release dependencies.</param>
        internal static void InternalReleaseLocations(IList<IResourceLocation> locations)
        {
            foreach (var loc in locations)
                Addressables.ResourceManager.ReleaseResource<object>(null, loc);
        }

        /// <summary>
        /// Instantiate object.
        /// </summary>
        /// <param name="location">The location of the Object to instantiate.</param>
        /// <param name="instantiateParameters">Parameters for instantiation.</param>
        public static IAsyncOperation<GameObject> Instantiate(IResourceLocation location, InstantiationParameters instantiateParameters)
        {
            var instOp = Addressables.ResourceManager.ProvideInstance<GameObject>(location, instantiateParameters);
            (instOp as IAsyncOperation).Completed += s_RecordInstanceAction;
            instOp.Key = location;
            return instOp;
        }


        /// <summary>
        /// Instantiate single object.
        /// </summary>
        /// <param name="key">The key of the location of the Object to instantiate.</param>
        /// <param name="instantiateParameters">Parameters for instantiation.</param>
        public static IAsyncOperation<GameObject> Instantiate(object key, InstantiationParameters instantiateParameters)
        {
            if (!InitializationOperation.IsDone)
                return AsyncOperationCache.Instance.Acquire<ChainOperation<GameObject, IResourceLocator>>().Start(null, key, InitializationOperation, op => Instantiate(key, instantiateParameters)).Retain();

            IList<IResourceLocation> locations;
            if (!GetResourceLocations(key, out locations))
                return new CompletedOperation<GameObject>().Start(locations, key, null, new InvalidKeyException(key));


            var instOp = Instantiate(locations[0], instantiateParameters);
            instOp.Key = key;
            return instOp;
        }

        /// <summary>
        /// Instantiate single object.
        /// </summary>
        /// <param name="location">The location of the Object to instantiate.</param>
        /// <param name="parent">Parent transform for instantiated object.</param>
        /// <param name="instantiateInWorldSpace">Option to retain world space when instantiated with a parent.</param>
        public static IAsyncOperation<GameObject> Instantiate(IResourceLocation location, Transform parent = null, bool instantiateInWorldSpace = false)
        {
            return Instantiate(location, new InstantiationParameters(parent, instantiateInWorldSpace));
        }

        /// <summary>
        /// Instantiate single object.
        /// </summary>
        /// <param name="location">The location of the Object to instantiate.</param>
        /// <param name="position">The position of the instantiated object.</param>
        /// <param name="rotation">The rotation of the instantiated object.</param>
        /// <param name="parent">Parent transform for instantiated object.</param>
        public static IAsyncOperation<GameObject> Instantiate(IResourceLocation location, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            return Instantiate(location, new InstantiationParameters(position, rotation, parent));
        }

        /// <summary>
        /// Instantiate single object.
        /// </summary>
        /// <param name="key">The key of the location of the Object to instantiate.</param>
        /// <param name="parent">Parent transform for instantiated object.</param>
        /// <param name="instantiateInWorldSpace">Option to retain world space when instantiated with a parent.</param>
        public static IAsyncOperation<GameObject> Instantiate(object key, Transform parent = null, bool instantiateInWorldSpace = false)
        {
            return Instantiate(key, new InstantiationParameters(parent, instantiateInWorldSpace));
        }

        /// <summary>
        /// Instantiate single object.
        /// </summary>
        /// <param name="key">The key of the location of the Object to instantiate.</param>
        /// <param name="position">The position of the instantiated object.</param>
        /// <param name="rotation">The rotation of the instantiated object.</param>
        /// <param name="parent">Parent transform for instantiated object.</param>
        public static IAsyncOperation<GameObject> Instantiate(object key, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            return Instantiate(key, new InstantiationParameters(position, rotation, parent));
        }

        /// <summary>
        /// Instantiate multiple objects.
        /// </summary>
        /// <param name="key">The key of the locations of the objects to instantiate.</param>
        /// <param name="callback">Callback Action that is called per load operation</param>
        /// <param name="parent">Parent transform for instantiated objects.</param>
        /// <param name="instantiateInWorldSpace">Option to retain world space when instantiated with a parent.</param>
        public static IAsyncOperation<IList<GameObject>> InstantiateAll(object key, Action<IAsyncOperation<GameObject>> callback, Transform parent = null, bool instantiateInWorldSpace = false)
        {
            return InstantiateAll(key, callback, new InstantiationParameters(parent, instantiateInWorldSpace));
        }

        /// <summary>
        /// Instantiate multiple objects.
        /// </summary>
        /// <param name="key">The key of the location of the objects to instantiate.</param>
        /// <param name="callback">This callback will be invoked once for each object that is instantiated.</param>
        /// <param name="instantiateParameters">Parameters for instantiation.</param>
        public static IAsyncOperation<IList<GameObject>> InstantiateAll(object key, Action<IAsyncOperation<GameObject>> callback, InstantiationParameters instantiateParameters)
        {
            if (!InitializationOperation.IsDone)
                return AsyncOperationCache.Instance.Acquire<ChainOperation<IList<GameObject>, IResourceLocator>>().Start(null, key, InitializationOperation, op => InstantiateAll(key, callback, instantiateParameters)).Retain();

            IList<IResourceLocation> locations;
            if (!GetResourceLocations(key, out locations))
                return new CompletedOperation<IList<GameObject>>().Start(locations, key, null, new InvalidKeyException(key));


            var instOp = InstantiateAll(locations, callback, instantiateParameters);
            instOp.Key = key;
            return instOp;
        }

        /// <summary>
        /// Instantiate multiple objects.
        /// </summary>
        /// <param name="locations">The locations of the objects to instantiate.</param>
        /// <param name="callback">This callback will be invoked once for each object that is instantiated.</param>
        /// <param name="instantiateParameters">Parameters for instantiation.</param>
        public static IAsyncOperation<IList<GameObject>> InstantiateAll(IList<IResourceLocation> locations, Action<IAsyncOperation<GameObject>> callback, InstantiationParameters instantiateParameters)
        {
            var instOp = Addressables.ResourceManager.ProvideInstances(locations, callback, instantiateParameters);
            (instOp as IAsyncOperation).Completed += s_RecordInstanceListAction;
            instOp.Key = locations;
            return instOp;
        }

        /// <summary>
        /// Release instantiated object.
        /// </summary>
        /// <param name="instance">The instantiated object to release.</param>
        /// <param name="delay">The time delay in seconds to wait until releasing the instantiated object.  If this value is less than 0, it will release immediately.</param>
        public static void ReleaseInstance(GameObject instance, float delay = 0)
        {
            if (delay > 0)
            {
                DelayedActionManager.AddAction(s_ReleaseInstanceAction, delay, instance, 0);
                return;
            }
            if (instance == null)
            {
                LogWarning("Addressables.ResourceManager.ReleaseInstance() - trying to release null instance");
                return;
            }
            if (s_CurrentFrame != Time.frameCount)
            {
                s_CurrentFrame = Time.frameCount;
                s_InstancesReleasedInCurrentFrame.Clear();
            }

            //silently ignore multiple releases that occur in the same frame
            if (s_InstancesReleasedInCurrentFrame.Contains(instance))
                return;

            s_InstancesReleasedInCurrentFrame.Add(instance);
            IResourceLocation location;
            if (!s_InstanceToLocationMap.TryGetValue(instance, out location))
            {
                //TODO - need to keep this around for to-be-implemented a verbose loggging option
                //Addressables.LogWarningFormat("Addressables.ResourceManager.ReleaseInstance() - unable to find location for instance {0}.", instance.GetInstanceID());
                if (Application.isPlaying)
                    Object.Destroy(instance);
                else
                    Object.DestroyImmediate(instance);
                return;
            }

            if (!s_SceneToInstances[instance.scene].Remove(instance))
                LogWarningFormat("Instance {0} was not found in scene {1}.", instance.GetInstanceID(), instance.scene);
            if (!s_InstanceToScene.Remove(instance))
                LogWarningFormat("Instance {0} was not found instance->scene map.", instance.GetInstanceID());


            s_InstanceToLocationMap.Remove(instance);
            Addressables.ResourceManager.ReleaseInstance(instance, location);
        }

        /// <summary>
        /// Load scene.
        /// </summary>
        /// <param name="key">The key of the location of the scene to load.</param>
        /// <param name="loadMode">Scene load mode.</param>
        public static IAsyncOperation<Scene> LoadScene(object key, LoadSceneMode loadMode = LoadSceneMode.Single)
        {
            if (!InitializationOperation.IsDone)
                return AsyncOperationCache.Instance.Acquire<ChainOperation<Scene, IResourceLocator>>().Start(null, key, InitializationOperation, op => LoadScene(key, loadMode)).Retain();

            IList<IResourceLocation> locations;
            if (!GetResourceLocations(key, out locations))
                return new CompletedOperation<Scene>().Start(locations, key, default(Scene), new InvalidKeyException(key));


            var loadOp = LoadScene(locations[0], loadMode);
            loadOp.Key = key;
            return loadOp;
        }

        /// <summary>
        /// Load scene.
        /// </summary>
        /// <param name="location">The location of the scene to load.</param>
        /// <param name="loadMode">Scene load mode.</param>
        public static IAsyncOperation<Scene> LoadScene(IResourceLocation location, LoadSceneMode loadMode = LoadSceneMode.Single)
        {
            if (loadMode == LoadSceneMode.Single)
                ValidateSceneInstances();

            var loadOp = Addressables.ResourceManager.ProvideScene(location, loadMode);
            loadOp.Completed += op => s_SceneToLocationMap.Add(op.Result, location);
            loadOp.Key = location;
            return loadOp;
        }

        /// <summary>
        /// Unload scene.
        /// </summary>
        /// <param name="scene">The scene to unload.</param>
        public static IAsyncOperation<Scene> UnloadScene(Scene scene)
        {
            IResourceLocation location;
            if (!s_SceneToLocationMap.TryGetValue(scene, out location))
                return new CompletedOperation<Scene>().Start(null, scene, default(Scene), new ArgumentNullException("scene", string.Format("UnloadScene - unable to find location for scene {0}.", scene)));

            s_SceneToLocationMap.Remove(scene);
            return Addressables.ResourceManager.ReleaseScene(scene, location);
        }


        /// <summary>
        /// Notify the ResourceManager that a tracked instance has changed scenes so that it can be released properly when the scene is unloaded.
        /// </summary>
        /// <param name="gameObject">The gameobject that is being moved to a new scene.</param>
        /// <param name="previousScene">Previous scene for gameobject.</param>
        /// <param name="currentScene">Current scene for gameobject.</param>
        public static void RecordInstanceSceneChange(GameObject gameObject, Scene previousScene, Scene currentScene)
        {
            if (gameObject == null)
                return;
            HashSet<GameObject> instanceIds;
            if (!s_SceneToInstances.TryGetValue(previousScene, out instanceIds))
                LogFormat("Unable to find instance table for instance {0}.", gameObject.GetInstanceID());
            else
                instanceIds.Remove(gameObject);
            if (!s_SceneToInstances.TryGetValue(currentScene, out instanceIds))
                s_SceneToInstances.Add(currentScene, instanceIds = new HashSet<GameObject>());
            instanceIds.Add(gameObject);

            s_InstanceToScene[gameObject] = currentScene;
        }

        static void OnSceneUnloaded(Scene scene)
        {
            if (!Application.isPlaying)
                return;

            if (s_CurrentFrame != Time.frameCount)
            {
                s_CurrentFrame = Time.frameCount;
                s_InstancesReleasedInCurrentFrame.Clear();
            }

            HashSet<GameObject> instances;
            if (s_SceneToInstances.TryGetValue(scene, out instances))
            {
                foreach (var go in instances)
                {
                    if(IsDontDestroyOnLoad(go))
                        continue;

                    if (s_InstancesReleasedInCurrentFrame.Contains(go))
                        continue;


                    IResourceLocation loc;
                    if (s_InstanceToLocationMap.TryGetValue(go, out loc))
                    {
                        if (!s_InstanceToScene.Remove(go))
                            LogWarningFormat("Scene not found for instance {0}", go);

                        s_InstanceToLocationMap.Remove(go);
                        Addressables.ResourceManager.ReleaseInstance(go, loc);
                    }
                    else
                    {
                        //object has already been released
                        LogWarningFormat("Object instance {0} has already been released.", go);
                    }
                }

                s_SceneToInstances.Remove(scene);
            }
        }

        private static string m_DontDestroyOnLoadSceneName = "DontDestroyOnLoad";
        static bool IsDontDestroyOnLoad(GameObject go)
        {
            if (go != null && go.scene.name == m_DontDestroyOnLoadSceneName)
            {
                Scene temp;
                if (!s_InstanceToScene.TryGetValue(go, out temp))
                    s_InstanceToScene.Add(go, go.scene);
                else
                    s_InstanceToScene[go] = go.scene;

                HashSet<GameObject> newInstances;
                if (!s_SceneToInstances.TryGetValue(go.scene, out newInstances))
                    s_SceneToInstances.Add(go.scene, newInstances = new HashSet<GameObject>());

                if(!newInstances.Contains(go))
                    newInstances.Add(go);

                return true;
            }

            return false;
        }

        static void ValidateSceneInstances()
        {
            var objectsThatNeedToBeFixed = new List<KeyValuePair<Scene, GameObject>>();
            foreach (var kvp in s_SceneToInstances)
            {
                foreach (var go in kvp.Value)
                {
                    if (go == null)
                    {
                        LogWarningFormat("GameObject instance has been destroyed, use Addressables.ResourceManager.ReleaseInstance to ensure proper reference counts.");
                    }
                    else
                    {
                        if (go.scene != kvp.Key)
                        {
                            LogWarningFormat("GameObject instance {0} has been moved to from scene {1} to scene {2}.  When moving tracked instances, use Addressables.ResourceManager.RecordInstanceSceneChange to ensure that reference counts are accurate.", go, kvp.Key, go.scene.GetHashCode());
                            objectsThatNeedToBeFixed.Add(new KeyValuePair<Scene, GameObject>(kvp.Key, go));
                        }
                    }
                }
            }

            foreach (var go in objectsThatNeedToBeFixed)
                RecordInstanceSceneChange(go.Value, go.Key, go.Value.scene);
        }
    }

}

