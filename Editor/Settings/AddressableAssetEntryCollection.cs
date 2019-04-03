using UnityEngine;
using System.Collections.Generic;

namespace UnityEditor.AddressableAssets
{
    public class AddressablesEntryCollection : ScriptableObject
    {
        [SerializeField]
        private List<AddressableAssetEntry> m_serializeEntries = new List<AddressableAssetEntry>();
        public List<AddressableAssetEntry> Entries { get { return m_serializeEntries; } }
    }
}