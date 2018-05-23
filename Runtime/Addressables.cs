using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.ResourceManagement;
using UnityEngine.SceneManagement;

namespace UnityEngine.AddressableAssets
{
    /// <summary>
    /// Entry point for Addressable API, this provides a simpler interface than using ResourceManager directly as it assumes string address type.
    /// </summary>
    public class Addressables
    {
        public class InvalidKeyException : Exception
        {
            public object Key { get; private set; }
            public InvalidKeyException(object key)
            {
                Key = key;
            }
            public override string Message
            {
                get
                {
                    return base.Message + ", Key=" + Key;
                }
            }
        }

        public enum MergeMode
        {
            None,
            Union,
            Intersection
        }

        static List<IResourceLocator> s_resourceLocators = new List<IResourceLocator>();
        static IAsyncOperation<bool> s_initializationOperation = null;

        static Dictionary<object, KeyValuePair<IResourceLocation, int>> s_assetToLocationMap = new Dictionary<object, KeyValuePair<IResourceLocation, int>>();
        static Dictionary<GameObject, IResourceLocation> s_instanceToLocationMap = new Dictionary<GameObject, IResourceLocation>();
        static Dictionary<Scene, IResourceLocation> s_sceneToLocationMap = new Dictionary<Scene, IResourceLocation>();

        static Dictionary<GameObject, Scene> s_instanceToScene = new Dictionary<GameObject, Scene>();
        static Dictionary<Scene, HashSet<GameObject>> s_sceneToInstances = new Dictionary<Scene, HashSet<GameObject>>();
        static Action<IAsyncOperation> s_recordAssetAction;
        static Action<IAsyncOperation> s_recordAssetListAction;
        static Action<IAsyncOperation> s_recordInstanceAction;
        static Action<IAsyncOperation> s_recordInstanceListAction;
        static Action<GameObject, float> s_releaseInstanceAction;

        static int s_currentFrame;
        static HashSet<Object> s_instancesReleasedInCurrentFrame = new HashSet<Object>();
        /// <summary>
        /// Gets the list of configured <see cref="IResourceLocator"/> objects. Resource Locators are used to find <see cref="IResourceLocation"/> objects from user-defined typed keys.
        /// </summary>
        /// <value>The resource locators list.</value>
        public static IList<IResourceLocator> ResourceLocators
        {
            get
            {
                return s_resourceLocators;
            }
        }

        internal static bool GetResourceLocations(object key, out IList<IResourceLocation> locations)
        {
            locations = null;
            HashSet<IResourceLocation> current = null;
            foreach (var l in s_resourceLocators)
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
                        if (merge == MergeMode.None)
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
            }

            if (current == null)
                return locations != null;

            locations = new List<IResourceLocation>(current);
            return true;
        }

        /// <summary>
        /// TODO - doc
        /// </summary>
        [RuntimeInitializeOnLoadMethod]
        private static void InitializeResourceManager()
        {
            if (s_initializationOperation != null)
                return;
            var playMode = (ResourceManagerRuntimeData.EditorPlayMode)PlayerPrefs.GetInt("AddressablesPlayMode", 0);
            if (playMode == ResourceManagerRuntimeData.EditorPlayMode.Invalid)
                return;
            if (!Application.isPlaying)
                Debug.LogWarning("Addressables are not available in edit mode.");
            s_releaseInstanceAction = ReleaseInstance;
            s_recordAssetAction = RecordObjectLocation;
            s_recordAssetListAction = RecordObjectListLocation;
            s_recordInstanceAction = RecordInstanceLocation;
            s_recordInstanceListAction = RecordInstanceListLocation;
            ResourceManagement.Diagnostics.DiagnosticEventCollector.ProfileEvents = true;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            s_initializationOperation = new InitializationOperation(playMode);
        }

        private static IAsyncOperation<bool> InitializationOperation
        {
            get
            {
                if (s_initializationOperation == null)
                    InitializeResourceManager();
                return s_initializationOperation;
            }
        }


        private static void RecordAsset(object asset, IResourceLocation location)
        {
            if (asset == null)
            {
                Debug.LogWarningFormat("RecordInstance() - parameter asset cannot be null.");
                return;
            }
            if (location == null)
            {
                Debug.LogWarningFormat("RecordInstance() - parameter location cannot be null.");
                return;
            }

            KeyValuePair<IResourceLocation, int> info;
            if (!s_assetToLocationMap.TryGetValue(asset, out info))
                s_assetToLocationMap.Add(asset, new KeyValuePair<IResourceLocation, int>(location, 1));
            else
                s_assetToLocationMap[asset] = new KeyValuePair<IResourceLocation, int>(location, info.Value + 1);

        }

        private static void RecordObjectLocation(IAsyncOperation op)
        {
            RecordAsset(op.Result, op.Context as IResourceLocation);
        }

        private static void RecordObjectListLocation(IAsyncOperation op)
        {
            var locations = op.Context as IList<IResourceLocation>;
            if (locations == null)
            {
                Debug.LogWarningFormat("RecordInstanceListLocation() - Context is not an IList<IResourceLocation> {0}", op.Context);
                return;
            }
            var results = op.Result as IList;
            if (results == null)
            {
                Debug.LogWarningFormat("RecordInstanceListLocation() - Result is not a IList {0}", op.Result);
                return;
            }

            for (int i = 0; i < results.Count; i++)
                RecordAsset(results[i], locations[i]);
        }

        private static void RecordInstance(GameObject gameObject, IResourceLocation location)
        {
            if (gameObject == null)
            {
                Debug.LogWarningFormat("RecordInstance() - parameter gameObject cannot be null.");
                return;
            }
           
            if (location == null)
            {
                Debug.LogWarningFormat("RecordInstance() - parameter location cannot be null.");
                return;
            }
            if (!gameObject.scene.IsValid() || !gameObject.scene.isLoaded)
            {
                Debug.LogWarningFormat("RecordInstance() - scene is not valid and loaded " + gameObject.scene);
                return;
            }

            s_instanceToLocationMap.Add(gameObject, location);
            s_instanceToScene.Add(gameObject, gameObject.scene);
            HashSet<GameObject> instances;
            if (!s_sceneToInstances.TryGetValue(gameObject.scene, out instances))
                s_sceneToInstances.Add(gameObject.scene, instances = new HashSet<GameObject>());
            instances.Add(gameObject);
        }

        private static void RecordInstanceLocation(IAsyncOperation op)
        {
            RecordInstance(op.Result as GameObject, op.Context as IResourceLocation);
        }

        private static void RecordInstanceListLocation(IAsyncOperation op)
        {
            var locations = op.Context as IList<IResourceLocation>;
            if (locations == null)
            {
                Debug.LogWarningFormat("RecordInstanceListLocation() - Context is not an IList<IResourceLocation> {0}", op.Context);
                return;
            }
            var results = op.Result as IList;
            if (results == null)
            {
                Debug.LogWarningFormat("RecordInstanceListLocation() - Result is not a IList {0}", op.Result);
                return;
            }
            for (int i = 0; i < results.Count; i++)
                RecordInstance(results[i] as GameObject, locations[i]);
        }

        internal class GetLocationsOperation : AsyncOperationBase<IList<IResourceLocation>>
        {
            object m_key;
            IList<object> m_keys;
            Action<IAsyncOperation<IResourceLocation>> m_callback;
            MergeMode m_mode;
            public IAsyncOperation<IList<IResourceLocation>> Start(object key, Action<IAsyncOperation<IResourceLocation>> callback)
            {
                m_keys = null;
                m_key = key;
                m_callback = callback;
                m_result = null;
                DelayedActionManager.AddAction((Action)OnComplete);
                return this;
            }
            public IAsyncOperation<IList<IResourceLocation>> Start(IList<object> keys, Action<IAsyncOperation<IResourceLocation>> callback, MergeMode mode)
            {
                m_key = null;
                m_keys = keys;
                m_mode = mode;
                m_callback = callback;
                m_result = null;
                DelayedActionManager.AddAction((Action)OnComplete);
                return this;
            }

            void OnComplete()
            {
                IList<IResourceLocation> locations = null;
                if(m_key != null)
                    GetResourceLocations(m_key, out locations);
                else if(m_keys != null)
                    GetResourceLocations(m_keys, m_mode, out locations);

                if (m_callback != null && locations != null)
                {
                    foreach (var l in locations)
                    {
                        //very wasteful, but needed to ensure expected behavior - callbacks should not be passed in for location queries but they need to be supported if the user chooses to
                        var op = new EmptyOperation<IResourceLocation>();
                        op.SetResult(l);
                        m_callback(op);
                    }
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
            var loadOp = ResourceManager.ProvideResource<TObject>(location);
            (loadOp as IAsyncOperation).Completed += s_recordAssetAction;
            return loadOp;
        }

        /// <summary>
        /// Load a single asset
        /// </summary>
        /// <param name="key">The key of the location of the asset.</param>        
        public static IAsyncOperation<TObject> LoadAsset<TObject>(object key) where TObject : class
        {
            if (!InitializationOperation.IsDone)
                return AsyncOperationCache.Instance.Acquire<ChainOperation<TObject, bool>>().Start(InitializationOperation, (op) => LoadAsset<TObject>(key)).Retain();

            if (typeof(IResourceLocation).IsAssignableFrom(typeof(TObject)))
            {
                var op = new EmptyOperation<IResourceLocation>();
                IList<IResourceLocation> locs;
                if(GetResourceLocations(key, out locs))
                    op.SetResult(locs[0]);
                return op as IAsyncOperation<TObject>;
            }

            IList<IResourceLocation> locations;
            if (GetResourceLocations(key, out locations))
            {
                foreach (var loc in locations)
                {
                    var provider = ResourceManager.GetResourceProvider<TObject>(loc);
                    if (provider != null)
                        return provider.Provide<TObject>(loc, ResourceManager.LoadDependencies(loc));
                }
                throw new UnknownResourceProviderException(locations[0]);
            }
            return new EmptyOperation<TObject>().Start(null, null);
            //throw new InvalidKeyException(key);
        }

        /// <summary>
        /// Load multiple assets
        /// </summary>
        /// <param name="locations">The locations of the assets.</param>        
        public static IAsyncOperation<IList<TObject>> LoadAssets<TObject>(IList<IResourceLocation> locations, Action<IAsyncOperation<TObject>> callback)
            where TObject : class
        {
            var loadOp = ResourceManager.ProvideResources(locations, callback);
            (loadOp as IAsyncOperation).Completed += s_recordAssetListAction;
            return loadOp;
        }


        /// <summary>
        /// Load mutliple assets
        /// </summary>
        /// <param name="keys">List of keys for the locations.</param>
        /// <param name="callback">Callback Action that is called per load operation.</param>
        /// <param name="mode">Merge method of locations.</param>
        public static IAsyncOperation<IList<TObject>> LoadAssets<TObject>(IList<object> keys, Action<IAsyncOperation<TObject>> callback, MergeMode mode = MergeMode.None)
            where TObject : class
        {
            if (!InitializationOperation.IsDone)
                return AsyncOperationCache.Instance.Acquire<ChainOperation<IList<TObject>, bool>>().Start(InitializationOperation, (op) => LoadAssets<TObject>(keys, callback, mode)).Retain();

            if (typeof(IResourceLocation).IsAssignableFrom(typeof(TObject)))
                return AsyncOperationCache.Instance.Acquire<GetLocationsOperation>().Start(keys, callback as Action<IAsyncOperation<IResourceLocation>>, mode).Retain() as IAsyncOperation<IList<TObject>>;

            IList<IResourceLocation> locations;
            if (GetResourceLocations(keys, mode, out locations))
                return LoadAssets(locations, callback);
            throw new InvalidKeyException(keys);
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
                return AsyncOperationCache.Instance.Acquire<ChainOperation<IList<TObject>, bool>>().Start(InitializationOperation, (op) => LoadAssets(key, callback)).Retain();

            if (typeof(IResourceLocation).IsAssignableFrom(typeof(TObject)))
                return AsyncOperationCache.Instance.Acquire<GetLocationsOperation>().Start(key, callback as Action<IAsyncOperation<IResourceLocation>>).Retain() as IAsyncOperation<IList<TObject>>;

            IList<IResourceLocation> locations;
            if (GetResourceLocations(key, out locations))
                return LoadAssets(locations, callback);

            throw new InvalidKeyException(key);
        }

        /// <summary>
        /// Release asset.
        /// </summary>
        /// <param name="asset">The asset to release.</param>
        public static void ReleaseAsset<TObject>(TObject asset)
            where TObject : class
        {
            KeyValuePair<IResourceLocation, int> info;
            if (!s_assetToLocationMap.TryGetValue(asset, out info))
            {
                Debug.LogWarningFormat("ResourceManager.Release() - unable to find location info for asset {0}.", asset);
                return;
            }
            if (info.Value <= 1)
                s_assetToLocationMap.Remove(asset);
            else
                s_assetToLocationMap[asset] = new KeyValuePair<IResourceLocation, int>(info.Key, info.Value - 1);
            ResourceManager.ReleaseResource(asset, info.Key);
        }

        /// <summary>
        /// Asynchronously loads only the dependencies for the specified list of <paramref name="key"/>s.
        /// </summary>
        /// <returns>An async operation that will complete when all individual async load operations are complete.</returns>
        /// <param name="keys">List of keys for which to load dependencies.</param>
        /// <param name="callback">This callback will be invoked once for each object that is loaded.</param>
        public static IAsyncOperation<IList<object>> PreloadDependencies(IList<object> keys, Action<IAsyncOperation<object>> callback, MergeMode mode = MergeMode.None)
        {
            if (!InitializationOperation.IsDone)
                return AsyncOperationCache.Instance.Acquire<ChainOperation<IList<object>, bool>>().Start(InitializationOperation, (op) => PreloadDependencies(keys, callback, mode)).Retain();

            IList<IResourceLocation> locations;
            if (!GetResourceLocations(keys, mode, out locations))
                throw new InvalidKeyException(keys);

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
            loadOp.Completed += (op) => DelayedActionManager.AddAction((Action<IList<IResourceLocation>>)InternalReleaseLocations, 0, op.Context);
            return loadOp;
        }

        /// <summary>
        /// Asynchronously loads only the dependencies for the specified <paramref name="key"/>.
        /// </summary>
        /// <returns>An async operation that will complete when all individual async load operations are complete.</returns>
        /// <param name="key">key for which to load dependencies.</param>
        /// <param name="callback">This callback will be invoked once for each object that is loaded.</param>
        public static IAsyncOperation<IList<object>> PreloadDependencies(object key, Action<IAsyncOperation<object>> callback)
        {
            if (!InitializationOperation.IsDone)
                return AsyncOperationCache.Instance.Acquire<ChainOperation<IList<object>, bool>>().Start(InitializationOperation, (op) => PreloadDependencies(key, callback)).Retain();

            IList<IResourceLocation> locations;
            if (!GetResourceLocations(key, out locations))
                throw new InvalidKeyException(key);

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
            loadOp.Completed += (op) => DelayedActionManager.AddAction((Action<IList<IResourceLocation>>)InternalReleaseLocations, 0, op.Context);
            return loadOp;
        }

        /// <summary>
        /// Release dependencies for the specified <paramref name="location"/>.
        /// </summary>
        /// <param name="location">Location for which to release dependencies.</param>
        internal static void InternalReleaseLocations(IList<IResourceLocation> locations)
        {
            foreach (var loc in locations)
                ResourceManager.ReleaseResource<object>(null, loc);
        }

        /// <summary>
        /// Instantiate object.
        /// </summary>
        /// <param name="location">The location of the Object to instantiate.</param>
        /// <param name="instantiateParameters">Parameters for instantiation.</param>
        public static IAsyncOperation<TObject> Instantiate<TObject>(IResourceLocation location, InstantiationParameters instantiateParameters)
            where TObject : Object
        {
            var instOp = ResourceManager.ProvideInstance<TObject>(location, instantiateParameters);
            (instOp as IAsyncOperation).Completed += s_recordInstanceAction;
            return instOp;
        }


        /// <summary>
        /// Instantiate single object.
        /// </summary>
        /// <param name="key">The key of the location of the Object to instantiate.</param>
        /// <param name="instantiateParameters">Parameters for instantiation.</param>
        public static IAsyncOperation<TObject> Instantiate<TObject>(object key, InstantiationParameters instantiateParameters)
            where TObject : Object
        {
            if (!InitializationOperation.IsDone)
                return AsyncOperationCache.Instance.Acquire<ChainOperation<TObject, bool>>().Start(InitializationOperation, (op) => Instantiate<TObject>(key, instantiateParameters)).Retain();

            IList<IResourceLocation> locations;
            if (!GetResourceLocations(key, out locations))
                throw new InvalidKeyException(key);

            return Instantiate<TObject>(locations[0], instantiateParameters);
        }

        /// <summary>
        /// Instantiate single object.
        /// </summary>
        /// <param name="location">The location of the Object to instantiate.</param>
        /// <param name="parent">Parent transform for instantiated object.</param>
        /// <param name="instantiateInWorldSpace">Option to retain world space when instantiated with a parent.</param>
        public static IAsyncOperation<TObject> Instantiate<TObject>(IResourceLocation location, Transform parent = null, bool instantiateInWorldSpace = false) where TObject : Object
        {
            return Instantiate<TObject>(location, new InstantiationParameters(parent, instantiateInWorldSpace));
        }

        /// <summary>
        /// Instantiate single object.
        /// </summary>
        /// <param name="location">The location of the Object to instantiate.</param>
        /// <param name="position">The position of the instantiated object.</param>
        /// <param name="rotation">The rotation of the instantiated object.</param>
        /// <param name="parent">Parent transform for instantiated object.</param>
        public static IAsyncOperation<TObject> Instantiate<TObject>(IResourceLocation location, Vector3 position, Quaternion rotation, Transform parent = null) where TObject : Object
        {
            return Instantiate<TObject>(location, new InstantiationParameters(position, rotation, parent));
        }

        /// <summary>
        /// Instantiate single object.
        /// </summary>
        /// <param name="key">The key of the location of the Object to instantiate.</param>
        /// <param name="parent">Parent transform for instantiated object.</param>
        /// <param name="instantiateInWorldSpace">Option to retain world space when instantiated with a parent.</param>
        public static IAsyncOperation<TObject> Instantiate<TObject>(object key, Transform parent = null, bool instantiateInWorldSpace = false) where TObject : Object
        {
            return Instantiate<TObject>(key, new InstantiationParameters(parent, instantiateInWorldSpace));
        }

        /// <summary>
        /// Instantiate single object.
        /// </summary>
        /// <param name="key">The key of the location of the Object to instantiate.</param>
        /// <param name="position">The position of the instantiated object.</param>
        /// <param name="rotation">The rotation of the instantiated object.</param>
        /// <param name="parent">Parent transform for instantiated object.</param>
        public static IAsyncOperation<TObject> Instantiate<TObject>(object key, Vector3 position, Quaternion rotation, Transform parent = null) where TObject : Object
        {
            return Instantiate<TObject>(key, new InstantiationParameters(position, rotation, parent));
        }

        /// <summary>
        /// Instantiate multiple objects.
        /// </summary>
        /// <param name="key">The key of the locations of the objects to instantiate.</param>
        /// <param name="parent">Parent transform for instantiated objects.</param>
        /// <param name="instantiateInWorldSpace">Option to retain world space when instantiated with a parent.</param>
        public static IAsyncOperation<IList<TObject>> InstantiateAll<TObject>(object key, Action<IAsyncOperation<TObject>> callback, Transform parent = null, bool instantiateInWorldSpace = false)
            where TObject : Object
        {
            return InstantiateAll(key, callback, new InstantiationParameters(parent, instantiateInWorldSpace));
        }

        /// <summary>
        /// Instantiate multiple objects.
        /// </summary>
        /// <param name="key">The key of the location of the objects to instantiate.</param>
        /// <param name="callback">This callback will be invoked once for each object that is instantiated.</param>
        /// <param name="instantiateParameters">Parameters for instantiation.</param>
        public static IAsyncOperation<IList<TObject>> InstantiateAll<TObject>(object key, Action<IAsyncOperation<TObject>> callback, InstantiationParameters instantiateParameters)
            where TObject : Object
        {
            if (!InitializationOperation.IsDone)
                return AsyncOperationCache.Instance.Acquire<ChainOperation<IList<TObject>, bool>>().Start(InitializationOperation, (op) => InstantiateAll(key, callback, instantiateParameters)).Retain();

            IList<IResourceLocation> locations;
            if (!GetResourceLocations(key, out locations))
                throw new InvalidKeyException(key);

            return InstantiateAll(locations, callback, instantiateParameters);
        }

        /// <summary>
        /// Instantiate multiple objects.
        /// </summary>
        /// <param name="locations">The locations of the objects to instantiate.</param>
        /// <param name="callback">This callback will be invoked once for each object that is instantiated.</param>
        /// <param name="instantiateParameters">Parameters for instantiation.</param>
        public static IAsyncOperation<IList<TObject>> InstantiateAll<TObject>(IList<IResourceLocation> locations, Action<IAsyncOperation<TObject>> callback, InstantiationParameters instantiateParameters)
            where TObject : Object
        {
            var instOp = ResourceManager.ProvideInstances(locations, callback, instantiateParameters);
            (instOp as IAsyncOperation).Completed += s_recordInstanceListAction;
            return instOp;
        }

        /// <summary>
        /// Release instantiated object.
        /// </summary>
        /// <param name="instance">The instantiated object to release.</param>
        /// <param name="delay">The time delay in seconds to wait until releasing the instantiated object.  If this value is less than 0, it will release immediately.</param>
        public static void ReleaseInstance(Object instance, float delay = 0)
        {
            if (delay > 0)
            {
                DelayedActionManager.AddAction(s_releaseInstanceAction, delay, instance, 0);
                return;
            }
            if (instance == null)
            {
                Debug.LogWarning("ResourceManager.ReleaseInstance() - trying to release null instance");
                return;
            }
            if (s_currentFrame != Time.frameCount)
            {
                s_currentFrame = Time.frameCount;
                s_instancesReleasedInCurrentFrame.Clear();
            }

            //silently ignore multiple releases that occur in the same frame
            if (s_instancesReleasedInCurrentFrame.Contains(instance))
                return;

            var go = instance as GameObject;
            if (go == null)
            {
                Debug.LogWarning("ResourceManager.ReleaseInstance() - only GameObject types are supported");
                GameObject.Destroy(go);
                return;
            }
            
            s_instancesReleasedInCurrentFrame.Add(instance);
            IResourceLocation location;
            if (!s_instanceToLocationMap.TryGetValue(go, out location))
            {
                Debug.LogWarningFormat("ResourceManager.ReleaseInstance() - unable to find location for instance {0}.", instance.GetInstanceID());
                return;
            }

            if (!s_sceneToInstances[go.scene].Remove(go))
                Debug.LogWarningFormat("Instance {0} was not found in scene {1}.", go.GetInstanceID(), go.scene);
            if(!s_instanceToScene.Remove(go))
                Debug.LogWarningFormat("Instance {0} was not found instance->scene map.", go.GetInstanceID());


            s_instanceToLocationMap.Remove(go);
            ResourceManager.ReleaseInstance(go, location);
        }

        /// <summary>
        /// Load scene.
        /// </summary>
        /// <param name="key">The key of the location of the scene to load.</param>
        /// <param name="loadMode">Scene load mode.</param>
        public static IAsyncOperation<Scene> LoadScene(object key, LoadSceneMode loadMode = LoadSceneMode.Single)
        {
            if (!InitializationOperation.IsDone)
                return AsyncOperationCache.Instance.Acquire<ChainOperation<Scene, bool>>().Start(InitializationOperation, (op) => LoadScene(key, loadMode)).Retain();

            IList<IResourceLocation> locations;
            if (!GetResourceLocations(key, out locations))
                throw new InvalidKeyException(key);

            return LoadScene(locations[0], loadMode);
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

            var loadOp = ResourceManager.ProvideScene(location, loadMode);
            loadOp.Completed += (op) => s_sceneToLocationMap.Add(op.Result, location);
            return loadOp;
        }

        /// <summary>
        /// Unload scene.
        /// </summary>
        /// <param name="scene">The scene to unload.</param>
        public static IAsyncOperation<Scene> UnloadScene(Scene scene)
        {
            IResourceLocation location;
            if (!s_sceneToLocationMap.TryGetValue(scene, out location))
                throw new InvalidKeyException(string.Format("UnloadScene - unable to find location for scene {0}.", scene));

            s_sceneToLocationMap.Remove(scene);
            return ResourceManager.ReleaseScene(location, scene);
        }


        /// <summary>
        /// Notify the ResourceManager that a tracked instance has changed scenes so that it can be released properly when the scene is unloaded.
        /// </summary>
        /// <param name="gameObject">The gameobject that is being moved to a new scene.</param>
        /// <param name="previousScene">Previous scene for gameobject.</param>
        /// <param name="currentScene">Current scene for gameobject.</param>
        public static void RecordInstanceSceneChange(GameObject gameObject, Scene previousScene, Scene currentScene)
        {
            HashSet<GameObject> instanceIds = null;
            if (!s_sceneToInstances.TryGetValue(previousScene, out instanceIds))
                Debug.LogFormat("Unable to find instance table for instance {0}.", gameObject.GetInstanceID());
            else
                instanceIds.Remove(gameObject);
            if (!s_sceneToInstances.TryGetValue(currentScene, out instanceIds))
                s_sceneToInstances.Add(currentScene, instanceIds = new HashSet<GameObject>());
            instanceIds.Add(gameObject);

            s_instanceToScene[gameObject] = currentScene;
        }

        static void OnSceneUnloaded(Scene scene)
        {
            if (!Application.isPlaying)
                return;

            if (s_currentFrame != Time.frameCount)
            {
                s_currentFrame = Time.frameCount;
                s_instancesReleasedInCurrentFrame.Clear();
            }

            HashSet<GameObject> instances = null;
            if (s_sceneToInstances.TryGetValue(scene, out instances))
            {
                foreach (var go in instances)
                {
                    if (s_instancesReleasedInCurrentFrame.Contains(go))
                        continue;

                    IResourceLocation loc;
                    if (s_instanceToLocationMap.TryGetValue(go, out loc))
                    {
                        if (!s_instanceToScene.Remove(go))
                            Debug.LogWarningFormat("Scene not found for instance {0}", go);

                        s_instanceToLocationMap.Remove(go);
                        ResourceManager.ReleaseInstance(go, loc);
                    }
                    else
                    {
                        //object has already been released
                        Debug.LogWarningFormat("Object instance {0} has already been released.", go);
                    }
                }

                s_sceneToInstances.Remove(scene);
            }
        }

        static void ValidateSceneInstances()
        {
            var objectsThatNeedToBeFixed = new List<KeyValuePair<Scene, GameObject>>();
            foreach (var kvp in s_sceneToInstances)
            {
                foreach (var go in kvp.Value)
                {
                    if (go == null)
                    {
                        Debug.LogWarningFormat("GameObject instance has been destroyed, use ResourceManager.ReleaseInstance to ensure proper reference counts.");
                    }
                    else
                    {
                        if (go.scene != kvp.Key)
                        {
                            Debug.LogWarningFormat("GameObject instance {0} has been moved to from scene {1} to scene {2}.  When moving tracked instances, use ResourceManager.RecordInstanceSceneChange to ensure that reference counts are accurate.", go, kvp.Key, go.scene.GetHashCode());
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

