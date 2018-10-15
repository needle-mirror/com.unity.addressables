using System.Collections.Generic;
using UnityEngine;
using UnityEditor.IMGUI.Controls;
using System;
using System.Linq;
using UnityEditor.Build.Pipeline.Utilities;
using UnityEngine.AddressableAssets;

namespace UnityEditor.AddressableAssets
{
    [Serializable]
    internal class AddressableAssetsSettingsGroupEditor
    {
        [SerializeField]
        TreeViewState treeState;
        [SerializeField]
        MultiColumnHeaderState mchs;
        AddressableAssetEntryTreeView entryTree;

        public AddressableAssetsWindow window;

        SearchField searchField;
        const int k_SearchHeight = 20;
        internal AddressableAssetSettings settings { get { return AddressableAssetSettingsDefaultObject.Settings; } }

        bool m_ResizingVerticalSplitter = false;
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
            if (entryTree == null)
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
                    entryTree.Reload();
                    if (window != null)
                        window.Repaint();
                    break;
                case AddressableAssetSettings.ModificationEvent.EntryCreated:
                case AddressableAssetSettings.ModificationEvent.LabelAdded:
                case AddressableAssetSettings.ModificationEvent.LabelRemoved:
                case AddressableAssetSettings.ModificationEvent.ProfileAdded:
                case AddressableAssetSettings.ModificationEvent.ProfileRemoved:
                case AddressableAssetSettings.ModificationEvent.ProfileModified:
                default:
                    break;
            }
        }
        private GUIStyle GetStyle(string styleName)
        {
            GUIStyle s = GUI.skin.FindStyle(styleName);
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
        List<GUIStyle> searchStyles = null;
        [NonSerialized]
        GUIStyle buttonStyle = null;
        bool analyzeMode = false;
        //bool hostMode = false;
        [NonSerialized]
        public Texture2D cogIcon = null;

        void TopToolbar(Rect toolbarPos)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (searchStyles == null)
            {
                searchStyles = new List<GUIStyle>();
                searchStyles.Add(GetStyle("ToolbarSeachTextFieldPopup")); //GetStyle("ToolbarSeachTextField");
                searchStyles.Add(GetStyle("ToolbarSeachCancelButton"));
                searchStyles.Add(GetStyle("ToolbarSeachCancelButtonEmpty"));
            }
            if (buttonStyle == null)
                buttonStyle = GetStyle("ToolbarButton");
            if (cogIcon == null)
                cogIcon = EditorGUIUtility.FindTexture("_Popup");


            GUILayout.BeginArea(new Rect(0, 0, toolbarPos.width, k_SearchHeight));

            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            {
                float spaceBetween = 4f;

                CreateDropdown();
                GUILayout.Space(8);

                if (GUILayout.Button("Hosting", buttonStyle))
                    EditorWindow.GetWindow<HostingServicesWindow>().Show(settings);

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
                            if (m.CanBuildData<AddressablesPlayModeBuildResult>())
                                menu.AddItem(new GUIContent(m.Name), i == settings.ActivePlayModeDataBuilderIndex, OnSetActivePlayModeScript, i);
                        }
                        menu.DropDown(rMode);
                    }
                }
                {
                    GUILayout.Space(8);
                    var guiMode = new GUIContent("Build Script");
                    Rect rMode = GUILayoutUtility.GetRect(guiMode, EditorStyles.toolbarDropDown);
                    if (EditorGUI.DropdownButton(rMode, guiMode, FocusType.Passive, EditorStyles.toolbarDropDown))
                    {
                        var menu = new GenericMenu();
                        for (int i = 0; i < settings.DataBuilders.Count; i++)
                        {
                            var m = settings.GetDataBuilder(i);
                            if(m.CanBuildData<AddressablesPlayerBuildResult>())
                                menu.AddItem(new GUIContent(m.Name), i == settings.ActivePlayerDataBuilderIndex, OnSetActiveBuildScript, i);
                        }
                        menu.DropDown(rMode);
                    }
                }

                var p = GUILayout.Toggle(analyzeMode, "Analyze", buttonStyle);
                if (p != analyzeMode)
                {
                    analyzeMode = p;
                }

                var guiBuild = new GUIContent("Build");
                Rect rBuild = GUILayoutUtility.GetRect(guiBuild, EditorStyles.toolbarDropDown);
                if (EditorGUI.DropdownButton(rBuild, guiBuild, FocusType.Passive, EditorStyles.toolbarDropDown))
                {
                    //GUIUtility.hotControl = 0;
                    var menu = new GenericMenu();
                    menu.AddItem(new GUIContent("Clean/All Cached Data"), false, OnCleanAll);
                    menu.AddItem(new GUIContent("Clean/Addressables Cache"), false, OnCleanAddressables);
                    menu.AddItem(new GUIContent("Clean/Build Pipeline Cache"), false, OnCleanSBP);
                    menu.AddItem(new GUIContent("Prepare For Content Update"), false, OnPrepareUpdate);
                    menu.AddItem(new GUIContent("Build For Content Update"), false, OnUpdateBuild);
                    menu.DropDown(rBuild);
                }


                GUILayout.FlexibleSpace();

                GUILayout.Space(spaceBetween * 2f);

                Rect searchRect = GUILayoutUtility.GetRect(0, toolbarPos.width * 0.6f, 16f, 16f, searchStyles[0], GUILayout.MinWidth(65), GUILayout.MaxWidth(300));
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
                    var baseSearch = ProjectConfigData.hierarchicalSearch ? entryTree.customSearchString : entryTree.searchString;
                    var searchString = searchField.OnGUI(searchRect, baseSearch, searchStyles[0], searchStyles[1], searchStyles[2]);
                    if (baseSearch != searchString)
                    {
                        if (ProjectConfigData.hierarchicalSearch)
                        {
                            entryTree.customSearchString = searchString;
                            Reload();
                        }
                        else
                        {
                            entryTree.searchString = searchString;
                        }
                    }
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void OnCleanAll()
        {
            OnCleanAddressables();
            OnCleanSBP();
        }

        private void OnCleanAddressables()
        {
            var aa = AddressableAssetSettingsDefaultObject.Settings;
            if (aa == null)
                return;

            foreach (var s in aa.DataBuilders)
            {
                var builder = s as IDataBuilder;
                if(builder != null)
                    builder.ClearCachedData();
            }
        }

        private void OnCleanSBP()
        {
            BuildCache.PurgeCache();
        }

        private void OnPrepareUpdate()
        {
            ContentUpdatePreviewWindow.PrepareForContentUpdate(AddressableAssetSettingsDefaultObject.Settings, ContentUpdateScript.GetContentStateDataPath(true));
        }

        private void OnUpdateBuild()
        {
            ContentUpdateScript.BuildContentUpdate(AddressableAssetSettingsDefaultObject.Settings, ContentUpdateScript.GetContentStateDataPath(true));
        }

        void OnSetActiveBuildScript(object context)
        {
            AddressableAssetSettingsDefaultObject.Settings.ActivePlayerDataBuilderIndex = (int)context;
        }
        void OnSetActivePlayModeScript(object context)
        {
            AddressableAssetSettingsDefaultObject.Settings.ActivePlayModeDataBuilderIndex = (int)context;
        }

        void OnSendProfileClick()
        {
            ProjectConfigData.postProfilerEvents = !ProjectConfigData.postProfilerEvents;
        }
        void OnHierSearchClick()
        {
            ProjectConfigData.hierarchicalSearch = !ProjectConfigData.hierarchicalSearch;
            entryTree.ClearSearch();
        }
        void CreateDropdown()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var activeProfileName = settings.profileSettings.GetProfileName(settings.activeProfileId);
            if (settings.activeProfileId != null && string.IsNullOrEmpty(activeProfileName))
            {
                settings.activeProfileId = settings.profileSettings.GetProfileId(AddressableAssetProfileSettings.k_rootProfileName);
                activeProfileName = settings.profileSettings.GetProfileName(settings.activeProfileId);
            }
            var profileButton = new GUIContent("Profile: " + activeProfileName);

            Rect r = GUILayoutUtility.GetRect(profileButton, buttonStyle, GUILayout.Width(115f));
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
                menu.AddItem(new GUIContent("Inspect Profile Settings"), false, GoToSettingsAsset);
                menu.DropDown(r);
            }
        }

        void SetActiveProfile(object context)
        {
            var n = context as string;
            AddressableAssetSettingsDefaultObject.Settings.activeProfileId = AddressableAssetSettingsDefaultObject.Settings.profileSettings.GetProfileId(n);
        }
        private void GoToSettingsAsset()
        {
            EditorGUIUtility.PingObject(AddressableAssetSettingsDefaultObject.Settings);
            Selection.activeObject = AddressableAssetSettingsDefaultObject.Settings;
        }

        private bool m_modificationRegistered = false;
        public void OnEnable()
        {
            if (AddressableAssetSettingsDefaultObject.Settings == null)
                return;
            AddressableAssetSettingsDefaultObject.Settings.OnModification += OnSettingsModification;
            m_modificationRegistered = true;
        }

        public void OnDisable()
        {
            if (AddressableAssetSettingsDefaultObject.Settings == null)
                return;
            AddressableAssetSettingsDefaultObject.Settings.OnModification -= OnSettingsModification;
            m_modificationRegistered = false;
        }

        [SerializeField]
        AssetSettingsAnalyze m_analyzeEditor = null;

        public bool OnGUI(Rect pos)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
                return false;

            if (!m_modificationRegistered)
            {
                m_modificationRegistered = true;
                settings.OnModification -= OnSettingsModification; //just in case...
                settings.OnModification += OnSettingsModification;
            }



            if (entryTree == null)
            {
                if (treeState == null)
                    treeState = new TreeViewState();

                var headerState = AddressableAssetEntryTreeView.CreateDefaultMultiColumnHeaderState();
                if (MultiColumnHeaderState.CanOverwriteSerializedFields(mchs, headerState))
                    MultiColumnHeaderState.OverwriteSerializedFields(mchs, headerState);
                mchs = headerState;

                searchField = new SearchField();
                entryTree = new AddressableAssetEntryTreeView(treeState, mchs, this);
                entryTree.Reload();
            }

            HandleVerticalResize(pos);
            var width = pos.width - k_SplitterWidth * 2;
            var inRectY = pos.height;
            if (analyzeMode)
                inRectY = m_VerticalSplitterRect.yMin - pos.yMin;

            var searchRect = new Rect(pos.xMin, pos.yMin, pos.width, k_SearchHeight);
            var treeRect = new Rect(pos.xMin, pos.yMin + k_SearchHeight, pos.width, inRectY - k_SearchHeight);
            var botRect = new Rect(pos.xMin + k_SplitterWidth, pos.yMin + inRectY + k_SplitterWidth, width, pos.height - inRectY - k_SplitterWidth * 2);

            TopToolbar(searchRect);

            if (!analyzeMode)
            {
                entryTree.OnGUI(treeRect);
            }
            else
            {
                entryTree.OnGUI(treeRect);
                if (m_analyzeEditor == null)
                    m_analyzeEditor = new AssetSettingsAnalyze();
                m_analyzeEditor.OnGUI(botRect, settings);
            }



            return m_ResizingVerticalSplitter;
        }

        public void Reload()
        {
            if (entryTree != null)
                entryTree.Reload();
        }

        private void HandleVerticalResize(Rect position)
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
