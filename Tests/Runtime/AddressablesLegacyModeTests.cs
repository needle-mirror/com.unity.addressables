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

public class AddressablesLegacyModeTests : AddressablesBaseTests
{
    protected override void CreateLocations(ResourceLocationMap locations)
    {
        ResourceManager.InstanceProvider = new InstanceProvider();
        ResourceManager.ResourceProviders.Add(new LegacyResourcesProvider());
        ResourceManager.SceneProvider = new SceneProvider();
        Addressables.ResourceLocators.Clear();

        object[] labels = new object[] {"label1", "label2", "label3", "label4", "label5", 1234, new Hash128(234,3456,55,22) };

        for (int i = 0; i < 20; i++)
        {
            HashSet<object> labelSet = new HashSet<object>();
            int count = UnityEngine.Random.Range(1, labels.Length);
            for (int l = 0; l < count; l++)
                labelSet.Add(labels[UnityEngine.Random.Range(1, labels.Length)]);
            object[] labelsArray = new object[labelSet.Count + 2];
            labelsArray[0] = "asset" + i;
            labelsArray[1] = GUID.Generate();
            labelSet.CopyTo(labelsArray, 2);
            AddLocation(locations, "Resources/", "asset" + i, "asset" + i, typeof(LegacyResourcesProvider), labelsArray);
        }
    }

}