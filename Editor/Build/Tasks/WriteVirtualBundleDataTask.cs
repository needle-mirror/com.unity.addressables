using System;
using System.Collections.Generic;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEngine.ResourceManagement;
using UnityEngine.AddressableAssets;
using System.IO;
using System.Linq;
using UnityEditor.Build.Pipeline.Injector;
namespace UnityEditor.AddressableAssets
{
    internal class WriteVirtualBundleDataTask : IBuildTask
    {
        const int k_Version = 1;
        public int Version { get { return k_Version; } }
        bool m_SaveData;

#pragma warning disable 649
        [InjectContext(ContextUsage.In)]
        IAddressableAssetsBuildContext m_AABuildContext;

        [InjectContext]
        IBundleWriteData m_WriteData;
#pragma warning restore 649

        public WriteVirtualBundleDataTask(bool save)
        {
            m_SaveData = save;
        }

        public ReturnCode Run()
        {
            var aaContext = m_AABuildContext as AddressableAssetsBuildContext;

            var locations = aaContext.m_locations;

            aaContext.m_virtualBundleRuntimeData = new VirtualAssetBundleRuntimeData(ProjectConfigData.localLoadSpeed, ProjectConfigData.remoteLoadSpeed);
            var bundledAssets = new Dictionary<object, List<string>>();
            foreach (var loc in locations)
            {
                if (loc.Provider == typeof(BundledAssetProvider).FullName)
                {
                    if (loc.Dependencies == null || loc.Dependencies.Count == 0)
                        continue;
                    for (int i = 0; i < loc.Dependencies.Count; i++)
                    {
                        var dep = loc.Dependencies[i];
                        List<string> assetsInBundle = null;
                        if (!bundledAssets.TryGetValue(dep, out assetsInBundle))
                            bundledAssets.Add(dep, assetsInBundle = new List<string>());
                        if (i == 0) //only add the asset to the first bundle...
                            assetsInBundle.Add(loc.InternalId);
                    }
                }
            }

            foreach (var bd in bundledAssets)
            {
                var bundleLocData = locations.First(s => s.Keys[0] == bd.Key);
                var bundleData = new VirtualAssetBundle(bundleLocData.InternalId, !bundleLocData.InternalId.Contains("://"));

                long dataSize = 0;
                long headerSize = 0;
                foreach (var a in bd.Value)
                {
                    var size = ComputeSize(m_WriteData, a);
                    bundleData.Assets.Add(new VirtualAssetBundleEntry(a, size));
                    dataSize += size;
                    headerSize += (long)(a.Length * 5); //assume 5x path length overhead size per item, probably much less
                }
                if (bd.Value.Count == 0)
                {
                    dataSize = 100 * 1024;
                    headerSize = 1024;
                }
                bundleData.SetSize(dataSize, headerSize);
                aaContext.m_virtualBundleRuntimeData.AssetBundles.Add(bundleData);
            }
            if (m_SaveData)
                aaContext.m_virtualBundleRuntimeData.Save();
            return ReturnCode.Success;
        }

        private static long ComputeSize(IBundleWriteData writeData, string a)
        {
            var guid = AssetDatabase.AssetPathToGUID(a);
            if (string.IsNullOrEmpty(guid) || guid.Length < 2)
                return 1024;
            var path = string.Format("Library/metadata/{0}{1}/{2}", guid[0], guid[1], guid);
            if (!File.Exists(path))
                return 1024;
            return new FileInfo(path).Length;
        }
    }
}