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

        [NonSerialized]
        GUIStyle searchStyle = null;
        [NonSerialized]
        GUIStyle buttonStyle = null;
        bool previewMode = false;
        [NonSerialized]
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
                var baseSearch = ProjectConfigData.hierarchicalSearch ? entryTree.customSearchString : entryTree.searchString;
                var searchString = searchField.OnToolbarGUI(searchRect, baseSearch);
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

                GUILayout.Space(spaceBetween);
                var gc = new GUIContent(cogIcon);
                Rect r = GUILayoutUtility.GetRect(gc, buttonStyle);
                if (EditorGUI.DropdownButton(r, gc, FocusType.Passive,buttonStyle))
                {
                    if(speed == SpeedSettings.undefined)
                    {
                        if (ProjectConfigData.localLoadSpeed == 1024 * 1024 * 10)
                            speed = SpeedSettings.medium;
                        else if (ProjectConfigData.localLoadSpeed < 1024 * 1024 * 10)
                            speed = SpeedSettings.low;
                        else
                            speed = SpeedSettings.high;

                    }
                    var menu = new GenericMenu();
                    bool fast = ProjectConfigData.editorPlayMode == UnityEngine.AddressableAssets.ResourceManagerRuntimeData.EditorPlayMode.FastMode;
                    bool virt = ProjectConfigData.editorPlayMode == UnityEngine.AddressableAssets.ResourceManagerRuntimeData.EditorPlayMode.VirtualMode;
                    bool packed = ProjectConfigData.editorPlayMode == UnityEngine.AddressableAssets.ResourceManagerRuntimeData.EditorPlayMode.PackedMode;
                    menu.AddItem(new GUIContent("Play Mode/Fast"), fast, OnFastClick);
                    menu.AddItem(new GUIContent("Play Mode/Virtual"), virt, OnVirtualClick);
                    menu.AddItem(new GUIContent("Play Mode/Packed"), packed, OnPackedClick);
                    menu.AddSeparator("Play Mode/");
                    menu.AddItem(new GUIContent("Play Mode/Virtual Settings/3G speeds"), speed == SpeedSettings.low, OnVirtual3GSpeedClick);
                    menu.AddItem(new GUIContent("Play Mode/Virtual Settings/High-Speed"), speed == SpeedSettings.medium, OnVirtualHighSpeedClick);
                    menu.AddItem(new GUIContent("Play Mode/Virtual Settings/Max Speed"), speed == SpeedSettings.high, OnVirtualInfiniteSpeedClick);
                    menu.AddItem(new GUIContent("Send Profile Events"), ProjectConfigData.postProfilerEvents, OnSendProfileClick);
                    menu.AddItem(new GUIContent("Hierarchical Search"), ProjectConfigData.hierarchicalSearch, OnHierSearchClick);
                    menu.DropDown(r);
                }
                GUILayout.Space(spaceBetween);
            }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }
        void OnFastClick()
        {
            ProjectConfigData.editorPlayMode = UnityEngine.AddressableAssets.ResourceManagerRuntimeData.EditorPlayMode.FastMode;
        }
        void OnVirtualClick()
        {
            ProjectConfigData.editorPlayMode = UnityEngine.AddressableAssets.ResourceManagerRuntimeData.EditorPlayMode.VirtualMode;
        }
        void OnPackedClick()
        {
            ProjectConfigData.editorPlayMode = UnityEngine.AddressableAssets.ResourceManagerRuntimeData.EditorPlayMode.PackedMode;
        }
        void OnVirtual3GSpeedClick()
        {
            speed = SpeedSettings.low;
            ProjectConfigData.localLoadSpeed = 1024 * 1024 * 3;
            ProjectConfigData.remoteLoadSpeed = 1024 * 100;     //100 KB/s
        }
        void OnVirtualHighSpeedClick()
        {
            speed = SpeedSettings.medium;
            ProjectConfigData.localLoadSpeed = 1024 * 1024 * 10;
            ProjectConfigData.remoteLoadSpeed = 1024 * 1024 * 1; //1 MB/s
        }
        void OnVirtualInfiniteSpeedClick()
        {
            speed = SpeedSettings.high;
            ProjectConfigData.localLoadSpeed = 1024 * 1024 * 100;
            ProjectConfigData.remoteLoadSpeed = 1024 * 1024 * 1024;    //1 GB/s
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
        }

        public void OnDisable()
        {
            if (settings == null)
                return;
            settings.OnModification -= OnSettingsModification;
            m_modificationRegistered = false;
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
        internal string customSearchString = string.Empty;


        public enum ColumnID
        {
            Id,
            Type,
            Path,
            Labels
        }
        ColumnID[] m_SortOptions =
        {
            ColumnID.Id,
            ColumnID.Type,
            ColumnID.Path,
            ColumnID.Labels
        };
        public AddressableAssetEntryTreeView(TreeViewState state, MultiColumnHeaderState mchs, AddressableAssetsSettingsGroupEditor ed) : base(state, new MultiColumnHeader(mchs))
        {
            showBorder = true;
            editor = ed;
            columnIndexForTreeFoldouts = 0;
            multiColumnHeader.sortingChanged += OnSortingChanged;
        }

        private void OnSortingChanged(MultiColumnHeader multiColumnHeader)
        {
            SortChildren(rootItem);
            Reload();
        }

        protected override TreeViewItem BuildRoot()
        {
            var root = new TreeViewItem(-1, -1);
            foreach (var group in editor.settings.groups)
                AddGroupChildrenBuild(group, root);
        
            return root;
        }

        protected override IList<TreeViewItem> BuildRows(TreeViewItem root)
        {
            SortChildren(root);
            var rows = base.BuildRows(root);
            if(!string.IsNullOrEmpty(customSearchString))
            {
                var z= rows.Where(s => DoesItemMatchSearch(s, customSearchString)).ToList();
                return z;
            }
            return rows;
        }

        internal void ClearSearch()
        {
            customSearchString = string.Empty;
            searchString = string.Empty;
        }
        private void SortChildren(TreeViewItem root)
        {
            foreach (var child in root.children)
            {
                if (child != null)
                    SortHierarchical(child.children);
            }
        }

        private void SortHierarchical(List<TreeViewItem> children)
        {
            if (children == null)
                return;

            var sortedColumns = multiColumnHeader.state.sortedColumns;
            if (sortedColumns.Length == 0)
                return;

            List<AssetEntryTreeViewItem> kids = new List<AssetEntryTreeViewItem>();
            foreach (var c in children)
            {
                var child = c as AssetEntryTreeViewItem;
                if(child.entry != null)
                    kids.Add(child);
            }
            
            ColumnID col = m_SortOptions[sortedColumns[0]];
            bool ascending = multiColumnHeader.IsSortedAscending(sortedColumns[0]);

            IEnumerable<AssetEntryTreeViewItem> orderedKids = kids;
            switch(col)
            {
                case ColumnID.Type:
                    break;
                case ColumnID.Path:
                    orderedKids = kids.Order(l => l.entry.assetPath, ascending);
                    break;
                case ColumnID.Labels:
                    orderedKids = OrderByLabels(kids, ascending);
                    break;
                case ColumnID.Id:
                default:
                    orderedKids = kids.Order(l => l.displayName, ascending);
                    break;
            }
            
            children.Clear();
            foreach (var o in orderedKids)
                children.Add(o as TreeViewItem);


            foreach (var child in children)
            {
                if (child != null)
                    SortHierarchical(child.children);
            }

        }

        private IEnumerable<AssetEntryTreeViewItem> OrderByLabels(List<AssetEntryTreeViewItem> kids, bool ascending)
        {
            var emptyHalf = new List<AssetEntryTreeViewItem>();
            var namedHalf = new List<AssetEntryTreeViewItem>();
            foreach(var k in kids)
            {
                if (k.entry == null || k.entry.labels == null || k.entry.labels.Count < 1)
                    emptyHalf.Add(k);
                else
                    namedHalf.Add(k);
            }
            var orderedKids = namedHalf.Order(l => editor.settings.labelTable.GetString(l.entry.labels,200), ascending);

            List<AssetEntryTreeViewItem> result = new List<AssetEntryTreeViewItem>();
            if (ascending)
            {
                result.AddRange(emptyHalf);
                result.AddRange(orderedKids);
            }
            else
            {
                result.AddRange(orderedKids);
                result.AddRange(emptyHalf);
            }
            
            return result;
        }
        protected override bool DoesItemMatchSearch(TreeViewItem item, string search)
        {
            var aeItem = item as AssetEntryTreeViewItem;
            if (ProjectConfigData.hierarchicalSearch)
            {
                //does this item match?
                if (DoesAEItemMatchSearch(aeItem, search))
                    return true;

                //else check if children match.
                if (item.children != null)
                {
                    foreach (var c in item.children)
                    {
                        if (DoesItemMatchSearch(c, search))
                            return true;
                    }
                }

                //nope.
                return false;
            }
            else
            {
                return DoesAEItemMatchSearch(aeItem, search);
            }
            //SortSearchResult(result);
        }

        protected bool DoesAEItemMatchSearch(AssetEntryTreeViewItem aeItem, string search)
        {
            if (aeItem == null || aeItem.entry == null)
                return false;
            
            //check if item matches.
            if (aeItem.displayName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            if (aeItem.entry.assetPath.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (editor.settings.labelTable.GetString(aeItem.entry.labels, 200).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            return false;
        }

        void AddGroupChildrenBuild(AddressableAssetGroup group, TreeViewItem root)
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

        void AddAndRecurseEntriesBuild(AddressableAssetEntry entry, AssetEntryTreeViewItem parent, int depth)
        {
            var item = new AssetEntryTreeViewItem(entry, depth);
            parent.AddChild(item);
            var subAssets = new List<AddressableAssetEntry>();
            entry.GatherAllAssets(subAssets, false, false);
            if (subAssets.Count > 0)
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
            else if (item.entry != null && !args.isRenaming)
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
                        List<AddressableAssetEntry> entries = new List<AddressableAssetEntry>();
                        bool readOnly = false;
                        var newSelection = new List<int>();
                        foreach (var s in selection)
                        {
                            var aeItem = FindItem(s, rootItem) as AssetEntryTreeViewItem;
                            if (aeItem == null || aeItem.entry == null)
                                continue;

                            entries.Add(aeItem.entry);
                            newSelection.Add(s);
                            readOnly |= aeItem.entry.readOnly;
                            foreach (var label in aeItem.entry.labels)
                            {
                                var count = 0;
                                labelCounts.TryGetValue(label, out count);
                                count++;
                                labelCounts[label] = count;
                            }
                        }
                        SetSelection(newSelection);
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
            
            retVal[counter].headerContent = new GUIContent("Asset Address", "Address used to load asset at runtime");
            retVal[counter].minWidth = 100;
            retVal[counter].width = 260;
            retVal[counter].maxWidth = 500;
            retVal[counter].headerTextAlignment = TextAlignment.Left;
            retVal[counter].canSort = true;
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
            retVal[counter].canSort = true;
            retVal[counter].autoResize = true;
            counter++;

            retVal[counter].headerContent = new GUIContent("Labels", "Assets can have multiple labels");
            retVal[counter].minWidth = 20;
            retVal[counter].width = 160;
            retVal[counter].maxWidth = 1000;
            retVal[counter].headerTextAlignment = TextAlignment.Left;
            retVal[counter].canSort = true;
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
                {
                    item.entry.address = args.newName;
                }
                else if (item.group != null)
                {
                    if (editor.settings.IsNotUniqueGroupName(args.newName))
                    {
                        args.acceptedRename = false;
                        Debug.LogWarning("There is already a group named '" + args.newName + "'.  Cannot rename this group to match");
                    }
                    else
                        item.group.name = args.newName;
                }
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
                    if (item.entry.assetPath == AddressableAssetEntry.ResourcesPath)
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
                    Type selectedProcessorType = selectedNodes[0].group.Procesor.GetType();
                    for(int i = 1; i < selectedNodes.Count; i++)
                    {
                        if (selectedNodes[i].group.Procesor.GetType() != selectedProcessorType)
                        {
                            selectedProcessorType = null;
                            break;
                        }
                    }
                    foreach (var ty in AssetGroupProcessorManager.types)
                    {
                        bool isSameType = (selectedProcessorType == ty);

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
                            if(!isSameType)
                                menu.AddItem(new GUIContent("Convert Group Type/" + name), false, ConvertGroupsToProcessor, new KeyValuePair<string, List<AssetEntryTreeViewItem>>(ty.FullName, selectedNodes));
                            if (selectedNodes.Count > 1)
                                menu.AddItem(new GUIContent("Merge Groups/" + name), false, MergeGroupsToNewGroup, new KeyValuePair<string, List<AssetEntryTreeViewItem>>(ty.FullName, selectedNodes));
                        }
                    }
                    menu.AddItem(new GUIContent("Extract Common Assets"), false, ExtractCommonAssets, new KeyValuePair<string, List<AssetEntryTreeViewItem>>(typeof(RemoteAssetBundleAssetGroupProcessor).FullName, selectedNodes));
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
                    menu.AddItem(new GUIContent("Export Entries..."), false, CreateExternalEntryCollection, selectedNodes);

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

        void CreateExternalEntryCollection(object context)
        {
            List<AssetEntryTreeViewItem> selectedNodes = context as List<AssetEntryTreeViewItem>;
            var col = ScriptableObject.CreateInstance<AddressablesEntryCollection>();
            foreach (var item in selectedNodes)
            {
                item.entry.readOnly = true;
                item.entry.isSubAsset = true;
                col.Entries.Add(item.entry);
                editor.settings.RemoveAssetEntry(item.entry.guid, false);
            }
            var path = EditorUtility.SaveFilePanel("Create Entry Collection", "Assets", "AddressableEntryCollection", "asset");
            if (!string.IsNullOrEmpty(path))
            {
                path = path.Substring(path.ToLower().IndexOf("assets/"));
                AssetDatabase.CreateAsset(col, path);
                AssetDatabase.Refresh();
                var guid = AssetDatabase.AssetPathToGUID(path);
                editor.settings.CreateOrMoveEntry(guid, editor.settings.DefaultGroup);
            }
        }

        void MoveAllResourcesToGroup(object context)
        {
            var targetGroup = context as AddressableAssetGroup;
            var firstID = GetSelection().First();
            var item = FindItemInVisibleRows(firstID) as AssetEntryTreeViewItem;
            if (item != null)
            {
                SafeMoveResourcesToGroup(targetGroup, item.children.ConvertAll(instance => (AssetEntryTreeViewItem)instance));
            }
        }

        void MoveResourcesToGroup(object context)
        {
            var targetGroup = context as AddressableAssetGroup;
            var itemList = new List<AssetEntryTreeViewItem>();
            foreach (var nodeID in GetSelection())
            {
                var item = FindItemInVisibleRows(nodeID) as AssetEntryTreeViewItem;
                if (item != null)
                    itemList.Add(item);
            }

            SafeMoveResourcesToGroup(targetGroup, itemList);
        }

        void SafeMoveResourcesToGroup(AddressableAssetGroup targetGroup, List<AssetEntryTreeViewItem> itemList)
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
            var targetGroup = context as AddressableAssetGroup;
            var entries = new List<AddressableAssetEntry>();
            foreach (var nodeID in GetSelection())
            {
                var item = FindItemInVisibleRows(nodeID) as AssetEntryTreeViewItem;
                if (item != null)
                    entries.Add(item.entry);
            }
            if (entries.Count > 0)
                editor.settings.MoveEntriesToGroup(entries, targetGroup);
        }

        void FindProcessorTypes()
        {
            m_processorTypes = new List<Type>();
        }

        protected void CreateNewGroup(object context)
        {
            editor.settings.CreateGroup("", context as string);
        }

        protected void RemoveGroup(object context)
        {
            if (EditorUtility.DisplayDialog("Group Deletion", "Are you sure you want to delete the selected groups?", "Yes", "No"))
            {
                List<AssetEntryTreeViewItem> selectedNodes = context as List<AssetEntryTreeViewItem>;
                var groups = new List<AddressableAssetGroup>();
                foreach (var item in selectedNodes)
                {
                    editor.settings.RemoveGroup(item.group, false);
                    groups.Add(item.group);
                }
                editor.settings.PostModificationEvent(AddressableAssetSettings.ModificationEvent.GroupRemoved, groups);
            }
        }
        protected void ConvertGroupsToProcessor(object context)
        {
            var data = (KeyValuePair<string, List<AssetEntryTreeViewItem>>)context;
            foreach (var item in data.Value)
                editor.settings.ConvertGroup(item.group, data.Key);
        }


        protected void MergeGroupsToNewGroup(object context)
        {
            var data = (KeyValuePair<string, List<AssetEntryTreeViewItem>>)context;
            var newGroup = editor.settings.CreateGroup("", data.Key);
            var entries = new List<AddressableAssetEntry>();
            var groups = new List<AddressableAssetGroup>();
            foreach (var item in data.Value)
            {
                var g = item.group;
                foreach (var e in g.entries)
                {
                    e.parentGroup.RemoveAssetEntry(e, false);
                    newGroup.AddAssetEntry(e, false);
                    entries.Add(e);
                }
                editor.settings.RemoveGroup(g, false);
                groups.Add(g);
            }
            editor.settings.PostModificationEvent(AddressableAssetSettings.ModificationEvent.GroupRemoved, groups);
            editor.settings.PostModificationEvent(AddressableAssetSettings.ModificationEvent.EntryMoved, entries);
        }

        protected void ExtractCommonAssets(object context)
        {
            var data = (KeyValuePair<string, List<AssetEntryTreeViewItem>> )context;
            var groups = new List<AddressableAssetGroup>();
            foreach (var item in data.Value)
                if (item.group != null)
                    groups.Add(item.group);

            var common = BuildScript.ExtractCommonAssets(editor.settings, groups);
            if (common.Count > 0)
            {
                var group = editor.settings.CreateGroup("Common", data.Key);
                var entries = new List<AddressableAssetEntry>();
                foreach (var guid in common)
                    entries.Add(editor.settings.CreateOrMoveEntry(guid.ToString(), group));
                editor.settings.PostModificationEvent(AddressableAssetSettings.ModificationEvent.EntryMoved, entries);
            }
        }
        
        protected void SimplifyAddresses(object context)
        {
            List<AssetEntryTreeViewItem> selectedNodes = context as List<AssetEntryTreeViewItem>;
            var entries = new List<AddressableAssetEntry>();

            foreach (var item in selectedNodes)
            {
                item.entry.SetAddress(System.IO.Path.GetFileNameWithoutExtension(item.entry.address), false);
                entries.Add(item.entry);
            }
            editor.settings.PostModificationEvent(AddressableAssetSettings.ModificationEvent.EntryMoved, entries);
        }

        protected void RemoveEntry(object context)
        {
            List<AssetEntryTreeViewItem> selectedNodes = context as List<AssetEntryTreeViewItem>;
            var entries = new List<AddressableAssetEntry>();
            foreach (var item in selectedNodes)
            {
                editor.settings.RemoveAssetEntry(item.entry.guid, false);
                entries.Add(item.entry);
            }
            editor.settings.PostModificationEvent(AddressableAssetSettings.ModificationEvent.EntryRemoved, entries);
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
                        try
                        {
                            var rootType = typeof(AssetGroupProcessor);
                            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                            {
#if NET_4_6
                                foreach (var t in a.ExportedTypes)
#else
                                foreach (var t in a.GetExportedTypes())
#endif
                                {
                                    if (t != rootType && rootType.IsAssignableFrom(t) && !t.IsAbstract)
                                        m_types.Add(t);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogException(ex);
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
                        if (item.entry.guid == AddressableAssetEntry.EditorSceneListName)
                            return false;

                        //can't drag the root "Resources" entry
                        if (item.entry.guid == AddressableAssetEntry.ResourcesName)
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
                    AddressableAssetGroup parent = null;
                    if (target.group != null)
                        parent = target.group;
                    else if (target.entry != null)
                        parent = target.entry.parentGroup;

                    if (parent != null)
                    {
                        var entries = new List<AddressableAssetEntry>();
                        foreach (var p in DragAndDrop.paths)
                            entries.Add(editor.settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(p), parent, false, false));
                        editor.settings.PostModificationEvent(AddressableAssetSettings.ModificationEvent.EntryMoved, entries);
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
                        AddressableAssetGroup parent = null;
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
                                var entries = new List<AddressableAssetEntry>();
                                foreach (var node in draggedNodes)
                                    entries.Add(editor.settings.CreateOrMoveEntry(node.entry.guid, parent, false, false));
                                editor.settings.PostModificationEvent(AddressableAssetSettings.ModificationEvent.EntryMoved, entries);
                            }
                        }
                    }
                }
            }

            return visualMode;
        }

    }

    internal class AssetEntryTreeViewItem : TreeViewItem
    {
        public AddressableAssetEntry entry;
        public AddressableAssetGroup group;
        public Texture2D assetIcon;

        public AssetEntryTreeViewItem(AddressableAssetEntry e, int d) : base((e.address + e.guid).GetHashCode(), d, e.address)
        {
            entry = e;
            group = null;
            assetIcon = AssetDatabase.GetCachedIcon(e.assetPath) as Texture2D;
        }

        public AssetEntryTreeViewItem(AddressableAssetGroup g, int d) : base(g.guid.GetHashCode(), d, g.displayName)
        {
            entry = null;
            group = g;
            assetIcon = null;
        }
    }

   


    class LabelMaskPopupContent : PopupWindowContent
    {
        AddressableAssetSettings m_settings;
        List<AddressableAssetEntry> m_entries;
        Dictionary<string, int> m_labelCount;

        GUIStyle toggleMixed = null;


        int lastItemCount = -1;
        Vector2 rect = new Vector2();
        public LabelMaskPopupContent(AddressableAssetSettings settings, List<AddressableAssetEntry> e, Dictionary<string, int> count)
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

        private void SetLabelForEntries(string label, bool value)
        {
            m_settings.SetLabelValueForEntries(m_entries, label, value);
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
                    SetLabelForEntries(labelName, newState);
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
                    SetLabelForEntries(newEntry, true);
                }
                EditorGUILayout.EndHorizontal();
            }
            GUILayout.EndArea();
        }
    }

    internal class AssetDetailsView
    {
        public AddressableAssetGroup group;
        public List<AddressableAssetEntry> entries = new List<AddressableAssetEntry>();

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
