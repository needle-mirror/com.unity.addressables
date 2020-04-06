using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.IMGUI.Controls;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.U2D;

namespace UnityEditor.AddressableAssets.GUI
{
    using Object = UnityEngine.Object;

    [CustomPropertyDrawer(typeof(AssetReference), true)]
    class AssetReferenceDrawer : PropertyDrawer
    {
        public string newGuid;
        public string newGuidPropertyPath;
        string m_AssetName;
        internal Rect assetDropDownRect;
        internal const string noAssetString = "None (AddressableAsset)";
        AssetReference m_AssetRefObject;

        List<AssetReferenceUIRestrictionSurrogate> m_Restrictions = null;

#if UNITY_2019_1_OR_NEWER
        private Texture2D m_CaretTexture = null;
#endif

        /// <summary>
        /// Validates that the referenced asset allowable for this asset reference.
        /// </summary>
        /// <param name="path">The path to the asset in question.</param>
        /// <returns>Whether the referenced asset is valid.</returns>
        public bool ValidateAsset(string path)
        {
            if (m_AssetRefObject != null && m_AssetRefObject.ValidateAsset(path))
            {
                foreach (var restriction in m_Restrictions)
                {
                    if (!restriction.ValidateAsset(path))
                        return false;
                }
                return true;
            }

            return false;
        }

        bool SetObject(SerializedProperty property, Object target, out string guid)
        {
            guid = null;
            try
            {
                if (m_AssetRefObject == null)
                    return false;
                Undo.RecordObject(property.serializedObject.targetObject, "Assign Asset Reference");
                if (target == null)
                {
                    m_AssetRefObject.SetEditorAsset(null);
                    return true;
                }

                Object subObject = null;
                if (target.GetType() == typeof(Sprite))
                {
                    var atlasEntries = new List<AddressableAssetEntry>();
                    AddressableAssetSettingsDefaultObject.Settings.GetAllAssets(atlasEntries, false, null, e => AssetDatabase.GetMainAssetTypeAtPath(e.AssetPath) == typeof(SpriteAtlas));
                    var spriteName = target.name;
                    if (spriteName.EndsWith("(Clone)"))
                        spriteName = spriteName.Replace("(Clone)", "");

                    foreach (var a in atlasEntries)
                    {
                        var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(a.AssetPath);
                        if (atlas == null)
                            continue;
                        var s = atlas.GetSprite(spriteName);
                        if (s == null)
                            continue;
                        subObject = target;
                        target = atlas;
                        break;
                    }
                }
                var success = m_AssetRefObject.SetEditorAsset(target);
                if (success)
                {
                    if (subObject != null)
                        m_AssetRefObject.SetEditorSubObject(subObject);
                    guid = m_AssetRefObject.AssetGUID;
                    EditorUtility.SetDirty(property.serializedObject.targetObject);

                    var comp = property.serializedObject.targetObject as Component;
                    if (comp != null && comp.gameObject != null && comp.gameObject.activeInHierarchy)
                        EditorSceneManager.MarkSceneDirty(comp.gameObject.scene);
                }
                return success;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            return false;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property == null || label == null)
            {
                Debug.LogError("Error rendering drawer for AssetReference property.");
                return;
            }

            string labelText = label.text;
            m_AssetRefObject = property.GetActualObjectForSerializedProperty<AssetReference>(fieldInfo, ref labelText);
            label.text = labelText;
            if (m_AssetRefObject == null)
            {
                return;
            }

            EditorGUI.BeginProperty(position, label, property);

            GatherFilters(property);
            string guid = m_AssetRefObject.AssetGUID;
            var aaSettings = AddressableAssetSettingsDefaultObject.Settings;

            var checkToForceAddressable = string.Empty;
            if (!string.IsNullOrEmpty(newGuid) && newGuidPropertyPath == property.propertyPath)
            {
                if (newGuid == noAssetString)
                {
                    SetObject(property, null, out guid);
                    newGuid = string.Empty;
                }
                else
                {
                    if (SetObject(property, AssetDatabase.LoadAssetAtPath<Object>(AssetDatabase.GUIDToAssetPath(newGuid)), out guid))
                    {
                        checkToForceAddressable = newGuid;
                    }
                    newGuid = string.Empty;
                }
            }

            bool isNotAddressable = false;
            m_AssetName = noAssetString;
            if (aaSettings != null && !string.IsNullOrEmpty(guid))
            {
                var entry = aaSettings.FindAssetEntry(guid);
                if (entry != null)
                {
                    m_AssetName = entry.address;
                }
                else
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!string.IsNullOrEmpty(path))
                    {
                        var dir = Path.GetDirectoryName(path);
                        bool foundAddr = false;
                        while (!string.IsNullOrEmpty(dir))
                        {
                            var dirEntry = aaSettings.FindAssetEntry(AssetDatabase.AssetPathToGUID(dir));
                            if (dirEntry != null)
                            {
                                foundAddr = true;
                                m_AssetName = dirEntry.address + path.Remove(0, dir.Length);
                                break;
                            }
                            dir = Path.GetDirectoryName(dir);
                        }

                        if (!foundAddr)
                        {
                            m_AssetName = path;
                            if (!string.IsNullOrEmpty(checkToForceAddressable))
                            {
                                var newEntry = aaSettings.CreateOrMoveEntry(guid, aaSettings.DefaultGroup);
                                Addressables.LogFormat("Created AddressableAsset {0} in group {1}.", newEntry.address, aaSettings.DefaultGroup.Name);
                            }
                            else
                            {
                                if (!File.Exists(path))
                                {
                                    m_AssetName = "Missing File!";
                                }
                                else
                                    isNotAddressable = true;
                            }
                        }
                    }
                    else
                    {
                        m_AssetName = "Missing File!";
                    }
                }
            }

            assetDropDownRect = EditorGUI.PrefixLabel(position, label);
            var nameToUse = m_AssetName;
            if (isNotAddressable)
                nameToUse = "Not Addressable - " + nameToUse;
            if (m_AssetRefObject.editorAsset != null)
            {
                var subAssets = new List<Object>();
                subAssets.Add(null);
                var assetPath = AssetDatabase.GUIDToAssetPath(m_AssetRefObject.AssetGUID);
                subAssets.AddRange(AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath));
                var mainType = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
                if (mainType == typeof(SpriteAtlas))
                {
                    var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(assetPath);
                    var sprites = new Sprite[atlas.spriteCount];
                    atlas.GetSprites(sprites);
                    subAssets.AddRange(sprites);
                }

                if (subAssets.Count > 1)
                {
                    assetDropDownRect = DrawSubAssetsControl(property, subAssets);
                }
            }

            bool isDragging = Event.current.type == EventType.DragUpdated && position.Contains(Event.current.mousePosition);
            bool isDropping = Event.current.type == EventType.DragPerform && position.Contains(Event.current.mousePosition);

            DrawControl(property, isDragging, isDropping, nameToUse, isNotAddressable, guid);

            HandleDragAndDrop(property, isDragging, isDropping, guid);

            EditorGUI.EndProperty();
        }

        private void DrawControl(SerializedProperty property, bool isDragging, bool isDropping, string nameToUse, bool isNotAddressable, string guid)
        {
            float pickerWidth = 20f;
            Rect pickerRect = assetDropDownRect;
            pickerRect.width = pickerWidth;
            pickerRect.x = assetDropDownRect.xMax - pickerWidth;

            bool isPickerPressed = Event.current.type == EventType.MouseDown && Event.current.button == 0 && pickerRect.Contains(Event.current.mousePosition);
            bool isEnterKeyPressed = Event.current.type == EventType.KeyDown && Event.current.isKey && (Event.current.keyCode == KeyCode.KeypadEnter || Event.current.keyCode == KeyCode.Return);
            if (isPickerPressed || isDragging || isDropping || isEnterKeyPressed)
            {
                // To override ObjectField's default behavior
                Event.current.Use();
            }

            var asset = m_AssetRefObject?.editorAsset;
            if (asset != null)
            {
                var assetName = asset.name;
                asset.name = nameToUse;

                EditorGUI.ObjectField(assetDropDownRect, asset, asset.GetType(), false);

                asset.name = assetName;
            }
            else
            {
                EditorGUI.ObjectField(assetDropDownRect, null, typeof(AddressableAsset), false);
            }

#if UNITY_2019_1_OR_NEWER
            if (m_CaretTexture == null)
            {
                string caretIconPath = EditorGUIUtility.isProSkin
                    ? @"Packages\com.unity.addressables\Editor\Icons\PickerDropArrow-Pro.png"
                    : @"Packages\com.unity.addressables\Editor\Icons\PickerDropArrow-Personal.png";

                if (File.Exists(caretIconPath))
                {
                    m_CaretTexture = (Texture2D)AssetDatabase.LoadAssetAtPath(caretIconPath, typeof(Texture2D));
                }
            }

            if (m_CaretTexture != null)
            {
                UnityEngine.GUI.DrawTexture(pickerRect, m_CaretTexture, ScaleMode.ScaleToFit);
            }
#endif

            if (isPickerPressed || isEnterKeyPressed)
            {
                newGuidPropertyPath = property.propertyPath;
                var nonAddressedOption = isNotAddressable ? m_AssetName : string.Empty;
                PopupWindow.Show(assetDropDownRect, new AssetReferencePopup(this, guid, nonAddressedOption));
            }
        }

        private void HandleDragAndDrop(SerializedProperty property, bool isDragging, bool isDropping, string guid)
        {
            var aaSettings = AddressableAssetSettingsDefaultObject.Settings;
            //During the drag, doing a light check on asset validity.  The in-depth check happens during a drop, and should include a log if it fails.
            var rejectedDrag = false;
            if (isDragging)
            {
                if (aaSettings == null)
                    rejectedDrag = true;
                else
                {
                    var aaEntries = DragAndDrop.GetGenericData("AssetEntryTreeViewItem") as List<AssetEntryTreeViewItem>;
                    if (aaEntries != null)
                    {
                        if (aaEntries.Count != 1)
                            rejectedDrag = true;
                        else
                        {
                            if (aaEntries[0] == null || aaEntries[0].entry == null || aaEntries[0].entry.IsInResources || !ValidateAsset(aaEntries[0].entry.AssetPath))
                                rejectedDrag = true;
                        }
                    }
                    else
                    {
                        if (DragAndDrop.paths.Length != 1)
                        {
                            rejectedDrag = true;
                        }
                        else
                        {
                            if (!ValidateAsset(DragAndDrop.paths[0]))
                                rejectedDrag = true;
                        }
                    }
                }
                DragAndDrop.visualMode = rejectedDrag ? DragAndDropVisualMode.Rejected : DragAndDropVisualMode.Copy;
            }

            if (!rejectedDrag && isDropping)
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
                                Addressables.LogWarning("Cannot use an AssetReference on an asset in Resources. Move asset out of Resources first.");
                            else
                                SetObject(property, item.entry.TargetAsset, out guid);
                        }
                    }
                }
                else
                {
                    if (DragAndDrop.paths != null && DragAndDrop.paths.Length == 1)
                    {
                        var path = DragAndDrop.paths[0];
                        if (AddressableAssetUtility.IsInResources(path))
                            Addressables.LogWarning("Cannot use an AssetReference on an asset in Resources. Move asset out of Resources first. ");
                        else if (!AddressableAssetUtility.IsPathValidForEntry(path))
                            Addressables.LogWarning("Dragged asset is not valid as an Asset Reference. " + path);
                        else
                        {
                            Object obj;
                            if (DragAndDrop.objectReferences != null && DragAndDrop.objectReferences.Length == 1)
                                obj = DragAndDrop.objectReferences[0];
                            else
                                obj = AssetDatabase.LoadAssetAtPath<Object>(path);

                            if (SetObject(property, obj, out guid))
                            {
                                aaSettings = AddressableAssetSettingsDefaultObject.GetSettings(true);
                                var entry = aaSettings.FindAssetEntry(guid);
                                if (entry == null && !string.IsNullOrEmpty(guid))
                                {
                                    aaSettings.CreateOrMoveEntry(guid, aaSettings.DefaultGroup);
                                    newGuid = guid;
                                }
                            }
                        }
                    }
                }
            }
        }

        private Rect DrawSubAssetsControl(SerializedProperty property, List<Object> subAssets)
        {
            assetDropDownRect = new Rect(assetDropDownRect.position, new Vector2(assetDropDownRect.width / 2, assetDropDownRect.height));
            var objRect = new Rect(assetDropDownRect.xMax, assetDropDownRect.y, assetDropDownRect.width, assetDropDownRect.height);
            var objNames = new string[subAssets.Count];
            var selIndex = 0;
            for (int i = 0; i < subAssets.Count; i++)
            {
                var s = subAssets[i];
                var objName = s == null ? "<none>" : s.name;
                if (objName.EndsWith("(Clone)"))
                    objName = objName.Replace("(Clone)", "");
                objNames[i] = objName;
                if (m_AssetRefObject.SubObjectName == objName)
                    selIndex = i;
            }

            //TODO: handle large amounts of sprites with a custom popup
            var newIndex = EditorGUI.Popup(objRect, selIndex, objNames);
            if (newIndex != selIndex)
            {
                Undo.RecordObject(property.serializedObject.targetObject, "Assign Asset Reference Sub Object");
                var success = m_AssetRefObject.SetEditorSubObject(subAssets[newIndex]);
                if (success)
                {
                    EditorUtility.SetDirty(property.serializedObject.targetObject);
                    var comp = property.serializedObject.targetObject as Component;
                    if (comp != null && comp.gameObject != null && comp.gameObject.activeInHierarchy)
                        EditorSceneManager.MarkSceneDirty(comp.gameObject.scene);
                }
            }

            return assetDropDownRect;
        }

        void GatherFilters(SerializedProperty property)
        {
            if (m_Restrictions != null)
                return;

            m_Restrictions = new List<AssetReferenceUIRestrictionSurrogate>();
            var o = property.serializedObject.targetObject;
            if (o != null)
            {
                var t = o.GetType();

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
                        var uiRestriction = attr as AssetReferenceUIRestriction;
                        if (uiRestriction != null)
                        {
                            var surrogate = AssetReferenceUtility.GetSurrogate(uiRestriction.GetType());

                            if (surrogate != null)
                            {
                                var surrogateInstance = Activator.CreateInstance(surrogate) as AssetReferenceUIRestrictionSurrogate;
                                if (surrogateInstance != null)
                                {
                                    surrogateInstance.Init(uiRestriction);
                                    m_Restrictions.Add(surrogateInstance);
                                }
                            }
                            else
                            {
                                AssetReferenceUIRestrictionSurrogate restriction = new AssetReferenceUIRestrictionSurrogate();
                                restriction.Init(uiRestriction);
                                m_Restrictions.Add(restriction);
                            }
                        }
                    }
                }
            }
        }
    }

    class AddressableAsset { };

    class AssetReferencePopup : PopupWindowContent
    {
        AssetReferenceTreeView m_Tree;
        TreeViewState m_TreeState;
        bool m_ShouldClose;

        void ForceClose()
        {
            m_ShouldClose = true;
        }

        string m_CurrentName = string.Empty;
        AssetReferenceDrawer m_Drawer;
        string m_GUID;
        string m_NonAddressedAsset;

        SearchField m_SearchField;

        internal AssetReferencePopup(AssetReferenceDrawer drawer, string guid, string nonAddressedAsset)
        {
            m_Drawer = drawer;
            m_GUID = guid;
            m_NonAddressedAsset = nonAddressedAsset;
            m_SearchField = new SearchField();
            m_ShouldClose = false;
        }

        public override void OnOpen()
        {
            m_SearchField.SetFocus();
            base.OnOpen();
        }

        public override Vector2 GetWindowSize()
        {
            Vector2 result = base.GetWindowSize();
            result.x = m_Drawer.assetDropDownRect.width;
            return result;
        }

        public override void OnGUI(Rect rect)
        {
            int border = 4;
            int topPadding = 12;
            int searchHeight = 20;
            var searchRect = new Rect(border, topPadding, rect.width - border * 2, searchHeight);
            var remainTop = topPadding + searchHeight + border;
            var remainingRect = new Rect(border, topPadding + searchHeight + border, rect.width - border * 2, rect.height - remainTop - border);
            m_CurrentName = m_SearchField.OnGUI(searchRect, m_CurrentName);

            if (m_Tree == null)
            {
                if (m_TreeState == null)
                    m_TreeState = new TreeViewState();
                m_Tree = new AssetReferenceTreeView(m_TreeState, m_Drawer, this, m_GUID, m_NonAddressedAsset);
                m_Tree.Reload();
            }

            m_Tree.searchString = m_CurrentName;
            m_Tree.OnGUI(remainingRect);

            if (m_ShouldClose)
            {
                GUIUtility.hotControl = 0;
                editorWindow.Close();
            }
        }

        sealed class AssetRefTreeViewItem : TreeViewItem
        {
            public string guid;

            public AssetRefTreeViewItem(int id, int depth, string displayName, string g, string path)
                : base(id, depth, displayName)
            {
                guid = g;
                icon = AssetDatabase.GetCachedIcon(path) as Texture2D;
            }
        }

        class AssetReferenceTreeView : TreeView
        {
            AssetReferenceDrawer m_Drawer;
            AssetReferencePopup m_Popup;
            string m_GUID;
            string m_NonAddressedAsset;
            Texture2D m_WarningIcon;

            public AssetReferenceTreeView(TreeViewState state, AssetReferenceDrawer drawer, AssetReferencePopup popup, string guid, string nonAddressedAsset)
                : base(state)
            {
                m_Drawer = drawer;
                m_Popup = popup;
                showBorder = true;
                showAlternatingRowBackgrounds = true;
                m_GUID = guid;
                m_NonAddressedAsset = nonAddressedAsset;
                m_WarningIcon = EditorGUIUtility.FindTexture("console.warnicon");
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
                    if (assetRefItem != null && !string.IsNullOrEmpty(assetRefItem.guid))
                    {
                        m_Drawer.newGuid = assetRefItem.guid;
                    }
                    else
                    {
                        m_Drawer.newGuid = AssetReferenceDrawer.noAssetString;
                    }

                    m_Popup.ForceClose();
                }
            }

            protected override IList<TreeViewItem> BuildRows(TreeViewItem root)
            {
                if (string.IsNullOrEmpty(searchString))
                {
                    return base.BuildRows(root);
                }

                List<TreeViewItem> rows = new List<TreeViewItem>();

                foreach (var child in rootItem.children)
                {
                    if (child.displayName.IndexOf(searchString, StringComparison.OrdinalIgnoreCase) >= 0)
                        rows.Add(child);
                }

                return rows;
            }

            protected override TreeViewItem BuildRoot()
            {
                var root = new TreeViewItem(-1, -1);

                var aaSettings = AddressableAssetSettingsDefaultObject.Settings;
                if (aaSettings == null)
                {
                    var message = "Use 'Window->Addressables' to initialize.";
                    root.AddChild(new AssetRefTreeViewItem(message.GetHashCode(), 0, message, string.Empty, string.Empty));
                }
                else
                {
                    if (!string.IsNullOrEmpty(m_NonAddressedAsset))
                    {
                        var item = new AssetRefTreeViewItem(m_NonAddressedAsset.GetHashCode(), 0, "Make Addressable - " + m_NonAddressedAsset, m_GUID, string.Empty);
                        item.icon = m_WarningIcon;
                        root.AddChild(item);
                    }
                    root.AddChild(new AssetRefTreeViewItem(AssetReferenceDrawer.noAssetString.GetHashCode(), 0, AssetReferenceDrawer.noAssetString, string.Empty, string.Empty));
                    var allAssets = new List<AddressableAssetEntry>();
                    aaSettings.GetAllAssets(allAssets, false);
                    foreach (var entry in allAssets)
                    {
                        if (!AddressableAssetUtility.IsInResources(entry.AssetPath) &&
                            m_Drawer.ValidateAsset(entry.AssetPath))
                        {
                            var child = new AssetRefTreeViewItem(entry.address.GetHashCode(), 0, entry.address, entry.guid, entry.AssetPath);
                            root.AddChild(child);
                        }
                    }
                }

                return root;
            }
        }
    }

    public static class SerializedPropertyExtensions
    {
        public static T GetActualObjectForSerializedProperty<T>(this SerializedProperty property, FieldInfo field, ref string label)
        {
            try
            {
                if (property == null || field == null)
                    return default(T);
                var serializedObject = property.serializedObject;
                if (serializedObject == null)
                {
                    return default(T);
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
                            var arraySlice = currName.Split('[', ']');
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
                        var slice = property.propertyPath.Split('[', ']');
                        if (slice.Length >= 2)
                            label = "Element " + slice[slice.Length - 2];
                    }
                    else
                    {
                        label = slicedName.Last();
                    }

                    return DescendHierarchy<T>(targetObject, slicedName, arrayCounts, 0);
                }

                var obj = field.GetValue(targetObject);
                return (T)obj;
            }
            catch
            {
                return default(T);
            }
        }

        static T DescendHierarchy<T>(object targetObject, List<string> splitName, List<int> splitCounts, int depth)
        {
            if (depth >= splitName.Count)
                return default(T);

            var currName = splitName[depth];

            if (string.IsNullOrEmpty(currName))
                return DescendHierarchy<T>(targetObject, splitName, splitCounts, depth + 1);

            int arrayIndex = splitCounts[depth];

            var newField = targetObject.GetType().GetField(currName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (newField == null)
            {
                Type baseType = targetObject.GetType().BaseType;
                while (baseType != null && newField == null)
                {
                    newField = baseType.GetField(currName,
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    baseType = baseType.BaseType;
                }
            }

            var newObj = newField.GetValue(targetObject);
            if (depth == splitName.Count - 1)
            {
                T actualObject = default(T);
                if (arrayIndex >= 0)
                {
                    if (newObj.GetType().IsArray && ((System.Array)newObj).Length > arrayIndex)
                        actualObject = (T)((System.Array)newObj).GetValue(arrayIndex);

                    var newObjList = newObj as IList;
                    if (newObjList != null && newObjList.Count > arrayIndex)
                    {
                        actualObject = (T)newObjList[arrayIndex];

                        //if (actualObject == null)
                        //    actualObject = new T();
                    }
                }
                else
                {
                    actualObject = (T)newObj;
                }

                return actualObject;
            }
            else if (arrayIndex >= 0)
            {
                if (newObj is IList)
                {
                    IList list = (IList)newObj;
                    newObj = list[arrayIndex];
                }
                else if (newObj is System.Array)
                {
                    System.Array a = (System.Array)newObj;
                    newObj = a.GetValue(arrayIndex);
                }
            }

            return DescendHierarchy<T>(newObj, splitName, splitCounts, depth + 1);
        }
    }

    /// <summary>
    /// Used to restrict a class to only allow items with specific labels.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class AssetReferenceSurrogateAttribute : Attribute
    {
        public Type TargetType;
        /// <summary>
        /// Construct a new AssetReferenceSurrogateAttribute.
        /// </summary>
        /// <param name="type">The Type of the class in question.</param>
        public AssetReferenceSurrogateAttribute(Type type)
        {
            TargetType = type;
        }
    }

    /// <summary>
    /// Surrogate to AssetReferenceUIRestriction.
    /// This surrogate class provides the editor-side implementation of AssetReferenceUIRestriction attribute
    /// Used to restrict an AssetReference field or property to only allow items with specific labels. This is only enforced through the UI.
    /// </summary>
    [AssetReferenceSurrogate(typeof(AssetReferenceUIRestriction))]
    public class AssetReferenceUIRestrictionSurrogate : AssetReferenceUIRestriction
    {
        AssetReferenceUIRestriction data;
        /// <summary>
        /// Sets the AssetReferenceUIRestriction for this surrogate
        /// </summary>
        /// <param name="initData">To initialize AssetReferenceUIRestriction for surrogate</param>
        public virtual void Init(AssetReferenceUIRestriction initData)
        {
            data = initData;
        }
        /// <summary>
        /// Validates the referenced asset allowable for this asset reference.
        /// </summary>
        /// <param name="obj">The Object to validate.</param>
        /// <returns>Whether the referenced asset is valid.</returns>
        public override bool ValidateAsset(Object obj)
        {
            return data.ValidateAsset(obj);
        }
    }
    /// <summary>
    /// Surrogate to AssetReferenceUILabelRestriction 
    /// This surrogate class provides the editor-side implementation of AssetReferenceUILabelRestriction attribute
    /// Used to restrict an AssetReference field or property to only allow items wil specific labels. This is only enforced through the UI.
    /// </summary>
    [AssetReferenceSurrogate(typeof(AssetReferenceUILabelRestriction))]
    public class AssetReferenceUILabelRestrictionSurrogate : AssetReferenceUIRestrictionSurrogate
    {
        AssetReferenceUILabelRestriction data;
        /// <summary>
        /// Sets the AssetReferenceUILabelRestriction for this surrogate
        /// </summary>
        /// <param name="initData">To initialize AssetReferenceUILabelRestriction field</param>
        public override void Init(AssetReferenceUIRestriction initData)
        {
            data = initData as AssetReferenceUILabelRestriction;
        }

        /// <inheritdoc/>
        public override bool ValidateAsset(Object obj)
        {
            var path = AssetDatabase.GetAssetOrScenePath(obj);
            return ValidateAsset(path);
        }
        /// <inheritdoc/>
        public override bool ValidateAsset(string path)
        {
            if (AddressableAssetSettingsDefaultObject.Settings == null)
                return false;
            var guid = AssetDatabase.AssetPathToGUID(path);
            var entry = AddressableAssetSettingsDefaultObject.Settings.FindAssetEntry(guid);
            if (entry != null)
            {
                foreach (var label in data.m_AllowedLabels)
                {
                    if (entry.labels.Contains(label))
                        return true;
                }
            }
            return false;
        }

        ///<inheritdoc/>
        public override string ToString()
        {
            return data.ToString();
        }
    }

    /// <summary>
    /// Utility Class
    /// </summary>
    public class AssetReferenceUtility
    {
        /// <summary>
        /// Finds surrogate class for an Assembly with a particular TargetType
        /// </summary>
        /// <param name="assembly">Assembly for which the surrogate must be found</param>
        /// <param name="targetType">Target Type to search</param>
        /// <returns>Type of the surrogate found for the Assembly with a particular Target Type.</returns>
        public static Type GetSurrogate(Type targetType)
        {

            if (targetType == null)
            {
                Debug.LogError("targetType cannot be null");
                return null;
            }

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            List<Type> typesList = new List<Type>();
            foreach (Assembly assem in assemblies)
            {
                var assemblyTypeList = GatherTargetTypesFromAssembly(assem, targetType, out bool concreteTypeFound);
                if (concreteTypeFound == true)
                    return assemblyTypeList[0];
                typesList.AddRange(assemblyTypeList);
            }

            if(typesList.Count==0)
                return null;

            typesList.Sort(AssetReferenceUtility.CompareTypes);
            return typesList[0];
        }

        static int CompareTypes(object x, object y)
        {
            Type t1 = (Type)x;
            Type t2 = (Type)y;
            if (t1 == t2)
                return 0;
            else if (t1.IsAssignableFrom(t2))
                return 1;
            else
                return -1;
        }

        private static List<Type> GatherTargetTypesFromAssembly(Assembly assembly, Type targetType, out bool concreteTypeFound)
        {
            List<Type> assignableTypesList = new List<Type>();
            var typeList = assembly.GetTypes().Where(attrType => typeof(AssetReferenceUIRestrictionSurrogate).IsAssignableFrom(attrType)).ToList();
            foreach (var type in typeList)
            {
                var customAttribute = type.GetCustomAttribute<AssetReferenceSurrogateAttribute>();
                if (customAttribute == null)
                    continue;
                if (customAttribute.TargetType == targetType)
                {
                    assignableTypesList.Clear();
                    assignableTypesList.Add(type);
                    concreteTypeFound = true;
                    return assignableTypesList;
                }
                if (customAttribute.TargetType.IsAssignableFrom(targetType))
                {
                    assignableTypesList.Add(type);
                }
            }
            concreteTypeFound = false;
            return assignableTypesList;
        }
    }
}