using UnityEngine;
using System;
using UnityEditor.IMGUI.Controls;
using System.Collections.Generic;
using UnityEngine.AddressableAssets;

namespace UnityEditor.AddressableAssets
{
    [CustomPropertyDrawer(typeof(AssetReference))]
    internal class AssetReferenceDrawer : PropertyDrawer
    {
        public string newGuid;
        public string newGuidPropertyPath;
        string assetName;
        Rect smallPos;

        public AssetReferenceLabelRestriction labelFilter;
        public AssetReferenceTypeRestriction typeFilter;
        public const string k_noAssetString = "None (AddressableAsset)";

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            label.text = ObjectNames.NicifyVariableName(property.propertyPath);
            EditorGUI.BeginProperty(position, label, property);

            GatherFilters(property, out labelFilter, out typeFilter);
            
            var guidProp = property.FindPropertyRelative("assetGUID");

            if (!string.IsNullOrEmpty(newGuid) && newGuidPropertyPath == property.propertyPath)
            {
                var objProp = property.FindPropertyRelative("_cachedAsset");
                if (newGuid == k_noAssetString)
                {
                    guidProp.stringValue = string.Empty;
                    objProp.objectReferenceValue = null;

                    newGuid = string.Empty;
                }
                else
                {
                    guidProp.stringValue = newGuid;

                    var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(AssetDatabase.GUIDToAssetPath(newGuid));
                    objProp.objectReferenceValue = obj;

                    newGuid = string.Empty;
                }
            }

            assetName = k_noAssetString;
            Texture2D icon = null;
            var aaSettings = AddressableAssetSettings.GetDefault(false, false);
            if (aaSettings != null && !string.IsNullOrEmpty(guidProp.stringValue))
            {
                var entry = aaSettings.FindAssetEntry(guidProp.stringValue);
                if (entry != null)
                {
                    assetName = entry.address;
                    icon = AssetDatabase.GetCachedIcon(entry.assetPath) as Texture2D;
                }
            }

            if (labelFilter != null)
                label.text += " (label=" + labelFilter + ")";
            if (typeFilter != null)
                label.text += " (type=" + typeFilter + ")";
            smallPos = EditorGUI.PrefixLabel(position, label);
            var nameToUse = assetName;
            if (System.IO.File.Exists(assetName))
                nameToUse = System.IO.Path.GetFileNameWithoutExtension(assetName);
            if (EditorGUI.DropdownButton(smallPos, new GUIContent(nameToUse, icon, "Addressable Asset Reference"), FocusType.Keyboard))
            {
                newGuidPropertyPath = property.propertyPath;
                PopupWindow.Show(smallPos, new AssetReferencePopup(this));
            }


            if (Event.current.type == EventType.DragUpdated && position.Contains(Event.current.mousePosition))
            {
                bool rejected = false;
                if (typeFilter != null)
                {
                    UnityEngine.Object obj = null;
                    var aaEntries = DragAndDrop.GetGenericData("AssetEntryTreeViewItem") as List<AssetEntryTreeViewItem>;
                    if (aaEntries != null)
                    {
                        if (aaEntries.Count != 1)
                            rejected = true;
                        if (rejected && !typeFilter.Validate(AssetDatabase.GetMainAssetTypeAtPath(aaEntries[0].entry.assetPath)))
                            rejected = true;
                    }
                    else
                    {
                        if (DragAndDrop.objectReferences != null && DragAndDrop.objectReferences.Length == 1)
                            obj = DragAndDrop.objectReferences[0];
                        if (obj == null)
                            rejected = true;

                        if (!rejected && !typeFilter.Validate(obj.GetType()))
                            rejected = true;
                    }
                }

                if (!rejected && labelFilter != null)
                {
                    var aaEntries = DragAndDrop.GetGenericData("AssetEntryTreeViewItem") as List<AssetEntryTreeViewItem>;
                    if (aaEntries != null)
                    {
                        if (aaEntries.Count != 1)
                            rejected = true;
                        if (rejected && !labelFilter.Validate(aaEntries[0].entry.labels))
                            rejected = true;
                    }
                    else
                    {
                        if (DragAndDrop.paths.Length == 1)
                        {
                            var guid = AssetDatabase.AssetPathToGUID(DragAndDrop.paths[0]);
                            var entry = aaSettings.FindAssetEntry(guid);
                            //for now, do not allow creation of new AssetEntries when there is a label filter on the property.
                            //This could be changed in the future to be configurable via the attribute if desired (allowEntryCreate = true)
                            if (entry == null)
                                rejected = true;
                            if (!rejected && !labelFilter.Validate(entry.labels))
                                rejected = true;
                        }
                        else
                        {
                            rejected = true;
                        }
                    }
                }
                
                DragAndDrop.visualMode = rejected ? DragAndDropVisualMode.Rejected : DragAndDropVisualMode.Copy;
            }
            if (Event.current.type == EventType.DragPerform && position.Contains(Event.current.mousePosition))
            {
                var aaEntries = DragAndDrop.GetGenericData("AssetEntryTreeViewItem") as List<AssetEntryTreeViewItem>;
                if (aaEntries != null)
                {
                    if (aaEntries.Count == 1)
                    {
                        var entry = aaEntries[0].entry;
                        guidProp.stringValue = entry.guid;
                        var objProp = property.FindPropertyRelative("_cachedAsset");
                        objProp.objectReferenceValue = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(entry.assetPath);
                    }
                }
                else
                {
                    if (DragAndDrop.paths != null && DragAndDrop.paths.Length == 1)
                    {
                        UnityEngine.Object obj = null;
                        if (DragAndDrop.objectReferences != null && DragAndDrop.objectReferences.Length == 1)
                            obj = DragAndDrop.objectReferences[0];
                        var newPath = DragAndDrop.paths[0];
                        var newGuid = AssetDatabase.AssetPathToGUID(newPath);
                        var entry = aaSettings.FindAssetEntry(newGuid);
                        if (entry == null && !string.IsNullOrEmpty(newGuid))
                        {
                            aaSettings.CreateOrMoveEntry(newGuid, aaSettings.DefaultGroup);
                            Debug.Log("Creating AddressableAsset " + newPath + " in group " + aaSettings.DefaultGroup.name);
                        }
                        guidProp.stringValue = newGuid;
                        var objProp = property.FindPropertyRelative("_cachedAsset");
                        objProp.objectReferenceValue = obj;
                    }
                }
            }
            EditorGUI.EndProperty();
        }

        private void GatherFilters(SerializedProperty property, out AssetReferenceLabelRestriction labelFilter, out AssetReferenceTypeRestriction typeFilter)
        {
            labelFilter = null;
            typeFilter = null;
            var o = property.serializedObject.targetObject;
            if (o != null)
            {
                var t = o.GetType();
                if (t != null)
                {
                    string propertyName = property.name;
                    int i = property.propertyPath.IndexOf('.');
                    if (i > 0)
                        propertyName = property.propertyPath.Substring(0, i);
                    var f = t.GetField(propertyName);
                    if (f != null)
                    {
                        var a = f.GetCustomAttributes(false);
                        foreach (var attr in a)
                        {
                            var labelFilterAttribute = attr as AssetReferenceLabelRestriction;
                            if (labelFilterAttribute != null)
                                labelFilter = labelFilterAttribute;
                            var typeFilterAttribute = attr as AssetReferenceTypeRestriction;
                            if (typeFilterAttribute != null)
                                typeFilter = typeFilterAttribute;
                        }
                    }
                }
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return base.GetPropertyHeight(property, label);
        }

        class AssetReferencePopup : PopupWindowContent
        {
            AssetReferenceTreeView tree;
            [SerializeField]
            TreeViewState treeState;

            string currentName = string.Empty;
            AssetReferenceDrawer m_drawer;

            SearchField searchField;

            public AssetReferencePopup(AssetReferenceDrawer drawer)
            {
                m_drawer = drawer;
                searchField = new SearchField();
            }

            public override void OnClose()
            {
                base.OnClose();
            }

            public override void OnOpen()
            {
                searchField.SetFocus();
                base.OnOpen();
            }

            public override Vector2 GetWindowSize()
            {
                Vector2 result = base.GetWindowSize();
                result.x = m_drawer.smallPos.width;
                return result;
            }

            public override void OnGUI(Rect rect)
            {
                int border = 4;
                int topPadding = 12;
                int searchHeight = 20;
                var searchRect = new Rect(border, topPadding, rect.width - border * 2, searchHeight);
                var remainTop = topPadding + searchHeight + border;
                var remainginRect = new Rect(border, topPadding + searchHeight + border, rect.width - border * 2, rect.height - remainTop - border);
                currentName = searchField.OnGUI(searchRect, currentName);


                if (tree == null)
                {
                    if (treeState == null)
                        treeState = new TreeViewState();
                    tree = new AssetReferenceTreeView(treeState, m_drawer);
                    tree.Reload();
                }
                tree.searchString = currentName;
                tree.OnGUI(remainginRect);
            }

            private class AssetRefTreeViewItem : TreeViewItem
            {
                public string guid;
                public AssetRefTreeViewItem(int id, int depth, string displayName, string g, string path) : base(id, depth, displayName)
                {
                    guid = g;
                    icon = AssetDatabase.GetCachedIcon(path) as Texture2D;
                }
            }
            private class AssetReferenceTreeView : TreeView
            {
                AssetReferenceDrawer m_drawer;
                public AssetReferenceTreeView(TreeViewState state, AssetReferenceDrawer drawer) : base(state)
                {
                    m_drawer = drawer;
                    showBorder = true;
                    showAlternatingRowBackgrounds = true;
                }

                protected override bool CanMultiSelect(TreeViewItem item)
                {
                    return false;
                }

                protected override void SelectionChanged(IList<int> selectedIds)
                {
                    if (selectedIds.Count == 1)
                    {
                        var assetRefItem = FindItem(selectedIds[0], rootItem) as AssetRefTreeViewItem;
                        if (!string.IsNullOrEmpty(assetRefItem.guid))
                        {
                            m_drawer.newGuid = assetRefItem.guid;
                        }
                        else
                        {
                            m_drawer.newGuid = k_noAssetString;
                        }
                        PopupWindow.focusedWindow.Close();
                    }
                }

                protected override IList<TreeViewItem> BuildRows(TreeViewItem root)
                {
                    if (string.IsNullOrEmpty(searchString))
                    {
                        return base.BuildRows(root);
                    }
                    else
                    {
                        List<TreeViewItem> rows = new List<TreeViewItem>();

                        foreach (var child in rootItem.children)
                        {
                            if (child.displayName.IndexOf(searchString, StringComparison.OrdinalIgnoreCase) >= 0)
                                rows.Add(child);
                        }

                        return rows;
                    }
                }

                protected override TreeViewItem BuildRoot()
                {
                    var root = new TreeViewItem(-1, -1);

                    var aaSettings = AddressableAssetSettings.GetDefault(false, false);
                    if (aaSettings == null)
                    {
                        var message = "Use 'Window->Addressable Assets' to initialize.";
                        root.AddChild(new AssetRefTreeViewItem(message.GetHashCode(), 0, message, string.Empty, string.Empty));
                    }
                    else
                    {
                        root.AddChild(new AssetRefTreeViewItem(k_noAssetString.GetHashCode(), 0, k_noAssetString, string.Empty, string.Empty));
                        foreach (var entry in aaSettings.assetEntries)
                        {
                            if ((entry.guid != AddressableAssetSettings.AssetGroup.AssetEntry.EditorSceneListName) &&
                                (entry.guid != AddressableAssetSettings.AssetGroup.AssetEntry.ResourcesName))
                            {
                                bool passedFilters = true;
                                if (m_drawer.labelFilter != null && !m_drawer.labelFilter.Validate(entry.labels))
                                    passedFilters = false;

                                if (passedFilters && m_drawer.typeFilter != null && !m_drawer.typeFilter.Validate(AssetDatabase.GetMainAssetTypeAtPath(entry.assetPath)))
                                    passedFilters = false;
                               
                                if(passedFilters)
                                    root.AddChild(new AssetRefTreeViewItem(entry.address.GetHashCode(), 0, entry.address, entry.guid, entry.assetPath));
                            }
                        }
                    }

                    return root;
                }
            }
        }
    }
}
