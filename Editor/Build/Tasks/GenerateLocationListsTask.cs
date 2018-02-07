using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Interfaces;

namespace UnityEditor.AddressableAssets
{
    public struct GenerateLocationListsTask : IBuildTask
    {
        const int k_Version = 1;
        public int Version { get { return k_Version; } }

        static readonly Type[] k_RequiredTypes = { typeof(IAddressableAssetsBuildContext), typeof(IBundleWriteData), typeof(IBuildResults) };
        public Type[] RequiredContextTypes { get { return k_RequiredTypes; } }

        public ReturnCodes Run(IBuildContext context)
        {
            return Run(context.GetContextObject<IAddressableAssetsBuildContext>(), context.GetContextObject<IBundleWriteData>(), context.GetContextObject<IBuildResults>());
        }

        public static ReturnCodes Run(IAddressableAssetsBuildContext aaContext, IBundleWriteData writeData, IBuildResults results)
        {
            var aabc = aaContext as AddressableAssetsBuildContext;
            var aaSettings = aabc.m_settings;
            var contentCatalog = aabc.m_contentCatalog;
            var bundleToAssetGroup = aabc.m_bundleToAssetGroup;
            var bundleToAssets = new Dictionary<string, List<GUID>>();
            var assetsToBundles = new Dictionary<GUID, List<string>>();
            foreach (var k in writeData.AssetToFiles)
            {
                List<string> bundleList = new List<string>();
                assetsToBundles.Add(k.Key, bundleList);
                List<GUID> assetList = null;
                foreach (var f in k.Value)
                {
                    var bundle = writeData.FileToBundle[f];
                    if (!bundleToAssets.TryGetValue(bundle, out assetList))
                        bundleToAssets.Add(bundle, assetList = new List<GUID>());
                    if (!bundleList.Contains(bundle))
                        bundleList.Add(bundle);
                }
                assetList.Add(k.Key);
            }
            var assetGroupToBundle = (aabc.m_assetGroupToBundles = new Dictionary<AddressableAssetSettings.AssetGroup, List<string>>());
            foreach (var kvp in bundleToAssets)
            {
                AddressableAssetSettings.AssetGroup assetGroup = null;
                if (!bundleToAssetGroup.TryGetValue(kvp.Key, out assetGroup))
                    continue;
                List<string> bundles;
                if (!assetGroupToBundle.TryGetValue(assetGroup, out bundles))
                    assetGroupToBundle.Add(assetGroup, bundles = new List<string>());
                bundles.Add(kvp.Key);

                assetGroup.processor.CreateResourceLocationData(aaSettings, assetGroup, kvp.Key, kvp.Value, assetsToBundles, contentCatalog.locations);
            }

            return ReturnCodes.Success;
        }
    }
}