using System;
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

        [FormerlySerializedAs("m_generalFoldout")]
        [SerializeField]
        bool m_GeneralFoldout = true;
        [FormerlySerializedAs("m_profilesFoldout")]
        [SerializeField]
        bool m_CatalogFoldout = true;
        [SerializeField]
        bool m_ProfilesFoldout = true;
        [FormerlySerializedAs("m_dataBuildersFoldout")]
        [SerializeField]
        bool m_DataBuildersFoldout = true;
        [SerializeField]
        bool m_GroupTemplateObjectsFoldout = true;
        [FormerlySerializedAs("m_initObjectsFoldout")]
        [SerializeField]
        bool m_InitObjectsFoldout = true;

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
        GUIContent m_BuildRemoteCatalog =
            new GUIContent("Build Remote Catalog", "If set, this will create a copy of the content catalog for storage on a remote server.  This catalog can be overwritten later for content updates.");
        GUIContent m_BundleLocalCatalog =
            new GUIContent("Compress Local Catalog", "If set, the local content catalog will be compressed in an asset bundle. This will affect build and load time of catalog. We recommend disabling this during iteration.");
        GUIContent m_CheckForCatalogUpdateOnInit =
            new GUIContent("Disable Catalog Update on Startup", "If set, this will forgo checking for content catalog updates on initialization.");
        GUIContent m_RemoteCatBuildPath =
            new GUIContent("Build Path", "The path for a remote content catalog.");
        GUIContent m_RemoteCatLoadPath =
            new GUIContent("Load Path", "The path to load a remote content catalog.");
        GUIContent m_CertificateHandlerType =
             new GUIContent("Custom certificate handler", "The class to use for custom certificate handling.  This type must inherit from UnityEngine.Networking.CertificateHandler.");
        GUIContent m_ProfileInUse = 
            new GUIContent("Profile In Use", "This is the active profile that will be used to evaluate all profile variables during a build and when entering play mode.");

        public override void OnInspectorGUI()
        {
            serializedObject.UpdateIfRequiredOrScript(); // use updated values

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
                   
                    // Current profile in use was changed by different window
                    if (AddressableAssetSettingsDefaultObject.Settings.profileSettings.profiles[m_CurrentProfileIndex].id != AddressableAssetSettingsDefaultObject.Settings.activeProfileId)
                    {
                        m_CurrentProfileIndex = profileNames.IndexOf(AddressableAssetSettingsDefaultObject.Settings.profileSettings.GetProfileName(AddressableAssetSettingsDefaultObject.Settings.activeProfileId));
                    }
                    
                    m_CurrentProfileIndex = EditorGUILayout.Popup(m_ProfileInUse, m_CurrentProfileIndex, profileNames.ToArray());
                    AddressableAssetSettingsDefaultObject.Settings.activeProfileId = AddressableAssetSettingsDefaultObject.Settings.profileSettings.GetProfileId(profileNames[m_CurrentProfileIndex]);
                    
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
            m_CatalogFoldout = EditorGUILayout.Foldout(m_CatalogFoldout, "Catalog");
            if (m_CatalogFoldout)
            {
                m_AasTarget.DisableCatalogUpdateOnStartup = EditorGUILayout.Toggle(m_CheckForCatalogUpdateOnInit, m_AasTarget.DisableCatalogUpdateOnStartup);
                m_AasTarget.OverridePlayerVersion = EditorGUILayout.TextField(m_OverridePlayerVersion, m_AasTarget.OverridePlayerVersion);
                m_AasTarget.BuildRemoteCatalog = EditorGUILayout.Toggle(m_BuildRemoteCatalog, m_AasTarget.BuildRemoteCatalog);
                m_AasTarget.BundleLocalCatalog = EditorGUILayout.Toggle(m_BundleLocalCatalog, m_AasTarget.BundleLocalCatalog);

                if ( (m_AasTarget.RemoteCatalogBuildPath != null && m_AasTarget.RemoteCatalogLoadPath != null) // these will never actually be null, as the accessor initializes them.
                     && (m_AasTarget.BuildRemoteCatalog))
                {
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("m_RemoteCatalogBuildPath"), m_RemoteCatBuildPath);
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("m_RemoteCatalogLoadPath"), m_RemoteCatLoadPath);
                }
            }
            
            GUILayout.Space(6);
            m_GeneralFoldout = EditorGUILayout.Foldout(m_GeneralFoldout, "General");
            if (m_GeneralFoldout)
            {
                ProjectConfigData.postProfilerEvents = EditorGUILayout.Toggle(m_SendProfilerEvents, ProjectConfigData.postProfilerEvents);
                m_AasTarget.buildSettings.LogResourceManagerExceptions = EditorGUILayout.Toggle(m_LogRuntimeExceptions, m_AasTarget.buildSettings.LogResourceManagerExceptions);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_CertificateHandlerType"), m_CertificateHandlerType);
                m_AasTarget.UniqueBundleIds = EditorGUILayout.Toggle(m_UniqueBundles, m_AasTarget.UniqueBundleIds);
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


            serializedObject.ApplyModifiedProperties();
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
            var assetPath = EditorUtility.OpenFilePanelWithFilters("Data Builder", "Assets", new[] {"Data Builder", "asset" });
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
            var templateObj = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath.Substring(assetPath.IndexOf("Assets/")));
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

    }

}
