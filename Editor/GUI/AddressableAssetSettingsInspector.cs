using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.ResourceManagement.Util;
using UnityEngine.Serialization;

namespace UnityEditor.AddressableAssets.GUI
{
    [CustomEditor(typeof(AddressableAssetSettings))]
    class AddressableAssetSettingsInspector : Editor
    {
        AddressableAssetSettings m_AasTarget;

        [FormerlySerializedAs("m_profilesFoldout")]
        [SerializeField]
        bool m_ProfilesFoldout = true;
        [SerializeField]
        bool m_DiagnosticsFoldout = true;
        [SerializeField]
        bool m_CatalogFoldout = true;
        [SerializeField]
        bool m_ContentUpdateFoldout = true;
        [SerializeField]
        bool m_DownloadsFoldout = true;
        [SerializeField]
        bool m_BuildFoldout = true;
        [FormerlySerializedAs("m_dataBuildersFoldout")]
        [SerializeField]
        bool m_DataBuildersFoldout = true;
        [SerializeField]
        bool m_GroupTemplateObjectsFoldout = true;
        [FormerlySerializedAs("m_initObjectsFoldout")]
        [SerializeField]
        bool m_InitObjectsFoldout = true;

#if UNITY_2019_4_OR_NEWER
        [SerializeField]
        bool m_CCDEnabledFoldout = true;
#endif 

        //Used for displaying path pairs
        bool m_UseCustomPaths = false;
        bool m_ShowPaths = true;

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
        }

        GUIContent m_SendProfilerEvents =
            new GUIContent("Send Profiler Events",
                "Turning this on enables the use of the Addressables Profiler window.");
        GUIContent m_LogRuntimeExceptions =
            new GUIContent("Log Runtime Exceptions",
                "Addressables does not throw exceptions at run time when there are loading issues, instead it adds to the error state of the IAsyncOperation.  With this flag enabled, exceptions will also be logged.");
        GUIContent m_OverridePlayerVersion =
            new GUIContent("Player Version Override", "If set, this will be used as the player version instead of one based off of a time stamp.");
        GUIContent m_UniqueBundles =
            new GUIContent("Unique Bundle IDs", "If set, every content build (original or update) will result in asset bundles with more complex internal names.  This may result in more bundles being rebuilt, but safer mid-run updates.  See docs for more info.");
        GUIContent m_ContiguousBundles =
            new GUIContent("Contiguous Bundles", "If set, packs assets in bundles contiguously based on the ordering of the source asset which results in improved asset loading times. Disable this if you've built bundles with a version of Addressables older than 1.12.1 and you want to minimize bundle changes.");
#if NONRECURSIVE_DEPENDENCY_DATA
        GUIContent m_NonRecursiveBundleBuilding =
            new GUIContent("Non-Recursive Dependency Calculation", "If set, Calculates and build asset bundles using Non-Recursive Dependency calculation methods. This approach helps reduce asset bundle rebuilds and runtime memory consumption.");
#else
        GUIContent m_NonRecursiveBundleBuilding =
            new GUIContent("Non-Recursive Dependency Calculation", "If set, Calculates and build asset bundles using Non-Recursive Dependency calculation methods. This approach helps reduce asset bundle rebuilds and runtime memory consumption.\n*Requires Unity 2019.4.19f1 or above");
#endif
        GUIContent m_BuildRemoteCatalog =
            new GUIContent("Build Remote Catalog", "If set, this will create a copy of the content catalog for storage on a remote server.  This catalog can be overwritten later for content updates.");
        GUIContent m_BundleLocalCatalog =
            new GUIContent("Compress Local Catalog", "If set, the local content catalog will be compressed in an asset bundle. This will affect build and load time of catalog. We recommend disabling this during iteration.");
        GUIContent m_OptimizeCatalogSize =
            new GUIContent("Optimize Catalog Size", "If set, duplicate internal ids will be extracted to a lookup table and reconstructed at runtime.  This can reduce the size of the catalog but may impact performance due to extra processing at load time.");
        GUIContent m_CheckForCatalogUpdateOnInit =
            new GUIContent("Disable Catalog Update on Startup", "If set, this will forgo checking for content catalog updates on initialization.");
        GUIContent m_RemoteCatBuildPath =
            new GUIContent("Build Path", "The path for a remote content catalog.");
        GUIContent m_RemoteCatLoadPath =
            new GUIContent("Load Path", "The path to load a remote content catalog.");
        GUIContent m_CatalogTimeout =
            new GUIContent("Catalog Download Timeout", "The time until a catalog hash or json UnityWebRequest download will timeout in seconds. 0 for no timeout.");
        GUIContent m_CertificateHandlerType =
            new GUIContent("Custom certificate handler", "The class to use for custom certificate handling.  This type must inherit from UnityEngine.Networking.CertificateHandler.");
        GUIContent m_ProfileInUse =
            new GUIContent("Profile In Use", "This is the active profile that will be used to evaluate all profile variables during a build and when entering play mode.");
        GUIContent m_MaxConcurrentWebRequests =
            new GUIContent("Max Concurrent Web Requests", "Limits the number of concurrent web requests.  If more requests are made, they will be queued until some requests complete.");
        GUIContent m_IgnoreUnsupportedFilesInBuild =
            new GUIContent("Ignore Invalid/Unsupported Files in Build", "If enabled, files that cannot be built will be ignored.");
        GUIContent m_ContentStateFileBuildPath =
            new GUIContent("Content State Build Path", "The path used for saving the addressables_content_state.bin file. If empty, this will be the addressable settings config folder in your project.");
        GUIContent m_ShaderBundleNaming =
            new GUIContent("Shader Bundle Naming Prefix", "This setting determines how the Unity built in shader bundle will be named during the build.  The recommended setting is Project Name Hash.");
        GUIContent m_ShaderBundleCustomNaming =
            new GUIContent("Shader Bundle Custom Prefix", "Custom prefix for Unity built in shader bundle.");
        GUIContent m_MonoBundleNaming =
            new GUIContent("MonoScript Bundle Naming Prefix", "This setting determines how and if the MonoScript bundle will be named during the build.  The recommended setting is Project Name Hash.");
        GUIContent m_MonoBundleCustomNaming =
            new GUIContent("MonoScript Bundle Custom Prefix", "Custom prefix for MonoScript bundle.");
        GUIContent m_StripUnityVersionFromBundleBuild =
            new GUIContent("Strip Unity Version from AssetBundles", "If enabled, the Unity Editor version is stripped from the AssetBundle header.");
        GUIContent m_DisableVisibleSubAssetRepresentations =
            new GUIContent("Disable Visible Sub Asset Representations", "If enabled, the build will assume that all sub Assets have no visible asset representations.");
#if UNITY_2019_4_OR_NEWER
        GUIContent m_CCDEnabled = new GUIContent("Enable Experimental CCD Features", "If enabled, will unlock experimental CCD features");
#endif
#if UNITY_2021_2_OR_NEWER
        GUIContent m_BuildAddressablesWithPlayerBuild =
            new GUIContent("Build Addressables on Player Build", "Determines if a new Addressables build will be built with a Player Build.");
#endif
        
        public override void OnInspectorGUI()
        {
            m_QueuedChanges.Clear();
            serializedObject.UpdateIfRequiredOrScript(); // use updated values
            EditorGUI.BeginChangeCheck();

            GUILayout.Space(8);
            if (GUILayout.Button("Manage Groups", "Minibutton", GUILayout.ExpandWidth(true)))
            {
                AddressableAssetsWindow.Init();
            }

            GUILayout.Space(12);
            m_ProfilesFoldout = EditorGUILayout.Foldout(m_ProfilesFoldout, "Profile");
            if (m_ProfilesFoldout)
            {
                if (m_AasTarget.profileSettings.profiles.Count > 0)
                {
                    if (m_CurrentProfileIndex < 0 || m_CurrentProfileIndex >= m_AasTarget.profileSettings.profiles.Count)
                        m_CurrentProfileIndex = 0;
                    var profileNames = m_AasTarget.profileSettings.GetAllProfileNames();

                    int currentProfileIndex = m_CurrentProfileIndex;
                    // Current profile in use was changed by different window
                    if (AddressableAssetSettingsDefaultObject.Settings.profileSettings.profiles[m_CurrentProfileIndex].id != AddressableAssetSettingsDefaultObject.Settings.activeProfileId)
                    {
                        currentProfileIndex = profileNames.IndexOf(AddressableAssetSettingsDefaultObject.Settings.profileSettings.GetProfileName(AddressableAssetSettingsDefaultObject.Settings.activeProfileId));
                        if (currentProfileIndex != m_CurrentProfileIndex)
                            m_QueuedChanges.Add(() => m_CurrentProfileIndex = currentProfileIndex);
                    }
                    currentProfileIndex = EditorGUILayout.Popup(m_ProfileInUse, currentProfileIndex, profileNames.ToArray());
                    if (currentProfileIndex != m_CurrentProfileIndex)
                        m_QueuedChanges.Add(() => m_CurrentProfileIndex = currentProfileIndex);

                    AddressableAssetSettingsDefaultObject.Settings.activeProfileId = AddressableAssetSettingsDefaultObject.Settings.profileSettings.GetProfileId(profileNames[currentProfileIndex]);

                    EditorGUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Manage Profiles", "Minibutton"))
                    {
                        EditorWindow.GetWindow<ProfileWindow>().Show(true);
                    }
                    EditorGUILayout.EndHorizontal();
                }
                else
                {
                    EditorGUILayout.LabelField("No valid profiles found");
                }
            }

            GUILayout.Space(6);
            m_DiagnosticsFoldout = EditorGUILayout.Foldout(m_DiagnosticsFoldout, "Diagnostics");
            if (m_DiagnosticsFoldout)
            {
                ProjectConfigData.PostProfilerEvents = EditorGUILayout.Toggle(m_SendProfilerEvents, ProjectConfigData.PostProfilerEvents);
                bool logResourceManagerExceptions = EditorGUILayout.Toggle(m_LogRuntimeExceptions, m_AasTarget.buildSettings.LogResourceManagerExceptions);

                if (logResourceManagerExceptions != m_AasTarget.buildSettings.LogResourceManagerExceptions)
                    m_QueuedChanges.Add(() => m_AasTarget.buildSettings.LogResourceManagerExceptions = logResourceManagerExceptions);
            }

            GUILayout.Space(6);
            m_CatalogFoldout = EditorGUILayout.Foldout(m_CatalogFoldout, "Catalog");
            if (m_CatalogFoldout)
            {
                string overridePlayerVersion = EditorGUILayout.TextField(m_OverridePlayerVersion, m_AasTarget.OverridePlayerVersion);
                if (overridePlayerVersion != m_AasTarget.OverridePlayerVersion)
                    m_QueuedChanges.Add(() => m_AasTarget.OverridePlayerVersion = overridePlayerVersion);

                bool bundleLocalCatalog = EditorGUILayout.Toggle(m_BundleLocalCatalog, m_AasTarget.BundleLocalCatalog);
                if (bundleLocalCatalog != m_AasTarget.BundleLocalCatalog)
                    m_QueuedChanges.Add(() => m_AasTarget.BundleLocalCatalog = bundleLocalCatalog);

                bool optimizeCatalogSize = EditorGUILayout.Toggle(m_OptimizeCatalogSize, m_AasTarget.OptimizeCatalogSize);
                if (optimizeCatalogSize != m_AasTarget.OptimizeCatalogSize)
                    m_QueuedChanges.Add(() => m_AasTarget.OptimizeCatalogSize = optimizeCatalogSize);
            }

            GUILayout.Space(6);
            m_ContentUpdateFoldout = EditorGUILayout.Foldout(m_ContentUpdateFoldout, "Content Update");
            if (m_ContentUpdateFoldout)
            {
                bool disableCatalogOnStartup = EditorGUILayout.Toggle(m_CheckForCatalogUpdateOnInit, m_AasTarget.DisableCatalogUpdateOnStartup);
                if (disableCatalogOnStartup != m_AasTarget.DisableCatalogUpdateOnStartup)
                    m_QueuedChanges.Add(() => m_AasTarget.DisableCatalogUpdateOnStartup = disableCatalogOnStartup);

                string contentStateBuildPath = EditorGUILayout.TextField(m_ContentStateFileBuildPath, m_AasTarget.ContentStateBuildPath);
                if (contentStateBuildPath != m_AasTarget.ContentStateBuildPath)
                    m_QueuedChanges.Add(() => m_AasTarget.ContentStateBuildPath = contentStateBuildPath);

                bool buildRemoteCatalog = EditorGUILayout.Toggle(m_BuildRemoteCatalog, m_AasTarget.BuildRemoteCatalog);
                if (buildRemoteCatalog != m_AasTarget.BuildRemoteCatalog)
                    m_QueuedChanges.Add(() => m_AasTarget.BuildRemoteCatalog = buildRemoteCatalog);
                if ((m_AasTarget.RemoteCatalogBuildPath != null && m_AasTarget.RemoteCatalogLoadPath != null) // these will never actually be null, as the accessor initializes them.
                    && (buildRemoteCatalog))
                {
                    DrawRemoteCatalogPaths();
                }
            }

            GUILayout.Space(6);
            m_DownloadsFoldout = EditorGUILayout.Foldout(m_DownloadsFoldout, "Downloads");
            if (m_DownloadsFoldout)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_CertificateHandlerType"), m_CertificateHandlerType);

                var maxWebReqs = EditorGUILayout.IntSlider(m_MaxConcurrentWebRequests, m_AasTarget.MaxConcurrentWebRequests, 1, 1024);
                if (maxWebReqs != m_AasTarget.MaxConcurrentWebRequests)
                    m_QueuedChanges.Add(() => m_AasTarget.MaxConcurrentWebRequests = maxWebReqs);

                var catalogTimeouts = EditorGUILayout.IntField(m_CatalogTimeout, m_AasTarget.CatalogRequestsTimeout);
                if (catalogTimeouts != m_AasTarget.CatalogRequestsTimeout)
                    m_QueuedChanges.Add(() => m_AasTarget.CatalogRequestsTimeout = catalogTimeouts);
            }

            GUILayout.Space(6);
            m_BuildFoldout = EditorGUILayout.Foldout(m_BuildFoldout, "Build");
            if (m_BuildFoldout)
            {
#if UNITY_2021_2_OR_NEWER
                int index = (int) m_AasTarget.BuildAddressablesWithPlayerBuild;
                int newIndex = EditorGUILayout.Popup(m_BuildAddressablesWithPlayerBuild, index, new[]
                {
                    "Use global Settings (stored in preferences)",
                    "Build Addressables content on Player Build",
                    "Do not Build Addressables content on Player build"
                });
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
#endif
                
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

                ShaderBundleNaming shaderBundleNaming = (ShaderBundleNaming)EditorGUILayout.Popup(m_ShaderBundleNaming,
                    (int)m_AasTarget.ShaderBundleNaming, new[] { "Project Name Hash", "Default Group GUID", "Custom" });
                if (shaderBundleNaming != m_AasTarget.ShaderBundleNaming)
                    m_QueuedChanges.Add(() => m_AasTarget.ShaderBundleNaming = shaderBundleNaming);
                if (shaderBundleNaming == ShaderBundleNaming.Custom)
                {
                    string customShaderBundleName = EditorGUILayout.TextField(m_ShaderBundleCustomNaming, m_AasTarget.ShaderBundleCustomNaming);
                    if (customShaderBundleName != m_AasTarget.ShaderBundleCustomNaming)
                        m_QueuedChanges.Add(() => m_AasTarget.ShaderBundleCustomNaming = customShaderBundleName);
                }

                MonoScriptBundleNaming monoBundleNaming = (MonoScriptBundleNaming)EditorGUILayout.Popup(m_MonoBundleNaming,
                    (int)m_AasTarget.MonoScriptBundleNaming, new[] { "Disable MonoScript Bundle Build", "Project Name Hash", "Default Group GUID", "Custom" });
                if (monoBundleNaming != m_AasTarget.MonoScriptBundleNaming)
                    m_QueuedChanges.Add(() => m_AasTarget.MonoScriptBundleNaming = monoBundleNaming);
                if (monoBundleNaming == MonoScriptBundleNaming.Custom)
                {
                    string customMonoScriptBundleName = EditorGUILayout.TextField(m_MonoBundleCustomNaming, m_AasTarget.MonoScriptBundleCustomNaming);
                    if (customMonoScriptBundleName != m_AasTarget.MonoScriptBundleCustomNaming)
                        m_QueuedChanges.Add(() => m_AasTarget.MonoScriptBundleCustomNaming = customMonoScriptBundleName);
                }

                bool stripUnityVersion = EditorGUILayout.Toggle(m_StripUnityVersionFromBundleBuild, m_AasTarget.StripUnityVersionFromBundleBuild);
                if (stripUnityVersion != m_AasTarget.StripUnityVersionFromBundleBuild)
                    m_QueuedChanges.Add(() => m_AasTarget.StripUnityVersionFromBundleBuild = stripUnityVersion);

                bool disableVisibleSubAssetRepresentations = EditorGUILayout.Toggle(m_DisableVisibleSubAssetRepresentations, m_AasTarget.DisableVisibleSubAssetRepresentations);
                if (disableVisibleSubAssetRepresentations != m_AasTarget.DisableVisibleSubAssetRepresentations)
                    m_QueuedChanges.Add(() => m_AasTarget.DisableVisibleSubAssetRepresentations = disableVisibleSubAssetRepresentations);
            }

            GUILayout.Space(6);
            m_DataBuildersFoldout = EditorGUILayout.Foldout(m_DataBuildersFoldout, "Build and Play Mode Scripts");
            if (m_DataBuildersFoldout)
                m_DataBuildersRl.DoLayoutList();

            GUILayout.Space(6);
            m_GroupTemplateObjectsFoldout = EditorGUILayout.Foldout(m_GroupTemplateObjectsFoldout, "Asset Group Templates");
            if (m_GroupTemplateObjectsFoldout)
                m_GroupTemplateObjectsRl.DoLayoutList();

            GUILayout.Space(6);
            m_InitObjectsFoldout = EditorGUILayout.Foldout(m_InitObjectsFoldout, "Initialization Objects");
            if (m_InitObjectsFoldout)
                m_InitObjectsRl.DoLayoutList();

#if UNITY_2019_4_OR_NEWER
            GUILayout.Space(6);
            m_CCDEnabledFoldout = EditorGUILayout.Foldout(m_CCDEnabledFoldout, "Cloud Content Delivery");
            if (m_CCDEnabledFoldout)
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
            }
#endif

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
            var assetPath = EditorUtility.OpenFilePanelWithFilters("Data Builder", "Assets", new[] { "Data Builder", "asset" });
            if (string.IsNullOrEmpty(assetPath))
                return;
            var builder = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath.Substring(assetPath.IndexOf("Assets/")));
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
            var assetPath = EditorUtility.OpenFilePanelWithFilters("Assets Group Templates", "Assets", new[] { "Group Template Object", "asset" });
            if (string.IsNullOrEmpty(assetPath))
                return;
            if (assetPath.StartsWith(Application.dataPath) == false)
            {
                Debug.LogWarningFormat("Path at {0} is not an Asset of this project.", assetPath);
                return;
            }
            
            string relativePath = assetPath.Remove(0, Application.dataPath.Length-7);
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
            var assetPath = EditorUtility.OpenFilePanelWithFilters("Initialization Object", "Assets", new[] { "Initialization Object", "asset" });
            if (string.IsNullOrEmpty(assetPath))
                return;
            var initObj = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath.Substring(assetPath.IndexOf("Assets/")));
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
            if (settings == null) return;
            List<ProfileGroupType> groupTypes = ProfileGroupType.CreateGroupTypes(settings.profileSettings.GetProfile(settings.activeProfileId));
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
            var newIndex = EditorGUILayout.Popup("Build & Load Paths", selected.HasValue ? selected.Value : options.Count - 1, options.ToArray());
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
            m_ShowPaths = EditorGUILayout.Foldout(m_ShowPaths, "Path Preview", true);
            if (m_ShowPaths)
            {
                EditorStyles.helpBox.fontSize = 12;
                var baseBuildPathValue = settings.profileSettings.GetValueById(settings.activeProfileId, BuildPath.Id);
                var baseLoadPathValue = settings.profileSettings.GetValueById(settings.activeProfileId, LoadPath.Id);
                EditorGUILayout.HelpBox(String.Format("Build Path: {0}", settings.profileSettings.EvaluateString(settings.activeProfileId, baseBuildPathValue)), MessageType.None);
                EditorGUILayout.HelpBox(String.Format("Load Path: {0}", settings.profileSettings.EvaluateString(settings.activeProfileId, baseLoadPathValue)), MessageType.None);
            }
            EditorGUI.indentLevel--;
        }

    }
}
