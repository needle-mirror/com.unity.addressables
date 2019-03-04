#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace UnityEngine.ResourceManagement.ResourceProviders.Simulation
{
    /// <summary>
    /// Custom version of AssetBundleRequestOptions used to compute needed download size while bypassing the cache.  In the future a virtual cache may be implemented.
    /// </summary>
    [Serializable]
    public class VirtualAssetBundleRequestOptions : AssetBundleRequestOptions
    {
        /// <summary>
        /// Computes the amount of data needed to be downloaded for this bundle.
        /// </summary>
        /// <param name="loc">The location of the bundle.</param>
        /// <returns>The size in bytes of the bundle that is needed to be downloaded.  If the local cache contains the bundle or it is a local bundle, 0 will be returned.</returns>
        public override long ComputeSize(IResourceLocation loc)
        {
            if (!loc.InternalId.Contains("://"))
            {
//                Debug.LogFormat("Location {0} is local, ignoring size", loc);
                return 0;
            }
            var locHash = Hash128.Parse(Hash);
            if (!locHash.isValid)
            {
 //               Debug.LogFormat("Location {0} has invalid hash, using size of {1}", loc, BundleSize);
                return BundleSize;
            }
            //TODO: implement support for virtual bundle cache
 //           Debug.LogFormat("Location {0} has hash and is NOT in the cache, using size {1}", loc, BundleSize);
            return BundleSize;
        }
    }

    /// <summary>
    /// Provides assets from virtual asset bundles.  Actual loads are done through the AssetDatabase API.
    /// </summary>
    public class VirtualBundledAssetProvider : ResourceProviderBase
    {
        /// <summary>
        /// Default copnstructor.
        /// </summary>
        public VirtualBundledAssetProvider()
        {
            m_ProviderId = typeof(BundledAssetProvider).FullName; 
        }

        class InternalOp<TObject> : InternalProviderOperation<TObject>
            where TObject : class
        {
            IAsyncOperation<TObject> m_RequestOperation;

            public InternalProviderOperation<TObject> Start(IResourceLocation location, IList<object> deps)
            {
                m_Result = null;
                m_RequestOperation = null;

                VirtualAssetBundle bundle = deps[0] as VirtualAssetBundle;
                if(bundle != null)
                {
                    m_RequestOperation = bundle.LoadAssetAsync<TObject>(location);
                    m_RequestOperation.Completed += OnComplete;
                }
                else
                {
                    OnComplete();
                }

                return base.Start(location);
            }

            public override float PercentComplete
            {
                get
                {
                    if (IsDone)
                        return 1;

                    return m_RequestOperation != null ? m_RequestOperation.PercentComplete : 0.0f;
                }
            }
            internal override TObject ConvertResult(AsyncOperation operation) { return null; }
        }

        /// <inheritdoc/>
        public override IAsyncOperation<TObject> Provide<TObject>(IResourceLocation location, IList<object> deps)
        {
            if (location == null)
                throw new ArgumentNullException("location");
            var operation = AsyncOperationCache.Instance.Acquire<InternalOp<TObject>>();
            return operation.Start(location, deps);
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
#endif
