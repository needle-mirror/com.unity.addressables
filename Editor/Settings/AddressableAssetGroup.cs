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
    public partial class AddressableAssetGroup : IComparer<AddressableAssetEntry>
    {
        [SerializeField]
        private string m_name;
        [SerializeField]
        private KeyDataStore m_data = new KeyDataStore();
        [SerializeField]
        private string m_guid;
        [SerializeField]
        private string m_processorAssembly;
        [SerializeField]
        private string m_processorClass;
        [SerializeField]
        private List<AddressableAssetEntry> m_serializeEntries = new List<AddressableAssetEntry>();
        [SerializeField]
        private bool m_readOnly;

        private Dictionary<string, AddressableAssetEntry> entryMap = new Dictionary<string, AddressableAssetEntry>();
        private AddressableAssetSettings m_settings;
        private AssetGroupProcessor m_processor;

        /// <summary>
        /// TODO - doc
        /// </summary>
        public string Name
        {
            get
            {
                if (string.IsNullOrEmpty(m_name))
                    m_name = Guid;

                return m_name;
            }
            set
            {
                m_name = value;
                PostModificationEvent(AddressableAssetSettings.ModificationEvent.GroupRenamed, this);
            }
        }
        public string Guid
        {
            get
            {
                if (string.IsNullOrEmpty(m_guid))
                    m_guid = GUID.Generate().ToString();
                return m_guid;
            }
        }

        internal void SetNameIfInvalid(string name)
        {
            if (string.IsNullOrEmpty(m_name))
                m_name = name;
        }

        public bool IsProcessorType(Type processorType)
        {
            return m_processorClass == processorType.FullName && m_processorAssembly == processorType.Assembly.FullName;
        }

        public bool ReadOnly
        {
            get { return m_readOnly; }
        }

        internal AddressableAssetSettings Settings
        {
            get
            {
                return m_settings;
            }
        }


        /// <summary>
        /// TODO - doc
        /// </summary>
        public KeyDataStore Data
        {
            get
            {
                return m_data;
            }
        }

        /// <summary>
        /// TODO - doc
        /// </summary>
        public Dictionary<string, AddressableAssetEntry>.ValueCollection entries
        {
            get
            {
                return entryMap.Values;
            }
        }

        /// <summary>
        /// TODO - doc
        /// </summary>
        [SerializeField]
        public AssetGroupProcessor Processor
        {
            get
            {
                if (m_processor == null)
                {
                    m_processor = CreateProcessor(false);
                    if (m_processor == null)
                        m_processor = CreateProcessor(true);
                    if (m_processor != null)
                        m_processor.CreateDefaultData(this);
                }
                return m_processor;
            }
        }

        private AssetGroupProcessor CreateProcessor(bool resetToDefault)
        {
            try
            {
                if (resetToDefault || string.IsNullOrEmpty(m_processorAssembly) || string.IsNullOrEmpty(m_processorClass))
                {
                    var type = typeof(BundledAssetGroupProcessor);
                    m_processorAssembly = type.Assembly.FullName;
                    m_processorClass = type.FullName;
                }
                var assembly = System.Reflection.Assembly.Load(m_processorAssembly);
                var objType = assembly.GetType(m_processorClass);
                return (AssetGroupProcessor)Activator.CreateInstance(objType);
            }
            catch (Exception)
            {
                return null;
            }
        }

        internal void SetProcessorType(Type processorType)
        {
            m_processorAssembly = processorType.Assembly.FullName;
            m_processorClass = processorType.FullName;
            m_processor = null;
            if (IsProcessorType(typeof(PlayerDataAssetGroupProcessor)))
                m_readOnly = true;
        }

        public int Compare(AddressableAssetEntry x, AddressableAssetEntry y)
        {
            return x.guid.CompareTo(y.guid);
        }

        internal void OnBeforeSerialize(AddressableAssetSettings settings)
        {
            m_serializeEntries.Clear();
            foreach (var e in entries)
                m_serializeEntries.Add(e);
            m_serializeEntries.Sort(this);
        }

        internal void SerializeForHash(BinaryFormatter formatter, Stream stream)
        {
            formatter.Serialize(stream, m_name);
            formatter.Serialize(stream, m_guid);
            formatter.Serialize(stream, entries.Count);
            foreach (var e in entries)
                e.SerializeForHash(formatter, stream);
            formatter.Serialize(stream, m_readOnly);
            formatter.Serialize(stream, m_processorAssembly);
            formatter.Serialize(stream, m_processorClass);
            //TODO: serialize group data
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
            if (IsProcessorType(typeof(PlayerDataAssetGroupProcessor)))
                m_readOnly = true;

            m_data.OnSetData += OnDataModified;
        }

        internal void Validate(AddressableAssetSettings addressableAssetSettings)
        {
            if (m_processorAssembly == null)
            {
                var editorList = GetAssetEntry(AddressableAssetEntry.EditorSceneListName);
                if (editorList != null)
                {
                    SetProcessorType(typeof(PlayerDataAssetGroupProcessor));
                    if (m_name == null)
                        m_name = AddressableAssetSettings.PlayerDataGroupName;
                }
                else
                {
                    SetProcessorType(typeof(BundledAssetGroupProcessor));
                    if (m_name == null)
                        m_name = m_settings.FindUniqueGroupName("Packed Content Group");
                }
            }
        }

        void OnDataModified(string key, object val, bool isNew)
        {
            PostModificationEvent(AddressableAssetSettings.ModificationEvent.GroupDataModified, this);
        }
        

        /// <summary>
        /// TODO - doc
        /// </summary>
        internal AddressableAssetGroup() { }
        internal AddressableAssetGroup(AddressableAssetSettings settings, string name, Type processorType, string guid, bool readOnly)
        {
            m_settings = settings;
            m_name = name;
            m_readOnly = readOnly;
            SetProcessorType(processorType);
            m_guid = guid;
        }

        /// <summary>
        /// TODO - doc
        /// </summary>
        public void GatherAllAssets(List<AddressableAssetEntry> results, bool includeSelf, bool recurseAll)
        {
            foreach (var e in entries)
                e.GatherAllAssets(results, includeSelf, recurseAll);
        }

        internal void ReplaceProcessor(Type proc)
        {
            SetProcessorType(proc);
        }

        /// <summary>
        /// TODO - doc
        /// </summary>
        internal void AddAssetEntry(AddressableAssetEntry e, bool postEvent = true)
        {
            e.isSubAsset = false;
            e.parentGroup = this;
            entryMap[e.guid] = e;
            if (postEvent)
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
            if (Settings != null)
                Settings.PostModificationEvent(e, o);
        }

        /// <summary>
        /// TODO - doc
        /// </summary>
        internal void RemoveAssetEntry(AddressableAssetEntry entry, bool postEvent = true)
        {
            entryMap.Remove(entry.guid);
            entry.parentGroup = null;
            if (postEvent)
                PostModificationEvent(AddressableAssetSettings.ModificationEvent.EntryRemoved, entry);
        }
    }

}
