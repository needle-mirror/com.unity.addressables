using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine.Networking;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.Diagnostics;
using UnityEngine.ResourceManagement.Exceptions;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.ResourceManagement.Util;
using UnityEngine.SceneManagement;

[assembly: InternalsVisibleTo("Unity.ResourceManager.Tests")]
[assembly: InternalsVisibleTo("Unity.Addressables.Tests")]
[assembly: InternalsVisibleTo("Unity.Addressables")]
#if UNITY_EDITOR
[assembly: InternalsVisibleTo("Unity.Addressables.Editor")]
#endif

namespace UnityEngine.ResourceManagement
{
    /// <summary>
    /// Entry point for ResourceManager API
    /// </summary>
    public class ResourceManager : IDisposable
    {
        /// <summary>
        /// Event types that will be sent by the ResourceManager
        /// </summary>
        public enum DiagnosticEventType
        {
            AsyncOperationFail,
            AsyncOperationCreate,
            AsyncOperationPercentComplete,
            AsyncOperationComplete,
            AsyncOperationReferenceCount,
            AsyncOperationDestroy,
        }

        /// <summary>
        /// Container for information associated with a Diagnostics event.
        /// </summary>
        public struct DiagnosticEventContext
        {
            /// <summary>
            /// Operation handle for the event.
            /// </summary>
            public AsyncOperationHandle OperationHandle { get; }

            /// <summary>
            /// The type of diagnostic event.
            /// </summary>
            public DiagnosticEventType Type { get; }

            /// <summary>
            /// The value for this event.
            /// </summary>
            public int EventValue { get; }

            /// <summary>
            /// The IResourceLocation being provided by the operation triggering this event.
            /// This value is null if the event is not while providing a resource. 
            /// </summary>
            public IResourceLocation Location { get; }

            /// <summary>
            /// Addition data included with this event.
            /// </summary>
            public object Context { get; }

            /// <summary>
            /// Any error that occured.
            /// </summary>
            public string Error { get; }

            /// <summary>
            /// Construct a new DiagnosticEventContext.
            /// </summary>
            /// <param name="op">Operation handle for the event.</param>
            /// <param name="type">The type of diagnostic event.</param>
            /// <param name="eventValue">The value for this event.</param>
            /// <param name="error">Any error that occured.</param>
            /// <param name="context">Additional context data.</param>
            public DiagnosticEventContext(AsyncOperationHandle op, DiagnosticEventType type, int eventValue = 0, string error = null, object context = null)
            {
                OperationHandle = op;
                Type = type;
                EventValue = eventValue;
                Location = op.m_InternalOp != null && op.m_InternalOp is IGenericProviderOperation gen
                    ? gen.Location
                    : null;
                Error = error;
                Context = context;
            }
        }

        /// <summary>
        /// Global exception handler.  This will be called whenever an IAsyncOperation.OperationException is set to a non-null value.
        /// </summary>
        public static Action<AsyncOperationHandle, Exception> ExceptionHandler { get; set; }

        /// <summary>
        /// Functor to transform internal ids before being used by the providers.
        /// </summary>
        public Func<IResourceLocation, string> InternalIdTransformFunc { get; set; }

        /// <summary>
        /// Checks for an internal id transform function and uses it to modify the internal id value.
        /// </summary>
        /// <param name="location">The location to transform the internal id of.</param>
        /// <returns>If a transform func is set, use it to pull the local id. otheriwse the InternalId property of the location is used.</returns>
        public string TransformInternalId(IResourceLocation location)
        {
            return InternalIdTransformFunc == null ? location.InternalId : InternalIdTransformFunc(location);
        }

        internal bool CallbackHooksEnabled = true; // tests might need to disable the callback hooks to manually pump updating
        private MonoBehaviourCallbackHooks m_CallbackHooks;

        ListWithEvents<IResourceProvider> m_ResourceProviders = new ListWithEvents<IResourceProvider>();
        IAllocationStrategy m_allocator;

        // list of all the providers in s_ResourceProviders that implement IUpdateReceiver
        ListWithEvents<IUpdateReceiver> m_UpdateReceivers = new ListWithEvents<IUpdateReceiver>();
        List<IUpdateReceiver> m_UpdateReceiversToRemove = null;
        bool m_UpdatingReceivers = false;
        internal int OperationCacheCount { get { return m_AssetOperationCache.Count; } }
        internal int InstanceOperationCount { get { return m_TrackedInstanceOperations.Count; } }
        //cache of type + providerId to IResourceProviders for faster lookup
        Dictionary<int, IResourceProvider> m_providerMap = new Dictionary<int, IResourceProvider>();
        Dictionary<int, IAsyncOperation> m_AssetOperationCache = new Dictionary<int, IAsyncOperation>();
        HashSet<InstanceOperation> m_TrackedInstanceOperations = new HashSet<InstanceOperation>();
        DelegateList<float> m_UpdateCallbacks = DelegateList<float>.CreateWithGlobalCache();
        List<IAsyncOperation> m_DeferredCompleteCallbacks = new List<IAsyncOperation>();

        Action<AsyncOperationHandle, DiagnosticEventType, int, object> m_obsoleteDiagnosticsHandler; // For use in working with Obsolete RegisterDiagnosticCallback method.
        Action<DiagnosticEventContext> m_diagnosticsHandler;
        Action<IAsyncOperation> m_ReleaseOpNonCached;
        Action<IAsyncOperation> m_ReleaseOpCached;
        Action<IAsyncOperation> m_ReleaseInstanceOp;
        static int s_GroupOperationTypeHash = typeof(GroupOperation).GetHashCode();
        static int s_InstanceOperationTypeHash = typeof(InstanceOperation).GetHashCode();

        /// <summary>
        /// Add an update reveiver. 
        /// </summary>
        /// <param name="receiver">The object to add. The Update method will be called until the object is removed. </param>
        public void AddUpdateReceiver(IUpdateReceiver receiver)
        {
            if (receiver == null)
                return;
            m_UpdateReceivers.Add(receiver);
        }

        /// <summary>
        /// Remove update receiver.
        /// </summary>
        /// <param name="receiver">The object to remove.</param>
        public void RemoveUpdateReciever(IUpdateReceiver receiver)
        {
            if (receiver == null)
                return;

            if (m_UpdatingReceivers)
            {
                if (m_UpdateReceiversToRemove == null)
                    m_UpdateReceiversToRemove = new List<IUpdateReceiver>();
                m_UpdateReceiversToRemove.Add(receiver);
            }
            else
            {
                m_UpdateReceivers.Remove(receiver);
            }
        }

        /// <summary>
        /// The allocation strategy object.
        /// </summary>
        public IAllocationStrategy Allocator { get { return m_allocator; } set { m_allocator = value; } }
        
        /// <summary>
        /// Gets the list of configured <see cref="IResourceProvider"/> objects. Resource Providers handle load and release operations for <see cref="IResourceLocation"/> objects.
        /// </summary>
        /// <value>The resource providers list.</value>
        public IList<IResourceProvider> ResourceProviders { get { return m_ResourceProviders; } }
        
        /// <summary>
        /// The CertificateHandler instance object.
        /// </summary>
        public CertificateHandler CertificateHandlerInstance { get; set; }

        /// <summary>
        /// Constructor for the resource manager.
        /// </summary>
        /// <param name="alloc">The allocation strategy to use.</param>
        public ResourceManager(IAllocationStrategy alloc = null)
        {
            m_ReleaseOpNonCached = OnOperationDestroyNonCached;
            m_ReleaseOpCached = OnOperationDestroyCached;
            m_ReleaseInstanceOp = OnInstanceOperationDestroy;
            m_allocator = alloc == null ? new LRUCacheAllocationStrategy(1000, 1000, 100, 10) : alloc;
            m_ResourceProviders.OnElementAdded += OnObjectAdded;
            m_ResourceProviders.OnElementRemoved += OnObjectRemoved;
            m_UpdateReceivers.OnElementAdded += x => RegisterForCallbacks();
        }

        private void OnObjectAdded(object obj)
        {
            IUpdateReceiver updateReceiver = obj as IUpdateReceiver;
            if (updateReceiver != null)
                AddUpdateReceiver(updateReceiver);
        }

        private void OnObjectRemoved(object obj)
        {
            IUpdateReceiver updateReceiver = obj as IUpdateReceiver;
            if (updateReceiver != null)
                RemoveUpdateReciever(updateReceiver);
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
        /// Clears out the diagnostics callback handler.
        /// </summary>
        [Obsolete("ClearDiagnosticsCallback is Obsolete, use ClearDiagnosticCallbacks instead.")]
        public void ClearDiagnosticsCallback()
        {
            m_diagnosticsHandler = null;
            m_obsoleteDiagnosticsHandler = null;
        }

        /// <summary>
        /// Clears out the diagnostics callbacks handler.
        /// </summary>
        public void ClearDiagnosticCallbacks()
        {
            m_diagnosticsHandler = null;
            m_obsoleteDiagnosticsHandler = null;
        }

        /// <summary>
        /// Unregister a handler for diagnostic events.
        /// </summary>
        /// <param name="func">The event handler function.</param>
        public void UnregisterDiagnosticCallback(Action<DiagnosticEventContext> func)
        {
            if (m_diagnosticsHandler != null)
                m_diagnosticsHandler -= func;
            else
                Debug.LogError("No Diagnostic callbacks registered, cannot remove callback.");
        }

        /// <summary>
        /// Register a handler for diagnostic events.
        /// </summary>
        /// <param name="func">The event handler function.</param>
        [Obsolete]
        public void RegisterDiagnosticCallback(Action<AsyncOperationHandle, ResourceManager.DiagnosticEventType, int, object> func)
        {
            m_obsoleteDiagnosticsHandler = func;
        }

        /// <summary>
        /// Register a handler for diagnostic events.
        /// </summary>
        /// <param name="func">The event handler function.</param>
        public void RegisterDiagnosticCallback(Action<DiagnosticEventContext> func)
        {
            m_diagnosticsHandler += func;
        }

        internal void PostDiagnosticEvent(DiagnosticEventContext context)
        {
            m_diagnosticsHandler?.Invoke(context);

            if (m_obsoleteDiagnosticsHandler == null)
                return;
            m_obsoleteDiagnosticsHandler(context.OperationHandle, context.Type, context.EventValue, string.IsNullOrEmpty(context.Error) ? context.Context : context.Error);
        }

        /// <summary>
        /// Gets the appropriate <see cref="IResourceProvider"/> for the given <paramref name="location"/> and <paramref name="type"/>.
        /// </summary>
        /// <returns>The resource provider. Or null if an appropriate provider cannot be found</returns>
        /// <param name="location">The resource location.</param>
        /// <param name="type">The desired object type to be loaded from the provider.</param>
        public IResourceProvider GetResourceProvider(Type t, IResourceLocation location)
        {
            if (location != null)
            {
                IResourceProvider prov = null;
                var hash = location.ProviderId.GetHashCode() * 31 + (t == null ? 0 : t.GetHashCode());
                if (!m_providerMap.TryGetValue(hash, out prov))
                {
                    for (int i = 0; i < ResourceProviders.Count; i++)
                    {
                        var p = ResourceProviders[i];
                        if (p.ProviderId.Equals(location.ProviderId, StringComparison.Ordinal) && (t == null || p.CanProvide(t, location)))
                        {
                            m_providerMap.Add(hash, prov = p);
                            break;
                        }
                    }
                }
                return prov;
            }
            return null;
        }

        Type GetDefaultTypeForLocation(IResourceLocation loc)
        {
            var provider = GetResourceProvider(null, loc);
            if (provider == null)
                return typeof(object);
            Type t = provider.GetDefaultType(loc);
            return t != null ? t : typeof(object);
        }

        private int CalculateLocationsHash(IList<IResourceLocation> locations, Type t = null)
        {
            if (locations == null || locations.Count == 0)
                return 0;
            int hash = 17;
            foreach (var loc in locations)
            {
                Type t2 = t != null ? t : GetDefaultTypeForLocation(loc);
                hash = hash * 31 + loc.Hash(t2);
            }
            return hash;
        }

        /// <summary>
        /// Load the <typeparamref name="TObject"/> at the specified <paramref name="location"/>.
        /// </summary>
        /// <returns>An async operation.</returns>
        /// <param name="location">Location to load.</param>
        /// <typeparam name="TObject">Object type to load.</typeparam>
        private AsyncOperationHandle ProvideResource(IResourceLocation location, Type desiredType = null)
        {
            if (location == null)
                throw new ArgumentNullException("location");
            IResourceProvider provider = null;
            if (desiredType == null)
            {
                provider = GetResourceProvider(desiredType, location);
                if (provider == null)
                    return CreateCompletedOperation<object>(null, new UnknownResourceProviderException(location).Message);
                desiredType = provider.GetDefaultType(location);
            }

            IAsyncOperation op;
            int hash = location.Hash(desiredType);
            if (m_AssetOperationCache.TryGetValue(hash, out op))
            {
                op.IncrementReferenceCount();
                return new AsyncOperationHandle(op);
            }

            Type provType;
            if (!m_ProviderOperationTypeCache.TryGetValue(desiredType, out provType))
                m_ProviderOperationTypeCache.Add(desiredType, provType = typeof(ProviderOperation<>).MakeGenericType(new Type[] { desiredType }));
            op = CreateOperation<IAsyncOperation>(provType, provType.GetHashCode(), hash, m_ReleaseOpCached);

            // Calculate the hash of the dependencies
            int depHash = location.DependencyHashCode;
            var depOp = location.HasDependencies ? ProvideResourceGroupCached(location.Dependencies, depHash, null, null) : default(AsyncOperationHandle<IList<AsyncOperationHandle>>);
            if (provider == null)
                provider = GetResourceProvider(desiredType, location);

            ((IGenericProviderOperation)op).Init(this, provider, location, depOp);

            var handle = StartOperation(op, depOp);

            if (depOp.IsValid())
                depOp.Release();

            return handle;
        }
        Dictionary<Type, Type> m_ProviderOperationTypeCache = new Dictionary<Type, Type>();

        /// <summary>
        /// Load the <typeparamref name="TObject"/> at the specified <paramref name="location"/>.
        /// </summary>
        /// <returns>An async operation.</returns>
        /// <param name="location">Location to load.</param>
        /// <typeparam name="TObject">Object type to load.</typeparam>
        public AsyncOperationHandle<TObject> ProvideResource<TObject>(IResourceLocation location)
        {
            AsyncOperationHandle handle = ProvideResource(location, typeof(TObject));
            return handle.Convert<TObject>();
        }

        /// <summary>
        /// Registers an operation with the ResourceManager. The operation will be executed when the <paramref name="dependency"/> completes.
        /// This should only be used when creating custom operations.
        /// </summary>
        /// <returns>The AsyncOperationHandle used to access the result and status of the operation.</returns>
        /// <param name="operation">The custom AsyncOperationBase object</param>
        /// <param name="dependency">Execution of the operation will not occur until this handle completes. A default handle can be passed if no dependency is required.</param>
        /// <typeparam name="TObject">Object type associated with this operation.</typeparam>
        public AsyncOperationHandle<TObject> StartOperation<TObject>(AsyncOperationBase<TObject> operation, AsyncOperationHandle dependency)
        {
            operation.Start(this, dependency, m_UpdateCallbacks);
            return operation.Handle;
        }

        internal AsyncOperationHandle StartOperation(IAsyncOperation operation, AsyncOperationHandle dependency)
        {
            operation.Start(this, dependency, m_UpdateCallbacks);
            return operation.Handle;
        }

        class CompletedOperation<TObject> : AsyncOperationBase<TObject>
        {
            bool m_Success;
            string m_ErrorMsg;
            public CompletedOperation() { }
            public void Init(TObject result, bool success, string errorMsg)
            {
                Result = result;
                m_Success = success;
                m_ErrorMsg = errorMsg;
            }
            protected override void Execute()
            {
                Complete(Result, m_Success, m_ErrorMsg);
            }
        }

        void OnInstanceOperationDestroy(IAsyncOperation o)
        {
            m_TrackedInstanceOperations.Remove(o as InstanceOperation);
            Allocator.Release(o.GetType().GetHashCode(), o);
        }

        void OnOperationDestroyNonCached(IAsyncOperation o)
        {
            Allocator.Release(o.GetType().GetHashCode(), o);
        }

        void OnOperationDestroyCached(IAsyncOperation o)
        {
            Allocator.Release(o.GetType().GetHashCode(), o);
            var cachable = o as ICachable;
            if (cachable != null)
                RemoveOperationFromCache(cachable.Hash);
        }

        internal T CreateOperation<T>(Type actualType, int typeHash, int operationHash, Action<IAsyncOperation> onDestroyAction) where T : IAsyncOperation
        {
            if (operationHash == 0)
            {
                var op = (T)Allocator.New(actualType, typeHash);
                op.OnDestroy = onDestroyAction;
                return op;
            }
            else
            {
                var op = (T)Allocator.New(actualType, typeHash);
                op.OnDestroy = onDestroyAction;
                var cachable = op as ICachable;
                if (cachable != null)
                    cachable.Hash = operationHash;
                AddOperationToCache(operationHash, op);
                return op;
            }
        }

        internal void AddOperationToCache(int hash, IAsyncOperation operation)
        {
            if(!IsOperationCached(hash))
                m_AssetOperationCache.Add(hash, operation);
        }

        internal bool RemoveOperationFromCache(int hash)
        {
            if (!IsOperationCached(hash))
                return true;

            return m_AssetOperationCache.Remove(hash);
        }

        internal bool IsOperationCached(int hash)
        {
            return m_AssetOperationCache.ContainsKey(hash);
        }

        internal int CachedOperationCount()
        {
            return m_AssetOperationCache.Count;
        }

        /// <summary>
        /// Creates an operation that has already completed with a specified result and error message./>.
        /// </summary>
        /// <param name="result">The result that the operation will provide.</param>
        /// <param name="errorMsg">The error message if the operation should be in the failed state. Otherwise null or empty string.</param>
        /// <typeparam name="TObject">Object type.</typeparam>
        public AsyncOperationHandle<TObject> CreateCompletedOperation<TObject>(TObject result, string errorMsg)
        {
            var cop = CreateOperation<CompletedOperation<TObject>>(typeof(CompletedOperation<TObject>), typeof(CompletedOperation<TObject>).GetHashCode(), 0, null);
            cop.Init(result, string.IsNullOrEmpty(errorMsg), errorMsg);
            return StartOperation(cop, default(AsyncOperationHandle));
        }

        internal AsyncOperationHandle<TObject> CreateCompletedOperation<TObject>(TObject result, bool success, string errorMsg)
        {
            var cop = CreateOperation<CompletedOperation<TObject>>(typeof(CompletedOperation<TObject>), typeof(CompletedOperation<TObject>).GetHashCode(), 0, null);
            cop.Init(result, success, errorMsg);
            return StartOperation(cop, default(AsyncOperationHandle));
        }

        /// <summary>
        /// Release the operation associated with the specified handle
        /// </summary>
        /// <param name="handle">The handle to release.</param>
        public void Release(AsyncOperationHandle handle)
        {
            handle.Release();
        }
        /// <summary>
        /// Increment reference count of operation handle.
        /// </summary>
        /// <param name="handle">The handle to the resource to increment the reference count for.</param>
        public void Acquire(AsyncOperationHandle handle)
        {
            handle.Acquire();
        }

        private GroupOperation AcquireGroupOpFromCache(int hash)
        {
            IAsyncOperation opGeneric;
            if (m_AssetOperationCache.TryGetValue(hash, out opGeneric))
            {
                opGeneric.IncrementReferenceCount();
                return (GroupOperation)opGeneric;
            }
            return null;
        }

        /// <summary>
        /// Create a group operation for a set of locations.
        /// </summary>
        /// <typeparam name="T">The expected object type for the operations.</typeparam>
        /// <param name="locations">The list of locations to load.</param>
        /// <returns>The operation for the entire group.</returns>
        public AsyncOperationHandle<IList<AsyncOperationHandle>> CreateGroupOperation<T>(IList<IResourceLocation> locations)
        {
            var op = CreateOperation<GroupOperation>(typeof(GroupOperation), s_GroupOperationTypeHash, 0, m_ReleaseOpNonCached);
            var ops = new List<AsyncOperationHandle>(locations.Count);
            foreach (var loc in locations)
                ops.Add(ProvideResource<T>(loc));

            op.Init(ops);
            return StartOperation(op, default);
        }

        /// <summary>
        /// Create a group operation for a set of AsyncOperationHandles
        /// </summary>
        /// <param name="operations">The list of operations that need to complete.</param>
        /// <param name="releasedCachedOpOnComplete">Determine if the cached operation should be released or not.</param>
        /// <returns>The operation for the entire group</returns>
        public AsyncOperationHandle<IList<AsyncOperationHandle>> CreateGenericGroupOperation(List<AsyncOperationHandle> operations, bool releasedCachedOpOnComplete = false)
        {
            var op = CreateOperation<GroupOperation>(typeof(GroupOperation), s_GroupOperationTypeHash, operations.GetHashCode(), releasedCachedOpOnComplete ? m_ReleaseOpCached : m_ReleaseOpNonCached);
            op.Init(operations);
            return StartOperation(op, default);
        }

        internal AsyncOperationHandle<IList<AsyncOperationHandle>> ProvideResourceGroupCached(IList<IResourceLocation> locations, int groupHash, Type desiredType, Action<AsyncOperationHandle> callback)
        {
            GroupOperation op = AcquireGroupOpFromCache(groupHash);
            AsyncOperationHandle<IList<AsyncOperationHandle>> handle;
            if (op == null)
            {
                op = CreateOperation<GroupOperation>(typeof(GroupOperation), s_GroupOperationTypeHash, groupHash, m_ReleaseOpCached);
                var ops = new List<AsyncOperationHandle>(locations.Count);
                foreach (var loc in locations)
                    ops.Add(ProvideResource(loc, desiredType));

                op.Init(ops);

                handle = StartOperation(op, default(AsyncOperationHandle));
            }
            else
            {
                handle = op.Handle;
            }

            if (callback != null)
            {
                var depOps = op.GetDependentOps();
                for (int i = 0; i < depOps.Count; i++)
                {
                    depOps[i].Completed += callback;
                }
            }

            return handle;
        }

        /// <summary>
        /// Asynchronously load all objects in the given collection of <paramref name="locations"/>.
        /// </summary>
        /// <returns>An async operation that will complete when all individual async load operations are complete.</returns>
        /// <param name="locations">locations to load.</param>
        /// <param name="callback">This callback will be invoked once for each object that is loaded.</param>
        /// <typeparam name="TObject">Object type to load.</typeparam>
        public AsyncOperationHandle<IList<TObject>> ProvideResources<TObject>(IList<IResourceLocation> locations, Action<TObject> callback = null)
        {
            if (locations == null)
                return CreateCompletedOperation<IList<TObject>>(null, "Null Location");

            Action<AsyncOperationHandle> callbackGeneric = null;
            if (callback != null)
            {
                callbackGeneric = (x) => callback((TObject)(x.Result));
            }
            var typelessHandle = ProvideResourceGroupCached(locations, CalculateLocationsHash(locations, typeof(TObject)), typeof(TObject), callbackGeneric);
            var chainOp = CreateChainOperation(typelessHandle, (x) =>
            {
                if (x.Status != AsyncOperationStatus.Succeeded)
                    return CreateCompletedOperation<IList<TObject>>(null, x.OperationException != null ? x.OperationException.Message : "ProvidResources failed");

                var list = new List<TObject>();
                foreach (var r in x.Result)
                    list.Add(r.Convert<TObject>().Result);
                return CreateCompletedOperation<IList<TObject>>(list, string.Empty);
            });
            // chain operation holds the dependency
            typelessHandle.Release();
            return chainOp;
        }

        /// <summary>
        /// Create a chain operation to handle dependencies.
        /// </summary>
        /// <typeparam name="TObject">The type of operation handle to return.</typeparam>
        /// <typeparam name="TObjectDependency">The type of the dependency operation.</typeparam>
        /// <param name="dependentOp">The dependency operation.</param>
        /// <param name="callback">The callback method that will create the dependent operation from the dependency operation.</param>
        /// <returns>The operation handle.</returns>
        public AsyncOperationHandle<TObject> CreateChainOperation<TObject, TObjectDependency>(AsyncOperationHandle<TObjectDependency> dependentOp, Func<AsyncOperationHandle<TObjectDependency>, AsyncOperationHandle<TObject>> callback)
        {
            var op = CreateOperation<ChainOperation<TObject, TObjectDependency>>(typeof(ChainOperation<TObject, TObjectDependency>), typeof(ChainOperation<TObject, TObjectDependency>).GetHashCode(), 0, null);
            op.Init(dependentOp, callback);
            return StartOperation(op, dependentOp);
        }
        /// <summary>
        /// Create a chain operation to handle dependencies.
        /// </summary>
        /// <typeparam name="TObject">The type of operation handle to return.</typeparam>
        /// <param name="dependentOp">The dependency operation.</param>
        /// <param name="callback">The callback method that will create the dependent operation from the dependency operation.</param>
        /// <returns>The operation handle.</returns>
        public AsyncOperationHandle<TObject> CreateChainOperation<TObject>(AsyncOperationHandle dependentOp, Func<AsyncOperationHandle, AsyncOperationHandle<TObject>> callback)
        {
            var cOp = new ChainOperationTypelessDepedency<TObject>();
            cOp.Init(dependentOp, callback);
            return StartOperation(cOp, dependentOp);
        }
        internal class InstanceOperation : AsyncOperationBase<GameObject>
        {
            AsyncOperationHandle<GameObject> m_dependency;
            InstantiationParameters m_instantiationParams;
            IInstanceProvider m_instanceProvider;
            GameObject m_instance;
            Scene m_scene;

            public void Init(ResourceManager rm, IInstanceProvider instanceProvider, InstantiationParameters instantiationParams, AsyncOperationHandle<GameObject> dependency)
            {
                m_RM = rm;
                m_dependency = dependency;
                m_instanceProvider = instanceProvider;
                m_instantiationParams = instantiationParams;
                m_scene = default(Scene);
            }

            protected override void GetDependencies(List<AsyncOperationHandle> deps)
            {
                deps.Add(m_dependency);
            }
            protected override string DebugName
            {
                get
                {
                    if (m_instanceProvider == null)
                        return "Instance<Invalid>";
                    return string.Format("Instance<{0}>({1}", m_instanceProvider.GetType().Name, m_dependency.IsValid() ? m_dependency.DebugName : "Invalid");
                }
            }

            public Scene InstanceScene() => m_scene;

            protected override void Destroy()
            {
                m_instanceProvider.ReleaseInstance(m_RM, m_instance);
            }

            protected override float Progress
            {
                get
                {
                    return m_dependency.PercentComplete;
                }
            }

            protected override void Execute()
            {
                Exception e = m_dependency.OperationException;
                if (m_dependency.Status == AsyncOperationStatus.Succeeded)
                {
                    m_instance = m_instanceProvider.ProvideInstance(m_RM, m_dependency, m_instantiationParams);
                    if (m_instance != null)
                        m_scene = m_instance.scene;
                    Complete(m_instance, true, null);
                }
                else
                {
                    Complete(m_instance, false, string.Format("Dependency operation failed with {0}.", e));
                }
            }

        }


        /// <summary>
        /// Load a scene at a specificed resource location.
        /// </summary>
        /// <param name="sceneProvider">The scene provider instance.</param>
        /// <param name="location">The location of the scene.</param>
        /// <param name="loadMode">The load mode for the scene.</param>
        /// <param name="activateOnLoad">If false, the scene will be loaded in the background and not activated when complete.</param>
        /// <param name="priority">The priority for the load operation.</param>
        /// <returns>Async operation handle that will complete when the scene is loaded.  If activateOnLoad is false, then Activate() will need to be called on the SceneInstance returned.</returns>
        public AsyncOperationHandle<SceneInstance> ProvideScene(ISceneProvider sceneProvider, IResourceLocation location, LoadSceneMode loadMode, bool activateOnLoad, int priority)
        {
            if (sceneProvider == null)
                throw new NullReferenceException("sceneProvider is null");

            return sceneProvider.ProvideScene(this, location, loadMode, activateOnLoad, priority);
        }

        /// <summary>
        /// Release a scene.
        /// </summary>
        /// <param name="sceneProvider">The scene provider.</param>
        /// <param name="sceneLoadHandle">The operation handle used to load the scene.</param>
        /// <returns>An operation handle for the unload.</returns>
        public AsyncOperationHandle<SceneInstance> ReleaseScene(ISceneProvider sceneProvider, AsyncOperationHandle<SceneInstance> sceneLoadHandle)
        {
            if (sceneProvider == null)
                throw new NullReferenceException("sceneProvider is null");
            //           if (sceneLoadHandle.ReferenceCount == 0)
            //               return CreateCompletedOperation<SceneInstance>(default(SceneInstance), "");
            return sceneProvider.ReleaseScene(this, sceneLoadHandle);
        }

        /// <summary>
        /// Asynchronouslly instantiate a prefab (GameObject) at the specified <paramref name="location"/>.
        /// </summary>
        /// <returns>Async operation that will complete when the prefab is instantiated.</returns>
        /// <param name="provider">An implementation of IInstanceProvider that will be used to instantiate and destroy the GameObject.</param>
        /// <param name="location">Location of the prefab.</param>
        /// <param name="instantiateParameters">A struct containing the parameters to pass the the Instantiation call.</param>
        public AsyncOperationHandle<GameObject> ProvideInstance(IInstanceProvider provider, IResourceLocation location, InstantiationParameters instantiateParameters)
        {
            if (provider == null)
                throw new NullReferenceException("provider is null.  Assign a valid IInstanceProvider object before using.");

            if (location == null)
                throw new ArgumentNullException("location");

            var depOp = ProvideResource<GameObject>(location);
            var baseOp = CreateOperation<InstanceOperation>(typeof(InstanceOperation), s_InstanceOperationTypeHash, 0, m_ReleaseInstanceOp);
            baseOp.Init(this, provider, instantiateParameters, depOp);
            m_TrackedInstanceOperations.Add(baseOp);
            return StartOperation<GameObject>(baseOp, depOp);
        }

        public void CleanupSceneInstances(Scene scene)
        {
            List<InstanceOperation> handlesToRelease = null;
            foreach (var h in m_TrackedInstanceOperations)
            {
                if (h.Result == null && scene == h.InstanceScene())
                {
                    if (handlesToRelease == null)
                        handlesToRelease = new List<InstanceOperation>();
                    handlesToRelease.Add(h);
                }
            }
            if (handlesToRelease != null)
            {
                foreach (var h in handlesToRelease)
                {
                    m_TrackedInstanceOperations.Remove(h);
                    h.DecrementReferenceCount();
                }
            }
        }

        private void ExecuteDeferredCallbacks()
        {
            for (int i = 0; i < m_DeferredCompleteCallbacks.Count; i++)
            {
                m_DeferredCompleteCallbacks[i].InvokeCompletionEvent();
                m_DeferredCompleteCallbacks[i].DecrementReferenceCount();
            }
            m_DeferredCompleteCallbacks.Clear();
        }

        internal void RegisterForDeferredCallback(IAsyncOperation op, bool incrementRefCount = true)
        {
            if(incrementRefCount)
                op.IncrementReferenceCount();
            m_DeferredCompleteCallbacks.Add(op);
            RegisterForCallbacks();
        }

        internal void Update(float unscaledDeltaTime)
        {
            m_UpdateCallbacks.Invoke(unscaledDeltaTime);
            m_UpdatingReceivers = true;
            for (int i = 0; i < m_UpdateReceivers.Count; i++)
                m_UpdateReceivers[i].Update(unscaledDeltaTime);
            m_UpdatingReceivers = false;
            if (m_UpdateReceiversToRemove != null)
            {
                foreach (var r in m_UpdateReceiversToRemove)
                    m_UpdateReceivers.Remove(r);
                m_UpdateReceiversToRemove = null;
            }
            ExecuteDeferredCallbacks();
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