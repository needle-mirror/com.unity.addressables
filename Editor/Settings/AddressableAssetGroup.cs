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
        private KeyDataStore m_data;
        [SerializeField]
        private string m_guid;
        [SerializeField]
        private List<AddressableAssetEntry> m_serializeEntries = new List<AddressableAssetEntry>();
        [SerializeField]
        private bool m_readOnly;
        [SerializeField]
        private AddressableAssetSettings m_settings;
        [SerializeField]
        private AddressableAssetGroupSchemaSet m_schemaSet = new AddressableAssetGroupSchemaSet();

        private Dictionary<string, AddressableAssetEntry> entryMap = new Dictionary<string, AddressableAssetEntry>();

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
                m_name = m_name.Replace('/', '-');
                m_name = m_name.Replace('\\', '-');
                if(m_name != value)
                    Debug.Log("Group names cannot include '\\' or '/'.  Replacing with '-'. " + m_name);
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
                            {
                                var setPath = AssetDatabase.MoveAsset(path, newPath);
                                if (!string.IsNullOrEmpty(setPath))
                                {
                                    //unable to rename group due to invalid file name
                                    Debug.LogError("Rename of Group failed. " + setPath);
                                }
                                m_name = name;
                                
                            }
                        }
                    }
                    else
                    {
                        //this isn't a valid asset, which means it wasn't persisted, so just set the object name to the desired display name.
                        name = m_name;
                    }
                    SetDirty(AddressableAssetSettings.ModificationEvent.GroupRenamed, this, true);
                }
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
        /// List of schemas for this group.
        /// </summary>
        public List<AddressableAssetGroupSchema> Schemas { get { return m_schemaSet.Schemas; } }

        string GetSchemaAssetPath(Type type)
        {
            return Settings.IsPersisted ? (Settings.GroupSchemaFolder + "/" + m_guid + "_" + type.Name + ".asset") : string.Empty;
        }

        /// <summary>
        /// Adds a copy of the provided schema object.
        /// </summary>
        /// <param name="schema">The schema to add. A copy will be made and saved in a folder relative to the main Addressables settings asset. </param>
        /// <returns>The created schema object.</returns>
        public AddressableAssetGroupSchema AddSchema(AddressableAssetGroupSchema schema, bool postEvent = true)
        {
            var added = m_schemaSet.AddSchema(schema, GetSchemaAssetPath);
            if (added != null)
            {
                added.Group = this;
                SetDirty(AddressableAssetSettings.ModificationEvent.GroupSchemaAdded, this, postEvent);
            }
            return added;
        }

        /// <summary>
        /// Creates and adds a schema of a given type to this group.  The schema asset will be created in the GroupSchemas directory relative to the settings asset.
        /// </summary>
        /// <param name="type">The schema type. This type must not already be added.</param>
        /// <returns>The created schema object.</returns>
        public AddressableAssetGroupSchema AddSchema(Type type, bool postEvent = true)
        {
            var added = m_schemaSet.AddSchema(type, GetSchemaAssetPath);
            if (added != null)
            {
                added.Group = this;
                SetDirty(AddressableAssetSettings.ModificationEvent.GroupSchemaAdded, this, postEvent);
            }
            return added;
        }

        /// <summary>
        /// Creates and adds a schema of a given type to this group.
        /// </summary>
        /// <typeparam name="TSchema">The schema type. This type must not already be added.</typeparam>
        /// <returns>The created schema object.</returns>
        public TSchema AddSchema<TSchema>(bool postEvent = true) where TSchema : AddressableAssetGroupSchema
        {
            return AddSchema(typeof(TSchema), postEvent) as TSchema;
        }

        /// <summary>
        ///  Remove a given schema from this group.
        /// </summary>
        /// <param name="type">The schema type.</param>
        /// <returns>True if the schema was found and removed, false otherwise.</returns>
        public bool RemoveSchema(Type type, bool postEvent = true)
        {
            if (!m_schemaSet.RemoveSchema(type))
                return false;

            SetDirty(AddressableAssetSettings.ModificationEvent.GroupSchemaRemoved, this, postEvent);
            return true;
        }

        /// <summary>
        ///  Remove a given schema from this group.
        /// </summary>
        /// <typeparam name="TSchema">The schema type.</typeparam>
        /// <returns>True if the schema was found and removed, false otherwise.</returns>
        public bool RemoveSchema<TSchema>(bool postEvent = true)
        {
            return RemoveSchema(typeof(TSchema), postEvent);
        }
        
        /// <summary>
        /// Gets an added schema of the specified type.
        /// </summary>
        /// <typeparam name="TSchema">The schema type.</typeparam>
        /// <returns>The schema if found, otherwise null.</returns>
        public TSchema GetSchema<TSchema>() where TSchema : AddressableAssetGroupSchema
        {
            return GetSchema(typeof(TSchema)) as TSchema;
        }

        /// <summary>
        /// Gets an added schema of the specified type.
        /// </summary>
        /// <param name="type">The schema type.</typeparam>
        /// <returns>The schema if found, otherwise null.</returns>
        public AddressableAssetGroupSchema GetSchema(Type type)
        {
            return m_schemaSet.GetSchema(type);
        }

        /// <summary>
        /// Checks if the group contains a schema of a given type.
        /// </summary>
        /// <typeparam name="TSchema">The schema type.</typeparam>
        /// <returns>True if the schema type or subclass has been added to this group.</returns>
        public bool HasSchema<TSchema>()
        {
            return HasSchema(typeof(TSchema));
        }

        /// <summary>
        /// Removes all schemas and optionally deletes the assets associated with them.
        /// </summary>
        /// <param name="deleteAssets">If true, the schema assets will also be deleted.</param>
        public void ClearSchemas(bool deleteAssets, bool postEvent = true)
        {
            m_schemaSet.ClearSchemas(deleteAssets);
            SetDirty(AddressableAssetSettings.ModificationEvent.GroupRemoved, this, postEvent);
        }

        /// <summary>
        /// Checks if the group contains a schema of a given type.
        /// </summary>
        /// <param name="type">The schema type.</typeparam>
        /// <returns>True if the schema type or subclass has been added to this group.</returns>
        public bool HasSchema(Type type)
        {
            return GetSchema(type) != null;
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
                    m_settings = AddressableAssetSettingsDefaultObject.Settings;

                return m_settings;
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
        /// Is the default group.
        /// </summary>
        public virtual bool Default
        {
            get { return Guid == Settings.DefaultGroup.Guid; }
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
        }

        void OnEnable()
        {
            Validate();
        } 

        internal void Validate()
        {
            
            bool allValid = false;
            while (!allValid)
            {
                allValid = true;
                for (int i = 0; i < m_schemaSet.Schemas.Count; i++)
                {
                    if (m_schemaSet.Schemas[i] == null)
                    {
                        m_schemaSet.Schemas.RemoveAt(i);
                        allValid = false;
                        break;
                    }
                }
            }

            var editorList = GetAssetEntry(AddressableAssetEntry.EditorSceneListName); 
            if (editorList != null)
            {
                if (m_name == null)
                    m_name = AddressableAssetSettings.PlayerDataGroupName;
                if (m_data != null)
                {
                    if(!HasSchema<PlayerDataGroupSchema>())
                        AddSchema<PlayerDataGroupSchema>();
                    m_data = null;
                }
            }
            else if(Settings != null)
            {
                if (m_name == null)
                    m_name = Settings.FindUniqueGroupName("Packed Content Group");
                if (m_data != null)
                {
                    if (!HasSchema<BundledAssetGroupSchema>())
                    {
                        var schema = AddSchema<BundledAssetGroupSchema>();
                        schema.BuildPath.SetVariableById(Settings, m_data.GetData("BuildPath", Settings.profileSettings.GetProfileDataByName(AddressableAssetSettings.kLocalBuildPath).Id));
                        schema.LoadPath.SetVariableById(Settings, m_data.GetData("LoadPath", Settings.profileSettings.GetProfileDataByName(AddressableAssetSettings.kLocalLoadPath).Id));
                        schema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
                        AddSchema<ContentUpdateGroupSchema>().StaticContent = m_data.GetData("StaticContent", "false") == "true";
                    }
                    m_data = null;
                }
            }
        }

        //TODO: deprecate and remove once most users have transitioned to newer external data files
        internal void Initialize(AddressableAssetSettings settings, string groupName, AddressableAssetGroupDeprecated old, bool staticContent)
        {
            m_settings = settings;
            m_name = groupName;
            m_readOnly = old.m_readOnly;
            m_guid = old.m_guid;
            m_serializeEntries = old.m_serializeEntries;

            if (old.m_processorClass.Contains("PlayerDataAssetGroupProcessor"))
            {
                AddSchema<PlayerDataGroupSchema>();
            }
            else
            {
                var schema = AddSchema<BundledAssetGroupSchema>();
                schema.BuildPath.SetVariableByName(settings, old.m_data.GetDataString("BuildPath", ""));
                schema.LoadPath.SetVariableByName(settings, old.m_data.GetDataString("LoadPath", ""));
                schema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
                AddSchema<ContentUpdateGroupSchema>().StaticContent = false;
            }
            OnAfterDeserialize();
        }

        internal void Initialize(AddressableAssetSettings settings, string groupName, string guid, bool readOnly)
        {
            m_settings = settings;
            m_name = groupName;
            m_readOnly = readOnly;
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

        internal void AddAssetEntry(AddressableAssetEntry e, bool postEvent = true)
        {
            e.IsSubAsset = false;
            e.parentGroup = this;
            entryMap[e.guid] = e;
            SetDirty(AddressableAssetSettings.ModificationEvent.EntryAdded, e, postEvent);
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

        /// <summary>
        /// Marks the object as modified.
        /// </summary>
        /// <param name="modificationEvent">The event type that is changed.</param>
        /// <param name="eventData">The object data that corresponds to the event.</param>
        /// <param name="postEvent">If true, the event is propagated to callbacks.</param>
        public void SetDirty(AddressableAssetSettings.ModificationEvent modificationEvent, object eventData, bool postEvent)
        {
            if (Settings != null)
            {
                if (Settings.IsPersisted)
                    EditorUtility.SetDirty(this);
                Settings.SetDirty(modificationEvent, eventData, postEvent);
            }
        }

        /// <summary>
        /// Remove an entry.
        /// </summary>
        /// <param name="entry">The entry to remove.</param>
        /// <param name="postEvent">If true, post the event to callbacks.</param>
        public void RemoveAssetEntry(AddressableAssetEntry entry, bool postEvent = true)
        {
            entryMap.Remove(entry.guid);
            entry.parentGroup = null;
            SetDirty(AddressableAssetSettings.ModificationEvent.EntryRemoved, entry, postEvent);
        }
    }

}
