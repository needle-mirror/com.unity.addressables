using System;
using System.Collections.Generic;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEditor.Build.Pipeline.Injector;

namespace UnityEditor.AddressableAssets
{
    internal class PostProcessBundlesTask : IBuildTask
    {
        const int k_Version = 1;
        public int Version { get { return k_Version; } }
#pragma warning disable 649
        [InjectContext(ContextUsage.In)]
        IAddressableAssetsBuildContext m_AABuildContext;

        [InjectContext]
        IBundleWriteData m_WriteData;
        [InjectContext]
        IBundleBuildResults m_BundleBuildResults;
#pragma warning restore 649

        public ReturnCode Run()
        {
            var aabc = m_AABuildContext as AddressableAssetsBuildContext;
            var aaSettings = aabc.m_settings;
            var runtimeData = aabc.m_runtimeData;
            var locations = aabc.m_locations;
            var assetGroupToBundle = aabc.m_assetGroupToBundles;
            foreach (var assetGroup in aaSettings.groups)
            {
                List<string> bundles;
                if (assetGroupToBundle.TryGetValue(assetGroup, out bundles))
                    assetGroup.Processor.PostProcessBundles(assetGroup, bundles, m_BundleBuildResults, m_WriteData, runtimeData, locations);
            }
            return ReturnCode.Success;
        }
    }
}