using System;
using System.Collections.Generic;
using UnityEngine.ResourceManagement;
using UnityEngine.Serialization;

namespace UnityEngine.AddressableAssets
{
    /// <summary>
    /// Runtime data that is used to initialize the Addressables system.
    /// </summary>
    [Serializable]
    public class ResourceManagerRuntimeData
    {
        [FormerlySerializedAs("m_settingsHash")]
        [SerializeField]
        string m_SettingsHash;
        /// <summary>
        /// The hash of the settings that generated this runtime data.
        /// </summary>
        public string SettingsHash { get { return m_SettingsHash; } set { m_SettingsHash = value; } }
        [FormerlySerializedAs("m_catalogLocations")]
        [SerializeField]
        List<ResourceLocationData> m_CatalogLocations = new List<ResourceLocationData>();
        /// <summary>
        /// List of catalog locations to download in order (try remote first, then local)
        /// </summary>
        public List<ResourceLocationData> CatalogLocations { get { return m_CatalogLocations; } }
        [FormerlySerializedAs("m_profileEvents")]
        [SerializeField]
        bool m_ProfileEvents;
        /// <summary>
        /// Flag to control whether the ResourceManager sends profiler events.
        /// </summary>
        public bool ProfileEvents { get { return m_ProfileEvents; } set { m_ProfileEvents = value; } }

        [FormerlySerializedAs("m_logResourceManagerExceptions")]
        [SerializeField]
        bool m_LogResourceManagerExceptions = true;
        /// <summary>
        /// When enabled, the ResourceManager.ExceptionHandler is set to (op, ex) => Debug.LogException(ex);
        /// </summary>
        public bool LogResourceManagerExceptions { get { return m_LogResourceManagerExceptions; } set { m_LogResourceManagerExceptions = value; } }

        [FormerlySerializedAs("m_usePooledInstanceProvider")]
        [SerializeField]
        bool m_UsePooledInstanceProvider;
        /// <summary>
        ///  obsolete - this will be refactored out of here.
        /// </summary>
        [Obsolete("This data has been moved to the ResourceProviderData for the instance provider.")]
        public bool UsePooledInstanceProvider { get { return m_UsePooledInstanceProvider; } set { m_UsePooledInstanceProvider = value; } }

        [FormerlySerializedAs("m_assetCacheSize")]
        [SerializeField]
        int m_AssetCacheSize = 25;
        /// <summary>
        /// obsolete - this will be refactored out of here.
        /// </summary>
        [Obsolete("This data has been moved to the ResourceProviderData for each provider.")]
        public int AssetCacheSize { get { return m_AssetCacheSize; } set { m_AssetCacheSize = value; } }
         [FormerlySerializedAs("m_assetCacheAge")]
         [SerializeField]
        float m_AssetCacheAge = 5;
        /// <summary>
        /// obsolete - this will be refactored out of here.
        /// </summary>
        [Obsolete("This data has been moved to the ResourceProviderData for each provider.")]
        public float AssetCacheAge { get { return m_AssetCacheAge; } set { m_AssetCacheAge = value; } }
        [FormerlySerializedAs("m_bundleCacheSize")]
        [SerializeField]
        int m_BundleCacheSize = 5;
        /// <summary>
        /// obsolete - this will be refactored out of here.
        /// </summary>
        [Obsolete("This data has been moved to the ResourceProviderData for each provider.")]
        public int BundleCacheSize { get { return m_BundleCacheSize; } set { m_BundleCacheSize = value; } }

        [FormerlySerializedAs("m_bundleCacheAge")]
        [SerializeField]
        float m_BundleCacheAge = 5;
        /// <summary>
        /// obsolete - this will be refactored out of here.
        /// </summary>
        [Obsolete("This data has been moved to the ResourceProviderData for each provider.")]
        public float BundleCacheAge { get { return m_BundleCacheAge; } set { m_BundleCacheAge = value; } }

        [FormerlySerializedAs("m_instanceProviderData")]
        [SerializeField]
        ObjectInitializationData m_InstanceProviderData;
        /// <summary>
        /// Data for the ResourceManager.InstanceProvider initialization;
        /// </summary>
        public ObjectInitializationData InstanceProviderData
        {
            get
            {
                return m_InstanceProviderData;
            }
            set
            {
                m_InstanceProviderData = value;
            }
        }

        [FormerlySerializedAs("m_sceneProviderData")]
        [SerializeField]
        ObjectInitializationData m_SceneProviderData;
        /// <summary>
        /// Data for the ResourceManager.SceneProvider initialization.
        /// </summary>
        public ObjectInitializationData SceneProviderData
        {
            get
            {
                return m_SceneProviderData;
            }
            set
            {
                m_SceneProviderData = value;
            }
        }

        [FormerlySerializedAs("m_resourceProviderData")]
        [SerializeField]
        List<ObjectInitializationData> m_ResourceProviderData = new List<ObjectInitializationData>();
        /// <summary>
        /// The list of resource provider data.  Each entry will add an IResourceProvider to the ResourceManager.ResourceProviders list.
        /// </summary>
        public List<ObjectInitializationData> ResourceProviderData { get { return m_ResourceProviderData; } }

        [FormerlySerializedAs("m_extraInitializationData")]
        [SerializeField]
        List<ObjectInitializationData> m_ExtraInitializationData = new List<ObjectInitializationData>();
        /// <summary>
        /// The list of initialization data.  These objects will get deserialized and initialized during the Addressables initialization process.  This happens after resource providers have been set up but before any catalogs are loaded.
        /// </summary>
        public List<ObjectInitializationData> InitializationObjects { get { return m_ExtraInitializationData; } }
    }
}
