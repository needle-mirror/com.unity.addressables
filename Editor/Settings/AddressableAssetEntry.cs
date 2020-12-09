using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEditor.Build.Content;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.ResourceManagement.Util;
using UnityEngine.Serialization;
using UnityEngine.U2D;

namespace UnityEditor.AddressableAssets.Settings
{
    interface IReferenceEntryData
    {
        string AssetPath { get; }
        string address { get; set; }
        bool IsInResources { get; set; }
    }

    internal struct ImplicitAssetEntry : IReferenceEntryData
    {
        public string AssetPath { get; set; }
        public string address { get; set; }
        public bool IsInResources { get; set; }
    }

    /// <summary>
    /// Contains data for an addressable asset entry.
    /// </summary>
    [Serializable]
    public class AddressableAssetEntry : ISerializationCallbackReceiver, IReferenceEntryData
    {
        internal const string EditorSceneListName = "EditorSceneList";
        internal const string EditorSceneListPath = "Scenes In Build";

        internal const string ResourcesName = "Resources";
        internal const string ResourcesPath = "*/Resources/";

        [FormerlySerializedAs("m_guid")]
        [SerializeField]
        string m_GUID;
        [FormerlySerializedAs("m_address")]
        [SerializeField]
        string m_Address;
        [FormerlySerializedAs("m_readOnly")]
        [SerializeField]
        bool m_ReadOnly;

        [FormerlySerializedAs("m_serializedLabels")]
        [SerializeField]
        List<string> m_SerializedLabels;
        HashSet<string> m_Labels = new HashSet<string>();

        internal virtual bool HasSettings() { return false; }

        [NonSerialized]
        AddressableAssetGroup m_ParentGroup;
        /// <summary>
        /// The asset group that this entry belongs to.  An entry can only belong to a single group at a time.
        /// </summary>
        public AddressableAssetGroup parentGroup
        {
            get { return m_ParentGroup; }
            set { m_ParentGroup = value; }
        }

        /// <summary>
        /// The id for the bundle file.
        /// </summary>
        public string BundleFileId
        {
            get;
            set;
        }

        /// <summary>
        /// The asset guid.
        /// </summary>
        public string guid { get { return m_GUID; } }

        /// <summary>
        /// The address of the entry.  This is treated as the primary key in the ResourceManager system.
        /// </summary>
        public string address
        {
            get
            {
                return m_Address;
            }
            set
            {
                SetAddress(value);
            }
        }

        /// <summary>
        /// Set the address of the entry.
        /// </summary>
        /// <param name="addr">The address.</param>
        /// <param name="postEvent">Post modification event.</param>
        public void SetAddress(string addr, bool postEvent = true)
        {
            if (m_Address != addr)
            {
                m_Address = addr;
                if (string.IsNullOrEmpty(m_Address))
                    m_Address = AssetPath;
                if (m_GUID.Length > 0 && m_Address.Contains("[") && m_Address.Contains("]"))
                    Debug.LogErrorFormat("Address '{0}' cannot contain '[ ]'.", m_Address);
                SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, this, postEvent);
            }
        }

        /// <summary>
        /// Read only state of the entry.
        /// </summary>
        public bool ReadOnly
        {
            get
            {
                return m_ReadOnly;
            }
            set
            {
                if (m_ReadOnly != value)
                {
                    m_ReadOnly = value;
                    SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, this, true);
                }
            }
        }

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
        /// Stores a reference to the parent entry. Only used if the asset is a sub asset.
        /// </summary>
        public AddressableAssetEntry ParentEntry { get; set; }
        bool m_CheckedIsScene;
        bool m_IsScene;
        /// <summary>
        /// Is this entry for a scene.
        /// </summary>
        public bool IsScene
        {
            get
            {
                if (!m_CheckedIsScene)
                {
                    m_CheckedIsScene = true;
                    m_IsScene = AssetPath.EndsWith(".unity");
                }
                return m_IsScene;
            }
        }
        /// <summary>
        /// The set of labels for this entry.  There is no inherent limit to the number of labels.
        /// </summary>
        public HashSet<string> labels { get { return m_Labels; } }

        internal Type m_cachedMainAssetType = null;
        internal Type MainAssetType
        {
            get
            {
                if (m_cachedMainAssetType == null)
                {
                    m_cachedMainAssetType = AssetDatabase.GetMainAssetTypeAtPath(AssetPath);
                    if (m_cachedMainAssetType == null)
                        return typeof(object); // do not cache a bad type lookup.
                }
                return m_cachedMainAssetType;
            }
        }

        /// <summary>
        /// Set or unset a label on this entry.
        /// </summary>
        /// <param name="label">The label name.</param>
        /// <param name="enable">Setting to true will add the label, false will remove it.</param>
        /// <param name="force">When enable is true, setting force to true will force the label to exist on the parent AddressableAssetSettings object if it does not already.</param>
        /// <param name="postEvent">Post modification event.</param>
        /// <returns></returns>
        public bool SetLabel(string label, bool enable, bool force = false, bool postEvent = true)
        {
            if (enable)
            {
                if (force)
                    parentGroup.Settings.AddLabel(label, postEvent);
                if (m_Labels.Add(label))
                {
                    SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, this, postEvent);
                    return true;
                }
            }
            else
            {
                if (m_Labels.Remove(label))
                {
                    SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, this, postEvent);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Creates a list of keys that can be used to load this entry.
        /// </summary>
        /// <returns>The list of keys.  This will contain the address, the guid as a Hash128 if valid, all assigned labels, and the scene index if applicable.</returns>
        public List<object> CreateKeyList() => CreateKeyList(true, true, true);

        /// <summary>
        /// Creates a list of keys that can be used to load this entry.
        /// </summary>
        /// <returns>The list of keys.  This will contain the address, the guid as a Hash128 if valid, all assigned labels, and the scene index if applicable.</returns>
        internal List<object> CreateKeyList(bool includeAddress, bool includeGUID, bool includeLabels)
        {
            var keys = new List<object>();
            //the address must be the first key
            if (includeAddress)
                keys.Add(address);
            if (includeGUID && !string.IsNullOrEmpty(guid))
                keys.Add(guid);
            if (IsScene && IsInSceneList)
            {
                int index = BuiltinSceneCache.GetSceneIndex(new GUID(guid));
                if (index != -1)
                    keys.Add(index);
            }

            if (includeLabels && labels != null && labels.Count > 0)
            {
                var labelsToRemove = new HashSet<string>();
                var currentLabels = parentGroup.Settings.GetLabels();
                foreach (var l in labels)
                {
                    if (currentLabels.Contains(l))
                        keys.Add(l);
                    else
                        labelsToRemove.Add(l);
                }

                foreach (var l in labelsToRemove)
                    labels.Remove(l);
            }
            return keys;
        }

        internal AddressableAssetEntry(string guid, string address, AddressableAssetGroup parent, bool readOnly)
        {
            if (guid.Length > 0 && address.Contains("[") && address.Contains("]"))
                Debug.LogErrorFormat("Address '{0}' cannot contain '[ ]'.", address);
            m_GUID = guid;
            m_Address = address;
            m_ReadOnly = readOnly;
            parentGroup = parent;
            IsInResources = false;
            IsInSceneList = false;
        }

        internal void SerializeForHash(BinaryFormatter formatter, Stream stream)
        {
            formatter.Serialize(stream, m_GUID);
            formatter.Serialize(stream, m_Address);
            formatter.Serialize(stream, m_ReadOnly);
            formatter.Serialize(stream, m_Labels.Count);

            foreach (var t in m_Labels)
                formatter.Serialize(stream, t);

            formatter.Serialize(stream, IsInResources);
            formatter.Serialize(stream, IsInSceneList);
            formatter.Serialize(stream, IsSubAsset);
        }

        internal void SetDirty(AddressableAssetSettings.ModificationEvent e, object o, bool postEvent)
        {
            if (parentGroup != null)
                parentGroup.SetDirty(e, o, postEvent, true);
        }

        internal void SetCachedPath(string newCachedPath)
        {
            if (newCachedPath != m_cachedAssetPath)
            {
                m_cachedAssetPath = newCachedPath;
                m_MainAsset = null;
                m_TargetAsset = null;
            }
        }

        internal void SetSubObjectType(Type type)
        {
            m_cachedMainAssetType = type;
        }

        internal string m_cachedAssetPath = null;
        /// <summary>
        /// The path of the asset.
        /// </summary>
        public string AssetPath
        {
            get
            {
                if (string.IsNullOrEmpty(m_cachedAssetPath))
                {
                    if (string.IsNullOrEmpty(guid))
                        SetCachedPath(string.Empty);
                    else if (guid == EditorSceneListName)
                        SetCachedPath(EditorSceneListPath);
                    else if (guid == ResourcesName)
                        SetCachedPath(ResourcesPath);
                    else
                        SetCachedPath(AssetDatabase.GUIDToAssetPath(guid));
                }
                return m_cachedAssetPath;
            }
        }

        [SerializeField]
        private UnityEngine.Object m_MainAsset;
        /// <summary>
        /// The main asset object for this entry.
        /// </summary>
        public UnityEngine.Object MainAsset
        {
            get
            {
                if (m_MainAsset == null)
                {
                    AddressableAssetEntry e = this;
                    while (string.IsNullOrEmpty(e.AssetPath))
                    {
                        if (e.ParentEntry == null)
                            return null;
                        e = e.ParentEntry;
                    }

                    m_MainAsset = AssetDatabase.LoadMainAssetAtPath(e.AssetPath);
                }

                return m_MainAsset;
            }
        }

        [SerializeField]
        private UnityEngine.Object m_TargetAsset;

        /// <summary>
        /// The asset object for this entry.
        /// </summary>
        public UnityEngine.Object TargetAsset
        {
            get
            {
                if (m_TargetAsset == null)
                {
                    if (!string.IsNullOrEmpty(AssetPath) || !IsSubAsset)
                    {
                        m_TargetAsset = MainAsset;
                        return m_TargetAsset;
                    }

                    if (ParentEntry == null || !string.IsNullOrEmpty(AssetPath) || string.IsNullOrEmpty(ParentEntry.AssetPath))
                        return null;

                    var mainAsset = ParentEntry.MainAsset;
                    if (ResourceManagerConfig.ExtractKeyAndSubKey(address, out string mainKey, out string subObjectName))
                    {
                        if (mainAsset != null && mainAsset.GetType() == typeof(SpriteAtlas))
                        {
                            m_TargetAsset = (mainAsset as SpriteAtlas).GetSprite(subObjectName);
                            return m_TargetAsset;
                        }

                        var subObjects = AssetDatabase.LoadAllAssetRepresentationsAtPath(ParentEntry.AssetPath);
                        foreach (var s in subObjects)
                        {
                            if (s != null && s.name == subObjectName)
                            {
                                m_TargetAsset = s;
                                break;
                            }
                        }
                    }
                }
                return m_TargetAsset;
            }
        }
        /// <summary>
        /// The asset load path.  This is used to determine the internal id of resource locations.
        /// </summary>
        /// <param name="isBundled">True if the asset will be contained in an asset bundle.</param>
        /// <returns>Return the runtime path that should be used to load this entry.</returns>
        public string GetAssetLoadPath(bool isBundled)
        {
            return GetAssetLoadPath(isBundled, null);
        }

        /// <summary>
        /// The asset load path.  This is used to determine the internal id of resource locations.
        /// </summary>
        /// <param name="isBundled">True if the asset will be contained in an asset bundle.</param>
        /// <returns>Return the runtime path that should be used to load this entry.</returns>
        internal string GetAssetLoadPath(bool isBundled, HashSet<string> otherLoadPaths)
        {
            if (!IsScene)
            {
                if (IsInResources)
                {
                    return GetResourcesPath(AssetPath);
                }
                else
                {
                    if (isBundled)
                        return parentGroup.GetSchema<GroupSchemas.BundledAssetGroupSchema>().GetAssetLoadPath(AssetPath, otherLoadPaths, p => guid);
                    return AssetPath;
                }
            }
            else
            {
                if (isBundled)
                    return parentGroup.GetSchema<GroupSchemas.BundledAssetGroupSchema>().GetAssetLoadPath(AssetPath, otherLoadPaths, p => guid);
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
            int ri = path.ToLower().LastIndexOf("/resources/");
            if (ri >= 0)
                path = path.Substring(ri + "/resources/".Length);
            int i = path.LastIndexOf('.');
            if (i > 0)
                path = path.Substring(0, i);
            return path;
        }

        /// <summary>
        /// Gathers all asset entries.  Each explicit entry may contain multiple sub entries. For example, addressable folders create entries for each asset contained within.
        /// </summary>
        /// <param name="assets">The generated list of entries.  For simple entries, this will contain just the entry itself if specified.</param>
        /// <param name="includeSelf">Determines if the entry should be contained in the result list or just sub entries.</param>
        /// <param name="recurseAll">Determines if full recursion should be done when gathering entries.</param>
        /// <param name="includeSubObjects">Determines if sub objects such as sprites should be included.</param>
        /// <param name="entryFilter">Optional predicate to run against each entry, only returning those that pass.  A null filter will return all entries</param>
        public void GatherAllAssets(List<AddressableAssetEntry> assets, bool includeSelf, bool recurseAll, bool includeSubObjects, Func<AddressableAssetEntry, bool> entryFilter = null)
        {
            if (assets == null)
                assets = new List<AddressableAssetEntry>();

            if (guid == EditorSceneListName)
            {
                GatherEditorSceneEntries(assets, entryFilter);
            }
            else if (guid == ResourcesName)
            {
                GatherResourcesEntries(assets, recurseAll, entryFilter);
            }
            else
            {
                if (string.IsNullOrEmpty(AssetPath))
                    return;

                if (AssetDatabase.IsValidFolder(AssetPath))
                {
                    GatherFolderEntries(assets, recurseAll, entryFilter);
                }
                else
                {
                    if (MainAssetType == typeof(AddressableAssetEntryCollection))
                    {
                        GatherAssetEntryCollectionEntries(assets, entryFilter);
                    }
                    else
                    {
                        if (includeSelf)
                            if (entryFilter == null || entryFilter(this))
                                assets.Add(this);
                        if (includeSubObjects)
                        {
                            var mainType = AssetDatabase.GetMainAssetTypeAtPath(AssetPath);
                            if (mainType == typeof(SpriteAtlas))
                            {
                                GatherSpriteAtlasEntries(assets, AssetPath);
                            }
                            else
                            {
                                GatherSubObjectEntries(assets, AssetPath);
                            }
                        }
                    }
                }
            }
        }

        void GatherSubObjectEntries(List<AddressableAssetEntry> assets, string path)
        {
            var objs = AssetDatabase.LoadAllAssetRepresentationsAtPath(AssetPath);
            for (int i = 0; i < objs.Length; i++)
            {
                var o = objs[i];
                var namedAddress = string.Format("{0}[{1}]", address, o == null ? "missing reference" : o.name);
                if (o == null)
                {
                    if (string.IsNullOrEmpty(AssetPath) && IsSubAsset)
                        path = ParentEntry.AssetPath;
                    Debug.LogWarning(string.Format("NullReference in entry {0}\nAssetPath: {1}\nAddressableAssetGroup: {2}", address, path, parentGroup.Name));
                    assets.Add(new AddressableAssetEntry("", namedAddress, parentGroup, true));
                }
                else
                {
                    var newEntry = parentGroup.Settings.CreateEntry("", namedAddress, parentGroup, true);
                    newEntry.IsSubAsset = true;
                    newEntry.ParentEntry = this;
                    newEntry.IsInResources = IsInResources;
                    newEntry.SetSubObjectType(o.GetType());
                    assets.Add(newEntry);
                }
            }
        }

        void GatherSpriteAtlasEntries(List<AddressableAssetEntry> assets, string path)
        {
            var settings = parentGroup.Settings;
            var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(AssetPath);
            var sprites = new Sprite[atlas.spriteCount];
            atlas.GetSprites(sprites);

            for (int i = 0; i < atlas.spriteCount; i++)
            {
                var spriteName = sprites[i] == null ? "missing reference" : sprites[i].name;
                if (sprites[i] == null)
                {
                    if (string.IsNullOrEmpty(AssetPath) && IsSubAsset)
                        path = ParentEntry.AssetPath;
                    Debug.LogWarning(string.Format("NullReference in entry {0}\nAssetPath: {1}\nAddressableAssetGroup: {2}", address, path, parentGroup.Name));
                    assets.Add(new AddressableAssetEntry("", spriteName, parentGroup, true));
                }
                else
                {
                    if (spriteName.EndsWith("(Clone)"))
                        spriteName = spriteName.Replace("(Clone)", "");

                    var namedAddress = string.Format("{0}[{1}]", address, spriteName);
                    var newEntry = settings.CreateEntry("", namedAddress, parentGroup, true);
                    newEntry.IsSubAsset = true;
                    newEntry.ParentEntry = this;
                    newEntry.IsInResources = IsInResources;
                    assets.Add(newEntry);
                }
            }
        }

        void GatherAssetEntryCollectionEntries(List<AddressableAssetEntry> assets, Func<AddressableAssetEntry, bool> entryFilter)
        {
            var settings = parentGroup.Settings;
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
                        foreach (var l in m_Labels)
                            entry.SetLabel(l, true, false);
                        if (entryFilter == null || entryFilter(entry))
                            assets.Add(entry);
                    }
                }
            }
        }

        void GatherFolderEntries(List<AddressableAssetEntry> assets, bool recurseAll, Func<AddressableAssetEntry, bool> entryFilter)
        {
            var path = AssetPath;
            var settings = parentGroup.Settings;
            foreach (var file in AddressablesFileEnumeration.EnumerateAddressableFolder(path, settings, recurseAll))
            {
                var subGuid = AssetDatabase.AssetPathToGUID(file);
                var entry = settings.CreateSubEntryIfUnique(subGuid, address + GetRelativePath(file, path), this);

                if (entry != null)
                {
                    entry.IsInResources =
                        IsInResources; //if this is a sub-folder of Resources, copy it on down
                    entry.m_Labels = m_Labels;
                    if (entryFilter == null || entryFilter(entry))
                        assets.Add(entry);
                }
            }

            if (!recurseAll)
            {
                foreach (var fo in Directory.EnumerateDirectories(path, "*.*", SearchOption.TopDirectoryOnly))
                {
                    var folder = fo.Replace('\\', '/');
                    if (AssetDatabase.IsValidFolder(folder))
                    {
                        var entry = settings.CreateSubEntryIfUnique(AssetDatabase.AssetPathToGUID(folder), address + GetRelativePath(folder, path), this);
                        if (entry != null)
                        {
                            entry.IsInResources = IsInResources; //if this is a sub-folder of Resources, copy it on down
                            entry.m_Labels = m_Labels;
                            if (entryFilter == null || entryFilter(entry))
                                assets.Add(entry);
                        }
                    }
                }
            }
        }

        internal void GatherResourcesEntries(List<AddressableAssetEntry> assets, bool recurseAll, Func<AddressableAssetEntry, bool> entryFilter)
        {
            var settings = parentGroup.Settings;
            var pd = parentGroup.GetSchema<GroupSchemas.PlayerDataGroupSchema>();
            if (pd.IncludeResourcesFolders)
            {
                foreach (var resourcesDir in GetResourceDirectories())
                {
                    foreach (var file in Directory.GetFiles(resourcesDir, "*.*", recurseAll ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly))
                    {
                        if (AddressableAssetUtility.IsPathValidForEntry(file))
                        {
                            var g = AssetDatabase.AssetPathToGUID(file);
                            var addr = GetResourcesPath(file);
                            var entry = settings.CreateSubEntryIfUnique(g, addr, this);

                            if (entry != null) //TODO - it's probably really bad if this is ever null. need some error detection
                            {
                                entry.IsInResources = true;
                                entry.m_Labels = m_Labels;
                                if (entryFilter == null || entryFilter(entry))
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
                                    entry.m_Labels = m_Labels;
                                    if (entryFilter == null || entryFilter(entry))
                                        assets.Add(entry);
                                }
                            }
                        }
                    }
                }
            }
        }

        static IEnumerable<string> GetResourceDirectories()
        {
            foreach (string path in GetResourceDirectoriesatPath("Assets"))
            {
                yield return path;
            }

            List<PackageManager.PackageInfo> packages = AddressableAssetUtility.GetPackages();
            foreach (PackageManager.PackageInfo package in packages)
            {
                foreach (string path in GetResourceDirectoriesatPath(package.assetPath))
                {
                    yield return path;
                }
            }
        }

        static IEnumerable<string> GetResourceDirectoriesatPath(string rootPath)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                foreach (string dir in Directory.EnumerateDirectories(rootPath, "*", SearchOption.AllDirectories))
                {
                    if (dir.EndsWith("/resources", StringComparison.OrdinalIgnoreCase))
                        yield return dir;
                }
            }
            else
            {
                foreach (string dir in Directory.EnumerateDirectories(rootPath, "Resources", SearchOption.AllDirectories))
                    yield return dir;
            }
        }

        void GatherEditorSceneEntries(List<AddressableAssetEntry> assets, Func<AddressableAssetEntry, bool> entryFilter)
        {
            var settings = parentGroup.Settings;
            var pd = parentGroup.GetSchema<GroupSchemas.PlayerDataGroupSchema>();
            if (pd.IncludeBuildSettingsScenes)
            {
                foreach (var s in BuiltinSceneCache.scenes)
                {
                    if (s.enabled)
                    {
                        var entry = settings.CreateSubEntryIfUnique(s.guid.ToString(), Path.GetFileNameWithoutExtension(s.path), this);
                        if (entry != null) //TODO - it's probably really bad if this is ever null. need some error detection
                        {
                            entry.IsInSceneList = true;
                            entry.m_Labels = m_Labels;
                            if (entryFilter == null || entryFilter(entry))
                                assets.Add(entry);
                        }
                    }
                }
            }
        }

        internal void GatherAllAssetReferenceDrawableEntries(List<IReferenceEntryData> refEntries, AddressableAssetSettings settings)
        {
            var path = AssetPath;
            if (string.IsNullOrEmpty(path))
                return;

            if (guid == EditorSceneListName)
            {
                //We don't add Built in scenes to the list of assignable AssetReferences so no need to check this.
                return;
            }
            if (guid == ResourcesName)
            {
                //We don't add Resources assets to the list of assignable AssetReferences so no need to check this.
                return;
            }
            if (AssetDatabase.IsValidFolder(path))
            {
                foreach (var fi in AddressablesFileEnumeration.EnumerateAddressableFolder(path, settings, true))
                {
                    string relativeAddress = address + GetRelativePath(fi, path);
                    var reference = new ImplicitAssetEntry()
                    {
                        address = relativeAddress,
                        AssetPath = fi,
                        IsInResources = IsInResources
                    };

                    refEntries.Add(reference);
                }
            }
            else if (MainAssetType == typeof(AddressableAssetEntryCollection))
            {
                var col = AssetDatabase.LoadAssetAtPath<AddressableAssetEntryCollection>(AssetPath);
                if (col != null)
                {
                    foreach (var e in col.Entries)
                        refEntries.Add(e);
                }
            }
            else
            {
                refEntries.Add(this);
            }
        }

        string GetRelativePath(string file, string path)
        {
            return file.Substring(path.Length);
        }

        /// <summary>
        /// Implementation of ISerializationCallbackReceiver.  Converts data to serializable form before serialization.
        /// </summary>
        public void OnBeforeSerialize()
        {
            m_SerializedLabels = new List<string>();
            foreach (var t in m_Labels)
                m_SerializedLabels.Add(t);
        }

        /// <summary>
        /// Implementation of ISerializationCallbackReceiver.  Converts data from serializable form after deserialization.
        /// </summary>
        public void OnAfterDeserialize()
        {
            m_Labels = new HashSet<string>();
            foreach (var s in m_SerializedLabels)
                m_Labels.Add(s);
            m_SerializedLabels = null;
            m_cachedMainAssetType = null;
        }

        /// <summary>
        /// Returns the address of the AddressableAssetEntry.
        /// </summary>
        /// <returns>The address of the AddressableAssetEntry</returns>
        public override string ToString()
        {
            return m_Address;
        }

        /// <summary>
        /// Create all entries for this addressable asset.  This will expand subassets (Sprites, Meshes, etc) and also different representations.
        /// </summary>
        /// <param name="entries">The list of entries to fill in.</param>
        /// <param name="isBundled">Whether the entry is bundles or not.  This will affect the load path.</param>
        /// <param name="providerType">The provider type for the main entry.</param>
        /// <param name="dependencies">Keys of dependencies</param>
        /// <param name="extraData">Extra data to append to catalog entries.</param>
        /// <param name="providerTypes">Any unknown provider types are added to this set in order to ensure they are not stripped.</param>
        public void CreateCatalogEntries(List<ContentCatalogDataEntry> entries, bool isBundled, string providerType, IEnumerable<object> dependencies, object extraData, HashSet<Type> providerTypes)
        {
            CreateCatalogEntriesInternal(entries, isBundled, providerType, dependencies, extraData, null, providerTypes, true, true, true, null);
        }

        internal void CreateCatalogEntriesInternal(List<ContentCatalogDataEntry> entries, bool isBundled, string providerType, IEnumerable<object> dependencies, object extraData, Dictionary<GUID, AssetLoadInfo> depInfo, HashSet<Type> providerTypes, bool includeAddress, bool includeGUID, bool includeLabels, HashSet<string> assetsInBundle)
        {
            if (string.IsNullOrEmpty(AssetPath))
                return;

            string assetPath = GetAssetLoadPath(isBundled, assetsInBundle);
            List<object> keyList = CreateKeyList(includeAddress, includeGUID, includeLabels);
            if (keyList.Count == 0)
                return;

            Type mainType = AddressableAssetUtility.MapEditorTypeToRuntimeType(MainAssetType, false);
            if (mainType == null && !IsInResources)
            {
                var t = MainAssetType;
                Debug.LogWarningFormat("Type {0} is in editor assembly {1}.  Asset location with internal id {2} will be stripped.", t.Name, t.Assembly.FullName, assetPath);
                return;
            }

            Type runtimeProvider = GetRuntimeProviderType(providerType, mainType);
            if (runtimeProvider != null)
                providerTypes.Add(runtimeProvider);

            if (!IsScene)
            {
                ObjectIdentifier[] ids = depInfo != null ? depInfo[new GUID(guid)].includedObjects.ToArray() :
                    ContentBuildInterface.GetPlayerObjectIdentifiersInAsset(new GUID(guid), EditorUserBuildSettings.activeBuildTarget);
                foreach (var t in GatherSubObjectTypes(ids, guid))
                    entries.Add(new ContentCatalogDataEntry(t, assetPath, providerType, keyList, dependencies, extraData));
            }
            else if (mainType != null)
            {
                entries.Add(new ContentCatalogDataEntry(mainType, assetPath, providerType, keyList, dependencies, extraData));
            }
        }

        static internal IEnumerable<Type> GatherSubObjectTypes(ObjectIdentifier[] ids, string guid)
        {
            if (ids.Length > 0)
            {
                Type[] typesForObjs = ContentBuildInterface.GetTypeForObjects(ids);
                HashSet<Type> typesSeen = new HashSet<Type>();
                foreach (var objType in typesForObjs)
                {
                    if (typeof(Component).IsAssignableFrom(objType))
                        continue;
                    Type rtType = AddressableAssetUtility.MapEditorTypeToRuntimeType(objType, false);
                    if (rtType != null && !typesSeen.Contains(rtType))
                    {
                        yield return rtType;
                        typesSeen.Add(rtType);
                    }
                }
            }
        }

        internal Type GetRuntimeProviderType(string providerType, Type mainEntryType)
        {
            if (string.IsNullOrEmpty(providerType))
                return null;

            if (mainEntryType == typeof(SpriteAtlas))
                return typeof(AtlasSpriteProvider);
            return Assembly.GetAssembly(typeof(ResourceProviderBase)).GetType(providerType);
        }
    }
}
