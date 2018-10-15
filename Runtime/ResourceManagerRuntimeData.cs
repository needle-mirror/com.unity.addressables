using System.Collections.Generic;
using UnityEngine.ResourceManagement;
using System;
using System.IO;


namespace UnityEngine.AddressableAssets
{
    /// <summary>
    /// Runtime data that is used to initialize the Addressables system.
    /// </summary>
    public class ResourceManagerRuntimeData
    {
        [SerializeField]
        string m_settingsHash;
        /// <summary>
        /// The hash of the settings that generated this runtime data.
        /// </summary>
        public string SettingsHash { get { return m_settingsHash; } set { m_settingsHash = value; } }
        [SerializeField]
        List<ResourceLocationData> m_catalogLocations = new List<ResourceLocationData>();
        /// <summary>
        /// List of catalog locations to download in order (try remote first, then local)
        /// </summary>
        public List<ResourceLocationData> CatalogLocations { get { return m_catalogLocations; } }
        [SerializeField]
        bool m_profileEvents = false;
        /// <summary>
        /// Flag to control whether the ResourceManager sends profiler events.
        /// </summary>
        public bool ProfileEvents { get { return m_profileEvents; } set { m_profileEvents = value; } }

        [SerializeField]
        bool m_logResourceManagerExceptions = true;
        /// <summary>
        /// When enabled, the ResourceManager.ExceptionHandler is set to (op, ex) => Debug.LogException(ex);
        /// </summary>
        public bool LogResourceManagerExceptions { get { return m_logResourceManagerExceptions; } set { m_logResourceManagerExceptions = value; } }

        [SerializeField]
        bool m_usePooledInstanceProvider = false;
        /// <summary>
        ///  obsolete - this will be refactored out of here.
        /// </summary>
        [Obsolete("This data has been moved to the ResourceProviderData for the instance provider.")]
        public bool UsePooledInstanceProvider { get { return m_usePooledInstanceProvider; } set { m_usePooledInstanceProvider = value; } }

        [SerializeField]
        int m_assetCacheSize = 25;
        /// <summary>
        /// obsolete - this will be refactored out of here.
        /// </summary>
        [Obsolete("This data has been moved to the ResourceProviderData for each provider.")]
        public int AssetCacheSize { get { return m_assetCacheSize; } set { m_assetCacheSize = value; } }
         [SerializeField]
        float m_assetCacheAge = 5;
        /// <summary>
        /// obsolete - this will be refactored out of here.
        /// </summary>
        [Obsolete("This data has been moved to the ResourceProviderData for each provider.")]
        public float AssetCacheAge { get { return m_assetCacheAge; } set { m_assetCacheAge = value; } }
        [SerializeField]
        int m_bundleCacheSize = 5;
        /// <summary>
        /// obsolete - this will be refactored out of here.
        /// </summary>
        [Obsolete("This data has been moved to the ResourceProviderData for each provider.")]
        public int BundleCacheSize { get { return m_bundleCacheSize; } set { m_bundleCacheSize = value; } }

        [SerializeField]
        float m_bundleCacheAge = 5;
        /// <summary>
        /// obsolete - this will be refactored out of here.
        /// </summary>
        [Obsolete("This data has been moved to the ResourceProviderData for each provider.")]
        public float BundleCacheAge { get { return m_bundleCacheAge; } set { m_bundleCacheAge = value; } }

        [SerializeField]
        ObjectInitializationData m_instanceProviderData;
        /// <summary>
        /// Data for the ResourceManager.InstanceProvider initialization;
        /// </summary>
        public ObjectInitializationData InstanceProviderData
        {
            get
            {
                return m_instanceProviderData;
            }
            set
            {
                m_instanceProviderData = value;
            }
        }

        [SerializeField]
        ObjectInitializationData m_sceneProviderData;
        /// <summary>
        /// Data for the ResourceManager.SceneProvider initialization.
        /// </summary>
        public ObjectInitializationData SceneProviderData
        {
            get
            {
                return m_sceneProviderData;
            }
            set
            {
                m_sceneProviderData = value;
            }
        }

        [SerializeField]
        List<ObjectInitializationData> m_resourceProviderData = new List<ObjectInitializationData>();
        /// <summary>
        /// The list of resource provider data.  Each entry will add an IResourceProvider to the ResourceManager.ResourceProviders list.
        /// </summary>
        public List<ObjectInitializationData> ResourceProviderData { get { return m_resourceProviderData; } }

        [SerializeField]
        List<ObjectInitializationData> m_extraInitializationData = new List<ObjectInitializationData>();
        /// <summary>
        /// The list of initialization data.  These objects will get deserialized and initialized during the Addressables initialization process.  This happens after resource providers have been set up but before any catalogs are loaded.
        /// </summary>
        public List<ObjectInitializationData> InitializationObjects { get { return m_extraInitializationData; } }
    }
}
