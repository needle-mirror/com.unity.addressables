using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEditor.Build.Pipeline.Utilities;

[assembly: System.Runtime.CompilerServices.InternalsVisibleToAttribute("Unity.Addressables.Editor.Tests")]

namespace UnityEditor.AddressableAssets
{
    /// <summary>
    /// TODO - doc
    /// </summary>
    public partial class AddressableAssetSettings : ScriptableObject, ISerializationCallbackReceiver
    {
        public const string DefaultConfigName = "AddressableAssetSettings";
        public const string DefaultConfigFolder = "Assets/AddressableAssetsData";
        /// <summary>
        /// TODO - doc
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
            GroupRenamed,
            GroupProcessorModified,
            EntryModified,
            BuildSettingsChanged,
            BatchModification // <-- posted object will be null.
        }

        /// <summary>
        /// TODO - doc
        /// </summary>
        public Action<AddressableAssetSettings, ModificationEvent, object> OnModification { get; set; }
        [SerializeField]
        private string m_defaultGroup;
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
//                formatter.Serialize(stream, m_buildSettings);
                m_buildSettings.SerializeForHash(formatter, stream);
                formatter.Serialize(stream, activeProfileId);
                formatter.Serialize(stream, m_labelTable);
                formatter.Serialize(stream, m_profileSettings);
                formatter.Serialize(stream, m_groups.Count);
                foreach (var g in m_groups)
                    g.SerializeForHash(formatter, stream);
                return (m_cachedHash = HashingMethods.Calculate(stream).ToHash128());
            }
        }

        class ExternalEntryImporter : AssetPostprocessor
        {
            static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
            {
                var aa = GetDefault(false, false);
                if (aa == null)
                    return;
                bool modified = false;
                foreach (string str in importedAssets)
                {
                    if (AssetDatabase.GetMainAssetTypeAtPath(str) == typeof(AddressablesEntryCollection))
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
                    var guid = AssetDatabase.AssetPathToGUID(str);
                    if (aa.RemoveAssetEntry(guid))
                        modified = true;
                }
                foreach (var str in movedAssets)
                {
                    var guid = AssetDatabase.AssetPathToGUID(str);
                    if (aa.FindAssetEntry(guid) != null)
                        modified = true;
                }
                if (modified)
                    aa.MarkDirty();
            }
        }

        private void MarkDirty()
        {
            m_cachedHash = default(Hash128);
        }

        [SerializeField]
        List<AddressableAssetGroup> m_groups = new List<AddressableAssetGroup>();
        /// <summary>
        /// TODO - doc
        /// </summary>
        public List<AddressableAssetGroup> groups { get { return m_groups; } }

        [SerializeField]
        AddressableAssetBuildSettings m_buildSettings = new AddressableAssetBuildSettings();
        /// <summary>
        /// TODO - doc
        /// </summary>
        public AddressableAssetBuildSettings buildSettings { get { return m_buildSettings; } }

        [SerializeField]
        AddressableAssetProfileSettings m_profileSettings = new AddressableAssetProfileSettings();
        /// <summary>
        /// TODO - doc
        /// </summary>
        public AddressableAssetProfileSettings profileSettings { get { return m_profileSettings; } }
        
        [SerializeField]
        LabelTable m_labelTable = new LabelTable();
        /// <summary>
        /// TODO - doc
        /// </summary>
        internal LabelTable labelTable { get { return m_labelTable; } }
        /// <summary>
        /// TODO - doc
        /// </summary>
        public void AddLabel(string label, bool postEvent = true)
        {
            m_labelTable.AddLabelName(label);
            if(postEvent)
                PostModificationEvent(ModificationEvent.LabelAdded, label);
        }

        /// <summary>
        /// TODO - doc
        /// </summary>
        public void RemoveLabel(string label, bool postEvent = true)
        {
            m_labelTable.RemoveLabelName(label);
            if(postEvent)
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
            }
        }

        public void GetAllAssets(List<AddressableAssetEntry> assets)
        {
            foreach (var g in groups)
                g.GatherAllAssets(assets, true, true);
        }

        public bool RemoveAssetEntry(string guid, bool postEvent = true)
        {
            var entry = FindAssetEntry(guid);
            if (entry != null)
            {
                if (entry.parentGroup != null)
                    entry.parentGroup.RemoveAssetEntry(entry, postEvent);
                if(postEvent)
                    PostModificationEvent(ModificationEvent.EntryRemoved, entry);
                return true;
            }
            return false;
        }

        public void OnBeforeSerialize()
        {
            foreach (var g in groups)
                g.OnBeforeSerialize(this);
        }

        public void OnAfterDeserialize()
        {
            string[] groupNames = new string[] { PlayerDataGroupName, DefaultLocalGroupName, "Remote Group" };
            for (int i = 0; i < groups.Count; i++)
            {
                var g = groups[i];
                g.SetNameIfInvalid(groupNames[Mathf.Clamp(i, 0, groupNames.Length - 1)]);
                g.OnAfterDeserialize(this);
            }
            profileSettings.OnAfterDeserialize(this);
            buildSettings.OnAfterDeserialize(this);
        }

        public void OnEnable()
        {
            Validate();
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
                    //uncomment this to restore the browse behavior
                    //if (browse)
                    //    path = EditorUtility.SaveFilePanelInProject("Addressable Assets Config Folder", configName, "asset", "Select file for Addressable Assets Settings", configFolder);
                    Debug.Log("Creating Addressables settings object: " + path);

                    AssetDatabase.CreateAsset(aa = CreateInstance<AddressableAssetSettings>(), path);
                    aa.profileSettings.Reset();
                    aa.name = configName;
                    var playerData = aa.CreateGroup(PlayerDataGroupName, typeof(PlayerDataAssetGroupProcessor), false, true);
                    var resourceEntry = aa.CreateOrMoveEntry(AddressableAssetEntry.ResourcesName, playerData);
                    resourceEntry.isInResources = true;
                    aa.CreateOrMoveEntry(AddressableAssetEntry.EditorSceneListName, playerData);
                    aa.CreateGroup(DefaultLocalGroupName, typeof(BundledAssetGroupProcessor), true, false);

                    AssetDatabase.SaveAssets();
                    EditorBuildSettings.AddConfigObject(configName, aa, true);
                }
            }
            return aa;
        }

        public AddressableAssetGroup FindGroup(string name)
        {
            return groups.Find(s => s.Name == name);
        }

        /// <summary>
        /// TODO - doc
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
                        m_defaultGroup = g.Guid;
                        if (!g.IsProcessorType(typeof(PlayerDataAssetGroupProcessor)))
                            break;
                    }
                }
                return groups.Find(s => s.Guid == m_defaultGroup);
            }
        }

        private AddressableAssetEntry CreateEntry(string guid, string address, AddressableAssetGroup parent, bool readOnly, bool postEvent = true)
        {
            var entry = new AddressableAssetEntry(guid, address, parent, readOnly);
            if(!readOnly && postEvent)
                PostModificationEvent(ModificationEvent.EntryCreated, entry);
            return entry;
        }

        internal void PostModificationEvent(ModificationEvent e, object o)
        {
            if (e == ModificationEvent.ProfileRemoved && o as string == activeProfileId)
                activeProfileId = null;

            if (OnModification != null)
                OnModification(this, e, o);
            if(o is UnityEngine.Object)
                EditorUtility.SetDirty(o as UnityEngine.Object);
            EditorUtility.SetDirty(this); 
            m_cachedHash = default(Hash128);
        }

        /// <summary>
        /// TODO - doc
        /// </summary>
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

        /// <summary>
        /// TODO - doc
        /// </summary>
        public void MoveAssetsFromResources(Dictionary<string, string> guidToNewPath, AddressableAssetGroup targetParent)
        {
            if (guidToNewPath == null)
                return;
            var entries = new List<AddressableAssetEntry>();
            AssetDatabase.StartAssetEditing();
            foreach (var item in guidToNewPath)
            {

                var dirInfo = new FileInfo(item.Value).Directory;
                if(!dirInfo.Exists)
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
                    Debug.LogError("Error moving asset: " + errorStr);
                }
                else
                {
                    AddressableAssetEntry e = FindAssetEntry(item.Key);
                    if (e != null)
                        e.isInResources = false;

                    var newEntry = CreateOrMoveEntry(item.Key, targetParent, false, false);
                    var index = oldPath.ToLower().LastIndexOf("resources/");
                    if(index >= 0)
                    {
                        var newAddress = Path.GetFileNameWithoutExtension(oldPath.Substring(index + 10));
                        if(!string.IsNullOrEmpty(newAddress))
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
        /// TODO - doc
        /// </summary>
        // create a new entry, or if one exists in a different group, move it into the new group
        public AddressableAssetEntry CreateOrMoveEntry(string guid, AddressableAssetGroup targetParent, bool readOnly = false, bool postEvent = true)
        {
            if (targetParent == null)
                return null;

            AddressableAssetEntry entry = FindAssetEntry(guid);
            if (entry != null) //move entry to where it should go...
            {
                entry.isSubAsset = false;
                entry.readOnly = readOnly;
                if (entry.parentGroup == targetParent)
                {
                    targetParent.AddAssetEntry(entry, postEvent); //in case this is a sub-asset, make sure parent knows about it now.
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

        /// <summary>
        /// TODO - doc
        /// </summary>
        // create a new entry, or if one exists in a different group, return null. do not tell parent group about new entry
        internal AddressableAssetEntry CreateSubEntryIfUnique(string guid, string address, AddressableAssetEntry parentEntry)
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

        public void ConvertGroup(AddressableAssetGroup group, Type processorType)
        {
            if (group == null)
                return;
            group.SetProcessorType(processorType);
            PostModificationEvent(ModificationEvent.GroupProcessorChanged, group);
        }

        /// <summary>
        /// TODO - doc
        /// </summary>
        public AddressableAssetGroup CreateGroup(string name, Type processorType, bool setAsDefaultGroup, bool readOnly, bool postEvent = true)
        {
            if (string.IsNullOrEmpty(name))
                name = processorType + " Group";
            string validName = FindUniqueGroupName(name);
            var g = new AddressableAssetGroup(this, validName, processorType, GUID.Generate().ToString(), readOnly);
            groups.Add(g);
            if (setAsDefaultGroup)
                m_defaultGroup = g.Guid;
            if(postEvent)
                PostModificationEvent(ModificationEvent.GroupAdded, g);
            return g;
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
                    Debug.LogError("Unable to create valid name for new Addressable Assets group.");
                    return name;
                }
                foundExisting = IsNotUniqueGroupName(validName);
                if(foundExisting)
                {
                    validName = name + index.ToString();
                    index++;
                }
            }

            return validName;
        }

        public bool IsNotUniqueGroupName(string name)
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
        /// TODO - doc
        /// </summary>
        public void RemoveGroup(AddressableAssetGroup g, bool postEvent = true)
        {
            groups.Remove(g);
            if(postEvent)
                PostModificationEvent(ModificationEvent.GroupRemoved, g);
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
