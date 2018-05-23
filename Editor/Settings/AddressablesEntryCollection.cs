using UnityEngine;
using System.Collections.Generic;

namespace UnityEditor.AddressableAssets
{
    public class AddressablesEntryCollection : ScriptableObject
    {
        [SerializeField]
        private List<AddressableAssetSettings.AssetGroup.AssetEntry> m_serializeEntries = new List<AddressableAssetSettings.AssetGroup.AssetEntry>();
        public List<AddressableAssetSettings.AssetGroup.AssetEntry> Entries { get { return m_serializeEntries; } }
    }
}