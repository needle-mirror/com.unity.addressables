using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using UnityEngine.Networking;
using UnityEngine.Profiling;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.Exceptions;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.Util;
using UnityEngine.Serialization;


namespace UnityEngine.ResourceManagement.ResourceProviders
{
    internal class DownloadOnlyLocation : LocationWrapper
    {
        public DownloadOnlyLocation(IResourceLocation location) : base(location)
        {
        }
    }

    /// <summary>
    /// Used to indication how Assets are loaded from the AssetBundle on the first load request.
    /// </summary>
    public enum AssetLoadMode
    {
        /// <summary>
        /// Only load the requested Asset and Dependencies
        /// </summary>
        RequestedAssetAndDependencies = 0,

        /// <summary>
        /// Load all assets inside the AssetBundle
        /// </summary>
        AllPackedAssetsAndDependencies,
    }

    /// <summary>
    /// Wrapper for asset bundles.
    /// </summary>
    public interface IAssetBundleResource
    {
        /// <summary>
        /// Retrieves the asset bundle.
        /// </summary>
        /// <returns>Returns the asset bundle.</returns>
        AssetBundle GetAssetBundle();
    }

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
        public string Hash
        {
            get { return m_Hash; }
            set { m_Hash = value; }
        }

        [FormerlySerializedAs("m_crc")]
        [SerializeField]
        uint m_Crc;

        /// <summary>
        /// CRC value of the bundle.
        /// </summary>
        public uint Crc
        {
            get { return m_Crc; }
            set { m_Crc = value; }
        }

        [FormerlySerializedAs("m_timeout")]
        [SerializeField]
        int m_Timeout;

        /// <summary>
        /// Attempt to abort after the number of seconds in timeout have passed, where the UnityWebRequest has received no data.
        /// </summary>
        public int Timeout
        {
            get { return m_Timeout; }
            set { m_Timeout = value; }
        }

        [FormerlySerializedAs("m_chunkedTransfer")]
        [SerializeField]
        bool m_ChunkedTransfer;

        /// <summary>
        /// Indicates whether the UnityWebRequest system should employ the HTTP/1.1 chunked-transfer encoding method.
        /// </summary>
        public bool ChunkedTransfer
        {
            get { return m_ChunkedTransfer; }
            set { m_ChunkedTransfer = value; }
        }

        [FormerlySerializedAs("m_redirectLimit")]
        [SerializeField]
        int m_RedirectLimit = -1;

        /// <summary>
        /// Indicates the number of redirects which this UnityWebRequest will follow before halting with a “Redirect Limit Exceeded” system error.
        /// </summary>
        public int RedirectLimit
        {
            get { return m_RedirectLimit > 128 ? 128 : m_RedirectLimit; }
            set { m_RedirectLimit = value; }
        }

        [FormerlySerializedAs("m_retryCount")]
        [SerializeField]
        int m_RetryCount;

        /// <summary>
        /// Indicates the number of times the request will be retried.
        /// </summary>
        public int RetryCount
        {
            get { return m_RetryCount; }
            set { m_RetryCount = value; }
        }

        [SerializeField]
        string m_BundleName = null;

        /// <summary>
        /// The name of the original bundle.  This does not contain the appended hash.
        /// </summary>
        public string BundleName
        {
            get { return m_BundleName; }
            set { m_BundleName = value; }
        }

        [SerializeField]
        AssetLoadMode m_AssetLoadMode = AssetLoadMode.RequestedAssetAndDependencies;

        /// <summary>
        /// Determines how Assets are loaded when accessed.
        /// </summary>
        /// <remarks>
        /// Requested Asset And Dependencies, will only load the requested Asset (Recommended).
        /// All Packed Assets And Dependencies, will load all Assets that are packed together. Best used when loading all Assets into memory is required.
        ///</remarks>
        public AssetLoadMode AssetLoadMode
        {
            get { return m_AssetLoadMode; }
            set { m_AssetLoadMode = value; }
        }

        [SerializeField]
        long m_BundleSize;

        /// <summary>
        /// The size of the bundle, in bytes.
        /// </summary>
        public long BundleSize
        {
            get { return m_BundleSize; }
            set { m_BundleSize = value; }
        }

        [SerializeField]
        bool m_UseCrcForCachedBundles;

        /// <summary>
        /// If false, the CRC will not be used when loading bundles from the cache.
        /// </summary>
        public bool UseCrcForCachedBundle
        {
            get { return m_UseCrcForCachedBundles; }
            set { m_UseCrcForCachedBundles = value; }
        }

        [SerializeField]
        bool m_UseUWRForLocalBundles;

        /// <summary>
        /// If true, UnityWebRequest will be used even if the bundle is stored locally.
        /// </summary>
        public bool UseUnityWebRequestForLocalBundles
        {
            get { return m_UseUWRForLocalBundles; }
            set { m_UseUWRForLocalBundles = value; }
        }

        [SerializeField]
        bool m_ClearOtherCachedVersionsWhenLoaded;

        /// <summary>
        /// If false, the CRC will not be used when loading bundles from the cache.
        /// </summary>
        public bool ClearOtherCachedVersionsWhenLoaded
        {
            get { return m_ClearOtherCachedVersionsWhenLoaded; }
            set { m_ClearOtherCachedVersionsWhenLoaded = value; }
        }

        /// <summary>
        /// Computes the amount of data needed to be downloaded for this bundle.
        /// </summary>
        /// <param name="location">The location of the bundle.</param>
        /// <param name="resourceManager">The object that contains all the resource locations.</param>
        /// <returns>The size in bytes of the bundle that is needed to be downloaded.  If the local cache contains the bundle or it is a local bundle, 0 will be returned.</returns>
        public virtual long ComputeSize(IResourceLocation location, ResourceManager resourceManager)
        {
            var id = resourceManager == null ? location.InternalId : resourceManager.TransformInternalId(location);
            if (!ResourceManagerConfig.IsPathRemote(id))
                return 0;
            var locHash = Hash128.Parse(Hash);
#if ENABLE_CACHING
            //If we have a hash, ensure that our desired version is cached.
            if (locHash.isValid
                && Caching.IsVersionCached(new CachedAssetBundle(BundleName, locHash)))
                return 0;
#endif
            return BundleSize;
        }
    }

    /// <summary>
    /// Provides methods for loading an AssetBundle from a local or remote location.
    /// </summary>
    public class AssetBundleResource : IAssetBundleResource, IUpdateReceiver
    {
        /// <summary>
        /// Options for where an AssetBundle can be loaded from.
        /// </summary>
        public enum LoadType
        {
            /// <summary>
            /// Cannot determine where the AssetBundle is located.
            /// </summary>
            None,

            /// <summary>
            /// Load the AssetBundle from a local file location.
            /// </summary>
            Local,

            /// <summary>
            /// Download the AssetBundle from a web server.
            /// </summary>
            Web
        }

        internal enum CacheStatus
        {
            /// <summary>
            /// cache status has not been determined yet
            /// </summary>
            Unknown,
            /// <summary>
            /// the bundle is cached
            /// </summary>
            Cached,
            /// <summary>
            /// the bundle is not cached
            /// </summary>
            NotCached
        }

        AssetBundle m_AssetBundle;
        AsyncOperation m_RequestOperation;
        internal WebRequestQueueOperation m_WebRequestQueueOperation;
        internal ProvideHandle m_ProvideHandle;
        internal AssetBundleRequestOptions m_Options;
        internal CacheStatus cacheStatus;

        [NonSerialized]
        bool m_RequestCompletedCallbackCalled = false;

        int m_Retries;
        BundleSource m_Source = BundleSource.None;
        long m_BytesToDownload;
        long m_DownloadedBytes;
        bool m_Completed = false;
        AssetBundleUnloadOperation m_UnloadOperation;
        const int k_WaitForWebRequestMainThreadSleep = 1;
        string m_TransformedInternalId;
        AssetBundleRequest m_PreloadRequest;
        bool m_PreloadCompleted = false;
        ulong m_LastDownloadedByteCount = 0;
        float m_TimeoutTimer = 0;
        int m_TimeoutOverFrames = 0;
        internal bool m_DownloadOnly = false;
        int m_LastFrameCount = -1;
        float m_TimeSecSinceLastUpdate = 0;

        internal Func<UnityWebRequestResult, bool> m_RequestRetryCallback = x => x.ShouldRetryDownloadError();

        private bool HasTimedOut => m_Options != null && m_TimeoutTimer >= m_Options.Timeout && m_TimeoutOverFrames > 5;

        internal long BytesToDownload
        {
            get
            {
                if (m_BytesToDownload == -1)
                {
                    if (m_Options != null && !IsCached())
                        m_BytesToDownload = m_Options.ComputeSize(m_ProvideHandle.Location, m_ProvideHandle.ResourceManager);
                    else
                        m_BytesToDownload = 0;
                }

                return m_BytesToDownload;
            }
        }

        internal bool IsCached()
        {
#if !ENABLE_CACHING
            return false;
#else

            if (cacheStatus != CacheStatus.Unknown)
                return cacheStatus == CacheStatus.Cached;

            // only do this if the CacheStatus is unknown
            cacheStatus = CacheStatus.NotCached;
            var hash = Hash128.Parse(m_Options.Hash);
            if (hash.isValid)
            {
                CachedAssetBundle cachedBundle = new CachedAssetBundle(m_Options.BundleName, hash);
                bool cached = Caching.IsVersionCached(cachedBundle);
                if (cached)
                    cacheStatus = CacheStatus.Cached;
            }
            return cacheStatus == CacheStatus.Cached;
#endif
        }

        internal UnityWebRequest CreateWebRequest(IResourceLocation loc)
        {
            var url = m_ProvideHandle.ResourceManager.TransformInternalId(loc);
            return CreateWebRequest(url);
        }

        internal UnityWebRequest CreateWebRequest(string url)
        {
            string sanitizedUrl = Uri.UnescapeDataString(url);

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            Uri uri = new Uri(sanitizedUrl.Replace(" ", "%20"));
#else
            Uri uri = new Uri(Uri.EscapeUriString(sanitizedUrl));
#endif

            if (m_Options == null)
            {
                m_Source = BundleSource.Download;
#if ENABLE_PROFILER
                AddBundleToProfiler(Profiling.ContentStatus.Downloading, m_Source);
#endif
                return UnityWebRequestAssetBundle.GetAssetBundle(uri);
            }

            UnityWebRequest webRequest;
            if (!string.IsNullOrEmpty(m_Options.Hash))
            {
                bool cached = IsCached();
                CachedAssetBundle cachedBundle = new CachedAssetBundle(m_Options.BundleName, Hash128.Parse(m_Options.Hash));
                m_Source = cached ? BundleSource.Cache : BundleSource.Download;
                if (m_Options.UseCrcForCachedBundle || m_Source == BundleSource.Download)
                    webRequest = UnityWebRequestAssetBundle.GetAssetBundle(uri, cachedBundle, m_Options.Crc);
                else
                    webRequest = UnityWebRequestAssetBundle.GetAssetBundle(uri, cachedBundle);
            }
            else
            {
                m_Source = BundleSource.Download;
                webRequest = UnityWebRequestAssetBundle.GetAssetBundle(uri, m_Options.Crc);
            }

            if (m_Options.RedirectLimit >= 0 && m_Options.RedirectLimit < 129)
                webRequest.redirectLimit = m_Options.RedirectLimit;
            if (m_ProvideHandle.ResourceManager.CertificateHandlerInstance != null)
            {
                webRequest.certificateHandler = m_ProvideHandle.ResourceManager.CertificateHandlerInstance;
                webRequest.disposeCertificateHandlerOnDispose = false;
            }

            m_ProvideHandle.ResourceManager.WebRequestOverride?.Invoke(webRequest);
            return webRequest;
        }

        /// <summary>
        /// Creates a request for loading all assets from an AssetBundle.
        /// </summary>
        /// <returns>Returns the request.</returns>
        public AssetBundleRequest GetAssetPreloadRequest()
        {
            if (m_PreloadCompleted || GetAssetBundle() == null || m_Options == null)
                return null;

            if (m_Options.AssetLoadMode == AssetLoadMode.AllPackedAssetsAndDependencies)
            {
#if !UNITY_2021_1_OR_NEWER
                if (AsyncOperationHandle.IsWaitingForCompletion)
                {
                    m_AssetBundle.LoadAllAssets();
                    m_PreloadCompleted = true;
                    return null;
                }
#endif
                if (m_PreloadRequest == null)
                {
                    m_PreloadRequest = m_AssetBundle.LoadAllAssetsAsync();
                    m_PreloadRequest.completed += operation => m_PreloadCompleted = true;
                }

                return m_PreloadRequest;
            }

            return null;
        }

        float PercentComplete()
        {
            return m_RequestOperation != null ? m_RequestOperation.progress : 0.0f;
        }

        DownloadStatus GetDownloadStatus()
        {
            if (m_Options == null)
                return default;
            var status = new DownloadStatus() {TotalBytes = BytesToDownload, IsDone = PercentComplete() >= 1f};
            if (BytesToDownload > 0)
            {
                if (m_WebRequestQueueOperation != null && string.IsNullOrEmpty(m_WebRequestQueueOperation.m_WebRequest.error))
                    m_DownloadedBytes = (long)(m_WebRequestQueueOperation.m_WebRequest.downloadedBytes);
                else if (m_RequestOperation != null && m_RequestOperation is UnityWebRequestAsyncOperation operation && string.IsNullOrEmpty(operation.webRequest.error))
                    m_DownloadedBytes = (long)operation.webRequest.downloadedBytes;
            }

            status.DownloadedBytes = m_DownloadedBytes;
            return status;
        }

        /// <summary>
        /// Get the asset bundle object managed by this resource.  This call may force the bundle to load if not already loaded.
        /// </summary>
        /// <returns>The asset bundle.</returns>
        public AssetBundle GetAssetBundle()
        {
            if (m_ProvideHandle.IsValid)
            {
                Debug.Assert(!(m_ProvideHandle.Location is DownloadOnlyLocation), "GetAssetBundle does not return a value when an AssetBundle is download only.");
            }

            return m_AssetBundle;
        }

#if ENABLE_PROFILER
        private void AddBundleToProfiler(Profiling.ContentStatus status, BundleSource source)
        {
            if (!Profiler.enabled)
                return;
            if (!m_ProvideHandle.IsValid)
                return;
            if (m_Options == null)
                return;

            if (status == Profiling.ContentStatus.Active && m_AssetBundle == null) // is this going to suggest load only are released?
                Profiling.ProfilerRuntime.BundleReleased(m_Options.BundleName);
            else
                Profiling.ProfilerRuntime.AddBundleOperation(m_ProvideHandle, m_Options, status, source);
        }

        private void RemoveBundleFromProfiler()
        {
            if (m_Options == null)
                return;
            Profiling.ProfilerRuntime.BundleReleased(m_Options.BundleName);
        }
#endif
        void OnUnloadOperationComplete(AsyncOperation op)
        {
            m_UnloadOperation = null;
            BeginOperation();
        }

        /// <summary>
        /// Stores AssetBundle loading information, starts loading the bundle.
        /// </summary>
        /// <param name="provideHandle">The container for AssetBundle loading information.</param>
        /// <param name="unloadOp">The async operation for unloading the AssetBundle.</param>
        /// <param name="requestRetryCallback">The callback for retrying the AssetBundle download request.</param>
        public void Start(ProvideHandle provideHandle, AssetBundleUnloadOperation unloadOp, Func<UnityWebRequestResult, bool> requestRetryCallback)
        {
            m_Retries = 0;
            m_AssetBundle = null;
            m_RequestOperation = null;
            m_RequestCompletedCallbackCalled = false;
            m_ProvideHandle = provideHandle;
            m_Options = m_ProvideHandle.Location.Data as AssetBundleRequestOptions;
            m_BytesToDownload = -1;
            m_DownloadOnly = m_ProvideHandle.Location is DownloadOnlyLocation;
            if (m_DownloadOnly && m_Options == null)
            {
                m_ProvideHandle.Complete<AssetBundleResource>(null, false, new RemoteProviderException($"Attempt made to download bundle with stripped AssetBundleRequestOptions.  Ensure that StripDownloadOptions is not enabled for this bundle's group. '{m_TransformedInternalId}'."));
                return;
            }
            m_ProvideHandle.SetProgressCallback(PercentComplete);
            m_ProvideHandle.SetDownloadProgressCallbacks(GetDownloadStatus);
            m_ProvideHandle.SetWaitForCompletionCallback(WaitForCompletionHandler);
            m_RequestRetryCallback = requestRetryCallback;
            m_UnloadOperation = unloadOp;
            if (m_UnloadOperation != null && !m_UnloadOperation.isDone)
                m_UnloadOperation.completed += OnUnloadOperationComplete;
            else
                BeginOperation();
        }

        private bool WaitForCompletionHandler()
        {
            if (m_UnloadOperation != null && !m_UnloadOperation.isDone)
            {
                m_UnloadOperation.completed -= OnUnloadOperationComplete;
                m_UnloadOperation.WaitForCompletion();
                m_UnloadOperation = null;
                BeginOperation();
            }

            if (m_RequestOperation == null)
            {
                if (m_WebRequestQueueOperation == null)
                    return false;
                else
                    WebRequestQueue.WaitForRequestToBeActive(m_WebRequestQueueOperation, k_WaitForWebRequestMainThreadSleep);
            }

            //We don't want to wait for request op to complete if it's a LoadFromFileAsync. Only UWR will complete in a tight loop like this.
            if (m_RequestOperation is UnityWebRequestAsyncOperation op)
            {
                while (!UnityWebRequestUtilities.IsAssetBundleDownloaded(op))
                    System.Threading.Thread.Sleep(k_WaitForWebRequestMainThreadSleep);
#if ENABLE_ASYNC_ASSETBUNDLE_UWR
                if (m_Source == BundleSource.Cache)
                {
                    var downloadHandler = (DownloadHandlerAssetBundle)op?.webRequest?.downloadHandler;
                    if (downloadHandler.autoLoadAssetBundle)
                        m_AssetBundle = downloadHandler.assetBundle;
                }
#endif
                WebRequestQueue.DequeueRequest(op);

                if (!m_RequestCompletedCallbackCalled)
                {
                    m_RequestOperation.completed -= WebRequestOperationCompleted;
                    WebRequestOperationCompleted(m_RequestOperation);
                }
            }

            if (!m_Completed && m_Source == BundleSource.Local)
            {
                // we don't have to check for done with local files as calling
                // m_requestOperation.assetBundle is blocking and will wait for the file to load
                if (!m_RequestCompletedCallbackCalled)
                {
                    m_RequestOperation.completed -= LocalRequestOperationCompleted;
                    LocalRequestOperationCompleted(m_RequestOperation);
                }
            }

            if (!m_Completed && m_RequestOperation.isDone)
            {
                m_ProvideHandle.Complete(this, m_AssetBundle != null, null);
                m_Completed = true;
            }

            return m_Completed;
        }

        void AddCallbackInvokeIfDone(AsyncOperation operation, Action<AsyncOperation> callback)
        {
            if (operation.isDone)
                callback(operation);
            else
                operation.completed += callback;
        }

        /// <summary>
        /// Determines where an AssetBundle can be loaded from.
        /// </summary>
        /// <param name="handle">The container for AssetBundle loading information.</param>
        /// <param name="loadType">Specifies where an AssetBundle can be loaded from.</param>
        /// <param name="path">The file path or url where the AssetBundle is located.</param>
        public static void GetLoadInfo(ProvideHandle handle, out LoadType loadType, out string path)
        {
            GetLoadInfo(handle.Location, handle.ResourceManager, out loadType, out path);
        }

        internal static void GetLoadInfo(IResourceLocation location, ResourceManager resourceManager, out LoadType loadType, out string path)
        {
            var options = location?.Data as AssetBundleRequestOptions;
            if (options == null)
            {
                loadType = LoadType.Local;
                path = resourceManager.TransformInternalId(location);
                if (ResourceManagerConfig.ShouldPathUseWebRequest(path))
                    Debug.LogWarning($"Location {location} appears to be remote but the download option have been stripped.  Ensure that the group that contains this bundle does not have StripDownloadOptions enabled.");
                return;
            }

            path = resourceManager.TransformInternalId(location);
            if (Application.platform == RuntimePlatform.Android && path.StartsWith("jar:", StringComparison.Ordinal))
                loadType = options.UseUnityWebRequestForLocalBundles ? LoadType.Web : LoadType.Local;
            else if (ResourceManagerConfig.ShouldPathUseWebRequest(path))
                loadType = LoadType.Web;
            else if (options.UseUnityWebRequestForLocalBundles)
            {
                path = "file:///" + Path.GetFullPath(path);
                loadType = LoadType.Web;
            }
            else
                loadType = LoadType.Local;

            if (loadType == LoadType.Web)
                path = path.Replace('\\', '/');
        }

        private void BeginOperation()
        {
            // retrying a failed request will call BeginOperation multiple times. Any member variables
            // should be reset at the beginning of the operation
            m_DownloadedBytes = 0;
            m_RequestCompletedCallbackCalled = false;
            GetLoadInfo(m_ProvideHandle, out LoadType loadType, out m_TransformedInternalId);
            bool isDownloadOnly = m_ProvideHandle.Location is DownloadOnlyLocation;

            if (loadType == LoadType.Local)
            {
                //download only bundles loads should not load local bundles
                if (isDownloadOnly)
                {
                    m_Source = BundleSource.Local;
                    m_RequestOperation = null;
                    m_ProvideHandle.Complete<AssetBundleResource>(null, true, null);
                    m_Completed = true;
                    return;
                }
                LoadLocalBundle();
                return;
            }

            bool forceWebRequest = m_Options.UseCrcForCachedBundle;
            CachedAssetBundle cachedBundle = new CachedAssetBundle(m_Options.BundleName, Hash128.Parse(m_Options.Hash));
            bool cached = IsCached();
            // so if this is download only and we do not need to check CRC and we have a cached version,
            // we should do nothing
            bool skipWebDownload = loadType == LoadType.Web && isDownloadOnly && cached && !forceWebRequest;
            if (skipWebDownload)
            {
                m_Source = BundleSource.Cache;
                m_RequestOperation = null;
                m_ProvideHandle.Complete<AssetBundleResource>(null, true, null);
                m_Completed = true;
                return;
            }


            if (loadType == LoadType.Web)
            {
                m_WebRequestQueueOperation = EnqueueWebRequest(m_TransformedInternalId);
                AddBeginWebRequestHandler(m_WebRequestQueueOperation);
                return;
            }

            m_Source = BundleSource.None;
            m_RequestOperation = null;
            m_ProvideHandle.Complete<AssetBundleResource>(null, false,
                new RemoteProviderException(string.Format("Invalid path in AssetBundleProvider: '{0}'.", m_TransformedInternalId), m_ProvideHandle.Location));
            m_Completed = true;
        }

        private void LoadLocalBundle()
        {
            m_Source = BundleSource.Local;
#if !UNITY_2021_1_OR_NEWER
            if (AsyncOperationHandle.IsWaitingForCompletion)
                CompleteBundleLoad(AssetBundle.LoadFromFile(m_TransformedInternalId, m_Options == null ? 0 : m_Options.Crc));
            else
#endif
            {
                m_RequestOperation = AssetBundle.LoadFromFileAsync(m_TransformedInternalId, m_Options == null ? 0 : m_Options.Crc);
#if ENABLE_PROFILER
                AddBundleToProfiler(Profiling.ContentStatus.Loading, m_Source);
#endif
                AddCallbackInvokeIfDone(m_RequestOperation, LocalRequestOperationCompleted);
            }
        }

        internal WebRequestQueueOperation EnqueueWebRequest(string internalId)
        {
            var req = CreateWebRequest(internalId);
#if ENABLE_ASYNC_ASSETBUNDLE_UWR
            ((DownloadHandlerAssetBundle)req.downloadHandler).autoLoadAssetBundle = !(m_ProvideHandle.Location is DownloadOnlyLocation);
#endif
            req.disposeDownloadHandlerOnDispose = false;

            return WebRequestQueue.QueueRequest(req);
        }

        internal void AddBeginWebRequestHandler(WebRequestQueueOperation webRequestQueueOperation)
        {
            if (webRequestQueueOperation.IsDone)
            {
                BeginWebRequestOperation(webRequestQueueOperation.Result);
            }
            else
            {
#if ENABLE_PROFILER
                AddBundleToProfiler(Profiling.ContentStatus.Queue, m_Source);
#endif
                webRequestQueueOperation.OnComplete += asyncOp => BeginWebRequestOperation(asyncOp);
            }
        }

        private void BeginWebRequestOperation(AsyncOperation asyncOp)
        {
            m_TimeoutTimer = 0;
            m_TimeoutOverFrames = 0;
            m_LastDownloadedByteCount = 0;
            m_RequestOperation = asyncOp;
            if (m_RequestOperation == null || m_RequestOperation.isDone)
                WebRequestOperationCompleted(m_RequestOperation);
            else
            {
                if (m_Options != null && m_Options.Timeout > 0)
                    m_ProvideHandle.ResourceManager.AddUpdateReceiver(this);
#if ENABLE_PROFILER
                AddBundleToProfiler(m_Source == BundleSource.Cache ? Profiling.ContentStatus.Loading : Profiling.ContentStatus.Downloading, m_Source);
#endif
                m_RequestOperation.completed += WebRequestOperationCompleted;
            }
        }

        /// <inheritdoc/>
        public void Update(float unscaledDeltaTime)
        {
            if (m_RequestOperation != null && m_RequestOperation is UnityWebRequestAsyncOperation operation && !operation.isDone)
            {
                if (m_LastDownloadedByteCount != operation.webRequest.downloadedBytes)
                {
                    m_TimeoutTimer = 0;
                    m_TimeoutOverFrames = 0;
                    m_LastDownloadedByteCount = operation.webRequest.downloadedBytes;

                    m_LastFrameCount = -1;
                    m_TimeSecSinceLastUpdate = 0;
                }
                else
                {
                    float updateTime = unscaledDeltaTime;
                    if (m_LastFrameCount == Time.frameCount)
                    {
                        updateTime = Time.realtimeSinceStartup - m_TimeSecSinceLastUpdate;
                    }

                    m_TimeoutTimer += updateTime;
                    if (HasTimedOut)
                        operation.webRequest.Abort();
                    m_TimeoutOverFrames++;

                    m_LastFrameCount = Time.frameCount;
                    m_TimeSecSinceLastUpdate = Time.realtimeSinceStartup;
                }
            }
        }

        private void LocalRequestOperationCompleted(AsyncOperation op)
        {
            if (m_RequestCompletedCallbackCalled)
            {
                return;
            }

            m_RequestCompletedCallbackCalled = true;
            UnityWebRequestUtilities.LogOperationResult(op);
            CompleteBundleLoad((op as AssetBundleCreateRequest).assetBundle);
        }

        private void CompleteBundleLoad(AssetBundle bundle)
        {
            m_AssetBundle = bundle;
#if ENABLE_PROFILER
            AddBundleToProfiler(Profiling.ContentStatus.Active, m_Source);
#endif
            if (m_AssetBundle != null)
                m_ProvideHandle.Complete(this, true, null);
            else
                m_ProvideHandle.Complete<AssetBundleResource>(null, false,
                    new RemoteProviderException(string.Format("Invalid path in AssetBundleProvider: '{0}'.", m_TransformedInternalId), m_ProvideHandle.Location));
            m_Completed = true;
        }

        private void WebRequestOperationCompleted(AsyncOperation op)
        {
            if (m_RequestCompletedCallbackCalled)
                return;

            m_RequestCompletedCallbackCalled = true;

            if (m_Options != null && m_Options.Timeout > 0)
                m_ProvideHandle.ResourceManager.RemoveUpdateReciever(this);

            UnityWebRequestAsyncOperation remoteReq = op as UnityWebRequestAsyncOperation;
            var webReq = remoteReq?.webRequest;
            var downloadHandler = webReq?.downloadHandler as DownloadHandlerAssetBundle;
            UnityWebRequestResult uwrResult = null;
            if (webReq != null && !UnityWebRequestUtilities.RequestHasErrors(webReq, out uwrResult))
            {
                if (!m_Completed)
                {
                    if (!(m_ProvideHandle.Location is DownloadOnlyLocation))
                    {
                        // this loads the bundle into memory which we don't want to do with download only bundles
                        m_AssetBundle = downloadHandler.assetBundle;
                    }
#if ENABLE_PROFILER
                    AddBundleToProfiler(Profiling.ContentStatus.Active, m_Source);
#endif
                    downloadHandler.Dispose();
                    downloadHandler = null;
                    m_ProvideHandle.Complete(this, true, null);
                    m_Completed = true;
                }
#if ENABLE_CACHING
                if (m_Options != null)
                {
                    if (!string.IsNullOrEmpty(m_Options.Hash) && m_Options.ClearOtherCachedVersionsWhenLoaded)
                        Caching.ClearOtherCachedVersions(m_Options.BundleName, Hash128.Parse(m_Options.Hash));
                }
#endif
            }
            else
            {
                if (HasTimedOut)
                    uwrResult.Error = "Request timeout";
                webReq = m_WebRequestQueueOperation.m_WebRequest;
                if (uwrResult == null)
                    uwrResult = new UnityWebRequestResult(m_WebRequestQueueOperation.m_WebRequest);

                downloadHandler = webReq.downloadHandler as DownloadHandlerAssetBundle;
                downloadHandler.Dispose();
                downloadHandler = null;
                if (m_Options != null)
                {
                    bool forcedRetry = false;
                    string message = $"Web request failed, retrying ({m_Retries}/{m_Options.RetryCount})...\n{uwrResult}";
                    bool canRetryRequest = m_RequestRetryCallback.Invoke(uwrResult);
#if ENABLE_CACHING
                    if (!string.IsNullOrEmpty(m_Options.Hash))
                    {
                        if (m_Source == BundleSource.Cache)
                        {
                            message = $"Web request failed to load from cache. The cached AssetBundle will be cleared from the cache and re-downloaded. Retrying...\n{uwrResult}";
                            Caching.ClearCachedVersion(m_Options.BundleName, Hash128.Parse(m_Options.Hash));
                            // When attempted to load from cache we always retry on first attempt and failed
                            if (m_Retries == 0 && canRetryRequest)
                            {
                                Debug.LogFormat(message);
                                BeginOperation();
                                m_Retries++; //Will prevent us from entering an infinite loop of retrying if retry count is 0
                                forcedRetry = true;
                            }
                        }
                    }
#endif
                    if (!forcedRetry)
                    {
                        if (m_Retries < m_Options.RetryCount && canRetryRequest)
                        {
                            m_Retries++;
                            Debug.LogFormat(message);
                            BeginOperation();
                        }
                        else
                        {
                            message = $"Unable to load asset bundle from : {webReq.url}";
                            if (!canRetryRequest && m_Options.RetryCount > 0)
                                message += $"\nRetry count set to {m_Options.RetryCount} but cannot retry request due to error {uwrResult.Error}. To override use a custom AssetBundle provider.";
                            var exception = new RemoteProviderException(message, m_ProvideHandle.Location, uwrResult);
                            m_ProvideHandle.Complete<AssetBundleResource>(null, false, exception);
                            m_Completed = true;
#if ENABLE_PROFILER
                            RemoveBundleFromProfiler();
#endif
                        }
                    }
                }
            }

            webReq.Dispose();
        }

        /// <summary>
        /// Starts an async operation that unloads all resources associated with the AssetBundle.
        /// </summary>
        /// <param name="unloadOp">The async operation.</param>
        /// <returns>Returns true if the async operation object is valid.</returns>
        public bool Unload(out AssetBundleUnloadOperation unloadOp)
        {
            unloadOp = null;
            if (m_AssetBundle != null)
            {
                unloadOp = m_AssetBundle.UnloadAsync(true);
                m_AssetBundle = null;
            }

            m_RequestOperation = null;
#if ENABLE_PROFILER
            RemoveBundleFromProfiler();
#endif
            return unloadOp != null;
        }
    }

    /// <summary>
    /// IResourceProvider for asset bundles.  Loads bundles via UnityWebRequestAssetBundle API if the internalId starts with "http".  If not, it will load the bundle via AssetBundle.LoadFromFileAsync.
    /// </summary>
    [DisplayName("AssetBundle Provider")]
    public class AssetBundleProvider : ResourceProviderBase
    {
        internal static Dictionary<string, AssetBundleUnloadOperation> m_UnloadingBundles = new Dictionary<string, AssetBundleUnloadOperation>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Init()
        {
            m_UnloadingBundles = new Dictionary<string, AssetBundleUnloadOperation>();
        }

        /// <summary>
        /// Stores async operations that unload the requested AssetBundles.
        /// </summary>
        protected internal static Dictionary<string, AssetBundleUnloadOperation> UnloadingBundles
        {
            get { return m_UnloadingBundles; }
            internal set { m_UnloadingBundles = value; }
        }

        internal static int UnloadingAssetBundleCount => m_UnloadingBundles.Count;
        internal static int AssetBundleCount => AssetBundle.GetAllLoadedAssetBundles().Count() - UnloadingAssetBundleCount;
        internal static void WaitForAllUnloadingBundlesToComplete()
        {
            if (UnloadingAssetBundleCount > 0)
            {
                var bundles = m_UnloadingBundles.Values.ToArray();
                foreach (var b in bundles)
                    b.WaitForCompletion();
            }
        }

        /// <inheritdoc/>
        public override void Provide(ProvideHandle providerInterface)
        {
            if (m_UnloadingBundles.TryGetValue(providerInterface.Location.InternalId, out var unloadOp))
            {
                if (unloadOp.isDone)
                    unloadOp = null;
            }
            new AssetBundleResource().Start(providerInterface, unloadOp, ShouldRetryDownloadError);
        }

        /// <inheritdoc/>
        public override Type GetDefaultType(IResourceLocation location)
        {
            return typeof(IAssetBundleResource);
        }

        /// <summary>
        /// Releases the asset bundle via AssetBundle.Unload(true).
        /// </summary>
        /// <param name="location">The location of the asset to release</param>
        /// <param name="asset">The asset in question</param>
        public override void Release(IResourceLocation location, object asset)
        {
            if (location == null)
                throw new ArgumentNullException("location");
            if (asset == null)
            {
                if(!(location is DownloadOnlyLocation))
                    Debug.LogWarningFormat("Releasing null asset bundle from location {0}.  This is an indication that the bundle failed to load.", location);
                return;
            }

            var bundle = asset as AssetBundleResource;
            if (bundle != null)
            {
                if (bundle.Unload(out var unloadOp))
                {
                    m_UnloadingBundles.Add(location.InternalId, unloadOp);
                    unloadOp.completed += op => m_UnloadingBundles.Remove(location.InternalId);
                }
            }
        }

        /// <summary>
        /// Determines if the web request can be retried based on its result info.
        /// </summary>
        /// <param name="uwrResult">Result info about the web request.</param>
        /// <returns>Returns true if the web request can be retried. Otherwise returns false.</returns>
        public virtual bool ShouldRetryDownloadError(UnityWebRequestResult uwrResult)
        {
            return uwrResult.ShouldRetryDownloadError();
        }

        internal virtual IOperationCacheKey CreateCacheKeyForLocation(ResourceManager rm, IResourceLocation location, Type desiredType)
        {
            //We need to transform the ID first
            //so we don't try and load the same bundle twice if the user is manipulating the path at runtime.
            return new IdCacheKey(location.GetType(), rm.TransformInternalId(location));
        }
    }
}
