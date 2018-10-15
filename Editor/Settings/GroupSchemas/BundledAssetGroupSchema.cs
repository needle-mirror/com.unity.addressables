using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.ResourceManagement;

namespace UnityEditor.AddressableAssets
{
    /// <summary>
    /// Schema used for bundled asset groups.
    /// </summary>
    [CreateAssetMenu(fileName = "BundledAssetGroupSchema.asset", menuName = "Addressable Assets/Group Schemas/Bundled Assets")]
    public class BundledAssetGroupSchema : AddressableAssetGroupSchema, IHostingServiceConfigurationProvider, ISerializationCallbackReceiver
    {
        /// <summary>
        /// Defines how bundles are created.
        /// </summary>
        public enum BundlePackingMode
        {
            /// <summary>
            /// Pack all entries into as few bundles as possible (Scenes are put into separate bundles).
            /// </summary>
            PackTogether,
            /// <summary>
            /// Create a bundle per entry.  This is useful if each entry is a folder as all sub entries will go to the same bundle.
            /// </summary>
            PackSeparately
        }

        [SerializeField]
        [Tooltip("If true, the CRC and Hash values of the asset bundle are used to determine if a bundle can be loaded from the local cache instead of downloaded.")]
        private bool m_useAssetBundleCache = true;
        /// <summary>
        /// If true, the CRC and Hash values of the asset bundle are used to determine if a bundle can be loaded from the local cache instead of downloaded.
        /// </summary>
        public bool UseAssetBundleCache
        {
            get { return m_useAssetBundleCache; }
            set
            {
                m_useAssetBundleCache = value;
                SetDirty(true);
            }
        }

        [SerializeField]
        [Tooltip("Sets UnityWebRequest to attempt to abort after the number of seconds in timeout have passed.")]
        int m_timeout = 0;
        /// <summary>
        /// Sets UnityWebRequest to attempt to abort after the number of seconds in timeout have passed.
        /// </summary>
        public int Timeout { get { return m_timeout; } set { m_timeout = value; } }
        [SerializeField]
        [Tooltip("Indicates whether the UnityWebRequest system should employ the HTTP/1.1 chunked-transfer encoding method.")]
        bool m_chunkedTransfer = false;
        /// <summary>
        /// Indicates whether the UnityWebRequest system should employ the HTTP/1.1 chunked-transfer encoding method.
        /// </summary>
        public bool ChunkedTransfer { get { return m_chunkedTransfer; } set { m_chunkedTransfer = value; } }
        [SerializeField]
        [Tooltip("Indicates the number of redirects which this UnityWebRequest will follow before halting with a “Redirect Limit Exceeded” system error.")]
        int m_redirectLimit = -1;
        /// <summary>
        /// Indicates the number of redirects which this UnityWebRequest will follow before halting with a “Redirect Limit Exceeded” system error.
        /// </summary>
        public int RedirectLimit { get { return m_redirectLimit; } set { m_redirectLimit = value; } }
        [SerializeField]
        [Tooltip("Indicates the number of times the request will be retried.")]
        int m_retryCount = 0;
        /// <summary>
        /// Indicates the number of times the request will be retried.  
        /// </summary>
        public int RetryCount { get { return m_retryCount; } set { m_retryCount = value; } }

        [SerializeField]
        [Tooltip("The path to copy asset bundles to.")]
        private ProfileValueReference m_buildPath = new ProfileValueReference();
        /// <summary>
        /// The path to copy asset bundles to.
        /// </summary>
        public ProfileValueReference BuildPath
        {
            get { return m_buildPath; }
        }

        [SerializeField]
        [Tooltip("The path to load bundles from.")]
        private ProfileValueReference m_loadPath = new ProfileValueReference();
        /// <summary>
        /// The path to load bundles from.
        /// </summary>
        public ProfileValueReference LoadPath
        {
            get { return m_loadPath; }
        }

        [SerializeField]
        [Tooltip("Controls how bundles are packed.  If set to PackTogether, a single asset bundle will be created for the entire group, with the exception of scenes, which are packed separately.  If set to PackSeparately, an asset bundle will be created for each top level entry in the group.")]
        private BundlePackingMode m_bundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
        /// <summary>
        /// Controls how bundles are packed.  If set to PackTogether, a single asset bundle will be created for the entire group, with the exception of scenes, which are packed separately.  If set to PackSeparately, an asset bundle will be created for each top level entry in the group.
        /// </summary>
        public BundlePackingMode BundleMode
        {
            get { return m_bundleMode; }
            set
            {
                m_bundleMode = value;
                SetDirty(true);
            }
        }

        /// <inheritdoc/>
        public string HostingServicesContentRoot
        {
            get
            {
                return BuildPath.GetValue(Group.Settings);
            }
        }

        [SerializeField]
        [SerializedTypeRestriction(type = typeof(IResourceProvider))]
        [Tooltip("The provider type to use for loading asset bundles.")]
        private SerializedType m_assetBundleProviderType;
        /// <summary>
        /// The provider type to use for loading asset bundles.
        /// </summary>
        public SerializedType AssetBundleProviderType { get { return m_assetBundleProviderType; } }

        /// <summary>
        /// Set default values taken from the assigned group.
        /// </summary>
        /// <param name="group">The group this schema has been added to.</param>
        protected override void OnSetGroup(AddressableAssetGroup group)
        {
            BuildPath.SetVariableByName(group.Settings, AddressableAssetSettings.kLocalBuildPath);
            LoadPath.SetVariableByName(group.Settings, AddressableAssetSettings.kLocalLoadPath);
            m_assetBundleProviderType.Value = typeof(AssetBundleProvider);
        }

        /// <summary>
        /// Impementation of ISerializationCallbackReceiver, does nothing.
        /// </summary>
        public void OnBeforeSerialize()
        {
            
        }

        /// <summary>
        /// Impementation of ISerializationCallbackReceiver, used to set callbacks for ProfileValueReference changes.
        /// </summary>
        public void OnAfterDeserialize()
        {
            BuildPath.OnValueChanged += (s)=> SetDirty(true);
            LoadPath.OnValueChanged += (s) => SetDirty(true);
            if(m_assetBundleProviderType.Value == null)
                m_assetBundleProviderType.Value = typeof(AssetBundleProvider);
        }
    }
}