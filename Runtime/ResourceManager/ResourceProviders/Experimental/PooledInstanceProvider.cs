using System;
using System.Collections.Generic;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.Util;

namespace UnityEngine.ResourceManagement.ResourceProviders.Experimental
{
    /// <summary>
    /// Implementation of IInstanceProvider that uses an internal pool of created objects. It relies on an internal provider to load the source object that will be instantiated.
    /// </summary>
    public class PooledInstanceProvider : IInstanceProvider
    {
        Dictionary<IResourceLocation, InstancePool> m_Pools = new Dictionary<IResourceLocation, InstancePool>();

        float m_ReleaseTime;
        ResourceManager m_ResourceManager;
        /// <summary>
        /// Construct a new PooledInstanceProvider.
        /// </summary>
        /// <param name="name">The name of the GameObject to be created.</param>
        /// <param name="releaseTime">Controls how long object stay in the pool.  The pool will reduce faster the larger it is.  This value roughly represents how many seconds it will take for a pool to completely empty once it contains only 1 item.</param>
        public PooledInstanceProvider(string name, float releaseTime, ResourceManager rm)
        {
            m_ResourceManager = rm;
            m_ReleaseTime = releaseTime;
            var go = new GameObject(name, typeof(PooledInstanceProviderBehavior));
            go.GetComponent<PooledInstanceProviderBehavior>().Init(this);
            go.hideFlags = HideFlags.HideAndDontSave;
        }

        /// <inheritdoc/>
        public bool CanProvideInstance<TObject>(IResourceProvider loadProvider, IResourceLocation location) where TObject : Object
        {
            return loadProvider != null && loadProvider.CanProvide<TObject>(location) && ResourceManagerConfig.IsInstance<TObject, GameObject>();
        }

        /// <inheritdoc/>
        public IAsyncOperation<TObject> ProvideInstanceAsync<TObject>(IResourceProvider loadProvider, IResourceLocation location, IList<object> deps, InstantiationParameters instantiateParameters) where TObject : Object
        {
            if (location == null)
                throw new ArgumentNullException("location");
            if (loadProvider == null)
                throw new ArgumentNullException("loadProvider");
            InstancePool pool;
            if (!m_Pools.TryGetValue(location, out pool))
                m_Pools.Add(location, pool = new InstancePool(loadProvider, location, m_ResourceManager));

            pool.holdCount++;
            return pool.ProvideInstanceAsync<TObject>(loadProvider, deps, instantiateParameters);
        }

        /// <inheritdoc/>
        public bool ReleaseInstance(IResourceProvider loadProvider, IResourceLocation location, Object instance)
        {
            InstancePool pool;
            if (!m_Pools.TryGetValue(location, out pool))
                m_Pools.Add(location, pool = new InstancePool(loadProvider, location, m_ResourceManager));
            pool.holdCount--;
            pool.Put(instance);
            return false;
        }

        internal void Update()
        {
            foreach (var p in m_Pools)
            {
                if (!p.Value.Update(m_ReleaseTime))
                {
                    m_Pools.Remove(p.Key);
                    break;
                }
            }
        }

        void HoldPool(IResourceProvider provider, IResourceLocation location)
        {
            InstancePool pool;
            if (!m_Pools.TryGetValue(location, out pool))
                m_Pools.Add(location, pool = new InstancePool(provider, location, m_ResourceManager));
            pool.holdCount++;
        }

        void ReleasePool(IResourceProvider provider, IResourceLocation location)
        {
            InstancePool pool;
            if (!m_Pools.TryGetValue(location, out pool))
                m_Pools.Add(location, pool = new InstancePool(provider, location, m_ResourceManager));
            pool.holdCount--;
        }

        internal class InternalOp<TObject> : AsyncOperationBase<TObject> where TObject : Object
        {
            TObject m_PrefabResult;
            int m_StartFrame;
            Action<IAsyncOperation<TObject>> m_OnLoadOperationCompleteAction;
            Action<TObject> m_OnValidResultCompleteAction;
            InstantiationParameters m_InstParams;
            public InternalOp()
            {
                m_OnLoadOperationCompleteAction = OnLoadComplete;
                m_OnValidResultCompleteAction = OnInstantComplete;
            }

            public InternalOp<TObject> Start(IAsyncOperation<TObject> loadOperation, IResourceLocation location, TObject value, InstantiationParameters instantiateParameters)
            {
                Validate();
                m_PrefabResult = null;
                m_InstParams = instantiateParameters;
                SetResult(value);
                Context = location;
                m_StartFrame = Time.frameCount;
                if (loadOperation != null)
                    loadOperation.Completed += m_OnLoadOperationCompleteAction;
                else
                    DelayedActionManager.AddAction(m_OnValidResultCompleteAction, 0, Result);

                return this;
            }

            void OnInstantComplete(TObject res)
            {
                Validate();
                SetResult(res);
                var go = Result as GameObject;
                if (go != null)
                {
                    if (m_InstParams.Parent != null)
                        go.transform.SetParent(m_InstParams.Parent);
                    if (m_InstParams.SetPositionRotation)
                    {
                        if (m_InstParams.InstantiateInWorldPosition)
                        {
                            go.transform.position = m_InstParams.Position;
                            go.transform.rotation = m_InstParams.Rotation;
                        }
                        else
                        {
                            go.transform.SetPositionAndRotation(m_InstParams.Position, m_InstParams.Rotation);
                        }
                    }
                }
                ResourceManagerEventCollector.PostEvent(ResourceManagerEventCollector.EventType.InstantiateAsyncCompletion, Context, Time.frameCount - m_StartFrame);
                InvokeCompletionEvent();
            }

            void OnLoadComplete(IAsyncOperation<TObject> operation)
            {
                Validate();
                ResourceManagerEventCollector.PostEvent(ResourceManagerEventCollector.EventType.InstantiateAsyncCompletion, Context, Time.frameCount - m_StartFrame);
                m_PrefabResult = operation.Result;

                if (m_PrefabResult == null)
                {
                    Debug.LogWarning("NULL prefab on instantiate: " + Context);
                }
                else if (Result == null)
                {
                    SetResult(m_InstParams.Instantiate(m_PrefabResult));
                }

                InvokeCompletionEvent();
            }
        }

        class InstancePool
        {
            IResourceLocation m_Location;
            float m_LastRefTime;
            float m_LastReleaseTime;
            public int holdCount;
            Stack<Object> m_Instances = new Stack<Object>();
            public bool Empty { get { return m_Instances.Count == 0; } }
            IResourceProvider m_LoadProvider;
            ResourceManager m_ResourceManager;
            public InstancePool(IResourceProvider provider, IResourceLocation location, ResourceManager rm)
            {
                m_ResourceManager = rm;
                m_Location = location;
                m_LoadProvider = provider;
                m_LastRefTime = Time.unscaledTime;
            }

            public T Get<T>() where T : class
            {
                m_LastRefTime = Time.unscaledTime;
                ResourceManagerEventCollector.PostEvent(ResourceManagerEventCollector.EventType.PoolCount, m_Location, m_Instances.Count - 1);
                var o = m_Instances.Pop() as T;
                (o as GameObject).SetActive(true);
                return o;
            }

            public void Put(Object gameObject)
            {
                (gameObject as GameObject).SetActive(false);
                m_Instances.Push(gameObject);
                ResourceManagerEventCollector.PostEvent(ResourceManagerEventCollector.EventType.PoolCount, m_Location, m_Instances.Count);
            }

            void ReleaseInternal(IResourceProvider provider, IResourceLocation location)
            {
                ResourceManagerEventCollector.PostEvent(ResourceManagerEventCollector.EventType.Release, location, Time.frameCount);
                provider.Release(location, null);
                for (int i = 0; location.Dependencies != null && i < location.Dependencies.Count; i++)
                    ReleaseInternal(m_ResourceManager.GetResourceProvider<object>(location.Dependencies[i]), location.Dependencies[i]);
            }

            internal bool Update(float releaseTime)
            {
                if (m_Instances.Count > 0)
                {
                    if ((m_Instances.Count > 1 && Time.unscaledTime - m_LastReleaseTime > releaseTime) || Time.unscaledTime - m_LastRefTime > (1f / m_Instances.Count) * releaseTime)  //the last item will take releaseTime seconds to drop...
                    {
                        m_LastReleaseTime = m_LastRefTime = Time.unscaledTime;
                        var instance = m_Instances.Pop();
                        ReleaseInternal(m_LoadProvider, m_Location);
                        if (Application.isPlaying)
                            Object.Destroy(instance);
                        else
                            Object.DestroyImmediate(instance);
                        ResourceManagerEventCollector.PostEvent(ResourceManagerEventCollector.EventType.PoolCount, m_Location, m_Instances.Count);
                        if (m_Instances.Count == 0 && holdCount == 0)
                            return false;
                    }
                }
                return true;
            }

            internal IAsyncOperation<TObject> ProvideInstanceAsync<TObject>(IResourceProvider loadProvider, IList<object> deps, InstantiationParameters instantiateParameters) where TObject : Object
            {
                if (m_Instances.Count > 0)
                {
                    //this accounts for the dependency load which is not needed since the asset is cached.
                    for (int i = 0; m_Location.Dependencies != null && i < m_Location.Dependencies.Count; i++)
                        ReleaseInternal(m_ResourceManager.GetResourceProvider<object>(m_Location.Dependencies[i]), m_Location.Dependencies[i]);

                    return AsyncOperationCache.Instance.Acquire<InternalOp<TObject>>().Start(null, m_Location, Get<TObject>(), instantiateParameters);
                }

                var depOp = loadProvider.Provide<TObject>(m_Location, deps);
                return AsyncOperationCache.Instance.Acquire<InternalOp<TObject>>().Start(depOp, m_Location, null, instantiateParameters);
            }
        }
    }
}
