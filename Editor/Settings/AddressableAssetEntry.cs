using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;

namespace UnityEditor.AddressableAssets
{
    /// <summary>
    /// Contains data for an addressable asset entry.
    /// </summary>
    [Serializable]
    public class AddressableAssetEntry : ISerializationCallbackReceiver
    {
        internal const string EditorSceneListName = "EditorSceneList";
        internal const string EditorSceneListPath = "Scenes In Build";

        internal const string ResourcesName = "Resources";
        internal const string ResourcesPath = "*/Resources/";

        [SerializeField]
        string m_guid;
        [SerializeField]
        string m_address;
        [SerializeField]
        bool m_readOnly;

        [SerializeField]
        List<string> m_serializedLabels;
        HashSet<string> m_labels = new HashSet<string>();

        internal virtual bool HasSettings() { return false; }


        [NonSerialized]
        private AddressableAssetGroup m_parentGroup;
        /// <summary>
        /// The asset group that this entry belongs to.  An entry can only belong to a single group at a time.
        /// </summary>
        public AddressableAssetGroup parentGroup
        {
            get { return m_parentGroup; }
            set { m_parentGroup = value; }
        }
        
        /// <summary>
        /// The asset guid.
        /// </summary>
        public string guid { get { return m_guid; } }

        /// <summary>
        /// The address of the entry.  This is treated as the primary key in the ResourceManager system.
        /// </summary>
        public string address
        {
            get
            {
                return m_address;
            }
            set
            {
                SetAddress(value);
            }
        }

        /// <summary>
        /// Set the address of the entry.
        /// </summary>
        /// <param name="address">The address.</param>
        /// <param name="postEvent">Post modification event.</param>
        public void SetAddress(string address, bool postEvent = true)
        {
            if (m_address != address)
            {
                m_address = address;
                if (string.IsNullOrEmpty(m_address))
                    m_address = AssetPath;
                if (postEvent)
                    PostModificationEvent(AddressableAssetSettings.ModificationEvent.EntryModified, this);
            }
        }

        /// <summary>
        /// Read only state of the entry.
        /// </summary>
        public bool ReadOnly { get { return m_readOnly; } set { if (m_readOnly != value) { m_readOnly = value; PostModificationEvent(AddressableAssetSettings.ModificationEvent.EntryModified, this); } } }

        /// <summary>
        /// Is the asset in a resource folder.
        /// </summary>
        public bool IsInResources { get; set; }
        /// <summary>
        /// Is scene in scene list.
        /// </summary>
        public bool IsInSceneList { get; set; }
        /// <summary>
        /// Is a sub asset.  For example an asset in an addressable folder.
        /// </summary>
        public bool IsSubAsset { get; set; }
        /// <summary>
        /// TODO - doc
        /// </summary>
        bool m_checkedIsScene = false;
        bool m_isScene = false;
        /// <summary>
        /// Is this entry for a scene.
        /// </summary>
        public bool IsScene
        {
            get
            {
                if (!m_checkedIsScene)
                {
                    m_checkedIsScene = true;
                    m_isScene = AssetDatabase.GUIDToAssetPath(m_guid).EndsWith(".unity");
                }
                return m_isScene;
            }

        }
        /// <summary>
        /// The set of labels for this entry.  There is no inherent limit to the number of labels.
        /// </summary>
        public HashSet<string> labels { get { return m_labels; } }
        /// <summary>
        /// Set or unset a label on this entry.
        /// </summary>
        /// <param name="label">The label name.</param>
        /// <param name="val">The value to set.  Setting to true will add the label, false will remove it.</param>
        /// <param name="postEvent">Post modification event.</param>
        /// <returns></returns>
        public bool SetLabel(string label, bool val, bool postEvent = true)
        {
            if (val)
            {
                if (m_labels.Add(label))
                {
                    if (postEvent)
                        PostModificationEvent(AddressableAssetSettings.ModificationEvent.EntryModified, this);
                    return true;
                }
            }
            else
            {
                if (m_labels.Remove(label))
                {
                    if (postEvent)
                        PostModificationEvent(AddressableAssetSettings.ModificationEvent.EntryModified, this);
                    return true;
                }
            }
            return false;
        }

        internal AddressableAssetEntry(string guid, string address, AddressableAssetGroup parent, bool readOnly)
        {
            m_guid = guid;
            m_address = address;
            m_readOnly = readOnly;
            parentGroup = parent;
            IsInResources = false;
            IsInSceneList = false;
        }

        internal void SerializeForHash(BinaryFormatter formatter, Stream stream)
        {
            formatter.Serialize(stream, m_guid);
            formatter.Serialize(stream, m_address);
            formatter.Serialize(stream, m_readOnly);
            formatter.Serialize(stream, m_labels.Count);
            foreach (var t in m_labels)
                formatter.Serialize(stream, t);

            formatter.Serialize(stream, IsInResources);
            formatter.Serialize(stream, IsInSceneList);
            formatter.Serialize(stream, IsSubAsset);
        }


        internal void PostModificationEvent(AddressableAssetSettings.ModificationEvent e, object o)
        {
            if (parentGroup != null)
                parentGroup.PostModificationEvent(e, o);
        }

        static bool IsValidAsset(string p)
        {
            return !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(p));
        }

        /// <summary>
        /// The path of the asset.
        /// </summary>
        public string AssetPath
        {
            get
            {
                if (string.IsNullOrEmpty(m_guid))
                    return string.Empty;

                if (m_guid == EditorSceneListName)
                    return EditorSceneListPath;
                if (m_guid == ResourcesName)
                    return ResourcesPath;

                return AssetDatabase.GUIDToAssetPath(m_guid);
            }
        }

        /// <summary>
        /// The asset load path.  This is used to determine the internal id of resource locations.
        /// </summary>
        /// <param name="isBundled">True if the asset will be contained in an asset bundle.</param>
        /// <returns>Return the runtime path that should be used to load this entry.</returns>
        public string GetAssetLoadPath(bool isBundled)
        {
            if (!IsScene)
            {
                var path = AssetPath;
                int ri = path.ToLower().LastIndexOf("resources/");
                if (ri == 0 || (ri > 0 && path[ri - 1] == '/'))
                {
                    path = path.Substring(ri + "resources/".Length);
                    int i = path.LastIndexOf('.');
                    if (i > 0)
                        path = path.Substring(0, i);
                }
                return path;
            }
            else
            {
                if (isBundled)
                    return AssetPath;// Path.GetFileNameWithoutExtension(assetPath);
                var path = AssetPath;
                int i = path.LastIndexOf(".unity");
                if (i > 0)
                    path = path.Substring(0, i);
                i = path.ToLower().IndexOf("assets/");
                if (i == 0)
                    path = path.Substring("assets/".Length);
                return path;
            }

        }

        static string GetResourcesPath(string path)
        {
            path = path.Replace('\\', '/');
            int ri = path.ToLower().LastIndexOf("resources/");
            if (ri == 0 || (ri > 0 && path[ri - 1] == '/'))
                path = path.Substring(ri + "resources/".Length);
            int i = path.LastIndexOf('.');
            if (i > 0)
                path = path.Substring(0, i);
            return path;
        }

        internal void GatherAllAssets(List<AddressableAssetEntry> assets, bool includeSelf, bool recurseAll)
        {
            var settings = parentGroup.Settings;

            if (m_guid == EditorSceneListName)
            {
                foreach (var s in EditorBuildSettings.scenes)
                {
                    if (s.enabled)
                    {
                        var entry = settings.CreateSubEntryIfUnique(s.guid.ToString(), Path.GetFileNameWithoutExtension(s.path), this);
                        if (entry != null) //TODO - it's probably really bad if this is ever null. need some error detection
                        {
                            entry.IsInSceneList = true;
                            entry.m_labels = m_labels;
                            assets.Add(entry);
                        }
                    }
                }
            }
            else if (m_guid == ResourcesName)
            {
                foreach (var resourcesDir in Directory.GetDirectories("Assets", "Resources", SearchOption.AllDirectories))
                {
                    foreach (var file in Directory.GetFiles(resourcesDir, "*.*", recurseAll ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly))
                    {
                        if (IsValidAsset(file))
                        {
                            var g = AssetDatabase.AssetPathToGUID(file);
                            var addr = GetResourcesPath(file);
                            var entry = settings.CreateSubEntryIfUnique(g, addr, this);
                            if (entry != null) //TODO - it's probably really bad if this is ever null. need some error detection
                            {
                                entry.IsInResources = true;
                                entry.m_labels = m_labels;
                                assets.Add(entry);
                            }
                        }
                    }
                    if (!recurseAll)
                    {
                        foreach (var folder in Directory.GetDirectories(resourcesDir))
                        {
                            if (AssetDatabase.IsValidFolder(folder))
                            {
                                var entry = settings.CreateSubEntryIfUnique(AssetDatabase.AssetPathToGUID(folder), GetResourcesPath(folder), this);
                                if (entry != null) //TODO - it's probably really bad if this is ever null. need some error detection
                                {
                                    entry.IsInResources = true;
                                    entry.m_labels = m_labels;
                                    assets.Add(entry);
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                var path = AssetDatabase.GUIDToAssetPath(m_guid);
                if (string.IsNullOrEmpty(path))
                    return;

                if (AssetDatabase.IsValidFolder(path))
                {
                    foreach (var fi in Directory.GetFiles(path, "*.*", recurseAll ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly))
                    {
                        var file = fi.Replace('\\', '/');
                        if (IsValidAsset(file))
                        {
                            var entry = settings.CreateSubEntryIfUnique(AssetDatabase.AssetPathToGUID(file), address + GetRelativePath(file, path), this);
                            if (entry != null)
                            {
                                entry.IsInResources = IsInResources; //if this is a sub-folder of Resources, copy it on down
                                entry.m_labels = m_labels;
                                assets.Add(entry);
                            }
                        }
                    }
                    if (!recurseAll)
                    {
                        foreach (var fo in Directory.GetDirectories(path, "*.*", SearchOption.TopDirectoryOnly))
                        {
                            var folder = fo.Replace('\\', '/');
                            if (AssetDatabase.IsValidFolder(folder))
                            {
                                var entry = settings.CreateSubEntryIfUnique(AssetDatabase.AssetPathToGUID(folder), address + GetRelativePath(folder, path), this);
                                if (entry != null)
                                {
                                    entry.IsInResources = IsInResources; //if this is a sub-folder of Resources, copy it on down
                                    entry.m_labels = m_labels;
                                    assets.Add(entry);
                                }
                            }
                        }
                    }
                }
                else
                {
                    if (AssetDatabase.GetMainAssetTypeAtPath(path) == typeof(AddressableAssetEntryCollection))
                    {
                        var col = AssetDatabase.LoadAssetAtPath<AddressableAssetEntryCollection>(AssetPath);
                        if (col != null)
                        {
                            foreach (var e in col.Entries)
                            {
                                var entry = settings.CreateSubEntryIfUnique(e.guid, e.address, this);
                                if (entry != null)
                                {
                                    entry.IsInResources = e.IsInResources;
                                    foreach (var l in e.labels)
                                        entry.SetLabel(l, true, false);
                                    foreach (var l in m_labels)
                                        entry.SetLabel(l, true, false);
                                    assets.Add(entry);
                                }
                            }
                        }
                    }
                    else
                    {
                        if (includeSelf)
                            assets.Add(this);
                    }
                }
            }
        }

        private string GetRelativePath(string file, string path)
        {
            return file.Substring(path.Length);
        }

        /// <summary>
        /// Implementation of ISerializationCallbackReceiver.  Converts data to serializable form before serialization.
        /// </summary>
        public void OnBeforeSerialize()
        {
            m_serializedLabels = new List<string>();
            foreach (var t in m_labels)
                m_serializedLabels.Add(t);
        }

        /// <summary>
        /// Implementation of ISerializationCallbackReceiver.  Converts data from serializable form after deserialization.
        /// </summary>
        public void OnAfterDeserialize()
        {
            m_labels = new HashSet<string>();
            foreach (var s in m_serializedLabels)
                m_labels.Add(s);
            m_serializedLabels = null;
        }
    }
}
