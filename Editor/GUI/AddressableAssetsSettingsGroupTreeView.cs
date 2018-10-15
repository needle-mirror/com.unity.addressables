using System.Collections.Generic;
using UnityEngine;
using UnityEditor.IMGUI.Controls;
using System;
using System.Linq;
using UnityEngine.AddressableAssets;

namespace UnityEditor.AddressableAssets
{
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

            EditorBuildSettings.sceneListChanged += OnScenesChanged;
        }

        private void OnScenesChanged()
        {
            Reload();
        }

        private void OnSortingChanged(MultiColumnHeader multiColumnHeader)
        {
            SortChildren(rootItem);
            Reload();
        }
        protected override void SelectionChanged(IList<int> selectedIds)
        {
            base.SelectionChanged(selectedIds);

            if (selectedIds.Count == 1)
            {
                var item = FindItemInVisibleRows(selectedIds[0]);
                if (item != null && item.group != null)
                {
                    Selection.activeObject = item.group;
                }
            }

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
            if (!string.IsNullOrEmpty(customSearchString))
            {
                var z = rows.Where(s => DoesItemMatchSearch(s, customSearchString)).ToList();
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
            if (!root.hasChildren)
                return;
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
                if (child.entry != null)
                    kids.Add(child);
            }

            ColumnID col = m_SortOptions[sortedColumns[0]];
            bool ascending = multiColumnHeader.IsSortedAscending(sortedColumns[0]);

            IEnumerable<AssetEntryTreeViewItem> orderedKids = kids;
            switch (col)
            {
                case ColumnID.Type:
                    break;
                case ColumnID.Path:
                    orderedKids = kids.Order(l => l.entry.AssetPath, ascending);
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
            foreach (var k in kids)
            {
                if (k.entry == null || k.entry.labels == null || k.entry.labels.Count < 1)
                    emptyHalf.Add(k);
                else
                    namedHalf.Add(k);
            }
            var orderedKids = namedHalf.Order(l => editor.settings.labelTable.GetString(l.entry.labels, 200), ascending);

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
            if (item == null)
                return false;
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

            if (aeItem.entry.AssetPath.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
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
                using (new EditorGUI.DisabledScope(item.group.ReadOnly))
                {
                    base.RowGUI(args);
                }
            }
            else if (item.entry != null && !args.isRenaming)
            {
                using (new EditorGUI.DisabledScope(item.entry.ReadOnly))
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
                        labelStyle.Draw(cellRect, item.entry.AssetPath, false, false, args.selected, args.focused);
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
                            readOnly |= aeItem.entry.ReadOnly;
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
            retVal[counter].maxWidth = 10000;
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

        protected bool CheckForRename(TreeViewItem item, bool isActualRename)
        {
            bool result = false;
            var assetItem = item as AssetEntryTreeViewItem;
            if (assetItem != null)
            {
                if (assetItem.group != null)
                    result = !assetItem.group.ReadOnly;
                else if (assetItem.entry != null)
                    result = !assetItem.entry.ReadOnly;
                if (isActualRename)
                    assetItem.isRenaming = result;
            }
            return result;
        }
        protected override bool CanRename(TreeViewItem item)
        {
            return CheckForRename(item, true);
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
                item.isRenaming = false;
            }

            if (args.originalName == args.newName)
                return;

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
                        Addressables.LogWarning("There is already a group named '" + args.newName + "'.  Cannot rename this group to match");
                    }
                    else
                        item.group.Name = args.newName;
                }
                Reload();
            }
        }

        protected override void DoubleClickedItem(int id)
        {
            var item = FindItemInVisibleRows(id);
            if (item != null)
            {

                UnityEngine.Object o = null;
                if (item.entry != null)
                    o = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(item.entry.AssetPath);
                else if (item.group != null)
                    o = item.group;

                if (o != null)
                {
                    EditorGUIUtility.PingObject(o);
                    Selection.activeObject = o;
                }
            }
        }

        bool m_ContextOnItem = false;
        protected override void ContextClicked()
        {
            if (m_ContextOnItem)
            {
                m_ContextOnItem = false;
                return;
            }

            GenericMenu menu = new GenericMenu();
            foreach (var st in editor.settings.SchemaTemplates)
                menu.AddItem(new GUIContent("Create New Group/" + st.DisplayName, st.Description), false, CreateNewGroup, st);
            var bundleList = AssetDatabase.GetAllAssetBundleNames();
            if (bundleList != null && bundleList.Length > 0)
                menu.AddItem(new GUIContent("Convert Legacy Bundles"), false, editor.window.OfferToConvert);

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
                    hasReadOnly |= item.group.ReadOnly;
                    isGroup = true;
                }
                else if (item.entry != null)
                {
                    if (item.entry.AssetPath == AddressableAssetEntry.ResourcesPath)
                    {
                        if (selectedNodes.Count > 1)
                            return;
                        isResourcesHeader = true;
                    }
                    else if (item.entry.AssetPath == AddressableAssetEntry.EditorSceneListPath)
                    {
                        return;
                    }
                    hasReadOnly |= item.entry.ReadOnly;
                    isEntry = true;
                    resourceCount += item.entry.IsInResources ? 1 : 0;
                    sceneListCount += item.entry.IsInSceneList ? 1 : 0;
                }
            }
            if (isEntry && isGroup)
                return;

            GenericMenu menu = new GenericMenu();
            if (isResourcesHeader)
            {
                foreach (var g in editor.settings.groups)
                {
                    if (!g.ReadOnly)
                        menu.AddItem(new GUIContent("Move ALL Resources to group/" + g.Name), false, MoveAllResourcesToGroup, g);
                }
            }
            else if (!hasReadOnly)
            {
                if (isGroup)
                {
                    menu.AddItem(new GUIContent("Remove Group(s)"), false, RemoveGroup, selectedNodes);

                    if (selectedNodes.Count == 1)
                    {
                        if (!selectedNodes.First().group.Default)
                            menu.AddItem(new GUIContent("Set as Default"), false, SetGroupAsDefault, selectedNodes);
                        menu.AddItem(new GUIContent("Inspect Group Settings"), false, GoToGroupAsset, selectedNodes);
                    }
                }
                if (isEntry)
                {
                    foreach (var g in editor.settings.groups)
                    {
                        if (!g.ReadOnly)
                            menu.AddItem(new GUIContent("Move entries to group/" + g.Name), false, MoveEntriesToGroup, g);
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
                            if (!g.ReadOnly)
                                menu.AddItem(new GUIContent("Move entries to group/" + g.Name), false, MoveResourcesToGroup, g);
                        }
                    }
                    else if (resourceCount == 0)
                    {
                        foreach (var g in editor.settings.groups)
                        {
                            if (!g.ReadOnly)
                                menu.AddItem(new GUIContent("Move entries to group/" + g.Name), false, MoveEntriesToGroup, g);
                        }
                    }
                }
            }

            if (selectedNodes.Count == 1)
            {
                if (CheckForRename(selectedNodes.First(), false))
                    menu.AddItem(new GUIContent("Rename"), false, RenameItem, selectedNodes);
            }
            menu.ShowAsContext();
        }

        private void GoToGroupAsset(object context)
        {
            List<AssetEntryTreeViewItem> selectedNodes = context as List<AssetEntryTreeViewItem>;
            if (selectedNodes.Count == 0)
                return;
            var group = selectedNodes.First().group;
            if (group == null)
                return;
            EditorGUIUtility.PingObject(group);
            Selection.activeObject = group;
        }

        void CreateExternalEntryCollection(object context)
        {
            var path = EditorUtility.SaveFilePanel("Create Entry Collection", "Assets", "AddressableEntryCollection", "asset");
            if (!string.IsNullOrEmpty(path))
            {
                List<AssetEntryTreeViewItem> selectedNodes = context as List<AssetEntryTreeViewItem>;
                var col = ScriptableObject.CreateInstance<AddressableAssetEntryCollection>();
                foreach (var item in selectedNodes)
                {
                    item.entry.ReadOnly = true;
                    item.entry.IsSubAsset = true;
                    col.Entries.Add(item.entry);
                    editor.settings.RemoveAssetEntry(item.entry.guid, false);
                }
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
            if (item != null && item.children != null)
            {
                SafeMoveResourcesToGroup(targetGroup, item.children.ConvertAll(instance => (AssetEntryTreeViewItem)instance));
            }
            else
                Debug.LogWarning("No Resources found to move");
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

        bool SafeMoveResourcesToGroup(AddressableAssetGroup targetGroup, List<AssetEntryTreeViewItem> itemList)
        {
            var guids = new List<string>();
            var paths = new List<string>();
            foreach (AssetEntryTreeViewItem child in itemList)
            {
                if (child != null)
                {
                    guids.Add(child.entry.guid);
                    paths.Add(child.entry.AssetPath);
                }
            }
            return SafeMoveResourcesToGroup(targetGroup, paths, guids);
        }
        bool SafeMoveResourcesToGroup(AddressableAssetGroup targetGroup, List<string> paths)
        {
            var guids = new List<string>();
            foreach (var p in paths)
            {
                guids.Add(AssetDatabase.AssetPathToGUID(p));
            }
            return SafeMoveResourcesToGroup(targetGroup, paths, guids);
        }
        bool SafeMoveResourcesToGroup(AddressableAssetGroup targetGroup, List<string> paths, List<string> guids)
        {
            if (guids == null || guids.Count == 0 || paths == null || guids.Count != paths.Count)
            {
                Debug.LogWarning("No valid Resources found to move");
                return false;
            }

            if (targetGroup == null)
            {
                Debug.LogWarning("No valid group to move Resources to");
                return false;
            }

            Dictionary<string, string> guidToNewPath = new Dictionary<string, string>();

            var message = "Any assets in Resources that you wish to mark as Addressable must be moved within the project. We will move the files to:\n\n";
            for (int i = 0; i < guids.Count; i++)
            {
                var newName = paths[i].Replace("\\", "/");
                newName = newName.Replace("Resources", "Resources_moved");
                newName = newName.Replace("resources", "resources_moved");
                if (newName == paths[i])
                    continue;

                guidToNewPath.Add(guids[i], newName);
                message += newName + "\n";
            }
            message += "\nAre you sure you want to proceed?";
            if (EditorUtility.DisplayDialog("Move From Resources", message, "Yes", "No"))
            {
                editor.settings.MoveAssetsFromResources(guidToNewPath, targetGroup);
                return true;
            }
            return false;
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

        protected void CreateNewGroup(object context)
        {
            var schemaTemplate = context as AddressableAssetGroupSchemaTemplate;
            if (schemaTemplate != null)
            {
                editor.settings.CreateGroup(schemaTemplate.DisplayName, false, false, true, schemaTemplate.GetTypes());
            }
            else
            {
                editor.settings.CreateGroup("", false, false, false);
            }
        }

        internal void SetGroupAsDefault(object context)
        {
            List<AssetEntryTreeViewItem> selectedNodes = context as List<AssetEntryTreeViewItem>;
            if (selectedNodes.Count == 0)
                return;
            var group = selectedNodes.First().group;
            if (group == null)
                return;
            editor.settings.DefaultGroup = group;
            Reload();
        }

        protected void RemoveGroup(object context)
        {
            if (EditorUtility.DisplayDialog("Delete selected groups?", "Are you sure you want to delete the selected groups?\n\nYou cannot undo this action.", "Yes", "No"))
            {
                List<AssetEntryTreeViewItem> selectedNodes = context as List<AssetEntryTreeViewItem>;
                var groups = new List<AddressableAssetGroup>();
                foreach (var item in selectedNodes)
                {
                    editor.settings.RemoveGroupInternal(item.group, true, false);
                    groups.Add(item.group);
                }
                editor.settings.SetDirty(AddressableAssetSettings.ModificationEvent.GroupRemoved, groups, true);
            }
        }
        /*
        protected void ExtractCommonAssets(object context)
        {
            var items = (List<AssetEn tryTreeViewItem>)context;
            var groups = new List<AddressableAssetGroup>();
            foreach (var item in items)
                if (item.group != null)
                    groups.Add(item.group);
            
            var common = BuildScript.ExtractCommonAssets(editor.settings, groups);
            if (common.Count > 0)
            {
                var group = editor.settings.CreateGroup("Common", false, false, false, false);
                var entries = new List<AddressableAssetEntry>();
                foreach (var guid in common)
                    entries.Add(editor.settings.CreateOrMoveEntry(guid.ToString(), group));
                editor.settings.PostModificationEvent(AddressableAssetSettings.ModificationEvent.EntryMoved, entries);
            }
        }
        */
        protected void SimplifyAddresses(object context)
        {
            List<AssetEntryTreeViewItem> selectedNodes = context as List<AssetEntryTreeViewItem>;
            var entries = new List<AddressableAssetEntry>();

            foreach (var item in selectedNodes)
            {
                item.entry.SetAddress(System.IO.Path.GetFileNameWithoutExtension(item.entry.address), false);
                entries.Add(item.entry);
            }
            editor.settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entries, true);
        }

        protected void RemoveEntry(object context)
        {
            if (EditorUtility.DisplayDialog("Delete selected entries?", "Are you sure you want to delete the selected entries?\n\nYou cannot undo this action.", "Yes", "No"))
            {
                List<AssetEntryTreeViewItem> selectedNodes = context as List<AssetEntryTreeViewItem>;
                var entries = new List<AddressableAssetEntry>();
                foreach (var item in selectedNodes)
                {
                    if (item.entry != null)
                    {
                        editor.settings.RemoveAssetEntry(item.entry.guid, false);
                        entries.Add(item.entry);
                    }
                }
                editor.settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryRemoved, entries, true);
            }
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
                foreach (var nodeID in GetSelection())
                {
                    var item = FindItemInVisibleRows(nodeID) as AssetEntryTreeViewItem;
                    if (item != null)
                        selectedNodes.Add(item);
                    if (item.entry == null)
                        allEntries = false;
                    else
                        allGroups = false;
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
                        if (item.entry.IsInResources)
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

            if (target.entry != null && target.entry.ReadOnly)
                return DragAndDropVisualMode.None;


            if (DragAndDrop.paths != null && DragAndDrop.paths.Length > 0)
            {
                if (!AddressableAssetUtility.IsPathValidForEntry(DragAndDrop.paths[0]))
                    visualMode = DragAndDropVisualMode.Rejected;
                else
                    visualMode = DragAndDropVisualMode.Copy;

                if (args.performDrop && visualMode != DragAndDropVisualMode.Rejected)
                {
                    AddressableAssetGroup parent = null;
                    if (target.group != null)
                        parent = target.group;
                    else if (target.entry != null)
                        parent = target.entry.parentGroup;

                    if (parent != null)
                    {
                        var resourcePaths = new List<string>();
                        var nonResourcePaths = new List<string>();
                        foreach (var p in DragAndDrop.paths)
                        {
                            if (AddressableAssetUtility.IsInResources(p))
                                resourcePaths.Add(p);
                            else
                                nonResourcePaths.Add(p);
                        }
                        bool canMarkNonResources = true;
                        if (resourcePaths.Count > 0)
                        {
                            canMarkNonResources = SafeMoveResourcesToGroup(parent, resourcePaths);
                        }
                        if (canMarkNonResources)
                        {
                            var entries = new List<AddressableAssetEntry>();
                            foreach (var p in nonResourcePaths)
                            {
                                entries.Add(editor.settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(p), parent, false, false));
                            }
                            editor.settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entries, true);
                        }

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
                            if (draggedNodes.First().entry.IsInResources)
                            {
                                SafeMoveResourcesToGroup(parent, draggedNodes);
                            }
                            else
                            {
                                var entries = new List<AddressableAssetEntry>();
                                foreach (var node in draggedNodes)
                                    entries.Add(editor.settings.CreateOrMoveEntry(node.entry.guid, parent, false, false));
                                editor.settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entries, true);
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
        public bool isRenaming;

        public AssetEntryTreeViewItem(AddressableAssetEntry e, int d) : base((e.address + e.guid).GetHashCode(), d, e.address)
        {
            entry = e;
            group = null;
            assetIcon = AssetDatabase.GetCachedIcon(e.AssetPath) as Texture2D;
            isRenaming = false;
        }

        public AssetEntryTreeViewItem(AddressableAssetGroup g, int d) : base(g.Guid.GetHashCode(), d, g.Name)
        {
            entry = null;
            group = g;
            assetIcon = null;
            isRenaming = false;
        }

        public override string displayName
        {
            get
            {
                if (!isRenaming && group != null && group.Default)
                    return base.displayName + " (Default)";
                return base.displayName;
            }

            set
            {
                base.displayName = value;
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
