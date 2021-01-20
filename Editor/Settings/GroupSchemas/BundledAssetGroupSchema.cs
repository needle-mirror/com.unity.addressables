using System;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEditor.AddressableAssets.HostingServices;
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
    public class BundledAssetGroupSchema : AddressableAssetGroupSchema, IHostingServiceConfigurationProvider, ISerializationCallbackReceiver
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
        BundleCompressionMode m_Compression = BundleCompressionMode.LZ4;
        /// <summary>
        /// Build compression.
        /// </summary>
        public BundleCompressionMode Compression
        {
            get => m_Compression;
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
        internal enum AssetNamingMode
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
        internal bool IncludeAddressInCatalog
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
        internal bool IncludeGUIDInCatalog
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
        internal bool IncludeLabelsInCatalog
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
        internal AssetNamingMode InternalIdNamingMode
        {
            get { return m_InternalIdNamingMode; }
            set { m_InternalIdNamingMode = value; SetDirty(true); }
        }

        [SerializeField]
        [Tooltip("Indicates how the internal asset name will be generated.")]
        AssetNamingMode m_InternalIdNamingMode = AssetNamingMode.FullPath;


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
        public SerializedType BundledAssetProviderType { get { return m_BundledAssetProviderType; } }

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
            get => m_UseAssetBundleCache; 
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
            get => m_UseAssetBundleCrc; 
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
            get => m_UseAssetBundleCrcForCachedBundles;
            set
            {
                if (m_UseAssetBundleCrcForCachedBundles != value)
                {
                    m_UseAssetBundleCrcForCachedBundles = value;
                    SetDirty(true);
                }
            }
        }
        [FormerlySerializedAs("m_timeout")]
        [SerializeField]
        [Tooltip("Sets UnityWebRequest to attempt to abort after the number of seconds in timeout have passed. (Only applies to remote asset bundles)")]
        int m_Timeout;
        /// <summary>
        /// Sets UnityWebRequest to attempt to abort after the number of seconds in timeout have passed.
        /// </summary>
        public int Timeout
        {
            get => m_Timeout;
            set
            {
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
        int m_RedirectLimit = -1;
        /// <summary>
        /// Indicates the number of redirects which this UnityWebRequest will follow before halting with a “Redirect Limit Exceeded” system error.
        /// </summary>
        public int RedirectLimit
        {
            get => m_RedirectLimit;
            set
            {
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
        int m_RetryCount;
        /// <summary>
        /// Indicates the number of times the request will be retried.
        /// </summary>
        public int RetryCount
        {
            get => m_RetryCount;
            set
            {
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
        [Tooltip("Controls how bundles are packed.  If set to PackTogether, a single asset bundle will be created for the entire group, with the exception of scenes, which are packed in a second bundle.  If set to PackSeparately, an asset bundle will be created for each entry in the group; in the case that an entry is a folder, one bundle is created for the folder and all of its sub entries.")]
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

        /// <inheritdoc/>
        public string HostingServicesContentRoot
        {
            get
            {
                return BuildPath.GetValue(Group.Settings);
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
        public SerializedType AssetBundleProviderType { get { return m_AssetBundleProviderType; } }

        /// <summary>
        /// Set default values taken from the assigned group.
        /// </summary>
        /// <param name="group">The group this schema has been added to.</param>
        protected override void OnSetGroup(AddressableAssetGroup group)
        {
            //this can happen during the load of the addressables asset
            if (group.Settings != null)
            {
                if (BuildPath == null || string.IsNullOrEmpty(BuildPath.GetValue(group.Settings)))
                {
                    m_BuildPath = new ProfileValueReference();
                    BuildPath.SetVariableByName(group.Settings, AddressableAssetSettings.kLocalBuildPath);
                }

                if (LoadPath == null || string.IsNullOrEmpty(LoadPath.GetValue(group.Settings)))
                {
                    m_LoadPath = new ProfileValueReference();
                    LoadPath.SetVariableByName(group.Settings, AddressableAssetSettings.kLocalLoadPath);
                }
            }

            if (m_AssetBundleProviderType.Value == null)
                m_AssetBundleProviderType.Value = typeof(AssetBundleProvider);
            if (m_BundledAssetProviderType.Value == null)
                m_BundledAssetProviderType.Value = typeof(BundledAssetProvider);
        }

        internal string GetAssetLoadPath(string assetPath, HashSet<string> otherLoadPaths, Func<string, string> pathToGUIDFunc)
        {
            switch (InternalIdNamingMode)
            {
                case AssetNamingMode.FullPath: return assetPath;
                case AssetNamingMode.Filename: return System.IO.Path.GetFileName(assetPath);
                case AssetNamingMode.GUID: return pathToGUIDFunc(assetPath);
                case AssetNamingMode.Dynamic:
                    {
                        var g = pathToGUIDFunc(assetPath);
                        if (otherLoadPaths == null)
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
                int newValue = EditorGUI.Popup(position, new GUIContent(label.text, "Controls how the output AssetBundle's will be named."), enumValue, contents);
                if (EditorGUI.EndChangeCheck())
                {
                    newValue = newValue == 0 ? 1 : newValue == 1 ? 0 : newValue;
                    property.enumValueIndex = newValue;
                }

                EditorGUI.EndProperty();
            }
        }

        [SerializeField]
        BundleNamingStyle m_BundleNaming;
        /// <summary>
        /// Naming style to use for generated AssetBundle(s).
        /// </summary>
        public BundleNamingStyle BundleNaming
        {
            get { return m_BundleNaming; }
            set
            {
                m_BundleNaming = value;
                SetDirty(true);
            }
        }

        private bool m_ShowPaths = true;
        private bool m_ShowAdvanced = false;

        /// <summary>
        /// Used for drawing properties in the inspector.
        /// </summary>
        public override void ShowAllProperties()
        {
            m_ShowPaths = true;
            m_ShowAdvanced = true;
        }

        GUIContent m_AssetProviderContent = new GUIContent("Asset Provider", "The provider to use for loading assets out of AssetBundles");
        GUIContent m_BundleProviderContent = new GUIContent("Asset Bundle Provider", "The provider to use for loading AssetBundles (not the assets within bundles)");

        /// <inheritdoc/>
        public override void OnGUI()
        {
            var so = new SerializedObject(this);

            m_ShowPaths = EditorGUILayout.Foldout(m_ShowPaths, "Build and Load Paths");
            if (m_ShowPaths)
            {
                ShowPaths(so);
            }

            m_ShowAdvanced = EditorGUILayout.Foldout(m_ShowAdvanced, "Advanced Options");
            if (m_ShowAdvanced)
            {
                ShowAdvancedProperties(so);
            }
            so.ApplyModifiedProperties();
        }

        /// <inheritdoc/>
        public override void OnGUIMultiple(List<AddressableAssetGroupSchema> otherSchemas)
        {
            List<Action<BundledAssetGroupSchema, BundledAssetGroupSchema>> queuedChanges = null;
            var so = new SerializedObject(this);

            List<BundledAssetGroupSchema> otherBundledSchemas = new List<BundledAssetGroupSchema>();
            foreach (var schema in otherSchemas)
            {
                otherBundledSchemas.Add(schema as BundledAssetGroupSchema);
            }

            EditorGUI.BeginChangeCheck();
            m_ShowPaths = EditorGUILayout.Foldout(m_ShowPaths, "Build and Load Paths");
            if (EditorGUI.EndChangeCheck())
            {
                foreach (var schema in otherBundledSchemas)
                    schema.m_ShowPaths = m_ShowPaths;
            }
            if (m_ShowPaths)
            {
                ShowPathsMulti(so, otherSchemas, ref queuedChanges);
            }

            EditorGUI.BeginChangeCheck();
            m_ShowAdvanced = EditorGUILayout.Foldout(m_ShowAdvanced, "Advanced Options");
            if (EditorGUI.EndChangeCheck())
            {
                foreach (var schema in otherBundledSchemas)
                    schema.m_ShowAdvanced = m_ShowAdvanced;
            }
            if (m_ShowAdvanced)
            {
                ShowAdvancedPropertiesMulti(so, otherSchemas, ref queuedChanges);
            }

            so.ApplyModifiedProperties();
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
            ShowSelectedPropertyMulti(so, nameof(m_BuildPath), null, otherBundledSchemas, ref queuedChanges, (src, dst) => { dst.m_BuildPath.Id = src.BuildPath.Id; dst.SetDirty(true); }, m_BuildPath.Id, ref m_BuildPath);
            ShowSelectedPropertyMulti(so, nameof(m_LoadPath), null, otherBundledSchemas, ref queuedChanges, (src, dst) => { dst.m_LoadPath.Id = src.LoadPath.Id; dst.SetDirty(true); }, m_LoadPath.Id, ref m_LoadPath);
        }

        void ShowAdvancedProperties(SerializedObject so)
        {
            EditorGUILayout.PropertyField(so.FindProperty("m_Compression"), true);
            EditorGUILayout.PropertyField(so.FindProperty("m_IncludeInBuild"), true);
            EditorGUILayout.PropertyField(so.FindProperty("m_ForceUniqueProvider"), true);
            EditorGUILayout.PropertyField(so.FindProperty("m_UseAssetBundleCache"), true);
            EditorGUILayout.PropertyField(so.FindProperty("m_UseAssetBundleCrc"), true);
            EditorGUILayout.PropertyField(so.FindProperty("m_UseAssetBundleCrcForCachedBundles"), true);
            EditorGUILayout.PropertyField(so.FindProperty("m_Timeout"), true);
            EditorGUILayout.PropertyField(so.FindProperty("m_ChunkedTransfer"), true);
            EditorGUILayout.PropertyField(so.FindProperty("m_RedirectLimit"), true);
            EditorGUILayout.PropertyField(so.FindProperty("m_RetryCount"), true);
            EditorGUILayout.PropertyField(so.FindProperty("m_BundleMode"), true);
            EditorGUILayout.PropertyField(so.FindProperty("m_BundleNaming"), true);
            EditorGUILayout.PropertyField(so.FindProperty("m_BundledAssetProviderType"), m_AssetProviderContent, true);
            EditorGUILayout.PropertyField(so.FindProperty("m_AssetBundleProviderType"), m_BundleProviderContent, true);
        }

        void ShowAdvancedPropertiesMulti(SerializedObject so, List<AddressableAssetGroupSchema> otherBundledSchemas, ref List<Action<BundledAssetGroupSchema, BundledAssetGroupSchema>> queuedChanges)
        {
            ShowSelectedPropertyMulti(so, nameof(m_Compression), null, otherBundledSchemas, ref queuedChanges, (src, dst) => dst.Compression = src.Compression, ref m_Compression);
            ShowSelectedPropertyMulti(so, nameof(m_IncludeInBuild), null, otherBundledSchemas, ref queuedChanges, (src, dst) => dst.IncludeInBuild = src.IncludeInBuild, ref m_IncludeInBuild);
            ShowSelectedPropertyMulti(so, nameof(m_ForceUniqueProvider), null, otherBundledSchemas, ref queuedChanges, (src, dst) => dst.ForceUniqueProvider = src.ForceUniqueProvider, ref m_ForceUniqueProvider);
            ShowSelectedPropertyMulti(so, nameof(m_UseAssetBundleCache), null, otherBundledSchemas, ref queuedChanges, (src, dst) => dst.UseAssetBundleCache = src.UseAssetBundleCache, ref m_UseAssetBundleCache);
            ShowSelectedPropertyMulti(so, nameof(m_UseAssetBundleCrc), null, otherBundledSchemas, ref queuedChanges, (src, dst) => dst.UseAssetBundleCrc = src.UseAssetBundleCrc, ref m_UseAssetBundleCrc);
            ShowSelectedPropertyMulti(so, nameof(m_UseAssetBundleCrcForCachedBundles), null, otherBundledSchemas, ref queuedChanges, (src, dst) => dst.UseAssetBundleCrcForCachedBundles = src.UseAssetBundleCrcForCachedBundles, ref m_UseAssetBundleCrcForCachedBundles);
            ShowSelectedPropertyMulti(so, nameof(m_Timeout), null, otherBundledSchemas, ref queuedChanges, (src, dst) => dst.Timeout = src.Timeout, ref m_Timeout);
            ShowSelectedPropertyMulti(so, nameof(m_ChunkedTransfer), null, otherBundledSchemas, ref queuedChanges, (src, dst) => dst.ChunkedTransfer = src.ChunkedTransfer, ref m_ChunkedTransfer);
            ShowSelectedPropertyMulti(so, nameof(m_RedirectLimit), null, otherBundledSchemas, ref queuedChanges, (src, dst) => dst.RedirectLimit = src.RedirectLimit, ref m_RedirectLimit);
            ShowSelectedPropertyMulti(so, nameof(m_RetryCount), null, otherBundledSchemas, ref queuedChanges, (src, dst) => dst.RetryCount = src.RetryCount, ref m_RetryCount);
            ShowSelectedPropertyMulti(so, nameof(m_IncludeAddressInCatalog), null, otherBundledSchemas, ref queuedChanges, (src, dst) => dst.IncludeAddressInCatalog = src.IncludeAddressInCatalog, ref m_IncludeAddressInCatalog);
            ShowSelectedPropertyMulti(so, nameof(m_IncludeGUIDInCatalog), null, otherBundledSchemas, ref queuedChanges, (src, dst) => dst.IncludeGUIDInCatalog = src.IncludeGUIDInCatalog, ref m_IncludeGUIDInCatalog);
            ShowSelectedPropertyMulti(so, nameof(m_IncludeLabelsInCatalog), null, otherBundledSchemas, ref queuedChanges, (src, dst) => dst.IncludeLabelsInCatalog = src.IncludeLabelsInCatalog, ref m_IncludeLabelsInCatalog);
            ShowSelectedPropertyMulti(so, nameof(m_BundleMode), null, otherBundledSchemas, ref queuedChanges, (src, dst) => dst.BundleMode = src.BundleMode, ref m_BundleMode);
            ShowSelectedPropertyMulti(so, nameof(m_InternalIdNamingMode), null, otherBundledSchemas, ref queuedChanges, (src, dst) => dst.InternalIdNamingMode = src.InternalIdNamingMode, ref m_InternalIdNamingMode);
            ShowSelectedPropertyMulti(so, nameof(m_BundleNaming), null, otherBundledSchemas, ref queuedChanges, (src, dst) => dst.BundleNaming = src.BundleNaming, ref m_BundleNaming);
            ShowSelectedPropertyMulti(so, nameof(m_BundledAssetProviderType), m_AssetProviderContent, otherBundledSchemas, ref queuedChanges, (src, dst) => { dst.m_BundledAssetProviderType = src.BundledAssetProviderType; dst.SetDirty(true); }, ref m_BundledAssetProviderType);
            ShowSelectedPropertyMulti(so, nameof(m_AssetBundleProviderType), m_BundleProviderContent, otherBundledSchemas, ref queuedChanges, (src, dst) => { dst.m_AssetBundleProviderType = src.AssetBundleProviderType; dst.SetDirty(true); }, ref m_AssetBundleProviderType);
        }

        void ShowSelectedPropertyMulti<T>(SerializedObject so, string propertyName, GUIContent label, List<AddressableAssetGroupSchema> otherSchemas, ref List<Action<BundledAssetGroupSchema, BundledAssetGroupSchema>> queuedChanges, Action<BundledAssetGroupSchema, BundledAssetGroupSchema> a, ref T propertyValue)
        {
            var prop = so.FindProperty(propertyName);
            ShowMixedValue(prop, otherSchemas, typeof(T), propertyName);

            T newValue = default(T);
            
            EditorGUI.BeginChangeCheck();
            if (typeof(T) == typeof(bool))
            {
                newValue = (T)(object)EditorGUILayout.Toggle(prop.displayName, (bool)(object)propertyValue);
            }
            else if (typeof(T).IsEnum)
            {
                newValue = (T)(object)(AssetNamingMode)EditorGUILayout.EnumPopup(prop.displayName, (Enum)(object)propertyValue);
            }
            else if (typeof(T) == typeof(int))
            {
                newValue = (T)(object)EditorGUILayout.IntField(prop.displayName, (int)(object)propertyValue);
            }
            else
            {
                EditorGUILayout.PropertyField(prop, label, true);
            }
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(so.targetObject, so.targetObject.name + propertyName);
                if (typeof(T) == typeof(bool) || typeof(T).IsEnum || typeof(T) == typeof(int))
                    propertyValue = newValue;
                if (queuedChanges == null)
                    queuedChanges = new List<Action<BundledAssetGroupSchema, BundledAssetGroupSchema>>();
                queuedChanges.Add(a);
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
    }
}
