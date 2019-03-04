using System;
using System.Collections.Generic;
using UnityEngine.Networking;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.Util;

namespace UnityEngine.ResourceManagement.ResourceProviders
{
    /// <summary>
    /// Provides assets stored in an asset bundle.
    /// </summary>
    public class BundledAssetProvider : ResourceProviderBase
    {
        internal class InternalOp<TObject> : InternalProviderOperation<TObject>
           where TObject : class
        {
            AssetBundleRequest m_RequestOperation;
            public IAsyncOperation<TObject> Start(IResourceLocation location, IList<object> deps)
            {
                m_Result = null;
                m_RequestOperation = null;
                var bundle = AssetBundleProvider.LoadBundleFromDependecies(deps);
                if (bundle == null)
                {
                    m_Error = new Exception("Unable to load dependent bundle from location " + location);
                    DelayedActionManager.AddAction((Action<AsyncOperation>)OnComplete, 0, null);
                }
                else
                {
                    var t = typeof(TObject);
                    if (t.IsArray)
                        m_RequestOperation = bundle.LoadAssetWithSubAssetsAsync(location.InternalId, t.GetElementType());
                    else if (t.IsGenericType && typeof(IList<>) == t.GetGenericTypeDefinition())
                        m_RequestOperation = bundle.LoadAssetWithSubAssetsAsync(location.InternalId, t.GetGenericArguments()[0]);
                    else
                        m_RequestOperation = bundle.LoadAssetAsync<TObject>(location.InternalId);

                    if (m_RequestOperation.isDone)
                        DelayedActionManager.AddAction((Action<AsyncOperation>)OnComplete, 0, m_RequestOperation);
                    else
                        m_RequestOperation.completed += OnComplete;
                }
                return base.Start(location);
            }

            public override float PercentComplete
            {
                get
                {
                    if (IsDone)
                        return 1;
                    return m_RequestOperation != null ? m_RequestOperation.progress : 0.0f;
                }
            }

            internal override TObject ConvertResult(AsyncOperation op)
            {
                var t = typeof(TObject);
                try
                {
                    var req = op as AssetBundleRequest;
                    if (req == null)
                        return null;
                    
                    if (t.IsArray)
                        return ResourceManagerConfig.CreateArrayResult<TObject>(req.allAssets);
                    if (t.IsGenericType && typeof(IList<>) == t.GetGenericTypeDefinition())
                        return ResourceManagerConfig.CreateListResult<TObject>(req.allAssets);
                    return req.asset as TObject;
                }
                catch (Exception e)
                {
                    OperationException = e;
                    return null;
                }
            }
        }

        /// <inheritdoc/>
        public override IAsyncOperation<TObject> Provide<TObject>(IResourceLocation location, IList<object> loadDependencyOperation)
        {
            if (location == null)
                throw new ArgumentNullException("location");
            if (loadDependencyOperation == null)
                return new CompletedOperation<TObject>().Start(location, location, default(TObject), new ArgumentNullException("IAsyncOperation<IList<object>> loadDependencyOperation"));

            return AsyncOperationCache.Instance.Acquire<InternalOp<TObject>>().Start(location, loadDependencyOperation);
        }

        /// <inheritdoc/>
        public override bool Release(IResourceLocation location, object asset)
        {
            if (location == null)
                throw new ArgumentNullException("location");
            return true;
        }
    }
}
