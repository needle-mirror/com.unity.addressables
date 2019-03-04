using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine.Networking;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.Util;
using UnityEngine.Serialization;

namespace UnityEngine.ResourceManagement.ResourceProviders
{
    /// <summary>
    /// Contains cache information to be used by the AssetBundleProvider
    /// </summary>
    [Serializable]
    public class AssetBundleRequestOptions : ILocationSizeData
    {
        [FormerlySerializedAs("m_hash")]
        [SerializeField]
        string m_Hash = "";
        /// <summary>
        /// Hash value of the asset bundle.
        /// </summary>
        public string Hash { get { return m_Hash; } set { m_Hash = value; } }
        [FormerlySerializedAs("m_crc")]
        [SerializeField]
        uint m_Crc;
        /// <summary>
        /// CRC value of the bundle.
        /// </summary>
        public uint Crc { get { return m_Crc; } set { m_Crc = value; } }
        [FormerlySerializedAs("m_timeout")]
        [SerializeField]
        int m_Timeout;
        /// <summary>
        /// Sets UnityWebRequest to attempt to abort after the number of seconds in timeout have passed.
        /// </summary>
        public int Timeout { get { return m_Timeout; } set { m_Timeout = value; } }
        [FormerlySerializedAs("m_chunkedTransfer")]
        [SerializeField]
        bool m_ChunkedTransfer;
        /// <summary>
        /// Indicates whether the UnityWebRequest system should employ the HTTP/1.1 chunked-transfer encoding method.
        /// </summary>
        public bool ChunkedTransfer { get { return m_ChunkedTransfer; } set { m_ChunkedTransfer = value; } }
        [FormerlySerializedAs("m_redirectLimit")]
        [SerializeField]
        int m_RedirectLimit = -1;
        /// <summary>
        /// Indicates the number of redirects which this UnityWebRequest will follow before halting with a “Redirect Limit Exceeded” system error.
        /// </summary>
        public int RedirectLimit { get { return m_RedirectLimit; } set { m_RedirectLimit = value; } }
        [FormerlySerializedAs("m_retryCount")]
        [SerializeField]
        int m_RetryCount;
        /// <summary>
        /// Indicates the number of times the request will be retried.  
        /// </summary>
        public int RetryCount { get { return m_RetryCount; } set { m_RetryCount = value; } }

        [SerializeField]
        string m_BundleName = null;
        /// <summary>
        /// The name of the original bundle.  This does not contain the appended hash.
        /// </summary>
        public string BundleName { get { return m_BundleName; } set { m_BundleName = value; } }

        [SerializeField]
        long m_BundleSize;
        /// <summary>
        /// The size of the bundle, in bytes.
        /// </summary>
        public long BundleSize { get { return m_BundleSize; } set { m_BundleSize = value; } }
        /// <summary>
        /// Computes the amount of data needed to be downloaded for this bundle.
        /// </summary>
        /// <param name="loc">The location of the bundle.</param>
        /// <returns>The size in bytes of the bundle that is needed to be downloaded.  If the local cache contains the bundle or it is a local bundle, 0 will be returned.</returns>
        public virtual long ComputeSize(IResourceLocation loc)
        {
            if (!loc.InternalId.Contains("://"))
            {
 //               Debug.LogFormat("Location {0} is local, ignoring size", loc);
                return 0;
            }
            var locHash = Hash128.Parse(Hash);
            if (!locHash.isValid)
            {
   //             Debug.LogFormat("Location {0} has invalid hash, using size of {1}", loc, BundleSize);
                return BundleSize;
            }
            List<Hash128> versions = new List<Hash128>();
            var bundleName = Path.GetFileName(loc.InternalId);
            Caching.GetCachedVersions(bundleName, versions);

            foreach (var v in versions)
            {
                if (v == locHash)
                {
     //               Debug.LogFormat("Location {0} has hash and is in the cache, ignoring size", loc);
                    return 0;
                }
            }
     //       Debug.LogFormat("Location {0} has hash and is NOT in the cache, using size {1}", loc, BundleSize);
            return BundleSize;
        }
    }

    /// <summary>
    /// IResourceProvider for asset bundles.  Loads bundles via UnityWebRequestAssetBundle API if the internalId contains "://".  If not, it will load the bundle via AssetBundle.LoadFromFileAsync.
    /// </summary>
    public class AssetBundleProvider : ResourceProviderBase
    {
        internal class InternalOp<TObject> : InternalProviderOperation<TObject>
            where TObject : class
        {
            IAsyncOperation<IList<object>> m_DependencyOperation;
            AsyncOperation m_RequestOperation;
            int m_Retries;
            int m_MaxRetries;
            public InternalOp()
            {
            }

            protected override void OnComplete(AsyncOperation op)
            {
                Validate();
                TObject res = default(TObject);
                try
                {
                    var localReq = op as AssetBundleCreateRequest;
                    if (localReq != null)
                    {
                        res = localReq.assetBundle as TObject;
                    }
                    else
                    {
                        var remoteReq = op as UnityWebRequestAsyncOperation;
                        if (remoteReq != null)
                        {
                            var webReq = remoteReq.webRequest;
                            if (string.IsNullOrEmpty(webReq.error))
                            {
                                if (typeof(TObject) == typeof(AssetBundle) && webReq.downloadHandler is DownloadHandlerAssetBundle)
                                {
                                    var handler = webReq.downloadHandler as DownloadHandlerAssetBundle;
                                    res = handler.assetBundle as TObject;
                                    if(handler.assetBundle != null)
                                        loadedDownloadHandlers.Add(handler);
                                }
                                else
                                    res = webReq.downloadHandler as TObject;
                            }
                            else
                            {
                                if (m_Retries < m_MaxRetries)
                                {
                                    m_Retries++;
                                    Debug.LogFormat("Web request {0} failed with error '{1}', retrying ({2}/{3})...",webReq.url, webReq.error,  m_Retries, m_MaxRetries);
                                    return;
                                }

                                OperationException = new Exception(string.Format("RemoteAssetBundleProvider unable to load from url {0}, result='{1}'.", webReq.url, webReq.error));
                            }
                            webReq.Dispose();
                        }
                    }
                }
                catch (Exception ex)
                {
                    OperationException = ex;
                }
                SetResult(res);
                OnComplete();
            }

            UnityWebRequest CreateWebRequest(IResourceLocation loc)
            {
                var options = loc.Data as AssetBundleRequestOptions;
                if (options == null)
                    return UnityWebRequestAssetBundle.GetAssetBundle(loc.InternalId);

                var webRequest = !string.IsNullOrEmpty(options.Hash) ? 
                    UnityWebRequestAssetBundle.GetAssetBundle(loc.InternalId, Hash128.Parse(options.Hash), options.Crc) : 
                    UnityWebRequestAssetBundle.GetAssetBundle(loc.InternalId, options.Crc);

                if(options.Timeout > 0)
                    webRequest.timeout = options.Timeout;
                if (options.RedirectLimit > 0)
                    webRequest.redirectLimit = options.RedirectLimit;
                webRequest.chunkedTransfer = options.ChunkedTransfer;
                m_MaxRetries = options.RetryCount;
                return webRequest;
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

            public InternalProviderOperation<TObject> Start(IResourceLocation location, IList<object> deps)
            {
                Context = location;
                m_Result = null;
                m_RequestOperation = null;

                var loc = Context as IResourceLocation;
                var path = loc.InternalId;
                if (File.Exists(path))
                {
                    var options = loc.Data as AssetBundleRequestOptions;
                    m_RequestOperation = AssetBundle.LoadFromFileAsync(path, options == null ? 0 : options.Crc);
                }
                else if (path.Contains("://"))
                {
                    var req = CreateWebRequest(loc);
                    req.disposeDownloadHandlerOnDispose = false;
                    m_RequestOperation = req.SendWebRequest();
                }
                else
                {
                    m_RequestOperation = null;
                    OperationException = new Exception(string.Format("Invalid path in AssetBundleProvider: '{0}'.", path));
                    SetResult(default(TObject));
                    OnComplete();
                }
                if (m_RequestOperation != null)
                {
                    if (m_RequestOperation.isDone)
                        DelayedActionManager.AddAction((Action<AsyncOperation>)OnComplete, 0, m_RequestOperation);
                    else
                        m_RequestOperation.completed += OnComplete;
                }

                return base.Start(location);
            }
        }
        /// <inheritdoc/>
        public override IAsyncOperation<TObject> Provide<TObject>(IResourceLocation location, IList<object> deps)
        {
            if (location == null)
                throw new ArgumentNullException("location");
            var operation = AsyncOperationCache.Instance.Acquire<InternalOp<TObject>>();
            return operation.Start(location, deps);
        }

        static HashSet<DownloadHandlerAssetBundle> loadedDownloadHandlers = new HashSet<DownloadHandlerAssetBundle>();
        internal static AssetBundle LoadBundleFromDependecies(IList<object> results)
        {
            //access all of the dependent bundles to force them to load
            for (int i = 1; i < results.Count; i++)
            {
                
                var handler = results[i] as DownloadHandlerAssetBundle;
                if (handler != null)
                {
                    var b = handler.assetBundle;
                    if (b != null)
                        loadedDownloadHandlers.Add(handler);
                }
            }

            AssetBundle bundle = results[0] as AssetBundle;
            if (bundle == null)
            {
                var handler = results[0] as DownloadHandlerAssetBundle;
                if (handler != null)
                {
                    bundle = handler.assetBundle;
                    if (bundle != null)
                        loadedDownloadHandlers.Add(handler);
                }
            }
            return bundle;
        }

        /// <summary>
        /// Releases the asset bundle via AssetBundle.Unload(true).
        /// </summary>
        /// <param name="location"></param>
        /// <param name="asset"></param>
        /// <returns></returns>
        public override bool Release(IResourceLocation location, object asset)
        {
            if (location == null)
                throw new ArgumentNullException("location");
            if (asset == null)
            {
                Debug.LogWarningFormat("Releasing null asset bundle from location {0}.  This is an indication that the bundle failed to load.", location);
                return false;
            }
            var bundle = asset as AssetBundle;
            if (bundle != null)
            {
                bundle.Unload(true);
                return true;
            }
            var dhHandler = asset as DownloadHandlerAssetBundle;
            if (dhHandler != null)
            {
                if (loadedDownloadHandlers.Contains(dhHandler))
                {
                    bundle = dhHandler.assetBundle;
                    if (bundle != null)
                    {
                        bundle.Unload(true);
                    }
                    else
                    {
                        Debug.LogWarningFormat("Asset Bundle {0} was marked as loaded but is null.", location.InternalId);
                    }
                    loadedDownloadHandlers.Remove(dhHandler);
                }
                dhHandler.Dispose();
                return true;
            }
            return false;
        }
    }
}
