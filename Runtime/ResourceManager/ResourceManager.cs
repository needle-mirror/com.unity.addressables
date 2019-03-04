using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.Diagnostics;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.ResourceManagement.Util;
using UnityEngine.SceneManagement;

[assembly: InternalsVisibleTo("Unity.ResourceManager.Tests")]
namespace UnityEngine.ResourceManagement
{
    /// <summary>
    /// Entry point for ResourceManager API
    /// </summary>
    public class ResourceManager : IDisposable
    {
        /// <summary>
        /// Global exception handler.  This will be called whenever an IAsyncOperation.OperationException is set to a non-null value.
        /// </summary>
        public static Action<IAsyncOperation, Exception> ExceptionHandler { get; set; }

        internal bool CallbackHooksEnabled = true; // tests might need to disable the callback hooks to manually pump updating
        private MonoBehaviourCallbackHooks m_CallbackHooks;

        ListWithEvents<IResourceProvider> m_ResourceProviders = new ListWithEvents<IResourceProvider>();

        // list of all the providers in s_ResourceProviders that implement IUpdateReceiver
        ListWithEvents<IUpdateReceiver> m_UpdateReceivers = new ListWithEvents<IUpdateReceiver>();

        /// <summary>
        /// Gets or sets the <see cref="IInstanceProvider"/>. The instance provider handles instatiating and releasing prefabs.
        /// </summary>
        /// <value>The instance provider.</value>
        public IInstanceProvider InstanceProvider { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="ISceneProvider"/>. The scene provider handles load and release operations for scenes.
        /// </summary>
        /// <value>The scene provider.</value>
        public ISceneProvider SceneProvider { get; set; }

        /// <summary>
        /// Gets the list of configured <see cref="IResourceProvider"/> objects. Resource Providers handle load and release operations for <see cref="IResourceLocation"/> objects.
        /// </summary>
        /// <value>The resource providers list.</value>
        public IList<IResourceProvider> ResourceProviders { get { return m_ResourceProviders; } }

        public ResourceManager()
        {
            m_ResourceProviders.OnElementAdded += OnObjectAdded;
            m_ResourceProviders.OnElementRemoved += OnObjectRemoved;
            m_UpdateReceivers.OnElementAdded += x => RegisterForCallbacks();
        }

        private void OnObjectAdded(object obj)
        {
            IUpdateReceiver updateReceiver = obj as IUpdateReceiver;
            if (updateReceiver != null && updateReceiver.NeedsUpdate)
                m_UpdateReceivers.Add(updateReceiver);
        }

        private void OnObjectRemoved(object obj)
        {
            IUpdateReceiver updateReceiver = obj as IUpdateReceiver;
            if (updateReceiver != null)
                m_UpdateReceivers.Remove(updateReceiver);
        }

        private void RegisterForCallbacks()
        {
            if (CallbackHooksEnabled && m_CallbackHooks == null)
            {
                m_CallbackHooks = new GameObject("ResourceManagerCallbacks", typeof(MonoBehaviourCallbackHooks)).GetComponent<MonoBehaviourCallbackHooks>();
                m_CallbackHooks.OnUpdateDelegate += Update;
            }
        }

        /// <summary>
        /// Gets the appropriate <see cref="IResourceProvider"/> for the given <paramref name="location"/>.
        /// </summary>
        /// <returns>The resource provider.</returns>
        /// <param name="location">The resource location.</param>
        /// <typeparam name="TObject">The desired object type to be loaded from the provider.</typeparam>
        public IResourceProvider GetResourceProvider<TObject>(IResourceLocation location)
            where TObject : class
        {
            if (location == null)
                return null;

            for (int i = 0; i < ResourceProviders.Count; i++)
            {
                var p = ResourceProviders[i];
                if (p.CanProvide<TObject>(location))
                    return p;
            }
            return null;
        }

        IAsyncOperation<TObject> ProvideFunc<TObject>(IResourceLocation location, AsyncOperationStatus dependencyOperationStatus, IList<object> deps)
       where TObject : class
        {
            IResourceProvider provider = GetResourceProvider<TObject>(location);
            bool dependenciesFailed = dependencyOperationStatus != AsyncOperationStatus.Succeeded;
            if (location.HasDependencies)
            {
                dependenciesFailed = dependenciesFailed || location.Dependencies.Count != deps.Count;
                for (int i = 0; i < deps.Count; i++)
                    dependenciesFailed = dependenciesFailed || deps[i] == null;
            }

            bool canLoadWithFailedDepdnencies = (provider.BehaviourFlags & ProviderBehaviourFlags.CanProvideWithFailedDependencies) != 0;
            if (dependenciesFailed && !canLoadWithFailedDepdnencies)
            {
                var exception = new ResourceManagerException(string.Format("Cannot provide resource at {0}, because a dependency has failed to load", location.ToString()));
                return new CompletedOperation<TObject>().Start(location, location, default(TObject), exception);
            }
            return provider.Provide<TObject>(location, deps);
        }

        /// <summary>
        /// Load the <typeparamref name="TObject"/> at the specified <paramref name="location"/>.
        /// </summary>
        /// <returns>An async operation.</returns>
        /// <param name="location">Location to load.</param>
        /// <typeparam name="TObject">Object type to load.</typeparam>
        public IAsyncOperation<TObject> ProvideResource<TObject>(IResourceLocation location)
    where TObject : class
        {
            if (location == null)
                return new CompletedOperation<TObject>().Start(null, null, default(TObject), new ArgumentNullException("location"));

            var provider = GetResourceProvider<TObject>(location);
            if (provider == null)
                return new CompletedOperation<TObject>().Start(location, location, default(TObject), new UnknownResourceProviderException(location));

            if (location.HasDependencies)
            {
                ChainOperation<TObject, IList<object>> chainOp = AsyncOperationCache.Instance.Acquire<ChainOperation<TObject, IList<object>>>();
                bool canLoadWithFailedDepdnencies = (provider.BehaviourFlags & ProviderBehaviourFlags.CanProvideWithFailedDependencies) != 0;
                chainOp.Start(location, location, LoadDependencies(location), op => ProvideFunc<TObject>(location, op.Status, op.Result), canLoadWithFailedDepdnencies);
                chainOp.Retain();
                return chainOp;
            }
            else
            {
                var op = provider.Provide<TObject>(location, null);
                op.Retain();
                return op;
            }
            
        }

        /// <summary>
        /// Asynchronously load all objects in the given collection of <paramref name="locations"/>.
        /// </summary>
        /// <returns>An async operation that will complete when all individual async load operations are complete.</returns>
        /// <param name="locations">locations to load.</param>
        /// <param name="callback">This callback will be invoked once for each object that is loaded.</param>
        /// <typeparam name="TObject">Object type to load.</typeparam>
        public IAsyncOperation<IList<TObject>> ProvideResources<TObject>(IList<IResourceLocation> locations, Action<IAsyncOperation<TObject>> callback)
            where TObject : class
        {
            if (locations == null)
                return new CompletedOperation<IList<TObject>>().Start(null, locations, null, new ArgumentNullException("locations"));
            return AsyncOperationCache.Instance.Acquire<GroupOperation<TObject>>().Start(locations, callback, ProvideResource<TObject>).Retain();
        }

        /// <summary>
        /// Release resources belonging to the <paramref name="asset"/> at the specified <paramref name="location"/>.
        /// </summary>
        /// <param name="asset">Object to release.</param>
        /// <param name="location">The location of the resource to release.</param>
        /// <typeparam name="TObject">Object type.</typeparam>
        public void ReleaseResource<TObject>(TObject asset, IResourceLocation location)
            where TObject : class
        {
            if (location == null)
                return;
            var provider = GetResourceProvider<TObject>(location);
            if (provider == null)
                return;
            ResourceManagerEventCollector.PostEvent(ResourceManagerEventCollector.EventType.Release, location, Time.frameCount);
            provider.Release(location, asset);
            if (location.HasDependencies)
            {
                foreach (var dep in location.Dependencies)
                    ReleaseResource<object>(null, dep);
            }
        }

        IAsyncOperation<TObject> ProvideInstanceFunc<TObject>(IInstanceProvider instanceProvider, IResourceProvider provider, InstantiationParameters instantiateParameters, IResourceLocation location, AsyncOperationStatus dependencyOperationStatus, IList<object> deps)
where TObject : Object
        {
            bool dependenciesFailed = dependencyOperationStatus != AsyncOperationStatus.Succeeded;
            if (location.HasDependencies)
            {
                dependenciesFailed = dependenciesFailed || location.Dependencies.Count != deps.Count;
                for (int i = 0; i < deps.Count; i++)
                    dependenciesFailed = dependenciesFailed || deps[i] == null;
            }

            bool canLoadWithFailedDepdnencies = false; // TODO: Could allow instance provider to proceed when load fails (provider.BehaviourFlags & ProviderBehaviourFlags.CanProvideWithFailedDependencies) != 0;
            if (dependenciesFailed && !canLoadWithFailedDepdnencies)
            {
                var exception = new ResourceManagerException(string.Format("Cannot provide resource at {0}, because a dependency has failed to load", location.ToString()));
                return new CompletedOperation<TObject>().Start(location, location, default(TObject), exception);
            }
            IAsyncOperation<TObject> op = instanceProvider.ProvideInstanceAsync<TObject>(provider, location, deps, instantiateParameters);
            return op;
        }

        /// <summary>
        /// Asynchronouslly instantiate a prefab (GameObject) at the specified <paramref name="location"/>.
        /// </summary>
        /// <returns>Async operation that will complete when the prefab is instantiated.</returns>
        /// <param name="location">location of the prefab.</param>
        /// <param name="instantiateParameters">A struct containing the parameters to pass the the Instantiation call.</param>
        /// <typeparam name="TObject">Instantiated object type.</typeparam>
        public IAsyncOperation<TObject> ProvideInstance<TObject>(IResourceLocation location, InstantiationParameters instantiateParameters)
            where TObject : Object
        {
            if (InstanceProvider == null)
                throw new NullReferenceException("ResourceManager.InstanceProvider is null.  Assign a valid IInstanceProvider object before using.");

            if (location == null)
                return new CompletedOperation<TObject>().Start(null, null, default(TObject), new ArgumentNullException("location"));
            var provider = GetResourceProvider<TObject>(location);
            if (provider == null)
                return new CompletedOperation<TObject>().Start(location, location, default(TObject), new UnknownResourceProviderException(location));

            ResourceManagerEventCollector.PostEvent(ResourceManagerEventCollector.EventType.InstantiateAsyncRequest, location, Time.frameCount);
            ResourceManagerEventCollector.PostEvent(ResourceManagerEventCollector.EventType.LoadAsyncRequest, location, Time.frameCount);

            if (location.HasDependencies)
            {
                ChainOperation<TObject, IList<object>> chainOp = AsyncOperationCache.Instance.Acquire<ChainOperation<TObject, IList<object>>>();
                chainOp.Start(location, location, LoadDependencies(location), op => ProvideInstanceFunc<TObject>(InstanceProvider, provider, instantiateParameters, location, op.Status, op.Result));
                chainOp.Retain();
                return chainOp;
            }
            else
            {
                IAsyncOperation<TObject> op = InstanceProvider.ProvideInstanceAsync<TObject>(provider, location, null, instantiateParameters);
                op.Retain();
                return op;
            }
        }

        /// <summary>
        /// Asynchronously instantiate multiple prefabs (GameObjects) at the specified <paramref name="locations"/>.
        /// </summary>
        /// <returns>Async operation that will complete when the prefab is instantiated.</returns>
        /// <param name="locations">locations of prefab asset</param>
        /// <param name="callback">This is called for each instantiated object.</param>
        /// <param name="instantiateParameters">A struct containing the parameters to pass the the Instantiation call.</param>
        /// <typeparam name="TObject">Instantiated object type.</typeparam>
        public IAsyncOperation<IList<TObject>> ProvideInstances<TObject>(IList<IResourceLocation> locations, Action<IAsyncOperation<TObject>> callback, InstantiationParameters instantiateParameters)
            where TObject : Object
        {
            if (InstanceProvider == null)
                throw new NullReferenceException("ResourceManager.InstanceProvider is null.  Assign a valid IInstanceProvider object before using.");

            if (locations == null)
                return new CompletedOperation<IList<TObject>>().Start(null, locations, null, new ArgumentNullException("locations"));

            return AsyncOperationCache.Instance.Acquire<GroupOperation<TObject>>().Start(locations, callback, ProvideInstance<TObject>, instantiateParameters).Retain();
        }

        /// <summary>
        /// Releases resources belonging to the prefab instance.
        /// </summary>
        /// <param name="instance">Instance to release.</param>
        /// <param name="location">The location of the instance.</param>
        public void ReleaseInstance(Object instance, IResourceLocation location)
        {
            if (InstanceProvider == null)
                throw new NullReferenceException("ResourceManager.InstanceProvider is null.  Assign a valid IInstanceProvider object before using.");
            if (location == null)
                return;

            ResourceManagerEventCollector.PostEvent(ResourceManagerEventCollector.EventType.ReleaseInstance, location, Time.frameCount);
            if (InstanceProvider.ReleaseInstance(GetResourceProvider<Object>(location), location, instance))
                ReleaseResource<object>(null, location);
        }

        /// <summary>
        /// Asynchronously loads the scene a the given <paramref name="location"/>.
        /// </summary>
        /// <returns>Async operation for the scene.</returns>
        /// <param name="location">location of the scene to load.</param>
        /// <param name="loadMode">Scene Load mode.</param>
        public IAsyncOperation<Scene> ProvideScene(IResourceLocation location, LoadSceneMode loadMode = LoadSceneMode.Single)
        {
            if (SceneProvider == null)
                throw new NullReferenceException("ResourceManager.SceneProvider is null.  Assign a valid ISceneProvider object before using.");
            if (location == null)
                return new CompletedOperation<Scene>().Start(null, null, default(Scene), new ArgumentNullException("location"));

            ResourceManagerEventCollector.PostEvent(ResourceManagerEventCollector.EventType.LoadSceneAsyncRequest, location, 1);
            ResourceManagerEventCollector.PostEvent(ResourceManagerEventCollector.EventType.CacheEntryLoadPercent, location, 0);

            return SceneProvider.ProvideSceneAsync(location, LoadDependencies(location), loadMode).Retain();
        }

        /// <summary>
        /// Asynchronously unloads the scene.
        /// </summary>
        /// <param name="scene">The scene to unload.</param>
        /// <param name="location">key of the scene to unload.</param>
        /// <returns>Async operation for the scene unload.</returns>
        public IAsyncOperation<Scene> ReleaseScene(Scene scene, IResourceLocation location)
        {
            if (SceneProvider == null)
                throw new NullReferenceException("ResourceManager.SceneProvider is null.  Assign a valid ISceneProvider object before using.");
            if (location == null)
                return new CompletedOperation<Scene>().Start(null, null, default(Scene), new ArgumentNullException("location"));
            return SceneProvider.ReleaseSceneAsync(location, scene).Retain();
        }

        /// <summary>
        /// Asynchronously unloads the scene.
        /// </summary>
        /// <param name="location">The location of the scene to unload.</param>
        /// <param name="scene">The scene to unload.</param>
        /// <returns>Async operation for the scene unload.</returns>
        [Obsolete("Use ReleaseScene(Scene scene, IResourceLocation location) instead.  The parameter order has been changed to be consistent with other ResourceManager API.")]
        public IAsyncOperation<Scene> ReleaseScene(IResourceLocation location, Scene scene)
        {
            return ReleaseScene(scene, location);
        }

        /// <summary>
        /// Asynchronously dependencies of a location.
        /// </summary>
        /// <returns>Async operation for the dependency loads.</returns>
        /// <param name="location">location to load dependencies for.</param>
        public IAsyncOperation<IList<object>> LoadDependencies(IResourceLocation location)
        {
            if (location == null || !location.HasDependencies)
                return null;
            return ProvideResources<object>(location.Dependencies, null);
        }

        internal void Update(float unscaledDeltaTime)
        {
            for(int i = 0; i < m_UpdateReceivers.Count; i++ )
                m_UpdateReceivers[i].Update(unscaledDeltaTime);
        }

        /// <summary>
        /// Disposes internal resources used by the resource manager
        /// </summary>
        public void Dispose()
        {
            if (m_CallbackHooks != null)
                GameObject.Destroy(m_CallbackHooks.gameObject);
            m_CallbackHooks = null;
        }
    }
}
