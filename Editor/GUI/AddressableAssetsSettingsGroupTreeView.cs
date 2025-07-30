using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Debug = UnityEngine.Debug;
using static UnityEditor.AddressableAssets.Settings.AddressablesFileEnumeration;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine.UIElements;
using Assert = UnityEngine.Assertions.Assert;
using TreeView = UnityEditor.IMGUI.Controls.TreeView;

namespace UnityEditor.AddressableAssets.GUI
{
    using Object = UnityEngine.Object;

    internal class AddressableAssetEntryTreeView : TreeView
    {
        AddressableAssetsSettingsGroupEditor m_Editor;
        internal string customSearchString = string.Empty;
        string m_FirstSelectedGroup;
        private readonly Dictionary<AssetEntryTreeViewItem, bool> m_SearchedEntries = new Dictionary<AssetEntryTreeViewItem, bool>();
        private bool m_ForceSelectionClear = false;
        private AddressableAssetEntryIconLazyLoad m_lazyLoader = new();

        enum ColumnId
        {
            Notification,
            Id,
            Type,
            Path,
            Labels
        }

        ColumnId[] m_SortOptions =
        {
            ColumnId.Notification,
            ColumnId.Id,
            ColumnId.Type,
            ColumnId.Path,
            ColumnId.Labels
        };

        internal AddressableAssetEntryTreeView(AddressableAssetSettings settings)
            : this(new AddressableAssetEntryTreeViewState(), CreateDefaultMultiColumnHeaderState(), new AddressableAssetsSettingsGroupEditor(ScriptableObject.CreateInstance<AddressableAssetsWindow>()))
        {
            m_Editor.settings = settings;
        }

        public AddressableAssetEntryTreeView(AddressableAssetEntryTreeViewState state, MultiColumnHeaderState mchs, AddressableAssetsSettingsGroupEditor ed) : base(state, new AddressableAssetSettingsGroupHeader(mchs, ed.settings))
        {
            showBorder = true;
            m_Editor = ed;
            columnIndexForTreeFoldouts = 1;
            multiColumnHeader.sortingChanged += OnSortingChanged;
            multiColumnHeader.columnSettingsChanged += OnColumnChanged;
            multiColumnHeader.visibleColumnsChanged += OnColumnChanged;

            BuiltinSceneCache.sceneListChanged += OnScenesChanged;
            AddressablesAssetPostProcessor.OnPostProcess.Register(OnPostProcessAllAssets, 1);
        }

        internal void Cleanup()
        {
            m_lazyLoader.RemoveLazyIconLoadCallback();
        }

        GUIContent m_WarningIcon;

        GUIContent WarningIcon
        {
            get
            {
                if (m_WarningIcon == null)
                    m_WarningIcon = EditorGUIUtility.IconContent("console.warnicon.sml");
                return m_WarningIcon;
            }
        }


        internal TreeViewItem Root => rootItem;

        void OnScenesChanged()
        {
            if (m_Editor.settings == null)
                return;
            Reload();
        }

        // called when the sort is changed on the header
        internal void OnSortingChanged(MultiColumnHeader mch)
        {
            if (mch is AddressableAssetSettingsGroupHeader h)
            {
                h.SaveEditorPrefs();
            }
            Reload();
        }

        internal void OnColumnChanged(MultiColumnHeader mch)
        {
            if (mch is AddressableAssetSettingsGroupHeader h)
            {
                h.SaveEditorPrefs();
            }
        }

        internal void OnColumnChanged(int columnIndex)
        {
            if (multiColumnHeader is AddressableAssetSettingsGroupHeader h)
            {
                h.SaveEditorPrefs();
            }
        }

        void OnPostProcessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            // Clear any icon cache...
            m_lazyLoader.ClearIconCache();
            foreach (Object obj in Selection.objects)
            {
                if (obj == null)
                    continue;
                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj.GetInstanceID(), out string guid, out long localId))
                {
                    if (obj is GameObject go)
                    {
                        if (UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(go) != null)
                            return;

                        var containingScene = go.scene;
                        if (containingScene.IsValid() && containingScene.isLoaded)
                            return;
                    }

                    m_ForceSelectionClear = true;
                    return;
                }
            }
        }

        protected override void SelectionChanged(IList<int> selectedIds)
        {
            if (selectedIds.Count == 1)
            {
                var item = FindItemInVisibleRows(selectedIds[0]);
                if (item != null && item.group != null)
                {
                    m_FirstSelectedGroup = item.group.name;
                }
            }

            base.SelectionChanged(selectedIds);

            UnityEngine.Object[] selectedObjects = new UnityEngine.Object[selectedIds.Count];
            for (int i = 0; i < selectedIds.Count; i++)
            {
                var item = FindItemInVisibleRows(selectedIds[i]);
                if (item != null)
                {
                    if (item.group != null)
                        selectedObjects[i] = item.group;
                    else if (item.entry != null)
                        selectedObjects[i] = item.entry.TargetAsset;
                }
            }

            // Make last selected group the first object in the array
            if (!string.IsNullOrEmpty(m_FirstSelectedGroup) && selectedObjects.Length > 1)
            {
                for (int i = 0; i < selectedObjects.Length - 1; ++i)
                {
                    if (selectedObjects[i] != null && selectedObjects[i].name == m_FirstSelectedGroup)
                    {
                        var temp = selectedObjects[i];
                        selectedObjects[i] = selectedObjects[selectedIds.Count - 1];
                        selectedObjects[selectedIds.Count - 1] = temp;
                    }
                }
            }

            Selection.objects = selectedObjects; // change selection
        }

        protected override TreeViewItem BuildRoot()
        {
            var root = new TreeViewItem(-1, -1);
            using (new AddressablesFileEnumerationScope(BuildAddressableTree(m_Editor.settings)))
            {
                SortGroups();
                var guidMap = new Dictionary<string, AddressableAssetGroup>();
                foreach (var group in m_Editor.settings.groups)
                    guidMap.Add(group.Guid, group);

                foreach (var groupGuid in GetTreeViewState().sortOrderList)
                    AddGroupChildrenBuild(guidMap[groupGuid], root);
            }

            return root;
        }

        protected override IList<TreeViewItem> BuildRows(TreeViewItem root)
        {
            m_lazyLoader.ClearWorkQueue();
            if (!string.IsNullOrEmpty(searchString))
            {
                var rows = base.BuildRows(root);
                SortHierarchical(rows);
                // At the end of a Search, we schedule an icon lazy load only for results
                // By doing this, we ensure we only load necessary icons, which massively improves search performance
                Search(rows);
                return rows;
            }

            if (!string.IsNullOrEmpty(customSearchString))
            {
                SortChildren(root);
                return Search(base.BuildRows(root));
            }

            SortChildren(root);
            LazyLoadIcons(root);
            return base.BuildRows(root);
        }

        /// <summary>
        /// Schedules all icons to be lazy loaded for rows, and will recurse children
        /// </summary>
        /// <param name="rows">Rows to be lazy loaded</param>
        /// <param name="recurseGroups">Whether to include groups; this will often cause more icons to be loaded than necessary</param>
        private void LazyLoadIcons(IList<TreeViewItem> rows, bool recurseGroups = false)
        {
            foreach (var row in rows)
            {
                if (recurseGroups || row is AssetEntryTreeViewItem { entry: not null })
                    LazyLoadIcons(row);
            }
        }

        /// <summary>
        /// Schedules all icons to be loaded recursively upon root.
        /// Will skip groups which are not expanded.
        /// </summary>
        private void LazyLoadIcons(TreeViewItem root)
        {
            if (root is AssetEntryTreeViewItem entryItem)
            {
                if (entryItem.entry == null)
                {
                    if (!IsExpanded(entryItem.id))
                    {
                        return;
                    }
                }
                else
                {
                    m_lazyLoader.LoadIconLazy(entryItem);
                }
            }

            if (root.children == null)
                return;
            foreach (var child in root.children)
            {
                LazyLoadIcons(child);
            }
        }

        internal IList<TreeViewItem> Search(string search)
        {
            if (ProjectConfigData.HierarchicalSearch)
            {
                customSearchString = search;
                Reload();
            }
            else
            {
                searchString = search;
            }

            return GetRows();
        }

        protected IList<TreeViewItem> Search(IList<TreeViewItem> rows)
        {
            if (rows == null)
                return new List<TreeViewItem>();

            m_SearchedEntries.Clear();
            List<TreeViewItem> items = new List<TreeViewItem>(rows.Count);
            foreach (TreeViewItem item in rows)
            {
                if (ProjectConfigData.HierarchicalSearch)
                {
                    if (SearchHierarchical(item, customSearchString))
                        items.Add(item);
                }
                else if (DoesItemMatchSearch(item, searchString))
                    items.Add(item);
            }
            LazyLoadIcons(items);

            return items;
        }

        /*
         * Hierarchical search requirements :
         * An item is kept if :
         * - it matches
         * - an ancestor matches
         * - at least one descendant matches
         */
        bool SearchHierarchical(TreeViewItem item, string search, bool? ancestorMatching = null)
        {
            var aeItem = item as AssetEntryTreeViewItem;
            if (aeItem == null || search == null)
                return false;

            if (m_SearchedEntries.ContainsKey(aeItem))
                return m_SearchedEntries[aeItem];

            if (ancestorMatching == null)
                ancestorMatching = DoesAncestorMatch(aeItem, search);

            bool isMatching = false;
            if (!ancestorMatching.Value)
                isMatching = DoesItemMatchSearch(aeItem, search);

            bool descendantMatching = false;
            if (!ancestorMatching.Value && !isMatching && aeItem.hasChildren)
            {
                foreach (var child in aeItem.children)
                {
                    descendantMatching = SearchHierarchical(child, search, false);
                    if (descendantMatching)
                        break;
                }
            }

            bool keep = isMatching || ancestorMatching.Value || descendantMatching;
            m_SearchedEntries.Add(aeItem, keep);
            return keep;
        }

        private bool DoesAncestorMatch(TreeViewItem aeItem, string search)
        {
            if (aeItem == null)
                return false;

            var ancestor = aeItem.parent as AssetEntryTreeViewItem;
            bool isMatching = DoesItemMatchSearch(ancestor, search);
            while (ancestor != null && !isMatching)
            {
                ancestor = ancestor.parent as AssetEntryTreeViewItem;
                isMatching = DoesItemMatchSearch(ancestor, search);
            }

            return isMatching;
        }

        internal void ClearSearch()
        {
            customSearchString = string.Empty;
            searchString = string.Empty;
            m_SearchedEntries.Clear();
        }

        internal void SwapSearchType()
        {
            string temp = customSearchString;
            customSearchString = searchString;
            searchString = temp;
            m_SearchedEntries.Clear();
        }

        internal void SortGroups()
        {
            if (state is AddressableAssetEntryTreeViewState s)
            {
                var missingGuid = false;
                var guidToName = new Dictionary<string, string>();
                var newSortOrder = new List<string>();
                var guidToExistingIndex = new Dictionary<string, int>();
                for (var i = 0; i < s.sortOrderList.Count; i++)
                {
                    guidToExistingIndex[s.sortOrderList[i]] = i;
                }
                for (var i = 0; i <  m_Editor.settings.groups.Count; i++)
                {
                    var guid = m_Editor.settings.groups[i].Guid;
                    newSortOrder.Add(guid);
                    guidToName[guid] = m_Editor.settings.groups[i].Name;
                    if (!guidToExistingIndex.ContainsKey(guid))
                    {
                        missingGuid = true;
                        guidToExistingIndex[guid] = -1;
                    }
                }

                // if the count is the same and all of the guids are in the state's sortOrder skip sorting
                if (m_Editor.settings.groups.Count == s.sortOrderList.Count && !missingGuid)
                {
                    return;
                }

                // Rules:
                // if both have indexes we compare by index
                // if one doesn't have an index we want it at the top so we set the default index to -1
                // if both don't have indexes we want to compare them alphabetically
                newSortOrder.Sort((guid1, guid2) =>
                {
                    // assign -1 by default to push to the top of the list if they don't appear
                    // in the older sorting
                    var index1 = guidToExistingIndex[guid1];
                    var index2 = guidToExistingIndex[guid2];
                    // neither of these elements appear in the older sorting
                    if (index1 == -1 && index2 == -1)
                    {
                        // compare alphabetically
                        return string.CompareOrdinal(guidToName[guid1], guidToName[guid2]);
                    }
                    // compare by index in the previous sorting
                    return index1
                        .CompareTo(index2);
                });
                s.sortOrderList = newSortOrder;

                // we have updated the sort order, so save it
                SerializeState(AssetDatabase.GUIDFromAssetPath(AssetDatabase.GetAssetPath(m_Editor.settings)));
            }

        }

        void SortChildren(TreeViewItem root)
        {
            if (!root.hasChildren)
                return;

            foreach (var child in root.children)
            {
                if (child != null && IsExpanded(child.id))
                    SortHierarchical(child.children);
            }
        }

        void SortHierarchical(IList<TreeViewItem> children)
        {
            if (children == null)
                return;

            var sortedColumns = multiColumnHeader.state.sortedColumns;
            if (sortedColumns.Length == 0)
                return;

            List<AssetEntryTreeViewItem> kids = new List<AssetEntryTreeViewItem>();
            List<TreeViewItem> copy = new List<TreeViewItem>(children);
            children.Clear();
            foreach (var c in copy)
            {
                var child = c as AssetEntryTreeViewItem;
                if (child != null && child.entry != null)
                    kids.Add(child);
                else
                    children.Add(c);
            }

            ColumnId col = m_SortOptions[sortedColumns[0]];
            bool ascending = multiColumnHeader.IsSortedAscending(sortedColumns[0]);

            IEnumerable<AssetEntryTreeViewItem> orderedKids = kids;
            switch (col)
            {
                case ColumnId.Notification:
                case ColumnId.Type:
                    break;
                case ColumnId.Path:
                    orderedKids = kids.Order(l => l.entry.AssetPath, ascending);
                    break;
                case ColumnId.Labels:
                    orderedKids = OrderByLabels(kids, ascending);
                    break;
                default:
                    orderedKids = kids.Order(l => l.displayName, ascending);
                    break;
            }

            foreach (var o in orderedKids)
                children.Add(o);


            foreach (var child in children)
            {
                if (child != null && IsExpanded(child.id))
                    SortHierarchical(child.children);
            }
        }

        IEnumerable<AssetEntryTreeViewItem> OrderByLabels(List<AssetEntryTreeViewItem> kids, bool ascending)
        {
            var emptyHalf = new List<AssetEntryTreeViewItem>();
            var namedHalf = new List<AssetEntryTreeViewItem>();
            foreach (var k in kids)
            {
                if (k.entry == null || k.entry.labels == null || k.entry.labels.Count < 1)
                    emptyHalf.Add(k);
                else
                    namedHalf.Add(k);
            }

            var orderedKids = namedHalf.Order(l => m_Editor.settings.labelTable.GetString(l.entry.labels, 200), ascending);

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
            if (string.IsNullOrEmpty(search))
                return true;

            var aeItem = item as AssetEntryTreeViewItem;
            if (aeItem == null)
                return false;

            //check if item matches.
            if (aeItem.displayName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (aeItem.entry == null)
                return false;
            if (aeItem.entry.AssetPath.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            foreach (string label in aeItem.entry.labels)
            {
                if (label.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        void AddGroupChildrenBuild(AddressableAssetGroup group, TreeViewItem root)
        {
            int depth = 0;

            AssetEntryTreeViewItem groupItem = null;
            if (ProjectConfigData.ShowGroupsAsHierarchy && group != null)
            {
                //// dash in name imitates hiearchy.
                TreeViewItem newRoot = root;
                var parts = group.Name.Split('-');
                string partialRestore = "";
                for (int index = 0; index < parts.Length - 1; index++)
                {
                    TreeViewItem folderItem = null;
                    partialRestore += parts[index];
                    int hash = partialRestore.GetHashCode();
                    if (!TryGetChild(newRoot, hash, ref folderItem))
                    {
                        folderItem = new AssetEntryTreeViewItem(parts[index], depth, hash);
                        newRoot.AddChild(folderItem);
                    }

                    depth++;
                    newRoot = folderItem;
                }

                groupItem = new AssetEntryTreeViewItem(group, depth);
                newRoot.AddChild(groupItem);
            }
            else
            {
                groupItem = new AssetEntryTreeViewItem(group, 0);
                root.AddChild(groupItem);
            }

            if (group != null && group.entries.Count > 0)
            {
                foreach (var entry in group.entries)
                {
                    AddAndRecurseEntriesBuild(entry, groupItem, depth + 1, IsExpanded(groupItem.id));
                }
            }
        }

        bool TryGetChild(TreeViewItem root, int childHash, ref TreeViewItem childItem)
        {
            if (root.children == null)
                return false;
            foreach (var child in root.children)
            {
                if (child.id == childHash)
                {
                    childItem = child;
                    return true;
                }
            }

            return false;
        }

        void AddAndRecurseEntriesBuild(AddressableAssetEntry entry, AssetEntryTreeViewItem parent, int depth, bool expanded)
        {
            var item = new AssetEntryTreeViewItem(entry, depth);
            parent.AddChild(item);
            if (!expanded)
            {
                item.checkedForChildren = false;
                return;
            }

            RecurseEntryChildren(entry, item, depth);
        }

        internal void RecurseEntryChildren(AddressableAssetEntry entry, AssetEntryTreeViewItem item, int depth)
        {
            item.checkedForChildren = true;
            var subAssets = new List<AddressableAssetEntry>();
            bool includeSubObjects = ProjectConfigData.ShowSubObjectsInGroupView && !entry.IsFolder && !string.IsNullOrEmpty(entry.guid);
            entry.GatherAllAssets(subAssets, false, false, includeSubObjects);
            if (subAssets.Count > 0)
            {
                foreach (var e in subAssets)
                {
                    if (e.guid.Length > 0 && e.address.Contains('[') && e.address.Contains(']'))
                        Debug.LogErrorFormat("Subasset address '{0}' cannot contain '[ ]'.", e.address);
                    AddAndRecurseEntriesBuild(e, item, depth + 1, IsExpanded(item.id));
                }
            }
        }

        protected override void ExpandedStateChanged()
        {
            foreach (var id in state.expandedIDs)
            {
                var item = FindItem(id, rootItem);
                if (item != null && item.hasChildren)
                {
                    foreach (AssetEntryTreeViewItem c in item.children)
                        if (!c.checkedForChildren)
                            RecurseEntryChildren(c.entry, c, c.depth + 1);
                }
            }
        }

        public override void OnGUI(Rect rect)
        {
            m_Editor.settings.labelTable.Initialize();

            base.OnGUI(rect);

            //TODO - this occasionally causes a "hot control" issue.
            if (m_ForceSelectionClear ||
                (Event.current.type == EventType.MouseDown &&
                 Event.current.button == 0 &&
                 rect.Contains(Event.current.mousePosition)))
            {
                SetSelection(new int[0], TreeViewSelectionOptions.FireSelectionChanged);
                if (m_ForceSelectionClear)
                    m_ForceSelectionClear = false;
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
                    for (int rowId = first; rowId <= last; rowId++)
                    {
                        var aeI = rows[rowId] as AssetEntryTreeViewItem;
                        if (aeI != null && aeI.entry != null)
                        {
                            DefaultStyles.backgroundEven.Draw(GetRowRect(rowId), false, false, false, false);
                        }
                    }
                }
            }
        }

        GUIStyle m_LabelStyle;

        protected override void RowGUI(RowGUIArgs args)
        {
            if (m_LabelStyle == null)
            {
                m_LabelStyle = new GUIStyle("PR Label");
                if (m_LabelStyle == null)
                    m_LabelStyle = UnityEngine.GUI.skin.GetStyle("Label");
            }

            var item = args.item as AssetEntryTreeViewItem;
            if (item == null || item.group == null && item.entry == null)
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    if (!string.IsNullOrEmpty(item.folderPath))
                        CellGUI(args.GetCellRect(1), null, (int)ColumnId.Id, ref args);
                    else
                        base.RowGUI(args);
                }
            }
            else
            {
                bool isReadOnly = item.group == null ? item.entry.ReadOnly : item.group.ReadOnly;
                if (item.group != null)
                {
                    if (item.isRenaming && !args.isRenaming)
                        item.isRenaming = false;
                }

                using (new EditorGUI.DisabledScope(isReadOnly))
                {
                    for (int i = 0; i < args.GetNumVisibleColumns(); ++i)
                        CellGUI(args.GetCellRect(i), item, args.GetColumn(i), ref args);
                }
            }
        }

        void CellGUI(Rect cellRect, AssetEntryTreeViewItem item, int column, ref RowGUIArgs args)
        {
            CenterRectUsingSingleLineHeight(ref cellRect);

            switch ((ColumnId)column)
            {
                case ColumnId.Notification:
                    bool flaggedForUpdateWarning = item.entry == null ? item.group.FlaggedDuringContentUpdateRestriction : item.entry.FlaggedDuringContentUpdateRestriction;
                    if (flaggedForUpdateWarning)
                    {
                        var notification = WarningIcon;
                        if (item.group != null)
                            notification.tooltip = "This group contains assets with the setting �Prevent Updates� that have been modified. " +
                                "To resolve, change the group setting, or move the assets to a different group.";
                        else if (item.entry != null)
                            notification.tooltip = "This asset has been modified, but it is in a group with the setting �Prevent Updates�. " +
                                "To resolve, change the group setting, or move the asset to a different group.";
                        UnityEngine.GUI.Label(cellRect, notification);
                    }

                    break;

                case ColumnId.Id:
                {
                    args.rowRect = cellRect;
                    base.RowGUI(args);
                }
                break;
                case ColumnId.Path:
                    if (item.entry != null && Event.current.type == EventType.Repaint)
                    {
                        var path = item.entry.AssetPath;
                        if (string.IsNullOrEmpty(path))
                            path = item.entry.ReadOnly ? "" : "Missing File";
                        m_LabelStyle.Draw(cellRect, path, false, false, args.selected, args.focused);
                    }

                    break;
                case ColumnId.Type:
                    if (item.assetIcon != null)
                        UnityEngine.GUI.DrawTexture(cellRect, item.assetIcon, ScaleMode.ScaleToFit, true);
                    break;
                case ColumnId.Labels:
                    if (item.entry != null && EditorGUI.DropdownButton(cellRect, new GUIContent(m_Editor.settings.labelTable.GetString(item.entry.labels, cellRect.width)), FocusType.Passive))
                    {
                        var selection = GetItemsForContext(args.item.id);
                        Dictionary<string, int> labelCounts = new Dictionary<string, int>();
                        List<AddressableAssetEntry> entries = new List<AddressableAssetEntry>();
                        var newSelection = new List<int>();
                        foreach (var s in selection)
                        {
                            var aeItem = FindItem(s, rootItem) as AssetEntryTreeViewItem;
                            if (aeItem == null || aeItem.entry == null)
                                continue;

                            entries.Add(aeItem.entry);
                            newSelection.Add(s);
                            foreach (var label in aeItem.entry.labels)
                            {
                                int count;
                                labelCounts.TryGetValue(label, out count);
                                count++;
                                labelCounts[label] = count;
                            }
                        }

                        SetSelection(newSelection);
                        PopupWindow.Show(cellRect, new LabelMaskPopupContent(cellRect, m_Editor.settings, entries, labelCounts));
                    }

                    break;
            }
        }

        IList<int> GetItemsForContext(int row)
        {
            var selection = GetSelection();
            if (selection.Contains(row))
                return selection;

            selection = new List<int>();
            selection.Add(row);
            return selection;
        }

        public static MultiColumnHeaderState CreateDefaultMultiColumnHeaderState()
        {
            return new MultiColumnHeaderState(AddressableAssetSettingsGroupHeader.GetColumns());
        }

        protected string CheckForRename(TreeViewItem item, bool isActualRename)
        {
            string result = string.Empty;
            var assetItem = item as AssetEntryTreeViewItem;
            if (assetItem != null)
            {
                if (assetItem.group != null && !assetItem.group.ReadOnly)
                    result = "Rename";
                else if (assetItem.entry != null && !assetItem.entry.ReadOnly)
                    result = "Change Address";
                if (isActualRename)
                    assetItem.isRenaming = !string.IsNullOrEmpty(result);
            }

            return result;
        }

        protected override bool CanRename(TreeViewItem item)
        {
            return !string.IsNullOrEmpty(CheckForRename(item, true));
        }

        AssetEntryTreeViewItem FindItemInVisibleRows(int id)
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
            args.acceptedRename = PerformRename(args.originalName, args.newName, args.itemID, args.acceptedRename);
        }
        internal bool PerformRename(string originalName, string newName, int itemID, bool acceptedRename)
        {
            if (!acceptedRename)
                return acceptedRename;

            var item = FindItemInVisibleRows(itemID);
            if (item != null)
            {
                item.isRenaming = false;
            }

            if (originalName == newName)
                return acceptedRename;

            if (item != null)
            {
                if (newName != null && newName.Contains("[") && newName.Contains("]"))
                {
                    acceptedRename = false;
                    Debug.LogErrorFormat("Rename of address '{0}' cannot contain '[ ]'.", originalName);
                }
                else if (item.entry != null)
                {
                    item.entry.address = newName;
                    AddressableAssetUtility.OpenAssetIfUsingVCIntegration(item.entry.parentGroup, true);
                }
                else if (item.group != null)
                {
                    if (m_Editor.settings.IsNotUniqueGroupName(newName))
                    {
                        acceptedRename = false;
                        Addressables.LogWarning("There is already a group named '" + newName + "'.  Cannot rename this group to match");
                    }
                    else
                    {
                        item.group.Name = newName;
                        AddressableAssetUtility.OpenAssetIfUsingVCIntegration(item.group, true);
                        AddressableAssetUtility.OpenAssetIfUsingVCIntegration(item.group.Settings, true);
                    }
                }

                Reload();
            }
            return acceptedRename;
    }

        protected override bool CanMultiSelect(TreeViewItem item)
        {
            return true;
        }

        protected override void DoubleClickedItem(int id)
        {
            var item = FindItemInVisibleRows(id);
            if (item != null)
            {
                Object o = null;
                if (item.entry != null)
                    o = AssetDatabase.LoadAssetAtPath<Object>(item.entry.AssetPath);
                else if (item.group != null)
                    o = item.group;

                if (o != null)
                {
                    EditorGUIUtility.PingObject(o);
                    Selection.activeObject = o;
                }
            }
        }

        protected override void SingleClickedItem(int id)
        {
            var nodes = GetSelection();
            // If a user clicks on an asset, prioritise it's icon loading
            foreach (var node in nodes)
            {
                var item = FindItemInVisibleRows(id);
                if (item is { entry: not null })
                {
                    m_lazyLoader.LoadIconLazyPriority(item);
                }
            }
        }

        bool m_ContextOnItem;

        protected override void ContextClicked()
        {
            if (m_ContextOnItem)
            {
                m_ContextOnItem = false;
                return;
            }

            GenericMenu menu = new GenericMenu();
            PopulateGeneralContextMenu(ref menu);
            menu.ShowAsContext();
        }

        void PopulateGeneralContextMenu(ref GenericMenu menu)
        {
            foreach (var templateObject in m_Editor.settings.GroupTemplateObjects)
            {
                Assert.IsNotNull(templateObject);
                menu.AddItem(new GUIContent("Create New Group/" + templateObject.name), false, CreateNewGroup, templateObject);
            }

            menu.AddItem(new GUIContent("Clear Content Update Warnings"), false, ClearContentUpdateWarnings);
        }

        void ClearContentUpdateWarnings()
        {
            foreach (var group in m_Editor.settings.groups)
                ContentUpdateScript.ClearContentUpdateNotifications(group);

            Reload();
        }

        void HandleCustomContextMenuItemGroups(object context)
        {
            var d = context as Tuple<string, List<AssetEntryTreeViewItem>>;
            AddressableAssetSettings.InvokeAssetGroupCommand(d.Item1, d.Item2.Select(s => s.group));
        }

        void HandleCustomContextMenuItemEntries(object context)
        {
            var d = context as Tuple<string, List<AssetEntryTreeViewItem>>;
            AddressableAssetSettings.InvokeAssetEntryCommand(d.Item1, d.Item2.Select(s => s.entry));
        }

        protected override void ContextClickedItem(int id)
        {
            List<AssetEntryTreeViewItem> selectedNodes = new List<AssetEntryTreeViewItem>();
            foreach (var nodeId in GetSelection())
            {
                var item = FindItemInVisibleRows(nodeId); //TODO - this probably makes off-screen but selected items not get added to list.
                if (item != null)
                    selectedNodes.Add(item);
            }

            if (selectedNodes.Count == 0)
                return;

            m_ContextOnItem = true;

            bool isGroup = false;
            bool isEntry = false;
            bool hasReadOnly = false;
            bool isMissingPath = false;
            foreach (var item in selectedNodes)
            {
                if (item.group != null)
                {
                    hasReadOnly |= item.group.ReadOnly;
                    isGroup = true;
                }
                else if (item.entry != null)
                {
                    hasReadOnly |= item.entry.ReadOnly;
                    hasReadOnly |= item.entry.parentGroup.ReadOnly;
                    isEntry = true;
                    isMissingPath |= string.IsNullOrEmpty(item.entry.AssetPath);
                }
                else if (!string.IsNullOrEmpty(item.folderPath))
                {
                    hasReadOnly = true;
                }
            }

            if (isEntry && isGroup)
                return;

            GenericMenu menu = new GenericMenu();
            if (!hasReadOnly)
            {
                if (isGroup)
                {
                    var group = selectedNodes.First().group;
                    if (!group.IsDefaultGroup())
                        menu.AddItem(new GUIContent("Remove Group(s)"), false, RemoveGroup, selectedNodes);
                    menu.AddItem(new GUIContent("Simplify Addressable Names"), false, SimplifyAddresses, selectedNodes);
                    if (selectedNodes.Count == 1)
                    {
                        if (!group.IsDefaultGroup() && group.CanBeSetAsDefault())
                            menu.AddItem(new GUIContent("Set as Default"), false, SetGroupAsDefault, selectedNodes);
                        menu.AddItem(new GUIContent("Inspect Group Settings"), false, GoToGroupAsset, selectedNodes);
                    }

                    foreach (var i in AddressableAssetSettings.CustomAssetGroupCommands)
                        menu.AddItem(new GUIContent(i), false, HandleCustomContextMenuItemGroups, new Tuple<string, List<AssetEntryTreeViewItem>>(i, selectedNodes));
                }
                else if (isEntry)
                {
                    menu.AddItem(new GUIContent("Move Addressables to Group..."), false, MoveEntriesToGroup, new Tuple<Event, List<AssetEntryTreeViewItem>>(Event.current, selectedNodes));
                    menu.AddItem(new GUIContent("Move Addressables to New Group with settings from..."), false, MoveEntriesToNewGroupWithSettings, new Tuple<Event, List<AssetEntryTreeViewItem>>(Event.current, selectedNodes));

                    menu.AddItem(new GUIContent("Remove Addressables"), false, RemoveEntry, selectedNodes);
                    menu.AddItem(new GUIContent("Simplify Addressable Names"), false, SimplifyAddresses, selectedNodes);

                    if (selectedNodes.Count == 1)
                        menu.AddItem(new GUIContent("Copy Address to Clipboard"), false, CopyAddressesToClipboard, selectedNodes);

                    else if (selectedNodes.Count > 1)
                        menu.AddItem(new GUIContent("Copy " + selectedNodes.Count + " Addresses to Clipboard"), false, CopyAddressesToClipboard, selectedNodes);

                    foreach (var i in AddressableAssetSettings.CustomAssetEntryCommands)
                        menu.AddItem(new GUIContent(i), false, HandleCustomContextMenuItemEntries, new Tuple<string, List<AssetEntryTreeViewItem>>(i, selectedNodes));
                }
                else
                    menu.AddItem(new GUIContent("Clear missing references."), false, RemoveMissingReferences);
            }
            else
            {
                if (isEntry)
                {
                    if (selectedNodes.Count == 1)
                        menu.AddItem(new GUIContent("Copy Address to Clipboard"), false, CopyAddressesToClipboard, selectedNodes);
                    else if (selectedNodes.Count > 1)
                        menu.AddItem(new GUIContent("Copy " + selectedNodes.Count + " Addresses to Clipboard"), false, CopyAddressesToClipboard, selectedNodes);
                }
            }

            if (selectedNodes.Count == 1)
            {
                var label = CheckForRename(selectedNodes.First(), false);
                if (!string.IsNullOrEmpty(label))
                    menu.AddItem(new GUIContent(label), false, RenameItem, selectedNodes);
            }

            PopulateGeneralContextMenu(ref menu);

            menu.ShowAsContext();
        }

        void GoToGroupAsset(object context)
        {
            List<AssetEntryTreeViewItem> selectedNodes = context as List<AssetEntryTreeViewItem>;
            if (selectedNodes == null || selectedNodes.Count == 0)
                return;
            var group = selectedNodes.First().group;
            if (group == null)
                return;
            EditorGUIUtility.PingObject(group);
            Selection.activeObject = group;
        }

        internal static void CopyAddressesToClipboard(object context)
        {
            List<AssetEntryTreeViewItem> selectedNodes = context as List<AssetEntryTreeViewItem>;
            string buffer = "";

            foreach (AssetEntryTreeViewItem item in selectedNodes)
                buffer += item.entry.address + ",";

            buffer = buffer.TrimEnd(',');
            GUIUtility.systemCopyBuffer = buffer;
        }

        void MoveEntriesToNewGroupWithSettings(object context)
        {
            var pair = context as Tuple<Event, List<AssetEntryTreeViewItem>>;
            var entries = new List<AddressableAssetEntry>();
            foreach (AssetEntryTreeViewItem item in pair.Item2)
            {
                if (item.entry != null)
                    entries.Add(item.entry);
            }

            var window = EditorWindow.GetWindow<GroupsPopupWindow>(true, "Select Addressable Group");
            Vector2 mousePosition = pair.Item1 == null ? Vector2.zero : pair.Item1.mousePosition;
            window.Initialize(null, false, false, mousePosition, MoveEntriesToNewGroupWithSettings, m_Editor.settings, entries);
        }

        void MoveEntriesToNewGroupWithSettings(AddressableAssetSettings settings, List<AddressableAssetEntry> entries, AddressableAssetGroup group)
        {
            var newGroup = settings.CreateGroup(AddressableAssetSettings.kNewGroupName, false, false, true, group.Schemas);
            foreach (AddressableAssetEntry entry in entries)
            {
                settings.MoveEntry(entry, newGroup, entry.ReadOnly, true);
            }
        }

        void MoveEntriesToGroup(object context)
        {
            var pair = context as Tuple<Event, List<AssetEntryTreeViewItem>>;
            var entries = new List<AddressableAssetEntry>();
            bool mixedGroups = false;
            AddressableAssetGroup displayGroup = null;
            foreach (AssetEntryTreeViewItem item in pair.Item2)
            {
                if (item.entry != null)
                {
                    entries.Add(item.entry);
                    if (displayGroup == null)
                        displayGroup = item.entry.parentGroup;
                    else if (item.entry.parentGroup != displayGroup)
                    {
                        mixedGroups = true;
                    }
                }
            }

            var window = EditorWindow.GetWindow<GroupsPopupWindow>(true, "Select Addressable Group");
            AddressableAssetGroup initialSelection = !mixedGroups ? entries[0].parentGroup : null;
            Vector2 mousePosition = pair.Item1 == null ? Vector2.zero : pair.Item1.mousePosition;
            window.Initialize(initialSelection, false, false, mousePosition, AddressableAssetUtility.MoveEntriesToGroup, m_Editor.settings, entries);
        }

        internal void CreateNewGroup(object context)
        {
            var groupTemplate = context as AddressableAssetGroupTemplate;
            if (groupTemplate != null)
            {
                var newGroup = m_Editor.settings.CreateGroup(groupTemplate.Name, false, false, true, null, groupTemplate.GetTypes());
                groupTemplate.ApplyToAddressableAssetGroup(newGroup);
            }
            else
            {
                 m_Editor.settings.CreateGroup("", false, false, false, null);
            }
        }

        internal void SetGroupAsDefault(object context)
        {
            List<AssetEntryTreeViewItem> selectedNodes = context as List<AssetEntryTreeViewItem>;
            if (selectedNodes == null || selectedNodes.Count == 0)
                return;
            var group = selectedNodes.First().group;
            if (group == null)
                return;
            m_Editor.settings.DefaultGroup = group;
            Reload();
        }

        protected void RemoveMissingReferences()
        {
            RemoveMissingReferencesImpl();
        }

        internal void RemoveMissingReferencesImpl()
        {
            if (m_Editor.settings.RemoveMissingGroupReferences())
            {
                m_Editor.settings.SetDirty(AddressableAssetSettings.ModificationEvent.GroupRemoved, null, true, true);
            }
        }

        protected void RemoveGroup(object context)
        {
            RemoveGroupImpl(context);
        }

        internal void RemoveGroupImpl(object context, bool forceRemoval = false)
        {
            if (forceRemoval || EditorUtility.DisplayDialog("Delete selected groups?", "Are you sure you want to delete the selected groups?\n\nYou cannot undo this action.", "Yes", "No"))
            {
                List<AssetEntryTreeViewItem> selectedNodes = context as List<AssetEntryTreeViewItem>;
                if (selectedNodes == null || selectedNodes.Count < 1)
                    return;
                var groups = new List<AddressableAssetGroup>();
                AssetDatabase.StartAssetEditing();
                try
                {
                    foreach (var item in selectedNodes)
                    {
                        m_Editor.settings.RemoveGroupInternal(item == null ? null : item.group, true, false);
                        GetTreeViewState().sortOrderList.Remove(item.group?.Guid);
                        groups.Add(item.group);
                    }
                }
                finally
                {
                    AssetDatabase.StopAssetEditing();
                }

                m_Editor.settings.SetDirty(AddressableAssetSettings.ModificationEvent.GroupRemoved, groups, true, true);
                AddressableAssetUtility.OpenAssetIfUsingVCIntegration(m_Editor.settings);
            }
        }

        protected void SimplifyAddresses(object context)
        {
            SimplifyAddressesImpl(context);
        }

        internal void SimplifyAddressesImpl(object context)
        {
            List<AssetEntryTreeViewItem> selectedNodes = context as List<AssetEntryTreeViewItem>;
            if (selectedNodes == null || selectedNodes.Count < 1)
                return;
            var entries = new List<AddressableAssetEntry>();
            HashSet<AddressableAssetGroup> modifiedGroups = new HashSet<AddressableAssetGroup>();
            foreach (var item in selectedNodes)
            {
                if (item.IsGroup)
                {
                    foreach (var e in item.group.entries)
                    {
                        e.SetAddress(Path.GetFileNameWithoutExtension(e.address), false);
                        entries.Add(e);
                    }

                    modifiedGroups.Add(item.group);
                }
                else
                {
                    item.entry.SetAddress(Path.GetFileNameWithoutExtension(item.entry.address), false);
                    entries.Add(item.entry);
                    modifiedGroups.Add(item.entry.parentGroup);
                }
            }

            foreach (var g in modifiedGroups)
            {
                g.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, entries, false, true);
                AddressableAssetUtility.OpenAssetIfUsingVCIntegration(g);
            }

            m_Editor.settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, entries, true, false);
        }

        protected void RemoveEntry(object context)
        {
            RemoveEntryImpl(context);
        }

        internal void RemoveEntryImpl(object context, bool forceRemoval = false)
        {
            if (forceRemoval || EditorUtility.DisplayDialog("Delete selected entries?", "Are you sure you want to delete the selected entries?\n\nYou cannot undo this action.", "Yes", "No"))
            {
                List<AssetEntryTreeViewItem> selectedNodes = context as List<AssetEntryTreeViewItem>;
                if (selectedNodes == null || selectedNodes.Count < 1)
                    return;
                var entries = new List<AddressableAssetEntry>();
                HashSet<AddressableAssetGroup> modifiedGroups = new HashSet<AddressableAssetGroup>();
                foreach (var item in selectedNodes)
                {
                    if (item.entry != null)
                    {
                        entries.Add(item.entry);
                        modifiedGroups.Add(item.entry.parentGroup);
                        m_Editor.settings.RemoveAssetEntry(item.entry.guid, false);
                    }
                }

                foreach (var g in modifiedGroups)
                {
                    g.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, entries, false, true);
                    AddressableAssetUtility.OpenAssetIfUsingVCIntegration(g);
                }

                m_Editor.settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryRemoved, entries, true, false);
            }
        }

        protected void RenameItem(object context)
        {
            RenameItemImpl(context);
        }

        internal void RenameItemImpl(object context)
        {
            List<AssetEntryTreeViewItem> selectedNodes = context as List<AssetEntryTreeViewItem>;
            if (selectedNodes != null && selectedNodes.Count >= 1)
            {
                var item = selectedNodes.First();
                if (CanRename(item))
                    BeginRename(item);
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
            if (Event.current.type == EventType.KeyUp && Event.current.keyCode == KeyCode.Delete && GetSelection().Count > 0)
            {
                List<AssetEntryTreeViewItem> selectedNodes = new List<AssetEntryTreeViewItem>();
                bool allGroups = true;
                bool allEntries = true;
                foreach (var nodeId in GetSelection())
                {
                    var item = FindItemInVisibleRows(nodeId);
                    if (item != null)
                    {
                        selectedNodes.Add(item);
                        if (item.entry == null)
                            allEntries = false;
                        else
                            allGroups = false;
                    }
                }

                if (allEntries)
                    RemoveEntry(selectedNodes);
                if (allGroups)
                    RemoveGroup(selectedNodes);
            }
        }

        protected override bool CanStartDrag(CanStartDragArgs args)
        {
            int resourcesCount = 0;
            foreach (var id in args.draggedItemIDs)
            {
                var item = FindItemInVisibleRows(id);
                if (item != null)
                {
                    if (item.entry != null)
                    {
                        //if it's missing a path, it can't be moved.  most likely this is a sub-asset.
                        if (string.IsNullOrEmpty(item.entry.AssetPath))
                            return false;
                    }
                }
            }

            if ((resourcesCount > 0) && (resourcesCount < args.draggedItemIDs.Count))
                return false;

            return true;
        }

        protected override void SetupDragAndDrop(SetupDragAndDropArgs args)
        {
            DragAndDrop.PrepareStartDrag();

            var selectedNodes = new List<AssetEntryTreeViewItem>();
            foreach (var id in args.draggedItemIDs)
            {
                var item = FindItemInVisibleRows(id);
                if (item.entry != null || item.@group != null)
                    selectedNodes.Add(item);
            }

            DragAndDrop.paths = null;
            DragAndDrop.objectReferences = new Object[] {};
            DragAndDrop.SetGenericData("AssetEntryTreeViewItem", selectedNodes);
            DragAndDrop.visualMode = selectedNodes.Count > 0 ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
            DragAndDrop.StartDrag("AssetBundleTree");
        }

        protected override DragAndDropVisualMode HandleDragAndDrop(DragAndDropArgs args)
        {
            DragAndDropVisualMode visualMode = DragAndDropVisualMode.None;

            var target = args.parentItem as AssetEntryTreeViewItem;

            if (target != null && target.entry != null && target.entry.ReadOnly)
                return DragAndDropVisualMode.Rejected;

            if (DragAndDrop.paths != null && DragAndDrop.paths.Length > 0)
            {
                visualMode = HandleDragAndDropPaths(target, args);
            }
            else
            {
                visualMode = HandleDragAndDropItems(target, args);
            }

            return visualMode;
        }

        DragAndDropVisualMode HandleDragAndDropItems(AssetEntryTreeViewItem target, DragAndDropArgs args)
        {
            var draggedNodes = DragAndDrop.GetGenericData("AssetEntryTreeViewItem") as List<AssetEntryTreeViewItem>;
            return HandleDragAndDropItems(draggedNodes, target, args.parentItem, args.insertAtIndex, args.performDrop);
        }
        internal DragAndDropVisualMode HandleDragAndDropItems(List<AssetEntryTreeViewItem> draggedNodes, AssetEntryTreeViewItem target, TreeViewItem parentItem, int insertAtIndex, bool performDrop)
        {
            DragAndDropVisualMode visualMode = DragAndDropVisualMode.None;
            if (draggedNodes != null && draggedNodes.Count > 0)
            {
                visualMode = DragAndDropVisualMode.Copy;
                AssetEntryTreeViewItem firstItem = draggedNodes.First();
                bool isDraggingGroup = firstItem.IsGroup;
                bool isDraggingNestedGroup = isDraggingGroup && firstItem.parent != rootItem;
                bool dropParentIsRoot = parentItem == rootItem || parentItem == null;
                bool parentGroupIsReadOnly = target?.@group != null && target.@group.ReadOnly;

                if (isDraggingNestedGroup || isDraggingGroup && !dropParentIsRoot || !isDraggingGroup && dropParentIsRoot || parentGroupIsReadOnly)
                    visualMode = DragAndDropVisualMode.Rejected;

                if (performDrop)
                {
                    if (parentItem == null || parentItem == rootItem && visualMode != DragAndDropVisualMode.Rejected)
                    {
                        var treeViewState = GetTreeViewState();
                        // Need to insert groups in reverse order because all groups will be inserted at the same index
                        for (int i = draggedNodes.Count - 1; i >= 0; i--)
                        {
                            AssetEntryTreeViewItem node = draggedNodes[i];
                            // if this isn't a group we can't move it, assume it's just an asset that got highlighted
                            // with the group
                            if (node?.group == null)
                            {
                                continue;
                            }
                            AddressableAssetGroup group = node.@group;
                            List<string> sortedGroups = new List<string>(treeViewState.sortOrderList);
                            int index = sortedGroups.IndexOf(group.Guid);
                            if (index < insertAtIndex)
                                insertAtIndex--;

                            sortedGroups.RemoveAt(index);

                            if (insertAtIndex < 0 || insertAtIndex > sortedGroups.Count)
                                sortedGroups.Add(group.Guid);
                            else
                                sortedGroups.Insert(insertAtIndex, group.Guid);

                            treeViewState.sortOrderList = sortedGroups;
                        }
                        // we have to update the sort order anytime we manipulate m_Editor.settings.groups directly
                        m_Editor.settings.SetDirty(AddressableAssetSettings.ModificationEvent.GroupMoved, m_Editor.settings.groups, true, true);
                    }
                    else
                    {
                        AddressableAssetGroup parent = null;
                        if (target.group != null)
                            parent = target.group;
                        else if (target.entry != null)
                            parent = target.entry.parentGroup;

                        if (parent != null)
                        {
                            var entries = new List<AddressableAssetEntry>();
                            foreach (AssetEntryTreeViewItem node in draggedNodes)
                            {
                                entries.Add(node.entry);
                            }


                            var modifiedGroups = new HashSet<AddressableAssetGroup>();
                            modifiedGroups.Add(parent);
                            foreach (AddressableAssetEntry entry in entries)
                            {
                                modifiedGroups.Add(entry.parentGroup);
                                m_Editor.settings.MoveEntry(entry, parent, false, false);
                            }

                            foreach (AddressableAssetGroup modifiedGroup in modifiedGroups)
                                AddressableAssetUtility.OpenAssetIfUsingVCIntegration(modifiedGroup);

                            m_Editor.settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entries, true, false);
                        }
                    }
                }
            }

            return visualMode;
        }

        DragAndDropVisualMode HandleDragAndDropPaths(AssetEntryTreeViewItem target, DragAndDropArgs args)
        {
            var draggedPaths = DragAndDrop.paths;
            return HandleDragAndDropPaths(draggedPaths, target, args.performDrop);
        }

        internal DragAndDropVisualMode HandleDragAndDropPaths(string[] paths, AssetEntryTreeViewItem target, bool performDrop)
        {
            DragAndDropVisualMode visualMode = DragAndDropVisualMode.None;

            bool parentGroupIsReadOnly = target?.@group != null && target.@group.ReadOnly;
            if (target == null || parentGroupIsReadOnly)
                return DragAndDropVisualMode.Rejected;

            foreach (String path in paths)
            {
                if (!AddressableAssetUtility.IsPathValidForEntry(path) && target != rootItem)
                    return DragAndDropVisualMode.Rejected;
            }

            visualMode = DragAndDropVisualMode.Copy;

            if (performDrop && visualMode != DragAndDropVisualMode.Rejected)
            {
                AddressableAssetGroup parent = null;
                bool targetIsGroup = false;
                if (target.group != null)
                {
                    parent = target.group;
                    targetIsGroup = true;
                }
                else if (target.entry != null)
                    parent = target.entry.parentGroup;

                if (parent != null)
                {
                    var resourcePaths = new List<string>();
                    var nonResourceGuids = new List<string>();
                    foreach (var p in paths)
                    {
                        if (AddressableAssetUtility.IsInResources(p))
                            resourcePaths.Add(p);
                        else
                            nonResourceGuids.Add(AssetDatabase.AssetPathToGUID(p));
                    }

                    bool canMarkNonResources = true;
                    if (resourcePaths.Count > 0)
                        canMarkNonResources = AddressableAssetUtility.SafeMoveResourcesToGroup(m_Editor.settings, parent, resourcePaths, null);

                    if (canMarkNonResources)
                    {
                        if (nonResourceGuids.Count > 0)
                        {
                            var entriesMoved = new List<AddressableAssetEntry>();
                            var entriesCreated = new List<AddressableAssetEntry>();
                            m_Editor.settings.CreateOrMoveEntries(nonResourceGuids, parent, entriesCreated, entriesMoved, false, false);

                            if (entriesMoved.Count > 0)
                                m_Editor.settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entriesMoved, true);
                            if (entriesCreated.Count > 0)
                                m_Editor.settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryAdded, entriesCreated, true);

                            AddressableAssetUtility.OpenAssetIfUsingVCIntegration(parent);
                        }

                        if (targetIsGroup)
                        {
                            SetExpanded(target.id, true);
                        }
                    }
                }
            }

            return visualMode;
        }

        private bool PathPointsToAssetGroup(string path)
        {
            return AssetDatabase.GetMainAssetTypeAtPath(path) == typeof(AddressableAssetGroup);
        }

        public void SerializeState(GUID guid)
        {

            if (state is AddressableAssetEntryTreeViewState s)
            {
                var settings = AddressableAssetGroupSortSettings.GetSettings();
                settings.sortOrder = new string[s.sortOrderList.Count];
                for (var i = 0; i < s.sortOrderList.Count; i++)
                {
                    settings.sortOrder[i] = s.sortOrderList[i];
                }

                AddressableAssetUtility.OpenAssetIfUsingVCIntegration(settings);
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
            }

            if (multiColumnHeader is AddressableAssetSettingsGroupHeader h)
            {
                h.SaveEditorPrefs();
            }

        }

        public void DeserializeState(GUID guid)
        {
            if (multiColumnHeader is AddressableAssetSettingsGroupHeader h)
            {
                h.LoadEditorPrefs();
            }

            if (state is AddressableAssetEntryTreeViewState s)
            {
                var settings = AddressableAssetGroupSortSettings.GetSettings();
                s.sortOrderList = new List<string>();
                s.sortOrderList.AddRange(settings.sortOrder);
            }
        }

        public AddressableAssetEntryTreeViewState GetTreeViewState()
        {
            var s = state as AddressableAssetEntryTreeViewState;
            if (s == null)
            {
                throw new Exception("GroupTreeView using incompatible state " + state.GetType());
            }
            return s;
        }
    }

    class AssetEntryTreeViewItem : TreeViewItem
    {
        public AddressableAssetEntry entry;
        public AddressableAssetGroup group;
        public string folderPath;
        public Texture2D assetIcon;
        public bool isRenaming;
        public bool checkedForChildren = true;

        public AssetEntryTreeViewItem(AddressableAssetEntry e, int d) : base(e == null ? 0 : (e.address + e.guid).GetHashCode(), d, e == null ? "[Missing Reference]" : e.address)
        {
            entry = e;
            group = null;
            folderPath = string.Empty;
            // LoadIconLazy will populate the icon for us.
            assetIcon = null;
            isRenaming = false;
        }

        public AssetEntryTreeViewItem(AddressableAssetGroup g, int d) : base(g == null ? 0 : g.Guid.GetHashCode(), d, g == null ? "[Missing Reference]" : g.Name)
        {
            entry = null;
            group = g;
            folderPath = string.Empty;
            assetIcon = null;
            isRenaming = false;
        }

        public AssetEntryTreeViewItem(string folder, int d, int id) : base(id, d, string.IsNullOrEmpty(folder) ? "missing" : folder)
        {
            entry = null;
            group = null;
            folderPath = folder;
            assetIcon = null;
            isRenaming = false;
        }

        public bool IsGroup => group != null && entry == null;

        public override string displayName
        {
            get
            {
                if (!isRenaming && group != null && group.Default)
                    return base.displayName + " (Default)";
                return base.displayName;
            }

            set { base.displayName = value; }
        }
    }

    static class MyExtensionMethods
    {
        // Find digits in a string
        static Regex s_Regex = new Regex(@"\d+", RegexOptions.Compiled);

        public static IEnumerable<T> Order<T>(this IEnumerable<T> items, Func<T, string> selector, bool ascending)
        {
            if (EditorPrefs.HasKey("AllowAlphaNumericHierarchy") && EditorPrefs.GetBool("AllowAlphaNumericHierarchy"))
            {
                // Find the length of the longest number in the string
                int maxDigits = items
                    .SelectMany(i => s_Regex.Matches(selector(i)).Cast<Match>().Select(digitChunk => (int?)digitChunk.Value.Length))
                    .Max() ?? 0;

                // in the evaluator, pad numbers with zeros so they all have the same length
                var tempSelector = selector;
                selector = i => s_Regex.Replace(tempSelector(i), match => match.Value.PadLeft(maxDigits, '0'));
            }

            return ascending ? items.OrderBy(selector) : items.OrderByDescending(selector);
        }
    }

    /// <summary>
    /// Helper object that hooks into EditorApplication update to lazy load icon assets
    /// </summary>
    internal class AddressableAssetEntryIconLazyLoad
    {
        internal static class PrefabIcons
        {
            private static Texture s_prefabIcon;
            private static Texture s_prefabVariantIcon;

            /// <summary>
            /// Determine if a prefab is a Prefab Variant, without loading it as a full object
            /// Useful for cases where a user may have a large prefab, which shouldn't be loaded just to determine icons
            /// </summary>
            /// <param name="path">Path to the prefab</param>
            /// <returns>
            /// True if the prefab is a Prefab Variant.
            /// False if the path is not a prefab or a variant
            /// </returns>
            internal static bool IsPrefabVariant(string path)
            {
                if (path == null)
                    return false;
                if (!path.EndsWith(".prefab"))
                    return false;
                // See comment below about this being the best way to do this without incurring a full deserialization (expensive)
                using var reader = new StreamReader(path);
                // Skip to line 3
                for (var i = 0; i < 3; i++)
                {
                    if (reader.ReadLine() == null)
                        return false;
                }
                var prefabType = reader.ReadLine();
                return prefabType != null && prefabType.StartsWith("PrefabInstance");
            }

            public static Texture2D PrefabIcon(string prefabPath)
            {
                Assert.IsTrue(prefabPath.EndsWith(".prefab"));
                if (s_prefabIcon)
                    return s_prefabIcon as Texture2D;

                // Unfortunately we need to perform a file read, to ensure we don't pollute this field
                if (IsPrefabVariant(prefabPath))
                {
                    return PrefabVariantIcon(prefabPath);
                }

                s_prefabIcon = AssetDatabase.GetCachedIcon(prefabPath);
                return s_prefabIcon as Texture2D;
            }

            public static Texture2D PrefabVariantIcon(string prefabPath)
            {
                Assert.IsTrue(prefabPath.EndsWith(".prefab"));
                if (s_prefabVariantIcon)
                    return s_prefabVariantIcon as Texture2D;
                // Unfortunately we need to perform a file read, to ensure we don't pollute this field
                if (!IsPrefabVariant(prefabPath))
                {
                    return PrefabIcon(prefabPath);
                }
                s_prefabVariantIcon = AssetDatabase.GetCachedIcon(prefabPath);
                return s_prefabVariantIcon as Texture2D;
            }
        }
        // Ensure we don't tank Editor performance when loading icons lazily
        private const double k_timeBudget = 0.008f;
        private const int k_maxFileReadsBeforePushingAsync = 64;

        private readonly Queue<AssetEntryTreeViewItem> m_needsIconRefresh = new();
        private readonly Queue<AssetEntryTreeViewItem> m_needsIconRefreshPriority = new();
        private readonly List<AssetEntryTreeViewItem> m_reloadWhenCacheInvalidated = new();
        private bool m_editorHooked;
        private int m_editorProgressId = -1;
        private int m_progressLength = -1;
        private int m_lastTempRequestFrame;
        private int m_currentFileReads;

        // Texture Caches
        private static Texture s_prefabIcon;
        private readonly Dictionary<string, Texture> m_scriptGuidToIconCache = new();

        /// <summary>
        /// Assigns a temporary interim texture for <paramref name="item"/>
        /// This will also register <paramref name="item"/> internally, such that it's true icon will be loaded later
        /// Does nothing if <paramref name="item"/> does not represent an asset.
        /// </summary>
        /// <param name="item">Tree item that needs to be registered</param>
        internal void LoadIconLazy(AssetEntryTreeViewItem item)
        {
            if (item.entry == null)
                return;
            if (Time.frameCount != m_lastTempRequestFrame)
            {
                m_lastTempRequestFrame = Time.frameCount;
                m_currentFileReads = 0;
            }

            if (m_currentFileReads < k_maxFileReadsBeforePushingAsync)
            {
                item.assetIcon = GetBestTempIcon(item.entry, true);
                m_currentFileReads++;
            }
            else
            {
                item.assetIcon = GetBestTempIcon(item.entry);
            }
            AddLazyIconLoadCallback();
            m_needsIconRefresh.Enqueue(item);
        }

        /// <summary>
        /// Ensure a registered item is loaded first. Intended for UX where the real icon must be shown
        /// (e.g. when a user clicks on a row, it should load it's real icon first)
        /// <seealso cref="LoadIconLazy"/>
        /// </summary>
        internal void LoadIconLazyPriority(AssetEntryTreeViewItem item)
        {
            if (item.entry == null)
                return;
            // No attempt is made to GetBestTempIcon, as this is only for prioritisation
            AddLazyIconLoadCallback();
            m_needsIconRefreshPriority.Enqueue(item);
        }

        /// <summary>
        /// Fast retrieval of a MonoBehaviour (e.g. ScriptableObject's) GUID from the beginning of it's asset
        /// Removes need to fully deserialize to deduce type information
        /// </summary>
        internal static string GetTypeGuidFromAsset(string assetPath)
        {
            if (assetPath == null)
                return null;
            if (!assetPath.EndsWith(".asset"))
                return null;

            // We need to do this to prevent the performance cost of deserializing the entire asset
            // Unfortunately this is the best way to do it
            using var reader = new StreamReader(assetPath);
            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine()!.Trim();
                // Parse the line and attempt to extract a valid GUID
                if (!line.StartsWith("m_Script")) continue;
                var guidMatch = Regex.Match(line, "guid: .+,", RegexOptions.Compiled);
                // If we did not extract a valid GUID, we should get out now and let AssetDatabase do this properly...
                if (!guidMatch.Success) return null;
                var guidStr = guidMatch.Value.Replace("guid: ", "").Replace(",", "");
                return guidStr;
            }

            return null;
        }

        /// <summary>
        /// Get a useful icon for <paramref name="entry"/> without causing a full deserialization of the asset
        /// This is unlike <seealso cref="AssetDatabase.GetCachedIcon"/> which will deserialize an asset if the icon is not cached
        /// </summary>
        /// <param name="entry">Entry to get an icon for</param>
        /// <param name="performFileReads">
        /// Whether we should perform disk reads in order to determine the icon.
        /// Slightly slower, but is required to determine ScriptableObject icons and if a Prefab is a Variant
        /// </param>
        /// <returns></returns>
        private Texture2D FastIconFromPath(AddressableAssetEntry entry, bool performFileReads = false)
        {
            if (entry.AssetPath == null)
                return null;
            if (!File.Exists(entry.AssetPath))
                return null;
            var isPrefab = entry.AssetPath.EndsWith(".prefab");
            var isAsset = entry.AssetPath.EndsWith(".asset");

            if (!performFileReads)
            {
                if (isPrefab)
                {
                    return PrefabIcons.PrefabIcon(entry.AssetPath);
                }

                return null;
            }

            if (isAsset)
            {
                var guid = GetTypeGuidFromAsset(entry.AssetPath);
                if (guid == null) return null;
                if (!m_scriptGuidToIconCache.ContainsKey(guid))
                {
                    m_scriptGuidToIconCache[guid] = AssetDatabase.GetCachedIcon(entry.AssetPath);
                }
                return m_scriptGuidToIconCache[guid] as Texture2D;
            }
            if (isPrefab)
            {
                if (PrefabIcons.IsPrefabVariant(entry.AssetPath))
                {
                    return PrefabIcons.PrefabVariantIcon(entry.AssetPath);
                }
                return PrefabIcons.PrefabIcon(entry.AssetPath);
            }
            return null;
        }

        /// <summary>
        /// Returns the best case icon that can be retrieved quickly
        /// </summary>
        private Texture2D GetBestTempIcon(AddressableAssetEntry entry, bool performFileRead = false)
        {
            var icon = FastIconFromPath(entry, performFileRead);
            return icon == null ? AssetPreview.GetMiniTypeThumbnail(entry.MainAssetType) : icon;
        }

        /// <summary>
        /// Hook into the Editor's update, if we are not already hooked in
        /// </summary>
        private void AddLazyIconLoadCallback()
        {
            if (!m_editorHooked)
            {
                EditorApplication.update += WorkOnIconQueue;
                m_editorHooked = true;
            }
        }

        /// <summary>
        /// Unhook from the Editor and clean up our resources
        /// </summary>
        internal void RemoveLazyIconLoadCallback()
        {
            EditorApplication.update -= WorkOnIconQueue;
            m_editorHooked = false;
            if (m_editorProgressId > 0)
                Progress.Finish(m_editorProgressId);
            m_editorProgressId = -1;
            m_progressLength = -1;
            m_reloadWhenCacheInvalidated.Clear();
            ClearWorkQueue();
        }

        /// <summary>
        /// Clear out the work queue, without unhooking.
        /// Use this when rebuilding the tree state, to remove any pending loads
        /// </summary>
        internal void ClearWorkQueue()
        {
            m_needsIconRefresh.Clear();
            m_needsIconRefreshPriority.Clear();
            m_reloadWhenCacheInvalidated.Clear();
            // this will cause the progress to be recomputed
            m_progressLength = 0;
        }

        /// <summary>
        /// Worker method that is hooked into EditorApplication
        /// </summary>
        private void WorkOnIconQueue()
        {
            double startTime = EditorApplication.timeSinceStartup;
            while (EditorApplication.timeSinceStartup - startTime < k_timeBudget)
            {
                // Always try and pull from priority queue first
                if (!m_needsIconRefreshPriority.TryDequeue(out var item))
                {
                    if (!m_needsIconRefresh.TryDequeue(out item))
                    {
                        RemoveLazyIconLoadCallback();
                        return;
                    }
                }
                if (item.entry == null)
                {
                    item.assetIcon = null;
                    continue;
                }
                if (m_needsIconRefreshPriority.Count + m_needsIconRefresh.Count > m_progressLength)
                {
                    m_progressLength = m_needsIconRefresh.Count;
                }

                if (m_editorProgressId == -1)
                {
                    m_editorProgressId = Progress.Start("Loading icons for Addressable Groups...");
                }

                // no need to revalidate scene files, the icon won't change
                if (!item.entry.AssetPath.EndsWith(".unity"))
                    m_reloadWhenCacheInvalidated.Add(item);

                // Prefabs will already have the Blue Cube they always have *but* we should check if this is a variant...
                if (item.entry.AssetPath.EndsWith(".prefab") || item.entry.AssetPath.EndsWith(".asset"))
                {
                    // Always allow file reads in this path...
                    item.assetIcon = FastIconFromPath(item.entry, true);
                }
                else
                    item.assetIcon = AssetDatabase.GetCachedIcon(item.entry.AssetPath) as Texture2D;
            }
            Progress.Report(m_editorProgressId, m_progressLength - m_needsIconRefresh.Count, m_progressLength);
        }

        /// <summary>
        /// Clear the icon cache, and force any currently loaded rows to be reloaded
        /// </summary>
        internal void ClearIconCache()
        {
            // If we don't re-load icons, we have the potential for an icon to be representing an old state
            // As such, we should reload every icon to ensure it is valid
            foreach (var item in m_reloadWhenCacheInvalidated)
            {
                LoadIconLazy(item);
            }
            m_scriptGuidToIconCache.Clear();
            m_reloadWhenCacheInvalidated.Clear();
        }
    }
}
