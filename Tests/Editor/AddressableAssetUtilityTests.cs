using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build.Utilities;
using UnityEngine;
using UnityEngine.Audio;

namespace UnityEditor.AddressableAssets.Tests
{
    public class AddressableAssetUtilityTests : AddressableAssetTestBase
    {
        [Test]
        public void IsInResourcesProperlyHandlesCase()
        {
            Assert.IsTrue(AddressableAssetUtility.IsInResources("/rEsOurces/"));
            Assert.IsTrue(AddressableAssetUtility.IsInResources("/resources/"));
            Assert.IsTrue(AddressableAssetUtility.IsInResources("/RESOURCES/"));
        }

        [Test]
        public void IsInResourcesHandlesExtraPathing()
        {
            Assert.IsTrue(AddressableAssetUtility.IsInResources("path/path/resources/path"));
            Assert.IsTrue(AddressableAssetUtility.IsInResources("path/path/resources/"));
            Assert.IsTrue(AddressableAssetUtility.IsInResources("/resources/path"));
        }

        [Test]
        public void IsInResourcesHandlesResourcesInWrongContext()
        {
            Assert.IsFalse(AddressableAssetUtility.IsInResources("resources/"));
            Assert.IsFalse(AddressableAssetUtility.IsInResources("/resources"));
            Assert.IsFalse(AddressableAssetUtility.IsInResources("path/resourcesOther/path"));
            Assert.IsFalse(AddressableAssetUtility.IsInResources("/path/res/ources/path"));
        }

        [Test]
        public void IsPathValidBlocksCommonStrings()
        {
            Assert.IsFalse(AddressableAssetUtility.IsPathValidForEntry(string.Empty));
            Assert.IsFalse(AddressableAssetUtility.IsPathValidForEntry(CommonStrings.UnityEditorResourcePath));
            Assert.IsFalse(AddressableAssetUtility.IsPathValidForEntry(CommonStrings.UnityDefaultResourcePath));
            Assert.IsFalse(AddressableAssetUtility.IsPathValidForEntry(CommonStrings.UnityBuiltInExtraPath));
        }

        [Test]
        public void IsPathValidBlocksBadExtensions()
        {
            Assert.IsFalse(AddressableAssetUtility.IsPathValidForEntry("file.cs"));
            Assert.IsFalse(AddressableAssetUtility.IsPathValidForEntry("file.js"));
            Assert.IsFalse(AddressableAssetUtility.IsPathValidForEntry("file.boo"));
            Assert.IsFalse(AddressableAssetUtility.IsPathValidForEntry("file.exe"));
            Assert.IsFalse(AddressableAssetUtility.IsPathValidForEntry("file.dll"));
        }

        [Test]
        public void IsPathValidAllowsBasicTypes()
        {
            Assert.IsTrue(AddressableAssetUtility.IsPathValidForEntry("file.asset"));
            Assert.IsTrue(AddressableAssetUtility.IsPathValidForEntry("file.png"));
            Assert.IsTrue(AddressableAssetUtility.IsPathValidForEntry("file.bin"));
            Assert.IsTrue(AddressableAssetUtility.IsPathValidForEntry("file.txt"));
            Assert.IsTrue(AddressableAssetUtility.IsPathValidForEntry("file.prefab"));
            Assert.IsTrue(AddressableAssetUtility.IsPathValidForEntry("file.mat"));
            Assert.IsTrue(AddressableAssetUtility.IsPathValidForEntry("file.wav"));
            Assert.IsTrue(AddressableAssetUtility.IsPathValidForEntry("file.jpg"));
            Assert.IsTrue(AddressableAssetUtility.IsPathValidForEntry("file.avi"));
            Assert.IsTrue(AddressableAssetUtility.IsPathValidForEntry("file.controller"));
        }

        [Test]
        public void IsEditorTypeRemappedToNull()
        {
            Assert.IsNull(AddressableAssetUtility.MapEditorTypeToRuntimeType(typeof(UnityEditor.AssetImporter), false));
        }

        [Test]
        public void IsRuntimeTypeNotRemapped()
        {
            Assert.AreEqual(AddressableAssetUtility.MapEditorTypeToRuntimeType(typeof(UnityEngine.Vector3), false), typeof(UnityEngine.Vector3));
        }

        [Test]
        public void AreConvertableEditorAssemblyTypesConverted()
        {
            Assembly asm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.FullName == "UnityEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null");
            var conversionMapping = new Dictionary<Type, Type>()
            {
                { asm.GetType("UnityEditor.Audio.AudioMixerGroupController"), typeof(AudioMixerGroup) },
                { asm.GetType("UnityEditor.Audio.AudioMixerController"), typeof(AudioMixer) },
                { typeof(UnityEditor.SceneAsset),  typeof(UnityEngine.ResourceManagement.ResourceProviders.SceneInstance) },
                { typeof(UnityEditor.Animations.AnimatorController), typeof(RuntimeAnimatorController) }
            };

            foreach (Type key in conversionMapping.Keys)
            {
                var type = AddressableAssetUtility.MapEditorTypeToRuntimeType(key, false);
                Assert.AreEqual(type, conversionMapping[key]);
            }
        }

        [TestCase(1, TestName = "OneBundle")]
        [TestCase(5, TestName = "MultipleBundles")]
        [Test]
        public void AddressableAssetUtility_ConvertAssetBundlesToAddressables_CanConvertBundles(int numBundles)
        {
            // Setup
            var prevGroupCount = Settings.groups.Count;
            var testAssetGUIDs = new List<string>();
            for (int i = 0; i < numBundles; i++)
            {
                var testObject = new GameObject("TestObjectForBundles" + i);
#if UNITY_2018_3_OR_NEWER
                PrefabUtility.SaveAsPrefabAsset(testObject, ConfigFolder + "/testasset" + i + ".prefab");
#else
                PrefabUtility.CreatePrefab(k_TestConfigFolder + "/testasset" + i + ".prefab", testObject);
#endif
                testAssetGUIDs.Add(AssetDatabase.AssetPathToGUID(ConfigFolder + "/testasset" + i + ".prefab"));
                var importer = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(testAssetGUIDs[i]));
                importer.assetBundleName = "testAssetBundleName" + i;
                AssetDatabase.SaveAssets();
            }
            AddressableAssetSettingsDefaultObject.Settings = Settings;

            // Test
            AddressableAssetUtility.ConvertAssetBundlesToAddressables();
            Assert.AreEqual(prevGroupCount + numBundles, Settings.groups.Count);
            Assert.AreEqual(0, AssetDatabase.GetAllAssetBundleNames().Length);
            for (int i = 0; i < numBundles; i++)
            {
                Assert.NotNull(Settings.FindAssetEntry(testAssetGUIDs[i]));
            }

            // Cleanup
            for (int i = 0; i < numBundles; i++)
            {
                var lastGroupIndex = AddressableAssetSettingsDefaultObject.Settings.groups.Count - 1;
                AddressableAssetSettingsDefaultObject.Settings.RemoveGroup(AddressableAssetSettingsDefaultObject.Settings.groups[lastGroupIndex]);
            }
        }
    }
}
