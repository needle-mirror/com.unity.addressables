using System;
using System.Collections.Generic;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.Util;
using UnityEngine.Serialization;

namespace UnityEngine.ResourceManagement.ResourceProviders
{
    /// <summary>
    /// Provider that can wrap other IResourceProviders and add caching and reference counting of objects.
    /// </summary>
    public class CachedProvider : IResourceProvider, IUpdateReceiver
    {
        internal abstract class CacheEntry
        {
            protected IAsyncOperation m_Operation;
            protected object m_Result;
            protected CacheList m_CacheList;
            protected AsyncOperationStatus m_Status;
            protected Exception m_Error;
            internal abstract bool CanProvide<TObject>(IResourceLocation location) where TObject : class;
            public IAsyncOperation InternalOperation { get { return m_Operation; } }

            public bool IsShared { get; internal set; }
            public abstract bool IsDone { get; }
            public abstract float PercentComplete { get; }
            public object Result { get { return m_Result; } }
            public abstract void ReleaseInternalOperation();

            public void Reset() { }

            public void ResetStatus()
            {
                //should never be called as this operation doe not end up in cache
            }
        }

        internal class CacheEntry<TObject> : CacheEntry, IAsyncOperation<TObject>
            where TObject : class
        {
            System.Threading.EventWaitHandle m_waitHandle;
            public System.Threading.WaitHandle WaitHandle
            {
                get
                {
                    if (m_waitHandle == null)
                        m_waitHandle = new System.Threading.EventWaitHandle(false, System.Threading.EventResetMode.ManualReset);
                    m_waitHandle.Reset();
                    return m_waitHandle;
                }
            }

#if NET_4_6
            public System.Threading.Tasks.Task<TObject> Task
            {
                get
                {
                    return System.Threading.Tasks.Task.Factory.StartNew(o =>
                    {
                        var asyncOperation = o as IAsyncOperation<TObject>;
                        asyncOperation.WaitHandle.WaitOne();
                        return asyncOperation.Result;
                    }, this);
                }
            }
#endif
            public AsyncOperationStatus Status
            {
                get
                {
                    Validate();
                    return m_Status > AsyncOperationStatus.None ? m_Status : m_Operation.Status;
                }
            }

            public Exception OperationException
            {
                get
                {
                    Validate();
                    return m_Error != null ? m_Error : m_Operation.OperationException;
                }
                protected set
                {
                    m_Error = value;
                    if (m_Error != null && ResourceManager.ExceptionHandler != null)
                        ResourceManager.ExceptionHandler(this, value);
                }
            }

            public new TObject Result
            {
                get
                {
                    Validate();
                    return m_Result as TObject;
                }
            }

            public override bool IsDone
            {
                get
                {
                    Validate();
                    return !(EqualityComparer<TObject>.Default.Equals(Result, default(TObject)));
                }
            }

            public object Current
            {
                get
                {
                    Validate();
                    return m_Result;
                }
            }

            public bool MoveNext()
            {
                Validate();
                return m_Result == null;
            }

            public object Context
            {
                get
                {
                    Validate();
                    return m_Operation.Context;
                }
            }

            public object Key
            {
                get
                {
                    Validate();
                    return m_Operation.Key;
                }
                set
                {
                    m_Operation.Key = value;
                }
            }


            public bool IsValid { get { return m_Operation != null && m_Operation.IsValid; } set { } }


            public override void ReleaseInternalOperation()
            {
                if (!IsShared)
                    m_Operation.Release();

                m_Operation = null;
            }

            public override float PercentComplete
            {
                get
                {
                    Validate();
                    return IsDone ? 1f : m_Operation.PercentComplete;
                }
            }
            List<Action<IAsyncOperation<TObject>>> m_CompletedActionT;
            protected event Action<IAsyncOperation> completedAction;
            public event Action<IAsyncOperation<TObject>> Completed
            {
                add
                {
                    Validate();
                    if (IsDone)
                    {
                        DelayedActionManager.AddAction(value, 0, this);
                    }
                    else
                    {
                        if (m_CompletedActionT == null)
                            m_CompletedActionT = new List<Action<IAsyncOperation<TObject>>>(2);
                        m_CompletedActionT.Add(value);
                    }
                }

                remove
                {
                    m_CompletedActionT.Remove(value);
                }
            }

            event Action<IAsyncOperation> IAsyncOperation.Completed
            {
                add
                {
                    Validate();
                    if (IsDone)
                        DelayedActionManager.AddAction(value, 0, this);
                    else
                        completedAction += value;
                }

                remove
                {
                    completedAction -= value;
                }
            }

            public CacheEntry(CacheList cacheList, IAsyncOperation operation, bool isShared)
            {
                m_CacheList = cacheList;
                IsShared = isShared;
                if (!isShared)
                    m_Operation = operation.Retain();
                else
                    m_Operation = operation;

                ResourceManagerEventCollector.PostEvent(ResourceManagerEventCollector.EventType.CacheEntryLoadPercent, Context, 0);
                operation.Completed += OnComplete;
            }

            void OnComplete(IAsyncOperation operation)
            {
                Validate();
                m_Result = operation.Result;
                ResourceManagerEventCollector.PostEvent(ResourceManagerEventCollector.EventType.CacheEntryLoadPercent, Context, 100);
                if (completedAction != null)
                {
                    var tmpEvent = completedAction;
                    completedAction = null;
                    try
                    {
                        tmpEvent(this);
                    }
                    catch (Exception e)
                    {
                        m_Status = AsyncOperationStatus.Failed;
                        OperationException = e;
                    }
                }

                if (m_CompletedActionT != null)
                {
                    for (int i = 0; i < m_CompletedActionT.Count; i++)
                    {
                        try
                        {
                            m_CompletedActionT[i](this);
                        }
                        catch (Exception e)
                        {
                            m_Status = AsyncOperationStatus.Failed;
                            OperationException = e;
                        }
                    }
                    m_CompletedActionT.Clear();
                }
                if (m_waitHandle != null)
                    m_waitHandle.Set();
            }

            internal override bool CanProvide<T1>(IResourceLocation location)
            {
                Validate();
                return typeof(TObject) == typeof(T1);
            }

            public bool Validate()
            {
                if (!IsValid)
                {
                    Debug.LogError("IAsyncOperation Validation Failed!");
                    return false;
                }
                return true;
            }

            public IAsyncOperation<TObject> Retain()
            {
                return this;
            }
            /// <inheritdoc />
            IAsyncOperation IAsyncOperation.Retain()
            {
                Validate();
                return this;
            }

            public void Release()
            {
                //do nothing
            }
        }

        internal class CacheList
        {
            public int refCount;
            public float lastAccessTime;
            public IResourceLocation location;
            public List<CacheEntry> entries = new List<CacheEntry>();
            public CacheList(IResourceLocation location) { this.location = location; }
            public int RefCount
            {
                get
                {
                    return refCount;
                }
            }

            public override int GetHashCode()
            {
                return location.GetHashCode();
            }

            public bool IsDone
            {
                get
                {
                    foreach (var ee in entries)
                        if (!ee.IsDone)
                            return false;
                    return true;
                }
            }

            public float CompletePercent
            {
                get
                {
                    if (entries.Count == 0)
                        return 0;
                    float rc = 0;
                    foreach (var ee in entries)
                        rc += ee.PercentComplete;
                    return rc / entries.Count;
                }
            }

            public CacheEntry<TObject> FindEntry<TObject>(IResourceLocation resLocation)
                 where TObject : class
            {
                if (entries.Count == 0)
                    return null;

                //look for an existing match first
                for (int i = 0; i < entries.Count; i++)
                {
                    var e = entries[i];
                    if (e.CanProvide<TObject>(resLocation))
                        return e as CacheEntry<TObject>;
                }

                //try to cast to the requested type from an existing one
                for (int i = 0; i < entries.Count; i++)
                {
                    var e = entries[i];
                    var r = (TObject)e.Result;
                    if (r != default(TObject))
                        return CreateEntry<TObject>(e.InternalOperation, true);
                }

                return null;
            }

            public CacheEntry<TObject> CreateEntry<TObject>(IAsyncOperation operation, bool isShared)
                where TObject : class
            {
                var entry = new CacheEntry<TObject>(this, operation, isShared);
                entries.Add(entry);
                return entry;
            }


            internal void Retain()
            {
                lastAccessTime = Time.unscaledTime;
                refCount++;
                ResourceManagerEventCollector.PostEvent(ResourceManagerEventCollector.EventType.CacheEntryRefCount, location, refCount);
            }

            internal bool Release()
            {
                refCount--;
                ResourceManagerEventCollector.PostEvent(ResourceManagerEventCollector.EventType.CacheEntryRefCount, location, refCount);
                return refCount == 0;
            }

            internal void ReleaseAssets(IResourceProvider provider)
            {
                ResourceManagerEventCollector.PostEvent(ResourceManagerEventCollector.EventType.CacheEntryLoadPercent, location, 0);
                foreach (var e in entries)
                {
                    Debug.Assert(e.IsDone);
                    if (!e.IsShared)
                        provider.Release(location, e.Result);
                    e.ReleaseInternalOperation();
                }
            }
        }

        class CachedProviderUpdater : MonoBehaviour
        {
            CachedProvider m_Provider;
            public void Init(CachedProvider provider)
            {
                m_Provider = provider;
                DontDestroyOnLoad(gameObject);
            }

            void Update()
            {
                m_Provider.UpdateLru();
            }
        }

        Dictionary<int, CacheList> m_Cache = new Dictionary<int, CacheList>();
        IResourceProvider m_InternalProvider;
        LinkedList<CacheList> m_Lru;
        string m_ProviderId;
        internal int maxLruCount;
        internal float maxLruAge;

        /// <summary>
        /// Settings object used to initialize the cache provider.
        /// </summary>
        [Serializable]
        public class Settings
        {
            [FormerlySerializedAs("m_maxLRUCount")]
            [SerializeField]
            int m_MaxLruCount;
            /// <summary>
            /// The maximum number of items to hold in the LRU.  If set to 0, the LRU is not used and items will be released as soon as the reference count drops to 0.
            /// </summary>
            public int maxLruCount { get { return m_MaxLruCount; } set { m_MaxLruCount = value; } }
            [FormerlySerializedAs("m_maxLRUAge")]
            [SerializeField]
            float m_MaxLruAge;
            /// <summary>
            /// The time, in seconds, to hold on to items in the cache.  This value scales inversely to how full the LRU is.  The last item will take this long to be removed.  If set to 0, items are not automatically removed.
            /// </summary>
            public float maxLruAge { get { return m_MaxLruAge; } set { m_MaxLruAge = value; } }
            [FormerlySerializedAs("m_internalProvider")]
            [SerializeField]
            ObjectInitializationData m_InternalProvider;
            /// <summary>
            /// The initialization data for the internal provider.
            /// </summary>
            public ObjectInitializationData InternalProviderData { get { return m_InternalProvider; } set { m_InternalProvider = value; } }
        }

        /// <summary>
        /// Construct a new CachedProvider object.  Initialize should be called after construction.
        /// </summary>
        public CachedProvider() { }

        /// <summary>
        /// Construct a new CachedProvider object.
        /// </summary>
        /// <param name="provider">The internal provider that will handle the loading and releasing of the objects.</param>
        /// <param name="maxCacheItemCount">How many items to keep in the cache.  If set to 0, items are not cached.</param>
        /// <param name="maxCacheItemAge">How long to keep items in the cache. If set to 0, cached items are kept indefinitely.</param>
        public CachedProvider(IResourceProvider provider, string providerId, int maxCacheItemCount = 0, float maxCacheItemAge = 0)
        {
            InitInternal(provider, providerId, maxCacheItemCount, maxCacheItemAge);
        }

        /// <summary>
        /// Initialize this instance.
        /// </summary>
        /// <param name="id">The provider id.</param>
        /// <param name="data">Serialized data.  This can be generated by calling CreateInitializationData.</param>
        public bool Initialize(string id, string data)
        {
            var settings = JsonUtility.FromJson<Settings>(data);
            if (settings == null)
                return false;
            return InitInternal(settings.InternalProviderData.CreateInstance<IResourceProvider>(), id, settings.maxLruCount, settings.maxLruAge);
        }

        internal bool InitInternal(IResourceProvider provider, string providerId, int maxCacheItemCount, float maxCacheItemAge)
        {
            if (provider == null || string.IsNullOrEmpty(providerId))
                return false;

            m_InternalProvider = provider;
            m_ProviderId = providerId;
            maxLruCount = maxCacheItemCount;
            if (maxLruCount > 0)
            {
                m_Lru = new LinkedList<CacheList>();
                maxLruAge = maxCacheItemAge;
                if (maxCacheItemAge > 0)
                {
                    var go = new GameObject("CachedProviderUpdater", typeof(CachedProviderUpdater));
                    go.GetComponent<CachedProviderUpdater>().Init(this);
                    go.hideFlags = HideFlags.HideAndDontSave;
                }
                ResourceManagerEventCollector.PostEvent(ResourceManagerEventCollector.EventType.CacheLruCount, ProviderId, m_Lru.Count);
            }
            return true;
        }

        void UpdateLru()
        {
            if (m_Lru != null)
            {
                float time = Time.unscaledTime;
                while (m_Lru.Last != null && (time - m_Lru.Last.Value.lastAccessTime) > maxLruAge && m_Lru.Last.Value.IsDone)
                {
                    m_Lru.Last.Value.ReleaseAssets(m_InternalProvider);
                    m_Lru.RemoveLast();
                }
                ResourceManagerEventCollector.PostEvent(ResourceManagerEventCollector.EventType.CacheLruCount, ProviderId, m_Lru.Count);
            }
        }


        /// <inheritdoc/>
        public override string ToString() { return "CachedProvider[" + m_InternalProvider + "]"; }
        /// <inheritdoc/>
        public string ProviderId { get { return m_ProviderId; } }

        /// <inheritdoc/>
        public bool CanProvide<TObject>(IResourceLocation location)
            where TObject : class
        {
            return ProviderId.Equals(location.ProviderId, StringComparison.Ordinal);
        }

        Action<CacheList> m_RetryReleaseEntryAction;
        /// <summary>
        /// Releasing an object to a CachedProvider will decrease it reference count, which may result in the object getting actually released.  Released objects are added to an in memory cache.
        /// </summary>
        /// <param name="location">The location of the object.</param>
        /// <param name="asset">The object to release.</param>
        /// <returns>True if the reference count reaches 0 and the asset is released.</returns>
        public bool Release(IResourceLocation location, object asset)
        {
            CacheList entryList;
            if (location == null || !m_Cache.TryGetValue(location.GetHashCode(), out entryList))
                return false;

            return ReleaseCache(entryList);
        }

        bool ReleaseCache(CacheList entryList)
        {
            if (entryList.Release())
            {
                if (!entryList.IsDone)
                {
                    if (m_RetryReleaseEntryAction == null)
                        m_RetryReleaseEntryAction = RetryEntryRelease;
                    entryList.Retain(); //hold on since this will be retried...
                    DelayedActionManager.AddAction(m_RetryReleaseEntryAction, .2f, entryList);
                    return false;
                }

                if (m_Lru != null)
                {
                    m_Lru.AddFirst(entryList);
                    while (m_Lru.Count > maxLruCount && m_Lru.Last.Value.IsDone)
                    {
                        m_Lru.Last.Value.ReleaseAssets(m_InternalProvider);
                        m_Lru.RemoveLast();
                    }
                    ResourceManagerEventCollector.PostEvent(ResourceManagerEventCollector.EventType.CacheLruCount, ProviderId + " LRU", m_Lru.Count);
                }
                else
                {
                    entryList.ReleaseAssets(m_InternalProvider);
                }

                if (!m_Cache.Remove(entryList.GetHashCode()))
                    Debug.LogWarningFormat("Unable to find entryList {0} in cache.", entryList.location);
                return true;
            }
            return false;
        }


        internal void RetryEntryRelease(CacheList e)
        {
            ReleaseCache(e);
        }

        /// <summary>
        /// Provide the requested object.  The cache will be checked first for existing objects.  If not found, the internal IResourceProvider will be used to provide the object.  The reference count for the asset will be incremented.
        /// </summary>
        /// <typeparam name="TObject"></typeparam>
        /// <param name="location"></param>
        /// <param name="loadDependencyOperation"></param>
        /// <returns></returns>
        public IAsyncOperation<TObject> Provide<TObject>(IResourceLocation location, IList<object> deps)
            where TObject : class
        {
            if (location == null)
                throw new ArgumentNullException("location");

            CacheList entryList;
            if (!m_Cache.TryGetValue(location.GetHashCode(), out entryList))
            {
                if (m_Lru != null && m_Lru.Count > 0)
                {
                    var node = m_Lru.First;
                    while (node != null)
                    {
                        if (node.Value.location.GetHashCode() == location.GetHashCode())
                        {
                            ResourceManagerEventCollector.PostEvent(ResourceManagerEventCollector.EventType.CacheEntryLoadPercent, location, 1);
                            entryList = node.Value;
                            m_Lru.Remove(node);
                            ResourceManagerEventCollector.PostEvent(ResourceManagerEventCollector.EventType.CacheLruCount, ProviderId, m_Lru.Count);
                            break;
                        }
                        node = node.Next;
                    }
                }
                if (entryList == null)
                    entryList = new CacheList(location);

                m_Cache.Add(location.GetHashCode(), entryList);
            }

            entryList.Retain();
            var entry = entryList.FindEntry<TObject>(location);
            if (entry != null)
            {
                if (entry.Status == AsyncOperationStatus.Failed)
                {
                    m_InternalProvider.Release(location, entry.Result);
                    entry.ReleaseInternalOperation();
                    entryList.entries.Remove(entry);
                }
                else
                {
                    return entry;
                }
            }
            return entryList.CreateEntry<TObject>(m_InternalProvider.Provide<TObject>(location, deps), false);
        }

        void IUpdateReceiver.Update(float deltaTime)
        {
            IUpdateReceiver pb = this.m_InternalProvider as IUpdateReceiver;
            if (pb != null)
                pb.Update(deltaTime);
        }

        bool IUpdateReceiver.NeedsUpdate
        {
            get
            {
                IUpdateReceiver pb = m_InternalProvider as IUpdateReceiver;
                return pb != null && pb.NeedsUpdate;
            }
        }

        public ProviderBehaviourFlags BehaviourFlags { get { return m_InternalProvider.BehaviourFlags; } }
    }
}
