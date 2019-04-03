using System;
using System.Collections.Generic;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Interfaces;

namespace UnityEditor.AddressableAssets
{
    public class GenerateLocationListsTask : IBuildTask
    {
        const int k_Version = 1;
        public int Version { get { return k_Version; } }

        static readonly Type[] k_RequiredTypes = { typeof(IAddressableAssetsBuildContext), typeof(IBundleWriteData), typeof(IBuildResults) };
        public Type[] RequiredContextTypes { get { return k_RequiredTypes; } }

        public ReturnCode Run(IBuildContext context)
        {
            return Run(context.GetContextObject<IAddressableAssetsBuildContext>(), context.GetContextObject<IBundleWriteData>(), context.GetContextObject<IBuildResults>());
        }

        public static ReturnCode Run(IAddressableAssetsBuildContext aaContext, IBundleWriteData writeData, IBuildResults results)
        {
            var aabc = aaContext as AddressableAssetsBuildContext;
            var aaSettings = aabc.m_settings;
            var locations = aabc.m_locations;
            var bundleToAssetGroup = aabc.m_bundleToAssetGroup;
            var bundleToAssets = new Dictionary<string, List<GUID>>();
            var assetsToBundles = new Dictionary<GUID, List<string>>();
            foreach (var k in writeData.AssetToFiles)
            {
                List<string> bundleList = new List<string>();
                assetsToBundles.Add(k.Key, bundleList);
                List<GUID> assetList = null;
                var bundle = writeData.FileToBundle[k.Value[0]];
                if (!bundleToAssets.TryGetValue(bundle, out assetList))
                    bundleToAssets.Add(bundle, assetList = new List<GUID>());
                if (!bundleList.Contains(bundle))
                    bundleList.Add(bundle);
                foreach (var file in k.Value)
                {
                    var fileBundle = writeData.FileToBundle[file];
                    if (!bundleList.Contains(fileBundle))
                        bundleList.Add(fileBundle);
                    if (!bundleToAssets.ContainsKey(fileBundle))
                        bundleToAssets.Add(fileBundle, new List<GUID>());
                }

                assetList.Add(k.Key);
            }
            var assetGroupToBundle = (aabc.m_assetGroupToBundles = new Dictionary<AddressableAssetGroup, List<string>>());
            foreach (var kvp in bundleToAssets)
            {
                AddressableAssetGroup assetGroup = null;
                if (!bundleToAssetGroup.TryGetValue(kvp.Key, out assetGroup))
                    assetGroup = aaSettings.DefaultGroup;
                List<string> bundles;
                if (!assetGroupToBundle.TryGetValue(assetGroup, out bundles))
                    assetGroupToBundle.Add(assetGroup, bundles = new List<string>());
                bundles.Add(kvp.Key);

                assetGroup.processor.CreateResourceLocationData(aaSettings, assetGroup, kvp.Key, kvp.Value, assetsToBundles, locations);
            }

            return ReturnCode.Success;
        }
    }
}