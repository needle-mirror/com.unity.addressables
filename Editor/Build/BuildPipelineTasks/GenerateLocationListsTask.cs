using System;
using System.Collections.Generic;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Injector;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEngine;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace UnityEditor.AddressableAssets.Build.BuildPipelineTasks
{
    /// <summary>
    /// The BuildTask used to create location lists for Addressable assets.
    /// </summary>
    public class GenerateLocationListsTask : IBuildTask
    {
        const int k_Version = 1;

        /// <summary>
        /// The GenerateLocationListsTask version.
        /// </summary>
        public int Version { get { return k_Version; } }

#pragma warning disable 649
        [InjectContext(ContextUsage.In)]
        IAddressableAssetsBuildContext m_AaBuildContext;

        [InjectContext]
        IBundleWriteData m_WriteData;
#pragma warning restore 649

        /// <summary>
        /// Runs the build task with the injected context.
        /// </summary>
        /// <returns>The success or failure ReturnCode</returns>
        public ReturnCode Run()
        {
            return Run(m_AaBuildContext, m_WriteData);
        }

        /// <summary>
        /// Runs the build task with a give context and write data.
        /// </summary>
        /// <param name="aaBuildContext">The addressables build context.</param>
        /// <param name="writeData">The write data used to generate the location lists.</param>
        /// <returns>The success or failure ReturnCode</returns>
        public static ReturnCode Run(IAddressableAssetsBuildContext aaBuildContext, IBundleWriteData writeData)
        {
            var aaContext = aaBuildContext as AddressableAssetsBuildContext;
            if (aaContext == null)
                return ReturnCode.Error;
            var aaSettings = aaContext.settings;
            var locations = aaContext.locations;
            var bundleToAssetGroup = aaContext.bundleToAssetGroup;
            var bundleToAssets = new Dictionary<string, List<GUID>>();
            var dependencySetForBundle = new Dictionary<string, HashSet<string>>();
            foreach (var k in writeData.AssetToFiles)
            {
                List<GUID> assetList;
                var bundle = writeData.FileToBundle[k.Value[0]];
                if (!bundleToAssets.TryGetValue(bundle, out assetList))
                    bundleToAssets.Add(bundle, assetList = new List<GUID>());
                HashSet<string> bundleDeps;
                if (!dependencySetForBundle.TryGetValue(bundle, out bundleDeps))
                    dependencySetForBundle.Add(bundle, bundleDeps = new HashSet<string>());
                for (int i = 0; i < k.Value.Count; i++)
                    bundleDeps.Add(writeData.FileToBundle[k.Value[i]]);
                foreach (var file in k.Value)
                {
                    var fileBundle = writeData.FileToBundle[file];
                    if (!bundleToAssets.ContainsKey(fileBundle))
                        bundleToAssets.Add(fileBundle, new List<GUID>());
                }

                assetList.Add(k.Key);
            }
            var assetGroupToBundle = (aaContext.assetGroupToBundles = new Dictionary<AddressableAssetGroup, List<string>>());
            foreach (var kvp in bundleToAssets)
            {
                AddressableAssetGroup assetGroup = aaSettings.DefaultGroup;
                string groupGuid;
                if (bundleToAssetGroup.TryGetValue(kvp.Key, out groupGuid))
                    assetGroup = aaSettings.FindGroup(g => g != null && g.Guid == groupGuid);

                List<string> bundles;
                if (!assetGroupToBundle.TryGetValue(assetGroup, out bundles))
                    assetGroupToBundle.Add(assetGroup, bundles = new List<string>());
                bundles.Add(kvp.Key);
                HashSet<string> bundleDeps = null;
                dependencySetForBundle.TryGetValue(kvp.Key, out bundleDeps);
                CreateResourceLocationData(assetGroup, kvp.Key, GetLoadPath(assetGroup, kvp.Key), GetBundleProviderName(assetGroup), GetAssetProviderName(assetGroup), kvp.Value, bundleDeps, locations, aaContext.providerTypes);
            }

            return ReturnCode.Success;
        }

        internal static string GetBundleProviderName(AddressableAssetGroup group)
        {
            return group.GetSchema<BundledAssetGroupSchema>().GetBundleCachedProviderId();
        }

        internal static string GetAssetProviderName(AddressableAssetGroup group)
        {
            return group.GetSchema<BundledAssetGroupSchema>().GetAssetCachedProviderId();
        }

        static string GetLoadPath(AddressableAssetGroup group, string name)
        {
            var bagSchema = group.GetSchema<BundledAssetGroupSchema>();
            if (bagSchema == null || bagSchema.LoadPath == null)
            {
                Debug.LogError("Unable to determine load path for " + name + ". Check that your default group is not '" + AddressableAssetSettings.PlayerDataGroupName + "'");
                return string.Empty;
            }
            var loadPath = bagSchema.LoadPath.GetValue(group.Settings) + "/" + name;
            if(!string.IsNullOrEmpty(bagSchema.UrlSuffix))
                loadPath += bagSchema.UrlSuffix;
            return loadPath;
        }

        internal static void CreateResourceLocationData(
        AddressableAssetGroup assetGroup,
        string bundleName,
        string bundleInternalId,
        string bundleProvider,
        string assetProvider,
        List<GUID> assetsInBundle,
        HashSet<string> bundleDependencies,
        List<ContentCatalogDataEntry> locations,
        HashSet<Type> providerTypes)
        {
            locations.Add(new ContentCatalogDataEntry(typeof(IAssetBundleResource), bundleInternalId, bundleProvider, new object[] { bundleName }));

            var assets = new List<AddressableAssetEntry>();
            assetGroup.GatherAllAssets(assets, true, true, false);
            var guidToEntry = new Dictionary<string, AddressableAssetEntry>();
            foreach (var a in assets)
                guidToEntry.Add(a.guid, a);
            foreach (var a in assetsInBundle)
            {
                AddressableAssetEntry entry;
                if (!guidToEntry.TryGetValue(a.ToString(), out entry))
                    continue;
                entry.CreateCatalogEntries(locations, true, assetProvider, bundleDependencies, null, providerTypes);
            }
        }
    }

}
