using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement;
using Random = UnityEngine.Random;
#if UNITY_EDITOR

#endif

#if UNITY_EDITOR
public class AddressablesFastModeTests : AddressablesBaseTests
{
    protected override void CreateLocations(ResourceLocationMap locations)
    {
        ResourceManager.InstanceProvider = new InstanceProvider();
        ResourceManager.ResourceProviders.Add(new AssetDatabaseProvider());
        ResourceManager.SceneProvider = new SceneProvider();
        Addressables.ResourceLocators.Clear();

        object[] labels = { "label1", "label2", "label3", "label4", "label5", 1234, new Hash128(234, 3456, 55, 22) };

        for (int i = 0; i < 20; i++)
        {
            HashSet<object> labelSet = new HashSet<object>();
            int count = Random.Range(1, labels.Length);
            for (int l = 0; l < count; l++)
                labelSet.Add(labels[Random.Range(1, labels.Length)]);
            object[] labelsArray = new object[labelSet.Count + 2];
            labelsArray[0] = "asset" + i;
            labelsArray[1] = Guid.NewGuid();
            labelSet.CopyTo(labelsArray, 2);
            AddLocation(locations, "", "asset" + i, RootFolder + "/asset" + i + ".prefab", typeof(AssetDatabaseProvider), labelsArray);
        }
    }
}
#endif
