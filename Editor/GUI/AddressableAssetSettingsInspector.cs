using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Build;
using UnityEditor.Build.Player;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.ResourceManagement.Util;
using UnityEngine.Serialization;
using static UnityEditor.AddressableAssets.Settings.GroupSchemas.BundledAssetGroupSchema;

namespace UnityEditor.AddressableAssets.GUI
{
    [ExcludeFromCodeCoverage]
    [CustomEditor(typeof(AddressableAssetSettings))]
    class AddressableAssetSettingsInspector : Editor
    {
        AddressableAssetSettings m_AasTarget;

        static FoldoutSessionStateValue ProfilesFoldout = new FoldoutSessionStateValue("Addressables.ProfilesFoldout");
        GUIContent m_ProfilesHeader;
        static FoldoutSessionStateValue DiagnosticsFoldout = new FoldoutSessionStateValue("Addressables.DiagnosticsFoldout");
        GUIContent m_DiagnosticsHeader;
        static FoldoutSessionStateValue CatalogFoldout = new FoldoutSessionStateValue("Addressables.CatalogFoldout");
        GUIContent m_CatalogsHeader;
        static FoldoutSessionStateValue ContentUpdateFoldout = new FoldoutSessionStateValue("Addressables.ContentUpdateFoldout");
        GUIContent m_ContentUpdateHeader;
        static FoldoutSessionStateValue DownloadsFoldout = new FoldoutSessionStateValue("Addressables.DownloadsFoldout");
        GUIContent m_DownloadsHeader;
        static FoldoutSessionStateValue BuildFoldout = new FoldoutSessionStateValue("Addressables.BuildFoldout");
        GUIContent m_BuildHeader;
        static FoldoutSessionStateValue DataBuildersFoldout = new FoldoutSessionStateValue("Addressables.DataBuildersFoldout");
        GUIContent m_DataBuildersHeader;
        static FoldoutSessionStateValue GroupTemplateObjectsFoldout = new FoldoutSessionStateValue("Addressables.GroupTemplateObjectsFoldout");
        GUIContent m_GroupTemplateObjectsHeader;
        static FoldoutSessionStateValue InitObjectsFoldout = new FoldoutSessionStateValue("Addressables.InitObjectsFoldout");
        GUIContent m_InitObjectsHeader;
        static FoldoutSessionStateValue CCDEnabledFoldout = new FoldoutSessionStateValue("Addressables.CCDEnabledFoldout");
        GUIContent m_CCDEnabledHeader;

        //Used for displaying path pairs
        bool m_UseCustomPaths = false;
        bool m_ShowPaths = true;
        bool m_ShowContentStatePath = true;

        [FormerlySerializedAs("m_profileEntriesRL")]
        [SerializeField]
        ReorderableList m_ProfileEntriesRl;

        [FormerlySerializedAs("m_dataBuildersRL")]
        [SerializeField]
        ReorderableList m_DataBuildersRl;

        [SerializeField]
        ReorderableList m_GroupTemplateObjectsRl;

        [FormerlySerializedAs("m_initObjectsRL")]
        [SerializeField]
        ReorderableList m_InitObjectsRl;

        [FormerlySerializedAs("m_currentProfileIndex")]
        [SerializeField]
        int m_CurrentProfileIndex = -1;

        List<Action> m_QueuedChanges = new List<Action>();

        void OnEnable()
        {
            m_AasTarget = target as AddressableAssetSettings;
            if (m_AasTarget == null)
                return;

            var names = m_AasTarget.profileSettings.profileEntryNames;
            m_ProfileEntriesRl = new ReorderableList(names, typeof(AddressableAssetProfileSettings.ProfileIdData), true, true, true, true);
            m_ProfileEntriesRl.drawElementCallback = DrawProfileEntriesCallback;
            m_ProfileEntriesRl.drawHeaderCallback = DrawProfileEntriesHeader;
            m_ProfileEntriesRl.onAddCallback = OnAddProfileEntry;
            m_ProfileEntriesRl.onRemoveCallback = OnRemoveProfileEntry;

            m_DataBuildersRl = new ReorderableList(m_AasTarget.DataBuilders, typeof(ScriptableObject), true, true, true, true);
            m_DataBuildersRl.drawElementCallback = DrawDataBuilderCallback;
            m_DataBuildersRl.headerHeight = 0;
            m_DataBuildersRl.onAddDropdownCallback = OnAddDataBuilder;
            m_DataBuildersRl.onRemoveCallback = OnRemoveDataBuilder;

            m_GroupTemplateObjectsRl = new ReorderableList(m_AasTarget.GroupTemplateObjects, typeof(ScriptableObject), true, true, true, true);
            m_GroupTemplateObjectsRl.drawElementCallback = DrawGroupTemplateObjectCallback;
            m_GroupTemplateObjectsRl.headerHeight = 0;
            m_GroupTemplateObjectsRl.onAddDropdownCallback = OnAddGroupTemplateObject;
            m_GroupTemplateObjectsRl.onRemoveCallback = OnRemoveGroupTemplateObject;

            m_InitObjectsRl = new ReorderableList(m_AasTarget.InitializationObjects, typeof(ScriptableObject), true, true, true, true);
            m_InitObjectsRl.drawElementCallback = DrawInitializationObjectCallback;
            m_InitObjectsRl.headerHeight = 0;
            m_InitObjectsRl.onAddDropdownCallback = OnAddInitializationObject;
            m_InitObjectsRl.onRemoveCallback = OnRemoveInitializationObject;

            m_ProfilesHeader = new GUIContent("Profiles", "Settings affect profiles.");
            m_DiagnosticsHeader = new GUIContent("Diagnostics", "Settings affect profiles.");
            m_CatalogsHeader = new GUIContent("Catalog", "Settings affect profiles.");
            m_ContentUpdateHeader = new GUIContent("Update a Previous Build", "Settings affect profiles.");
            m_DownloadsHeader = new GUIContent("Downloads", "Settings affect profiles.");
            m_BuildHeader = new GUIContent("Build", "Settings affect profiles.");

            m_DataBuildersHeader = new GUIContent("Build and Play Mode Scripts", "Settings affect profiles.");
            m_GroupTemplateObjectsHeader = new GUIContent("Asset Group Templates", "Settings affect profiles.");
            m_InitObjectsHeader = new GUIContent("Initialization Objects", "Settings affect profiles.");
            m_CCDEnabledHeader = new GUIContent("Cloud Content Delivery", "Settings affect profiles.");
        }

        GUIContent m_ManageGroups =
            new GUIContent("Manage Groups", "Open the Addressables Groups window");

        GUIContent m_ManageProfiles =
            new GUIContent("Manage Profiles", "Open the Addressables Profiles window");

        GUIContent m_LogRuntimeExceptions =
            new GUIContent("Log Runtime Exceptions",
                "Addressables does not throw exceptions at run time when there are loading issues, instead it adds to the error state of the IAsyncOperation.  With this flag enabled, exceptions will also be logged.");

        GUIContent m_OverridePlayerVersion =
            new GUIContent("Player Version Override", "If set, this will be used as the player version instead of one based off of a time stamp.");

        GUIContent m_UniqueBundles =
            new GUIContent("Unique Bundle IDs",
                "If set, every content build (original or update) will result in asset bundles with more complex internal names.  This may result in more bundles being rebuilt, but safer mid-run updates.  See docs for more info.");

        GUIContent m_ContiguousBundles =
            new GUIContent("Contiguous Bundles",
                "If set, packs assets in bundles contiguously based on the ordering of the source asset which results in improved asset loading times. Disable this if you've built bundles with a version of Addressables older than 1.12.1 and you want to minimize bundle changes.");
#if NONRECURSIVE_DEPENDENCY_DATA
        GUIContent m_NonRecursiveBundleBuilding =
            new GUIContent("Non-Recursive Dependency Calculation",
                "If set, Calculates and build asset bundles using Non-Recursive Dependency calculation methods. This approach helps reduce asset bundle rebuilds and runtime memory consumption.");
#else
        GUIContent m_NonRecursiveBundleBuilding =
            new GUIContent("Non-Recursive Dependency Calculation", "If set, Calculates and build asset bundles using Non-Recursive Dependency calculation methods. This approach helps reduce asset bundle rebuilds and runtime memory consumption.\n*Requires Unity 2019.4.19f1 or above");
#endif
        GUIContent m_BuildRemoteCatalog =
            new GUIContent("Build Remote Catalog",
                "If set, this will create a copy of the content catalog for storage on a remote server.  This catalog can be overwritten later for content updates.");

        GUIContent m_BundleLocalCatalog =
            new GUIContent("Compress Local Catalog",
                "If set, the local content catalog will be compressed in an asset bundle. This will affect build and load time of catalog. We recommend disabling this during iteration.");

        GUIContent m_OptimizeCatalogSize =
            new GUIContent("Optimize Catalog Size",
                "If set, duplicate internal ids will be extracted to a lookup table and reconstructed at runtime.  This can reduce the size of the catalog but may impact performance due to extra processing at load time.");

        GUIContent m_CheckForCatalogUpdateOnInit =
            new GUIContent("Only update catalogs manually", "If set, this will forgo checking for content catalog updates on initialization.");

        GUIContent m_InternalIdNamingMode = new GUIContent("Internal Asset Naming Mode",
            "Mode for naming assets internally in bundles.  This can reduce the size of the catalog by replacing long paths with shorter strings. Dynamic recommended for Release. Full Path recommended for Development.");

        GUIContent m_InternalBundleIdMode = new GUIContent("Internal Bundle Id Mode",
            $"Specifies how the internal id of the bundle is generated.  This must be set to {BundleInternalIdMode.GroupGuid} or {BundleInternalIdMode.GroupGuidProjectIdHash} to ensure proper caching on device.");

        GUIContent m_AssetLoadMode = new GUIContent("Asset Load Mode", "Determines how Assets are loaded when accessed." +
            "\n- Requested Asset And Dependencies, will only load the requested Asset (Recommended for most platforms)." +
            "\n- All Packed Assets And Dependencies, will load all Assets that are packed together. Best used when loading all Assets into memory is required (Recommended for Nintendo Switch).");

        GUIContent m_AssetProvider =
            new GUIContent("Asset Provider", "The provider to use for loading assets out of AssetBundles. Modify only if you have a custom asset provider.");

        GUIContent m_BundleProvider =
            new GUIContent("Asset Bundle Provider", "The provider to use for loading AssetBundles (not the assets within bundles). Modify only if you have a custom AssetBundle provider.");

        GUIContent m_RemoteCatBuildandLoadPaths =
            new GUIContent("Build & Load Paths", "Paths to build or load a remote content catalog");

        GUIContent m_RemoteCatPathPreview =
            new GUIContent("Path Preview", "Preview of what the current paths will be evaluated to");

        GUIContent m_RemoteCatBuildPath =
            new GUIContent("Build Path", "The path for a remote content catalog.");

        GUIContent m_RemoteCatLoadPath =
            new GUIContent("Load Path", "The path to load a remote content catalog.");

        GUIContent m_CatalogTimeout =
            new GUIContent("Catalog Download Timeout", "The time until a catalog hash or json UnityWebRequest download will timeout in seconds. 0 for no timeout.");

        GUIContent m_BundleTimeout =
            new GUIContent("Bundle Request Timeout", "The timeout with no download activity (in seconds) for the bundle http request (Recommended 5-10s).");

        GUIContent m_BundleRetryCount =
            new GUIContent("Bundle Retry Count", "The number of times to retry the bundle http request. Note that a retry count of 0 allows auto-downloading asset bundles that fail to load from the cache. Set to -1 to prevent this auto-downloading behavior.");

        GUIContent m_BundleRedirectLimit =
            new GUIContent("Bundle Http Redirect Limit", "The redirect limit for the bundle http request.");

        GUIContent m_UseUWRForLocalBundles =
            new GUIContent("Use UnityWebRequest for Local Asset Bundles", "If enabled, local asset bundles will load through UnityWebRequest (Recommended for Android and WebGL).");

        GUIContent m_CertificateHandlerType =
            new GUIContent("Custom certificate handler", "The class to use for custom certificate handling.  This type must inherit from UnityEngine.Networking.CertificateHandler.");

        GUIContent m_ProfileInUse =
            new GUIContent("Profile In Use", "This is the active profile that will be used to evaluate all profile variables during a build and when entering play mode.");

        GUIContent m_MaxConcurrentWebRequests =
            new GUIContent("Max Concurrent Web Requests", "Limits the number of concurrent web requests.  If more requests are made, they will be queued until some requests complete.");

        GUIContent m_IgnoreUnsupportedFilesInBuild =
            new GUIContent("Ignore Invalid/Unsupported Files in Build", "If enabled, files that cannot be built will be ignored.");

        GUIContent m_ContentStateFileBuildPath =
            new GUIContent("Content State Build Path", "The path used for the addressables_content_state.bin file, which is used to detect modified assets during a Content Update.");

        GUIContent m_SharedBundleSettings =
            new GUIContent("Shared Bundle Settings", "Determines the group whose settings used for shared bundles (Built In and MonoScript bundles). By default this is the Default group.");

        GUIContent m_SharedBundleSettingsGroup =
            new GUIContent("Shared Bundle Settings Group", "Group whose settings are used for shared bundles (Built In and MonoScript bundles).");

        GUIContent m_BuiltInBundleNaming =
            new GUIContent("Built In Bundle Naming Prefix",
                "This setting determines how the Unity built in bundle will be named during the build.  The recommended setting is Project Name Hash.");

        GUIContent m_BuiltInBundleCustomNaming =
            new GUIContent("Built In Bundle Custom Prefix", "Custom prefix for Unity built in bundle.");

        GUIContent m_MonoBundleNaming =
            new GUIContent("MonoScript Bundle Naming Prefix",
                "This setting determines how and if the MonoScript bundle will be named during the build.  The recommended setting is Project Name Hash.");

        GUIContent m_MonoBundleCustomNaming =
            new GUIContent("MonoScript Bundle Custom Prefix", "Custom prefix for MonoScript bundle.");

        GUIContent m_StripUnityVersionFromBundleBuild =
            new GUIContent("Strip Unity Version from AssetBundles", "If enabled, the Unity Editor version is stripped from the archive file header and the bundles's serialized files.");

        GUIContent m_DisableVisibleSubAssetRepresentations =
            new GUIContent("Disable Visible Sub Asset Representations", "If enabled, the build will assume that all sub Assets have no visible asset representations.");

        GUIContent m_ContentUpdateAutoCheckForRestrictions =
            new GUIContent("Check for Update Issues", "Inform the system if it should perform a Content Update Restriction check as part of updating a previous build, and how to handle the result.");

        GUIContent m_AllowNestedFolders =
            new GUIContent("Allow Nested Folders", "If enabled and there is a path separator in an Addressables key, subfolders will be created when the bundle mode is set to Pack Separately.This is legacy behavior.");

#if (ENABLE_CCD)
        GUIContent m_BuildAndReleaseBinFile =
            new GUIContent("For Build & Release", "Determines where the system attempts to pull the previous content state file from for the Content Update.");
#endif

        GUIContent m_CCDEnabled = new GUIContent("Enable CCD Features", "If enabled, will add options to upload bundles to CCD.");

        GUIContent m_CCDLogRequests = new GUIContent("Log HTTP Requests", "If enabled, will log requests to the CCD management API.");
        GUIContent m_CCDLogRequestHeaders = new GUIContent("Log HTTP Request Headers", "If enabled, will log request headers.");
        GUIContent m_BuildAddressablesWithPlayerBuild =
            new GUIContent("Build Addressables on Player Build", "Determines if a new Addressables build will be built with a Player Build.");

        GUIContent[] m_BuildAddressablesWithPlayerBuildOptions = new GUIContent[]
        {
            new GUIContent("Use global Settings (stored in preferences)", "Use settings specified in the Preferences window"),
            new GUIContent("Build Addressables content on Player Build", "A new Addressables build will be created with a Player Build"),
            new GUIContent("Do not Build Addressables content on Player build", "No new Addressables build will be created with a Player Build")
        };

        GUIContent m_EnableJsonCatalog = new GUIContent("Enable Json Catalog",
            "If enabled, Json catalogs will be used instead of binary catalogs. Json catalogs are more human readible, but slower to load and have a larger size.");

        public override bool RequiresConstantRepaint()
        {
            return true;
        }

        public override void OnInspectorGUI()
        {
            m_QueuedChanges.Clear();
            serializedObject.UpdateIfRequiredOrScript(); // use updated values
            EditorGUI.BeginChangeCheck();
            float postBlockContentSpace = 10;

            GUILayout.Space(8);
            if (GUILayout.Button(m_ManageGroups, "Minibutton", GUILayout.ExpandWidth(true)))
            {
                AddressableAssetsWindow.Init();
            }

            GUILayout.Space(12);
            ProfilesFoldout.IsActive = AddressablesGUIUtility.BeginFoldoutHeaderGroupWithHelp(ProfilesFoldout.IsActive, m_ProfilesHeader, () =>
            {
                string url = AddressableAssetUtility.GenerateDocsURL("AddressableAssetSettings.html#profile");
                Application.OpenURL(url);
            });
            if (ProfilesFoldout.IsActive)
            {
                if (m_AasTarget.profileSettings.profiles.Count > 0)
                {
                    if (m_CurrentProfileIndex < 0 || m_CurrentProfileIndex >= m_AasTarget.profileSettings.profiles.Count)
                        m_CurrentProfileIndex = 0;
                    var profileNames = m_AasTarget.profileSettings.GetAllProfileNames();

                    int currentProfileIndex = m_CurrentProfileIndex;
                    // Current profile in use was changed by different window
                    if (m_AasTarget.profileSettings.profiles[m_CurrentProfileIndex].id != m_AasTarget.activeProfileId)
                    {
                        currentProfileIndex =
                            profileNames.IndexOf(m_AasTarget.profileSettings.GetProfileName(m_AasTarget.activeProfileId));
                        if (currentProfileIndex != m_CurrentProfileIndex)
                            m_QueuedChanges.Add(() => m_CurrentProfileIndex = currentProfileIndex);
                    }

                    currentProfileIndex = EditorGUILayout.Popup(m_ProfileInUse, currentProfileIndex, profileNames.ToArray());
                    if (currentProfileIndex != m_CurrentProfileIndex)
                        m_QueuedChanges.Add(() => m_CurrentProfileIndex = currentProfileIndex);

                    m_AasTarget.activeProfileId = m_AasTarget.profileSettings.GetProfileId(profileNames[currentProfileIndex]);

                    EditorGUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button(m_ManageProfiles, "Minibutton"))
                    {
                        EditorWindow.GetWindow<ProfileWindow>().Show(true);
                    }

                    EditorGUILayout.EndHorizontal();
                }
                else
                {
                    EditorGUILayout.LabelField("No valid profiles found");
                }

                GUILayout.Space(postBlockContentSpace);
            }

            EditorGUI.EndFoldoutHeaderGroup();

            DiagnosticsFoldout.IsActive = AddressablesGUIUtility.BeginFoldoutHeaderGroupWithHelp(DiagnosticsFoldout.IsActive, m_DiagnosticsHeader, () =>
            {
                string url = AddressableAssetUtility.GenerateDocsURL("AddressableAssetSettings.html#diagnostics");
                Application.OpenURL(url);
            });
            if (DiagnosticsFoldout.IsActive)
            {
                bool logResourceManagerExceptions = EditorGUILayout.Toggle(m_LogRuntimeExceptions, m_AasTarget.buildSettings.LogResourceManagerExceptions);

                if (logResourceManagerExceptions != m_AasTarget.buildSettings.LogResourceManagerExceptions)
                    m_QueuedChanges.Add(() => m_AasTarget.buildSettings.LogResourceManagerExceptions = logResourceManagerExceptions);
                GUILayout.Space(postBlockContentSpace);
            }

            EditorGUI.EndFoldoutHeaderGroup();

            CatalogFoldout.IsActive = AddressablesGUIUtility.BeginFoldoutHeaderGroupWithHelp(CatalogFoldout.IsActive, m_CatalogsHeader, () =>
            {
                string url = AddressableAssetUtility.GenerateDocsURL("AddressableAssetSettings.html#catalog");
                Application.OpenURL(url);
            });
            if (CatalogFoldout.IsActive)
            {
                string overridePlayerVersion = EditorGUILayout.TextField(m_OverridePlayerVersion, m_AasTarget.OverridePlayerVersion);
                if (overridePlayerVersion != m_AasTarget.OverridePlayerVersion)
                    m_QueuedChanges.Add(() => m_AasTarget.OverridePlayerVersion = overridePlayerVersion);

#if ENABLE_JSON_CATALOG
                bool bundleLocalCatalog = EditorGUILayout.Toggle(m_BundleLocalCatalog, m_AasTarget.BundleLocalCatalog);
                if (bundleLocalCatalog != m_AasTarget.BundleLocalCatalog)
                    m_QueuedChanges.Add(() => m_AasTarget.BundleLocalCatalog = bundleLocalCatalog);
#endif
/*
                bool optimizeCatalogSize = EditorGUILayout.Toggle(m_OptimizeCatalogSize, m_AasTarget.OptimizeCatalogSize);
                if (optimizeCatalogSize != m_AasTarget.OptimizeCatalogSize)
                    m_QueuedChanges.Add(() => m_AasTarget.OptimizeCatalogSize = optimizeCatalogSize);
*/
                bool buildRemoteCatalog = EditorGUILayout.Toggle(m_BuildRemoteCatalog, m_AasTarget.BuildRemoteCatalog);
                if (buildRemoteCatalog != m_AasTarget.BuildRemoteCatalog)
                    m_QueuedChanges.Add(() => m_AasTarget.BuildRemoteCatalog = buildRemoteCatalog);
                if ((m_AasTarget.RemoteCatalogBuildPath != null && m_AasTarget.RemoteCatalogLoadPath != null) // these will never actually be null, as the accessor initializes them.
                    && (buildRemoteCatalog))
                {
                    DrawRemoteCatalogPaths();
                }

                bool enableJsonCatalog = EditorGUILayout.Toggle(m_EnableJsonCatalog, m_AasTarget.EnableJsonCatalog);
                if (enableJsonCatalog != m_AasTarget.EnableJsonCatalog)
                    m_QueuedChanges.Add(() => ToggleJsonCatalog(m_AasTarget, enableJsonCatalog));

                EditorGUI.BeginDisabledGroup(!buildRemoteCatalog);
                bool disableCatalogOnStartup = EditorGUILayout.Toggle(m_CheckForCatalogUpdateOnInit, m_AasTarget.DisableCatalogUpdateOnStartup);
                if (disableCatalogOnStartup != m_AasTarget.DisableCatalogUpdateOnStartup)
                    m_QueuedChanges.Add(() => m_AasTarget.DisableCatalogUpdateOnStartup = disableCatalogOnStartup);
                EditorGUI.EndDisabledGroup();
                GUILayout.Space(postBlockContentSpace);

                var internalIdNamingMode = (AssetNamingMode)EditorGUILayout.EnumPopup(m_InternalIdNamingMode, m_AasTarget.InternalIdNamingMode);
                if (internalIdNamingMode != m_AasTarget.InternalIdNamingMode)
                    m_QueuedChanges.Add(() => m_AasTarget.InternalIdNamingMode = internalIdNamingMode);

                var internalBundleIdMode = (BundleInternalIdMode)EditorGUILayout.EnumPopup(m_InternalBundleIdMode, m_AasTarget.InternalBundleIdMode);
                if (internalBundleIdMode != m_AasTarget.InternalBundleIdMode)
                    m_QueuedChanges.Add(() => m_AasTarget.InternalBundleIdMode = internalBundleIdMode);

                var assetLoadMode = (AssetLoadMode)EditorGUILayout.EnumPopup(m_AssetLoadMode, m_AasTarget.AssetLoadMode);
                if (assetLoadMode != m_AasTarget.AssetLoadMode)
                    m_QueuedChanges.Add(() => m_AasTarget.AssetLoadMode = assetLoadMode);

                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_BundledAssetProviderType"), m_AssetProvider, true);
                if (EditorGUI.EndChangeCheck())
                {
                    m_QueuedChanges.Add(() => m_AasTarget.UpdateBundledAssetProviderType());
                }

                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_AssetBundleProviderType"), m_BundleProvider, true);
                if (EditorGUI.EndChangeCheck())
                {
                    m_QueuedChanges.Add(() => m_AasTarget.UpdateAssetBundleProviderType());
                }

                GUILayout.Space(postBlockContentSpace);
            }

            EditorGUI.EndFoldoutHeaderGroup();

            ContentUpdateFoldout.IsActive = AddressablesGUIUtility.BeginFoldoutHeaderGroupWithHelp(ContentUpdateFoldout.IsActive, m_ContentUpdateHeader, () =>
            {
                string url = AddressableAssetUtility.GenerateDocsURL("AddressableAssetSettings.html#content-update");
                Application.OpenURL(url);
            });
            if (ContentUpdateFoldout.IsActive)
            {
                if (!m_AasTarget.BuildRemoteCatalog)
                    EditorGUILayout.HelpBox("You must enable the remote catalog in the Catalog settings to update a previous build.", MessageType.Warning);
                EditorGUI.BeginDisabledGroup(!m_AasTarget.BuildRemoteCatalog);

                int contentUpdateRestrictionsCheckOptionIndex = EditorGUILayout.Popup(m_ContentUpdateAutoCheckForRestrictions, (int)m_AasTarget.CheckForContentUpdateRestrictionsOption,
                    new[] {"List Restricted Assets (recommended)", "Fail Build", "Disabled"});
                if (contentUpdateRestrictionsCheckOptionIndex != (int)m_AasTarget.CheckForContentUpdateRestrictionsOption)
                    m_QueuedChanges.Add(() => m_AasTarget.CheckForContentUpdateRestrictionsOption =
                            (CheckForContentUpdateRestrictionsOptions)contentUpdateRestrictionsCheckOptionIndex);

#if ENABLE_CCD
                if (m_AasTarget.BuildAndReleaseBinFileOption == BuildAndReleaseContentStateBehavior.UseCCDBucket)
                {
                    EditorGUILayout.HelpBox($"Addressables settings are set to use CCD buckets for the building of the previous content " +
                        $"state file.  The addressables_content_state.bin file will be built to {m_AasTarget.RemoteCatalogBuildPath.GetValue(m_AasTarget)}. " +
                        $"If you wish to manually set the build path, change the Build and Release Content State file option to Use Content State Build Path.", MessageType.Info);
                }
                else
#endif
                {
                    var variableNames = m_AasTarget.profileSettings.GetVariableNames();
                    variableNames.Add(AddressableAssetProfileSettings.customEntryString);
                    //This needs to be the last item in the list, or setting the index to names.Count - 1 below needs to be refactored.
                    variableNames.Add(AddressableAssetProfileSettings.defaultSettingsPathString);

                    int profileVariableIdIndex = EditorGUILayout.Popup(m_ContentStateFileBuildPath,
                        variableNames.IndexOf(m_AasTarget.m_ContentStateBuildPathProfileVariableName),
                        variableNames.ToArray());

                    //if this hasn't been set, make it use the default settings path, unless there's a path already set.
                    if (profileVariableIdIndex <= -1)
                    {
                        if (!string.IsNullOrEmpty(m_AasTarget.m_ContentStateBuildPath))
                        {
                            //there was an existing content state path put into the project, lets retain that.
                            profileVariableIdIndex = variableNames.IndexOf(AddressableAssetProfileSettings.customEntryString);
                        }
                        else //Use the <default settings path>
                            profileVariableIdIndex = variableNames.Count - 1;
                    }

                    //This is mainly to make sure we don't get into a bad state if the user changes the value via script
                    if (m_AasTarget.m_ContentStateBuildPathProfileVariableName != AddressableAssetProfileSettings.customEntryString &&
                        !string.IsNullOrEmpty(m_AasTarget.m_ContentStateBuildPath))
                    {
                        m_QueuedChanges.Add(() => { m_AasTarget.m_ContentStateBuildPathProfileVariableName = AddressableAssetProfileSettings.customEntryString; });
                    }

                    if (m_AasTarget.m_ContentStateBuildPathProfileVariableName != variableNames[profileVariableIdIndex])
                        m_QueuedChanges.Add(() =>
                        {
                            m_AasTarget.m_ContentStateBuildPathProfileVariableName = variableNames[profileVariableIdIndex];
                            //We only want the value serialized into the content state build path if using the <custom> option
                            //This is pretty edge case now, given how the ContentStateBuildPath property is setup.  This is only useful
                            //if the user has set the path via script and then gone into the UI and changed it
                            if (m_AasTarget.m_ContentStateBuildPathProfileVariableName != AddressableAssetProfileSettings.customEntryString)
                                m_AasTarget.m_ContentStateBuildPath = "";
                        });

                    //If we're set to a custom entry, display the custom entry text field
                    if (variableNames[profileVariableIdIndex] == AddressableAssetProfileSettings.customEntryString)
                    {
                        string contentStateBuildPath = EditorGUILayout.TextField("Custom Content State Build Path", m_AasTarget.m_CustomContentStateBuildPath);
                        if (contentStateBuildPath != m_AasTarget.ContentStateBuildPath)
                            m_QueuedChanges.Add(() => { m_AasTarget.m_CustomContentStateBuildPath = contentStateBuildPath; });
                    }

                    EditorGUI.indentLevel++;
                    m_ShowContentStatePath = EditorGUILayout.Foldout(m_ShowContentStatePath, "Path Preview", true);
                    if (m_ShowContentStatePath)
                    {
                        EditorStyles.helpBox.fontSize = 12;
                        string evaluatedBuildPath = m_AasTarget.profileSettings.EvaluateString(m_AasTarget.activeProfileId, m_AasTarget.ContentStateBuildPath);
                        EditorGUILayout.HelpBox(String.Format("Build Path: {0}", evaluatedBuildPath), MessageType.None);
                        if (ResourceManagerConfig.ShouldPathUseWebRequest(evaluatedBuildPath))
                        {
                            string contentStatePath = "";
#if ENABLE_CCD
                            contentStatePath = Path.Combine(m_AasTarget.RemoteCatalogBuildPath.GetValue(m_AasTarget), "addressables_content_state.bin");
#else
                            contentStatePath = ContentUpdateScript.PreviousContentStateFileCachePath;
#endif
                            EditorGUILayout.HelpBox($"The content state file path is a remote path. For new content builds, the content state file can be found at {contentStatePath}. " +
                                $"When updating a previous build, the remote file will be downloaded to that same location.", MessageType.Info);
                        }
                    }

                    EditorGUI.indentLevel--;
                }

#if ENABLE_CCD
                int buildAndReleaseContentBehavior = EditorGUILayout.Popup(m_BuildAndReleaseBinFile, (int)m_AasTarget.BuildAndReleaseBinFileOption,
                    new[] {"Use Content State Build Path", "Use uploaded addressables_content_state.bin"});
                if (buildAndReleaseContentBehavior != (int)m_AasTarget.BuildAndReleaseBinFileOption)
                    m_QueuedChanges.Add(() => m_AasTarget.BuildAndReleaseBinFileOption = (BuildAndReleaseContentStateBehavior)buildAndReleaseContentBehavior);
#endif
                EditorGUI.EndDisabledGroup();

                GUILayout.Space(postBlockContentSpace);
            }

            EditorGUI.EndFoldoutHeaderGroup();

            DownloadsFoldout.IsActive = AddressablesGUIUtility.BeginFoldoutHeaderGroupWithHelp(DownloadsFoldout.IsActive, m_DownloadsHeader, () =>
            {
                string url = AddressableAssetUtility.GenerateDocsURL("AddressableAssetSettings.html#downloads");
                Application.OpenURL(url);
            });
            if (DownloadsFoldout.IsActive)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_CertificateHandlerType"), m_CertificateHandlerType);

                var maxWebReqs = EditorGUILayout.IntField(m_MaxConcurrentWebRequests, m_AasTarget.MaxConcurrentWebRequests);
                maxWebReqs = Mathf.Clamp(maxWebReqs, 1, 1024);
                if (maxWebReqs != m_AasTarget.MaxConcurrentWebRequests)
                    m_QueuedChanges.Add(() => m_AasTarget.MaxConcurrentWebRequests = maxWebReqs);

                var catalogTimeouts = EditorGUILayout.IntField(m_CatalogTimeout, m_AasTarget.CatalogRequestsTimeout);
                if (catalogTimeouts != m_AasTarget.CatalogRequestsTimeout)
                    m_QueuedChanges.Add(() => m_AasTarget.CatalogRequestsTimeout = catalogTimeouts);
                GUILayout.Space(postBlockContentSpace);

                var useUWRForLocalBundles = EditorGUILayout.Toggle(m_UseUWRForLocalBundles, m_AasTarget.UseUnityWebRequestForLocalBundles);
                if (useUWRForLocalBundles != m_AasTarget.UseUnityWebRequestForLocalBundles)
                    m_QueuedChanges.Add(() => m_AasTarget.UseUnityWebRequestForLocalBundles = useUWRForLocalBundles);

                var bundleTimeout = EditorGUILayout.IntField(m_BundleTimeout, m_AasTarget.BundleTimeout);
                if (bundleTimeout != m_AasTarget.BundleTimeout)
                    m_QueuedChanges.Add(() => m_AasTarget.BundleTimeout = bundleTimeout);

                var bundleRetryCount = EditorGUILayout.IntField(m_BundleRetryCount, m_AasTarget.BundleRetryCount);
                if (bundleRetryCount != m_AasTarget.BundleRetryCount)
                    m_QueuedChanges.Add(() => m_AasTarget.BundleRetryCount = bundleRetryCount);

                var bundleRedirectLimit = EditorGUILayout.IntField(m_BundleRedirectLimit, m_AasTarget.BundleRedirectLimit);
                if (bundleRedirectLimit != m_AasTarget.BundleRedirectLimit)
                    m_QueuedChanges.Add(() => m_AasTarget.BundleRedirectLimit = bundleRedirectLimit);
                GUILayout.Space(postBlockContentSpace);
            }

            EditorGUI.EndFoldoutHeaderGroup();

            BuildFoldout.IsActive = AddressablesGUIUtility.BeginFoldoutHeaderGroupWithHelp(BuildFoldout.IsActive, m_BuildHeader, () =>
            {
                string url = AddressableAssetUtility.GenerateDocsURL("AddressableAssetSettings.html#build");
                Application.OpenURL(url);
            });
            if (BuildFoldout.IsActive)
            {
                int index = (int)m_AasTarget.BuildAddressablesWithPlayerBuild;
                int newIndex = EditorGUILayout.Popup(m_BuildAddressablesWithPlayerBuild, index, m_BuildAddressablesWithPlayerBuildOptions);
                if (index != newIndex)
                    m_QueuedChanges.Add(() => m_AasTarget.BuildAddressablesWithPlayerBuild = (AddressableAssetSettings.PlayerBuildOption)newIndex);
                if (newIndex == 0)
                {
                    using (new EditorGUI.DisabledScope(true))
                    {
                        bool enabled = EditorPrefs.GetBool(AddressablesPreferences.kBuildAddressablesWithPlayerBuildKey, true);
                        EditorGUILayout.TextField(" ", enabled ? "Enabled" : "Disabled");
                    }
                }

                bool ignoreUnsupportedFilesInBuild = EditorGUILayout.Toggle(m_IgnoreUnsupportedFilesInBuild, m_AasTarget.IgnoreUnsupportedFilesInBuild);
                if (ignoreUnsupportedFilesInBuild != m_AasTarget.IgnoreUnsupportedFilesInBuild)
                    m_QueuedChanges.Add(() => m_AasTarget.IgnoreUnsupportedFilesInBuild = ignoreUnsupportedFilesInBuild);

                bool uniqueBundleIds = EditorGUILayout.Toggle(m_UniqueBundles, m_AasTarget.UniqueBundleIds);
                if (uniqueBundleIds != m_AasTarget.UniqueBundleIds)
                    m_QueuedChanges.Add(() => m_AasTarget.UniqueBundleIds = uniqueBundleIds);

                bool contiguousBundles = EditorGUILayout.Toggle(m_ContiguousBundles, m_AasTarget.ContiguousBundles);
                if (contiguousBundles != m_AasTarget.ContiguousBundles)
                    m_QueuedChanges.Add(() => m_AasTarget.ContiguousBundles = contiguousBundles);

#if !NONRECURSIVE_DEPENDENCY_DATA
                EditorGUI.BeginDisabledGroup(true);
#endif
                bool nonRecursiveBuilding = EditorGUILayout.Toggle(m_NonRecursiveBundleBuilding, m_AasTarget.NonRecursiveBuilding);
                if (nonRecursiveBuilding != m_AasTarget.NonRecursiveBuilding)
                    m_QueuedChanges.Add(() => m_AasTarget.NonRecursiveBuilding = nonRecursiveBuilding);
#if !NONRECURSIVE_DEPENDENCY_DATA
                EditorGUI.EndDisabledGroup();
#endif
                bool stripUnityVersion = EditorGUILayout.Toggle(m_StripUnityVersionFromBundleBuild, m_AasTarget.StripUnityVersionFromBundleBuild);
                if (stripUnityVersion != m_AasTarget.StripUnityVersionFromBundleBuild)
                    m_QueuedChanges.Add(() => m_AasTarget.StripUnityVersionFromBundleBuild = stripUnityVersion);

                bool disableVisibleSubAssetRepresentations = EditorGUILayout.Toggle(m_DisableVisibleSubAssetRepresentations, m_AasTarget.DisableVisibleSubAssetRepresentations);
                if (disableVisibleSubAssetRepresentations != m_AasTarget.DisableVisibleSubAssetRepresentations)
                    m_QueuedChanges.Add(() => m_AasTarget.DisableVisibleSubAssetRepresentations = disableVisibleSubAssetRepresentations);

                GUILayout.Space(postBlockContentSpace);

                SharedBundleSettings sharedBundleSettings = (SharedBundleSettings)EditorGUILayout.Popup(m_SharedBundleSettings,
                    (int)m_AasTarget.SharedBundleSettings, new[] { "Default Group", "Custom Group" });
                bool useDefaultGroup = sharedBundleSettings == SharedBundleSettings.DefaultGroup;
                if (sharedBundleSettings != m_AasTarget.SharedBundleSettings)
                {
                    // Initially set custom index to default group
                    if (!useDefaultGroup)
                    {
                        for (int i = 0; i < m_AasTarget.groups.Count; i++)
                        {
                            if (m_AasTarget.groups[i].IsDefaultGroup())
                                m_AasTarget.SharedBundleSettingsCustomGroupIndex = i;
                        }
                    }
                    m_QueuedChanges.Add(() => m_AasTarget.SharedBundleSettings = sharedBundleSettings);
                }

                EditorGUILayout.BeginHorizontal();
                GroupsPopupUtility.DrawGroupsDropdown(m_SharedBundleSettingsGroup, m_AasTarget.GetSharedBundleGroup(), !useDefaultGroup, false, true, SetSharedBundleSettingsCustomGroupIndex, null);
                EditorGUILayout.EndHorizontal();

                BuiltInBundleNaming builtInBundleNaming = (BuiltInBundleNaming)EditorGUILayout.Popup(m_BuiltInBundleNaming,
                    (int)m_AasTarget.BuiltInBundleNaming, new[] {"Project Name Hash", "Default Group GUID", "Custom"});
                if (builtInBundleNaming != m_AasTarget.BuiltInBundleNaming)
                    m_QueuedChanges.Add(() => m_AasTarget.BuiltInBundleNaming = builtInBundleNaming);
                if (builtInBundleNaming == BuiltInBundleNaming.Custom)
                {
                    string customShaderBundleName = EditorGUILayout.TextField(m_BuiltInBundleCustomNaming, m_AasTarget.BuiltInBundleCustomNaming);
                    if (customShaderBundleName != m_AasTarget.BuiltInBundleCustomNaming)
                        m_QueuedChanges.Add(() => m_AasTarget.BuiltInBundleCustomNaming = customShaderBundleName);
                }

                MonoScriptBundleNaming monoBundleNaming = (MonoScriptBundleNaming)EditorGUILayout.Popup(m_MonoBundleNaming,
                    (int)m_AasTarget.MonoScriptBundleNaming, new[] {"Project Name Hash", "Default Group GUID", "Custom"});
                if (monoBundleNaming != m_AasTarget.MonoScriptBundleNaming)
                    m_QueuedChanges.Add(() => m_AasTarget.MonoScriptBundleNaming = monoBundleNaming);
                if (monoBundleNaming == MonoScriptBundleNaming.Custom)
                {
                    string customMonoScriptBundleName = EditorGUILayout.TextField(m_MonoBundleCustomNaming, m_AasTarget.MonoScriptBundleCustomNaming);
                    if (customMonoScriptBundleName != m_AasTarget.MonoScriptBundleCustomNaming)
                        m_QueuedChanges.Add(() => m_AasTarget.MonoScriptBundleCustomNaming = customMonoScriptBundleName);
                }
                GUILayout.Space(postBlockContentSpace);

                bool allowNestedBundleFolders = EditorGUILayout.Toggle(m_AllowNestedFolders, m_AasTarget.AllowNestedBundleFolders);
                if (allowNestedBundleFolders != m_AasTarget.AllowNestedBundleFolders)
                    m_QueuedChanges.Add(() => m_AasTarget.AllowNestedBundleFolders = allowNestedBundleFolders);
                GUILayout.Space(postBlockContentSpace);
            }

            EditorGUI.EndFoldoutHeaderGroup();

            DataBuildersFoldout.IsActive = AddressablesGUIUtility.BeginFoldoutHeaderGroupWithHelp(DataBuildersFoldout.IsActive, m_DataBuildersHeader, () =>
            {
                string url = AddressableAssetUtility.GenerateDocsURL("AddressableAssetSettings.html#build-and-play-mode-scripts");
                Application.OpenURL(url);
            });
            if (DataBuildersFoldout.IsActive)
            {
                m_DataBuildersRl.DoLayoutList();
                GUILayout.Space(postBlockContentSpace);
            }

            EditorGUI.EndFoldoutHeaderGroup();

            GroupTemplateObjectsFoldout.IsActive = AddressablesGUIUtility.BeginFoldoutHeaderGroupWithHelp(GroupTemplateObjectsFoldout.IsActive, m_GroupTemplateObjectsHeader, () =>
            {
                string url = AddressableAssetUtility.GenerateDocsURL("AddressableAssetSettings.html#asset-group-templates");
                Application.OpenURL(url);
            });
            if (GroupTemplateObjectsFoldout.IsActive)
            {
                m_GroupTemplateObjectsRl.DoLayoutList();
                GUILayout.Space(postBlockContentSpace);
            }

            EditorGUI.EndFoldoutHeaderGroup();

            InitObjectsFoldout.IsActive = AddressablesGUIUtility.BeginFoldoutHeaderGroupWithHelp(InitObjectsFoldout.IsActive, m_InitObjectsHeader, () =>
            {
                string url = AddressableAssetUtility.GenerateDocsURL("AddressableAssetSettings.html#initialization-object-list");
                Application.OpenURL(url);
            });
            if (InitObjectsFoldout.IsActive)
            {
                m_InitObjectsRl.DoLayoutList();
                GUILayout.Space(postBlockContentSpace);
            }

            EditorGUI.EndFoldoutHeaderGroup();

            CCDEnabledFoldout.IsActive = AddressablesGUIUtility.BeginFoldoutHeaderGroupWithHelp(CCDEnabledFoldout.IsActive, m_CCDEnabledHeader, () =>
            {
                string url = AddressableAssetUtility.GenerateDocsURL("AddressablesCCD.html");
                Application.OpenURL(url);
            });
            if (CCDEnabledFoldout.IsActive)
            {
                var toggle = EditorGUILayout.Toggle(m_CCDEnabled, m_AasTarget.CCDEnabled);
                if (toggle != m_AasTarget.CCDEnabled)
                {
                    if (toggle)
                    {
                        toggle = AddressableAssetUtility.InstallCCDPackage();
                    }
                    else
                    {
                        toggle = AddressableAssetUtility.RemoveCCDPackage();
                    }

                    m_QueuedChanges.Add(() => m_AasTarget.CCDEnabled = toggle);
                }
#if CCD_REQUEST_LOGGING
                if (m_AasTarget.CCDEnabled)
                {
                    var loggingToggle = EditorGUILayout.Toggle(m_CCDLogRequests, m_AasTarget.CCDLogRequests);
                    if (loggingToggle != m_AasTarget.CCDLogRequests)
                    {
                        m_QueuedChanges.Add(() => m_AasTarget.CCDLogRequests = loggingToggle);
                        // we want to disable request header logging if we disable logging requests
                        m_QueuedChanges.Add(() => m_AasTarget.CCDLogRequestHeaders = loggingToggle && m_AasTarget.CCDLogRequestHeaders);
                    }

                    if (m_AasTarget.CCDLogRequests)
                    {
                        var headerLoggingToggle = EditorGUILayout.Toggle(m_CCDLogRequestHeaders, m_AasTarget.CCDLogRequestHeaders);
                        if (headerLoggingToggle != m_AasTarget.CCDLogRequestHeaders)
                        {
                            m_QueuedChanges.Add(() => m_AasTarget.CCDLogRequestHeaders = headerLoggingToggle);
                        }
                    }
                }
#endif

                GUILayout.Space(postBlockContentSpace);
            }

            EditorGUI.EndFoldoutHeaderGroup();

            if (EditorGUI.EndChangeCheck() || m_QueuedChanges.Count > 0)
            {
                Undo.RecordObject(m_AasTarget, "AddressableAssetSettings before changes");
                foreach (var change in m_QueuedChanges)
                {
                    change.Invoke();
                }

                m_AasTarget.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
                serializedObject.ApplyModifiedProperties();
            }
        }

        private List<NamedBuildTarget> buildTargets = new List<NamedBuildTarget>()
        {
          NamedBuildTarget.Android,
          NamedBuildTarget.EmbeddedLinux,
          NamedBuildTarget.iOS,
          NamedBuildTarget.LinuxHeadlessSimulation,
          NamedBuildTarget.NintendoSwitch,
          NamedBuildTarget.PS4,
          NamedBuildTarget.QNX,
          NamedBuildTarget.Server,
          NamedBuildTarget.Standalone,
          NamedBuildTarget.tvOS,
          NamedBuildTarget.WebGL,
          NamedBuildTarget.WindowsStoreApps,
          NamedBuildTarget.XboxOne
        };

        void ToggleJsonCatalog(AddressableAssetSettings settings, bool enableJsonCatalog)
        {
            settings.EnableJsonCatalog = enableJsonCatalog;
            foreach (var buildTarget in buildTargets)
                AddressableAssetSettings.UpdateSymbolsForBuildTarget(buildTarget, enableJsonCatalog);
        }

        internal static void SetSharedBundleSettingsCustomGroupIndex(AddressableAssetSettings settings, List<AddressableAssetEntry> entries, AddressableAssetGroup group)
        {
            for (int i = 0; i < settings.groups.Count; i++)
            {
                if (settings.groups[i].Guid == group.Guid)
                    settings.SharedBundleSettingsCustomGroupIndex = i;
            }
        }

        void DrawProfileEntriesHeader(Rect rect)
        {
            EditorGUI.LabelField(rect, "Profile Entries");
        }

        void DrawProfileEntriesCallback(Rect rect, int index, bool isActive, bool isFocused)
        {
            float halfW = rect.width * 0.4f;
            var currentEntry = m_AasTarget.profileSettings.profileEntryNames[index];
            var newName = EditorGUI.DelayedTextField(new Rect(rect.x, rect.y, halfW, rect.height), currentEntry.ProfileName);
            if (newName != currentEntry.ProfileName)
                currentEntry.SetName(newName, m_AasTarget.profileSettings);

            var currProfile = m_AasTarget.profileSettings.profiles[m_CurrentProfileIndex];
            var oldValue = m_AasTarget.profileSettings.GetValueById(currProfile.id, currentEntry.Id);
            var newValue = EditorGUI.TextField(new Rect(rect.x + halfW, rect.y, rect.width - halfW, rect.height), oldValue);
            if (oldValue != newValue)
            {
                m_AasTarget.profileSettings.SetValue(currProfile.id, currentEntry.ProfileName, newValue);
            }
        }

        void OnAddProfileEntry(ReorderableList list)
        {
            var uniqueProfileEntryName = m_AasTarget.profileSettings.GetUniqueProfileEntryName("New Entry");
            if (!string.IsNullOrEmpty(uniqueProfileEntryName))
                m_AasTarget.profileSettings.CreateValue(uniqueProfileEntryName, "");
        }

        void OnRemoveProfileEntry(ReorderableList list)
        {
            if (list.index >= 0 && list.index < m_AasTarget.profileSettings.profileEntryNames.Count)
            {
                var entry = m_AasTarget.profileSettings.profileEntryNames[list.index];
                if (entry != null)
                    m_AasTarget.profileSettings.RemoveValue(entry.Id);
            }
        }

        void DrawDataBuilderCallback(Rect rect, int index, bool isActive, bool isFocused)
        {
            var so = m_AasTarget.DataBuilders[index];
            var builder = so as IDataBuilder;
            var label = builder == null ? "" : builder.Name;
            var nb = EditorGUI.ObjectField(rect, label, so, typeof(ScriptableObject), false) as ScriptableObject;
            if (nb != so)
                m_AasTarget.SetDataBuilderAtIndex(index, nb as IDataBuilder);
        }

        void OnRemoveDataBuilder(ReorderableList list)
        {
            m_AasTarget.RemoveDataBuilder(list.index);
        }

        void OnAddDataBuilder(Rect buttonRect, ReorderableList list)
        {
            var assetPath = EditorUtility.OpenFilePanelWithFilters("Data Builder", "Assets", new[] {"Data Builder", "asset"});
            if (string.IsNullOrEmpty(assetPath))
                return;
            var builder = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath.Substring(assetPath.IndexOf("Assets/", StringComparison.Ordinal)));
            if (!typeof(IDataBuilder).IsAssignableFrom(builder.GetType()))
            {
                Debug.LogWarningFormat("Asset at {0} does not implement the IDataBuilder interface.", assetPath);
                return;
            }

            m_AasTarget.AddDataBuilder(builder as IDataBuilder);
        }

        void DrawGroupTemplateObjectCallback(Rect rect, int index, bool isActive, bool isFocused)
        {
            var so = m_AasTarget.GroupTemplateObjects[index];
            var groupTObj = so as IGroupTemplate;
            ScriptableObject newObj = null;
            if (groupTObj == null)
            {
                newObj = EditorGUI.ObjectField(rect, "Missing", null, typeof(ScriptableObject), false) as ScriptableObject;
            }
            else
            {
                newObj = EditorGUI.ObjectField(rect, groupTObj.Name, so, typeof(ScriptableObject), false) as ScriptableObject;
            }

            if (newObj != so)
                m_AasTarget.SetGroupTemplateObjectAtIndex(index, newObj as IGroupTemplate);
        }

        void OnRemoveGroupTemplateObject(ReorderableList list)
        {
            m_AasTarget.RemoveGroupTemplateObject(list.index);
        }

        void OnAddGroupTemplateObject(Rect buttonRect, ReorderableList list)
        {
            var assetPath = EditorUtility.OpenFilePanelWithFilters("Assets Group Templates", "Assets", new[] {"Group Template Object", "asset"});
            if (string.IsNullOrEmpty(assetPath))
                return;
            if (assetPath.StartsWith(Application.dataPath, StringComparison.Ordinal) == false)
            {
                Debug.LogWarningFormat("Path at {0} is not an Asset of this project.", assetPath);
                return;
            }

            string relativePath = assetPath.Remove(0, Application.dataPath.Length - 6);
            var templateObj = AssetDatabase.LoadAssetAtPath<ScriptableObject>(relativePath);
            if (templateObj == null)
            {
                Debug.LogWarningFormat("Failed to load Asset at {0}.", assetPath);
                return;
            }

            if (!typeof(IGroupTemplate).IsAssignableFrom(templateObj.GetType()))
            {
                Debug.LogWarningFormat("Asset at {0} does not implement the IGroupTemplate interface.", assetPath);
                return;
            }

            m_AasTarget.AddGroupTemplateObject(templateObj as IGroupTemplate);
        }

        void DrawInitializationObjectCallback(Rect rect, int index, bool isActive, bool isFocused)
        {
            var so = m_AasTarget.InitializationObjects[index];
            var initObj = so as IObjectInitializationDataProvider;
            var label = initObj == null ? "" : initObj.Name;
            var nb = EditorGUI.ObjectField(rect, label, so, typeof(ScriptableObject), false) as ScriptableObject;
            if (nb != so)
                m_AasTarget.SetInitializationObjectAtIndex(index, nb as IObjectInitializationDataProvider);
        }

        void OnRemoveInitializationObject(ReorderableList list)
        {
            m_AasTarget.RemoveInitializationObject(list.index);
        }

        void OnAddInitializationObject(Rect buttonRect, ReorderableList list)
        {
            var assetPath = EditorUtility.OpenFilePanelWithFilters("Initialization Object", "Assets", new[] {"Initialization Object", "asset"});
            if (string.IsNullOrEmpty(assetPath))
                return;
            var initObj = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath.Substring(assetPath.IndexOf("Assets/", StringComparison.Ordinal)));
            if (!typeof(IObjectInitializationDataProvider).IsAssignableFrom(initObj.GetType()))
            {
                Debug.LogWarningFormat("Asset at {0} does not implement the IObjectInitializationDataProvider interface.", assetPath);
                return;
            }

            m_AasTarget.AddInitializationObject(initObj as IObjectInitializationDataProvider);
        }

        void DrawRemoteCatalogPaths()
        {
            ProfileValueReference BuildPath = m_AasTarget.RemoteCatalogBuildPath;
            ProfileValueReference LoadPath = m_AasTarget.RemoteCatalogLoadPath;

            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
                return;
            List<ProfileGroupType> groupTypes = ProfileGroupType.CreateGroupTypes(settings.profileSettings.GetProfile(settings.activeProfileId), settings);
            List<string> options = groupTypes.Select(group => group.GroupTypePrefix).ToList();
            //set selected to custom
            options.Add(AddressableAssetProfileSettings.customEntryString);
            int? selected = options.Count - 1;
            HashSet<string> vars = settings.profileSettings.GetAllVariableIds();
            if (vars.Contains(BuildPath.Id) && vars.Contains(LoadPath.Id) && !m_UseCustomPaths)
            {
                for (int i = 0; i < groupTypes.Count; i++)
                {
                    ProfileGroupType.GroupTypeVariable buildPathVar = groupTypes[i].GetVariableBySuffix("BuildPath");
                    ProfileGroupType.GroupTypeVariable loadPathVar = groupTypes[i].GetVariableBySuffix("LoadPath");
                    if (BuildPath.GetName(settings) == groupTypes[i].GetName(buildPathVar) && LoadPath.GetName(settings) == groupTypes[i].GetName(loadPathVar))
                    {
                        selected = i;
                        break;
                    }
                }
            }

            if (selected.HasValue && selected != options.Count - 1)
            {
                m_UseCustomPaths = false;
            }
            else
            {
                m_UseCustomPaths = true;
            }

            EditorGUI.BeginChangeCheck();
            var newIndex = EditorGUILayout.Popup(m_RemoteCatBuildandLoadPaths, selected.HasValue ? selected.Value : options.Count - 1, options.ToArray());
            if (EditorGUI.EndChangeCheck() && newIndex != selected)
            {
                if (options[newIndex] != AddressableAssetProfileSettings.customEntryString)
                {
                    Undo.RecordObject(serializedObject.targetObject, serializedObject.targetObject.name + "Path Pair");
                    BuildPath.SetVariableByName(settings, groupTypes[newIndex].GroupTypePrefix + ProfileGroupType.k_PrefixSeparator + "BuildPath");
                    LoadPath.SetVariableByName(settings, groupTypes[newIndex].GroupTypePrefix + ProfileGroupType.k_PrefixSeparator + "LoadPath");
                    m_UseCustomPaths = false;
                }
                else
                {
                    Undo.RecordObject(serializedObject.targetObject, serializedObject.targetObject.name + "Path Pair");
                    m_UseCustomPaths = true;
                }

                EditorUtility.SetDirty(this);
            }

            if (m_UseCustomPaths)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_RemoteCatalogBuildPath"), m_RemoteCatBuildPath);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_RemoteCatalogLoadPath"), m_RemoteCatLoadPath);
            }

            EditorGUI.indentLevel++;
            m_ShowPaths = EditorGUILayout.Foldout(m_ShowPaths, m_RemoteCatPathPreview, true);
            if (m_ShowPaths)
            {
                EditorStyles.helpBox.fontSize = 12;
                var baseBuildPathValue = BuildPath.GetValue(settings, false);
                var baseLoadPathValue = LoadPath.GetValue(settings, false);
                EditorGUILayout.HelpBox(String.Format("Build Path: {0}", settings.profileSettings.EvaluateString(settings.activeProfileId, baseBuildPathValue)), MessageType.None);
                EditorGUILayout.HelpBox(String.Format("Load Path: {0}", settings.profileSettings.EvaluateString(settings.activeProfileId, baseLoadPathValue)), MessageType.None);
            }

            EditorGUI.indentLevel--;
        }
    }
}
