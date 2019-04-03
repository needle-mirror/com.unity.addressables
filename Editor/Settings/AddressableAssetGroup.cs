using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;

namespace UnityEditor.AddressableAssets
{

        /// <summary>
        /// TODO - doc
        /// </summary>
        [Serializable]
        public partial class AddressableAssetGroup
        {
            /// <summary>
            /// TODO - doc
            /// </summary>
            [SerializeField]
            internal string name;

            /// <summary>
            /// TODO - doc
            /// </summary>
            [SerializeField]
            internal string guid;

            /// <summary>
            /// TODO - doc
            /// </summary>
            public string displayName
            {
                get { return name; }
                set
                {
                    name = value;
                    PostModificationEvent(AddressableAssetSettings.ModificationEvent.GroupRenamed, this);
                }
            }

            private Dictionary<string, AddressableAssetEntry> entryMap = new Dictionary<string, AddressableAssetEntry>();
            [SerializeField]
            private List<AddressableAssetEntry> m_serializeEntries = new List<AddressableAssetEntry>();
            [NonSerialized]
            AddressableAssetSettings m_settings;
            internal AddressableAssetSettings settings { get { return m_settings; } }
            /// <summary>
            /// TODO - doc
            /// </summary>
            public Dictionary<string, AddressableAssetEntry>.ValueCollection entries
            {
                get { return entryMap.Values;  }
            }
            /// <summary>
            /// TODO - doc
            /// </summary>
            [SerializeField]
            internal AssetGroupProcessor processor;
            /// <summary>
            /// TODO - doc
            /// </summary>
            public AssetGroupProcessor Procesor { get { return processor; } }
            /// <summary>
            /// TODO - doc
            /// </summary>
            [SerializeField]
            internal bool isDefault = false;
            /// <summary>
            /// TODO - doc
            /// </summary>
            [SerializeField]
            internal bool readOnly;

            internal bool HasSettings() { return processor == null ? false : processor.HasSettings(); }

            internal void OnBeforeSerialize(AddressableAssetSettings settings)
            {
                m_serializeEntries.Clear();
                foreach (var e in entries)
                    m_serializeEntries.Add(e);
            }

            internal void SerializeForHash(BinaryFormatter formatter, Stream stream)
            {
                formatter.Serialize(stream, name);
                formatter.Serialize(stream, guid);
                formatter.Serialize(stream, entries.Count);
                foreach (var e in entries)
                    e.SerializeForHash(formatter, stream);
                formatter.Serialize(stream, isDefault);
                formatter.Serialize(stream, readOnly);
                formatter.Serialize(stream, processor.GetType().FullName);
                processor.SerializeForHash(formatter, stream);
            }

            internal void OnAfterDeserialize(AddressableAssetSettings settings)
            {
                m_settings = settings;
                entryMap.Clear();
                foreach (var e in m_serializeEntries)
                {
                    try
                    {
                        e.parentGroup = this;
                        e.isSubAsset = false;
                        entryMap.Add(e.guid, e);
                    }
                    catch (Exception ex)
                    {
                        Debug.Log(e.address);
                        Debug.LogException(ex);
                    }
                }
                m_serializeEntries.Clear();
            }

            /// <summary>
            /// TODO - doc
            /// </summary>
            internal AddressableAssetGroup() {}
            internal AddressableAssetGroup(string n, AssetGroupProcessor p, bool setAsDefault, string g)
            {
                name = n;
                processor = p;
               
                isDefault = setAsDefault;
                guid = g;
                readOnly = false;
            }

            /// <summary>
            /// TODO - doc
            /// </summary>
            public void GatherAllAssets(List<AddressableAssetEntry> results, bool includeSelf, bool recurseAll)
            {
                foreach (var e in entries)
                    e.GatherAllAssets(results, includeSelf, recurseAll);
            }

            internal void ReplaceProcessor(AssetGroupProcessor proc, string newGUID)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                AssetDatabase.DeleteAsset(path);
                processor = proc;
                guid = newGUID;
            }

            /// <summary>
            /// TODO - doc
            /// </summary>
            internal void AddAssetEntry(AddressableAssetEntry e, bool postEvent = true)
            {
                e.isSubAsset = false;
                e.parentGroup = this;
                entryMap[e.guid] = e;
                if(postEvent)
                    PostModificationEvent(AddressableAssetSettings.ModificationEvent.EntryAdded, e);
            }

            /// <summary>
            /// TODO - doc
            /// </summary>
            public AddressableAssetEntry GetAssetEntry(string guid)
            {
                if (entryMap.ContainsKey(guid))
                    return entryMap[guid];
                return null;
            }

            internal void PostModificationEvent(AddressableAssetSettings.ModificationEvent e, object o)
            {
                if (settings != null)
                    settings.PostModificationEvent(e, o);
            }

            /// <summary>
            /// TODO - doc
            /// </summary>
            internal void RemoveAssetEntry(AddressableAssetEntry entry, bool postEvent = true)
            {
                entryMap.Remove(entry.guid);
                entry.parentGroup = null;
                if(postEvent)
                    PostModificationEvent(AddressableAssetSettings.ModificationEvent.EntryRemoved, entry);
            }
        }
   
}
