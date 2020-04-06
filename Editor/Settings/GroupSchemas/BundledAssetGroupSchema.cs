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
        /// Compression mode for bundles in this group.
        /// </summary>
        public enum BundleCompressionMode
        {
            Uncompressed,
            LZ4,
            LZMA
        }

        /// <summary>
        /// Build compression.
        /// </summary>
        public BundleCompressionMode Compression
        {
            get { return m_Compression; }
            set { m_Compression = value; }
        }

        [SerializeField]
        BundleCompressionMode m_Compression = BundleCompressionMode.LZ4;

        /// <summary>
        /// Gets the build compression settings for bundles in this group.
        /// </summary>
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
            get { return m_IncludeInBuild; }
            set
            {
                m_IncludeInBuild = value;
                SetDirty(true);
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
            get { return m_ForceUniqueProvider; }
            set
            {
                m_ForceUniqueProvider = value;
                SetDirty(true);
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
            get { return m_UseAssetBundleCache; }
            set
            {
                m_UseAssetBundleCache = value;
                SetDirty(true);
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
            get { return m_UseAssetBundleCrc; }
            set
            {
                m_UseAssetBundleCrc = value;
                SetDirty(true);
            }
        }

        [FormerlySerializedAs("m_timeout")]
        [SerializeField]
        [Tooltip("Sets UnityWebRequest to attempt to abort after the number of seconds in timeout have passed. (Only applies to remote asset bundles)")]
        int m_Timeout;
        /// <summary>
        /// Sets UnityWebRequest to attempt to abort after the number of seconds in timeout have passed.
        /// </summary>
        public int Timeout { get { return m_Timeout; } set { m_Timeout = value; } }
        [FormerlySerializedAs("m_chunkedTransfer")]
        [SerializeField]
        [Tooltip("Deprecated in 2019.3+. Indicates whether the UnityWebRequest system should employ the HTTP/1.1 chunked-transfer encoding method. (Only applies to remote asset bundles)")]
        bool m_ChunkedTransfer;
        /// <summary>
        /// Indicates whether the UnityWebRequest system should employ the HTTP/1.1 chunked-transfer encoding method.
        /// </summary>
        public bool ChunkedTransfer { get { return m_ChunkedTransfer; } set { m_ChunkedTransfer = value; } }
        [FormerlySerializedAs("m_redirectLimit")]
        [SerializeField]
        [Tooltip("Indicates the number of redirects which this UnityWebRequest will follow before halting with a “Redirect Limit Exceeded” system error. (Only applies to remote asset bundles)")]
        int m_RedirectLimit = -1;
        /// <summary>
        /// Indicates the number of redirects which this UnityWebRequest will follow before halting with a “Redirect Limit Exceeded” system error.
        /// </summary>
        public int RedirectLimit { get { return m_RedirectLimit; } set { m_RedirectLimit = value; } }
        [FormerlySerializedAs("m_retryCount")]
        [SerializeField]
        [Tooltip("Indicates the number of times the request will be retried.")]
        int m_RetryCount;
        /// <summary>
        /// Indicates the number of times the request will be retried.  
        /// </summary>
        public int RetryCount { get { return m_RetryCount; } set { m_RetryCount = value; } }

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
            get { return m_BundleMode; }
            set
            {
                m_BundleMode = value;
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
            BuildPath.OnValueChanged += s=> SetDirty(true);
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
            AppendHash,
            NoHash,
            OnlyHash,
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
                if(EditorGUI.EndChangeCheck())
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
        GUIContent m_BundleProviderContent = new GUIContent("AssetBundle Provider", "The provider to use for loading AssetBundles (not the assets within bundles)");

        /// <inheritdoc/>
        public override void OnGUI()
        {
            var so = new SerializedObject(this);

            m_ShowPaths = EditorGUILayout.Foldout(m_ShowPaths, "Build and Load Paths");
            if (m_ShowPaths)
            {
                EditorGUILayout.PropertyField(so.FindProperty("m_BuildPath"), true);
                EditorGUILayout.PropertyField(so.FindProperty("m_LoadPath"), true);
            }

            m_ShowAdvanced = EditorGUILayout.Foldout(m_ShowAdvanced, "Advanced Options");
            if (m_ShowAdvanced)
            {
                EditorGUILayout.PropertyField(so.FindProperty("m_Compression"), true);
                EditorGUILayout.PropertyField(so.FindProperty("m_IncludeInBuild"), true);
                EditorGUILayout.PropertyField(so.FindProperty("m_ForceUniqueProvider"), true);
                EditorGUILayout.PropertyField(so.FindProperty("m_UseAssetBundleCache"), true);
                EditorGUILayout.PropertyField(so.FindProperty("m_UseAssetBundleCrc"), true);
                EditorGUILayout.PropertyField(so.FindProperty("m_Timeout"), true);
                EditorGUILayout.PropertyField(so.FindProperty("m_ChunkedTransfer"), true);
                EditorGUILayout.PropertyField(so.FindProperty("m_RedirectLimit"), true);
                EditorGUILayout.PropertyField(so.FindProperty("m_RetryCount"), true);
                EditorGUILayout.PropertyField(so.FindProperty("m_BundleMode"), true);
                EditorGUILayout.PropertyField(so.FindProperty("m_BundleNaming"), true);
                EditorGUILayout.PropertyField(so.FindProperty("m_BundledAssetProviderType"), m_AssetProviderContent, true);
                EditorGUILayout.PropertyField(so.FindProperty("m_AssetBundleProviderType"), m_BundleProviderContent, true);
            }

            so.ApplyModifiedProperties();
        }

        /// <inheritdoc/>
        public override void OnGUIMultiple(List<AddressableAssetGroupSchema> otherSchemas)
        {
            var so = new SerializedObject(this);
            SerializedProperty prop;

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
                {
                    schema.m_ShowPaths = m_ShowPaths;
                }
            }
            if (m_ShowPaths)
            {
                // BuildPath
                prop = so.FindProperty("m_BuildPath");
                ShowMixedValue(prop, otherSchemas, typeof(ProfileValueReference), "m_BuildPath");

                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(prop, true);
                if (EditorGUI.EndChangeCheck())
                {
                    foreach (var schema in otherBundledSchemas)
                        schema.m_BuildPath.Id = BuildPath.Id;
                }
                EditorGUI.showMixedValue = false;

                // LoadPath
                prop = so.FindProperty("m_LoadPath");
                ShowMixedValue(prop, otherSchemas, typeof(ProfileValueReference), "m_LoadPath");

                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(prop, true);
                if (EditorGUI.EndChangeCheck())
                {
                    foreach (var schema in otherBundledSchemas)
                        schema.m_LoadPath.Id = LoadPath.Id;
                }
                EditorGUI.showMixedValue = false;
            }

            EditorGUI.BeginChangeCheck();
            m_ShowAdvanced = EditorGUILayout.Foldout(m_ShowAdvanced, "Advanced Options");
            if (EditorGUI.EndChangeCheck())
            {
                foreach (var schema in otherBundledSchemas)
                {
                    schema.m_ShowAdvanced = m_ShowAdvanced;
                }
            }
            if (m_ShowAdvanced)
            {
                // Compression
                prop = so.FindProperty("m_Compression");
                ShowMixedValue(prop, otherSchemas, typeof(Enum), "m_Compression");
                EditorGUI.BeginChangeCheck();
                BundleCompressionMode newCompression = (BundleCompressionMode)EditorGUILayout.EnumPopup(prop.displayName, Compression);
                if (EditorGUI.EndChangeCheck())
                {
                    Compression = newCompression;
                    foreach (var schema in otherBundledSchemas)
                        schema.Compression = Compression;
                }
                EditorGUI.showMixedValue = false;

                // IncludeInBuild
                prop = so.FindProperty("m_IncludeInBuild");
                ShowMixedValue(prop, otherSchemas, typeof(bool), "m_IncludeInBuild");
                EditorGUI.BeginChangeCheck();
                bool newIncludeInBuild = (bool)EditorGUILayout.Toggle(prop.displayName, IncludeInBuild);
                if (EditorGUI.EndChangeCheck())
                {
                    IncludeInBuild = newIncludeInBuild;
                    foreach (var schema in otherBundledSchemas)
                        schema.IncludeInBuild = IncludeInBuild;
                }
                EditorGUI.showMixedValue = false;

                // ForceUniqueProvider
                prop = so.FindProperty("m_ForceUniqueProvider");
                ShowMixedValue(prop, otherSchemas, typeof(bool), "m_ForceUniqueProvider");
                EditorGUI.BeginChangeCheck();
                bool newForceUniqueProvider = (bool)EditorGUILayout.Toggle(prop.displayName, ForceUniqueProvider);
                if (EditorGUI.EndChangeCheck())
                {
                    ForceUniqueProvider = newForceUniqueProvider;
                    foreach (var schema in otherBundledSchemas)
                        schema.ForceUniqueProvider = ForceUniqueProvider;
                }
                EditorGUI.showMixedValue = false;

                // UseAssetBundleCache
                prop = so.FindProperty("m_UseAssetBundleCache");
                ShowMixedValue(prop, otherSchemas, typeof(bool), "m_UseAssetBundleCache");
                EditorGUI.BeginChangeCheck();
                bool newUseAssetBundleCache = (bool)EditorGUILayout.Toggle(prop.displayName, UseAssetBundleCache);
                if (EditorGUI.EndChangeCheck())
                {
                    UseAssetBundleCache = newUseAssetBundleCache;
                    foreach (var schema in otherBundledSchemas)
                        schema.UseAssetBundleCache = UseAssetBundleCache;
                }
                EditorGUI.showMixedValue = false;

                // UseAssetBundleCrc
                prop = so.FindProperty("m_UseAssetBundleCrc");
                ShowMixedValue(prop, otherSchemas, typeof(bool), "m_UseAssetBundleCrc");
                EditorGUI.BeginChangeCheck();
                bool newUseAssetBundleCrc = (bool)EditorGUILayout.Toggle(prop.displayName, UseAssetBundleCrc);
                if (EditorGUI.EndChangeCheck())
                {
                    UseAssetBundleCrc = newUseAssetBundleCrc;
                    foreach (var schema in otherBundledSchemas)
                        schema.UseAssetBundleCrc = UseAssetBundleCrc;
                }
                EditorGUI.showMixedValue = false;

                // Timeout
                prop = so.FindProperty("m_Timeout");
                ShowMixedValue(prop, otherSchemas, typeof(int), "m_Timeout");
                EditorGUI.BeginChangeCheck();
                int newTimeout = (int)EditorGUILayout.IntField(prop.displayName, Timeout);
                if (EditorGUI.EndChangeCheck())
                {
                    Timeout = newTimeout;
                    foreach (var schema in otherBundledSchemas)
                        schema.Timeout = Timeout;
                }
                EditorGUI.showMixedValue = false;

                // ChunkedTransfer
                prop = so.FindProperty("m_ChunkedTransfer");
                ShowMixedValue(prop, otherSchemas, typeof(bool), "m_ChunkedTransfer");
                EditorGUI.BeginChangeCheck();
                bool newChunkedTransfer = (bool)EditorGUILayout.Toggle(prop.displayName, ChunkedTransfer);
                if (EditorGUI.EndChangeCheck())
                {
                    ChunkedTransfer = newChunkedTransfer;
                    foreach (var schema in otherBundledSchemas)
                        schema.ChunkedTransfer = ChunkedTransfer;
                }
                EditorGUI.showMixedValue = false;

                // RedirectLimit
                prop = so.FindProperty("m_RedirectLimit");
                ShowMixedValue(prop, otherSchemas, typeof(int), "m_RedirectLimit");
                EditorGUI.BeginChangeCheck();
                int newRedirectLimit = (int)EditorGUILayout.IntField(prop.displayName, RedirectLimit);
                if (EditorGUI.EndChangeCheck())
                {
                    RedirectLimit = newRedirectLimit;
                    foreach (var schema in otherBundledSchemas)
                        schema.RedirectLimit = RedirectLimit;
                }
                EditorGUI.showMixedValue = false;

                // RetryCount
                prop = so.FindProperty("m_RetryCount");
                ShowMixedValue(prop, otherSchemas, typeof(int), "m_RetryCount");
                EditorGUI.BeginChangeCheck();
                int newRetryCount = (int)EditorGUILayout.IntField(prop.displayName, RetryCount);
                if (EditorGUI.EndChangeCheck())
                {
                    RetryCount = newRetryCount;
                    foreach (var schema in otherBundledSchemas)
                        schema.RetryCount = RetryCount;
                }
                EditorGUI.showMixedValue = false;

                // BundleMode
                prop = so.FindProperty("m_BundleMode");
                ShowMixedValue(prop, otherSchemas, typeof(Enum), "m_BundleMode");
                EditorGUI.BeginChangeCheck();
                BundlePackingMode newBundleMode = (BundlePackingMode)EditorGUILayout.EnumPopup(prop.displayName, BundleMode);
                if (EditorGUI.EndChangeCheck())
                {
                    BundleMode = newBundleMode;
                    foreach (var schema in otherBundledSchemas)
                        schema.BundleMode = BundleMode;
                }
                EditorGUI.showMixedValue = false;

                //Bundle Naming
                prop = so.FindProperty("m_BundleNaming");
                ShowMixedValue(prop, otherSchemas, typeof(Enum), "m_BundleNaming");
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(prop, true);
                if (EditorGUI.EndChangeCheck())
                {
                    BundleNamingStyle newNamingStyle = (BundleNamingStyle)prop.enumValueIndex;
                    BundleNaming = newNamingStyle;
                    foreach (var schema in otherBundledSchemas)
                        schema.BundleNaming = BundleNaming;
                }
                EditorGUI.showMixedValue = false;

                //Bundled Asset Provider Type
                prop = so.FindProperty("m_BundledAssetProviderType");
                ShowMixedValue(prop, otherSchemas, typeof(SerializedType), "m_BundledAssetProviderType");

                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(prop, m_AssetProviderContent, true);
                if (EditorGUI.EndChangeCheck())
                {
                    foreach (var schema in otherBundledSchemas)
                        schema.m_BundledAssetProviderType = BundledAssetProviderType;
                }
                EditorGUI.showMixedValue = false;

                //Asset Bundle Provider Type
                prop = so.FindProperty("m_AssetBundleProviderType");
                ShowMixedValue(prop, otherSchemas, typeof(SerializedType), "m_AssetBundleProviderType");

                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(prop, m_BundleProviderContent, true);
                if (EditorGUI.EndChangeCheck())
                {
                    foreach (var schema in otherBundledSchemas)
                        schema.m_AssetBundleProviderType = AssetBundleProviderType;
                }

                EditorGUI.showMixedValue = false;
            }

            so.ApplyModifiedProperties();
        }
    }
}