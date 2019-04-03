using System;
using System.Collections.Generic;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Interfaces;

namespace UnityEditor.AddressableAssets
{
    public class PostProcessBundlesTask : IBuildTask
    {
        const int k_Version = 1;
        public int Version { get { return k_Version; } }

        static readonly Type[] k_RequiredTypes = { typeof(IAddressableAssetsBuildContext), typeof(IBundleWriteData), typeof(IBundleBuildResults) };
        public Type[] RequiredContextTypes { get { return k_RequiredTypes; } }

        public ReturnCode Run(IBuildContext context)
        {
            IProgressTracker tracker;
            context.TryGetContextObject(out tracker);
            return Run(context.GetContextObject<IAddressableAssetsBuildContext>(), context.GetContextObject<IBundleWriteData>(), context.GetContextObject<IBundleBuildResults>(), tracker);
        }

        public static ReturnCode Run(IAddressableAssetsBuildContext aaContext, IBundleWriteData writeData, IBundleBuildResults results, IProgressTracker tracker)
        {
            var aabc = aaContext as AddressableAssetsBuildContext;
            var aaSettings = aabc.m_settings;
            var runtimeData = aabc.m_runtimeData;
            var locations = aabc.m_locations;
            var assetGroupToBundle = aabc.m_assetGroupToBundles;
            foreach (var assetGroup in aaSettings.groups)
            {
                if (tracker != null)
                    tracker.UpdateInfo("Postprocessing asset group " + assetGroup.displayName);
                List<string> bundles;
                if (assetGroupToBundle.TryGetValue(assetGroup, out bundles))
                    assetGroup.processor.PostProcessBundles(aaSettings, assetGroup, bundles, results, writeData, runtimeData, locations);
            }
            return ReturnCode.Success;
        }
    }
}