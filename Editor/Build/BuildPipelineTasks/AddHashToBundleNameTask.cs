using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.Build.Content;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Injector;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEditor.Build.Pipeline.Utilities;
using UnityEditor.Build.Utilities;
using UnityEngine;

namespace UnityEditor.AddressableAssets.Build.BuildPipelineTasks
{
    /// <summary>
    /// The BuildTask used to append the asset hash to the internal bundle name. 
    /// </summary>
    public class AddHashToBundleNameTask : IBuildTask
    {
        /// <summary>
        /// The task version.
        /// </summary>
        public int Version { get { return 1; } }

#pragma warning disable 649
        [InjectContext(ContextUsage.In)]
        IBuildParameters m_Parameters;

        [InjectContext(ContextUsage.In)]
        IBundleBuildContent m_BuildContent;

        [InjectContext]
        IDependencyData m_DependencyData;
        [InjectContext(ContextUsage.InOut, true)]
        IBuildSpriteData m_SpriteData;

        [InjectContext(ContextUsage.In)]
        IAddressableAssetsBuildContext m_AaBuildContext;

        [InjectContext(ContextUsage.In, true)]
        IBuildCache m_Cache;
#pragma warning restore 649

        /// <summary>
        /// Runs the AddHashToBundleNameTask.
        /// </summary>
        /// <returns>Success.</returns>
        public ReturnCode Run()
        {
            var aa = m_AaBuildContext as AddressableAssetsBuildContext;
            var newBundleLayout = new Dictionary<string, List<GUID>>();
            foreach (var bid in m_BuildContent.BundleLayout)
            {
                var hash = GetAssetsHash(bid.Value);
                var newName = $"{bid.Key}_{hash}";
                newBundleLayout.Add(newName, bid.Value);
                string assetGroup;
                if (aa.bundleToAssetGroup.TryGetValue(bid.Key, out assetGroup))
                {
                    aa.bundleToAssetGroup.Remove(bid.Key);
                    aa.bundleToAssetGroup.Add(newName, assetGroup);
                }
            }
            m_BuildContent.BundleLayout.Clear();

            foreach (var bid in newBundleLayout)
                m_BuildContent.BundleLayout.Add(bid.Key, bid.Value);
            return ReturnCode.Success;
        }

        private RawHash GetAssetsHash(List<GUID> assets)
        {
            var hashes = new HashSet<Hash128>();
            foreach (var g in assets)
            {
                AssetLoadInfo assetInfo;
                if (m_DependencyData.AssetInfo.TryGetValue(g, out assetInfo))
                    GetAssetHashes(hashes, g, assetInfo.referencedObjects, m_Cache != null && m_Parameters.UseCache);
            }
            return HashingMethods.Calculate(hashes.ToArray());
        }

        void GetAssetHashes(HashSet<Hash128> hashes, GUID g, List<ObjectIdentifier> referencedObjects, bool useCache)
        {
            if (useCache)
            {
                hashes.Add(m_Cache.GetCacheEntry(g, Version).Hash);
                foreach (var reference in referencedObjects)
                    hashes.Add(m_Cache.GetCacheEntry(reference).Hash);
            }
            else
            {
                hashes.Add(AssetDatabase.GetAssetDependencyHash(AssetDatabase.GUIDToAssetPath(g.ToString())));
                foreach (var reference in referencedObjects)
                {
                    if (!reference.guid.Empty())
                    {
                        hashes.Add(AssetDatabase.GetAssetDependencyHash(AssetDatabase.GUIDToAssetPath(reference.guid.ToString())));
                    }
                    else
                    {
                        if (reference.filePath.Equals(CommonStrings.UnityBuiltInExtraPath, StringComparison.OrdinalIgnoreCase) || reference.filePath.Equals(CommonStrings.UnityDefaultResourcePath, StringComparison.OrdinalIgnoreCase))
                            hashes.Add(HashingMethods.Calculate(Application.unityVersion, reference.filePath).ToHash128());
                    }
                }
            }
        }
    }
}