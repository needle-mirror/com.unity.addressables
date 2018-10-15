using System;
using UnityEngine.ResourceManagement;

namespace UnityEngine.AddressableAssets
{
    /// <summary>
    /// IInitializableObject that sets up the Caching system.
    /// </summary>
    [Serializable]
    public class CacheInitialization : IInitializableObject
    {
        /// <summary>
        /// Sets properties of the Caching system.
        /// </summary>
        /// <param name="id">The id of thei object.</param>
        /// <param name="dataStr">The JSON serialized CacheInitializationData object.</param>
        /// <returns>True if the initialization succeeded.</returns>
        public bool Initialize(string id, string dataStr)
        {
            var data = JsonUtility.FromJson<CacheInitializationData>(dataStr);
            if (data != null)
            {
                Debug.Log("Initializing caching system.");
                Caching.compressionEnabled = data.CompressionEnabled;
                var activeCache = Caching.currentCacheForWriting;
                if (!string.IsNullOrEmpty(data.CacheDirectoryOverride))
                    Caching.currentCacheForWriting = activeCache = Caching.AddCache(data.CacheDirectoryOverride);
                if (data.LimitCacheSize)
                    activeCache.maximumAvailableStorageSpace = data.MaximumCacheSize;
                else
                    activeCache.maximumAvailableStorageSpace = long.MaxValue;

                activeCache.expirationDelay = data.ExpirationDelay;
            }
            return true;
        }
    }

    /// <summary>
    /// Contains settings for the Caching system.
    /// </summary>
    [Serializable]
    public class CacheInitializationData
    {
        [SerializeField]
        bool m_compressionEnabled = true;
        /// <summary>
        /// Enable recompression of asset bundles into LZ4 format as they are saved to the cache.  This sets the Caching.compressionEnabled value.
        /// </summary>
        public bool CompressionEnabled { get { return m_compressionEnabled; } set { m_compressionEnabled = value; } }

        [SerializeField]
        string m_cacheDirectoryOverride = "";
        /// <summary>
        /// If not null or empty a new cache is created using Caching.AddCache and it is set active by assigning it to Caching.currentCacheForWriting.
        /// </summary>
        public string CacheDirectoryOverride { get { return m_cacheDirectoryOverride; } set { m_cacheDirectoryOverride = value; } }

        [SerializeField]
        int m_expirationDelay = 12960000;  //this value taken from the docs and is 150 days
        /// <summary>
        /// Controls how long bundles are kept in the cache. This value is applied to Caching.currentCacheForWriting.expirationDelay.  The value is in seconds and has a limit of 12960000 (150 days).
        /// </summary>
        public int ExpirationDelay { get { return m_expirationDelay; } set { m_expirationDelay = value; } }

        [SerializeField]
        bool m_limitCacheSize = false;
        /// <summary>
        /// If true, the maximum cache size will be set to MaximumCacheSize. 
        /// </summary>
        public bool LimitCacheSize { get { return m_limitCacheSize; } set { m_limitCacheSize = value; } }

        [SerializeField]
        long m_maximumCacheSize = long.MaxValue;
        /// <summary>
        /// The maximum size of the cache in bytes.  This value is applied to Caching.currentCacheForWriting.maximumAvailableStorageSpace.  This will only be set if LimitCacheSize is true.
        /// </summary>
        public long MaximumCacheSize { get { return m_maximumCacheSize; } set { m_maximumCacheSize = value; } }
    }
}