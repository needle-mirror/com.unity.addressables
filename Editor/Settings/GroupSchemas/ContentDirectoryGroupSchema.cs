using System;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;
using UnityEngine.Serialization;
using static UnityEditor.AddressableAssets.Settings.GroupSchemas.ContentDirectoryGroupSchema;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.ResourceManagement.Util;
using UnityEngine.AddressableAssets.Initialization;
using UnityEngine.AddressableAssets.ResourceProviders;
using UnityEditor.AddressableAssets.GUI;

namespace UnityEditor.AddressableAssets.Settings.GroupSchemas
{
    /// <summary>
    /// Schema for configuring groups to be built as Content Directories.
    /// Content Directories provide an alternative to AssetBundles for organizing and loading addressable content.
    /// </summary>
    [DisplayName("Content Directory")]
    [AddressablesHelpURL(AddressableAssetUtility.kContentDirectoriesDocsPage)]
    public class ContentDirectoryGroupSchema : AddressableAssetGroupSchema,
        ISerializationCallbackReceiver,
        IBuildableSchema,
        ICanIncludeFolderKeys,
        ICanIncludeLabels
    {
        // Retained (serialized) only so a project's previously-stored per-schema value can be migrated up to the
        // group on load. The IncludeInBuild property below no longer reads this field; it forwards to the group.
        [SerializeField]
        bool m_IncludeInBuild = true;

        internal override bool? GetDeprecatedIncludeInBuild() => m_IncludeInBuild;

        /// <summary>
        /// If true, the group's content will be included in the Addressables build.
        /// </summary>
        /// <remarks>
        /// Include in Build is stored on the owning <see cref="AddressableAssetGroup"/>. This property forwards to
        /// <see cref="AddressableAssetGroup.IncludeInBuild"/> so the group remains the single source of truth.
        /// </remarks>
        public bool IncludeInBuild
        {
            get => Group == null || Group.IncludeInBuild;
            set
            {
                if (Group != null)
                    Group.IncludeInBuild = value;
            }
        }

        [SerializeField]
        string m_CatalogName = ResourceManagerRuntimeData.kCatalogAddress;

        /// <summary>
        /// Gets or sets the catalog identifier for this content directory group.
        /// Groups with the same CatalogId will be built into the same catalog.
        /// </summary>
        public override string CatalogId { get => m_CatalogName; set => m_CatalogName = value; }

        private bool m_ShowPaths = true;

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

        [SerializeField]
        [Tooltip("The path to copy asset bundles to.")]
        internal ProfileValueReference m_BuildPath = new ProfileValueReference();

        /// <summary>
        /// The path to copy the built content directory to.
        /// </summary>
        public ProfileValueReference BuildPath
        {
            get { return m_BuildPath; }
        }

        [SerializeField]
        [Tooltip("The path to load bundles from.")]
        internal ProfileValueReference m_LoadPath = new ProfileValueReference();

        /// <summary>
        /// The path to load the content directory from at runtime.
        /// </summary>
        public ProfileValueReference LoadPath
        {
            get { return m_LoadPath; }
        }

        [SerializeField]
        [SerializedTypeRestriction(type = typeof(IResourceProvider))]
        [Tooltip("The provider type to use for loading entries from group root assets.")]
        SerializedType m_GroupAssetEntryProviderType;

        /// <summary>
        /// The provider type to use for loading entries from group root assets.
        /// </summary>
        public SerializedType GroupAssetEntryProviderType
        {
            get => m_GroupAssetEntryProviderType;
            set
            {
                m_GroupAssetEntryProviderType = value;
                SetDirty(true);
            }
        }

        [SerializeField]
        bool m_IncludeLabelsInCatalog = true;

        [SerializeField]
        bool m_IncludeFolderKeysInCatalog = true;

        [SerializeField]
        bool m_IncludeAddressesForFolderChildren = true;

        /// <summary>
        /// Gets or sets whether labels are included in the content catalog for this content
        /// directory group. This is required if labels are used at runtime to load assets.
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
        /// Gets or sets whether each addressable folder's own address is included as an extra
        /// shared key on every asset within that folder. This allows loading every asset in an
        /// addressable folder with a single call, for example
        /// Addressables.LoadAssetsAsync(folderAddress, ...), similar to Resources.LoadAll.
        /// </summary>
        public bool IncludeFolderKeysInCatalog
        {
            get => m_IncludeFolderKeysInCatalog;
            set
            {
                if (m_IncludeFolderKeysInCatalog != value)
                {
                    m_IncludeFolderKeysInCatalog = value;
                    SetDirty(true);
                }
            }
        }

        /// <summary>
        /// Gets or sets whether assets inside an addressable folder keep their own individual
        /// address in the catalog, in addition to the folder's shared key (see
        /// IncludeFolderKeysInCatalog). GUIDs are unaffected. Only takes effect when
        /// IncludeFolderKeysInCatalog is enabled. Disable to reduce catalog size when assets
        /// are always loaded via the folder key.
        /// </summary>
        public bool IncludeAddressesForFolderChildren
        {
            get => m_IncludeAddressesForFolderChildren;
            set
            {
                if (m_IncludeAddressesForFolderChildren != value)
                {
                    m_IncludeAddressesForFolderChildren = value;
                    SetDirty(true);
                }
            }
        }

        /// <summary>
        /// Determines whether a given schema will be included in a Schema Driven build. This is particularly useful
        /// if you want to alternate between building AssetBundles and ContentDirectories.
        /// Only one buildable schema can be enabled on a group at a time. If you attempt to enable multiple at once, an error will be thrown.
        /// </summary>
        public override bool IsEnabled
        {
            get => m_SchemaIsEnabled;
            set
            {
                if (m_SchemaIsEnabled != value)
                {
                    if (value)
                    {
                        string warningString = CanEnableSchema();
                        if (!string.IsNullOrEmpty(warningString))
                            Debug.LogError(warningString);
                        // Allow the set even when another buildable schema is enabled so the user can enable both;
                        // the group inspector shows an error and logs when both are enabled.
                    }

                    m_SchemaIsEnabled = value;
                    SetDirty(true);
                }
            }
        }

        /// <summary>
        /// Determines whether the ContentDirectorySchema can be enabled or not.
        /// A ContentDirectorySchema can be enabled if there are no other buildable schemas (such as a Content Packing &amp; Loading Schema) enabled.
        /// Used e.g. when adding a schema via Add Schema so the new schema is defaulted to disabled when the other is already enabled.
        /// The user can still manually enable both in the inspector; the group inspector then shows an error.
        /// </summary>
        /// <returns>Returns an empty string if enabling is valid, or an error/warning string if another buildable schema is already enabled.</returns>
        public override string CanEnableSchema()
        {
            foreach (var schema in Group.Schemas)
            {
                if (schema != this && schema is BundledAssetGroupSchema bags && bags.IsEnabled)
                    return AddressablesGUIUtility.CanEnableSchemaError(Group.Name, this.GetType(), schema.GetType());
            }
            return "";
        }

        internal override void Validate()
        {
            if (Group != null && Group.Settings != null)
            {
                List<string> variableNames = Group.Settings.profileSettings.GetVariableNames();
                SetPathVariable(Group.Settings, ref m_BuildPath, AddressableAssetSettings.kLocalBuildPath, "LocalBuildPath", variableNames);
                SetPathVariable(Group.Settings, ref m_LoadPath, AddressableAssetSettings.kLocalLoadPath, "LocalLoadPath", variableNames);
            }

#if ENABLE_CONTENT_DIRECTORIES
            if (m_GroupAssetEntryProviderType.Value == null)
                m_GroupAssetEntryProviderType.Value = typeof(NativeContentAssetEntryProvider);
#endif
        }

        internal const string k_AdvancedOptionsFoldoutKey = "Addressables.ContentDirectoryGroup.AdvancedOptions";

        // SessionState backed, so the open or closed state survives a domain reload.
        static FoldoutSessionStateValue s_AdvancedOptionsFoldout = new FoldoutSessionStateValue(k_AdvancedOptionsFoldoutKey);

        static readonly GUIContent k_AdvancedOptionsContent = new GUIContent("Advanced Options");

        internal const string k_IncludedInCatalogFoldoutKey = "Addressables.ContentDirectoryGroup.IncludedInCatalog";

        static FoldoutSessionStateValue s_IncludedInCatalogFoldout = new FoldoutSessionStateValue(k_IncludedInCatalogFoldoutKey);

        static readonly GUIContent k_IncludedInCatalogContent = new GUIContent("Included in Catalog");

        // Tests save and restore the foldouts through these. Going via IsActive keeps
        // the cached value in step with SessionState; writing SessionState alone does not.
        internal static bool AdvancedOptionsFoldoutActive
        {
            get => s_AdvancedOptionsFoldout.IsActive;
            set => s_AdvancedOptionsFoldout.IsActive = value;
        }

        internal static bool IncludedInCatalogFoldoutActive
        {
            get => s_IncludedInCatalogFoldout.IsActive;
            set => s_IncludedInCatalogFoldout.IsActive = value;
        }

        internal static readonly GUIContent k_IncludeLabelsInCatalogContent = new GUIContent("Labels",
            "Includes this group's labels in the catalog. Disable to reduce catalog size if labels are not needed.");

        internal static readonly GUIContent k_IncludeFolderKeysInCatalogContent = new GUIContent("Folder Key",
            "Adds each folder's address as a shared key on its assets, so you can load the whole folder in one call. Disabling reduces the catalog size if whole folder loading is not required.");

        internal static readonly GUIContent k_IncludeAddressesForFolderChildrenContent = new GUIContent("Individual Asset Addresses",
            "Includes each asset's own address in the catalog, in addition to its folder's shared key. Disable if assets are always loaded via their folder to reduce catalog size. GUIDs are unaffected.");

        // Which catalog toggles the user changed in one GUI pass.
        [Flags]
        internal enum CatalogToggles
        {
            None = 0,
            Labels = 1 << 0,
            FolderKeys = 1 << 1,
            AddressesForFolderChildren = 1 << 2
        }

        // The catalog toggle values as drawn in one GUI pass.
        internal struct CatalogToggleValues
        {
            public bool IncludeLabels;
            public bool IncludeFolderKeys;
            public bool IncludeAddressesForFolderChildren;
        }

        // Overriding the OnGUI here prevents the Include in Build setting from being shown twice
        // Currently the GUI for it is created in AssetInspectorGUI.DrawIncludeInBuildToggle
        /// <inheritdoc/>
        public override void OnGUI()
        {
            EditorGUI.BeginDisabledGroup(!IsEnabled);
            BuildAndLoadPathUIHelper.DrawPathPair(this, SchemaSerializedObject,
                ref m_BuildPath, ref m_LoadPath, ref m_UseCustomPaths, ref m_ShowPaths,
                ref m_SelectedPathPairIndex);
            var changedToggles = CatalogToggles.None;
            CatalogToggleValues toggleValues = default;
            if (BeginAdvancedOptions())
                changedToggles = ShowCatalogToggles(out toggleValues);

            // Flush the drawn properties before any setter replaces the cached object.
            SchemaSerializedObject.ApplyModifiedProperties();

            if (changedToggles != CatalogToggles.None)
            {
                Undo.RecordObject(this, name + "CatalogToggles");
                SetCatalogToggleOptions(this, changedToggles, toggleValues);
            }
            EditorGUI.EndDisabledGroup();
        }

        /// <inheritdoc/>
        public override void ShowAllProperties()
        {
            m_ShowPaths = true;
            s_AdvancedOptionsFoldout.IsActive = true;
            s_IncludedInCatalogFoldout.IsActive = true;
        }

        bool BeginAdvancedOptions()
        {
            s_AdvancedOptionsFoldout.IsActive = AddressablesGUIUtility.BeginFoldoutHeaderGroupWithHelp(
                s_AdvancedOptionsFoldout.IsActive, k_AdvancedOptionsContent,
                () => Application.OpenURL(AddressableAssetUtility.GenerateDocsURL(
                    "group-inspector-settings-reference.html#content-directory-advanced-options")),
                10);
            return s_AdvancedOptionsFoldout.IsActive;
        }

        // Shared by OnGUI and OnGUIMultiple.
        CatalogToggles ShowCatalogToggles(out CatalogToggleValues values)
        {
            values = new CatalogToggleValues
            {
                IncludeLabels = IncludeLabelsInCatalog,
                IncludeFolderKeys = IncludeFolderKeysInCatalog,
                IncludeAddressesForFolderChildren = IncludeAddressesForFolderChildren
            };

            var changed = CatalogToggles.None;
            EditorGUI.indentLevel++;
            s_IncludedInCatalogFoldout.IsActive = EditorGUILayout.Foldout(s_IncludedInCatalogFoldout.IsActive, k_IncludedInCatalogContent, true);
            if (s_IncludedInCatalogFoldout.IsActive)
            {
                if (AddressablesGUIUtility.DrawToggle(k_IncludeLabelsInCatalogContent, values.IncludeLabels, out values.IncludeLabels))
                    changed |= CatalogToggles.Labels;
                if (AddressablesGUIUtility.DrawToggle(k_IncludeFolderKeysInCatalogContent, values.IncludeFolderKeys, out values.IncludeFolderKeys))
                    changed |= CatalogToggles.FolderKeys;

                // Gate on the drawn value, not the field, which stays stale until the caller applies.
                if (values.IncludeFolderKeys)
                {
                    EditorGUI.indentLevel++;
                    if (AddressablesGUIUtility.DrawToggle(k_IncludeAddressesForFolderChildrenContent,
                            values.IncludeAddressesForFolderChildren, out values.IncludeAddressesForFolderChildren))
                        changed |= CatalogToggles.AddressesForFolderChildren;
                    EditorGUI.indentLevel--;
                }
            }
            EditorGUI.indentLevel--;
            return changed;
        }

        /// <inheritdoc/>
        public override void OnGUIMultiple(List<AddressableAssetGroupSchema> otherSchemas)
        {
            using (new EditorGUI.DisabledScope(!IsEnabled))
            {
                List<ContentDirectoryGroupSchema> otherContentDirectorySchemas = new List<ContentDirectoryGroupSchema>();
                foreach (var otherSchema in otherSchemas)
                {
                    if (otherSchema is ContentDirectoryGroupSchema otherContentDirectorySchema)
                        otherContentDirectorySchemas.Add(otherContentDirectorySchema);
                }

                bool pathPairModified = BuildAndLoadPathUIHelper.DrawPathPairMulti(this, SchemaSerializedObject, otherSchemas,
                    ref m_BuildPath, ref m_LoadPath, ref m_UseCustomPaths, ref m_ShowPaths,
                    ref m_SelectedPathPairIndex);

                var changedToggles = CatalogToggles.None;
                CatalogToggleValues toggleValues = default;
                if (BeginAdvancedOptions())
                    changedToggles = ShowCatalogToggles(out toggleValues);

                if (pathPairModified || changedToggles != CatalogToggles.None)
                {
                    Undo.SetCurrentGroupName("ContentDirectoryGroupSchemas BuildAndLoad Undos");

                    // Flush the path edits while the cached object is still the one they were
                    // drawn against, and before any setter replaces it. SetPathPairOption also
                    // reads this schema's path fields, so they have to be written by now.
                    SchemaSerializedObject.ApplyModifiedProperties();

                    Undo.RecordObject(this, "ContentDirectoryGroupSchema BuildAndLoad" + name);
                    SetCatalogToggleOptions(this, changedToggles, toggleValues);

                    foreach (var schema in otherContentDirectorySchemas)
                    {
                        Undo.RecordObject(schema, "ContentDirectoryGroupSchema BuildAndLoad" + schema.name);
                        if (pathPairModified)
                            SetPathPairOption(this, schema);
                        SetCatalogToggleOptions(schema, changedToggles, toggleValues);
                    }
                    Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
                }
            }
        }

        void SetPathPairOption(ContentDirectoryGroupSchema src, ContentDirectoryGroupSchema dst)
        {
            if (dst.m_BuildPath.Id != src.BuildPath.Id)
                dst.m_BuildPath.Id = src.BuildPath.Id;

            if (dst.m_LoadPath.Id != src.m_LoadPath.Id)
                dst.m_LoadPath.Id = src.m_LoadPath.Id;

            if (dst.m_UseCustomPaths != src.m_UseCustomPaths)
                dst.m_UseCustomPaths = src.m_UseCustomPaths;

            if (dst.SelectedPathPairIndex != src.SelectedPathPairIndex)
                dst.SelectedPathPairIndex = src.SelectedPathPairIndex;

            dst.SetDirty(true);
        }


        internal static void SetCatalogToggleOptions(ContentDirectoryGroupSchema schema,
            CatalogToggles toggles, CatalogToggleValues values)
        {
            if ((toggles & CatalogToggles.Labels) != 0)
                schema.IncludeLabelsInCatalog = values.IncludeLabels;

            if ((toggles & CatalogToggles.FolderKeys) != 0)
                schema.IncludeFolderKeysInCatalog = values.IncludeFolderKeys;

            if ((toggles & CatalogToggles.AddressesForFolderChildren) != 0)
                schema.IncludeAddressesForFolderChildren = values.IncludeAddressesForFolderChildren;
        }

        internal int DetermineSelectedIndex(List<ProfileGroupType> groupTypes, int defaultValue, AddressableAssetSettings addressableAssetSettings, HashSet<string> vars)
        {
            return BuildAndLoadPathUIHelper.DetermineSelectedIndex(BuildPath, LoadPath, m_UseCustomPaths, groupTypes, defaultValue, addressableAssetSettings, vars);
        }

        /// <summary>
        /// Implementation of ISerializationCallbackReceiver. Used to set callbacks for ProfileValueReference changes and default provider types.
        /// </summary>
        public void OnAfterDeserialize()
        {
            BuildPath.OnValueChanged -= OnPathValueChanged;
            BuildPath.OnValueChanged += OnPathValueChanged;
            LoadPath.OnValueChanged -= OnPathValueChanged;
            LoadPath.OnValueChanged += OnPathValueChanged;
#if ENABLE_CONTENT_DIRECTORIES
            if (m_GroupAssetEntryProviderType.Value == null)
                m_GroupAssetEntryProviderType.Value = typeof(NativeContentAssetEntryProvider);
#endif
        }

        void OnPathValueChanged(ProfileValueReference _)
        {
            SetDirty(true);
        }


        /// <summary>
        /// Implementation of ISerializationCallbackReceiver. Does nothing.
        /// </summary>
        public void OnBeforeSerialize()
        {
        }
    }
}
