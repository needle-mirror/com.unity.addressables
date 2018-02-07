using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEditor.Build.Utilities;
using System.Runtime.Serialization.Formatters.Binary;

[assembly: System.Runtime.CompilerServices.InternalsVisibleToAttribute("Unity.Addressables.Editor.Tests")]

namespace UnityEditor.AddressableAssets
{
    /// <summary>
    /// TODO - doc
    /// </summary>
    public partial class AddressableAssetSettings : ScriptableObject, ISerializationCallbackReceiver
    {
        const string DefaultConfigName = "AddresableAssetSettings";
        const string DefaultConfigFolder = "Assets/AddressableAssetsData";
        /// <summary>
        /// TODO - doc
        /// </summary>
        public enum ModificationEvent
        {
            GroupAdded,
            GroupRemoved,
            GroupProcessorChanged,
            EntryAdded,
            EntryMoved,
            EntryRemoved,
            LabelAdded,
            LabelRemoved,
            ProfileAdded,
            ProfileRemoved,
            ProfileModified,
            GroupRenamed,
            GroupProcessorModified,
            EntryModified,
            BuildSettingsChanged
        }

        /// <summary>
        /// TODO - doc
        /// </summary>
        public Action<AddressableAssetSettings, ModificationEvent, object> OnModification;
        [SerializeField]
        internal string pathForAsset;
        [SerializeField]
        Hash128 m_cachedHash;
        public Hash128 currentHash
        {
            get
            {
                if (m_cachedHash.isValid)
                    return m_cachedHash;
                var stream = new MemoryStream();
                var formatter = new BinaryFormatter();
                //formatter.Serialize(stream, m_buildSettings);
                m_buildSettings.SerializeForHash(formatter, stream);
                formatter.Serialize(stream, activeProfile);
                formatter.Serialize(stream, m_labelTable);
                formatter.Serialize(stream, m_profileSettings);
                formatter.Serialize(stream, m_groups.Count);
                foreach (var g in m_groups)
                    g.SerializeForHash(formatter, stream);
                return (m_cachedHash = HashingMethods.CalculateMD5Hash(stream));
            }
        }

        [SerializeField]
        List<AssetGroup> m_groups = new List<AssetGroup>();
        /// <summary>
        /// TODO - doc
        /// </summary>
        public List<AssetGroup> groups { get { return m_groups; } }

        [SerializeField]
        BuildSettings m_buildSettings = new BuildSettings();
        /// <summary>
        /// TODO - doc
        /// </summary>
        public BuildSettings buildSettings { get { return m_buildSettings; } }

        [SerializeField]
        ProfileSettings m_profileSettings = new ProfileSettings();
        /// <summary>
        /// TODO - doc
        /// </summary>
        public ProfileSettings profileSettings { get { return m_profileSettings; } }

        [SerializeField]
        LabelTable m_labelTable = new LabelTable();
        /// <summary>
        /// TODO - doc
        /// </summary>
        internal LabelTable labelTable { get { return m_labelTable; } }
        /// <summary>
        /// TODO - doc
        /// </summary>
        public void AddLabel(string label)
        {
            m_labelTable.AddLabelName(label);
            PostModificationEvent(ModificationEvent.LabelAdded, label);
        }

        /// <summary>
        /// TODO - doc
        /// </summary>
        public void RemoveLabel(string label)
        {
            m_labelTable.RemoveLabelName(label);
            PostModificationEvent(ModificationEvent.LabelRemoved, label);
        }

        /// <summary>
        /// TODO - doc
        /// </summary>
        public long GetLabelMask(HashSet<string> maskSet)
        {
            return m_labelTable.GetMask(maskSet);
        }

        [SerializeField]
        string m_activeProfileId;
        /// <summary>
        /// TODO - doc
        /// </summary>
        public string activeProfile
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
            }
        }

        private Dictionary<string, AssetGroup.AssetEntry> m_allEntries = new Dictionary<string, AssetGroup.AssetEntry>();
        internal Dictionary<string, AssetGroup.AssetEntry> allEntries { get { return m_allEntries; } }

        /// <summary>
        /// TODO - doc
        /// </summary>
        internal IEnumerable<AssetGroup.AssetEntry> assetEntries
        { get { return m_allEntries.Values; } }

        public void RemoveAssetEntry(string guid)
        {
            var entry = FindAssetEntry(guid);
            if (entry != null)
            {
                if (entry.parentGroup != null)
                    entry.parentGroup.RemoveAssetEntry(entry);
                m_allEntries.Remove(guid);
                PostModificationEvent(ModificationEvent.EntryRemoved, entry);
            }
        }

        public void OnBeforeSerialize()
        {
            foreach (var g in groups)
                g.OnBeforeSerialize(this);
        }

        public void OnAfterDeserialize()
        {
            m_allEntries.Clear();
            foreach (var g in groups)
                g.OnAfterDeserialize(m_allEntries);
            profileSettings.OnAfterDeserialize(this);
            buildSettings.OnAfterDeserialize(this);
        }

        internal const string PlayerDataGroupName = "Built In Data";
        internal const string DefaultLocalGroupName = "Default Local Group";
        public static AddressableAssetSettings GetDefault(bool create, bool browse)
        {
            return GetDefault(create, browse, DefaultConfigFolder, DefaultConfigName);
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
                    //if (browse)
                    //    path = EditorUtility.SaveFilePanelInProject("Addressable Assets Config Folder", configName, "asset", "Select file for Addressable Assets Settings", configFolder);
                    Debug.Log("Creating Addressables settings object: " + path);

                    AssetDatabase.CreateAsset(aa = CreateInstance<AddressableAssetSettings>(), path);
                    aa.profileSettings.Reset();
                    aa.name = configName;
                    aa.pathForAsset = path;
                    var playerData = aa.CreateGroup(PlayerDataGroupName, typeof(PlayerDataAssetGroupProcessor).Name);
                    playerData.readOnly = true;
                    var resourceEntry = aa.CreateOrMoveEntry(AssetGroup.AssetEntry.ResourcesName, playerData);
                    resourceEntry.isInResources = true;
                    aa.CreateOrMoveEntry(AssetGroup.AssetEntry.EditorSceneListName, playerData);

                    aa.CreateGroup(DefaultLocalGroupName, typeof(LocalAssetBundleAssetGroupProcessor).Name, true);

                    AssetDatabase.SaveAssets();
                    EditorBuildSettings.AddConfigObject(configName, aa, true);
                }
            }
            return aa;
        }

        public AssetGroup FindGroup(string name)
        {
            return groups.Find(s => s.name == name);
        }

        /// <summary>
        /// TODO - doc
        /// </summary>
        public AssetGroup DefaultGroup
        {
            get
            {
                return groups.Find(s => s.isDefault);
            }
        }


        internal void GetAllSceneEntries(List<AssetGroup.AssetEntry> entries)
        {
            foreach (var a in allEntries)
                a.Value.GatherSceneEntries(entries, this, true);
        }

        private AssetGroup.AssetEntry CreateEntry(string guid, string address, AssetGroup parent, bool readOnly)
        {
            var entry = new AssetGroup.AssetEntry(guid, address, parent, readOnly);
            m_allEntries.Add(guid, entry);
            PostModificationEvent(ModificationEvent.EntryAdded, entry);
            return entry;
        }

        internal void PostModificationEvent(ModificationEvent e, object o)
        {
            if (e == ModificationEvent.ProfileRemoved && o as string == activeProfile)
                activeProfile = null;

            if (OnModification != null)
                OnModification(this, e, o);
            EditorUtility.SetDirty(this);
            m_cachedHash = default(Hash128);
        }

        /// <summary>
        /// TODO - doc
        /// </summary>
        public AssetGroup.AssetEntry FindAssetEntry(string guid)
        {
            AssetGroup.AssetEntry entry;
            if (m_allEntries.TryGetValue(guid, out entry))
                return entry;
            return null;
        }

        /// <summary>
        /// TODO - doc
        /// </summary>
        public void MoveAssetsFromResources(Dictionary<string, string> guidToNewPath, AssetGroup targetParent)
        {
            foreach(var item in guidToNewPath)
            {
                AssetGroup.AssetEntry entry = FindAssetEntry(item.Key);
                if (entry != null) //move entry to where it should go...
                {   
                    var dirInfo = new FileInfo(item.Value).Directory;
                    if(!dirInfo.Exists)
                    {
                        dirInfo.Create();
                        AssetDatabase.Refresh();
                    }
                    

                    var errorStr = AssetDatabase.MoveAsset(entry.assetPath, item.Value);
                    if (errorStr != string.Empty)
                        Debug.LogError("Error moving asset: " + errorStr);
                    else
                    {
                        AssetGroup.AssetEntry e = FindAssetEntry(item.Key);
                        if (e != null)
                            e.isInResources = false;
                        CreateOrMoveEntry(item.Key, targetParent);
                    }
                }
            }

            AssetDatabase.Refresh();
        }

        /// <summary>
        /// TODO - doc
        /// </summary>
        // create a new entry, or if one exists in a different group, move it into the new group
        public AssetGroup.AssetEntry CreateOrMoveEntry(string guid, AssetGroup targetParent, bool readOnly = false)
        {
            AssetGroup.AssetEntry entry = FindAssetEntry(guid);
            if (entry != null) //move entry to where it should go...
            {
                entry.isSubAsset = false;
                entry.readOnly = readOnly;
                if (entry.parentGroup == targetParent)
                {
                    targetParent.AddAssetEntry(entry); //in case this is a sub-asset, make sure parent knows about it now.
                    PostModificationEvent(ModificationEvent.EntryMoved, entry);
                    return entry;
                }

                if (entry.isInSceneList)
                {
                    var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
                    foreach (var scene in scenes)
                    {
                        if (scene.guid == new GUID(entry.guid))
                            scene.enabled = false;
                    }
                    EditorBuildSettings.scenes = scenes.ToArray();
                    entry.isInSceneList = false;
                }
                if (entry.parentGroup != null)
                    entry.parentGroup.RemoveAssetEntry(entry);
                entry.parentGroup = targetParent;
            }
            else //create entry
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (AddressablesUtility.IsPathValidForEntry(path))
                {
                    entry = CreateEntry(guid, path, targetParent, readOnly);
                }
                else
                {
                    entry = CreateEntry(guid, guid, targetParent, true);
                }
            }

            targetParent.AddAssetEntry(entry);
            PostModificationEvent(ModificationEvent.EntryAdded, entry);
            return entry;
        }

        /// <summary>
        /// TODO - doc
        /// </summary>
        // create a new entry, or if one exists in a different group, return null. do not tell parent group about new entry
        internal AssetGroup.AssetEntry CreateSubEntryIfUnique(string guid, string address, AssetGroup.AssetEntry parentEntry)
        {
            if (string.IsNullOrEmpty(guid))
                return null;

            bool readOnly = true;
            var entry = FindAssetEntry(guid);
            if (entry == null)
            {
                entry = CreateEntry(guid, address, parentEntry.parentGroup, readOnly);
                entry.isSubAsset = true;
                return entry;
            }
            else
            {
                //if the sub-entry already exists update it's info.  This mainly covers the case of dragging folders around.
                if (entry.isSubAsset)
                {
                    entry.parentGroup = parentEntry.parentGroup;
                    entry.isInResources = parentEntry.isInResources;
                    entry.address = address;
                    entry.readOnly = readOnly;
                    return entry;
                }
            }
            return null;
        }

        public void ConvertGroup(AssetGroup group, string processorType)
        {
            var proc = CreateInstance(processorType) as AssetGroupProcessor;
            proc.Initialize(this);
            var name = proc.displayName + " Group";
            string validName = FindUniqueGroupName(name);
            var path = System.IO.Path.GetDirectoryName(pathForAsset) + "/" + processorType + "_" + validName + ".asset";
            AssetDatabase.CreateAsset(proc, path);
            var guid = AssetDatabase.AssetPathToGUID(path);
            AssetDatabase.MoveAsset(path, System.IO.Path.GetDirectoryName(pathForAsset) + "/" + guid.ToString() + ".asset");
            group.ReplaceProcessor(proc, guid);
            PostModificationEvent(ModificationEvent.GroupProcessorChanged, group);
        }

        /// <summary>
        /// TODO - doc
        /// </summary>
        public AssetGroup CreateGroup(string name, string processorType, bool setAsDefaultGroup = false)
        {
            var proc = CreateInstance(processorType) as AssetGroupProcessor;
            proc.Initialize(this);
            if (string.IsNullOrEmpty(name))
                name = proc.displayName + " Group";
            string validName = FindUniqueGroupName(name);
            var path = Path.GetDirectoryName(pathForAsset) + "/" + processorType + "_" + validName + ".asset";
            AssetDatabase.CreateAsset(proc, path);
            var guid = AssetDatabase.AssetPathToGUID(path);
            AssetDatabase.MoveAsset(path, Path.GetDirectoryName(pathForAsset) + "/" + guid.ToString() + ".asset");
            var g = new AssetGroup(validName, proc, setAsDefaultGroup, guid);
            groups.Add(g);
            PostModificationEvent(ModificationEvent.GroupAdded, g);
            return g;
        }

        string FindUniqueGroupName(string name)
        {
            var validName = name;
            int index = 1;
            bool foundExisting = true;
            while (foundExisting)
            {
                if (index > 1000)
                {
                    Debug.LogError("Unable to create valid name for new Addressable Assets group.");
                    return name;
                }
                foundExisting = false;
                foreach (var g in groups)
                {
                    if (g.name == validName)
                    {
                        foundExisting = true;
                        validName = name + index.ToString();
                        index++;
                        break;
                    }
                }
            }

            return validName;
        }

        /// <summary>
        /// TODO - doc
        /// </summary>
        public void RemoveGroup(AssetGroup g)
        {
            foreach (var e in g.entries)
                m_allEntries.Remove(e.guid);
            var path = System.IO.Path.GetDirectoryName(pathForAsset) + "/" + g.guid.ToString() + ".asset";
            AssetDatabase.DeleteAsset(path);
            groups.Remove(g);
            PostModificationEvent(ModificationEvent.GroupRemoved, g);
        }
    }
}
