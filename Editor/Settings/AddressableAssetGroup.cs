using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace UnityEditor.AddressableAssets
{
    /// <summary>
    /// Contains the collection of asset entries associated with this group.
    /// </summary>
    [Serializable]
    public class AddressableAssetGroup : ScriptableObject, IComparer<AddressableAssetEntry>, ISerializationCallbackReceiver
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
        [SerializeField]
        private AddressableAssetSettings m_settings;

        private Dictionary<string, AddressableAssetEntry> entryMap = new Dictionary<string, AddressableAssetEntry>();
        private AssetGroupProcessor m_processor;

        /// <summary>
        /// The group name.
        /// </summary>
        public virtual string Name
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
                if (m_name != name)
                {
                    string guid;
                    long localId;
                    if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(this, out guid, out localId))
                    {
                        var path = AssetDatabase.GUIDToAssetPath(guid);
                        if (!string.IsNullOrEmpty(path))
                        {
                            var newPath = path.Replace(name, m_name);
                            if (path != newPath)
                                AssetDatabase.MoveAsset(path, newPath);
                        }
                    }
                    name = m_name;
                }
                PostModificationEvent(AddressableAssetSettings.ModificationEvent.GroupRenamed, this);
            }
        }
        /// <summary>
        /// The group GUID.
        /// </summary>
        public virtual string Guid
        {
            get
            {
                if (string.IsNullOrEmpty(m_guid))
                    m_guid = GUID.Generate().ToString();
                return m_guid;
            }
        }

        /// <summary>
        /// Is the group static.  This property is used in determining which assets need to be moved to a new remote group during the content update process.
        /// </summary>
        public virtual bool StaticContent
        {
            get
            {
                return Data.GetData("StaticContent", false, true);
            }

            set
            {
                Data.SetData("StaticContent", value);
            }
        }

        internal void SetNameIfInvalid(string name)
        {
            if (string.IsNullOrEmpty(m_name))
                m_name = name;
        }

        /// <summary>
        /// Check the processor type of this group.
        /// </summary>
        /// <param name="processorType">The processor type.</param>
        /// <returns>True if this group has the same processor type.</returns>
        //[Obsolete("This API is going to be replaced soon with a more flexible build system.")]
        public virtual bool IsProcessorType(Type processorType)
        {
            return m_processorClass == processorType.FullName && m_processorAssembly == processorType.Assembly.FullName;
        }

        /// <summary>
        /// Is this group read only.  This is normally false.  Built in resources (resource folders and the scene list) are put into a special read only group.
        /// </summary>
        public virtual bool ReadOnly
        {
            get { return m_readOnly; }
        }

        internal AddressableAssetSettings Settings
        {
            get
            {
                if (m_settings == null)
                    m_settings = AddressableAssetSettings.GetDefault(false, false);

                return m_settings;
            }
        }


        /// <summary>
        /// The data store associated with this group.
        /// </summary>
        public virtual KeyDataStore Data
        {
            get
            {
                return m_data;
            }
        }

        /// <summary>
        /// The collection of asset entries.
        /// </summary>
        public virtual ICollection<AddressableAssetEntry> entries
        {
            get
            {
                return entryMap.Values;
            }
        }
        /// <summary>
        /// Gets the associated group processor. NOTE: This API is going to be replaced soon with a more flexible and user controlled system.
        /// </summary>
        //[Obsolete("This API is going to be replaced soon with a more flexible build system.")]
        public virtual AssetGroupProcessor Processor
        {
            get
            {
                EnsureValidProcessor();
                return m_processor;
            }
        }
        private void EnsureValidProcessor()
        {
            if (m_processor == null)
            {
                m_processor = CreateProcessor(false);
                if (m_processor == null)
                    m_processor = CreateProcessor(true);
                if (m_processor != null)
                    m_processor.CreateDefaultData(this);
            }

        }

        /// <summary>
        /// Is the default group.
        /// </summary>
        public virtual bool Default
        {
            get { return Guid == Settings.DefaultGroup.Guid; }
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

        /// <inheritdoc/>
        public virtual int Compare(AddressableAssetEntry x, AddressableAssetEntry y)
        {
            return x.guid.CompareTo(y.guid);
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

        /// <summary>
        /// Converts data to serializable format.
        /// </summary>
        public void OnBeforeSerialize()
        {
            m_serializeEntries.Clear();
            foreach (var e in entries)
                m_serializeEntries.Add(e);
            m_serializeEntries.Sort(this);

        }

        /// <summary>
        /// Converts data from serializable format.
        /// </summary>
        public void OnAfterDeserialize()
        {
            entryMap.Clear();
            foreach (var e in m_serializeEntries)
            {
                try
                {
                    e.parentGroup = this;
                    e.IsSubAsset = false;
                    entryMap.Add(e.guid, e);
                }
                catch (Exception ex)
                {
                    Addressables.Log(e.address);
                    Debug.LogException(ex);
                }
            }
            m_serializeEntries.Clear();
            if (IsProcessorType(typeof(PlayerDataAssetGroupProcessor)))
                m_readOnly = true;
        }

        void OnEnable()
        {
            m_data.OnSetData += OnDataModified;
        }

        void OnDisable()
        {
            m_data.OnSetData -= OnDataModified;
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
                        m_name = Settings.FindUniqueGroupName("Packed Content Group");
                }
            }
        }

        void OnDataModified(string key, object val, bool isNew)
        {
            PostModificationEvent(AddressableAssetSettings.ModificationEvent.GroupDataModified, this);
        }

        //TODO: deprecate and remove once most users have transitioned to newer external data files
        internal void Initialize(AddressableAssetSettings settings, string groupName, AddressableAssetGroupDeprecated old)
        {
            m_settings = settings;
            m_name = groupName;
            m_readOnly = old.m_readOnly;
            m_processorAssembly = old.m_processorAssembly;
            m_processorClass = old.m_processorClass;
            m_guid = old.m_guid;
            m_serializeEntries = old.m_serializeEntries;
            m_data = old.m_data;
            OnAfterDeserialize();
        }

        internal void Initialize(AddressableAssetSettings settings, string groupName, Type processorType, string guid, bool readOnly)
        {
            m_settings = settings;
            m_name = groupName;
            m_readOnly = readOnly;
            SetProcessorType(processorType);
            EnsureValidProcessor();
            m_guid = guid;
        }

        /// <summary>
        /// Gathers all asset entries.  Each explicit entry may contain multiple sub entries. For example, addressable folders create entries for each asset contained within.
        /// </summary>
        /// <param name="results">The generated list of entries.  For simple entries, this will contain just the entry itself if specified.</param>
        /// <param name="includeSelf">Determines if the entry should be contained in the result list or just sub entries.</param>
        /// <param name="recurseAll">Determines if full recursion should be done when gathering entries.</param>
        public virtual void GatherAllAssets(List<AddressableAssetEntry> results, bool includeSelf, bool recurseAll)
        {
            foreach (var e in entries)
                e.GatherAllAssets(results, includeSelf, recurseAll);
        }

        internal void ReplaceProcessor(Type proc)
        {
            SetProcessorType(proc);
        }

        internal void AddAssetEntry(AddressableAssetEntry e, bool postEvent = true)
        {
            e.IsSubAsset = false;
            e.parentGroup = this;
            entryMap[e.guid] = e;
            if (postEvent)
                PostModificationEvent(AddressableAssetSettings.ModificationEvent.EntryAdded, e);
        }

        /// <summary>
        /// Get an entry via the asset guid.
        /// </summary>
        /// <param name="guid">The asset guid.</param>
        /// <returns></returns>
        public virtual AddressableAssetEntry GetAssetEntry(string guid)
        {
            if (entryMap.ContainsKey(guid))
                return entryMap[guid];
            return null;
        }

        internal void PostModificationEvent(AddressableAssetSettings.ModificationEvent e, object o)
        {
            EditorUtility.SetDirty(this);
            if (Settings != null)
                Settings.PostModificationEvent(e, o);
        }

        internal void RemoveAssetEntry(AddressableAssetEntry entry, bool postEvent = true)
        {
            entryMap.Remove(entry.guid);
            entry.parentGroup = null;
            if (postEvent)
                PostModificationEvent(AddressableAssetSettings.ModificationEvent.EntryRemoved, entry);
        }
    }

}
