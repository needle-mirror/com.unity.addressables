using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;

#if UNITY_EDITOR
using UnityEngine.ResourceManagement.ResourceProviders.Simulation;
#endif

#if UNITY_EDITOR
namespace UnityEngine.ResourceManagement.Tests
{
    public class ResourceManagerVirtualModeTests : ResourceManagerBaseTests
    {
        VirtualAssetBundleRuntimeData virtualBundleData = null;
        List<IResourceLocation> sharedBundleLocations = null;
        Dictionary<string, VirtualAssetBundle> bundleMap = null;
        const int kBundleCount = 10;
        protected override IResourceLocation CreateLocationForAsset(string name, string path)
        {
            if (virtualBundleData == null)
            {
                virtualBundleData = new VirtualAssetBundleRuntimeData();
                sharedBundleLocations = new List<IResourceLocation>();
                bundleMap = new Dictionary<string, VirtualAssetBundle>();
                for (int i = 0; i < kBundleCount; i++)
                {
                    var bundleName = "shared" + i;
                    var b = new VirtualAssetBundle("shared" + i, i % 2 == 0, 0, "");
                    virtualBundleData.AssetBundles.Add(b);
                    bundleMap.Add(b.Name, b);
                    sharedBundleLocations.Add(new ResourceLocationBase(bundleName, bundleName, typeof(AssetBundleProvider).FullName));
                }
            }
            IResourceLocation bundle = sharedBundleLocations[Random.Range(0, sharedBundleLocations.Count)];
            VirtualAssetBundle vBundle = bundleMap[bundle.InternalId];
            vBundle.Assets.Add(new VirtualAssetBundleEntry(path, Random.Range(1024, 1024 * 1024)));
            IResourceLocation dep1Location = sharedBundleLocations[Random.Range(0, sharedBundleLocations.Count)];
            IResourceLocation dep2Location = sharedBundleLocations[Random.Range(0, sharedBundleLocations.Count)];
            return new ResourceLocationBase(name, path, typeof(BundledAssetProvider).FullName, bundle, dep1Location, dep2Location);
        }

        protected override void ProcessLocations(List<IResourceLocation> locations)
        {
            if (virtualBundleData != null)
            {
                foreach (var b in virtualBundleData.AssetBundles)
                {
                    b.SetSize(2048, 1024);
                    b.OnAfterDeserialize();
                }
                m_ResourceManager.ResourceProviders.Insert(0, new CachedProvider(new VirtualAssetBundleProvider(virtualBundleData), typeof(AssetBundleProvider).FullName));
                m_ResourceManager.ResourceProviders.Insert(0, new CachedProvider(new VirtualBundledAssetProvider(), typeof(BundledAssetProvider).FullName));
            }
        }
    }
}
#endif

