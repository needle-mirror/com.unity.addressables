using UnityEngine;
using System;
using UnityEditor.IMGUI.Controls;
using System.Collections.Generic;
using UnityEngine.AddressableAssets;
using System.Linq;
using UnityEditor.Graphs;

namespace UnityEditor.AddressableAssets
{
    [CustomPropertyDrawer(typeof(AssetReference), true)]
    internal class AssetReferenceDrawer : PropertyDrawer
    {
        public string newGuid;
        public string newGuidPropertyPath;
        string assetName;
        internal Rect smallPos;
        bool filtersGathered = false;
        public AssetReferenceLabelRestriction labelFilter;
        public AssetReferenceTypeRestriction typeFilter;
        internal const string k_noAssetString = "None (AddressableAsset)";
        AssetReference assetRefObject = null;
        bool SetObject(SerializedProperty property, UnityEngine.Object obj, out string guid)
        {
            guid = null;
            try
            {
                if (assetRefObject == null)
                    return false;
                if (obj == null)
                {
                    assetRefObject.editorAsset = null;
                    EditorUtility.SetDirty(property.serializedObject.targetObject);
                    var comp = property.serializedObject.targetObject as Component;
                    if (comp != null && comp.gameObject != null && comp.gameObject.activeInHierarchy)
                        SceneManagement.EditorSceneManager.MarkSceneDirty(comp.gameObject.scene);
                    return true;
                }
                if (assetRefObject.ValidateType(obj.GetType()))
                {
                    long lfid;
                    if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out guid, out lfid))
                    {
                        assetRefObject.editorAsset = obj;
                    }
                    EditorUtility.SetDirty(property.serializedObject.targetObject);
                    var comp = property.serializedObject.targetObject as Component;
                    if (comp != null && comp.gameObject != null && comp.gameObject.activeInHierarchy)
                        SceneManagement.EditorSceneManager.MarkSceneDirty(comp.gameObject.scene);
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
            {
                Debug.LogError("Error rendering drawer for AssetReference property.");
                return;
            }
            
            assetRefObject = property.GetActualObjectForSerializedProperty<AssetReference>(fieldInfo, ref label);
            
            if (assetRefObject == null)
            {
                return;
            }
            
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
            var aaSettings = AddressableAssetSettingsDefaultObject.Settings;
            if (aaSettings != null && !string.IsNullOrEmpty(guid))
            {
                var entry = aaSettings.FindAssetEntry(guid);
                if (entry != null)
                {
                    assetName = entry.address;
                    icon = AssetDatabase.GetCachedIcon(entry.AssetPath) as Texture2D;
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
                        if (rejected && !typeFilter.Validate(AssetDatabase.GetMainAssetTypeAtPath(aaEntries[0].entry.AssetPath)))
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
                    if (aaSettings == null)
                        rejected = true;
                    else
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
                        var item = aaEntries[0];
                        if (item.entry != null)
                        {
                            if (item.entry.IsInResources)
                                Debug.LogWarning("Cannot use an AssetReference on an asset in Resources. Move asset out of Resources first.");
                            else
                                SetObject(property, AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(item.entry.AssetPath), out guid);
                        }
                    }
                }
                else
                {
                    if (DragAndDrop.paths != null && DragAndDrop.paths.Length == 1)
                    {
                        var path = DragAndDrop.paths[0];
                        if (AddressableAssetUtility.IsInResources(path))
                            Debug.LogWarning("Cannot use an AssetReference on an asset in Resources. Move asset out of Resources first.");
                        else
                        {
                            UnityEngine.Object obj = null;
                            if (DragAndDrop.objectReferences != null && DragAndDrop.objectReferences.Length == 1)
                                obj = DragAndDrop.objectReferences[0];
                            else
                                obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);

                            if (SetObject(property, obj, out guid))
                            {
                                aaSettings = AddressableAssetSettingsDefaultObject.GetSettings(true);
                                var entry = aaSettings.FindAssetEntry(guid);
                                if (entry == null && !string.IsNullOrEmpty(guid))
                                {
                                    entry = aaSettings.CreateOrMoveEntry(guid, aaSettings.DefaultGroup);
                                    Addressables.LogFormat("Created AddressableAsset {0} in group {1}.", entry.address, aaSettings.DefaultGroup.Name);
                                }
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
    }

    class AssetReferencePopup : PopupWindowContent
    {
        AssetReferenceTreeView tree;
        [SerializeField]
        TreeViewState treeState;
        bool shouldClose;
        public void ForceClose()
        {
            shouldClose = true;
        }

        string currentName = string.Empty;
        AssetReferenceDrawer m_drawer;

        SearchField searchField;

        internal AssetReferencePopup(AssetReferenceDrawer drawer)
        {
            m_drawer = drawer;
            searchField = new SearchField();
            shouldClose = false;
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
                tree = new AssetReferenceTreeView(treeState, m_drawer, this);
                tree.Reload();
            }
            tree.searchString = currentName;
            tree.OnGUI(remainginRect);

            if (shouldClose)
            {
                EditorGUIUtility.hotControl = 0;
                editorWindow.Close();
            }
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
            AssetReferencePopup m_popup;
            public AssetReferenceTreeView(TreeViewState state, AssetReferenceDrawer drawer, AssetReferencePopup popup) : base(state)
            {
                m_drawer = drawer;
                m_popup = popup;
                showBorder = true;
                showAlternatingRowBackgrounds = true;
            }

            protected override bool CanMultiSelect(TreeViewItem item)
            {
                return false;
            }

            protected override void SelectionChanged(IList<int> selectedIds)
            {
                if (selectedIds != null && selectedIds.Count == 1)
                {
                    var assetRefItem = FindItem(selectedIds[0], rootItem) as AssetRefTreeViewItem;
                    if (!string.IsNullOrEmpty(assetRefItem.guid))
                    {
                        m_drawer.newGuid = assetRefItem.guid;
                    }
                    else
                    {
                        m_drawer.newGuid = AssetReferenceDrawer.k_noAssetString;
                    }
                    m_popup.ForceClose();
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

                var aaSettings = AddressableAssetSettingsDefaultObject.Settings;
                if (aaSettings == null)
                {
                    var message = "Use 'Window->Addressable Assets' to initialize.";
                    root.AddChild(new AssetRefTreeViewItem(message.GetHashCode(), 0, message, string.Empty, string.Empty));
                }
                else
                {
                    root.AddChild(new AssetRefTreeViewItem(AssetReferenceDrawer.k_noAssetString.GetHashCode(), 0, AssetReferenceDrawer.k_noAssetString, string.Empty, string.Empty));
                    var allAssets = new List<AddressableAssetEntry>();
                    aaSettings.GetAllAssets(allAssets);
                    foreach (var entry in allAssets)
                    {
                        bool passedFilters = true;
                        if (m_drawer.labelFilter != null && !m_drawer.labelFilter.Validate(entry.labels))
                            passedFilters = false;

                        if (passedFilters && m_drawer.typeFilter != null && !m_drawer.typeFilter.Validate(AssetDatabase.GetMainAssetTypeAtPath(entry.AssetPath)))
                            passedFilters = false;

                        if (passedFilters)
                            root.AddChild(new AssetRefTreeViewItem(entry.address.GetHashCode(), 0, entry.address, entry.guid, entry.AssetPath));
                    }
                }

                return root;
            }
        }
    }

    internal static class SerializedPropertyExtensions
    {
        public static T GetActualObjectForSerializedProperty<T>(this SerializedProperty property, System.Reflection.FieldInfo field, ref GUIContent label) where T : class, new()
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

                if (property.depth > 0)
                {
                    var slicedName = property.propertyPath.Split('.').ToList();
                    List<int> arrayCounts = new List<int>();
                    for (int index = 0; index < slicedName.Count; index++)
                    {
                        arrayCounts.Add(-1);
                        var currName = slicedName[index];
                        if (currName.EndsWith("]"))
                        {
                            var arraySlice = currName.Split(new char[] { '[', ']' });
                            if (arraySlice.Length >= 2)
                            {
                                arrayCounts[index - 2] = Convert.ToInt32(arraySlice[1]);
                                slicedName[index] = string.Empty;
                                slicedName[index - 1] = string.Empty;
                            }
                        }
                    }
                    
                    while (string.IsNullOrEmpty(slicedName.Last()))
                    {
                        int i = slicedName.Count - 1;
                        slicedName.RemoveAt(i);
                        arrayCounts.RemoveAt(i);
                    }

                    if (property.propertyPath.EndsWith("]"))
                    {
                        var slice = property.propertyPath.Split(new char[] { '[', ']' });
                        if (slice.Length >= 2)
                            label.text = "Element " + slice[slice.Length - 2];
                    }
                    else
                    {
                        label.text = slicedName.Last();
                    }

                    return DescendHierarchy<T>(property, targetObject, slicedName, arrayCounts, 0);
                }
                else
                {
                    var obj = field.GetValue(targetObject);
                    return obj as T;
                }
            }
            catch
            {
                return null;
            }
        }
        private static T DescendHierarchy<T>(SerializedProperty property, object targetObject, List<string> splitName, List<int> splitCounts, int depth) where T : class, new()
        {
            if (depth >= splitName.Count)
                return null;

            var currName = splitName[depth];

            if (string.IsNullOrEmpty(currName))
                return DescendHierarchy<T>(property, targetObject, splitName, splitCounts, depth + 1);


            int arrayIndex = splitCounts[depth];

            var newField = targetObject.GetType().GetField(currName);
            var newObj = newField.GetValue(targetObject);
            if (depth == splitName.Count - 1)
            {
                T actualObject = null;
                if (arrayIndex >= 0)
                {
                    if (newObj.GetType().IsArray && ((T[])newObj).Length > arrayIndex)
                        actualObject = ((T[])newObj)[arrayIndex];

                    var newObjList = newObj as List<T>;
                    if (newObjList != null && newObjList.Count > arrayIndex)
                    {
                        actualObject = newObjList[arrayIndex];
                        //if (actualObject == null)
                        //    actualObject = new T();
                    }
                }
                else
                {
                    actualObject = newObj as T;
                }
                return actualObject;
            }

            return DescendHierarchy<T>(property, newObj, splitName, splitCounts, depth + 1);
        }
    }

}
