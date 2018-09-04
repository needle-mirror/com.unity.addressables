using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEditor.Build.Pipeline.Utilities;
using UnityEngine.AddressableAssets;

[assembly: System.Runtime.CompilerServices.InternalsVisibleToAttribute("Unity.Addressables.Editor.Tests")]

namespace UnityEditor.AddressableAssets
{
    /// <summary>
    /// Contains editor data for the addressables system.
    /// </summary>
    public partial class AddressableAssetSettings : ScriptableObject
    {
        /// <summary>
        /// Default name for the config object.
        /// </summary>
        public const string kDefaultConfigName = "AddressableAssetSettings";
        /// <summary>
        /// The default folder for the serialized version of this class.
        /// </summary>
        public const string kDefaultConfigFolder = "Assets/AddressableAssetsData";
        /// <summary>
        /// Default name of a newly created group.
        /// </summary>
        public const string kNewGroupName = "New Group";
        /// <summary>
        /// Enumeration of different event types that are generated.
        /// </summary>
        public enum ModificationEvent
        {
            GroupAdded,
            GroupRemoved,
            GroupProcessorChanged,
            GroupDataModified,
            EntryCreated,
            EntryAdded,
            EntryMoved,
            EntryRemoved,
            LabelAdded,
            LabelRemoved,
            ProfileAdded,
            ProfileRemoved,
            ProfileModified,
            ActiveProfileSet,
            GroupRenamed,
            GroupProcessorModified,
            EntryModified,
            BuildSettingsChanged,
            BatchModification // <-- posted object will be null.
        }

        /// <summary>
        /// The path of the settings asset.
        /// </summary>
        public string AssetPath
        {
            get
            {
                string guid;
                long localId;
                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(this, out guid, out localId))
                    return kDefaultConfigFolder + "/" + kDefaultConfigName + ".asset";
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(assetPath))
                    return kDefaultConfigFolder + "/" + kDefaultConfigName + ".asset";
                return assetPath;
            }
        }

        /// <summary>
        /// The folder of the settings asset.
        /// </summary>
        public string ConfigFolder
        {
            get
            {
                return Path.GetDirectoryName(AssetPath);
            }
        }

        /// <summary>
        /// The folder for the group assets.
        /// </summary>
        public string GroupFolder
        {
            get
            {
                return ConfigFolder + "/AssetGroups";
            }
        }

        /// <summary>
        /// Event for watching settings changes.
        /// </summary>
        public Action<AddressableAssetSettings, ModificationEvent, object> OnModification { get; set; }
        [SerializeField]
        private string m_defaultGroup;
        [SerializeField]
        Hash128 m_cachedHash;
        /// <summary>
        /// Hash of the current settings.  This value is recomputed if anything changes.
        /// </summary>
        public Hash128 currentHash
        {
            get
            {
                if (m_cachedHash.isValid)
                    return m_cachedHash;
                var stream = new MemoryStream();
                var formatter = new BinaryFormatter();
                //                formatter.Serialize(stream, m_buildSettings);
                m_buildSettings.SerializeForHash(formatter, stream);
                formatter.Serialize(stream, activeProfileId);
                formatter.Serialize(stream, m_labelTable);
                formatter.Serialize(stream, m_profileSettings);
                formatter.Serialize(stream, m_groupAssets.Count);
                foreach (var g in m_groupAssets)
                    g.SerializeForHash(formatter, stream);
                return (m_cachedHash = HashingMethods.Calculate(stream).ToHash128());
            }
        }

        [SerializeField]
        bool m_assetsModified = true;
        internal bool AssetsModifiedSinceLastPackedBuild
        {
            get { return m_assetsModified; }
            set { m_assetsModified = value; }
        }

        internal class FileModificationWarning : AssetModificationProcessor
        {
            static string[] OnWillSaveAssets(string[] paths)
            {
                var aa = GetDefault(false, false);
                if (aa != null)
                    aa.AssetsModifiedSinceLastPackedBuild = true;
                return paths;
            }
        }

        class AddressablesAssetPostProcessor : AssetPostprocessor
        {
            internal static bool ignoreAll = true;
            static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
            {
                if (ignoreAll)
                    return;
                var aa = GetDefault(false, false);
                if (aa == null)
                    return;
                bool modified = false;
                foreach (string str in importedAssets)
                {
                    if (AssetDatabase.GetMainAssetTypeAtPath(str) == typeof(AddressableAssetEntryCollection))
                    {
                        aa.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(str), aa.DefaultGroup);
                        modified = true;
                    }
                    var guid = AssetDatabase.AssetPathToGUID(str);
                    if (aa.FindAssetEntry(guid) != null)
                        modified = true;
                }
                foreach (string str in deletedAssets)
                {
                    var guidOfDeletedAsset = AssetDatabase.AssetPathToGUID(str);
                    var oldGroupName = Path.GetFileNameWithoutExtension(str);
                    var group = aa.FindGroup(oldGroupName);
                    var groupObj = group as object;
                    bool assetIsGroup = false;
                    if (groupObj != null)
                    {
                        string guidOfGroup;
                        long localId;
                        if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(group, out guidOfGroup, out localId))
                        {
                            if (guidOfGroup == guidOfDeletedAsset)
                            {
                                assetIsGroup = true;
                                aa.RemoveGroupInternal(group, false, true);
                            }
                        }
                    }

                    if (!assetIsGroup)
                    {
                        var guid = AssetDatabase.AssetPathToGUID(str);
                        if (aa.RemoveAssetEntry(guid))
                            modified = true;
                    }
                }
                for (int i = 0; i < movedAssets.Length; i++)
                {
                    var str = movedAssets[i];
                    if (AssetDatabase.GetMainAssetTypeAtPath(str) == typeof(AddressableAssetGroup))
                    {
                        var oldGroupName = Path.GetFileNameWithoutExtension(movedFromAssetPaths[i]);
                        var group = aa.FindGroup(oldGroupName);
                        if (group != null)
                        {
                            var newGroupName = Path.GetFileNameWithoutExtension(str);
                            group.Name = newGroupName;
                        }
                    }
                    else
                    {
                        var guid = AssetDatabase.AssetPathToGUID(str);
                        if (aa.FindAssetEntry(guid) != null)
                            modified = true;
                    }
                }
                if (modified)
                    aa.MarkDirty();
                aa.AssetsModifiedSinceLastPackedBuild = true;
            }
        }

        /// <summary>
        /// Create an AssetReference object.  If the asset is not already addressable, it will be added.  
        /// </summary>
        /// <param name="guid">The guid of the asset reference.</param>
        /// <returns>Returns the newly created AssetReference.</returns>
        public AssetReference CreateAssetReference(string guid)
        {
            CreateOrMoveEntry(guid, DefaultGroup);
            return new AssetReference(guid);
        }

        private void MarkDirty()
        {
            m_cachedHash = default(Hash128);
        }

        /// <summary>
        /// The version of the player build.  This is implemented as a timestamp int UTC of the form  string.Format("{0:D4}.{1:D2}.{2:D2}.{3:D2}.{4:D2}.{5:D2}", now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second).
        /// </summary>
        public string PlayerBuildVersion
        {
            get
            {
                var now = DateTime.UtcNow;
                return string.Format("{0:D4}.{1:D2}.{2:D2}.{3:D2}.{4:D2}.{5:D2}", now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second);
            }
        }

        //TODO: deprecate and remove once most users have transitioned to newer external data files
        [SerializeField]
        List<AddressableAssetGroupDeprecated> m_groups = new List<AddressableAssetGroupDeprecated>();

        [SerializeField]
        List<AddressableAssetGroup> m_groupAssets = new List<AddressableAssetGroup>();
        /// <summary>
        /// List of asset groups.
        /// </summary>
        public List<AddressableAssetGroup> groups { get { return m_groupAssets; } }

        [SerializeField]
        AddressableAssetBuildSettings m_buildSettings = new AddressableAssetBuildSettings();
        /// <summary>
        /// Build settings object.
        /// </summary>
        public AddressableAssetBuildSettings buildSettings { get { return m_buildSettings; } }

        [SerializeField]
        AddressableAssetProfileSettings m_profileSettings = new AddressableAssetProfileSettings();
        /// <summary>
        /// Profile settings object.
        /// </summary>
        public AddressableAssetProfileSettings profileSettings { get { return m_profileSettings; } }

        [SerializeField]
        LabelTable m_labelTable = new LabelTable();
        /// <summary>
        /// LabelTable object.
        /// </summary>
        internal LabelTable labelTable { get { return m_labelTable; } }
        /// <summary>
        /// Add a new label.
        /// </summary>
        /// <param name="label">The label name.</param>
        /// <param name="postEvent">Send modification event.</param>
        public void AddLabel(string label, bool postEvent = true)
        {
            m_labelTable.AddLabelName(label);
            if (postEvent)
                PostModificationEvent(ModificationEvent.LabelAdded, label);
        }

        /// <summary>
        /// Remove a label by name.
        /// </summary>
        /// <param name="label">The label name.</param>
        /// <param name="postEvent">Send modification event.</param>
        public void RemoveLabel(string label, bool postEvent = true)
        {
            m_labelTable.RemoveLabelName(label);
            if (postEvent)
                PostModificationEvent(ModificationEvent.LabelRemoved, label);
        }

        [SerializeField]
        string m_activeProfileId;
        /// <summary>
        /// The active profile id.
        /// </summary>
        public string activeProfileId
        {
            get
            {
                if (string.IsNullOrEmpty(m_activeProfileId))
                    m_activeProfileId = m_profileSettings.CreateDefaultProfile();
                return m_activeProfileId;
            }
            set
            {
                m_activeProfileId = value;
                PostModificationEvent(ModificationEvent.ActiveProfileSet, m_activeProfileId);
            }
        }

        /// <summary>
        /// Gets all asset entries from all groups.
        /// </summary>
        /// <param name="assets">The list of asset entries.</param>
        public void GetAllAssets(List<AddressableAssetEntry> assets)
        {
            foreach (var g in groups)
                g.GatherAllAssets(assets, true, true);
        }

        /// <summary>
        /// Remove an asset entry.
        /// </summary>
        /// <param name="guid">The  guid of the asset.</param>
        /// <param name="postEvent">Send modifcation event.</param>
        /// <returns>True if the entry was found and removed.</returns>
        public bool RemoveAssetEntry(string guid, bool postEvent = true)
        {
            var entry = FindAssetEntry(guid);
            if (entry != null)
            {
                if (entry.parentGroup != null)
                    entry.parentGroup.RemoveAssetEntry(entry, postEvent);
                if (postEvent)
                    PostModificationEvent(ModificationEvent.EntryRemoved, entry);
                return true;
            }
            return false;
        }

        void OnEnable()
        {
            AddressablesAssetPostProcessor.ignoreAll = true;

            //TODO: deprecate and remove once most users have transitioned to newer external data files
            if (m_groups != null)
            {
                for(int i = 0; i < m_groups.Count; i++)
                    if (m_groups[i] != null)
                        this.ConvertDeprecatedGroupData(m_groups[i], i < 2);
                m_groups = null;
            }

            profileSettings.OnAfterDeserialize(this);
            buildSettings.OnAfterDeserialize(this);
            Validate();
            AddressablesAssetPostProcessor.ignoreAll = false;
        }

        void Validate()
        {
            if (m_buildSettings == null)
                m_buildSettings = new AddressableAssetBuildSettings();
            if (m_profileSettings == null)
                m_profileSettings = new AddressableAssetProfileSettings();
            if (m_labelTable == null)
                m_labelTable = new LabelTable();
            if (string.IsNullOrEmpty(m_activeProfileId))
                m_activeProfileId = m_profileSettings.CreateDefaultProfile();

            foreach (var g in groups)
                g.Validate(this);
            profileSettings.Validate(this);
            buildSettings.Validate(this);
        }

        internal const string PlayerDataGroupName = "Built In Data";
        internal const string DefaultLocalGroupName = "Default Local Group";
        /// <summary>
        /// Get the default addressables settings object.
        /// </summary>
        /// <param name="create">Create a new settings object if not found.</param>
        /// <param name="browse">Prompt the user with a dialog to browse for the location of the settings asset.</param>
        /// <returns></returns>
        public static AddressableAssetSettings GetDefault(bool create, bool browse)
        {
            return GetDefault(create, browse, kDefaultConfigFolder, kDefaultConfigName);
        }
        internal static AddressableAssetSettings GetDefault(bool create, bool browse, string configFolder, string configName)
        {
            AddressableAssetSettings aa = null;
            if (!EditorBuildSettings.TryGetConfigObject(configName, out aa))
            {
                if (create && !System.IO.Directory.Exists(configFolder))
                    System.IO.Directory.CreateDirectory(configFolder);

                var path = configFolder + "/" + configName + ".asset";
                aa = AssetDatabase.LoadAssetAtPath<AddressableAssetSettings>(path);
                if (aa == null && create)
                {
                    //uncomment this to restore the browse behavior
                    //if (browse)
                    //    path = EditorUtility.SaveFilePanelInProject("Addressable Assets Config Folder", configName, "asset", "Select file for Addressable Assets Settings", configFolder);
                    Addressables.Log("Creating Addressables settings object: " + path);

                    AssetDatabase.CreateAsset(aa = CreateInstance<AddressableAssetSettings>(), path);
                    aa.activeProfileId = aa.profileSettings.Reset();
                    aa.name = configName;
                    var playerData = aa.CreateGroup(PlayerDataGroupName, typeof(PlayerDataAssetGroupProcessor), false, true);
                    var resourceEntry = aa.CreateOrMoveEntry(AddressableAssetEntry.ResourcesName, playerData);
                    resourceEntry.IsInResources = true;
                    aa.CreateOrMoveEntry(AddressableAssetEntry.EditorSceneListName, playerData);
                    var localGroup = aa.CreateGroup(DefaultLocalGroupName, typeof(BundledAssetGroupProcessor), true, false);
                    localGroup.Processor.CreateDefaultData(localGroup);
                    localGroup.StaticContent = true;

                    AssetDatabase.SaveAssets();
                    EditorBuildSettings.AddConfigObject(configName, aa, true);
                }
            }
            return aa;
        }

        /// <summary>
        /// Find asset group by name.
        /// </summary>
        /// <param name="name">The name of the group.</param>
        /// <returns>The group found or null.</returns>
        public AddressableAssetGroup FindGroup(string name)
        {
            return groups.Find(s => s.Name == name);
        }

        /// <summary>
        /// The default group.  This group is used when marking assets as addressable via the inspector.
        /// </summary>
        public AddressableAssetGroup DefaultGroup
        {
            get
            {
                if (string.IsNullOrEmpty(m_defaultGroup))
                {
                    //set to the first non readonly group if possible
                    foreach (var g in groups)
                    {
                        if (!g.IsProcessorType(typeof(PlayerDataAssetGroupProcessor)))
                        {
                            m_defaultGroup = g.Guid;
                            break;
                        }
                    }
                    if (string.IsNullOrEmpty(m_defaultGroup))
                    {
                        Addressables.LogError("Attempting to access Default Addressables group but no valid group is available");
                        return null;
                    }
                }
                var group = groups.Find(s => s.Guid == m_defaultGroup);
                if(group == null)
                {
                    foreach(var g in groups)
                    {
                        if (!g.ReadOnly)
                        {
                            group = g;
                            break;
                        }
                    }
                    if(group == null)
                    {
                        Debug.LogWarning("Addressable assets must have at least one group that is not read-only to be default, creating new group");
                        group = CreateGroup("New Group", typeof(BundledAssetGroupProcessor), true, false);
                    }
                }
                return group;
            }
            set
            {
                m_defaultGroup = value.Guid;
            }
        }

        private AddressableAssetEntry CreateEntry(string guid, string address, AddressableAssetGroup parent, bool readOnly, bool postEvent = true)
        {
            var entry = new AddressableAssetEntry(guid, address, parent, readOnly);
            if (!readOnly && postEvent)
                PostModificationEvent(ModificationEvent.EntryCreated, entry);
            return entry;
        }

        internal void PostModificationEvent(ModificationEvent e, object o)
        {
            if (e == ModificationEvent.ProfileRemoved && o as string == activeProfileId)
                activeProfileId = null;

            if (OnModification != null)
                OnModification(this, e, o);
            var unityObj = o as UnityEngine.Object;
            if (unityObj != null)
                EditorUtility.SetDirty(unityObj);

            EditorUtility.SetDirty(this);
            m_cachedHash = default(Hash128);
        }

        /// <summary>
        /// Find and asset entry by guid.
        /// </summary>
        /// <param name="guid">The asset guid.</param>
        /// <returns>The found entry or null.</returns>
        public AddressableAssetEntry FindAssetEntry(string guid)
        {
            foreach (var g in groups)
            {
                var e = g.GetAssetEntry(guid);
                if (e != null)
                    return e;
            }
            return null;
        }

        internal void MoveAssetsFromResources(Dictionary<string, string> guidToNewPath, AddressableAssetGroup targetParent)
        {
            if (guidToNewPath == null)
                return;
            var entries = new List<AddressableAssetEntry>();
            AssetDatabase.StartAssetEditing();
            foreach (var item in guidToNewPath)
            {

                var dirInfo = new FileInfo(item.Value).Directory;
                if (!dirInfo.Exists)
                {
                    dirInfo.Create();
                    AssetDatabase.StopAssetEditing();
                    AssetDatabase.Refresh();
                    AssetDatabase.StartAssetEditing();
                }

                var oldPath = AssetDatabase.GUIDToAssetPath(item.Key);
                var errorStr = AssetDatabase.MoveAsset(oldPath, item.Value);
                if (!string.IsNullOrEmpty(errorStr))
                {
                    Addressables.LogError("Error moving asset: " + errorStr);
                }
                else
                {
                    AddressableAssetEntry e = FindAssetEntry(item.Key);
                    if (e != null)
                        e.IsInResources = false;

                    var newEntry = CreateOrMoveEntry(item.Key, targetParent, false, false);
                    var index = oldPath.ToLower().LastIndexOf("resources/");
                    if (index >= 0)
                    {
                        var newAddress = Path.GetFileNameWithoutExtension(oldPath.Substring(index + 10));
                        if (!string.IsNullOrEmpty(newAddress))
                        {
                            newEntry.address = newAddress;
                        }
                    }
                    entries.Add(newEntry);
                }

            }
            AssetDatabase.StopAssetEditing();
            AssetDatabase.Refresh();
            PostModificationEvent(AddressableAssetSettings.ModificationEvent.EntryMoved, entries);
        }


        /// <summary>
        /// Create a new entry, or if one exists in a different group, move it into the new group.
        /// </summary>
        /// <param name="guid">The asset guid.</param>
        /// <param name="targetParent">The group to add the entry to.</param>
        /// <param name="readOnly">Is the new entry read only.</param>
        /// <param name="postEvent">Send modification event.</param>
        /// <returns></returns>
        public AddressableAssetEntry CreateOrMoveEntry(string guid, AddressableAssetGroup targetParent, bool readOnly = false, bool postEvent = true)
        {
            if (targetParent == null)
                return null;

            AddressableAssetEntry entry = FindAssetEntry(guid);
            if (entry != null) //move entry to where it should go...
            {
                entry.IsSubAsset = false;
                entry.ReadOnly = readOnly;
                if (entry.parentGroup == targetParent)
                {
                    targetParent.AddAssetEntry(entry, postEvent); //in case this is a sub-asset, make sure parent knows about it now.
                    return entry;
                }

                if (entry.IsInSceneList)
                {
                    var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
                    foreach (var scene in scenes)
                    {
                        if (scene.guid == new GUID(entry.guid))
                            scene.enabled = false;
                    }
                    EditorBuildSettings.scenes = scenes.ToArray();
                    entry.IsInSceneList = false;
                }
                if (entry.parentGroup != null)
                    entry.parentGroup.RemoveAssetEntry(entry, postEvent);
                entry.parentGroup = targetParent;
            }
            else //create entry
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (AddressablesUtility.IsPathValidForEntry(path))
                {
                    entry = CreateEntry(guid, path, targetParent, readOnly, postEvent);
                }
                else
                {
                    entry = CreateEntry(guid, guid, targetParent, true, postEvent);
                }
            }

            targetParent.AddAssetEntry(entry, postEvent);
            return entry;
        }

        internal AddressableAssetEntry CreateSubEntryIfUnique(string guid, string address, AddressableAssetEntry parentEntry)
        {
            if (string.IsNullOrEmpty(guid))
                return null;

            bool readOnly = true;
            var entry = FindAssetEntry(guid);
            if (entry == null)
            {
                entry = CreateEntry(guid, address, parentEntry.parentGroup, readOnly);
                entry.IsSubAsset = true;
                return entry;
            }
            else
            {
                //if the sub-entry already exists update it's info.  This mainly covers the case of dragging folders around.
                if (entry.IsSubAsset)
                {
                    entry.parentGroup = parentEntry.parentGroup;
                    entry.IsInResources = parentEntry.IsInResources;
                    entry.address = address;
                    entry.ReadOnly = readOnly;
                    return entry;
                }
            }
            return null;
        }

        /// <summary>
        /// Obsolete - Convert a group to a new processor type.
        /// </summary>
        /// <param name="group">The group to convert.</param>
        /// <param name="processorType">The new processor type</param>
        //[Obsolete("This API is going to be replaced soon with a more flexible build system.")]
        public void ConvertGroup(AddressableAssetGroup group, Type processorType)
        {
            if (group == null)
                return;
            group.SetProcessorType(processorType);
            PostModificationEvent(ModificationEvent.GroupProcessorChanged, group);
        }

        /// <summary>
        /// Create a new asset group.
        /// </summary>
        /// <param name="groupName">The group name.</param>
        /// <param name="processorType">The processor type.</param>
        /// <param name="setAsDefaultGroup">Set the new group as the default group.</param>
        /// <param name="readOnly">Is the new group read only.</param>
        /// <param name="postEvent">Post modification event.</param>
        /// <returns>The newly created group.</returns>
        public AddressableAssetGroup CreateGroup(string groupName, Type processorType, bool setAsDefaultGroup, bool readOnly, bool postEvent = true)
        {
            if (string.IsNullOrEmpty(groupName))
                groupName = kNewGroupName;
            string validName = FindUniqueGroupName(groupName);
            var group = CreateInstance<AddressableAssetGroup>();
            group.Initialize(this, validName, processorType, GUID.Generate().ToString(), readOnly);
            groups.Add(group);
            if (!Directory.Exists(GroupFolder))
                Directory.CreateDirectory(GroupFolder);
            AssetDatabase.CreateAsset(group, GroupFolder + "/" + validName + ".asset");
            if (setAsDefaultGroup)
                DefaultGroup = group;
            if (postEvent)
                PostModificationEvent(ModificationEvent.GroupAdded, group);
            return group;
        }

        internal string FindUniqueGroupName(string name)
        {
            var validName = name;
            int index = 1;
            bool foundExisting = true;
            while (foundExisting)
            {
                if (index > 1000)
                {
                    Addressables.LogError("Unable to create valid name for new Addressable Assets group.");
                    return name;
                }
                foundExisting = IsNotUniqueGroupName(validName);
                if (foundExisting)
                {
                    validName = name + index.ToString();
                    index++;
                }
            }

            return validName;
        }

        internal bool IsNotUniqueGroupName(string name)
        {
            bool foundExisting = false;
            foreach (var g in groups)
            {
                if (g.Name == name)
                {
                    foundExisting = true;
                    break;
                }
            }
            return foundExisting;
        }

        /// <summary>
        /// Remove an asset group.
        /// </summary>
        /// <param name="g"></param>
        public void RemoveGroup(AddressableAssetGroup g)
        {
            RemoveGroupInternal(g, true, true);
        }

        internal void RemoveGroupInternal(AddressableAssetGroup g, bool deleteAsset, bool postEvent)
        {
            groups.Remove(g);
            if (postEvent)
                PostModificationEvent(ModificationEvent.GroupRemoved, g);
            if (deleteAsset)
            {
                string guidOfGroup;
                long localId;
                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(g, out guidOfGroup, out localId))
                {
                    var groupPath = AssetDatabase.GUIDToAssetPath(guidOfGroup);
                    if (!string.IsNullOrEmpty(groupPath))
                        AssetDatabase.DeleteAsset(groupPath);
                }
            }
        }


        internal void SetLabelValueForEntries(List<AddressableAssetEntry> entries, string label, bool value)
        {
            if (value)
                AddLabel(label);

            foreach (var e in entries)
                e.SetLabel(label, value, false);

            PostModificationEvent(ModificationEvent.EntryModified, entries);
        }

        internal void MoveEntriesToGroup(List<AddressableAssetEntry> entries, AddressableAssetGroup targetGroup)
        {
            foreach (var e in entries)
            {
                if (e.parentGroup != null)
                    e.parentGroup.RemoveAssetEntry(e, false);
                targetGroup.AddAssetEntry(e, false);
            }
            PostModificationEvent(ModificationEvent.EntryMoved, entries);
        }
    }
}
