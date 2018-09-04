using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.IO;
using UnityEngine.ResourceManagement;
using UnityEngine.AddressableAssets;
using System;

#if UNITY_EDITOR
using UnityEditor;
#endif

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

        object[] labels = new object[] { "label1", "label2", "label3", "label4", "label5", 1234, new Hash128(234, 3456, 55, 22) };
        for (int b = 0; b < 5; b++)
        {
            var isLocal = b % 2 == 0;
            var bundle = new VirtualAssetBundle("bundle" + b, isLocal);
            var bundleLocation = new ResourceLocationBase(bundle.Name, bundle.Name, typeof(AssetBundleProvider).FullName);
            for (int a = 0; a < 10; a++)
            {
                HashSet<object> labelSet = new HashSet<object>();
                int count = UnityEngine.Random.Range(1, labels.Length);
                for (int l = 0; l < count; l++)
                    labelSet.Add(labels[UnityEngine.Random.Range(1, labels.Length)]);
                object[] labelsArray = new object[labelSet.Count + 2];
                labelsArray[0] = "asset" + a;
                labelsArray[1] = GUID.Generate();
                labelSet.CopyTo(labelsArray, 2);

                var objectName = bundle.Name + "_asset" + a;
                var assetPath = RootFolder + "/" + objectName + ".prefab";
                CreateAsset(assetPath, objectName);

                var asset = new VirtualAssetBundleEntry(assetPath, UnityEngine.Random.Range(1024, 1024 * 1024));
                bundle.Assets.Add(asset);
                AddLocation(locations, new ResourceLocationBase(objectName, assetPath, typeof(BundledAssetProvider).FullName, bundleLocation, sharedBundleLocations[UnityEngine.Random.Range(0, sharedBundleLocations.Count)], sharedBundleLocations[UnityEngine.Random.Range(0, sharedBundleLocations.Count)]), labelSet);
            }
            bundle.OnAfterDeserialize();
            virtualBundleData.AssetBundles.Add(bundle);
        }
        new GameObject("AssetBundleSimulator", typeof(VirtualAssetBundleManager)).GetComponent<VirtualAssetBundleManager>().Initialize(virtualBundleData, (s) => s, 0, 0, 0, 0);
    }

}