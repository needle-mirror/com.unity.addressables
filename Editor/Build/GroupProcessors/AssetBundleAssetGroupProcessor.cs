using System.Collections.Generic;
using System.IO;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEngine;
using UnityEngine.ResourceManagement;
using UnityEngine.AddressableAssets;

namespace UnityEditor.AddressableAssets
{
    /// <summary>
    /// TODO - doc
    /// </summary>
    public abstract class AssetBundleAssetGroupProcessor : AssetGroupProcessor
    {
        /// <summary>
        /// TODO - doc
        /// </summary>
        public enum BundleMode
        {
            PackTogether,
            PackSeparately
        }
        protected abstract string GetBuildPath(AddressableAssetSettings settings);
        protected abstract string GetBundleLoadPath(AddressableAssetSettings settings, string bundleName);
        protected System.Type GetAssetLoadProvider(AddressableAssetSettings settings)
        {
            return typeof(BundledAssetProvider);
        }

        protected abstract BundleMode GetBundleMode(AddressableAssetSettings settings);

        internal override void CreateResourceLocationData(
            AddressableAssetSettings settings,
            AddressableAssetGroup assetGroup,
            string bundleName,
            List<GUID> assetsInBundle,
            Dictionary<GUID, List<string>> assetsToBundles,
            Dictionary<object, ContentCatalogData.DataEntry> locations)
        {
            locations.Add(bundleName, new ContentCatalogData.DataEntry(bundleName, null, GetBundleLoadPath(settings, bundleName), typeof(AssetBundleProvider)));

            var assets = new List<AddressableAssetEntry>();
            assetGroup.GatherAllAssets(assets, true, true);
            var guidToEntry = new Dictionary<string, AddressableAssetEntry>();
            foreach (var a in assets)
                guidToEntry.Add(a.guid, a);

            foreach (var a in assetsInBundle)
            {
                AddressableAssetEntry entry;
                if (!guidToEntry.TryGetValue(a.ToString(), out entry))
                    continue;
                var assetPath = entry.GetAssetLoadPath(ProjectConfigData.editorPlayMode == ResourceManagerRuntimeData.EditorPlayMode.PackedMode);
                locations.Add(entry.address, new ContentCatalogData.DataEntry(entry.address, entry.guid, assetPath, GetAssetLoadProvider(settings), entry.labels, assetsToBundles[a].ToArray()));
            }
        }

        internal override void ProcessGroup(AddressableAssetSettings settings, AddressableAssetGroup assetGroup, List<AssetBundleBuild> bundleInputDefs, Dictionary<object, ContentCatalogData.DataEntry> locationData)
        {
            if (GetBundleMode(settings) == BundleMode.PackTogether)
            {
                var allEntries = new List<AddressableAssetEntry>();
                foreach (var a in assetGroup.entries)
                    a.GatherAllAssets(allEntries, settings, true);
                GenerateBuildInputDefinitions(allEntries, bundleInputDefs, assetGroup.name, "all");
            }
            else
            {
                foreach (var a in assetGroup.entries)
                {
                    var allEntries = new List<AddressableAssetEntry>();
                    a.GatherAllAssets(allEntries, settings, true);
                    GenerateBuildInputDefinitions(allEntries, bundleInputDefs, assetGroup.name, a.address);
                }
            }
        }

        internal override void PostProcessBundles(AddressableAssetSettings settings, AddressableAssetGroup assetGroup, List<string> bundles, IBundleBuildResults buildResult, IWriteData writeData, ResourceManagerRuntimeData runtimeData, Dictionary<object, ContentCatalogData.DataEntry> locations)
        {
            var path = GetBuildPath(settings);
            if (string.IsNullOrEmpty(path))
                return;

            foreach (var bundleName in bundles)
            {
                var info = buildResult.BundleInfos[bundleName];
                var targetPath = Path.Combine(path, bundleName.Replace(".bundle", "_" + info.Hash + ".bundle"));
                ContentCatalogData.DataEntry dataEntry;
                if (locations.TryGetValue(bundleName, out dataEntry))
                {
                    var cacheData = new AssetBundleProvider.CacheInfo() { m_crc = info.Crc, m_hash = info.Hash.ToString() };
                    dataEntry.m_data = cacheData;
                    dataEntry.m_internalId = dataEntry.m_internalId.Replace(".bundle", "_" + info.Hash + ".bundle");
                }
                if (!Directory.Exists(Path.GetDirectoryName(targetPath)))
                    Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
                File.Copy(Path.Combine(settings.buildSettings.bundleBuildPath, bundleName), targetPath, true);
            }
        }

        private void GenerateBuildInputDefinitions(List<AddressableAssetEntry> allEntries, List<AssetBundleBuild> buildInputDefs, string groupName, string address)
        {
            var scenes = new List<AddressableAssetEntry>();
            var assets = new List<AddressableAssetEntry>();
            foreach (var e in allEntries)
            {
                if (e.assetPath.EndsWith(".unity"))
                    scenes.Add(e);
                else
                    assets.Add(e);
            }
            if (assets.Count > 0)
                buildInputDefs.Add(GenerateBuildInputDefinition(assets, groupName + "_assets_" + address + ".bundle"));
            if (scenes.Count > 0)
                buildInputDefs.Add(GenerateBuildInputDefinition(scenes, groupName + "_scenes_" + address + ".bundle"));
        }

        private AssetBundleBuild GenerateBuildInputDefinition(List<AddressableAssetEntry> assets, string name)
        {
            var assetsInputDef = new AssetBundleBuild();
            assetsInputDef.assetBundleName = name.ToLower().Replace(" ", "").Replace('\\', '/').Replace("//", "/");
            var assetIds = new List<string>(assets.Count);
            var assetGuids = new List<string>(assets.Count);
            foreach (var a in assets)
            {
                assetIds.Add(a.assetPath);
                assetGuids.Add(a.guid);
            }
            assetsInputDef.assetNames = assetIds.ToArray();
            assetsInputDef.addressableNames = new string[0];
            return assetsInputDef;
        }

        internal override void CreateCatalog(AddressableAssetSettings aaSettings, AddressableAssetGroup group, ContentCatalogData contentCatalog, List<ResourceLocationData> locations)
        {
            var buildPath = GetBuildPath(aaSettings) + aaSettings.profileSettings.EvaluateString(aaSettings.activeProfileId, "/catalog_[ContentVersion].json");
            var remoteHashLoadPath = GetBundleLoadPath(aaSettings, "catalog_{ContentVersion}.hash");
            var localCacheLoadPath = "{UnityEngine.Application.persistentDataPath}/Unity/AddressablesCatalogCache/catalog_{ContentVersion}.hash";

            var jsonText = JsonUtility.ToJson(contentCatalog);
            var contentHash = Build.Pipeline.Utilities.HashingMethods.Calculate(jsonText).ToString();

            var buildPathDir = Path.GetDirectoryName(buildPath);
            if (!Directory.Exists(buildPathDir))
                Directory.CreateDirectory(buildPathDir);
            File.WriteAllText(buildPath, jsonText);
            File.WriteAllText(buildPath.Replace(".json", ".hash"), contentHash);

            var remoteHash = new ResourceLocationData("RemoteCatalogHash" + group.guid, "", remoteHashLoadPath, typeof(TextDataProvider));
            var localHash = new ResourceLocationData("LocalCatalogHash" + group.guid, "", localCacheLoadPath, typeof(TextDataProvider));


            int priority = GetPriority(aaSettings, group);
            var internalId = remoteHashLoadPath.Replace(".hash", ".json");
            locations.Add(new ResourceLocationData(priority + "_RemoteCatalog_" + group.guid, "", internalId, typeof(ContentCatalogProvider), null, new string[] { localHash.m_address, remoteHash.m_address }));
            locations.Add(localHash);
            locations.Add(remoteHash);
        }

    }
}
