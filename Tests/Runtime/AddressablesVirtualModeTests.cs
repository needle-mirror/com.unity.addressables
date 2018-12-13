using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement;
using Random = UnityEngine.Random;
#if UNITY_EDITOR
using UnityEditor;
#endif

#if UNITY_EDITOR
public class AddressablesVirtualModeTests : AddressablesBaseTests
{
    protected override void CreateLocations(ResourceLocationMap locations)
    {
        ResourceManager.InstanceProvider = new InstanceProvider();
        ResourceManager.ResourceProviders.Add(new LegacyResourcesProvider());
        ResourceManager.SceneProvider = new SceneProvider();
        Addressables.ResourceLocators.Clear();


        var virtualBundleData = new VirtualAssetBundleRuntimeData();

        var sharedBundles = new List<VirtualAssetBundle>();
        var sharedBundleLocations = new List<IResourceLocation>();
        for (int i = 0; i < 10; i++)
        {
            var bundleName = "shared" + i;
            sharedBundles.Add(new VirtualAssetBundle("shared" + i, i % 2 == 0));
            sharedBundleLocations.Add(new ResourceLocationBase(bundleName, bundleName, typeof(AssetBundleProvider).FullName));
        }
        virtualBundleData.AssetBundles.AddRange(sharedBundles);

        object[] labels = { "label1", "label2", "label3", "label4", "label5", 1234, new Hash128(234, 3456, 55, 22) };
        for (int b = 0; b < 5; b++)
        {
            var isLocal = b % 2 == 0;
            var bundle = new VirtualAssetBundle("bundle" + b, isLocal);
            var bundleLocation = new ResourceLocationBase(bundle.Name, bundle.Name, typeof(AssetBundleProvider).FullName);
            for (int a = 0; a < 10; a++)
            {
                HashSet<object> labelSet = new HashSet<object>();
                int count = Random.Range(1, labels.Length);
                for (int l = 0; l < count; l++)
                    labelSet.Add(labels[Random.Range(1, labels.Length)]);
                object[] labelsArray = new object[labelSet.Count + 2];
                labelsArray[0] = "asset" + a;
                labelsArray[1] = GUID.Generate();
                labelSet.CopyTo(labelsArray, 2);

                var objectName = bundle.Name + "_asset" + a;
                var assetPath = RootFolder + "/" + objectName + ".prefab";
                CreateAsset(assetPath, objectName);

                var asset = new VirtualAssetBundleEntry(assetPath, Random.Range(1024, 1024 * 1024));
                bundle.Assets.Add(asset);
                AddLocation(locations, new ResourceLocationBase(objectName, assetPath, typeof(BundledAssetProvider).FullName, bundleLocation, sharedBundleLocations[Random.Range(0, sharedBundleLocations.Count)], sharedBundleLocations[Random.Range(0, sharedBundleLocations.Count)]), labelSet);
            }
            bundle.OnAfterDeserialize();
            virtualBundleData.AssetBundles.Add(bundle);
        }
        var abManager = new GameObject("AssetBundleSimulator", typeof(VirtualAssetBundleManager)).GetComponent<VirtualAssetBundleManager>();
        abManager.Initialize(virtualBundleData, s => s);
        ResourceManager.ResourceProviders.Insert(0, new CachedProvider(new VirtualAssetBundleProvider(abManager, typeof(AssetBundleProvider).FullName)));
        ResourceManager.ResourceProviders.Insert(0, new CachedProvider(new VirtualBundledAssetProvider()));
    }
}
#endif
