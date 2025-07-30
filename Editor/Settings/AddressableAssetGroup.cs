using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

namespace UnityEditor.AddressableAssets.Settings
{
    /// <summary>
    /// Contains the collection of asset entries associated with this group.
    /// </summary>
    [Serializable]
    public class AddressableAssetGroup : ScriptableObject, IComparer<AddressableAssetEntry>, ISerializationCallbackReceiver
    {
        internal static GUIContent RemoveSchemaContent = new GUIContent("Remove Schema", "Remove this schema.");
        internal static GUIContent MoveSchemaUpContent = new GUIContent("Move Up", "Move schema up one in list.");
        internal static GUIContent MoveSchemaDownContent = new GUIContent("Move Down", "Move schema down one in list.");
        internal static GUIContent ExpandSchemaContent = new GUIContent("Expand All", "Expand all settings within schema.");


        [FormerlySerializedAs("m_name")]
        [SerializeField]
        string m_GroupName;

        [FormerlySerializedAs("m_guid")]
        [SerializeField]
        string m_GUID;

        [FormerlySerializedAs("m_serializeEntries")]
        [SerializeField]
        internal List<AddressableAssetEntry> m_SerializeEntries = new List<AddressableAssetEntry>();

        [FormerlySerializedAs("m_readOnly")]
        [SerializeField]
        internal bool m_ReadOnly;

        [FormerlySerializedAs("m_settings")]
        [SerializeField]
        AddressableAssetSettings m_Settings;

        [FormerlySerializedAs("m_schemaSet")]
        [SerializeField]
        AddressableAssetGroupSchemaSet m_SchemaSet = new AddressableAssetGroupSchemaSet();

        Dictionary<string, AddressableAssetEntry> m_EntryMap = new Dictionary<string, AddressableAssetEntry>();
        List<AddressableAssetEntry> m_FolderEntryCache = null;

        /// <summary>
        /// If true, this Group is likely marked 'Cannot Change Post Release', but has a modified asset since the previous build.
        /// </summary>
        public bool FlaggedDuringContentUpdateRestriction { get; internal set; }

        internal void RefreshEntriesCache()
        {
            m_FolderEntryCache = new List<AddressableAssetEntry>();
            FlaggedDuringContentUpdateRestriction = false;
            foreach (AddressableAssetEntry e in entries)
            {
                if (!string.IsNullOrEmpty(e.AssetPath) && e.MainAssetType == typeof(DefaultAsset) && AssetDatabase.IsValidFolder(e.AssetPath))
                    m_FolderEntryCache.Add(e);
                if (e.FlaggedDuringContentUpdateRestriction)
                    FlaggedDuringContentUpdateRestriction = true;
            }
        }

        /// <summary>
        /// The group name.
        /// </summary>
        public virtual string Name
        {
            get
            {
                if (string.IsNullOrEmpty(m_GroupName))
                    m_GroupName = Guid;

                return m_GroupName;
            }
            set
            {
                string newName = value;
                newName = newName.Replace('/', '-');
                newName = newName.Replace('\\', '-');
                if (newName != value)
                    Debug.Log("Group names cannot include '\\' or '/'.  Replacing with '-'. " + m_GroupName);
                if (m_GroupName != newName)
                {
                    string previousName = m_GroupName;

                    string guid;
                    long localId;
                    if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(this, out guid, out localId))
                    {
                        var path = AssetDatabase.GUIDToAssetPath(guid);
                        if (!string.IsNullOrEmpty(path))
                        {
                            var folder = Path.GetDirectoryName(path);
                            var extension = Path.GetExtension(path);
                            var newPath = $"{folder}/{newName}{extension}".Replace('\\', '/');
                            if (path != newPath)
                            {
                                var setPath = AssetDatabase.MoveAsset(path, newPath);
                                bool success = false;
                                if (string.IsNullOrEmpty(setPath))
                                {
                                    name = m_GroupName = newName;
                                    success = RenameSchemaAssets();
                                }

                                if (success == false)
                                {
                                    //unable to rename group due to invalid file name
                                    Debug.LogError("Rename of Group failed. " + setPath);
                                    name = m_GroupName = previousName;
                                }
                            }
                        }
                    }
                    else
                    {
                        //this isn't a valid asset, which means it wasn't persisted, so just set the object name to the desired display name.
                        name = m_GroupName = newName;
                    }

                    SetDirty(AddressableAssetSettings.ModificationEvent.GroupRenamed, this, true, true);
                }
                else if (name != newName)
                {
                    name = m_GroupName;
                    SetDirty(AddressableAssetSettings.ModificationEvent.GroupRenamed, this, true, true);
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
                if (string.IsNullOrEmpty(m_GUID))
                    m_GUID = GUID.Generate().ToString();
                return m_GUID;
            }
        }

        /// <summary>
        /// List of schemas for this group.
        /// </summary>
        public List<AddressableAssetGroupSchema> Schemas
        {
            get { return m_SchemaSet.Schemas; }
        }

        /// <summary>
        /// Get the types of added schema for this group.
        /// </summary>
        public List<Type> SchemaTypes
        {
            get { return m_SchemaSet.Types; }
        }

        string GetSchemaAssetPath(Type type)
        {
            return Settings.IsPersisted ? (Settings.GroupSchemaFolder + "/" + Name + "_" + type.Name + ".asset") : string.Empty;
        }

        /// <summary>
        /// Determines if this group allows nested folders.
        /// </summary>
        public bool AllowNestedFolders
        {
            get
            {
                if (m_Settings)
                    return m_Settings.AllowNestedBundleFolders;
                else
                {
                    Debug.LogError("AddressableAssetSettings is null. Cannot determine if nested folders are allowed.");
                    return false;
                }
            }
        }

        /// <summary>
        /// Adds a copy of the provided schema object.
        /// </summary>
        /// <param name="schema">The schema to add. A copy will be made and saved in a folder relative to the main Addressables settings asset. </param>
        /// <param name="postEvent">Determines if this method call will post an event to the internal addressables event system</param>
        /// <returns>The created schema object.</returns>
        public AddressableAssetGroupSchema AddSchema(AddressableAssetGroupSchema schema, bool postEvent = true)
        {
            var added = m_SchemaSet.AddSchema(schema, GetSchemaAssetPath);
            if (added != null)
            {
                added.Group = this;
                if (m_Settings && m_Settings.IsPersisted)
                    EditorUtility.SetDirty(added);

                SetDirty(AddressableAssetSettings.ModificationEvent.GroupSchemaAdded, this, postEvent, true);

                AssetDatabase.SaveAssets();
            }

            return added;
        }

        /// <summary>
        /// Creates and adds a schema of a given type to this group.  The schema asset will be created in the GroupSchemas directory relative to the settings asset.
        /// </summary>
        /// <param name="type">The schema type. This type must not already be added.</param>
        /// <param name="postEvent">Determines if this method call will post an event to the internal addressables event system</param>
        /// <returns>The created schema object.</returns>
        public AddressableAssetGroupSchema AddSchema(Type type, bool postEvent = true)
        {
            var added = m_SchemaSet.AddSchema(type, GetSchemaAssetPath);
            if (added != null)
            {
                added.Group = this;
                if (m_Settings && m_Settings.IsPersisted)
                    EditorUtility.SetDirty(added);

                SetDirty(AddressableAssetSettings.ModificationEvent.GroupSchemaAdded, this, postEvent, true);

                AssetDatabase.SaveAssets();
            }

            return added;
        }

        /// <summary>
        /// Creates and adds a schema of a given type to this group.
        /// </summary>
        /// <param name="postEvent">Determines if this method call will post an event to the internal addressables event system</param>
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
        /// <param name="postEvent">Determines if this method call will post an event to the internal addressables event system</param>
        /// <returns>True if the schema was found and removed, false otherwise.</returns>
        public bool RemoveSchema(Type type, bool postEvent = true)
        {
            if (!m_SchemaSet.RemoveSchema(type))
                return false;

            SetDirty(AddressableAssetSettings.ModificationEvent.GroupSchemaRemoved, this, postEvent, true);
            return true;
        }

        /// <summary>
        ///  Remove a given schema from this group.
        /// </summary>
        /// <param name="postEvent">Determines if this method call will post an event to the internal addressables event system</param>
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
        /// <param name="type">The schema type.</param>
        /// <returns>The schema if found, otherwise null.</returns>
        public AddressableAssetGroupSchema GetSchema(Type type)
        {
            return m_SchemaSet.GetSchema(type);
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
        /// <param name="postEvent">Determines if this method call will post an event to the internal addressables event system</param>
        public void ClearSchemas(bool deleteAssets, bool postEvent = true)
        {
            m_SchemaSet?.ClearSchemas(deleteAssets);
            SetDirty(AddressableAssetSettings.ModificationEvent.GroupRemoved, this, postEvent, true);
        }

        /// <summary>
        /// Checks if the group contains a schema of a given type.
        /// </summary>
        /// <param name="type">The schema type.</param>
        /// <returns>True if the schema type or subclass has been added to this group.</returns>
        public bool HasSchema(Type type)
        {
            return GetSchema(type) != null;
        }

        /// <summary>
        /// Is this group read only.  This is normally false.
        /// </summary>
        public virtual bool ReadOnly
        {
            get => m_ReadOnly;
            internal set => m_ReadOnly = value;
        }

        /// <summary>
        /// The AddressableAssetSettings that this group belongs to.
        /// </summary>
        public AddressableAssetSettings Settings
        {
            get
            {
                if (m_Settings == null)
                    m_Settings = AddressableAssetSettingsDefaultObject.Settings;

                return m_Settings;
            }
        }

        /// <summary>
        /// The collection of asset entries.
        /// </summary>
        public virtual ICollection<AddressableAssetEntry> entries
        {
            get { return m_EntryMap.Values; }
        }

        internal Dictionary<string, AddressableAssetEntry> EntryMap => m_EntryMap;

        internal ICollection<AddressableAssetEntry> FolderEntries
        {
            get
            {
                if (m_FolderEntryCache == null)
                    RefreshEntriesCache();
                return m_FolderEntryCache;
            }
        }

        /// <summary>
        /// Is the default group.
        /// </summary>
        public virtual bool Default
        {
            get { return Guid == Settings.DefaultGroupGuid; }
        }

        /// <summary>
        /// Compares two asset entries based on their guids.
        /// </summary>
        /// <param name="x">The first entry to compare.</param>
        /// <param name="y">The second entry to compare.</param>
        /// <returns>Returns 0 if both entries are null or equivalent.
        /// Returns -1 if the first entry is null or the first entry precedes the second entry in the sort order.
        /// Returns 1 if the second entry is null or the first entry follows the second entry in the sort order.</returns>
        public virtual int Compare(AddressableAssetEntry x, AddressableAssetEntry y)
        {
            if (x == null && y == null)
                return 0;
            if (x == null)
                return -1;
            if (y == null)
                return 1;
            return x.guid.CompareTo(y.guid);
        }

        internal Hash128 m_CurrentHash;
        internal Hash128 currentHash
        {
            get
            {
                if (!m_CurrentHash.isValid)
                {
                    m_CurrentHash.Append(m_GroupName);
                    m_CurrentHash.Append(m_GUID);
                    m_CurrentHash.Append(entries.Count);
                    m_CurrentHash.Append(ref m_ReadOnly);
                    foreach (var e in entries)
                    {
                        var eh = e.currentHash;
                        m_CurrentHash.Append(ref eh);
                    }
                }
                return m_CurrentHash;
            }
        }

        /// <summary>
        /// Implementation of ISerializationCallbackReceiver. Converts data to serializable form before serialization,
        /// and sorts collections for deterministic ordering.
        /// </summary>
        public void OnBeforeSerialize()
        {
            if (m_SerializeEntries == null)
            {
                m_SerializeEntries = new List<AddressableAssetEntry>(entries.Count);
                foreach (var e in entries)
                    m_SerializeEntries.Add(e);
            }
            m_SerializeEntries.Sort((a, b) => string.CompareOrdinal(a.guid, b.guid));
        }

        /// <summary>
        /// Implementation of ISerializationCallbackReceiver. Converts data from serializable format.
        /// </summary>
        public void OnAfterDeserialize()
        {
            ResetEntryMap();
        }

        internal void ResetEntryMap()
        {
            m_EntryMap.Clear();
            m_FolderEntryCache = null;
            foreach (var e in m_SerializeEntries)
            {
                try
                {
                    e.parentGroup = this;
                    e.IsSubAsset = false;
                    m_EntryMap.Add(e.guid, e);
                }
                catch (Exception ex)
                {
                    Addressables.InternalSafeSerializationLog(e.address);
                    Debug.LogException(ex);
                }
            }
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
                for (int i = 0; i < m_SchemaSet.Schemas.Count; i++)
                {
                    if (m_SchemaSet.Schemas[i] == null)
                    {
                        m_SchemaSet.Schemas.RemoveAt(i);
                        allValid = false;
                        break;
                    }

                    if (m_SchemaSet.Schemas[i].Group == null)
                        m_SchemaSet.Schemas[i].Group = this;

                    m_SchemaSet.Schemas[i].Validate();
                }
            }

            if (m_Settings != null)
            {
                if (m_GroupName == null)
                    m_GroupName = Settings.FindUniqueGroupName("Packed Content Group");
            }
        }

        internal void DedupeEnteries()
        {
            if (m_Settings == null)
                return;

            List<AddressableAssetEntry> removeEntries = new List<AddressableAssetEntry>();
            foreach (AddressableAssetEntry e in m_EntryMap.Values)
            {
                AddressableAssetEntry lookedUpEntry = m_Settings.FindAssetEntry(e.guid);
                if (lookedUpEntry?.parentGroup != this)
                {
                    Debug.LogWarning(e.address
                        + " is already a member of group "
                        + lookedUpEntry?.parentGroup
                        + " but group "
                        + m_GroupName
                        + " contained a reference to it.  Removing reference.");
                    removeEntries.Add(e);
                }
            }

            if (removeEntries.Count > 0)
                RemoveAssetEntries(removeEntries);
        }

        internal void Initialize(AddressableAssetSettings settings, string groupName, string guid, bool readOnly)
        {
            m_Settings = settings;
            m_GroupName = groupName;
            if (m_GroupName == null)
                m_GroupName = settings.FindUniqueGroupName("Packed Content Group");
            m_ReadOnly = readOnly;
            m_GUID = guid;
        }

        /// <summary>
        /// Gathers all asset entries.  Each explicit entry may contain multiple sub entries. For example, addressable folders create entries for each asset contained within.
        /// </summary>
        /// <param name="results">The generated list of entries.  For simple entries, this will contain just the entry itself if specified.</param>
        /// <param name="includeSelf">Determines if the entry should be contained in the result list or just sub entries.</param>
        /// <param name="recurseAll">Determines if full recursion should be done when gathering entries.</param>
        /// <param name="includeSubObjects">Determines if sub objects such as sprites should be included.</param>
        /// <param name="entryFilter">Optional predicate to run against each entry, only returning those that pass.  A null filter will return all entries</param>
        public virtual void GatherAllAssets(List<AddressableAssetEntry> results, bool includeSelf, bool recurseAll, bool includeSubObjects, Func<AddressableAssetEntry, bool> entryFilter = null)
        {
            foreach (var e in entries)
                if (entryFilter == null || entryFilter(e))
                    e.GatherAllAssets(results, includeSelf, recurseAll, includeSubObjects, entryFilter);
        }

        internal void GatherAllDirectAssetReferenceEntryData(List<IReferenceEntryData> results, HashSet<string> processed)
        {
            if (processed == null)
                processed = new HashSet<string>();

            // go through all entries that are not in folder/collections
            foreach (AddressableAssetEntry entry in entries)
            {
                var path = entry.AssetPath;
                if (string.IsNullOrEmpty(path))
                    return;

                if (processed.Contains(path))
                    continue;
                if (FolderEntries.Contains(entry))
                    continue;

                processed.Add(path);
                var reference = new ImplicitAssetEntry()
                {
                    address = entry.address,
                    AssetPath = entry.AssetPath,
                    labels = new HashSet<string>(entry.labels)
                };
                results.Add(reference);
            }
        }

        internal void GatherAllFolderSubAssetReferenceEntryData(List<IReferenceEntryData> results, HashSet<string> processed)
        {
            if (processed == null)
                processed = new HashSet<string>();

            // go through all entries that are in folder
            foreach (AddressableAssetEntry folderEntry in FolderEntries)
            {
                var path = folderEntry.AssetPath;
                if (string.IsNullOrEmpty(path))
                    return;
                if (processed.Contains(path))
                    continue;

                processed.Add(folderEntry.AssetPath);

                string[] guids = AssetDatabase.FindAssets("", new[] {folderEntry.AssetPath});
                foreach (var guid in guids)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    if (string.IsNullOrEmpty(assetPath))
                        return;
                    if (processed.Contains(assetPath))
                        continue;
                    processed.Add(assetPath);

                    if (BuiltinSceneCache.Contains(assetPath))
                        continue;
                    if (AssetDatabase.IsValidFolder(assetPath))
                        continue;
                    if (!AddressableAssetUtility.IsPathValidForEntry(assetPath))
                        continue;

                    string relativePath = assetPath.Remove(0, folderEntry.AssetPath.Length);
                    string relativeAddress = folderEntry.address + relativePath;
                    var reference = new ImplicitAssetEntry()
                    {
                        address = relativeAddress,
                        AssetPath = assetPath,
                        labels = new HashSet<string>(folderEntry.labels)
                    };
                    results.Add(reference);
                }
            }
        }

        internal void AddAssetEntry(AddressableAssetEntry e, bool postEvent = true)
        {
            e.IsSubAsset = false;
            e.parentGroup = this;
            m_EntryMap[e.guid] = e;
            if (m_FolderEntryCache != null && !string.IsNullOrEmpty(e.AssetPath) && e.MainAssetType == typeof(DefaultAsset) && AssetDatabase.IsValidFolder(e.AssetPath))
                m_FolderEntryCache.Add(e);
            if (HasSchema<ContentUpdateGroupSchema>() && !GetSchema<ContentUpdateGroupSchema>().StaticContent)
                e.FlaggedDuringContentUpdateRestriction = false;
            m_SerializeEntries = null;
            SetDirty(AddressableAssetSettings.ModificationEvent.EntryAdded, e, postEvent, true);
        }

        /// <summary>
        /// Get an entry via the asset guid.
        /// </summary>
        /// <param name="guid">The asset guid.</param>
        /// <returns>The AddressableAssetEntry</returns>
        public virtual AddressableAssetEntry GetAssetEntry(string guid)
        {
            return GetAssetEntry(guid, false);
        }

        /// <summary>
        /// Get an entry via the asset guid.
        /// </summary>
        /// <param name="guid">The asset guid.</param>
        /// <param name="includeImplicit">Whether or not to include implicit asset entries in the search.</param>
        /// <returns>The AddressableAssetEntry</returns>
        public virtual AddressableAssetEntry GetAssetEntry(string guid, bool includeImplicit)
        {
            if (m_EntryMap.TryGetValue(guid, out var entry))
                return entry;

            if (includeImplicit && FolderEntries.Count > 0)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);

                foreach (var e in m_FolderEntryCache)
                {
                    entry = e.GetFolderSubEntry(guid, assetPath);
                    if (entry != null)
                        return entry;
                }
            }
            return null;
        }

        /// <summary>
        /// Marks the object as modified.
        /// </summary>
        /// <param name="modificationEvent">The event type that is changed.</param>
        /// <param name="eventData">The object data that corresponds to the event.</param>
        /// <param name="postEvent">If true, the event is propagated to callbacks.</param>
        /// <param name="groupModified">If true, the group asset will be marked as dirty.</param>
        public void SetDirty(AddressableAssetSettings.ModificationEvent modificationEvent, object eventData, bool postEvent, bool groupModified = false)
        {
            m_CurrentHash = default;
            if (Settings != null)
            {
                if (groupModified && Settings.IsPersisted && this != null)
                    EditorUtility.SetDirty(this);
                Settings.SetDirty(modificationEvent, eventData, postEvent, false);
            }
        }

        /// <summary>
        /// Remove an entry.
        /// </summary>
        /// <param name="entry">The entry to remove.</param>
        /// <param name="postEvent">If true, post the event to callbacks.</param>
        public void RemoveAssetEntry(AddressableAssetEntry entry, bool postEvent = true)
        {
            m_EntryMap.Remove(entry.guid);
            m_FolderEntryCache?.Remove(entry);
            entry.parentGroup = null;
            m_SerializeEntries = null;
            SetDirty(AddressableAssetSettings.ModificationEvent.EntryRemoved, entry, postEvent, true);
        }

        internal void RemoveAssetEntries(IEnumerable<AddressableAssetEntry> removeEntries, bool postEvent = true)
        {
            foreach (AddressableAssetEntry entry in removeEntries)
            {
                m_EntryMap.Remove(entry.guid);
                m_FolderEntryCache?.Remove(entry);
                entry.parentGroup = null;
            }

            if (removeEntries.Count() > 0)
            {
                m_SerializeEntries = null;
                SetDirty(AddressableAssetSettings.ModificationEvent.EntryRemoved, removeEntries.ToArray(), postEvent, true);
            }
        }

        /// <summary>
        /// Check to see if a group is the Default Group.
        /// </summary>
        /// <returns>True if it's the default group, false otherwise.</returns>
        public bool IsDefaultGroup()
        {
            return Guid == m_Settings.DefaultGroup.Guid;
        }

        /// <summary>
        /// Check if a group has the appropriate schemas and attributes that the Default Group requires.
        /// </summary>
        /// <returns>True if the Group isn't read only, false otherwise.</returns>
        public bool CanBeSetAsDefault()
        {
            return !m_ReadOnly;
        }

        /// <summary>
        /// Gets the index of a schema based on its specified type.
        /// </summary>
        /// <param name="type">The schema type.</param>
        /// <returns>Valid index if found, otherwise returns -1.</returns>
        public int FindSchema(Type type)
        {
            var schemas = m_SchemaSet.Schemas;
            for (int i = 0; i < schemas.Count; i++)
            {
                if (schemas[i].GetType() == type)
                {
                    return i;
                }
            }

            return -1;
        }

        private bool RenameSchemaAssets()
        {
            return m_SchemaSet.RenameSchemaAssets(GetSchemaAssetPath);
        }
    }
}
