using UnityEngine;
using UnityEditorInternal;

namespace UnityEditor.AddressableAssets
{
    [CustomEditor(typeof(AddressableAssetEntryCollection))]
    internal class AddressableAssetEntryCollectionEditor : Editor
    {
        AddressableAssetEntryCollection m_collection;
        ReorderableList m_entriesList;

        private void OnEnable()
        {
            m_collection = target as AddressableAssetEntryCollection;
            m_entriesList = new ReorderableList(m_collection.Entries, typeof(AddressableAssetEntry), false, true, false, false);
            m_entriesList.drawElementCallback = DrawEntry;
            m_entriesList.drawHeaderCallback = DrawHeader;
        }

        private void DrawHeader(Rect rect)
        {
            GUI.Label(rect, "Asset Entries");
        }

        private void DrawEntry(Rect rect, int index, bool isActive, bool isFocused)
        {
            GUI.Label(rect, m_collection.Entries[index].address);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            m_entriesList.DoLayoutList();
            serializedObject.ApplyModifiedProperties();
        }


    }

}
