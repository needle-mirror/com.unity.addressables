using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.ResourceManagement.Util;
using UnityEngine.Serialization;

namespace UnityEditor.AddressableAssets.Settings.GroupSchemas
{
    /// <summary>
    /// Schema used for bundled asset groups.
    /// </summary>
//    [CreateAssetMenu(fileName = "BundledAssetGroupSchema.asset", menuName = "Addressables/Group Schemas/Bundled Assets")]
    [DisplayName("Content Packing & Loading")]
    public class BundledAssetGroupSchema : AddressableAssetGroupSchema, ISerializationCallbackReceiver
    {
        /// <summary>
        /// Defines how bundles are created.
        /// </summary>
        public enum BundlePackingMode
        {
            /// <summary>
            /// Creates a bundle for all non-scene entries and another for all scenes entries.
            /// </summary>
            PackTogether,

            /// <summary>
            /// Creates a bundle per entry.  This is useful if each entry is a folder as all sub entries will go to the same bundle.
            /// </summary>
            PackSeparately,

            /// <summary>
            /// Creates a bundle per unique set of labels
            /// </summary>
            PackTogetherByLabel
        }

        /// <summary>
        /// Defines how internal bundles are named. This is used for both caching and for inter-bundle dependecies.  If possible, GroupGuidProjectIdHash should be used as it is stable and unique between projects.
        /// </summary>
        public enum BundleInternalIdMode
        {
            /// <summary>
            /// Use the guid of the group asset
            /// </summary>
            GroupGuid,

            /// <summary>
            /// Use the hash of the group asset guid and the project id
            /// </summary>
            GroupGuidProjectIdHash,

            /// <summary>
            /// Use the hash of the group asset, the project id and the guids of the entries in the group
            /// </summary>
            GroupGuidProjectIdEntriesHash
        }

        /// <summary>
        /// Options for compressing bundles in this group.
        /// </summary>
        public enum BundleCompressionMode
        {
            /// <summary>
            /// Use to indicate that bundles will not be compressed.
            /// </summary>
            Uncompressed,

            /// <summary>
            /// Use to indicate that bundles will be compressed using the LZ4 compression algorithm.
            /// </summary>
            LZ4,

            /// <summary>
            /// Use to indicate that bundles will be compressed using the LZMA compression algorithm.
            /// </summary>
            LZMA
        }

        [SerializeField]
        BundleInternalIdMode m_InternalBundleIdMode = BundleInternalIdMode.GroupGuidProjectIdHash;

        /// <summary>
        /// Internal bundle naming mode
        /// </summary>
        public BundleInternalIdMode InternalBundleIdMode
        {
            get => m_InternalBundleIdMode;
            set
            {
                if (m_InternalBundleIdMode != value)
                {
                    m_InternalBundleIdMode = value;
                    SetDirty(true);
                }
            }
        }

        [SerializeField]
        BundleCompressionMode m_Compression = BundleCompressionMode.LZ4;

        /// <summary>
        /// Build compression.
        /// </summary>
        public BundleCompressionMode Compression
        {
            get
            {
                if (UseDefaultSchemaSettings)
                    return GetDefaultSchemaSettings().compression;
                return m_Compression;
            }
            set
            {
                if (m_Compression != value)
                {
                    m_Compression = value;
                    SetDirty(true);
                }
            }
        }

        /// <summary>
        /// Options for internal id of assets in bundles.
        /// </summary>
        public enum AssetNamingMode
        {
            /// <summary>
            /// Use to identify assets by their full path.
            /// </summary>
            FullPath,

            /// <summary>
            /// Use to identify assets by their filename only.  There is a risk of collisions when assets in different folders have the same filename.
            /// </summary>
            Filename,

            /// <summary>
            /// Use to identify assets by their asset guid.  This will save space over using the full path and will be stable if assets move in the project.
            /// </summary>
            GUID,

            /// <summary>
            /// This method attempts to use the smallest identifier for internal asset ids.  For asset bundles with very few items, this can save a significant amount of space in the content catalog.
            /// </summary>
            Dynamic
        }

        [SerializeField]
        bool m_IncludeAddressInCatalog = true;

        [SerializeField]
        bool m_IncludeGUIDInCatalog = true;

        [SerializeField]
        bool m_IncludeLabelsInCatalog = true;

        /// <summary>
        /// If enabled, addresses are included in the content catalog.  This is required if assets are to be loaded via their main address.
        /// </summary>
        public bool IncludeAddressInCatalog
        {
            get => m_IncludeAddressInCatalog;
            set
            {
                if (m_IncludeAddressInCatalog != value)
                {
                    m_IncludeAddressInCatalog = value;
                    SetDirty(true);
                }
            }
        }

        /// <summary>
        /// If enabled, guids are included in content catalogs.  This is required if assets are to be loaded via AssetReference.
        /// </summary>
        public bool IncludeGUIDInCatalog
        {
            get => m_IncludeGUIDInCatalog;
            set
            {
                if (m_IncludeGUIDInCatalog != value)
                {
                    m_IncludeGUIDInCatalog = value;
                    SetDirty(true);
                }
            }
        }

        /// <summary>
        /// If enabled, labels are included in the content catalogs.  This is required if labels are used at runtime load load assets.
        /// </summary>
        public bool IncludeLabelsInCatalog
        {
            get => m_IncludeLabelsInCatalog;
            set
            {
                if (m_IncludeLabelsInCatalog != value)
                {
                    m_IncludeLabelsInCatalog = value;
                    SetDirty(true);
                }
            }
        }

        /// <summary>
        /// Internal Id mode for assets in bundles.
        /// </summary>
        public AssetNamingMode InternalIdNamingMode
        {
            get => m_InternalIdNamingMode;
            set
            {
                m_InternalIdNamingMode = value;
                SetDirty(true);
            }
        }

        [SerializeField]
        [Tooltip("Indicates how the internal asset name will be generated.")]
        AssetNamingMode m_InternalIdNamingMode = AssetNamingMode.FullPath;


        /// <summary>
        /// Behavior for clearing old bundles from the cache.
        /// </summary>
        public enum CacheClearBehavior
        {
            /// <summary>
            /// Bundles are only removed from the cache when space is needed.
            /// </summary>
            ClearWhenSpaceIsNeededInCache,

            /// <summary>
            /// Bundles are removed from the cache when a newer version has been loaded successfully.
            /// </summary>
            ClearWhenWhenNewVersionLoaded,
        }

        [SerializeField]
        CacheClearBehavior m_CacheClearBehavior = CacheClearBehavior.ClearWhenSpaceIsNeededInCache;

        /// <summary>
        /// Determines how other cached versions of asset bundles are cleared.
        /// </summary>
        public CacheClearBehavior AssetBundledCacheClearBehavior
        {
            get
            {
                if (UseDefaultSchemaSettings)
                    return GetDefaultSchemaSettings().assetBundledCacheClearBehavior;
                return m_CacheClearBehavior;
            }
            set
            {
                if (m_CacheClearBehavior != value)
                {
                    m_CacheClearBehavior = value;
                    SetDirty(true);
                }
            }
        }


        /// <summary>
        /// Gets the build compression settings for bundles in this group.
        /// </summary>
        /// <param name="bundleId">The bundle id.</param>
        /// <returns>The build compression.</returns>
        public virtual BuildCompression GetBuildCompressionForBundle(string bundleId)
        {
            //Unfortunately the BuildCompression struct is not serializable (nor is it settable), therefore this enum needs to be used to return the static members....
            switch (m_Compression)
            {
                case BundleCompressionMode.Uncompressed: return BuildCompression.Uncompressed;
                case BundleCompressionMode.LZ4: return BuildCompression.LZ4;
                case BundleCompressionMode.LZMA: return BuildCompression.LZMA;
            }

            return default(BuildCompression);
        }

        [FormerlySerializedAs("m_includeInBuild")]
        [SerializeField]
        [Tooltip("If true, the assets in this group will be included in the build of bundles.")]
        bool m_IncludeInBuild = true;

        /// <summary>
        /// If true, the assets in this group will be included in the build of bundles.
        /// </summary>
        public bool IncludeInBuild
        {
            get => m_IncludeInBuild;
            set
            {
                if (m_IncludeInBuild != value)
                {
                    m_IncludeInBuild = value;
                    SetDirty(true);
                }
            }
        }

        [SerializeField]
        [SerializedTypeRestriction(type = typeof(IResourceProvider))]
        [Tooltip("The provider type to use for loading assets from bundles.")]
        SerializedType m_BundledAssetProviderType;

        /// <summary>
        /// The provider type to use for loading assets from bundles.
        /// </summary>
        public SerializedType BundledAssetProviderType
        {
            get => m_BundledAssetProviderType;
            set
            {
                m_BundledAssetProviderType = value;
                SetDirty(true);
            }
        }
        [SerializeField]
        [Tooltip("If true, assetbundle download data will be stripped from the catalog.  This should only be enabled for local groups.  Only applies to binary catalogs.")]
        bool m_StripDownloadOptions = false;
        /// <summary>
        /// Strip unnecessary assetbundle download data from the catalog.  This should only be enabled for local groups.  Only applies to binary catalogs.
        /// </summary>
        public bool StripDownloadOptions
        {
            get
            {
                if (UseDefaultSchemaSettings)
                    return GetDefaultSchemaSettings().stripDownloadOptions;
                return m_StripDownloadOptions;
            }
            set
            {
                if (m_StripDownloadOptions != value)
                {
                    m_StripDownloadOptions = value;
                    SetDirty(true);
                }
            }
        }

        [SerializeField]
        [Tooltip("If true, the bundle and asset provider for assets in this group will get unique provider ids and will only provide for assets in this group.")]
        bool m_ForceUniqueProvider = false;

        /// <summary>
        /// If true, the bundle and asset provider for assets in this group will get unique provider ids and will only provide for assets in this group.
        /// </summary>
        public bool ForceUniqueProvider
        {
            get => m_ForceUniqueProvider;
            set
            {
                if (m_ForceUniqueProvider != value)
                {
                    m_ForceUniqueProvider = value;
                    SetDirty(true);
                }
            }
        }

        [FormerlySerializedAs("m_useAssetBundleCache")]
        [SerializeField]
        [Tooltip("If true, the Hash value of the asset bundle is used to determine if a bundle can be loaded from the local cache instead of downloaded. (Only applies to remote asset bundles)")]
        bool m_UseAssetBundleCache = true;

        /// <summary>
        /// If true, the CRC and Hash values of the asset bundle are used to determine if a bundle can be loaded from the local cache instead of downloaded.
        /// </summary>
        public bool UseAssetBundleCache
        {
            get
            {
                if (UseDefaultSchemaSettings)
                    return GetDefaultSchemaSettings().useAssetBundleCache;
                return m_UseAssetBundleCache;
            }
            set
            {
                if (m_UseAssetBundleCache != value)
                {
                    m_UseAssetBundleCache = value;
                    SetDirty(true);
                }
            }
        }

        [SerializeField]
        [Tooltip("If true, the CRC (Cyclic Redundancy Check) of the asset bundle is used to check the integrity.  This can be used for both local and remote bundles.")]
        bool m_UseAssetBundleCrc = true;

        /// <summary>
        /// If true, the CRC and Hash values of the asset bundle are used to determine if a bundle can be loaded from the local cache instead of downloaded.
        /// </summary>
        public bool UseAssetBundleCrc
        {
            get
            {
                if (UseDefaultSchemaSettings)
                    return GetDefaultSchemaSettings().useAssetBundleCrc;
                return m_UseAssetBundleCrc;
            }
            set
            {
                if (m_UseAssetBundleCrc != value)
                {
                    m_UseAssetBundleCrc = value;
                    SetDirty(true);
                }
            }
        }

        [SerializeField]
        [Tooltip("If true, the CRC (Cyclic Redundancy Check) of the asset bundle is used to check the integrity.")]
        bool m_UseAssetBundleCrcForCachedBundles = true;

        /// <summary>
        /// If true, the CRC and Hash values of the asset bundle are used to determine if a bundle can be loaded from the local cache instead of downloaded.
        /// </summary>
        public bool UseAssetBundleCrcForCachedBundles
        {
            get
            {
                if (UseDefaultSchemaSettings)
                    return GetDefaultSchemaSettings().useAssetBundleCrcForCachedBundles;
                return m_UseAssetBundleCrcForCachedBundles;
            }
            set
            {
                if (m_UseAssetBundleCrcForCachedBundles != value)
                {
                    m_UseAssetBundleCrcForCachedBundles = value;
                    SetDirty(true);
                }
            }
        }

        [SerializeField]
        [Tooltip("If true, local asset bundles will be loaded through UnityWebRequest.")]
        bool m_UseUWRForLocalBundles = false;

        /// <summary>
        /// If true, local asset bundles will be loaded through UnityWebRequest.
        /// </summary>
        public bool UseUnityWebRequestForLocalBundles
        {
            get => m_UseUWRForLocalBundles;
            set
            {
                if (m_UseUWRForLocalBundles != value)
                {
                    m_UseUWRForLocalBundles = value;
                    SetDirty(true);
                }
            }
        }

        [FormerlySerializedAs("m_timeout")]
        [SerializeField]
        [Tooltip("Attempt to abort after the number of seconds in timeout have passed, where the UnityWebRequest has received no data. (Only applies to remote asset bundles)")]
        [Min(0)]
        int m_Timeout;

        /// <summary>
        /// Attempt to abort after the number of seconds in timeout have passed, where the UnityWebRequest has received no data.
        /// Use 0 for no timeout
        /// </summary>
        public int Timeout
        {
            get => m_Timeout;
            set
            {
                if (value < 0)
                    value = 0;
                if (value > short.MaxValue)
                    value = short.MaxValue;
                if (m_Timeout != value)
                {
                    m_Timeout = value;
                    SetDirty(true);
                }
            }
        }

        [FormerlySerializedAs("m_chunkedTransfer")]
        [SerializeField]
        [Tooltip("Deprecated in 2019.3+. Indicates whether the UnityWebRequest system should employ the HTTP/1.1 chunked-transfer encoding method. (Only applies to remote asset bundles)")]
        bool m_ChunkedTransfer;

        /// <summary>
        /// Indicates whether the UnityWebRequest system should employ the HTTP/1.1 chunked-transfer encoding method.
        /// </summary>
        public bool ChunkedTransfer
        {
            get => m_ChunkedTransfer;
            set
            {
                if (m_ChunkedTransfer != value)
                {
                    m_ChunkedTransfer = value;
                    SetDirty(true);
                }
            }
        }


        [FormerlySerializedAs("m_redirectLimit")]
        [SerializeField]
        [Tooltip("Indicates the number of redirects which this UnityWebRequest will follow before halting with a “Redirect Limit Exceeded” system error. (Only applies to remote asset bundles)")]
        [Range(-1, 128)]
        int m_RedirectLimit = -1;

        /// <summary>
        /// Indicates the number of redirects which this UnityWebRequest will follow before halting with a “Redirect Limit Exceeded” system error.
        /// </summary>
        public int RedirectLimit
        {
            get => m_RedirectLimit;
            set
            {
                if (value < -1)
                    value = -1;
                if (value > 128)
                    value = 128;
                if (m_RedirectLimit != value)
                {
                    m_RedirectLimit = value;
                    SetDirty(true);
                }
            }
        }

        [FormerlySerializedAs("m_retryCount")]
        [SerializeField]
        [Tooltip("Indicates the number of times the request will be retried.")]
        [Range(0,128)]
        int m_RetryCount = 0;

        /// <summary>
        /// Indicates the number of times the request will be retried.
        /// </summary>
        public int RetryCount
        {
            get => m_RetryCount;
            set
            {
                if (value < 0)
                    value = 0;
                if (value > 128)
                    value = 128;
                if (m_RetryCount != value)
                {
                    m_RetryCount = value;
                    SetDirty(true);
                }
            }
        }

        [FormerlySerializedAs("m_buildPath")]
        [SerializeField]
        [Tooltip("The path to copy asset bundles to.")]
        ProfileValueReference m_BuildPath = new ProfileValueReference();

        /// <summary>
        /// The path to copy asset bundles to.
        /// </summary>
        public ProfileValueReference BuildPath
        {
            get { return m_BuildPath; }
        }

        [FormerlySerializedAs("m_loadPath")]
        [SerializeField]
        [Tooltip("The path to load bundles from.")]
        ProfileValueReference m_LoadPath = new ProfileValueReference();

        /// <summary>
        /// The path to load bundles from.
        /// </summary>
        public ProfileValueReference LoadPath
        {
            get { return m_LoadPath; }
        }

        //placeholder for UrlSuffix support...
        internal string UrlSuffix
        {
            get { return string.Empty; }
        }

        [FormerlySerializedAs("m_bundleMode")]
        [SerializeField]
        [Tooltip(
            "Controls how bundles are packed.  If set to PackTogether, a single asset bundle will be created for the entire group, with the exception of scenes, which are packed in a second bundle.  If set to PackSeparately, an asset bundle will be created for each entry in the group; in the case that an entry is a folder, one bundle is created for the folder and all of its sub entries.")]
        BundlePackingMode m_BundleMode = BundlePackingMode.PackTogether;

        /// <summary>
        /// Controls how bundles are packed.  If set to PackTogether, a single asset bundle will be created for the entire group, with the exception of scenes, which are packed in a second bundle.  If set to PackSeparately, an asset bundle will be created for each entry in the group; in the case that an entry is a folder, one bundle is created for the folder and all of its sub entries.
        /// </summary>
        public BundlePackingMode BundleMode
        {
            get => m_BundleMode;
            set
            {
                if (m_BundleMode != value)
                {
                    m_BundleMode = value;
                    SetDirty(true);
                }
            }
        }

        [FormerlySerializedAs("m_assetBundleProviderType")]
        [SerializeField]
        [SerializedTypeRestriction(type = typeof(IResourceProvider))]
        [Tooltip("The provider type to use for loading asset bundles.")]
        SerializedType m_AssetBundleProviderType;

        /// <summary>
        /// The provider type to use for loading asset bundles.
        /// </summary>
        public SerializedType AssetBundleProviderType
        {
            get => m_AssetBundleProviderType;
            set
            {
                m_AssetBundleProviderType = value;
                SetDirty(true);
            }
        }

        [SerializeField]
        bool m_UseDefaultSchemaSettings;
        /// <summary>
        /// Determines if user wants to override the default schema settings.
        /// </summary>
        public bool UseDefaultSchemaSettings
        {
            get
            {
                return m_UseDefaultSchemaSettings;
            }
            set
            {
                if (m_UseDefaultSchemaSettings != value)
                {
                    m_UseDefaultSchemaSettings = value;
                    SetDirty(true);
                }
            }
        }

        [SerializeField]
        int m_SelectedPathPairIndex;
        /// <summary>
        /// The selected path pair in use.
        /// Use this with care, as it could change when path pairs are added ore removed. It is generally more
        /// valid to lookup the path pair by Id for the current profile.
        /// </summary>
        public int SelectedPathPairIndex
        {
            get => m_SelectedPathPairIndex;
            set
            {
                if (m_SelectedPathPairIndex != value)
                {
                    m_SelectedPathPairIndex = value;
                    if (m_SelectedPathPairIndex < 0)
                        m_SelectedPathPairIndex = 0;
                    SetDirty(true);
                }
            }
        }

        /// <summary>
        /// Used to determine if dropdown should be custom
        /// </summary>
        internal bool m_UseCustomPaths = false;

        /// <summary>
        /// Internal settings
        /// </summary>
        internal AddressableAssetSettings settings
        {
            get { return AddressableAssetSettingsDefaultObject.Settings; }
        }

        private GUIContent m_BuildAndLoadPathsGUIContent = new GUIContent("Build & Load Paths", "Paths to build or load AssetBundles from");
        private GUIContent m_PathsPreviewGUIContent = new GUIContent("Path Preview", "Preview of what the current paths will be evaluated to");

        /// <summary>
        /// Set default values taken from the assigned group.
        /// </summary>
        /// <param name="group">The group this schema has been added to.</param>
        protected override void OnSetGroup(AddressableAssetGroup group)
        {
            //this can happen during the load of the addressables asset
        }

        internal void SetPathVariable(AddressableAssetSettings addressableAssetSettings, ref ProfileValueReference path, string newPathName, string oldPathName, List<string> variableNames)
        {
            if (path == null || !path.HasValue(addressableAssetSettings))
            {
                bool hasNewPath = variableNames.Contains(newPathName);
                bool hasOldPath = variableNames.Contains(oldPathName);

                if (hasNewPath && string.IsNullOrEmpty(path?.Id))
                {
                    path = new ProfileValueReference();
                    path.SetVariableByName(addressableAssetSettings, newPathName);
                    SetDirty(true);
                }
                else if (hasOldPath && string.IsNullOrEmpty(path?.Id))
                {
                    path = new ProfileValueReference();
                    path.SetVariableByName(addressableAssetSettings, oldPathName);
                    SetDirty(true);
                }
                else if (!hasOldPath && !hasNewPath)
                    Debug.LogWarning("Default path variable " + newPathName + " not found when initializing BundledAssetGroupSchema. Please manually set the path via the groups window.");
            }
        }

        internal override void Validate()
        {
            if (Group != null && Group.Settings != null)
            {
                List<string> variableNames = Group.Settings.profileSettings.GetVariableNames();
                SetPathVariable(Group.Settings, ref m_BuildPath, AddressableAssetSettings.kLocalBuildPath, "LocalBuildPath", variableNames);
                SetPathVariable(Group.Settings, ref m_LoadPath, AddressableAssetSettings.kLocalLoadPath, "LocalLoadPath", variableNames);
            }

            if (m_AssetBundleProviderType.Value == null)
                m_AssetBundleProviderType.Value = typeof(AssetBundleProvider);
            if (m_BundledAssetProviderType.Value == null)
                m_BundledAssetProviderType.Value = typeof(BundledAssetProvider);
        }

        internal string GetAssetLoadPath(string assetPath, HashSet<string> otherLoadPaths, Func<string, string> pathToGUIDFunc, bool isScene)
        {
            switch (InternalIdNamingMode)
            {
                case AssetNamingMode.FullPath: return assetPath;
                case AssetNamingMode.Filename: return isScene ? System.IO.Path.GetFileNameWithoutExtension(assetPath) : System.IO.Path.GetFileName(assetPath);
                case AssetNamingMode.GUID: return pathToGUIDFunc(assetPath);
                case AssetNamingMode.Dynamic:
                {
                    var g = pathToGUIDFunc(assetPath);
                    if (isScene || otherLoadPaths == null)
                        return g;
                    var len = 1;
                    var p = g.Substring(0, len);
                    while (otherLoadPaths.Contains(p))
                        p = g.Substring(0, ++len);
                    otherLoadPaths.Add(p);
                    return p;
                }
            }

            return assetPath;
        }

        /// <summary>
        /// Implementation of ISerializationCallbackReceiver. Does nothing.
        /// </summary>
        public void OnBeforeSerialize()
        {
        }

        /// <summary>
        /// Impementation of ISerializationCallbackReceiver. Used to set callbacks for ProfileValueReference changes.
        /// </summary>
        public void OnAfterDeserialize()
        {
            BuildPath.OnValueChanged += s => SetDirty(true);
            LoadPath.OnValueChanged += s => SetDirty(true);
            if (m_AssetBundleProviderType.Value == null)
                m_AssetBundleProviderType.Value = typeof(AssetBundleProvider);
            if (m_BundledAssetProviderType.Value == null)
                m_BundledAssetProviderType.Value = typeof(BundledAssetProvider);
        }

        /// <summary>
        /// Returns the id of the asset provider needed to load from this group.
        /// </summary>
        /// <returns>The id of the cached provider needed for this group.</returns>
        public string GetAssetCachedProviderId()
        {
            return ForceUniqueProvider ? string.Format("{0}_{1}", BundledAssetProviderType.Value.FullName, Group.Guid) : BundledAssetProviderType.Value.FullName;
        }

        /// <summary>
        /// Returns the id of the bundle provider needed to load from this group.
        /// </summary>
        /// <returns>The id of the cached provider needed for this group.</returns>
        public string GetBundleCachedProviderId()
        {
            return ForceUniqueProvider ? string.Format("{0}_{1}", AssetBundleProviderType.Value.FullName, Group.Guid) : AssetBundleProviderType.Value.FullName;
        }

        /// <summary>
        /// Used to determine how the final bundle name should look.
        /// </summary>
        public enum BundleNamingStyle
        {
            /// <summary>
            /// Use to indicate that the hash should be appended to the bundle name.
            /// </summary>
            AppendHash,

            /// <summary>
            /// Use to indicate that the bundle name should not contain the hash.
            /// </summary>
            NoHash,

            /// <summary>
            /// Use to indicate that the bundle name should only contain the given hash.
            /// </summary>
            OnlyHash,

            /// <summary>
            /// Use to indicate that the bundle name should only contain the hash of the file name.
            /// </summary>
            FileNameHash
        }

        /// <summary>
        /// Used to draw the Bundle Naming popup
        /// </summary>
        [CustomPropertyDrawer(typeof(BundleNamingStyle))]
        class BundleNamingStylePropertyDrawer : PropertyDrawer
        {
            /// <summary>
            /// Custom Drawer for the BundleNamingStyle in order to display easier to understand display names.
            /// </summary>
            public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
            {
                DrawGUI(position, property, label);
            }

            internal static int DrawGUI(Rect position, SerializedProperty property, GUIContent label)
            {
                bool showMixedValue = EditorGUI.showMixedValue;
                EditorGUI.BeginProperty(position, label, property);
                EditorGUI.showMixedValue = showMixedValue;

                GUIContent[] contents = new GUIContent[4];
                contents[0] = new GUIContent("Filename", "Leave filename unchanged.");
                contents[1] = new GUIContent("Append Hash to Filename", "Append filename with the AssetBundle content hash.");
                contents[2] = new GUIContent("Use Hash of AssetBundle", "Replace filename with AssetBundle hash.");
                contents[3] = new GUIContent("Use Hash of Filename", "Replace filename with hash of filename.");

                int enumValue = property.enumValueIndex;
                enumValue = enumValue == 0 ? 1 : enumValue == 1 ? 0 : enumValue;

                EditorGUI.BeginChangeCheck();
                int newValue = EditorGUI.Popup(position, new GUIContent(label.text, "Controls how the output AssetBundles will be named."), enumValue, contents);
                if (EditorGUI.EndChangeCheck())
                {
                    newValue = newValue == 0 ? 1 : newValue == 1 ? 0 : newValue;
                    property.enumValueIndex = newValue;
                }

                EditorGUI.EndProperty();
                return newValue;
            }
        }

        [SerializeField]
        BundleNamingStyle m_BundleNaming;

        /// <summary>
        /// Naming style to use for generated AssetBundle(s).
        /// </summary>
        public BundleNamingStyle BundleNaming
        {
            get
            {
                if (UseDefaultSchemaSettings)
                    return GetDefaultSchemaSettings().bundleNaming;
                return m_BundleNaming;
            }
            set
            {
                if (m_BundleNaming != value)
                {
                    m_BundleNaming = value;
                    SetDirty(true);
                }
            }
        }

        [SerializeField]
        AssetLoadMode m_AssetLoadMode;

        /// <summary>
        /// Will load all Assets into memory from the AssetBundle after the AssetBundle is loaded.
        /// </summary>
        public AssetLoadMode AssetLoadMode
        {
            get => m_AssetLoadMode;
            set
            {
                if (m_AssetLoadMode != value)
                {
                    m_AssetLoadMode = value;
                    SetDirty(true);
                }
            }
        }

        private bool m_ShowPaths = true;

        /// <summary>
        /// Used for drawing properties in the inspector.
        /// </summary>
        public override void ShowAllProperties()
        {
            m_ShowPaths = true;
            AdvancedOptionsFoldout.IsActive = true;
        }

        /// <inheritdoc/>
        public override void OnGUI()
        {
            ShowSelectedPropertyPathPair(SchemaSerializedObject);

            AdvancedOptionsFoldout.IsActive = GUI.AddressablesGUIUtility.FoldoutWithHelp(AdvancedOptionsFoldout.IsActive, new GUIContent("Advanced Options"), () =>
            {
                string url = AddressableAssetUtility.GenerateDocsURL("ContentPackingAndLoadingSchema.html#advanced-options");
                Application.OpenURL(url);
            });
            if (AdvancedOptionsFoldout.IsActive)
                ShowAdvancedProperties(SchemaSerializedObject);
            SchemaSerializedObject.ApplyModifiedProperties();
        }

        /// <inheritdoc/>
        public override void OnGUIMultiple(List<AddressableAssetGroupSchema> otherSchemas)
        {
            List<Action<BundledAssetGroupSchema, BundledAssetGroupSchema>> queuedChanges = null;

            List<BundledAssetGroupSchema> otherBundledSchemas = new List<BundledAssetGroupSchema>();
            foreach (var schema in otherSchemas)
            {
                otherBundledSchemas.Add(schema as BundledAssetGroupSchema);
            }

            foreach (var schema in otherBundledSchemas)
                schema.m_ShowPaths = m_ShowPaths;
            ShowSelectedPropertyPathPairMulti(SchemaSerializedObject, otherSchemas, ref queuedChanges,
                (src, dst) =>
                {
                    dst.m_BuildPath.Id = src.BuildPath.Id;
                    dst.m_LoadPath.Id = src.LoadPath.Id;
                    dst.m_UseCustomPaths = src.m_UseCustomPaths;
                    dst.SelectedPathPairIndex = src.SelectedPathPairIndex;
                    dst.SetDirty(true);
                });

            EditorGUI.BeginChangeCheck();
            AdvancedOptionsFoldout.IsActive = GUI.AddressablesGUIUtility.BeginFoldoutHeaderGroupWithHelp(AdvancedOptionsFoldout.IsActive, new GUIContent("Advanced Options"), () =>
            {
                string url = AddressableAssetUtility.GenerateDocsURL("ContentPackingAndLoadingSchema.html#advanced-options");
                Application.OpenURL(url);
            }, 10);
            if (AdvancedOptionsFoldout.IsActive)
            {
                ShowAdvancedPropertiesMulti(SchemaSerializedObject, otherSchemas, otherBundledSchemas, ref queuedChanges);
            }

            EditorGUI.EndFoldoutHeaderGroup();

            SchemaSerializedObject.ApplyModifiedProperties();
            if (queuedChanges != null)
            {
                Undo.SetCurrentGroupName("bundledAssetGroupSchemasUndos");
                foreach (var schema in otherBundledSchemas)
                    Undo.RecordObject(schema, "BundledAssetGroupSchema" + schema.name);

                foreach (var change in queuedChanges)
                {
                    foreach (var schema in otherBundledSchemas)
                        change.Invoke(this, schema);
                }
            }

            Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
        }

        void ShowPaths(SerializedObject so)
        {
            ShowSelectedPropertyPath(so, nameof(m_BuildPath), null, ref m_BuildPath);
            ShowSelectedPropertyPath(so, nameof(m_LoadPath), null, ref m_LoadPath);
        }

        void ShowPathsMulti(SerializedObject so, List<AddressableAssetGroupSchema> otherBundledSchemas, ref List<Action<BundledAssetGroupSchema, BundledAssetGroupSchema>> queuedChanges)
        {
            ShowSelectedPropertyMulti(so, nameof(m_BuildPath), null, otherBundledSchemas, ref queuedChanges, (src, dst) =>
            {
                dst.m_BuildPath.Id = src.BuildPath.Id;
                dst.SetDirty(true);
            }, m_BuildPath.Id, ref m_BuildPath);
            ShowSelectedPropertyMulti(so, nameof(m_LoadPath), null, otherBundledSchemas, ref queuedChanges, (src, dst) =>
            {
                dst.m_LoadPath.Id = src.LoadPath.Id;
                dst.SetDirty(true);
            }, m_LoadPath.Id, ref m_LoadPath);
        }

        static GUI.FoldoutSessionStateValue AdvancedOptionsFoldout = new GUI.FoldoutSessionStateValue("Addressables.BundledAssetGroup.AdvancedOptions");

        GUIContent m_StripDownloadOptionsContent = new GUIContent("Strip Bundle Download Options", "Strip unused asset bundle download data from catalog.  This should only be enabled for local groups and is disabled if UnityWebRequests are enabled for local bundles.");
        GUIContent m_CompressionContent = new GUIContent("Asset Bundle Compression", "Compression method to use for asset bundles.");
        GUIContent m_UseAssetBundleCacheContent = new GUIContent("Use Asset Bundle Cache", "If enabled and supported, the device will cache  asset bundles.");
        GUIContent m_AssetBundleCrcContent = new GUIContent("Asset Bundle CRC", "Defines which Asset Bundles will have their CRC checked when loading to ensure correct content.");

        private GUIContent[] m_CrcPopupContent = new GUIContent[]
        {
            new GUIContent("Disabled", "Bundles will not have their CRC checked when loading."),
            new GUIContent("Enabled, Including Cached", "All Bundles will have their CRC checked when loading."),
            new GUIContent("Enabled, Excluding Cached", "Bundles that have already been downloaded and cached will not have their CRC check when loading, otherwise CRC check will be performed.")
        };

        GUIContent m_IncludeAddressInCatalogContent = new GUIContent("Include Addresses in Catalog",
            "If disabled, addresses from this group will not be included in the catalog.  This is useful for reducing the size of the catalog if addresses are not needed.");

        GUIContent m_IncludeGUIDInCatalogContent = new GUIContent("Include GUIDs in Catalog",
            "If disabled, guids from this group will not be included in the catalog.  This is useful for reducing the size of the catalog if guids are not needed.");

        GUIContent m_IncludeLabelsInCatalogContent = new GUIContent("Include Labels in Catalog",
            "If disabled, labels from this group will not be included in the catalog.  This is useful for reducing the size of the catalog if labels are not needed.");

        GUIContent m_CacheClearBehaviorContent = new GUIContent("Cache Clear Behavior", "Controls how old cached asset bundles are cleared.");
        GUIContent m_BundleModeContent = new GUIContent("Bundle Mode", "Controls how bundles are created from this group.");
        GUIContent m_BundleNamingContent = new GUIContent("Bundle Naming Mode", "Controls the final file naming mode for bundles in this group.");

        private const string k_UseDefaultsLabel = "Use Defaults";
        GUIContent m_UseDefaultSettingsContent = new GUIContent(k_UseDefaultsLabel, $"Determines whether to use the default schema settings.");
        GUIContent m_UseDefaultSettingsContentDisabled = new GUIContent(k_UseDefaultsLabel, "This option is available when \"Build & Load Paths\" is set to \"Local\" or \"Remote\".");

        private float m_PostBlockContentSpace = 10;

        void ShowAdvancedProperties(SerializedObject so)
        {
            if (!m_UseCustomPaths)
            {
                var disableDefaultSchemaSettings = !HasDefaultSchemaSettings();
                EditorGUI.DisabledScope disableScope = new EditorGUI.DisabledScope(disableDefaultSchemaSettings);
                GUIContent toggleLabel = m_UseDefaultSettingsContent;
                if (disableDefaultSchemaSettings)
                    toggleLabel = m_UseDefaultSettingsContentDisabled;

                EditorGUI.BeginChangeCheck();
                bool useDefaultSettings = EditorGUILayout.Toggle(toggleLabel, m_UseDefaultSchemaSettings);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(so.targetObject, so.targetObject.name + nameof(UseDefaultSchemaSettings));
                    UseDefaultSchemaSettings = useDefaultSettings;
                }

                disableScope.Dispose();
            }
            using (new EditorGUI.DisabledScope(!m_UseCustomPaths && UseDefaultSchemaSettings))
            {
                EditorGUI.BeginChangeCheck();
                var compression = (BundleCompressionMode)EditorGUILayout.EnumPopup(m_CompressionContent, Compression);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(so.targetObject, so.targetObject.name + nameof(Compression));
                    Compression = compression;
                }

                bool buildTargetSupportsBundleCaching = BuildTargetSupportsBundleCaching(EditorUserBuildSettings.activeBuildTarget);
                if (buildTargetSupportsBundleCaching)
                {
                    EditorGUI.BeginChangeCheck();
                    bool useAssetBundleCache = EditorGUILayout.Toggle(m_UseAssetBundleCacheContent, UseAssetBundleCache);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(so.targetObject, so.targetObject.name + nameof(UseAssetBundleCache));
                        UseAssetBundleCache = useAssetBundleCache;
                    }

                    if (UseAssetBundleCache)
                    {
                        EditorGUI.BeginChangeCheck();
                        var cacheClearBehavior = (CacheClearBehavior)EditorGUILayout.EnumPopup(m_CacheClearBehaviorContent, AssetBundledCacheClearBehavior);
                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(so.targetObject, so.targetObject.name + nameof(AssetBundledCacheClearBehavior));
                            AssetBundledCacheClearBehavior = cacheClearBehavior;
                        }
                    }
                }
                CRCPropertyPopupField(so, buildTargetSupportsBundleCaching);

                EditorGUI.BeginChangeCheck();
                var bundleNaming = (BundleNamingStyle)EditorGUILayout.EnumPopup(m_BundleNamingContent, BundleNaming);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(so.targetObject, so.targetObject.name + nameof(BundleNaming));
                    BundleNaming = bundleNaming;
                }
                EditorGUI.BeginDisabledGroup(settings.UseUnityWebRequestForLocalBundles);
                EditorGUI.BeginChangeCheck();
                bool stripDLOptions = EditorGUILayout.Toggle(m_StripDownloadOptionsContent, StripDownloadOptions);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(so.targetObject, so.targetObject.name + nameof(StripDownloadOptions));
                    StripDownloadOptions = stripDLOptions;
                }
                EditorGUI.EndDisabledGroup();
            }
            GUILayout.Space(m_PostBlockContentSpace);


            EditorGUILayout.PropertyField(so.FindProperty(nameof(m_IncludeAddressInCatalog)), m_IncludeAddressInCatalogContent, true);
            EditorGUILayout.PropertyField(so.FindProperty(nameof(m_IncludeGUIDInCatalog)), m_IncludeGUIDInCatalogContent, true);
            EditorGUILayout.PropertyField(so.FindProperty(nameof(m_IncludeLabelsInCatalog)), m_IncludeLabelsInCatalogContent, true);
            EditorGUILayout.PropertyField(so.FindProperty(nameof(m_BundleMode)), m_BundleModeContent, true);
        }

        void CRCPropertyPopupField(SerializedObject so, bool buildTargetSupportsCaching)
        {
            if (buildTargetSupportsCaching)
            {
                int enumIndex = 0;
                if (UseAssetBundleCrc)
                    enumIndex = UseAssetBundleCrcForCachedBundles ? 1 : 2;

                int newEnumIndex = EditorGUILayout.Popup(m_AssetBundleCrcContent, enumIndex, m_CrcPopupContent);
                if (enumIndex != newEnumIndex)
                {
                    if (newEnumIndex != 0)
                    {
                        if (!UseAssetBundleCrc)
                        {
                            Undo.RecordObject(so.targetObject, so.targetObject.name + nameof(UseAssetBundleCrc));
                            UseAssetBundleCrc = true;
                        }
                        if (newEnumIndex == 1 && !UseAssetBundleCrcForCachedBundles)
                        {
                            Undo.RecordObject(so.targetObject, so.targetObject.name + nameof(UseAssetBundleCrcForCachedBundles));
                            UseAssetBundleCrcForCachedBundles = true;
                        }
                        else if (newEnumIndex == 2 && UseAssetBundleCrcForCachedBundles)
                        {
                            Undo.RecordObject(so.targetObject, so.targetObject.name + nameof(UseAssetBundleCrcForCachedBundles));
                            UseAssetBundleCrcForCachedBundles = false;
                        }
                    }
                    else
                    {
                        Undo.RecordObject(so.targetObject, so.targetObject.name + nameof(UseAssetBundleCrc));
                        UseAssetBundleCrc = false;
                    }
                }
            }
            else
            {
                EditorGUI.BeginChangeCheck();
                bool useAssetbundlecrc = EditorGUILayout.Toggle(m_AssetBundleCrcContent, UseAssetBundleCrc);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(so.targetObject, so.targetObject.name + nameof(UseAssetBundleCrc));
                    UseAssetBundleCrc = useAssetbundlecrc;
                }
            }
        }

        void CRCPropertyPopupFieldMulti(SerializedObject so, bool buildTargetSupportsCaching, List<BundledAssetGroupSchema> otherBundledSchemas, ref List<Action<BundledAssetGroupSchema, BundledAssetGroupSchema>> queuedChanges)
        {
            if (buildTargetSupportsCaching)
            {
                ShowMixedValue(this, otherBundledSchemas, (a, b) => a.UseAssetBundleCrc != b.UseAssetBundleCrc);
                if (!EditorGUI.showMixedValue)
                    ShowMixedValue(this, otherBundledSchemas, (a, b) => a.UseAssetBundleCrcForCachedBundles != b.UseAssetBundleCrcForCachedBundles);

                EditorGUI.BeginChangeCheck();
                CRCPropertyPopupField(so, buildTargetSupportsCaching);
                if (EditorGUI.EndChangeCheck())
                {
                    AddQueuedChanges(ref queuedChanges,
                        (src, dst) =>
                        {
                            dst.UseAssetBundleCrc = src.UseAssetBundleCrc;
                            dst.UseAssetBundleCrcForCachedBundles = src.UseAssetBundleCrcForCachedBundles;
                        });
                    EditorUtility.SetDirty(this);
                }
                EditorGUI.showMixedValue = false;
            }
        }


        void ShowMixedValue(BundledAssetGroupSchema schema, List<BundledAssetGroupSchema> otherBundledSchemas, Func<BundledAssetGroupSchema, BundledAssetGroupSchema, bool> showMixedValue)
        {
            foreach (BundledAssetGroupSchema bundledSchema in otherBundledSchemas)
            {
                if (showMixedValue.Invoke(schema, bundledSchema))
                {
                    EditorGUI.showMixedValue = true;
                    break;
                }
            }
        }

        void ShowAdvancedPropertiesMulti(SerializedObject so, List<AddressableAssetGroupSchema> otherSchemas, List<BundledAssetGroupSchema> otherBundledSchemas, ref List<Action<BundledAssetGroupSchema, BundledAssetGroupSchema>> queuedChanges)
        {
            ShowSelectedPropertyDefaultSettingsMulti(so, otherBundledSchemas, ref queuedChanges);
            GUILayout.Space(m_PostBlockContentSpace);

            ShowSelectedPropertyMulti(so, nameof(m_IncludeAddressInCatalog), m_IncludeAddressInCatalogContent, otherSchemas, ref queuedChanges,
                (src, dst) => dst.IncludeAddressInCatalog = src.IncludeAddressInCatalog, ref m_IncludeAddressInCatalog);
            ShowSelectedPropertyMulti(so, nameof(m_IncludeGUIDInCatalog), m_IncludeGUIDInCatalogContent, otherSchemas, ref queuedChanges,
                (src, dst) => dst.IncludeGUIDInCatalog = src.IncludeGUIDInCatalog, ref m_IncludeGUIDInCatalog);
            ShowSelectedPropertyMulti(so, nameof(m_IncludeLabelsInCatalog), m_IncludeLabelsInCatalogContent, otherSchemas, ref queuedChanges,
                (src, dst) => dst.IncludeLabelsInCatalog = src.IncludeLabelsInCatalog, ref m_IncludeLabelsInCatalog);
            ShowSelectedPropertyMulti(so, nameof(m_BundleMode), m_BundleModeContent, otherSchemas, ref queuedChanges, (src, dst) => dst.BundleMode = src.BundleMode, ref m_BundleMode);
        }

        void ShowSelectedPropertyMulti<T>(SerializedObject so, string propertyName, GUIContent label, List<AddressableAssetGroupSchema> otherSchemas,
            ref List<Action<BundledAssetGroupSchema, BundledAssetGroupSchema>> queuedChanges, Action<BundledAssetGroupSchema, BundledAssetGroupSchema> a, ref T propertyValue)
        {
            SerializedProperty serializedProperty = so.FindProperty(propertyName);
            Type propertySystemType = typeof(T);
            if (label == null)
                label = new GUIContent(serializedProperty.displayName);
            ShowMixedValue(serializedProperty, otherSchemas, propertySystemType, propertyName);

            T newValue = default(T);
            SerializedPropertyType serializedPropertyType = SerializedPropertyType.Generic;
            EditorGUI.BeginChangeCheck();
            if (propertySystemType == typeof(bool))
            {
                newValue = (T)(object)EditorGUILayout.Toggle(label, (bool)(object)propertyValue);
                serializedPropertyType = SerializedPropertyType.Boolean;
            }
            else if (propertySystemType.IsEnum)
            {
                serializedPropertyType = SerializedPropertyType.Enum;
                if (propertySystemType == typeof(BundleNamingStyle))
                {
                    Rect rect = EditorGUILayout.GetControlRect();
                    int enumValue = BundleNamingStylePropertyDrawer.DrawGUI(rect, serializedProperty, label);
                    newValue = (T)(object)enumValue;
                }
                else
                {
                    int enumValue = Convert.ToInt32(EditorGUILayout.EnumPopup(label, (Enum)(object)propertyValue));
                    newValue = (T)(object)enumValue;
                }
            }
            else if (propertySystemType == typeof(int))
            {
                newValue = (T)(object)EditorGUILayout.IntField(label, (int)(object)propertyValue);
                serializedPropertyType = SerializedPropertyType.Integer;
            }
            else
            {
                EditorGUILayout.PropertyField(serializedProperty, label, true);
                so.ApplyModifiedProperties();
            }
            if (EditorGUI.EndChangeCheck())
            {
                if (serializedPropertyType != SerializedPropertyType.Generic)
                {
                    HashSet<SerializedProperty> properties = new HashSet<SerializedProperty>() {serializedProperty};
                    foreach (AddressableAssetGroupSchema otherSchema in otherSchemas)
                        properties.Add(otherSchema.SchemaSerializedObject.FindProperty(propertyName));

                    foreach (SerializedProperty propertyForValueDestination in properties)
                    {
                        var destinationSerializedObject = propertyForValueDestination.serializedObject;
                        switch (serializedPropertyType)
                        {
                            case SerializedPropertyType.Boolean:
                                propertyForValueDestination.boolValue = (bool)(object)newValue;
                                break;
                            case SerializedPropertyType.Integer:
                                propertyForValueDestination.intValue = (int)(object)newValue;
                                break;
                            case SerializedPropertyType.Enum:
                                propertyForValueDestination.enumValueIndex = (int)(object)newValue;
                                break;
                        }

                        destinationSerializedObject.ApplyModifiedProperties();
                    }
                }
                else if (a != null)
                {
                    if (queuedChanges == null)
                        queuedChanges = new List<Action<BundledAssetGroupSchema, BundledAssetGroupSchema>>();
                    queuedChanges.Add(a);
                }
            }

            EditorGUI.showMixedValue = false;
        }

        void ShowSelectedPropertyMulti(SerializedObject so, string propertyName, GUIContent label,
            List<AddressableAssetGroupSchema> otherSchemas,
            ref List<Action<BundledAssetGroupSchema, BundledAssetGroupSchema>> queuedChanges,
            Action<BundledAssetGroupSchema, BundledAssetGroupSchema> a, string previousValue, ref ProfileValueReference currentValue)
        {
            var prop = so.FindProperty(propertyName);
            ShowMixedValue(prop, otherSchemas, typeof(ProfileValueReference), propertyName);

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(prop, label, true);
            if (EditorGUI.EndChangeCheck())
            {
                var newValue = currentValue.Id;
                currentValue.Id = previousValue;
                Undo.RecordObject(so.targetObject, so.targetObject.name + propertyName);
                currentValue.Id = newValue;
                if (queuedChanges == null)
                    queuedChanges = new List<Action<BundledAssetGroupSchema, BundledAssetGroupSchema>>();
                queuedChanges.Add(a);
            }

            EditorGUI.showMixedValue = false;
        }

        void ShowSelectedPropertyPath(SerializedObject so, string propertyName, GUIContent label, ref ProfileValueReference currentValue)
        {
            var prop = so.FindProperty(propertyName);
            string previousValue = currentValue.Id;
            EditorGUI.BeginChangeCheck();
            //Current implementation using ProfileValueReferenceDrawer
            EditorGUILayout.PropertyField(prop, label, true);
            if (EditorGUI.EndChangeCheck())
            {
                var newValue = currentValue.Id;
                currentValue.Id = previousValue;
                Undo.RecordObject(so.targetObject, so.targetObject.name + propertyName);
                currentValue.Id = newValue;
                EditorUtility.SetDirty(this);
            }

            EditorGUI.showMixedValue = false;
        }

        void ShowSelectedPropertyPathPairMulti(SerializedObject so, List<AddressableAssetGroupSchema> otherSchemas, ref List<Action<BundledAssetGroupSchema, BundledAssetGroupSchema>> queuedChanges,
            Action<BundledAssetGroupSchema, BundledAssetGroupSchema> a)
        {
            var buildPathProperty = so.FindProperty(nameof(m_BuildPath));
            var loadPathProperty = so.FindProperty(nameof(m_LoadPath));
            ShowMixedValue(buildPathProperty, otherSchemas, typeof(ProfileValueReference), nameof(m_BuildPath));
            ShowMixedValue(loadPathProperty, otherSchemas, typeof(ProfileValueReference), nameof(m_LoadPath));

            List<ProfileGroupType> groupTypes = ProfileGroupType.CreateGroupTypes(settings.profileSettings.GetProfile(settings.activeProfileId), settings);
            List<string> options = groupTypes.Select(group => group.GroupTypePrefix).ToList();
            //set selected to custom
            options.Add(AddressableAssetProfileSettings.customEntryString);
            int? selected = null;

            //Determine selection and whether to show custom
            if (!EditorGUI.showMixedValue)
            {
                //disregard custom value, want to check if valid pair
                selected = DetermineSelectedIndex(groupTypes, options.Count - 1, settings);
                if (selected != options.Count - 1)
                {
                    m_UseCustomPaths = false;
                }
                else
                {
                    m_UseCustomPaths = true;
                }
            }

            //Dropdown selector
            EditorGUI.BeginChangeCheck();
            var newIndex = EditorGUILayout.Popup(m_BuildAndLoadPathsGUIContent, selected.HasValue ? selected.Value : -1, options.ToArray());
            if (EditorGUI.EndChangeCheck() && newIndex != selected)
            {
                selected = newIndex;
                SetPathPairOption(so, options, groupTypes, newIndex);

                if (queuedChanges == null)
                    queuedChanges = new List<Action<BundledAssetGroupSchema, BundledAssetGroupSchema>>();
                queuedChanges.Add(a);
                EditorGUI.showMixedValue = false;
            }

            if (m_UseCustomPaths && selected.HasValue)
            {
                ShowPathsMulti(so, otherSchemas, ref queuedChanges);
            }

            ShowPathsPreview(!selected.HasValue);
            EditorGUI.showMixedValue = false;
        }

        void ShowSelectedPropertyPathPair(SerializedObject so)
        {
            List<ProfileGroupType> groupTypes = ProfileGroupType.CreateGroupTypes(settings.profileSettings.GetProfile(settings.activeProfileId), settings);
            List<string> options = groupTypes.Select(group => group.GroupTypePrefix).ToList();
            //Set selected to custom
            options.Add(AddressableAssetProfileSettings.customEntryString);

            //Determine selection and whether to show custom

            int? selected = DetermineSelectedIndex(groupTypes, options.Count - 1, settings);
            if (selected.HasValue && selected != options.Count - 1)
            {
                m_UseCustomPaths = false;
            }
            else
            {
                m_UseCustomPaths = true;
            }

            //Dropdown selector
            EditorGUI.BeginChangeCheck();
            var newIndex = EditorGUILayout.Popup(m_BuildAndLoadPathsGUIContent, selected.HasValue ? selected.Value : options.Count - 1, options.ToArray());
            if (EditorGUI.EndChangeCheck() && newIndex != selected)
            {
                SetPathPairOption(so, options, groupTypes, newIndex);
                EditorUtility.SetDirty(this);
            }

            if (m_UseCustomPaths)
            {
                ShowPaths(so);
            }

            ShowPathsPreview(false);
            EditorGUI.showMixedValue = false;
        }

        internal int DetermineSelectedIndex(List<ProfileGroupType> groupTypes, int defaultValue, AddressableAssetSettings addressableAssetSettings)
        {
            HashSet<string> vars = addressableAssetSettings.profileSettings.GetAllVariableIds();
            return DetermineSelectedIndex(groupTypes, defaultValue, addressableAssetSettings, vars);
        }

        internal int DetermineSelectedIndex(List<ProfileGroupType> groupTypes, int defaultValue, AddressableAssetSettings addressableAssetSettings, HashSet<string> vars)
        {
            int selected = defaultValue;

            if (addressableAssetSettings == null)
                return defaultValue;

            if (vars.Contains(m_BuildPath.Id) && vars.Contains(m_LoadPath.Id) && !m_UseCustomPaths)
            {
                for (int i = 0; i < groupTypes.Count; i++)
                {
                    ProfileGroupType.GroupTypeVariable buildPathVar = groupTypes[i].GetVariableBySuffix("BuildPath");
                    ProfileGroupType.GroupTypeVariable loadPathVar = groupTypes[i].GetVariableBySuffix("LoadPath");
                    if (m_BuildPath.GetName(addressableAssetSettings) == groupTypes[i].GetName(buildPathVar) && m_LoadPath.GetName(addressableAssetSettings) == groupTypes[i].GetName(loadPathVar))
                    {
                        selected = i;
                        break;
                    }
                }
            }

            return selected;
        }

        internal void SetPathPairOption(SerializedObject so, List<string> options, List<ProfileGroupType> groupTypes, int newIndex)
        {
            SelectedPathPairIndex = newIndex;

            if (options[newIndex] != AddressableAssetProfileSettings.customEntryString)
            {
                Undo.RecordObject(so.targetObject, so.targetObject.name + "Path Pair");
                m_BuildPath.SetVariableByName(settings, groupTypes[newIndex].GroupTypePrefix + ProfileGroupType.k_PrefixSeparator + "BuildPath");
                m_LoadPath.SetVariableByName(settings, groupTypes[newIndex].GroupTypePrefix + ProfileGroupType.k_PrefixSeparator + "LoadPath");
                m_UseCustomPaths = false;
            }
            else
            {
                Undo.RecordObject(so.targetObject, so.targetObject.name + "Path Pair");
                m_UseCustomPaths = true;
            }
        }

        void ShowPathsPreview(bool showMixedValue)
        {
            EditorGUI.indentLevel++;
            m_ShowPaths = EditorGUILayout.Foldout(m_ShowPaths, m_PathsPreviewGUIContent, true);
            if (m_ShowPaths)
            {
                EditorStyles.helpBox.fontSize = 12;
                var buildPathValue = m_BuildPath.GetValue(settings);
                var loadPathValue = m_LoadPath.GetValue(settings);
                EditorGUILayout.HelpBox(String.Format("Build Path: {0}", showMixedValue ? "-" : buildPathValue),
                    MessageType.None);
                EditorGUILayout.HelpBox(String.Format("Load Path: {0}", showMixedValue ? "-" : loadPathValue), MessageType.None);
            }

            EditorGUI.indentLevel--;
        }

        void AddQueuedChanges(ref List<Action<BundledAssetGroupSchema, BundledAssetGroupSchema>> queuedChanges, Action<BundledAssetGroupSchema, BundledAssetGroupSchema> a)
        {
            if (queuedChanges == null)
                queuedChanges = new List<Action<BundledAssetGroupSchema, BundledAssetGroupSchema>>();
            queuedChanges.Add(a);
        }

        void ShowSelectedPropertyDefaultSettingsMulti(SerializedObject so, List<BundledAssetGroupSchema> otherBundledSchemas, ref List<Action<BundledAssetGroupSchema, BundledAssetGroupSchema>> queuedChanges)
        {
            bool selectedSchemaIsUsingCustomPaths = m_UseCustomPaths;
            bool selectedSchemaIsUsingDefaultSettings = UseDefaultSchemaSettings && !m_UseCustomPaths;
            foreach (BundledAssetGroupSchema otherSchema in otherBundledSchemas)
            {
                selectedSchemaIsUsingCustomPaths |= otherSchema.m_UseCustomPaths;
                selectedSchemaIsUsingDefaultSettings |= otherSchema.UseDefaultSchemaSettings && !otherSchema.m_UseCustomPaths;
            }

            if (!selectedSchemaIsUsingCustomPaths)
            {
                ShowMixedValue(this, otherBundledSchemas, (a, b) => a.UseDefaultSchemaSettings != b.UseDefaultSchemaSettings);
                EditorGUI.BeginChangeCheck();
                bool useDefaultSettings = EditorGUILayout.Toggle(m_UseDefaultSettingsContent, UseDefaultSchemaSettings);
                if (EditorGUI.EndChangeCheck())
                    AddQueuedChanges(ref queuedChanges, (src, dst) => src.UseDefaultSchemaSettings = dst.UseDefaultSchemaSettings = useDefaultSettings);
                EditorGUI.showMixedValue = false;
            }

            using (new EditorGUI.DisabledScope(selectedSchemaIsUsingDefaultSettings))
            {
                ShowMixedValue(this, otherBundledSchemas, (a, b) => a.Compression != b.Compression);
                EditorGUI.BeginChangeCheck();
                var compression = (BundleCompressionMode)EditorGUILayout.EnumPopup(m_CompressionContent, Compression);
                if (EditorGUI.EndChangeCheck())
                    AddQueuedChanges(ref queuedChanges, (src, dst) => src.Compression = dst.Compression = compression);
                EditorGUI.showMixedValue = false;

                bool buildTargetSupportsBundleCaching = BuildTargetSupportsBundleCaching(EditorUserBuildSettings.activeBuildTarget);
                if (buildTargetSupportsBundleCaching)
                {
                    ShowMixedValue(this, otherBundledSchemas, (a, b) => a.UseAssetBundleCache != b.UseAssetBundleCache);
                    EditorGUI.BeginChangeCheck();
                    bool useAssetBundleCache = EditorGUILayout.Toggle(m_UseAssetBundleCacheContent, UseAssetBundleCache);
                    if (EditorGUI.EndChangeCheck())
                        AddQueuedChanges(ref queuedChanges, (src, dst) => src.UseAssetBundleCache = dst.UseAssetBundleCache = useAssetBundleCache);
                    EditorGUI.showMixedValue = false;

                    if (UseAssetBundleCache)
                    {
                        ShowMixedValue(this, otherBundledSchemas, (a, b) => a.AssetBundledCacheClearBehavior != b.AssetBundledCacheClearBehavior);
                        EditorGUI.BeginChangeCheck();
                        var cacheClearBehavior = (CacheClearBehavior)EditorGUILayout.EnumPopup(m_CacheClearBehaviorContent, AssetBundledCacheClearBehavior);
                        if (EditorGUI.EndChangeCheck())
                            AddQueuedChanges(ref queuedChanges, (src, dst) => src.AssetBundledCacheClearBehavior = dst.AssetBundledCacheClearBehavior = cacheClearBehavior);
                        EditorGUI.showMixedValue = false;
                    }
                }
                CRCPropertyPopupFieldMulti(so, buildTargetSupportsBundleCaching, otherBundledSchemas, ref queuedChanges);

                ShowMixedValue(this, otherBundledSchemas, (a, b) => a.BundleNaming != b.BundleNaming);
                EditorGUI.BeginChangeCheck();
                var bundleNaming = (BundleNamingStyle)EditorGUILayout.EnumPopup(m_BundleModeContent, BundleNaming);
                if (EditorGUI.EndChangeCheck())
                    AddQueuedChanges(ref queuedChanges, (src, dst) => src.BundleNaming = dst.BundleNaming = bundleNaming);
                EditorGUI.showMixedValue = false;


                ShowMixedValue(this, otherBundledSchemas, (a, b) => a.StripDownloadOptions != b.StripDownloadOptions);
                EditorGUI.BeginChangeCheck();
                bool stripDLOptions = EditorGUILayout.Toggle(m_StripDownloadOptionsContent, StripDownloadOptions);
                if (EditorGUI.EndChangeCheck())
                    AddQueuedChanges(ref queuedChanges, (src, dst) => src.StripDownloadOptions = dst.StripDownloadOptions = stripDLOptions);
                EditorGUI.showMixedValue = false;

            }
        }

        /// <summary>
        /// A group of build target platforms that share the recommended schema settings.
        /// </summary>
        /// <remarks>
        /// For example, builds that target 32-bit and 64-bit Windows are under the same group and have the same recommended schema settings.
        /// </remarks>
        /// <example>
        /// <code source="../../../Tests/Editor/DocExampleCode/ScriptReference/UsingDefaultSchemaSettingsBuildTargetGroup.cs" region="SAMPLE"/>
        /// </example>
        public enum DefaultSchemaSettingsBuildTargetGroup
        {
            /// <summary>
            /// The default schema settings build target group.
            /// </summary>
            Default = 0,
            /// <summary>
            /// The Standalone schema settings build target group.
            /// </summary>
            StandaloneWindows = 1,
            /// <summary>
            /// The iOS schema settings build target group.
            /// </summary>
            iOS = BuildTargetGroup.iOS,
            /// <summary>
            /// The Android schema settings build target group.
            /// </summary>
            Android = BuildTargetGroup.Android,
            /// <summary>
            /// The WebGL schema settings build target group.
            /// </summary>
            WebGL = BuildTargetGroup.WebGL
        };

        private struct SchemaSettingsPair
        {
            public DefaultSchemaSettings Local;
            public DefaultSchemaSettings Remote;
        }

        /// <summary>
        /// A set of recommmended schema settings.
        /// </summary>
        public struct DefaultSchemaSettings
        {
            /// <summary>
            /// The recommended setting for AssetBundle compression.
            /// </summary>
            public BundleCompressionMode compression;
            /// <summary>
            /// The recommended setting for AssetBundle cache usage.
            /// </summary>
            public bool useAssetBundleCache;
            /// <summary>
            /// The recommended setting for AssetBundle cache clearing.
            /// </summary>
            public CacheClearBehavior assetBundledCacheClearBehavior;
            /// <summary>
            /// The recommended setting for AssetBundle crc usage.
            /// </summary>
            public bool useAssetBundleCrc;
            /// <summary>
            /// The recommended setting for AssetBundle crc usage regarding cached bundles.
            /// </summary>
            public bool useAssetBundleCrcForCachedBundles;
            /// <summary>
            /// The recommended naming style for AssetBundle file name.
            /// </summary>
            public BundleNamingStyle bundleNaming;
            /// <summary>
            /// The recommended setting for stripping additional download metadata.
            /// </summary>
            public bool stripDownloadOptions;
        }

        internal Dictionary<DefaultSchemaSettingsBuildTargetGroup, DefaultSchemaSettings[]> m_DefaultSettings;

        /// <summary>
        /// Create sets of recommended settings based on the build target platform and AssetBundle loading strategy.
        /// </summary>
        /// <returns>Sets of recommended settings.</returns>
        public Dictionary<DefaultSchemaSettingsBuildTargetGroup, DefaultSchemaSettings[]> CreateDefaultSchemaSettings()
        {
            var defaultSettings = new Dictionary<DefaultSchemaSettingsBuildTargetGroup, DefaultSchemaSettings[]>();

            // Default
            {
                DefaultSchemaSettings defaultLocalSettings = default;
                DefaultSchemaSettings defaultRemoteSettings = default;
#if UNITY_SWITCH
                defaultLocalSettings.compression = BundleCompressionMode.Uncompressed;
                defaultLocalSettings.useAssetBundleCache = false; // bundle caching not supported
                defaultLocalSettings.assetBundledCacheClearBehavior = CacheClearBehavior.ClearWhenSpaceIsNeededInCache;
                defaultLocalSettings.useAssetBundleCrc = false;
                defaultLocalSettings.useAssetBundleCrcForCachedBundles = false;
                defaultLocalSettings.bundleNaming = BundleNamingStyle.NoHash;
                defaultLocalSettings.stripDownloadOptions = true;

                defaultRemoteSettings.compression = BundleCompressionMode.Uncompressed;
                defaultRemoteSettings.useAssetBundleCache = false; // bundle caching not supported
                defaultRemoteSettings.assetBundledCacheClearBehavior = CacheClearBehavior.ClearWhenSpaceIsNeededInCache;
                defaultRemoteSettings.useAssetBundleCrc = true;
                defaultRemoteSettings.useAssetBundleCrcForCachedBundles = false;
                defaultRemoteSettings.bundleNaming = BundleNamingStyle.NoHash;
                defaultRemoteSettings.stripDownloadOptions = false;
#elif UNITY_PS4
                defaultLocalSettings.compression = BundleCompressionMode.Uncompressed;
                defaultLocalSettings.useAssetBundleCache = false; // bundle caching not supported
                defaultLocalSettings.assetBundledCacheClearBehavior = CacheClearBehavior.ClearWhenSpaceIsNeededInCache;
                defaultLocalSettings.useAssetBundleCrc = false;
                defaultLocalSettings.useAssetBundleCrcForCachedBundles = false;
                defaultLocalSettings.bundleNaming = BundleNamingStyle.NoHash;
                defaultLocalSettings.stripDownloadOptions = true;

                defaultRemoteSettings.compression = BundleCompressionMode.Uncompressed;
                defaultRemoteSettings.useAssetBundleCache = false; // bundle caching not supported
                defaultRemoteSettings.assetBundledCacheClearBehavior = CacheClearBehavior.ClearWhenSpaceIsNeededInCache;
                defaultRemoteSettings.useAssetBundleCrc = true;
                defaultRemoteSettings.useAssetBundleCrcForCachedBundles = false;
                defaultRemoteSettings.bundleNaming = BundleNamingStyle.NoHash;
                defaultRemoteSettings.stripDownloadOptions = false;
#elif UNITY_PS5
                defaultLocalSettings.compression = BundleCompressionMode.Uncompressed;
                defaultLocalSettings.useAssetBundleCache = false;
                defaultLocalSettings.assetBundledCacheClearBehavior = CacheClearBehavior.ClearWhenSpaceIsNeededInCache;
                defaultLocalSettings.useAssetBundleCrc = false;
                defaultLocalSettings.useAssetBundleCrcForCachedBundles = false;
                defaultLocalSettings.bundleNaming = BundleNamingStyle.NoHash;
                defaultLocalSettings.stripDownloadOptions = true;

                defaultRemoteSettings.compression = BundleCompressionMode.Uncompressed;
                defaultRemoteSettings.useAssetBundleCache = true;
                defaultRemoteSettings.assetBundledCacheClearBehavior = CacheClearBehavior.ClearWhenSpaceIsNeededInCache;
                defaultRemoteSettings.useAssetBundleCrc = true;
                defaultRemoteSettings.useAssetBundleCrcForCachedBundles = false;
                defaultRemoteSettings.bundleNaming = BundleNamingStyle.NoHash;
                defaultRemoteSettings.stripDownloadOptions = false;
#elif UNITY_GAMECORE || UNITY_GAMECORE_XBOXONE || UNITY_GAMECORE_XBOXSERIES || UNITY_XBOXONE
                defaultLocalSettings.compression = BundleCompressionMode.LZ4;
                defaultLocalSettings.useAssetBundleCache = false;
                defaultLocalSettings.assetBundledCacheClearBehavior = CacheClearBehavior.ClearWhenSpaceIsNeededInCache;
                defaultLocalSettings.useAssetBundleCrc = false;
                defaultLocalSettings.useAssetBundleCrcForCachedBundles = false;
                defaultLocalSettings.bundleNaming = BundleNamingStyle.NoHash;
                defaultLocalSettings.stripDownloadOptions = true;

                defaultRemoteSettings.compression = BundleCompressionMode.LZMA;
                defaultRemoteSettings.useAssetBundleCache = true;
                defaultRemoteSettings.assetBundledCacheClearBehavior = CacheClearBehavior.ClearWhenSpaceIsNeededInCache;
                defaultRemoteSettings.useAssetBundleCrc = true;
                defaultRemoteSettings.useAssetBundleCrcForCachedBundles = false;
                defaultRemoteSettings.bundleNaming = BundleNamingStyle.NoHash;
                defaultRemoteSettings.stripDownloadOptions = false;
#else
                defaultLocalSettings.compression = BundleCompressionMode.LZ4;
                defaultLocalSettings.useAssetBundleCache = false;
                defaultLocalSettings.assetBundledCacheClearBehavior = CacheClearBehavior.ClearWhenSpaceIsNeededInCache;
                defaultLocalSettings.useAssetBundleCrc = false;
                defaultLocalSettings.useAssetBundleCrcForCachedBundles = false;
                defaultLocalSettings.bundleNaming = BundleNamingStyle.AppendHash;
                defaultLocalSettings.stripDownloadOptions = true;

                defaultRemoteSettings.compression = BundleCompressionMode.LZMA;
                defaultRemoteSettings.useAssetBundleCache = true;
                defaultRemoteSettings.assetBundledCacheClearBehavior = CacheClearBehavior.ClearWhenSpaceIsNeededInCache;
                defaultRemoteSettings.useAssetBundleCrc = true;
                defaultRemoteSettings.useAssetBundleCrcForCachedBundles = false;
                defaultRemoteSettings.stripDownloadOptions = false;
#endif
                defaultSettings[DefaultSchemaSettingsBuildTargetGroup.Default] = new DefaultSchemaSettings[2] { defaultLocalSettings, defaultRemoteSettings };
            }

            // StandaloneWindows
            {
                DefaultSchemaSettings windowsLocalSettings;
                windowsLocalSettings.compression = BundleCompressionMode.LZ4;
                windowsLocalSettings.useAssetBundleCache = false;
                windowsLocalSettings.assetBundledCacheClearBehavior = CacheClearBehavior.ClearWhenSpaceIsNeededInCache;
                windowsLocalSettings.useAssetBundleCrc = false;
                windowsLocalSettings.useAssetBundleCrcForCachedBundles = false;
                windowsLocalSettings.bundleNaming = BundleNamingStyle.OnlyHash; // help avoid max path limit
                windowsLocalSettings.stripDownloadOptions = true;

                DefaultSchemaSettings windowsRemoteSettings;
                windowsRemoteSettings.compression = BundleCompressionMode.LZMA;
                windowsRemoteSettings.useAssetBundleCache = true;
                windowsRemoteSettings.assetBundledCacheClearBehavior = CacheClearBehavior.ClearWhenSpaceIsNeededInCache;
                windowsRemoteSettings.useAssetBundleCrc = true;
                windowsRemoteSettings.useAssetBundleCrcForCachedBundles = false;
                windowsRemoteSettings.bundleNaming = BundleNamingStyle.OnlyHash; // help avoid max path limit
                windowsRemoteSettings.stripDownloadOptions = false;

                defaultSettings[DefaultSchemaSettingsBuildTargetGroup.StandaloneWindows] = new DefaultSchemaSettings[2] { windowsLocalSettings, windowsRemoteSettings };
            }

            // iOS
            {
                DefaultSchemaSettings iOSLocalSettings;
                iOSLocalSettings.compression = BundleCompressionMode.LZ4;
                iOSLocalSettings.useAssetBundleCache = false;
                iOSLocalSettings.assetBundledCacheClearBehavior = CacheClearBehavior.ClearWhenSpaceIsNeededInCache;
                iOSLocalSettings.useAssetBundleCrc = false;
                iOSLocalSettings.useAssetBundleCrcForCachedBundles = false;
                iOSLocalSettings.bundleNaming = BundleNamingStyle.AppendHash;
                iOSLocalSettings.stripDownloadOptions = true;

                DefaultSchemaSettings iOSRemoteSettings;
                iOSRemoteSettings.compression = BundleCompressionMode.LZMA;
                iOSRemoteSettings.useAssetBundleCache = true;
                iOSRemoteSettings.assetBundledCacheClearBehavior = CacheClearBehavior.ClearWhenWhenNewVersionLoaded; // frequent content updates
                iOSRemoteSettings.useAssetBundleCrc = true;
                iOSRemoteSettings.useAssetBundleCrcForCachedBundles = false;
                iOSRemoteSettings.bundleNaming = BundleNamingStyle.AppendHash;
                iOSRemoteSettings.stripDownloadOptions = false;

                defaultSettings[DefaultSchemaSettingsBuildTargetGroup.iOS] = new DefaultSchemaSettings[2] { iOSLocalSettings, iOSRemoteSettings };
            }

            // Android
            {
                DefaultSchemaSettings androidLocalSettings;
                androidLocalSettings.compression = BundleCompressionMode.LZ4;
                androidLocalSettings.useAssetBundleCache = false;
                androidLocalSettings.assetBundledCacheClearBehavior = CacheClearBehavior.ClearWhenSpaceIsNeededInCache;
                androidLocalSettings.useAssetBundleCrc = false;
                androidLocalSettings.useAssetBundleCrcForCachedBundles = false;
                androidLocalSettings.bundleNaming = BundleNamingStyle.AppendHash;
                androidLocalSettings.stripDownloadOptions = false;

                DefaultSchemaSettings androidRemoteSettings;
                androidRemoteSettings.compression = BundleCompressionMode.LZMA;
                androidRemoteSettings.useAssetBundleCache = true;
                androidRemoteSettings.assetBundledCacheClearBehavior = CacheClearBehavior.ClearWhenWhenNewVersionLoaded; // frequent content updates
                androidRemoteSettings.useAssetBundleCrc = true;
                androidRemoteSettings.useAssetBundleCrcForCachedBundles = false;
                androidRemoteSettings.bundleNaming = BundleNamingStyle.AppendHash;
                androidRemoteSettings.stripDownloadOptions = false;

                defaultSettings[DefaultSchemaSettingsBuildTargetGroup.Android] = new DefaultSchemaSettings[2] { androidLocalSettings, androidRemoteSettings };
            }

            // WebGL
            {
                DefaultSchemaSettings webGLSettings;
                webGLSettings.compression = BundleCompressionMode.LZMA; // can only load bundles by web requests
#if UNITY_2022_1_OR_NEWER
                webGLSettings.useAssetBundleCache = false; // no bundle caching for this platform
#else
                webGLSettings.useAssetBundleCache = true;
#endif
                webGLSettings.assetBundledCacheClearBehavior = CacheClearBehavior.ClearWhenSpaceIsNeededInCache;
                webGLSettings.useAssetBundleCrc = true;
                webGLSettings.useAssetBundleCrcForCachedBundles = false;
                webGLSettings.bundleNaming = BundleNamingStyle.AppendHash;
                webGLSettings.stripDownloadOptions = false;

                defaultSettings[DefaultSchemaSettingsBuildTargetGroup.WebGL] = new DefaultSchemaSettings[2] { webGLSettings, webGLSettings };
            }

            return defaultSettings;
        }

        /// <summary>
        /// Returns the corresponding DefaultSchemaSettingsBuildTargetGroup for the build target specified.
        /// </summary>
        /// <param name="buildTarget">The build target.</param>
        /// <returns>The corresponding DefaultSchemaSettingsBuildTargetGroup.</returns>
        public DefaultSchemaSettingsBuildTargetGroup GetDefaultSchemaSettingsBuildTargetGroup(BuildTarget buildTarget)
        {
            if (buildTarget == BuildTarget.StandaloneWindows || buildTarget == BuildTarget.StandaloneWindows64)
                return DefaultSchemaSettingsBuildTargetGroup.StandaloneWindows;

            BuildTargetGroup buildTargetGroup = BuildPipeline.GetBuildTargetGroup(buildTarget);
            switch (buildTargetGroup)
            {
                case BuildTargetGroup.iOS:
                    return DefaultSchemaSettingsBuildTargetGroup.iOS;
                case BuildTargetGroup.Android:
                    return DefaultSchemaSettingsBuildTargetGroup.Android;
                case BuildTargetGroup.WebGL:
                    return DefaultSchemaSettingsBuildTargetGroup.WebGL;
                default:
                    return DefaultSchemaSettingsBuildTargetGroup.Default;
            }
        }

        internal bool BuildTargetSupportsBundleCaching(BuildTarget buildTarget)
        {
#if UNITY_2022_1_OR_NEWER
            return buildTarget != BuildTarget.WebGL &&
                buildTarget != BuildTarget.PS4 &&
                buildTarget != BuildTarget.Switch;
#else
            return buildTarget != BuildTarget.PS4 &&
                buildTarget != BuildTarget.Switch;
#endif
        }

        internal enum DefaultSettingsTarget
        {
            Local,
            Remote
        }

        /// <summary>
        /// Get the default settings for an Addressable Group schema
        /// </summary>
        /// <returns>A set of recommended schema settings</returns>
        public DefaultSchemaSettings GetDefaultSchemaSettings()
        {
            if (m_UseCustomPaths)
                return default;

            if (!HasDefaultSchemaSettings())
                return default;

            if (m_DefaultSettings == null)
                m_DefaultSettings = CreateDefaultSchemaSettings();

            DefaultSchemaSettingsBuildTargetGroup targetGroup = GetDefaultSchemaSettingsBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget);
            if (Group != null && Group.Settings != null)
            {
                var loadPathName = m_LoadPath.GetName(Group.Settings);
                if (loadPathName.Equals(AddressableAssetSettings.kRemoteLoadPath))
                {
                    return m_DefaultSettings[targetGroup][(int) DefaultSettingsTarget.Remote];
                }

                if (loadPathName.Equals(AddressableAssetSettings.kLocalLoadPath))
                {
                    return m_DefaultSettings[targetGroup][(int)DefaultSettingsTarget.Local];
                }
            }

            Debug.LogError("Could not determine default settings for schema as it does not have a group attached. " +
                           "This may be due to the schema being initialized manually without setting its group.");
            return default;
        }

        internal bool HasDefaultSchemaSettings()
        {
            DefaultSchemaSettingsBuildTargetGroup targetGroup = GetDefaultSchemaSettingsBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget);
            if (Group != null && Group.Settings != null)
            {
                var loadPathName = m_LoadPath.GetName(Group.Settings);
                return loadPathName.Equals(AddressableAssetSettings.kRemoteLoadPath) ||
                       loadPathName.Equals(AddressableAssetSettings.kLocalLoadPath);
            }
            return false;
        }
    }
}
