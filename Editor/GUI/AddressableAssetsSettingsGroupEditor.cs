using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Diagnostics;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build.Pipeline.Utilities;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Serialization;

// ReSharper disable DelegateSubtraction

namespace UnityEditor.AddressableAssets.GUI
{
    [Serializable]
    class AddressableAssetsSettingsGroupEditor
    {
        [FormerlySerializedAs("treeState")]
        [SerializeField]
        TreeViewState m_TreeState;
        [FormerlySerializedAs("mchs")]
        [SerializeField]
        MultiColumnHeaderState m_Mchs;
        AddressableAssetEntryTreeView m_EntryTree;

        public AddressableAssetsWindow window;

        SearchField m_SearchField;
        const int k_SearchHeight = 20;
        internal AddressableAssetSettings settings { get { return AddressableAssetSettingsDefaultObject.Settings; } }

        bool m_ResizingVerticalSplitter;
        Rect m_VerticalSplitterRect = new Rect(0, 0, 10, k_SplitterWidth);
        [SerializeField]
        float m_VerticalSplitterPercent;
        const int k_SplitterWidth = 3;

        public AddressableAssetsSettingsGroupEditor(AddressableAssetsWindow w)
        {
            window = w;
            m_VerticalSplitterPercent = 0.8f;
            OnEnable();
        }

        void OnSettingsModification(AddressableAssetSettings s, AddressableAssetSettings.ModificationEvent e, object o)
        {
            if (m_EntryTree == null)
                return;

            switch (e)
            {
                case AddressableAssetSettings.ModificationEvent.GroupAdded:
                case AddressableAssetSettings.ModificationEvent.GroupRemoved:
                case AddressableAssetSettings.ModificationEvent.EntryAdded:
                case AddressableAssetSettings.ModificationEvent.EntryMoved:
                case AddressableAssetSettings.ModificationEvent.EntryRemoved:
                case AddressableAssetSettings.ModificationEvent.GroupRenamed:
                case AddressableAssetSettings.ModificationEvent.EntryModified:
                case AddressableAssetSettings.ModificationEvent.BatchModification:
                    m_EntryTree.Reload();
                    if (window != null)
                        window.Repaint();
                    break;
            }
        }

        GUIStyle GetStyle(string styleName)
        {
            GUIStyle s = UnityEngine.GUI.skin.FindStyle(styleName);
            if (s == null)
                s = EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector).FindStyle(styleName);
            if (s == null)
            {
                Addressables.LogError("Missing built-in guistyle " + styleName);
                s = new GUIStyle();
            }
            return s;
        }

        [NonSerialized]
        List<GUIStyle> m_SearchStyles;
        [NonSerialized]
        GUIStyle m_ButtonStyle;
        [NonSerialized]
        Texture2D m_CogIcon;

        void TopToolbar(Rect toolbarPos)
        {
            if (m_SearchStyles == null)
            {
                m_SearchStyles = new List<GUIStyle>();
                m_SearchStyles.Add(GetStyle("ToolbarSeachTextFieldPopup")); //GetStyle("ToolbarSeachTextField");
                m_SearchStyles.Add(GetStyle("ToolbarSeachCancelButton"));
                m_SearchStyles.Add(GetStyle("ToolbarSeachCancelButtonEmpty"));
            }
            if (m_ButtonStyle == null)
                m_ButtonStyle = GetStyle("ToolbarButton");
            if (m_CogIcon == null)
                m_CogIcon = EditorGUIUtility.FindTexture("_Popup");


            GUILayout.BeginArea(new Rect(0, 0, toolbarPos.width, k_SearchHeight));

            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            {
                float spaceBetween = 4f;

                
                {    
                    var guiMode = new GUIContent("Create");
                    Rect rMode = GUILayoutUtility.GetRect(guiMode, EditorStyles.toolbarDropDown);
                    if (EditorGUI.DropdownButton(rMode, guiMode, FocusType.Passive, EditorStyles.toolbarDropDown))
                    {
                        var menu = new GenericMenu();
                        foreach( var templateObject in settings.GroupTemplateObjects )
                        {
                            if(templateObject != null)
                                menu.AddItem( new GUIContent("Group/" + templateObject.name ), false, m_EntryTree.CreateNewGroup, templateObject);
                        }
                        menu.AddSeparator(string.Empty);
                        menu.AddItem( new GUIContent("Group/Blank (no schema)"), false, m_EntryTree.CreateNewGroup, null);
                        menu.DropDown(rMode);
                    }
                }
                
                CreateProfileDropdown();
            
                {    
                    var guiMode = new GUIContent("Tools");
                    Rect rMode = GUILayoutUtility.GetRect(guiMode, EditorStyles.toolbarDropDown);
                    if (EditorGUI.DropdownButton(rMode, guiMode, FocusType.Passive, EditorStyles.toolbarDropDown))
                    {
                        var menu = new GenericMenu();
                        menu.AddItem( new GUIContent("Inspect System Settings"), false, () => { 
                            EditorGUIUtility.PingObject(AddressableAssetSettingsDefaultObject.Settings);
                            Selection.activeObject = AddressableAssetSettingsDefaultObject.Settings;});
                        menu.AddItem( new GUIContent("Profiles"), false, () => EditorWindow.GetWindow<ProfileWindow>().Show(true));
                        menu.AddItem( new GUIContent("Labels"), false, () => EditorWindow.GetWindow<LabelWindow>(true).Intialize(settings));
                        menu.AddItem( new GUIContent("Analyze"), false, AnalyzeWindow.ShowWindow);
                        menu.AddItem( new GUIContent("Hosting Services"), false, () => EditorWindow.GetWindow<HostingServicesWindow>().Show(settings));
                        menu.AddItem( new GUIContent("Event Viewer"), false, ResourceProfilerWindow.ShowWindow);
                        menu.AddItem(new GUIContent("Check for Content Update Restrictions"), false, OnPrepareUpdate);
                        menu.AddItem(new GUIContent("Show Sprite and Subobject Addresses"), ProjectConfigData.showSubObjectsInGroupView, () => { ProjectConfigData.showSubObjectsInGroupView = !ProjectConfigData.showSubObjectsInGroupView; m_EntryTree.Reload(); });

                        var bundleList = AssetDatabase.GetAllAssetBundleNames();
                        if (bundleList != null && bundleList.Length > 0)
                            menu.AddItem( new GUIContent("Convert Legacy AssetBundles"), false, window.OfferToConvert);
                        
                        menu.DropDown(rMode);
                    }
                }

                GUILayout.FlexibleSpace();
                GUILayout.Space(spaceBetween * 2f);
                
                {
                    GUILayout.Space(8);
                    var guiMode = new GUIContent("Play Mode Script");
                    Rect rMode = GUILayoutUtility.GetRect(guiMode, EditorStyles.toolbarDropDown);
                    if (EditorGUI.DropdownButton(rMode, guiMode, FocusType.Passive, EditorStyles.toolbarDropDown))
                    {
                        var menu = new GenericMenu();
                        for (int i = 0; i < settings.DataBuilders.Count; i++)
                        {
                            var m = settings.GetDataBuilder(i);
                            if(m.CanBuildData<AddressablesPlayModeBuildResult>())
                                menu.AddItem(new GUIContent(m.Name), i == settings.ActivePlayModeDataBuilderIndex, OnSetActivePlayModeScript, i);
                        }
                        menu.DropDown(rMode);
                    }
                }
                
                var guiBuild = new GUIContent("Build");
                Rect rBuild = GUILayoutUtility.GetRect(guiBuild, EditorStyles.toolbarDropDown);
                if (EditorGUI.DropdownButton(rBuild, guiBuild, FocusType.Passive, EditorStyles.toolbarDropDown))
                {
                    //GUIUtility.hotControl = 0;
                    var menu = new GenericMenu();
                    for (int i = 0; i < settings.DataBuilders.Count; i++)
                    {
                        var m = settings.GetDataBuilder(i);
                        if (m.CanBuildData<AddressablesPlayerBuildResult>())
                        {
                            menu.AddItem(new GUIContent("New Build/" + m.Name), false, OnBuildScript, i);
                        }
                    }
                    
                    menu.AddItem(new GUIContent("Update a Previous Build"), false, OnUpdateBuild);
                    menu.AddItem(new GUIContent("Clean Build/All"), false, OnCleanAll);
                    menu.AddItem(new GUIContent("Clean Build/Content Builders/All"), false, OnCleanAddressables, null);
                    for (int i = 0; i < settings.DataBuilders.Count; i++)
                    {
                        var m = settings.GetDataBuilder(i);
                        menu.AddItem(new GUIContent("Clean Build/Content Builders/" + m.Name), false, OnCleanAddressables, m);
                    }
                    menu.AddItem(new GUIContent("Clean Build/Build Pipeline Cache"), false, OnCleanSBP);
                    menu.DropDown(rBuild);
                }

                GUILayout.Space(4);
                Rect searchRect = GUILayoutUtility.GetRect(0, toolbarPos.width * 0.6f, 16f, 16f, m_SearchStyles[0], GUILayout.MinWidth(65), GUILayout.MaxWidth(300));
                Rect popupPosition = searchRect;
                popupPosition.width = 20;

                if (Event.current.type == EventType.MouseDown && popupPosition.Contains(Event.current.mousePosition))
                {
                    var menu = new GenericMenu();
                    menu.AddItem(new GUIContent("Hierarchical Search"), ProjectConfigData.hierarchicalSearch, OnHierSearchClick);
                    menu.DropDown(popupPosition);
                }
                else
                {
                    var baseSearch = ProjectConfigData.hierarchicalSearch ? m_EntryTree.customSearchString : m_EntryTree.searchString;
                    var searchString = m_SearchField.OnGUI(searchRect, baseSearch, m_SearchStyles[0], m_SearchStyles[1], m_SearchStyles[2]);
                    if (baseSearch != searchString)
                    {
                        if (ProjectConfigData.hierarchicalSearch)
                        {
                            m_EntryTree.customSearchString = searchString;
                            Reload();
                        }
                        else
                        {
                            m_EntryTree.searchString = searchString;
                        }
                    }
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        void OnCleanAll()
        {
            OnCleanAddressables(null);
            OnCleanSBP();
        }

        void OnCleanAddressables(object builder)
        {
            AddressableAssetSettings.CleanPlayerContent(builder as IDataBuilder);
        }

        void OnCleanSBP()
        {
            BuildCache.PurgeCache(true);
        }

        void OnPrepareUpdate()
        {
            var path = ContentUpdateScript.GetContentStateDataPath(true);
            if (string.IsNullOrEmpty(path))
                Debug.LogWarning("No path specified for Content State Data file.");
            else if (!File.Exists(path))
                Debug.LogWarningFormat("No Content State Data file exists at path: {0}");
            else
                ContentUpdatePreviewWindow.PrepareForContentUpdate(AddressableAssetSettingsDefaultObject.Settings, path);
        }

        void OnBuildScript(object context)
        {
            OnSetActiveBuildScript(context);
            OnBuildPlayerData();
        }

        void OnBuildPlayerData()
        {
            AddressableAssetSettings.BuildPlayerContent();
        }

        void OnUpdateBuild()
        {
            var path = ContentUpdateScript.GetContentStateDataPath(true);
            if(!string.IsNullOrEmpty(path))
                ContentUpdateScript.BuildContentUpdate(AddressableAssetSettingsDefaultObject.Settings, path);
        }

        void OnSetActiveBuildScript(object context)
        {
            AddressableAssetSettingsDefaultObject.Settings.ActivePlayerDataBuilderIndex = (int)context;
        }
        void OnSetActivePlayModeScript(object context)
        {
            AddressableAssetSettingsDefaultObject.Settings.ActivePlayModeDataBuilderIndex = (int)context;
        }

        void OnHierSearchClick()
        {
            ProjectConfigData.hierarchicalSearch = !ProjectConfigData.hierarchicalSearch;
            m_EntryTree.ClearSearch();
            m_EntryTree.Reload();
            m_EntryTree.Repaint();
        }

        void CreateProfileDropdown()
        {
            var activeProfileName = settings.profileSettings.GetProfileName(settings.activeProfileId);
            if (string.IsNullOrEmpty(activeProfileName))
            {
                settings.activeProfileId = null; //this will reset it to default.
                activeProfileName = settings.profileSettings.GetProfileName(settings.activeProfileId);
            }
            var profileButton = new GUIContent("Profile: " + activeProfileName);

            Rect r = GUILayoutUtility.GetRect(profileButton, m_ButtonStyle, GUILayout.Width(115f));
            if (EditorGUI.DropdownButton(r, profileButton, FocusType.Passive, EditorStyles.toolbarDropDown))
            {
                //GUIUtility.hotControl = 0;
                var menu = new GenericMenu();

                var nameList = settings.profileSettings.GetAllProfileNames();

                foreach (var name in nameList)
                {
                    menu.AddItem(new GUIContent(name), name == activeProfileName, SetActiveProfile, name);
                }
                menu.AddSeparator(string.Empty);
                menu.AddItem(new GUIContent("Manage Profiles"), false, () => EditorWindow.GetWindow<ProfileWindow>().Show(true));
                menu.DropDown(r);
            }
        }

        void SetActiveProfile(object context)
        {
            var n = context as string;
            AddressableAssetSettingsDefaultObject.Settings.activeProfileId = AddressableAssetSettingsDefaultObject.Settings.profileSettings.GetProfileId(n);
        }

        bool m_ModificationRegistered;
        public void OnEnable()
        {
            if (AddressableAssetSettingsDefaultObject.Settings == null)
                return;
            AddressableAssetSettingsDefaultObject.Settings.OnModification += OnSettingsModification;
            m_ModificationRegistered = true;
        }

        public void OnDisable()
        {
            if (AddressableAssetSettingsDefaultObject.Settings == null)
                return;
            AddressableAssetSettingsDefaultObject.Settings.OnModification -= OnSettingsModification;
            m_ModificationRegistered = false;
        }

        public bool OnGUI(Rect pos)
        {
            if (settings == null)
                return false;

            if (!m_ModificationRegistered)
            {
                m_ModificationRegistered = true;
                settings.OnModification -= OnSettingsModification; //just in case...
                settings.OnModification += OnSettingsModification;
            }



            if (m_EntryTree == null)
            {
                if (m_TreeState == null)
                    m_TreeState = new TreeViewState();

                var headerState = AddressableAssetEntryTreeView.CreateDefaultMultiColumnHeaderState();
                if (MultiColumnHeaderState.CanOverwriteSerializedFields(m_Mchs, headerState))
                    MultiColumnHeaderState.OverwriteSerializedFields(m_Mchs, headerState);
                m_Mchs = headerState;

                m_SearchField = new SearchField();
                m_EntryTree = new AddressableAssetEntryTreeView(m_TreeState, m_Mchs, this);
                m_EntryTree.Reload();
            }

            HandleVerticalResize(pos);
            var inRectY = pos.height;
            var searchRect = new Rect(pos.xMin, pos.yMin, pos.width, k_SearchHeight);
            var treeRect = new Rect(pos.xMin, pos.yMin + k_SearchHeight, pos.width, inRectY - k_SearchHeight);

            TopToolbar(searchRect);
            m_EntryTree.OnGUI(treeRect);
            return m_ResizingVerticalSplitter;
        }

        public void Reload()
        {
            if (m_EntryTree != null)
                m_EntryTree.Reload();
        }

        void HandleVerticalResize(Rect position)
        {
            m_VerticalSplitterRect.y = (int)(position.yMin + position.height * m_VerticalSplitterPercent);
            m_VerticalSplitterRect.width = position.width;
            m_VerticalSplitterRect.height = k_SplitterWidth;


            EditorGUIUtility.AddCursorRect(m_VerticalSplitterRect, MouseCursor.ResizeVertical);
            if (Event.current.type == EventType.MouseDown && m_VerticalSplitterRect.Contains(Event.current.mousePosition))
                m_ResizingVerticalSplitter = true;

            if (m_ResizingVerticalSplitter)
            {
                var mousePosInRect = Event.current.mousePosition.y - position.yMin;
                m_VerticalSplitterPercent = Mathf.Clamp(mousePosInRect / position.height, 0.20f, 0.90f);
                m_VerticalSplitterRect.y = (int)(position.height * m_VerticalSplitterPercent + position.yMin);

                if (Event.current.type == EventType.MouseUp)
                {
                    m_ResizingVerticalSplitter = false;
                }
            }
            else
                m_VerticalSplitterPercent = Mathf.Clamp(m_VerticalSplitterPercent, 0.20f, 0.90f);
        }
    }
}
