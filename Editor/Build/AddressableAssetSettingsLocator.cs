using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Build.Content;
using UnityEngine;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using static UnityEditor.AddressableAssets.Settings.AddressablesFileEnumeration;

namespace UnityEditor.AddressableAssets.Settings
{
    internal class AddressableAssetSettingsLocator : IResourceLocator
    {
        public string LocatorId { get; private set; }
        public IEnumerable<object> Keys => m_keyToEntries.Keys;
        public Dictionary<object, List<AddressableAssetEntry>> m_keyToEntries;
        public Dictionary<CacheKey, IList<IResourceLocation>> m_Cache;
        public AddressableAssetTree m_AddressableAssetTree;
        public struct CacheKey : IEquatable<CacheKey>
        {
            public object m_key;
            public Type m_type;
            public bool Equals(CacheKey other)
            {
                if (!m_key.Equals(other.m_key))
                    return false;
                return m_type == other.m_type;
            }

            public override int GetHashCode() => m_key.GetHashCode() * 31 + (m_type == null ? 0 : m_type.GetHashCode());
        }

        bool m_includeResourcesFolders = false;
        public AddressableAssetSettingsLocator(AddressableAssetSettings settings)
        {
            m_AddressableAssetTree = AddressablesFileEnumeration.BuildAddressableTree(settings);
            LocatorId = settings.name;
            m_Cache = new Dictionary<CacheKey, IList<IResourceLocation>>();
            m_keyToEntries = new Dictionary<object, List<AddressableAssetEntry>>(settings.labelTable.labelNames.Count);
            using (new AddressablesFileEnumerationScope(m_AddressableAssetTree))
            {
                foreach (AddressableAssetGroup g in settings.groups)
                {
                    foreach (AddressableAssetEntry e in g.entries)
                    {
                        if (e.guid == AddressableAssetEntry.EditorSceneListName)
                        {
                            if (e.parentGroup.GetSchema<GroupSchemas.PlayerDataGroupSchema>().IncludeBuildSettingsScenes)
                            {
                                e.GatherAllAssets(null, false, false, false, s =>
                                {
                                    AddEntriesToTables(m_keyToEntries, s);
                                    return false;
                                });
                            }
                        }
                        else if (e.guid == AddressableAssetEntry.ResourcesName)
                        {
                            m_includeResourcesFolders = e.parentGroup.GetSchema<GroupSchemas.PlayerDataGroupSchema>().IncludeResourcesFolders;
                        }
                        else
                        {
                            AddEntriesToTables(m_keyToEntries, e);
                        }
                    }
                }
            }
        }

        static void AddEntry(AddressableAssetEntry e, object k, Dictionary<object, List<AddressableAssetEntry>> keyToEntries)
        {
            if (!keyToEntries.TryGetValue(k, out List<AddressableAssetEntry> entries))
                keyToEntries.Add(k, entries = new List<AddressableAssetEntry>());
            entries.Add(e);
        }

        static void AddEntriesToTables(Dictionary<object, List<AddressableAssetEntry>> keyToEntries, AddressableAssetEntry e)
        {
            AddEntry(e, e.address, keyToEntries);
            AddEntry(e, e.guid, keyToEntries);
            if (e.IsScene && e.IsInSceneList)
            {
                int index = BuiltinSceneCache.GetSceneIndex(new GUID(e.guid));
                if (index != -1)
                    AddEntry(e, index, keyToEntries);
            }
            if (e.labels != null)
            {
                foreach (string l in e.labels)
                {
                    AddEntry(e, l, keyToEntries);
                }
            }
        }

        static void GatherEntryLocations(AddressableAssetEntry entry, Type type, IList<IResourceLocation> locations, AddressableAssetTree assetTree)
        {
            if (!string.IsNullOrEmpty(entry.address) && entry.address.Contains("[") && entry.address.Contains("]"))
            {
                Debug.LogErrorFormat("Address '{0}' cannot contain '[ ]'.", entry.address);
                return;
            }
            using (new AddressablesFileEnumerationScope(assetTree))
            {
                entry.GatherAllAssets(null, true, true, false, e =>
                {
                    if (type == null || type.IsAssignableFrom(e.MainAssetType) || (type == typeof(SceneInstance) && e.IsScene))
                    {
                        var locType = e.IsScene ? typeof(SceneProvider).FullName : typeof(AssetDatabaseProvider).FullName;
                        locations.Add(new ResourceLocationBase(e.address, e.AssetPath, locType, e.MainAssetType));
                    }
                    else
                    {
                        ObjectIdentifier[] ids = ContentBuildInterface.GetPlayerObjectIdentifiersInAsset(new GUID(e.guid), EditorUserBuildSettings.activeBuildTarget);
                        if (ids.Length > 1)
                        {
                            foreach (var t in AddressableAssetEntry.GatherSubObjectTypes(ids, e.guid))
                            {
                                if (type.IsAssignableFrom(t))
                                    locations.Add(new ResourceLocationBase(e.address, e.AssetPath, typeof(AssetDatabaseProvider).FullName, t));
                            }
                        }
                    }
                    return false;
                });
            }
        }

        public bool Locate(object key, Type type, out IList<IResourceLocation> locations)
        {
            return LocateInternal(key, type, out locations, false);
        }

        public bool LocateInternal(object key, Type type, out IList<IResourceLocation> locations, bool AllowFolders)
        {
            CacheKey cacheKey = new CacheKey() { m_key = key, m_type = type };
            if (m_Cache.TryGetValue(cacheKey, out locations))
                return locations != null;

            locations = new List<IResourceLocation>();
            if (m_keyToEntries.TryGetValue(key, out List<AddressableAssetEntry> entries))
            {
                foreach (AddressableAssetEntry e in entries)
                {
                    if (AllowFolders || !AssetDatabase.IsValidFolder(e.AssetPath) || e.labels.Contains(key as string))
                        GatherEntryLocations(e, type, locations, m_AddressableAssetTree);
                }
            }
            if (m_includeResourcesFolders)
            {
                string resPath = key as string;
                if (!string.IsNullOrEmpty(resPath))
                {
                    UnityEngine.Object obj = Resources.Load(resPath, type == null ? typeof(UnityEngine.Object) : type);
                    if (obj != null)
                        locations.Add(new ResourceLocationBase(resPath, resPath, typeof(LegacyResourcesProvider).FullName, type));
                }
            }

            string keyStr = key as string;
            if (!string.IsNullOrEmpty(keyStr))
            {
                int slash = keyStr.LastIndexOf('/');
                if (slash > 0)
                {
                    var parentFolderKey = keyStr.Substring(0, slash);
                    if (LocateInternal(parentFolderKey, type, out IList<IResourceLocation> folderLocs, true))
                    {
                        var keyStrWithSlash = keyStr + "/";
                        foreach (IResourceLocation l in folderLocs)
                            if (l.PrimaryKey == keyStr || l.PrimaryKey.StartsWith(keyStrWithSlash))
                                locations.Add(l);
                    }
                }
            }

            if (locations.Count == 0)
            {
                keyStr = AssetDatabase.GUIDToAssetPath(key as string);
                if (!string.IsNullOrEmpty(keyStr))
                {
                    int slash = keyStr.LastIndexOf('/');
                    while (slash > 0)
                    {
                        keyStr = keyStr.Substring(0, slash);
                        var parentFolderKey = AssetDatabase.AssetPathToGUID(keyStr);
                        if (!string.IsNullOrEmpty(parentFolderKey) && m_keyToEntries.ContainsKey(parentFolderKey))
                        {
                            locations.Add(new ResourceLocationBase(key as string, AssetDatabase.GUIDToAssetPath(key as string), typeof(AssetDatabaseProvider).FullName, type));
                            break;
                        }
                        slash = keyStr.LastIndexOf('/');
                    }
                }
            }

            if (locations.Count == 0)
            {
                locations = null;
                m_Cache.Add(cacheKey, locations);
                return false;
            }

            m_Cache.Add(cacheKey, locations);
            return true;
        }
    }
}
