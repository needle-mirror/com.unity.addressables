using UnityEngine;
using System;
using UnityEditor.IMGUI.Controls;
using System.Collections.Generic;
using UnityEngine.AddressableAssets;
using System.Linq;

namespace UnityEditor.AddressableAssets
{
    [CustomPropertyDrawer(typeof(AssetReference), true)]
    internal class AssetReferenceDrawer : PropertyDrawer
    {
        public string newGuid;
        public string newGuidPropertyPath;
        string assetName;
        Rect smallPos;
        bool filtersGathered = false;
        public AssetReferenceLabelRestriction labelFilter;
        public AssetReferenceTypeRestriction typeFilter;
        public const string k_noAssetString = "None (AddressableAsset)";
        bool SetObject(SerializedProperty property, UnityEngine.Object obj, out string guid)
        {
            guid = null;
            try
            {
                AssetReference assetRefObject = property.GetActualObjectForSerializedProperty<AssetReference>(fieldInfo);
                if (obj == null || assetRefObject == null || assetRefObject.ValidateType(obj.GetType()))
                {
                    long lfid;
                    if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out guid, out lfid))
                    {
                        var objProp = property.FindPropertyRelative("m_cachedAsset");
                        objProp.objectReferenceValue = obj;
                        var guidProp = property.FindPropertyRelative("m_assetGUID");
                        guidProp.stringValue = guid;
                    }
                    return true;
                }
                return false;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return false;
            }
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property == null || label == null)
                return;
            label.text = ObjectNames.NicifyVariableName(property.propertyPath);
            EditorGUI.BeginProperty(position, label, property);

            GatherFilters(property, ref labelFilter, ref typeFilter);
            var guidProp = property.FindPropertyRelative("m_assetGUID");
            string guid = guidProp.stringValue;

            if (!string.IsNullOrEmpty(newGuid) && newGuidPropertyPath == property.propertyPath)
            {
                if (newGuid == k_noAssetString)
                {
                    if (SetObject(property, null, out guid))
                        newGuid = string.Empty;
                }
                else
                {
                    if (SetObject(property, AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(AssetDatabase.GUIDToAssetPath(newGuid)), out guid))
                        newGuid = string.Empty;
                }
            }

            assetName = k_noAssetString;
            Texture2D icon = null;
            var aaSettings = AddressableAssetSettings.GetDefault(false, false);
            if (aaSettings != null && !string.IsNullOrEmpty(guid))
            {
                var entry = aaSettings.FindAssetEntry(guid);
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
                            var entry = aaSettings.FindAssetEntry(AssetDatabase.AssetPathToGUID(DragAndDrop.paths[0]));
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
                        SetObject(property, AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(aaEntries[0].entry.assetPath), out guid);
                }
                else
                {
                    if (DragAndDrop.paths != null && DragAndDrop.paths.Length == 1)
                    {
                        UnityEngine.Object obj = null;
                        if (DragAndDrop.objectReferences != null && DragAndDrop.objectReferences.Length == 1)
                            obj = DragAndDrop.objectReferences[0];
                        else
                            obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(DragAndDrop.paths[0]);

                        if (SetObject(property, obj, out guid))
                        {
                            var entry = aaSettings.FindAssetEntry(guid);
                            if (entry == null && !string.IsNullOrEmpty(guid))
                            {
                                entry = aaSettings.CreateOrMoveEntry(guid, aaSettings.DefaultGroup);
                                Debug.LogFormat("Created AddressableAsset {0} in group {1}.", entry.address, aaSettings.DefaultGroup.Name);
                            }
                        }
                    }
                }
            }
            EditorGUI.EndProperty();
        }

        private void GatherFilters(SerializedProperty property, ref AssetReferenceLabelRestriction labelFilter, ref AssetReferenceTypeRestriction typeFilter)
        {
            if (filtersGathered)
                return;

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
            filtersGathered = true;
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
                    if (selectedIds!= null && selectedIds.Count == 1)
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
                        var allAssets = new List<AddressableAssetEntry>();
                        aaSettings.GetAllAssets(allAssets);
                        foreach (var entry in allAssets)
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

                    return root;
                }
            }
        }
    }

    public static class SerializedPropertyExtensions
    {
        public static T GetActualObjectForSerializedProperty<T>(this SerializedProperty property, System.Reflection.FieldInfo field) where T : class
        {
            try
            {
                if (property == null || field == null)
                    return null;
                var serializedObject = property.serializedObject;
                if (serializedObject == null)
                {
                    return null;
                }
                var targetObject = serializedObject.targetObject;
                var obj = field.GetValue(targetObject);
                if (obj == null)
                {
                    return null;
                }
                T actualObject = null;
                if (obj.GetType().IsArray)
                {
                    var index = Convert.ToInt32(new string(property.propertyPath.Where(c => char.IsDigit(c)).ToArray()));
                    actualObject = ((T[])obj)[index];
                }
                else
                {
                    actualObject = obj as T;
                }
                return actualObject;
            }
            catch
            {
                return null;
            }
        }
    }

}
