using UnityEngine;
using System.Collections.Generic;

namespace UnityEditor.AddressableAssets
{
    /// <summary>
    /// Contains a list of AddressableAssetEntries that can be included in the settings.  The purpose of this class is to provide a way of combining entries from external sources such as packages into your project settings.
    /// </summary>
    public class AddressableAssetEntryCollection : ScriptableObject
    {
        [SerializeField]
        private List<AddressableAssetEntry> m_serializeEntries = new List<AddressableAssetEntry>();
        /// <summary>
        /// The collection of entries.
        /// </summary>
        public List<AddressableAssetEntry> Entries { get { return m_serializeEntries; } }
    }
}