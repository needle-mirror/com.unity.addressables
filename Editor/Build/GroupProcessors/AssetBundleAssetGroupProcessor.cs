using System.Collections.Generic;
using System.IO;
using UnityEditor.Build.Interfaces;
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
        protected abstract string GetBundleLoadProvider(AddressableAssetSettings settings);
        protected string GetAssetLoadProvider(AddressableAssetSettings settings)
        {
            return typeof(BundledAssetProvider).FullName;
        }

        protected abstract BundleMode GetBundleMode(AddressableAssetSettings settings);

        internal override void CreateResourceLocationData(
            AddressableAssetSettings settings,
            AddressableAssetSettings.AssetGroup assetGroup,
            string bundleName,
            List<GUID> assetsInBundle,
            Dictionary<GUID, List<string>> assetsToBundles,
            List<ResourceLocationData> locations)
        {
            locations.Add(new ResourceLocationData(bundleName, string.Empty, GetBundleLoadPath(settings, bundleName), GetBundleLoadProvider(settings), false, ResourceLocationData.LocationType.String, 0, typeof(UnityEngine.AssetBundle).FullName, null));

            foreach (var a in assetsInBundle)
            {  
                var assetEntry = settings.FindAssetEntry(a.ToString());
                if (assetEntry == null)
                    continue;
                var t = AssetDatabase.GetMainAssetTypeAtPath(assetEntry.assetPath);
                var assetType = t == null ? string.Empty : t.FullName;
                if (t == null)
                    Debug.Log("Can't get asset type for " + assetEntry.assetPath);
                var assetPath = assetEntry.GetAssetLoadPath(settings.buildSettings.editorPlayMode == ResourceManagerRuntimeData.EditorPlayMode.PackedMode);
                locations.Add(new ResourceLocationData(assetEntry.address, assetEntry.guid, assetPath, GetAssetLoadProvider(settings), true, ResourceLocationData.LocationType.String, settings.labelTable.GetMask(assetEntry.labels), assetType, assetsToBundles[a].ToArray()));
            }
        }

        internal override void ProcessGroup(AddressableAssetSettings settings, AddressableAssetSettings.AssetGroup assetGroup, List<AssetBundleBuild> bundleInputDefs, List<ResourceLocationData> locationData)
        {
            if (GetBundleMode(settings) == BundleMode.PackTogether)
            {
                var allEntries = new List<AddressableAssetSettings.AssetGroup.AssetEntry>();
                foreach (var a in assetGroup.entries)
                    a.GatherAllAssets(allEntries, settings);
                GenerateBuildInputDefinitions(allEntries, bundleInputDefs, assetGroup.name, "all");
            }
            else
            {
                foreach (var a in assetGroup.entries)
                {
                    var allEntries = new List<AddressableAssetSettings.AssetGroup.AssetEntry>();
                    a.GatherAllAssets(allEntries, settings);
                    GenerateBuildInputDefinitions(allEntries, bundleInputDefs, assetGroup.name, a.address);
                }
            }
        }

        internal override void PostProcessBundles(AddressableAssetSettings settings, AddressableAssetSettings.AssetGroup assetGroup, List<string> bundles, IBuildResults buildResult, IWriteData writeData, ResourceManagerRuntimeData runtimeData)
        {
            var path = GetBuildPath(settings);
            if (string.IsNullOrEmpty(path))
                return;

            foreach (var bundleName in bundles)
            {
                var targetPath = Path.Combine(path, bundleName);
                if (!Directory.Exists(Path.GetDirectoryName(targetPath)))
                    Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
                File.Copy(Path.Combine(settings.buildSettings.bundleBuildPath, bundleName), targetPath, true);
            }
        }

        private void GenerateBuildInputDefinitions(List<AddressableAssetSettings.AssetGroup.AssetEntry> allEntries, List<AssetBundleBuild> buildInputDefs, string groupName, string address)
        {
            var scenes = new List<AddressableAssetSettings.AssetGroup.AssetEntry>();
            var assets = new List<AddressableAssetSettings.AssetGroup.AssetEntry>();
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

        private AssetBundleBuild GenerateBuildInputDefinition(List<AddressableAssetSettings.AssetGroup.AssetEntry> assets, string name)
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
    }
}
