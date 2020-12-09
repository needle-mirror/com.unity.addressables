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
        public Dictionary<object, List<AddressableAssetEntry>> m_keyToEntries;
        public Dictionary<CacheKey, IList<IResourceLocation>> m_Cache;
        public AddressableAssetTree m_AddressableAssetTree;
        HashSet<object> m_Keys = null;
        AddressableAssetSettings m_Settings;
        
        public IEnumerable<object> Keys
        {
            get
            {
                
                if (m_Keys == null)
                {
                    var visitedFolders = new HashSet<string>();
                    using (new AddressablesFileEnumerationScope(m_AddressableAssetTree))
                    {
                        m_Keys = new HashSet<object>();
                        foreach (var kvp in m_keyToEntries)
                        {
                            var hasNonFolder = false;
                            foreach (var e in kvp.Value)
                            {
                                if (AssetDatabase.IsValidFolder(e.AssetPath))
                                {
                                    if (!visitedFolders.Contains(e.AssetPath))
                                    {
                                        foreach (var f in EnumerateAddressableFolder(e.AssetPath, m_Settings, true))
                                        {
                                            m_Keys.Add(f.Replace(e.AssetPath, e.address));
                                            m_Keys.Add(AssetDatabase.AssetPathToGUID(f));
                                        }
                                        visitedFolders.Add(e.AssetPath);
                                    }
                                    foreach (var l in e.labels)
                                        m_Keys.Add(l);
                                }
                                else
                                {
                                    hasNonFolder = true;
                                }
                            }
                            if (hasNonFolder)
                                m_Keys.Add(kvp.Key);
                        }
                        if (m_includeResourcesFolders)
                        {
                            var resourcesEntry = m_Settings.FindAssetEntry(AddressableAssetEntry.ResourcesName);
                            resourcesEntry.GatherResourcesEntries(null, true, entry => 
                            {
                                m_Keys.Add(entry.address);
                                m_Keys.Add(entry.guid);
                                return false;
                            });
                        }
                    }
                }
                return m_Keys;
            }
        }
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
            m_Settings = settings;
            LocatorId = m_Settings.name;
            m_AddressableAssetTree = BuildAddressableTree(m_Settings);
            m_Cache = new Dictionary<CacheKey, IList<IResourceLocation>>();
            m_keyToEntries = new Dictionary<object, List<AddressableAssetEntry>>(m_Settings.labelTable.labelNames.Count);
            using (new AddressablesFileEnumerationScope(m_AddressableAssetTree))
            {
                foreach (AddressableAssetGroup g in m_Settings.groups)
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
                    if (e.IsScene)
                    {
                        if(type == null || type == typeof(object) || type == typeof(SceneInstance) || AddressableAssetUtility.MapEditorTypeToRuntimeType(e.MainAssetType, false) == type )
                            locations.Add(new ResourceLocationBase(e.address, e.AssetPath, typeof(SceneProvider).FullName, typeof(SceneInstance)));
                    }
                    else if (type == null || type.IsAssignableFrom(e.MainAssetType))
                    {
                        locations.Add(new ResourceLocationBase(e.address, e.AssetPath, typeof(AssetDatabaseProvider).FullName, e.MainAssetType));
                        return true;
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

                            return true;
                        }
                    }
                    return false;
                });
            }
        }

        public bool Locate(object key, Type type, out IList<IResourceLocation> locations)
        {
            CacheKey cacheKey = new CacheKey() { m_key = key, m_type = type };
            if (m_Cache.TryGetValue(cacheKey, out locations))
                return locations != null;

            locations = new List<IResourceLocation>();
            if (m_keyToEntries.TryGetValue(key, out List<AddressableAssetEntry> entries))
            {
                foreach (AddressableAssetEntry e in entries)
                {
                    if (AssetDatabase.IsValidFolder(e.AssetPath) && !e.labels.Contains(key as string))
                        continue;

                    if (type == null)
                    {
                        if (e.MainAssetType != typeof(SceneAsset))
                        {
                            ObjectIdentifier[] ids =
                                ContentBuildInterface.GetPlayerObjectIdentifiersInAsset(new GUID(e.guid),
                                    EditorUserBuildSettings.activeBuildTarget);
                            IEnumerable<Type> subObjectTypes = AddressableAssetEntry.GatherSubObjectTypes(ids, e.guid);

                            if (subObjectTypes.Any())
                            {
                                foreach (Type t in subObjectTypes)
                                    GatherEntryLocations(e, t, locations, m_AddressableAssetTree);
                            }
                            else
                            {
                                GatherEntryLocations(e, null, locations, m_AddressableAssetTree);
                            }
                        }
                        else
                        {
                            GatherEntryLocations(e, null, locations, m_AddressableAssetTree);
                        }
                    }
                    else
                    {
                        GatherEntryLocations(e, type, locations, m_AddressableAssetTree);
                    }
                }
            }

            string keyStr = key as string;
            if (!string.IsNullOrEmpty(keyStr))
            {
                //check if the key is a guid first
                var keyPath = AssetDatabase.GUIDToAssetPath(keyStr);
                if (!string.IsNullOrEmpty(keyPath))
                {
                    //only look for folders from GUID if no locations have been found
                    if (locations.Count == 0)
                    {
                        var slash = keyPath.LastIndexOf('/');
                        while (slash > 0)
                        {
                            keyPath = keyPath.Substring(0, slash);
                            var parentFolderKey = AssetDatabase.AssetPathToGUID(keyPath);
                            if (string.IsNullOrEmpty(parentFolderKey))
                                break;

                            if (m_keyToEntries.ContainsKey(parentFolderKey))
                            {
                                locations.Add(new ResourceLocationBase(keyPath, AssetDatabase.GUIDToAssetPath(keyStr), typeof(AssetDatabaseProvider).FullName, type));
                                break;
                            }
                            slash = keyPath.LastIndexOf('/');
                        }
                    }
                }
                else
                {
                    //if the key is not a GUID, see if it is contained in a folder entry
                    keyPath = keyStr;
                    int slash = keyPath.LastIndexOf('/');
                    while (slash > 0)
                    {
                        keyPath = keyPath.Substring(0, slash);
                        if (m_keyToEntries.TryGetValue(keyPath, out var entry))
                        {
                            var internalId = GetInternalIdFromFolderEntry(keyStr, entry[0]);
                            if (!string.IsNullOrEmpty(internalId) && !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(internalId)))
                                locations.Add(new ResourceLocationBase(keyStr, internalId, typeof(AssetDatabaseProvider).FullName, type));
                            break;
                        }
                        slash = keyPath.LastIndexOf('/');
                    }
                }
            }

            //check resources folders
            if (m_includeResourcesFolders)
            {
                UnityEngine.Object obj = Resources.Load(keyStr, type == null ? typeof(UnityEngine.Object) : type);
                if (obj != null)
                    locations.Add(new ResourceLocationBase(keyStr, keyStr, typeof(LegacyResourcesProvider).FullName, type));
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

        string GetInternalIdFromFolderEntry(string keyStr, AddressableAssetEntry entry)
        {
            var entryPath = entry.AssetPath;
            if(keyStr.StartsWith(entry.address + "/"))
                return keyStr.Replace(entry.address, entryPath);
            foreach(var l in entry.labels)
                if (keyStr.StartsWith(l + "/"))
                    return keyStr.Replace(l, entryPath);
            return string.Empty;
        }
    }
}
