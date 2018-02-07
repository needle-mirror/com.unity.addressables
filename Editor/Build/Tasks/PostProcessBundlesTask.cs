using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Interfaces;

namespace UnityEditor.AddressableAssets
{
    public struct PostProcessBundlesTask : IBuildTask
    {
        const int k_Version = 1;
        public int Version { get { return k_Version; } }

        static readonly Type[] k_RequiredTypes = { typeof(IAddressableAssetsBuildContext), typeof(IBundleWriteData), typeof(IBuildResults) };
        public Type[] RequiredContextTypes { get { return k_RequiredTypes; } }

        public ReturnCodes Run(IBuildContext context)
        {
            IProgressTracker tracker;
            context.TryGetContextObject(out tracker);
            return Run(context.GetContextObject<IAddressableAssetsBuildContext>(), context.GetContextObject<IBundleWriteData>(), context.GetContextObject<IBuildResults>(), tracker);
        }

        public static ReturnCodes Run(IAddressableAssetsBuildContext aaContext, IBundleWriteData writeData, IBuildResults results, IProgressTracker tracker)
        {
            var aabc = aaContext as AddressableAssetsBuildContext;
            var aaSettings = aabc.m_settings;
            var runtimeData = aabc.m_runtimeData;
            var assetGroupToBundle = aabc.m_assetGroupToBundles;
            foreach (var assetGroup in aaSettings.groups)
            {
                if (tracker != null)
                    tracker.UpdateInfo("Postprocessing asset group " + assetGroup.displayName);
                List<string> bundles;
                if (assetGroupToBundle.TryGetValue(assetGroup, out bundles))
                    assetGroup.processor.PostProcessBundles(aaSettings, assetGroup, bundles, results, writeData, runtimeData);
            }
            return ReturnCodes.Success;
        }
    }
}