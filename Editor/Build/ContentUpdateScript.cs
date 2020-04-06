using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using UnityEditor.AddressableAssets.Build.BuildPipelineTasks;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace UnityEditor.AddressableAssets.Build
{
    /// <summary>
    /// The given state of an Asset.  Represented by its guid and hash.
    /// </summary>
    [Serializable]
    public struct AssetState : IEquatable<AssetState>
    {
        /// <summary>
        /// Asset states GUID.
        /// </summary>
        public GUID guid;

        /// <summary>
        /// Asset State hash.
        /// </summary>
        public Hash128 hash;

        /// <summary>
        /// Check if one asset state is equal to another.
        /// </summary>
        /// <param name="other">Right hand side of comparision.</param>
        /// <returns>Returns true if the Asset States are equal to one another.</returns>
        public bool Equals(AssetState other)
        {
            return guid == other.guid && hash == other.hash;
        }
    }

    /// <summary>
    /// The Cached Asset State of an Addressable Asset.
    /// </summary>
    [Serializable]
    public class CachedAssetState : IEquatable<CachedAssetState>
    {
        /// <summary>
        /// The Asset State.
        /// </summary>
        public AssetState asset;

        /// <summary>
        /// The Asset State of all dependencies.
        /// </summary>
        public AssetState[] dependencies;

        /// <summary>
        /// The guid for the group the cached asset state belongs to.
        /// </summary>
        public string groupGuid;

        /// <summary>
        /// The name of the cached asset states bundle file.
        /// </summary>
        public string bundleFileId;

        /// <summary>
        /// The cached asset state data.
        /// </summary>
        public object data;

        /// <summary>
        /// Checks if one cached asset state is equal to another given the asset state and dependency state.
        /// </summary>
        /// <param name="other">Right hand side of comparision.</param>
        /// <returns>Returns true if the cached asset states are equal to one another.</returns>
        public bool Equals(CachedAssetState other)
        {
            bool result = other != null && asset.Equals(other.asset);
            result &= dependencies != null && other.dependencies != null;
            result &= dependencies.Length == other.dependencies.Length;
            var index = 0;
            while (result && index < dependencies.Length)
            {
                result &= dependencies[index].Equals(other.dependencies[index]);
                index++;
            }
            return result;
        }
    }


    /// <summary>
    /// Data stored with each build that is used to generated content updates.
    /// </summary>
    [Serializable]
    public class AddressablesContentState
    {
        /// <summary>
        /// The version that the player was built with.  This is usually set to AddressableAssetSettings.PlayerBuildVersion.
        /// </summary>
        [SerializeField]
        public string playerVersion;

        /// <summary>
        /// The version of the unity editor used to build the player.
        /// </summary>
        [SerializeField]
        public string editorVersion;

        /// <summary>
        /// Dependency information for all assets in the build that have been marked StaticContent.
        /// </summary>
        [SerializeField]
        public CachedAssetState[] cachedInfos;

        /// <summary>
        /// The path of a remote catalog.  This is the only place the player knows to look for an updated catalog.
        /// </summary>
        [SerializeField]
        public string remoteCatalogLoadPath;
    }

    /// <summary>
    /// Contains methods used for the content update workflow.
    /// </summary>
    public static class ContentUpdateScript
    {
        internal struct ContentUpdateContext
        {
            public Dictionary<string, CachedAssetState> GuidToPreviousAssetStateMap;
            public Dictionary<string, ContentCatalogDataEntry> IdToCatalogDataEntryMap;
            public Dictionary<string, string> BundleToInternalBundleIdMap;
            public IBundleWriteData WriteData;
            public AddressablesContentState ContentState;
            public FileRegistry Registry;
            public List<CachedAssetState> PreviousAssetStateCarryOver;
        }

        static bool GetAssetState(GUID asset, out AssetState assetState)
        {
            assetState = new AssetState();
            if (asset.Empty())
                return false;

            var path = AssetDatabase.GUIDToAssetPath(asset.ToString());
            if (string.IsNullOrEmpty(path))
                return false;

            var hash = AssetDatabase.GetAssetDependencyHash(path);
            if (!hash.isValid)
                return false;

            assetState.guid = asset;
            assetState.hash = hash;
            return true;
        }

        static bool GetCachedAssetStateForData(GUID asset, string bundleFileId, string groupGuid, object data, IEnumerable<GUID> dependencies, out CachedAssetState cachedAssetState)
        {
            cachedAssetState = null;

            AssetState assetState;
            if (!GetAssetState(asset, out assetState))
                return false;

            var visited = new HashSet<GUID>();
            visited.Add(asset);
            var dependencyStates = new List<AssetState>();
            foreach (var dependency in dependencies)
            {
                if (!visited.Add(dependency))
                    continue;

                AssetState dependencyState;
                if (!GetAssetState(dependency, out dependencyState))
                    continue;
                dependencyStates.Add(dependencyState);
            }

            cachedAssetState = new CachedAssetState();
            cachedAssetState.asset = assetState;
            cachedAssetState.dependencies = dependencyStates.ToArray();
            cachedAssetState.groupGuid = groupGuid;
            cachedAssetState.bundleFileId = bundleFileId;
            cachedAssetState.data = data;

            return true;
        }

        static bool HasAssetOrDependencyChanged(CachedAssetState cachedInfo)
        {
            CachedAssetState newCachedInfo;
            if (!GetCachedAssetStateForData(cachedInfo.asset.guid, cachedInfo.bundleFileId, cachedInfo.groupGuid, cachedInfo.data, cachedInfo.dependencies.Select(x => x.guid), out newCachedInfo))
                return true;
            return !cachedInfo.Equals(newCachedInfo);
        }

        /// <summary>
        /// Save the content update information for a set of AddressableAssetEntry objects.
        /// </summary>
        /// <param name="path">File to write content stat info to.  If file already exists, it will be deleted before the new file is created.</param>
        /// <param name="entries">The entries to save.</param>
        /// <param name="dependencyData">The raw dependency information generated from the build.</param>
        /// <param name="playerVersion">The player version to save. This is usually set to AddressableAssetSettings.PlayerBuildVersion.</param>
        /// <param name="remoteCatalogPath">The server path (if any) that contains an updateable content catalog.  If this is empty, updates cannot occur.</param>
        /// <returns>True if the file is saved, false otherwise.</returns>
        [Obsolete]
        public static bool SaveContentState(string path, List<AddressableAssetEntry> entries, IDependencyData dependencyData, string playerVersion, string remoteCatalogPath)
        {
            try
            {
                IList<CachedAssetState> cachedInfos = new List<CachedAssetState>();
                foreach (var assetData in dependencyData.AssetInfo)
                {
                    CachedAssetState cachedAssetState;
                    if (GetCachedAssetStateForData(assetData.Key, null, null, null, assetData.Value.referencedObjects.Select(x => x.guid), out cachedAssetState))
                        cachedInfos.Add(cachedAssetState);
                }
                foreach (var sceneData in dependencyData.SceneInfo)
                {
                    CachedAssetState cachedAssetState;
                    if (GetCachedAssetStateForData(sceneData.Key, null, null, null, sceneData.Value.referencedObjects.Select(x => x.guid), out cachedAssetState))
                        cachedInfos.Add(cachedAssetState);
                }

                var cacheData = new AddressablesContentState
                {
                    cachedInfos = cachedInfos.ToArray(),
                    playerVersion = playerVersion,
                    editorVersion = Application.unityVersion,
                    remoteCatalogLoadPath = remoteCatalogPath
                };
                var formatter = new BinaryFormatter();
                if (File.Exists(path))
                    File.Delete(path);
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
                formatter.Serialize(stream, cacheData);
                stream.Flush();
                stream.Close();
                stream.Dispose();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return false;
            }
        }

        /// <summary>
        /// Save the content update information for a set of AddressableAssetEntry objects.
        /// </summary>
        /// <param name="locations">The ContentCatalogDataEntry locations that were built into the Content Catalog.</param>
        /// <param name="path">File to write content stat info to.  If file already exists, it will be deleted before the new file is created.</param>
        /// <param name="entries">The entries to save.</param>
        /// <param name="dependencyData">The raw dependency information generated from the build.</param>
        /// <param name="playerVersion">The player version to save. This is usually set to AddressableAssetSettings.PlayerBuildVersion.</param>
        /// <param name="remoteCatalogPath">The server path (if any) that contains an updateable content catalog.  If this is empty, updates cannot occur.</param>
        /// <returns>True if the file is saved, false otherwise.</returns>
        public static bool SaveContentState(List<ContentCatalogDataEntry> locations, string path, List<AddressableAssetEntry> entries, IDependencyData dependencyData, string playerVersion, string remoteCatalogPath)
        {
            return SaveContentState(locations, path, entries, dependencyData, playerVersion, remoteCatalogPath, null);
        }

        /// <summary>
        /// Save the content update information for a set of AddressableAssetEntry objects.
        /// </summary>
        /// <param name="locations">The ContentCatalogDataEntry locations that were built into the Content Catalog.</param>
        /// <param name="path">File to write content stat info to.  If file already exists, it will be deleted before the new file is created.</param>
        /// <param name="entries">The entries to save.</param>
        /// <param name="dependencyData">The raw dependency information generated from the build.</param>
        /// <param name="playerVersion">The player version to save. This is usually set to AddressableAssetSettings.PlayerBuildVersion.</param>
        /// <param name="remoteCatalogPath">The server path (if any) that contains an updateable content catalog.  If this is empty, updates cannot occur.</param>
        /// <param name="carryOverCacheState">Cached state that needs to carry over from the previous build.  This mainly affects Content Update.</param>
        /// <returns>True if the file is saved, false otherwise.</returns>
        public static bool SaveContentState(List<ContentCatalogDataEntry> locations, string path, List<AddressableAssetEntry> entries, IDependencyData dependencyData, string playerVersion, string remoteCatalogPath, List<CachedAssetState> carryOverCacheState)
        {
            try
            {
                Dictionary<string, AddressableAssetEntry> guidToEntries = new Dictionary<string, AddressableAssetEntry>();
                Dictionary<string, ContentCatalogDataEntry> key1ToCCEntries = new Dictionary<string, ContentCatalogDataEntry>();

                foreach(AddressableAssetEntry entry in entries)
                    if (!guidToEntries.ContainsKey(entry.guid))
                        guidToEntries[entry.guid] = entry;
                foreach(ContentCatalogDataEntry ccEntry in locations)
                    if (ccEntry != null && ccEntry.Keys != null && ccEntry.Keys.Count > 1 && (ccEntry.Keys[1] as string) != null && !key1ToCCEntries.ContainsKey(ccEntry.Keys[1] as string))
                        key1ToCCEntries[ccEntry.Keys[1] as string] = ccEntry;

                IList<CachedAssetState> cachedInfos = new List<CachedAssetState>();
                foreach (var assetData in dependencyData.AssetInfo)
                {
                    guidToEntries.TryGetValue(assetData.Key.ToString(), out AddressableAssetEntry addressableAssetEntry);
                    key1ToCCEntries.TryGetValue(assetData.Key.ToString(), out ContentCatalogDataEntry catalogAssetEntry);
                    if (addressableAssetEntry != null && catalogAssetEntry != null &&
                        GetCachedAssetStateForData(assetData.Key, addressableAssetEntry.BundleFileId, addressableAssetEntry.parentGroup.Guid, catalogAssetEntry.Data, assetData.Value.referencedObjects.Select(x => x.guid), out CachedAssetState cachedAssetState))
                        cachedInfos.Add(cachedAssetState);
                }

                foreach (var sceneData in dependencyData.SceneInfo)
                {
                    guidToEntries.TryGetValue(sceneData.Key.ToString(), out AddressableAssetEntry addressableSceneEntry);
                    key1ToCCEntries.TryGetValue(sceneData.Key.ToString(), out ContentCatalogDataEntry catalogSceneEntry);
                    if (addressableSceneEntry != null && catalogSceneEntry != null && 
                        GetCachedAssetStateForData(sceneData.Key, addressableSceneEntry.BundleFileId, addressableSceneEntry.parentGroup.Guid, catalogSceneEntry.Data, sceneData.Value.referencedObjects.Select(x => x.guid), out CachedAssetState cachedAssetState))
                        cachedInfos.Add(cachedAssetState);
                }

                if (carryOverCacheState != null)
                {
                    foreach(var cs in carryOverCacheState)
                        cachedInfos.Add(cs);
                }

                var cacheData = new AddressablesContentState
                {
                    cachedInfos = cachedInfos.ToArray(),
                    playerVersion = playerVersion,
                    editorVersion = Application.unityVersion,
                    remoteCatalogLoadPath = remoteCatalogPath
                };
                var formatter = new BinaryFormatter();
                if (File.Exists(path))
                    File.Delete(path);
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
                formatter.Serialize(stream, cacheData);
                stream.Flush();
                stream.Close();
                stream.Dispose();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return false;
            }
        }

        /// <summary>
        /// Gets the path of the cache data from a selected build.
        /// </summary>
        /// <param name="browse">If true, the user is allowed to browse for a specific file.</param>
        /// <returns></returns>
        public static string GetContentStateDataPath(bool browse)
        {
            var assetPath = AddressableAssetSettingsDefaultObject.kDefaultConfigFolder;
            if (AddressableAssetSettingsDefaultObject.Settings != null)
                assetPath = AddressableAssetSettingsDefaultObject.Settings.ConfigFolder;
            assetPath = Path.Combine(assetPath, PlatformMappingService.GetPlatform().ToString());

            if (browse)
            {
                if (string.IsNullOrEmpty(assetPath))
                    assetPath = Application.dataPath;

                assetPath = EditorUtility.OpenFilePanel("Build Data File", Path.GetDirectoryName(assetPath), "bin");

                if (string.IsNullOrEmpty(assetPath))
                    return null;

                return assetPath;
            }

            Directory.CreateDirectory(assetPath);
            var path = Path.Combine(assetPath, "addressables_content_state.bin");
            return path;
        }

        /// <summary>
        /// Loads cache data from a specific location
        /// </summary>
        /// <param name="contentStateDataPath"></param>
        /// <returns>The ContentState object.</returns>
        public static AddressablesContentState LoadContentState(string contentStateDataPath)
        {
            if (string.IsNullOrEmpty(contentStateDataPath))
            {
                Debug.LogErrorFormat("Unable to load cache data from {0}.", contentStateDataPath);
                return null;
            }
            var stream = new FileStream(contentStateDataPath, FileMode.Open, FileAccess.Read);
            var formatter = new BinaryFormatter();
            var cacheData = formatter.Deserialize(stream) as AddressablesContentState;
            if (cacheData == null)
            {
                Addressables.LogError("Invalid hash data file.  This file is usually named addressables_content_state.bin and is saved in the same folder as your source AddressableAssetsSettings.asset file.");
                return null;
            }
            stream.Dispose();
            return cacheData;
        }

        static bool s_StreamingAssetsExists;
        static string kStreamingAssetsPath = "Assets/StreamingAssets";

        internal static void Cleanup(bool deleteStreamingAssetsFolderIfEmpty, bool cleanBuildPath)
        {
            if (cleanBuildPath && Directory.Exists(Addressables.BuildPath))
            {
                Directory.Delete(Addressables.BuildPath, true);
                if (File.Exists(Addressables.BuildPath + ".meta"))
                    File.Delete(Addressables.BuildPath + ".meta");
            }
            if (deleteStreamingAssetsFolderIfEmpty)
            {
                if (Directory.Exists(kStreamingAssetsPath))
                {
                    var files = Directory.GetFiles(kStreamingAssetsPath);
                    if (files.Length == 0)
                    {
                        Directory.Delete(kStreamingAssetsPath);
                        if (File.Exists(kStreamingAssetsPath + ".meta"))
                            File.Delete(kStreamingAssetsPath + ".meta");

                    }
                }
            }
        }

        /// <summary>
        /// Builds player content using the player content version from a specified cache file.
        /// </summary>
        /// <param name="settings">The settings object to use for the build.</param>
        /// <param name="contentStateDataPath">The path of the cache data to use.</param>
        /// <returns>The build operation.</returns>
        public static AddressablesPlayerBuildResult BuildContentUpdate(AddressableAssetSettings settings, string contentStateDataPath)
        {
            var cacheData = LoadContentState(contentStateDataPath);
            if (!IsCacheDataValid(settings, cacheData))
                return null;

            s_StreamingAssetsExists = Directory.Exists("Assets/StreamingAssets");
            var context = new AddressablesDataBuilderInput(settings, cacheData.playerVersion);
            context.PreviousContentState = cacheData;

            Cleanup(!s_StreamingAssetsExists, false);

            SceneManagerState.Record();
            var result = settings.ActivePlayerDataBuilder.BuildData<AddressablesPlayerBuildResult>(context);
            if (!string.IsNullOrEmpty(result.Error))
                Debug.LogError(result.Error);
            SceneManagerState.Restore();
            return result;
        }

        internal static bool IsCacheDataValid(AddressableAssetSettings settings, AddressablesContentState cacheData)
        {
            if (cacheData == null)
                return false;

            if (cacheData.editorVersion != Application.unityVersion)
                Addressables.LogWarningFormat("Building content update with Unity editor version `{0}`, data was created with version `{1}`.  This may result in incompatible data.", Application.unityVersion, cacheData.editorVersion);

            if (string.IsNullOrEmpty(cacheData.remoteCatalogLoadPath))
            {
                Addressables.LogError("Previous build had 'Build Remote Catalog' disabled.  You cannot update a player that has no remote catalog specified");
                return false;
            }
            if (!settings.BuildRemoteCatalog)
            {
                Addressables.LogError("Current settings have 'Build Remote Catalog' disabled.  You cannot update a player that has no remote catalog to look to.");
                return false;
            }

            if (cacheData.remoteCatalogLoadPath != settings.RemoteCatalogLoadPath.GetValue(settings))
            {
                Addressables.LogErrorFormat("Current 'Remote Catalog Load Path' does not match load path of original player.  Player will only know to look up catalog at original location. Original: {0}  Current: {1}", cacheData.remoteCatalogLoadPath, settings.RemoteCatalogLoadPath.GetValue(settings));
                return false;
            }

            return true;
        }
        /// <summary>
        /// Get all modified addressable asset entries in groups that have BundledAssetGroupSchema and ContentUpdateGroupSchema with static content enabled.
        /// This includes any Addressable dependencies that are affected by the modified entries.
        /// </summary>
        /// <param name="settings">Addressable asset settings.</param>
        /// <param name="cacheDataPath">The cache data path.</param>
        /// <returns>A list of all modified entries and dependencies (list is empty if there are none); null if failed to load cache data.</returns>
        public static List<AddressableAssetEntry> GatherModifiedEntries(AddressableAssetSettings settings, string cacheDataPath)
        {
            HashSet<AddressableAssetEntry> retVal = new HashSet<AddressableAssetEntry>();
            var entriesMap = GatherModifiedEntriesWithDependencies(settings, cacheDataPath);
            foreach (var entry in entriesMap.Keys)
            {
                if(!retVal.Contains(entry))
                    retVal.Add(entry);

                foreach(var dependency in entriesMap[entry])
                    if (!retVal.Contains(dependency))
                        retVal.Add(dependency);
            }

            return retVal.ToList();
        }

        internal static void GatherExplicitModifedEnteries(AddressableAssetSettings settings, string cacheDataPath, ref Dictionary<AddressableAssetEntry, List<AddressableAssetEntry>> dependencyMap)
        {
            var cacheData = LoadContentState(cacheDataPath);
            if (cacheData == null)
            {
                dependencyMap = null;
                return;
            }

            List<string> noBundledAssetGroupSchema = new List<string>();
            List<string> noStaticContent = new List<string>();

            var allEntries = new List<AddressableAssetEntry>();
            settings.GetAllAssets(allEntries, false, g =>
            {
                if (g == null)
                    return false;

                if (!g.HasSchema<BundledAssetGroupSchema>())
                {
                    noBundledAssetGroupSchema.Add(g.Name);
                    return false;
                }

                if (!g.HasSchema<ContentUpdateGroupSchema>())
                {
                    noStaticContent.Add(g.Name);
                    return false;
                }

                if (!g.GetSchema<ContentUpdateGroupSchema>().StaticContent)
                {
                    noStaticContent.Add(g.Name);
                    return false;
                }

                return true;
            });

            StringBuilder builder = new StringBuilder();
            builder.AppendFormat("Skipping Prepare for Content Update on {0} group(s):\n\n",
                noBundledAssetGroupSchema.Count + noStaticContent.Count);


            AddInvalidGroupsToLogMessage(builder, noBundledAssetGroupSchema, "Group Did Not Contain BundledAssetGroupSchema");
            AddInvalidGroupsToLogMessage(builder, noStaticContent, "Static Content Not Enabled In Schemas");

            Debug.Log(builder.ToString());

            var entryToCacheInfo = new Dictionary<string, CachedAssetState>();
            foreach (var cacheInfo in cacheData.cachedInfos)
                if (cacheInfo != null)
                    entryToCacheInfo[cacheInfo.asset.guid.ToString()] = cacheInfo;
            var modifiedEntries = new List<AddressableAssetEntry>();
            foreach (var entry in allEntries)
            {
                CachedAssetState cachedInfo;
                if (!entryToCacheInfo.TryGetValue(entry.guid, out cachedInfo) || HasAssetOrDependencyChanged(cachedInfo))
                    modifiedEntries.Add(entry);
            }

            AddAllDependentScenesFromModifiedEnteries(modifiedEntries);
            foreach (var entry in modifiedEntries)
            {
                if(!dependencyMap.ContainsKey(entry))
                    dependencyMap.Add(entry, new List<AddressableAssetEntry>());
            }
        }

        /// <summary>
        /// Get a Dictionary of all modified values and their dependencies.  Dependencies will be Addressable and part of a group
        /// with static content enabled.
        /// </summary>
        /// <param name="settings">Addressable asset settings.</param>
        /// <param name="cachePath">The cache data path.</param>
        /// <returns>A dictionary mapping explicit changed entries to their dependencies.</returns>
        public static Dictionary<AddressableAssetEntry, List<AddressableAssetEntry>> GatherModifiedEntriesWithDependencies(AddressableAssetSettings settings, string cachePath)
        {
            Dictionary<AddressableAssetEntry, List<AddressableAssetEntry>> modifiedData = new Dictionary<AddressableAssetEntry, List<AddressableAssetEntry>>();
            GatherExplicitModifedEnteries(settings, cachePath, ref modifiedData);
            GetStaticContentDependenciesForEntries(settings, ref modifiedData);
            return modifiedData;
        }

        internal static void GetStaticContentDependenciesForEntries(AddressableAssetSettings settings, ref Dictionary<AddressableAssetEntry, List<AddressableAssetEntry>> dependencyMap)
        {
            Dictionary<AddressableAssetGroup, bool> groupHasStaticContentMap = new Dictionary<AddressableAssetGroup, bool>();

            if (dependencyMap == null)
                return;

            foreach (AddressableAssetEntry entry in dependencyMap.Keys)
            {
                //since the entry here is from our list of modified enteries we know that it must be a part of a static content group.
                //Since it's part of a static content update group we can go ahead and set the value to true in the dictionary without explicitly checking it.
                if (!groupHasStaticContentMap.ContainsKey(entry.parentGroup))
                    groupHasStaticContentMap.Add(entry.parentGroup, true);

                string[] dependencies = AssetDatabase.GetDependencies(entry.AssetPath);
                foreach (string dependency in dependencies)
                {
                    string guid = AssetDatabase.AssetPathToGUID(dependency);
                    var depEntry = settings.FindAssetEntry(guid);
                    if (depEntry != null)
                    {
                        if (!groupHasStaticContentMap.TryGetValue(depEntry.parentGroup, out bool groupHasStaticContentEnabled))
                        {
                            groupHasStaticContentEnabled = depEntry.parentGroup.HasSchema<ContentUpdateGroupSchema>() &&
                                                           depEntry.parentGroup.GetSchema<ContentUpdateGroupSchema>().StaticContent;

                            groupHasStaticContentMap.Add(depEntry.parentGroup, groupHasStaticContentEnabled);
                        }

                        if (!dependencyMap.ContainsKey(depEntry) && groupHasStaticContentEnabled)
                        {
                            if(!dependencyMap.ContainsKey(entry))
                                dependencyMap.Add(entry, new List<AddressableAssetEntry>());
                            dependencyMap[entry].Add(depEntry);
                        }
                    }
                }
            }
        }

        internal static void AddAllDependentScenesFromModifiedEnteries(List<AddressableAssetEntry> modifiedEntries)
        {
            List<AddressableAssetEntry> entriesToAdd = new List<AddressableAssetEntry>();
            //If a scene has changed, all scenes that end up in the same bundle need to be marked as modified due to bundle dependencies
            foreach (AddressableAssetEntry entry in modifiedEntries)
            {
                if (entry.IsScene && !entriesToAdd.Contains(entry))
                {
                    switch (entry.parentGroup.GetSchema<BundledAssetGroupSchema>().BundleMode)
                    {
                        case BundledAssetGroupSchema.BundlePackingMode.PackTogether:
                            //Add every scene in the group to modified entries
                            foreach (AddressableAssetEntry sharedGroupEntry in entry.parentGroup.entries)
                            {
                                if (sharedGroupEntry.IsScene && !modifiedEntries.Contains(sharedGroupEntry))
                                    entriesToAdd.Add(sharedGroupEntry);
                            }
                            break;

                        case BundledAssetGroupSchema.BundlePackingMode.PackTogetherByLabel:
                            foreach (AddressableAssetEntry sharedGroupEntry in entry.parentGroup.entries)
                            {
                                //Check if one entry has 0 labels while the other contains labels.  The labels union check below will return true in this case.
                                //That is not the behavior we want.  So to avoid that, we check here first.
                                if (sharedGroupEntry.labels.Count == 0 ^ entry.labels.Count == 0)
                                    continue;

                                //Only add if labels are shared
                                if (sharedGroupEntry.IsScene && !modifiedEntries.Contains(sharedGroupEntry) && sharedGroupEntry.labels.Union(entry.labels).Any())
                                    entriesToAdd.Add(sharedGroupEntry);
                            }
                            break;

                        case BundledAssetGroupSchema.BundlePackingMode.PackSeparately:
                            //Do nothing.  The scene will be in a different bundle.
                            break;

                        default:
                            break;
                    }
                }
            }

            modifiedEntries.AddRange(entriesToAdd);
        }

        private static void AddInvalidGroupsToLogMessage(StringBuilder builder, List<string> invalidGroupList,
            string headerMessage)
        {
            if (invalidGroupList.Count > 0)
            {
                builder.AppendFormat("{0} ({1} groups):\n", headerMessage, invalidGroupList.Count);
                int maxList = 15;
                for (int i = 0; i < invalidGroupList.Count; i++)
                {
                    if (i > maxList)
                    {
                        builder.AppendLine("...");
                        break;
                    }

                    builder.AppendLine("-" + invalidGroupList[i]);
                }

                builder.AppendLine("");
            }
        }

        /// <summary>
        /// Create a new AddressableAssetGroup with the items and mark it as remote.
        /// </summary>
        /// <param name="settings">The settings object.</param>
        /// <param name="items">The items to move.</param>
        /// <param name="groupName">The name of the new group.</param>
        public static void CreateContentUpdateGroup(AddressableAssetSettings settings, List<AddressableAssetEntry> items, string groupName)
        {
            var contentGroup = settings.CreateGroup(settings.FindUniqueGroupName(groupName), false, false, true, null);
            var schema = contentGroup.AddSchema<BundledAssetGroupSchema>();
            schema.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteBuildPath);
            schema.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteLoadPath);
            schema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
            contentGroup.AddSchema<ContentUpdateGroupSchema>().StaticContent = false;
            settings.MoveEntries(items, contentGroup);
        }
    }
}