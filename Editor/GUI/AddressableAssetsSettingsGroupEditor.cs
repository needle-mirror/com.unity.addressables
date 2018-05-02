using System.Collections.Generic;
using UnityEngine;
using UnityEditor.IMGUI.Controls;
using System;
using System.Linq;

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


        [SerializeField]
        public AddressableAssetsSettingsConfigEditor m_configEditor = null;

        public AddressableAssetsWindow window;

        SearchField searchField;
        const int k_SearchHeight = 20;
        internal AddressableAssetSettings settings { get { return AddressableAssetSettings.GetDefault(false, false); } }

        public AssetDetailsView m_details = null;

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
                case AddressableAssetSettings.ModificationEvent.GroupProcessorModified:
                case AddressableAssetSettings.ModificationEvent.EntryModified:
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
                Debug.LogError("Missing built-in guistyle " + styleName);
                s = new GUIStyle();
            }
            return s;
        }

        GUIStyle searchStyle = null;
        GUIStyle buttonStyle = null;
        bool previewMode = false;
        public Texture2D cogIcon = null;

        //TODO - this is a temp hack until we have better speed settings
        enum SpeedSettings
        {
            undefined,
            low,
            medium,
            high
        };
        SpeedSettings speed =SpeedSettings.undefined;

        void TopToolbar(Rect toolbarPos)
        {
            if (searchStyle == null)
                searchStyle = GetStyle("ToolbarSeachTextField");
            if (buttonStyle == null)
                buttonStyle = GetStyle("ToolbarButton");
            if(cogIcon == null)
                cogIcon = EditorGUIUtility.FindTexture("_Popup");
           

            GUILayout.BeginArea(new Rect(0, 0, toolbarPos.width, k_SearchHeight));

            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            {
                float spaceBetween = 4f;
                
                CreateDropdown();
                GUILayout.Space(8);
                var p = GUILayout.Toggle(previewMode, "Preview", buttonStyle);
                if(p != previewMode)
                {
                    previewMode = p;
                    if (previewMode)
                        m_previewEditor.ReloadIfEmpty();
                }
                
                GUILayout.Space(20);
                using (new EditorGUI.DisabledScope(true))
                {
                    if (GUILayout.Button("Build", buttonStyle))
                        Debug.Log("Integrated build flow is not yet implemented.");
                }
                
                GUILayout.FlexibleSpace();

                GUILayout.Space(spaceBetween * 2f);

                Rect searchRect = GUILayoutUtility.GetRect(0, toolbarPos.width * 0.6f, 16f, 16f, searchStyle, GUILayout.MinWidth(65), GUILayout.MaxWidth(300));
                var searchString = searchField.OnToolbarGUI(searchRect, entryTree.searchString);
                entryTree.searchString = searchString;

                GUILayout.Space(spaceBetween);
                var gc = new GUIContent(cogIcon);
                Rect r = GUILayoutUtility.GetRect(gc, buttonStyle);
                if (EditorGUI.DropdownButton(r, gc, FocusType.Passive,buttonStyle))
                {
                    if(speed == SpeedSettings.undefined)
                    {
                        if (settings.buildSettings.localLoadSpeed == 1024 * 1024 * 10)
                            speed = SpeedSettings.medium;
                        else if (settings.buildSettings.localLoadSpeed < 1024 * 1024 * 10)
                            speed = SpeedSettings.low;
                        else
                            speed = SpeedSettings.high;

                    }
                    var menu = new GenericMenu();
                    bool fast = settings.buildSettings.editorPlayMode == UnityEngine.AddressableAssets.ResourceManagerRuntimeData.EditorPlayMode.FastMode;
                    bool virt = settings.buildSettings.editorPlayMode == UnityEngine.AddressableAssets.ResourceManagerRuntimeData.EditorPlayMode.VirtualMode;
                    bool packed = settings.buildSettings.editorPlayMode == UnityEngine.AddressableAssets.ResourceManagerRuntimeData.EditorPlayMode.PackedMode;
                    menu.AddItem(new GUIContent("Play Mode/Fast"), fast, OnFastClick);
                    menu.AddItem(new GUIContent("Play Mode/Virtual"), virt, OnVirtualClick);
                    menu.AddItem(new GUIContent("Play Mode/Packed"), packed, OnPackedClick);
                    menu.AddSeparator("Play Mode/");
                    menu.AddItem(new GUIContent("Play Mode/Virtual Settings/3G speeds"), speed == SpeedSettings.low, OnVirtual3GSpeedClick);
                    menu.AddItem(new GUIContent("Play Mode/Virtual Settings/High-Speed"), speed == SpeedSettings.medium, OnVirtualHighSpeedClick);
                    menu.AddItem(new GUIContent("Play Mode/Virtual Settings/Max Speed"), speed == SpeedSettings.high, OnVirtualInfiniteSpeedClick);
                    menu.AddItem(new GUIContent("Send Profile Events"), settings.buildSettings.postProfilerEvents, OnSendProfileClick);
                    menu.DropDown(r);
                }
                GUILayout.Space(spaceBetween);
            }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }
        void OnFastClick()
        {
            settings.buildSettings.editorPlayMode = UnityEngine.AddressableAssets.ResourceManagerRuntimeData.EditorPlayMode.FastMode;
        }
        void OnVirtualClick()
        {
            settings.buildSettings.editorPlayMode = UnityEngine.AddressableAssets.ResourceManagerRuntimeData.EditorPlayMode.VirtualMode;
        }
        void OnPackedClick()
        {
            settings.buildSettings.editorPlayMode = UnityEngine.AddressableAssets.ResourceManagerRuntimeData.EditorPlayMode.PackedMode;
        }
        void OnVirtual3GSpeedClick()
        {
            speed = SpeedSettings.low;
            settings.buildSettings.localLoadSpeed = 1024 * 1024 * 3;
            settings.buildSettings.remoteLoadSpeed = 262144; // 1024 * 1024 / 4;
        }
        void OnVirtualHighSpeedClick()
        {
            speed = SpeedSettings.medium;
            settings.buildSettings.localLoadSpeed = 1024 * 1024 * 10;
            settings.buildSettings.remoteLoadSpeed = 1024 * 1024 * 1;
        }
        void OnVirtualInfiniteSpeedClick()
        {
            speed = SpeedSettings.high;
            settings.buildSettings.localLoadSpeed = long.MaxValue;
            settings.buildSettings.remoteLoadSpeed = long.MaxValue;
        }
        void OnSendProfileClick()
        {
            settings.buildSettings.postProfilerEvents = !settings.buildSettings.postProfilerEvents;
        }
        void CreateDropdown()
        {
            var activeProfileName = settings.profileSettings.GetProfileName(settings.activeProfileId);
            if(settings.activeProfileId != null && string.IsNullOrEmpty(activeProfileName))
            {
                settings.activeProfileId = settings.profileSettings.GetProfileId(AddressableAssetSettings.ProfileSettings.k_rootProfileName);
                activeProfileName = settings.profileSettings.GetProfileName(settings.activeProfileId);
            }
            var profileButton = new GUIContent("Profile: " + activeProfileName);

            Rect r = GUILayoutUtility.GetRect(profileButton, buttonStyle, GUILayout.Width(115f));
            if (EditorGUI.DropdownButton(r, profileButton, FocusType.Passive, EditorStyles.toolbarDropDown))
            {
                //GUIUtility.hotControl = 0;
                var menu = new GenericMenu();

                var nameList = settings.profileSettings.GetAllProfileNames();
                
                foreach(var name in nameList)
                {
                    menu.AddItem(new GUIContent(name), name == activeProfileName, SetActiveProfile, name);
                }
                menu.AddItem(new GUIContent("Manage Profiles"), false, ShowManageProfilesWindow);
                menu.DropDown(r);
            }
        }

        void SetActiveProfile(object context)
        {
            var n = context as string;
            settings.activeProfileId = settings.profileSettings.GetProfileId(n);
        }

        void ShowManageProfilesWindow()
        {
            ProfilesWindow.OpenWindow();
        }

        private bool m_modificationRegistered = false;
        public void OnEnable()
        {
            if (settings == null)
                return;
            settings.OnModification += OnSettingsModification;
            m_modificationRegistered = true;


            if (m_configEditor != null)
                m_configEditor.OnEnable();
        }

        public void OnDisable()
        {
            if (settings == null)
                return;
            settings.OnModification -= OnSettingsModification;
            m_modificationRegistered = false;

            if (m_configEditor != null)
                m_configEditor.OnDisable();
        }

        [SerializeField]
        AssetSettingsPreview m_previewEditor = null;

        public bool OnGUI(Rect pos)
        {
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
            if (m_details == null)
            {
                m_details = new AssetDetailsView();
                var sel = entryTree.GetSelection();
                entryTree.SetSelection(sel, TreeViewSelectionOptions.FireSelectionChanged);
            }


            HandleVerticalResize(pos);
            var width = pos.width - k_SplitterWidth * 2;
            var inRectY = pos.height;
            if(m_details.SelectionHasSettings() && !previewMode)
                inRectY = m_VerticalSplitterRect.yMin - pos.yMin;

            var searchRect = new Rect(pos.xMin, pos.yMin, pos.width, k_SearchHeight);
            var treeRect = new Rect(pos.xMin, pos.yMin + k_SearchHeight, pos.width, inRectY - k_SearchHeight);
            var botRect = new Rect(pos.xMin + k_SplitterWidth, pos.yMin + inRectY + k_SplitterWidth, width, pos.height - inRectY - k_SplitterWidth * 2);

            TopToolbar(searchRect);

            if(!previewMode)
            {
                entryTree.OnGUI(treeRect);
                if(inRectY != pos.height)
                    m_details.OnGUI(settings, botRect);
            }
            else
            {

                if (m_previewEditor == null)
                    m_previewEditor = new AssetSettingsPreview();
                m_previewEditor.OnGUI(treeRect);
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
        }
    }

    internal class AddressableAssetEntryTreeView : TreeView
    {
        AddressableAssetsSettingsGroupEditor editor;
        public AddressableAssetEntryTreeView(TreeViewState state, MultiColumnHeaderState mchs, AddressableAssetsSettingsGroupEditor ed) : base(state, new MultiColumnHeader(mchs))
        {
            showBorder = true;
            editor = ed;
            columnIndexForTreeFoldouts = 0;
        }

        protected override TreeViewItem BuildRoot()
        {
            var root = new TreeViewItem(-1, -1);
            foreach (var group in editor.settings.groups)
                AddGroupChildrenBuild(group, root);
        
            return root;
        }

        public enum ColumnID
        {
            Id,
            Type,
            Path,
            Labels
        }

        protected virtual void Search(string search, List<TreeViewItem> rows)
        {
            if (string.IsNullOrEmpty(search))
                throw new ArgumentException("Invalid search: cannot be null or empty", "search");

            var stack = new Stack<AddressableAssetSettings.AssetGroup.AssetEntry>();

            //TODO - might be simpler to take advantage of settings.m_allEntries somehow.
            foreach (var group in editor.settings.groups)
            {
                foreach (var e in group.entries)
                {
                    stack.Push(e);
                }
            }
            while (stack.Count > 0)
            {
                AddressableAssetSettings.AssetGroup.AssetEntry current = stack.Pop();

                if (current.address.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    current.address.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var item = new AssetEntryTreeViewItem(current, 0);
                    rows.Add(item);
                    List<AddressableAssetSettings.AssetGroup.AssetEntry> subAssets = null;
                    current.GetSubAssets(out subAssets, editor.settings);
                    if (subAssets != null && subAssets.Count > 0)
                    {
                        foreach (var e in subAssets)
                        {
                            stack.Push(e);
                        }
                    }
                }
            }
            //SortSearchResult(result);
        }

        void AddGroupChildrenBuild(AddressableAssetSettings.AssetGroup group, TreeViewItem root)
        {
            var groupItem = new AssetEntryTreeViewItem(group, 0);
            root.AddChild(groupItem);
            if (group.entries.Count > 0)
            {
                
                    foreach (var entry in group.entries)
                    {
                        AddAndRecurseEntriesBuild(entry, groupItem, 1);
                    }
                
            }
        }

        void AddAndRecurseEntriesBuild(AddressableAssetSettings.AssetGroup.AssetEntry entry, AssetEntryTreeViewItem parent, int depth)
        {
            var item = new AssetEntryTreeViewItem(entry, depth);
            parent.AddChild(item);
            //rows.Add(item);
            List<AddressableAssetSettings.AssetGroup.AssetEntry> subAssets = null;
            entry.GetSubAssets(out subAssets, editor.settings);
            if (subAssets != null && subAssets.Count > 0)
            {
                
                    foreach (var e in subAssets)
                    {
                        AddAndRecurseEntriesBuild(e, item, depth + 1);
                    }
                
            }
        }

        public override void OnGUI(Rect rect)
        {
            base.OnGUI(rect);

            //TODO - this occasionally causes a "hot control" issue.
            if (Event.current.type == EventType.MouseDown &&
                Event.current.button == 0 &&
                rect.Contains(Event.current.mousePosition))
            {
                SetSelection(new int[0], TreeViewSelectionOptions.FireSelectionChanged);
            }
        }

        protected override void BeforeRowsGUI()
        {
            base.BeforeRowsGUI();

            if (Event.current.type == EventType.Repaint)
            {
                var rows = GetRows();
                if (rows.Count > 0)
                {
                    int first;
                    int last;
                    GetFirstAndLastVisibleRows(out first, out last);
                    for (int rowID = first; rowID <= last; rowID++)
                    {
                        var aeI = rows[rowID] as AssetEntryTreeViewItem;
                        if (aeI != null && aeI.entry != null)
                        {
                            DefaultStyles.backgroundEven.Draw(GetRowRect(rowID), false, false, false, false);
                        }
                    }
                }
            }
        }

        GUIStyle labelStyle = null;
        protected override void RowGUI(RowGUIArgs args)
        {
            if (labelStyle == null)
            {
                labelStyle = new GUIStyle("PR Label");
                if (labelStyle == null)
                    labelStyle = GUI.skin.GetStyle("Label");
            }

            var item = args.item as AssetEntryTreeViewItem;
            if (item == null)
            {
                base.RowGUI(args);
            }
            else if (item.group != null)
            {
                using (new EditorGUI.DisabledScope(item.group.readOnly))
                {
                    base.RowGUI(args);
                }
            }
            else if (item.entry != null)
            {
                using (new EditorGUI.DisabledScope(item.entry.readOnly))
                {
                    for (int i = 0; i < args.GetNumVisibleColumns(); ++i)
                    {
                        CellGUI(args.GetCellRect(i), item, args.GetColumn(i), ref args);
                    }
                }
            }
        }

        private void CellGUI(Rect cellRect, AssetEntryTreeViewItem item, int column, ref RowGUIArgs args)
        {



            CenterRectUsingSingleLineHeight(ref cellRect);

            
            switch ((ColumnID)column)
            {
                case ColumnID.Id:
                    {
                        // The rect is assumed indented and sized after the content when pinging
                        float indent = GetContentIndent(item) + extraSpaceBeforeIconAndLabel;
                        cellRect.xMin += indent;

                        if (Event.current.type == EventType.Repaint)
                            labelStyle.Draw(cellRect, item.entry.address, false, false, args.selected, args.focused);
                    }
                    break;
                case ColumnID.Path:
                    if (Event.current.type == EventType.Repaint)
                        labelStyle.Draw(cellRect, item.entry.assetPath, false, false, args.selected, args.focused);
                    break;
                case ColumnID.Type:
                    if (item.assetIcon != null)
                        GUI.DrawTexture(cellRect, item.assetIcon, ScaleMode.ScaleToFit, true);
                    break;
                case ColumnID.Labels:
                    if (EditorGUI.DropdownButton(cellRect, new GUIContent(editor.settings.labelTable.GetString(item.entry.labels, cellRect.width)), FocusType.Passive))
                    {
                        var selection = GetItemsForContext(args.item.id);
                        Dictionary<string, int> labelCounts = new Dictionary<string, int>();
                        List<AddressableAssetSettings.AssetGroup.AssetEntry> entries = new List<AddressableAssetSettings.AssetGroup.AssetEntry>();
                        bool readOnly = false;
                        foreach (var s in selection)
                        {
                            var aeItem = FindItem(s, rootItem) as AssetEntryTreeViewItem;
                            if (aeItem == null || aeItem.entry == null)
                                continue;

                            entries.Add(aeItem.entry);
                            readOnly |= aeItem.entry.readOnly;
                            foreach (var label in aeItem.entry.labels)
                            {
                                var count = 0;
                                labelCounts.TryGetValue(label, out count);
                                count++;
                                labelCounts[label] = count;
                            }
                        }
                        PopupWindow.Show(cellRect, new LabelMaskPopupContent(editor.settings, entries, labelCounts));
                    }
                    break;

            }
        }
        private IList<int> GetItemsForContext(int row)
        {
            var selection = GetSelection();
            if (selection.Contains(row))
                return selection;

            selection = new List<int>();
            selection.Add(row);
            return selection;
        }
        protected override void SelectionChanged(IList<int> selectedIds)
        {
            base.SelectionChanged(selectedIds);
            editor.m_details.entries.Clear();
            editor.m_details.group = null;
            if (selectedIds.Count == 0)
                return;
            if (selectedIds.Count == 1)
            {
                var e = FindItemInVisibleRows(selectedIds[0]);
                if (e != null)
                {
                    if (e.group != null)
                        editor.m_details.group = e.group;
                    if (e.entry != null)
                        editor.m_details.entries.Add(e.entry);
                }
            }
            else
            {
                foreach (var i in selectedIds)
                {
                    var e = FindItemInVisibleRows(i);
                    if (e != null)
                    {
                        if (e.entry == null)
                        {
                            editor.m_details.entries.Clear();
                            editor.m_details.group = null;
                            break;
                        }
                        editor.m_details.entries.Add(e.entry);
                    }
                }
            }
        }

        public static MultiColumnHeaderState CreateDefaultMultiColumnHeaderState()
        {
            return new MultiColumnHeaderState(GetColumns());
        }

        private static MultiColumnHeaderState.Column[] GetColumns()
        {
            var retVal = new MultiColumnHeaderState.Column[] {
                new MultiColumnHeaderState.Column(),
                new MultiColumnHeaderState.Column(),
                new MultiColumnHeaderState.Column(),
                new MultiColumnHeaderState.Column(),
                //new MultiColumnHeaderState.Column(),
            };

            int counter = 0;
            
            retVal[counter].headerContent = new GUIContent("Address", "Asset address");
            retVal[counter].minWidth = 100;
            retVal[counter].width = 260;
            retVal[counter].maxWidth = 500;
            retVal[counter].headerTextAlignment = TextAlignment.Left;
            retVal[counter].canSort = false;
            retVal[counter].autoResize = true;
            counter++;

            retVal[counter].headerContent = new GUIContent(EditorGUIUtility.FindTexture("FilterByType"), "Asset type");
            retVal[counter].minWidth = 20;
            retVal[counter].width = 20;
            retVal[counter].maxWidth = 20;
            retVal[counter].headerTextAlignment = TextAlignment.Left;
            retVal[counter].canSort = false;
            retVal[counter].autoResize = true;
            counter++;

            retVal[counter].headerContent = new GUIContent("Path", "Current Path of asset");
            retVal[counter].minWidth = 100;
            retVal[counter].width = 150;
            retVal[counter].maxWidth = 10000;
            retVal[counter].headerTextAlignment = TextAlignment.Left;
            retVal[counter].canSort = false;
            retVal[counter].autoResize = true;
            counter++;

            retVal[counter].headerContent = new GUIContent("Labels", "Assets can have multiple labels");
            retVal[counter].minWidth = 20;
            retVal[counter].width = 160;
            retVal[counter].maxWidth = 1000;
            retVal[counter].headerTextAlignment = TextAlignment.Left;
            retVal[counter].canSort = false;
            retVal[counter].autoResize = true;
            counter++;

            return retVal;
        }

        protected override bool CanRename(TreeViewItem item)
        {
            var assetItem = item as AssetEntryTreeViewItem;
            if (assetItem != null)
            {
                if (assetItem.group != null)
                    return !assetItem.group.readOnly;
                if (assetItem.entry != null)
                    return !assetItem.entry.readOnly;
            }
            return false;
        }

        private AssetEntryTreeViewItem FindItemInVisibleRows(int id)
        {
            var rows = GetRows();
            foreach (var r in rows)
            {
                if (r.id == id)
                {
                    return r as AssetEntryTreeViewItem;
                }
            }
            return null;
        }

        protected override void RenameEnded(RenameEndedArgs args)
        {
            var item = FindItemInVisibleRows(args.itemID);
            if (item != null)
            {
                if (item.entry != null)
                    item.entry.address = args.newName;
                else if (item.group != null)
                    item.group.name = args.newName;
                Reload();
            }
        }

        protected override void DoubleClickedItem(int id)
        {
            var item = FindItemInVisibleRows(id);
            if (item != null && item.entry != null)
            {
                UnityEngine.Object o = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(item.entry.assetPath);
                EditorGUIUtility.PingObject(o);
                Selection.activeObject = o;
            }
        }

        bool m_ContextOnItem = false;
        List<Type> m_processorTypes = null;
        protected override void ContextClicked()
        {
            if (m_ContextOnItem)
            {
                m_ContextOnItem = false;
                return;
            }

            if (m_processorTypes == null)
                FindProcessorTypes();

            GenericMenu menu = new GenericMenu();


            foreach (var ty in AssetGroupProcessorManager.types)
            {
                var name = string.Empty;
                var attr = ty.GetCustomAttributes(true);
                foreach (var a in attr)
                {
                    var custAttr = a as System.ComponentModel.DescriptionAttribute;
                    if (custAttr != null)
                    {
                        name = custAttr.Description;
                    }
                }

                if (name != string.Empty)
                    menu.AddItem(new GUIContent("Create New Group/" + name), false, CreateNewGroup, ty.FullName);
            }

            var bundleList = AssetDatabase.GetAllAssetBundleNames();
            if (bundleList != null && bundleList.Length > 0)
                menu.AddItem(new GUIContent("Convert Legacy Bundles"), false, AddressablesUtility.ConvertAssetBundlesToAddressables);

            menu.ShowAsContext();
        }

        protected override void ContextClickedItem(int id)
        {
            List<AssetEntryTreeViewItem> selectedNodes = new List<AssetEntryTreeViewItem>();
            foreach (var nodeID in GetSelection())
            {
                var item = FindItemInVisibleRows(nodeID) as AssetEntryTreeViewItem; //TODO - this probably makes off-screen but selected items not get added to list.
                if (item != null)
                    selectedNodes.Add(item);
            }
            if (selectedNodes.Count == 0)
                return;

            m_ContextOnItem = true;

            bool isGroup = false;
            bool isEntry = false;
            bool hasReadOnly = false;
            int resourceCount = 0;
            int sceneListCount = 0;
            bool isResourcesHeader = false;
            foreach (var item in selectedNodes)
            {
                if (item.group != null)
                {
                    hasReadOnly |= item.group.readOnly;
                    isGroup = true;
                }
                else if (item.entry != null)
                {
                    if (item.entry.assetPath == AddressableAssetSettings.AssetGroup.AssetEntry.ResourcesPath)
                    {
                        if (selectedNodes.Count > 1)
                            return;
                        isResourcesHeader = true;
                    }
                    hasReadOnly |= item.entry.readOnly;
                    isEntry = true;
                    resourceCount += item.entry.isInResources ? 1 : 0;
                    sceneListCount += item.entry.isInSceneList ? 1 : 0;
                }
            }
            if (isEntry && isGroup)
                return;

            GenericMenu menu = new GenericMenu();
            if (isResourcesHeader)
            {
                foreach (var g in editor.settings.groups)
                {
                    if (g.name != AddressableAssetSettings.PlayerDataGroupName)
                        menu.AddItem(new GUIContent("Move ALL Resources to group/" + g.name), false, MoveAllResourcesToGroup, g);
                }
            }
            else if (!hasReadOnly)
            {
                if (isGroup)
                {
                    foreach (var ty in AssetGroupProcessorManager.types)
                    {
                        var name = string.Empty;
                        var attr = ty.GetCustomAttributes(true);
                        foreach (var a in attr)
                        {
                            var custAttr = a as System.ComponentModel.DescriptionAttribute;
                            if (custAttr != null)
                            {
                                name = custAttr.Description;
                            }
                        }

                        if (name != string.Empty)
                        {
                            menu.AddItem(new GUIContent("Convert Group Type/" + name), false, ConvertGroupsToProcessor, new KeyValuePair<string, List<AssetEntryTreeViewItem>>(ty.FullName, selectedNodes));
                            if (selectedNodes.Count > 1)
                                menu.AddItem(new GUIContent("Merge Groups/" + name), false, MergeGroupsToNewGroup, new KeyValuePair<string, List<AssetEntryTreeViewItem>>(ty.FullName, selectedNodes));
                        }
                    }
                    menu.AddItem(new GUIContent("Remove Group(s)"), false, RemoveGroup, selectedNodes);
                }
                if (isEntry)
                {
                    foreach (var g in editor.settings.groups)
                    {
                        if (g.name != AddressableAssetSettings.PlayerDataGroupName)
                            menu.AddItem(new GUIContent("Move entries to group/" + g.name), false, MoveEntriesToGroup, g);
                    }
                    menu.AddItem(new GUIContent("Remove Entry(s)"), false, RemoveEntry, selectedNodes);
                    menu.AddItem(new GUIContent("Simplify Entry Names"), false, SimplifyAddresses, selectedNodes);
                }
            }
            else
            {
                if (isEntry)
                {
                    if (resourceCount == selectedNodes.Count)
                    {
                        foreach (var g in editor.settings.groups)
                        {
                            if (g.name != AddressableAssetSettings.PlayerDataGroupName)
                                menu.AddItem(new GUIContent("Move entries to group/" + g.name), false, MoveResourcesToGroup, g);
                        }
                    }
                    else if (resourceCount == 0)
                    {
                        foreach (var g in editor.settings.groups)
                        {
                            if (g.name != AddressableAssetSettings.PlayerDataGroupName)
                                menu.AddItem(new GUIContent("Move entries to group/" + g.name), false, MoveEntriesToGroup, g);
                        }
                    }
                }
            }

            if (selectedNodes.Count == 1)
            {
                if (CanRename(selectedNodes.First()))
                    menu.AddItem(new GUIContent("Rename"), false, RenameItem, selectedNodes);
            }
            menu.ShowAsContext();
        }

        void MoveAllResourcesToGroup(object context)
        {
            var targetGroup = context as AddressableAssetSettings.AssetGroup;
            var firstID = GetSelection().First();
            var item = FindItemInVisibleRows(firstID) as AssetEntryTreeViewItem;
            if (item != null)
            {
                SafeMoveResourcesToGroup(targetGroup, item.children.ConvertAll(instance => (AssetEntryTreeViewItem)instance));
            }
        }

        void MoveResourcesToGroup(object context)
        {
            var targetGroup = context as AddressableAssetSettings.AssetGroup;
            var itemList = new List<AssetEntryTreeViewItem>();
            foreach (var nodeID in GetSelection())
            {
                var item = FindItemInVisibleRows(nodeID) as AssetEntryTreeViewItem;
                if (item != null)
                    itemList.Add(item);
            }

            SafeMoveResourcesToGroup(targetGroup, itemList);
        }

        void SafeMoveResourcesToGroup(AddressableAssetSettings.AssetGroup targetGroup, List<AssetEntryTreeViewItem> itemList)
        {
            Dictionary<string, string> guidToNewPath = new Dictionary<string, string>();

            var message = "Addressable assets moved out of Resources must have their asset files moved within the project. We will move the files to:\n\n";
            foreach (var item in itemList)
            {
                var newName = item.entry.assetPath.Replace("\\", "/");
                newName = newName.Replace("Resources", "Resources_moved");
                newName = newName.Replace("resources", "resources_moved");

                guidToNewPath.Add(item.entry.guid, newName);
                message += newName + "\n";
            }
            message += "\nAre you sure you want to proceed?";
            if (EditorUtility.DisplayDialog("Move From Resources", message, "Yes", "No"))
            {
                editor.settings.MoveAssetsFromResources(guidToNewPath, targetGroup);
            }
        }

        void MoveEntriesToGroup(object context)
        {
            var targetGroup = context as AddressableAssetSettings.AssetGroup;
            foreach (var nodeID in GetSelection())
            {
                var item = FindItemInVisibleRows(nodeID) as AssetEntryTreeViewItem;
                if (item != null)
                    editor.settings.CreateOrMoveEntry(item.entry.guid, targetGroup);
            }
        }

        void FindProcessorTypes()
        {
            m_processorTypes = new List<Type>();
        }

        protected void CreateNewGroup(object context)
        {
            var name = context as string;
            editor.settings.CreateGroup("", name);
            Reload();
        }

        protected void RemoveGroup(object context)
        {
            if (EditorUtility.DisplayDialog("Group Deletion", "Are you sure you want to delete the selected groups?", "Yes", "No"))
            {
                List<AssetEntryTreeViewItem> selectedNodes = context as List<AssetEntryTreeViewItem>;

                foreach (var item in selectedNodes)
                {
                    editor.settings.RemoveGroup(item.group);
                }
                Reload();
            }
        }
        protected void ConvertGroupsToProcessor(object context)
        {
            var data = (KeyValuePair<string, List<AssetEntryTreeViewItem>>)context;
            foreach (var item in data.Value)
                editor.settings.ConvertGroup(item.group, data.Key);
            Reload();
        }


        protected void MergeGroupsToNewGroup(object context)
        {
            var data = (KeyValuePair<string, List<AssetEntryTreeViewItem>>)context;
            var newGroup = editor.settings.CreateGroup("", data.Key);
            foreach (var item in data.Value)
            {
                var g = item.group;
                foreach (var e in g.entries)
                    newGroup.AddAssetEntry(e);
                editor.settings.RemoveGroup(g);
            }
            Reload();
        }
        /*
        protected void ExtractCommonAssets(object context)
        {
            var data = (KeyValuePair<string, List<AssetEntryTreeViewItem>> )context;
            var groups = new List<AddressableAssetSettings.AssetGroup>();
            foreach (var item in data.Value)
                if (item.group != null)
                    groups.Add(item.group);

            var common = BuildScript.ExtractDuplicates(editor.settings, groups);
            if (common.Count > 0)
            {
                var group = editor.settings.CreateGroup("Common", data.Key);
                foreach (var guid in common)
                    editor.settings.CreateOrMoveEntry(guid, group);

                Reload();
            }
        }
        */
        protected void SimplifyAddresses(object context)
        {
            List<AssetEntryTreeViewItem> selectedNodes = context as List<AssetEntryTreeViewItem>;

            foreach (var item in selectedNodes)
                item.entry.address = System.IO.Path.GetFileNameWithoutExtension(item.entry.address);
            Reload();
        }

        protected void RemoveEntry(object context)
        {
            List<AssetEntryTreeViewItem> selectedNodes = context as List<AssetEntryTreeViewItem>;

            foreach (var item in selectedNodes)
            {
                editor.settings.RemoveAssetEntry(item.entry.guid);
            }
            Reload();
        }

        protected void RenameItem(object context)
        {
            List<AssetEntryTreeViewItem> selectedNodes = context as List<AssetEntryTreeViewItem>;
            if (selectedNodes.Count >= 1)
            {
                var item = selectedNodes.First();
                if (CanRename(item))
                    BeginRename(item);
            }
        }

        public class AssetGroupProcessorManager
        {
            static private List<Type> m_types;
            static public List<Type> types
            {
                get
                {
                    if (m_types == null)
                    {
                        m_types = new List<Type>();
                        var rootType = typeof(AssetGroupProcessor);
                        var listOfTypes = (from domainAssembly in AppDomain.CurrentDomain.GetAssemblies()
                                           from assemblyType in domainAssembly.GetExportedTypes()
                                           where rootType.IsAssignableFrom(assemblyType)
                                           select assemblyType).ToArray();

                        foreach (var t in listOfTypes)
                        {
                            if (t != rootType)
                                m_types.Add(t);
                        }
                    }
                    return m_types;
                }
            }
        }


        protected override bool CanBeParent(TreeViewItem item)
        {
            var aeItem = item as AssetEntryTreeViewItem;
            if (aeItem != null && aeItem.group != null)
                return true;
            
            return false;
        }

        protected override void KeyEvent()
        {
            if (Event.current.keyCode == KeyCode.Delete && GetSelection().Count > 0)
            {
                List<AssetEntryTreeViewItem> selectedNodes = new List<AssetEntryTreeViewItem>();
                foreach (var nodeID in GetSelection())
                {
                    var item = FindItemInVisibleRows(nodeID) as AssetEntryTreeViewItem;
                    if (item != null)
                        selectedNodes.Add(item);
                }
                RemoveEntry(selectedNodes);
            }
        }

        protected override bool CanStartDrag(CanStartDragArgs args)
        {
            int resourcesCount = 0;
            foreach (var id in args.draggedItemIDs)
            {
                var item = FindItemInVisibleRows(id) as AssetEntryTreeViewItem;
                if (item != null)
                {
                    //can't drag groups
                    if (item.group != null)
                        return false;

                    if (item.entry != null)
                    {
                        //can't drag the root "EditorSceneList" entry
                        if (item.entry.guid == AddressableAssetSettings.AssetGroup.AssetEntry.EditorSceneListName)
                            return false;

                        //can't drag the root "Resources" entry
                        if (item.entry.guid == AddressableAssetSettings.AssetGroup.AssetEntry.ResourcesName)
                            return false;

                        //if we're dragging resources, we should _only_ drag resources.
                        if (item.entry.isInResources)
                            resourcesCount++;
                    }
                }
            }
            if ((resourcesCount > 0) && (resourcesCount < args.draggedItemIDs.Count))
                return false;

            return true;
        }

        List<UnityEngine.Object> m_EmptyObjectList = new List<UnityEngine.Object>();
        protected override void SetupDragAndDrop(SetupDragAndDropArgs args)
        {
            DragAndDrop.PrepareStartDrag();

            var selectedNodes = new List<AssetEntryTreeViewItem>();
            foreach (var id in args.draggedItemIDs)
            {
                var item = FindItemInVisibleRows(id) as AssetEntryTreeViewItem;
                selectedNodes.Add(item);
            }
            DragAndDrop.paths = null;
            DragAndDrop.objectReferences = m_EmptyObjectList.ToArray();
            DragAndDrop.SetGenericData("AssetEntryTreeViewItem", selectedNodes);
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            DragAndDrop.StartDrag("AssetBundleTree");
        }
        
        protected override DragAndDropVisualMode HandleDragAndDrop(DragAndDropArgs args)
        {
            DragAndDropVisualMode visualMode = DragAndDropVisualMode.None;

            var target = args.parentItem as AssetEntryTreeViewItem;
            if (target == null)
                return DragAndDropVisualMode.None;

            if (target.entry != null && target.entry.readOnly)
                return DragAndDropVisualMode.None;


            if (DragAndDrop.paths != null && DragAndDrop.paths.Length > 0)
            {
                visualMode = DragAndDropVisualMode.Copy;
                if (args.performDrop)
                {
                    AddressableAssetSettings.AssetGroup parent = null;
                    if (target.group != null)
                        parent = target.group;
                    else if (target.entry != null)
                        parent = target.entry.parentGroup;

                    if (parent != null)
                    {
                        foreach (var p in DragAndDrop.paths)
                            editor.settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(p), parent);
                        Reload();
                    }
                }
            }
            else
            {
                var draggedNodes = DragAndDrop.GetGenericData("AssetEntryTreeViewItem") as List<AssetEntryTreeViewItem>;
                if (draggedNodes != null && draggedNodes.Count > 0)
                {
                    visualMode = DragAndDropVisualMode.Copy;
                    if (args.performDrop)
                    {
                        AddressableAssetSettings.AssetGroup parent = null;
                        if (target.group != null)
                            parent = target.group;
                        else if (target.entry != null)
                            parent = target.entry.parentGroup;

                        if (parent != null)
                        {
                            if (draggedNodes.First().entry.isInResources)
                            {
                                SafeMoveResourcesToGroup(parent, draggedNodes);
                            }
                            else
                            {
                                foreach (var node in draggedNodes)
                                    editor.settings.CreateOrMoveEntry(node.entry.guid, parent);
                            }
                            Reload();
                        }
                    }
                }
            }

            return visualMode;
        }
    }

    internal class AssetEntryTreeViewItem : TreeViewItem
    {
        public AddressableAssetSettings.AssetGroup.AssetEntry entry;
        public AddressableAssetSettings.AssetGroup group;
        public Texture2D assetIcon;

        public AssetEntryTreeViewItem(AddressableAssetSettings.AssetGroup.AssetEntry e, int d) : base((e.address + e.guid).GetHashCode(), d, e.address)
        {
            entry = e;
            group = null;
            assetIcon = AssetDatabase.GetCachedIcon(e.assetPath) as Texture2D;
        }

        public AssetEntryTreeViewItem(AddressableAssetSettings.AssetGroup g, int d) : base(g.guid.GetHashCode(), d, g.displayName)
        {
            entry = null;
            group = g;
            assetIcon = null;
        }
    }

   


    class LabelMaskPopupContent : PopupWindowContent
    {
        AddressableAssetSettings m_settings;
        List<AddressableAssetSettings.AssetGroup.AssetEntry> m_entries;
        Dictionary<string, int> m_labelCount;

        GUIStyle toggleMixed = null;


        int lastItemCount = -1;
        Vector2 rect = new Vector2();
        public LabelMaskPopupContent(AddressableAssetSettings settings, List<AddressableAssetSettings.AssetGroup.AssetEntry> e, Dictionary<string, int> count)
        {
            m_settings = settings;
            m_entries = e;
            m_labelCount = count;
        }

        public override Vector2 GetWindowSize()
        {
            var labelTable = m_settings.labelTable;
            if (lastItemCount != labelTable.labelNames.Count)
            {
                int maxLen = 0;
                string maxStr = "";
                for (int i = 0; i < labelTable.labelNames.Count; i++)
                {
                    var len = labelTable.labelNames[i].Length;
                    if (len > maxLen)
                    {
                        maxLen = len;
                        maxStr = labelTable.labelNames[i];
                    }
                }
                float minWidth, maxWidth;
                var content = new GUIContent(maxStr);
                GUI.skin.toggle.CalcMinMaxWidth(content, out minWidth, out maxWidth);
                var height = GUI.skin.toggle.CalcHeight(content, maxWidth) + 3.5f;
                rect = new Vector2(Mathf.Clamp(maxWidth + 100, 300, 600), Mathf.Clamp(labelTable.labelNames.Count * height + 25, 100, 65 * height));
                lastItemCount = labelTable.labelNames.Count;
            }
            return rect;
        }

        private void SetValueOnEntries(string label, bool value)
        {
            foreach (var e in m_entries)
            {
                e.SetLabel(label, value);
            }
            m_labelCount[label] = value ? m_entries.Count : 0;
        }

        bool focusTextEntry = false;
        public override void OnGUI(Rect rect)
        {
            if (m_entries.Count == 0)
                return;


            var labelTable = m_settings.labelTable;

            GUILayout.BeginArea(new Rect(rect.xMin + 3, rect.yMin + 3, rect.width - 6, rect.height - 6));

            string toRemove = null;
            foreach (var labelName in labelTable.labelNames)
            {
                EditorGUILayout.BeginHorizontal();

                bool oldState = false;
                bool newState = false;
                int count = 0;
                if (m_labelCount == null)
                    count = m_entries[0].labels.Contains(labelName) ? m_entries.Count : 0;
                else
                    m_labelCount.TryGetValue(labelName, out count);

                if (count == 0)
                {
                    newState = GUILayout.Toggle(oldState, new GUIContent(labelName), GUILayout.ExpandWidth(false));
                }
                else if (count == m_entries.Count)
                {
                    oldState = true;
                    newState = GUILayout.Toggle(oldState, new GUIContent(labelName), GUILayout.ExpandWidth(false));
                }
                else
                {
                    if (toggleMixed == null)
                        toggleMixed = new GUIStyle("ToggleMixed");
                    //if (GUILayout.Toggle(false, addressableAssetToggleText, toggleMixed, GUILayout.ExpandWidth(false)))
                    newState = GUILayout.Toggle(oldState, new GUIContent(labelName), toggleMixed, GUILayout.ExpandWidth(false));
                }
                if (oldState != newState)
                {
                    SetValueOnEntries(labelName, newState);
                }

                GUILayout.FlexibleSpace();
                if (GUILayout.Button(new GUIContent("X"), GUILayout.ExpandWidth(false)))
                    toRemove = labelName;
                EditorGUILayout.EndHorizontal();
            }
            if (toRemove != null)
                labelTable.RemoveLabelName(toRemove);

            if (labelTable.labelNames.Count < 64)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(new GUIContent("New"), GUILayout.Width(50), GUILayout.ExpandWidth(false));
                GUI.SetNextControlName("MaskTextEntry");
                var newEntry = EditorGUILayout.DelayedTextField("", GUILayout.ExpandWidth(true));
                if (focusTextEntry)
                {
                    focusTextEntry = false;
                    GUI.FocusWindow(GUIUtility.hotControl);
                    EditorGUI.FocusTextInControl("MaskTextEntry");
                }
                if (!string.IsNullOrEmpty(newEntry))
                {
                    focusTextEntry = true;
                    labelTable.AddLabelName(newEntry);
                    SetValueOnEntries(newEntry, true);
                }
                EditorGUILayout.EndHorizontal();
            }
            GUILayout.EndArea();
        }
    }

    internal class AssetDetailsView
    {
        public AddressableAssetSettings.AssetGroup group;
        public List<AddressableAssetSettings.AssetGroup.AssetEntry> entries = new List<AddressableAssetSettings.AssetGroup.AssetEntry>();

        public bool SelectionHasSettings()
        {
            bool result = false;
            if(group != null)
            {
                result = group.HasSettings();
                result &= entries.Count == 0;
            }
            else
            {
                result = entries.Count > 0;
                foreach (var e in entries)
                { 
                    if(e != null)
                        result &= e.HasSettings();
                }
            }
            return result;
        }
        Rect buttonRect, toolbarRect;
        public void OnGUI(AddressableAssetSettings settings, Rect pos)
        {
            MyExtensionMethods.DrawOutline(pos, 1f);
            if (entries.Count > 0)
            {
                //TODO - have the ability for entries to somehow show some GUI if the user has created a custom "entry" type.
            }
            else if (group != null && group.processor != null)
            {
                group.processor.OnDrawGUI(settings, pos);
            }
        }
    }

    //TODO - ideally need to get rid of this
    static class MyExtensionMethods
    {
        public static IOrderedEnumerable<T> Order<T, TKey>(this IEnumerable<T> source, Func<T, TKey> selector, bool ascending)
        {
            if (ascending)
            {
                return source.OrderBy(selector);
            }
            else
            {
                return source.OrderByDescending(selector);
            }
        }

        public static IOrderedEnumerable<T> ThenBy<T, TKey>(this IOrderedEnumerable<T> source, Func<T, TKey> selector, bool ascending)
        {
            if (ascending)
            {
                return source.ThenBy(selector);
            }
            else
            {
                return source.ThenByDescending(selector);
            }
        }

        internal static void DrawOutline(Rect rect, float size)
        {
            Color color = new Color(0.6f, 0.6f, 0.6f, 1.333f);
            if (EditorGUIUtility.isProSkin)
            {
                color.r = 0.12f;
                color.g = 0.12f;
                color.b = 0.12f;
            }

            if (Event.current.type != EventType.Repaint)
                return;

            Color orgColor = GUI.color;
            GUI.color = GUI.color * color;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, size), EditorGUIUtility.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - size, rect.width, size), EditorGUIUtility.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y + 1, size, rect.height - 2 * size), EditorGUIUtility.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMax - size, rect.y + 1, size, rect.height - 2 * size), EditorGUIUtility.whiteTexture);

            GUI.color = orgColor;
        }
    }
}
