using System;
using UnityEditorInternal;
using UnityEngine;

namespace UnityEditor.AddressableAssets
{
    [CustomEditor(typeof(AddressableAssetEntryCollection))]
    class AddressableAssetEntryCollectionEditor : Editor
    {
        AddressableAssetEntryCollection m_Collection;
        ReorderableList m_EntriesList;

        void OnEnable()
        {
            m_Collection = target as AddressableAssetEntryCollection;
            if (m_Collection != null)
            {
                m_EntriesList = new ReorderableList(m_Collection.Entries, typeof(AddressableAssetEntry), false, true, false, false);
                m_EntriesList.drawElementCallback = DrawEntry;
                m_EntriesList.drawHeaderCallback = DrawHeader;
            }
        }

        void DrawHeader(Rect rect)
        {
            GUI.Label(rect, "Asset Entries");
        }

        void DrawEntry(Rect rect, int index, bool isActive, bool isFocused)
        {
            GUI.Label(rect, m_Collection.Entries[index].address);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            m_EntriesList.DoLayoutList();
            serializedObject.ApplyModifiedProperties();
        }


    }

}
