using System;
using System.Collections.Generic;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.Util;

namespace UnityEngine.ResourceManagement.ResourceProviders
{
    /// <summary>
    /// Provides assets loaded via Resources.LoadAsync API.
    /// </summary>
    public class LegacyResourcesProvider : ResourceProviderBase
    {
        internal class InternalOp<TObject> : InternalProviderOperation<TObject>
            where TObject : class
        {
            AsyncOperation m_RequestOperation;
            public InternalProviderOperation<TObject> StartOp(IResourceLocation location)
            {
                m_Result = null;
                m_RequestOperation = Resources.LoadAsync<Object>(location.InternalId);

                if (m_RequestOperation.isDone)
                    DelayedActionManager.AddAction((Action<AsyncOperation>)OnComplete, 0, m_RequestOperation);
                else
                    m_RequestOperation.completed += OnComplete;
                return base.Start(location);
            }
            public override float PercentComplete
            {
                get
                {
                    if (IsDone)
                        return 1;
                    return m_RequestOperation.progress;
                }
            }
            internal override TObject ConvertResult(AsyncOperation op)
            {
                var request = op as ResourceRequest;
                return request == null ? null : request.asset as TObject;
            }
        }

        /// <inheritdoc/>
        public override IAsyncOperation<TObject> Provide<TObject>(IResourceLocation location, IList<object> deps)
        {
            if (location == null)
                throw new ArgumentNullException("location");

            var t = typeof(TObject);
            if (t.IsArray)
                return new CompletedOperation<TObject>().Start(location, location.InternalId, ResourceManagerConfig.CreateArrayResult<TObject>(Resources.LoadAll(location.InternalId, t.GetElementType())));
            if (t.IsGenericType && typeof(IList<>) == t.GetGenericTypeDefinition())
                return new CompletedOperation<TObject>().Start(location, location.InternalId, ResourceManagerConfig.CreateListResult<TObject>(Resources.LoadAll(location.InternalId, t.GetGenericArguments()[0])));
            return AsyncOperationCache.Instance.Acquire<InternalOp<TObject>>().StartOp(location);
        }

        /// <inheritdoc/>
        public override bool Release(IResourceLocation location, object asset)
        {
            if (location == null)
                throw new ArgumentNullException("location");
            var go = asset as GameObject;
            if (go != null)
            {
                //GameObjects cannot be resleased via Object.Destroy because they are considered an asset
                //but they can't be unloaded via Resources.UnloadAsset since they are NOT an asset?
                return true;
            }
            var obj = asset as Object;
            if (obj != null)
            {
                Resources.UnloadAsset(obj);
                return true;
            }

            return true;
        }
    }
}
