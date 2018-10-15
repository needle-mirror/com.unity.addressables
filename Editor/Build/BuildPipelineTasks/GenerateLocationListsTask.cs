using System;
using System.Collections.Generic;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEditor.Build.Pipeline.Injector;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement;
using UnityEngine;

namespace UnityEditor.AddressableAssets
{
    internal class GenerateLocationListsTask : IBuildTask
    {
        const int k_Version = 1;
        public int Version { get { return k_Version; } }

#pragma warning disable 649
        [InjectContext(ContextUsage.In)]
        IAddressableAssetsBuildContext m_AABuildContext;

        [InjectContext]
        IBundleWriteData m_WriteData;
#pragma warning restore 649

        public ReturnCode Run()
        {
            var aaContext = m_AABuildContext as AddressableAssetsBuildContext;
            var aaSettings = aaContext.m_settings;
            var locations = aaContext.m_locations;
            var bundleToAssetGroup = aaContext.m_bundleToAssetGroup;
            var bundleToAssets = new Dictionary<string, List<GUID>>();
            var assetsToBundles = new Dictionary<GUID, List<string>>();
            foreach (var k in m_WriteData.AssetToFiles)
            {
                List<string> bundleList = new List<string>();
                assetsToBundles.Add(k.Key, bundleList);
                List<GUID> assetList = null;
                var bundle = m_WriteData.FileToBundle[k.Value[0]];
                if (!bundleToAssets.TryGetValue(bundle, out assetList))
                    bundleToAssets.Add(bundle, assetList = new List<GUID>());
                if (!bundleList.Contains(bundle))
                    bundleList.Add(bundle);
                foreach (var file in k.Value)
                {
                    var fileBundle = m_WriteData.FileToBundle[file];
                    if (!bundleList.Contains(fileBundle))
                        bundleList.Add(fileBundle);
                    if (!bundleToAssets.ContainsKey(fileBundle))
                        bundleToAssets.Add(fileBundle, new List<GUID>());
                }

                assetList.Add(k.Key);
            }
            var assetGroupToBundle = (aaContext.m_assetGroupToBundles = new Dictionary<AddressableAssetGroup, List<string>>());
            foreach (var kvp in bundleToAssets)
            {
                AddressableAssetGroup assetGroup = null;
                if (!bundleToAssetGroup.TryGetValue(kvp.Key, out assetGroup))
                    assetGroup = aaSettings.DefaultGroup;
                List<string> bundles;
                if (!assetGroupToBundle.TryGetValue(assetGroup, out bundles))
                    assetGroupToBundle.Add(assetGroup, bundles = new List<string>());
                bundles.Add(kvp.Key);
                CreateResourceLocationData(assetGroup, kvp.Key, GetLoadPath(assetGroup, kvp.Key), GetProviderName(assetGroup), kvp.Value, assetsToBundles, locations);
            }

            return ReturnCode.Success;
        }

        static string GetProviderName(AddressableAssetGroup group)
        {
            return group.GetSchema<BundledAssetGroupSchema>().AssetBundleProviderType.Value.FullName;
        }

        static string GetLoadPath(AddressableAssetGroup group, string name)
        {
            return group.GetSchema<BundledAssetGroupSchema>().LoadPath.GetValue(group.Settings) + "/" + name;
        }

        static internal void CreateResourceLocationData(
        AddressableAssetGroup assetGroup,
        string bundleName,
        string bundleInternalId,
        string bundleProvider,
        List<GUID> assetsInBundle,
        Dictionary<GUID, List<string>> assetsToBundles,
        List<ContentCatalogDataEntry> locations)
        {
            var settings = assetGroup.Settings;
            
            locations.Add(new ContentCatalogDataEntry(bundleInternalId, bundleProvider, new object[] { bundleName }));

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
                var assetPath = entry.GetAssetLoadPath(true);
                var keys = new List<object>();
                keys.Add(entry.address);
                keys.Add(Hash128.Parse(entry.guid));
                foreach (var l in entry.labels)
                    keys.Add(l);

                locations.Add(new ContentCatalogDataEntry(assetPath, typeof(BundledAssetProvider).FullName, keys, assetsToBundles[a].ToArray()));
            }
        }
    }

}
