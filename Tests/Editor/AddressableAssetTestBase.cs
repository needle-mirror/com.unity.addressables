using UnityEngine;
using NUnit.Framework;
using System.IO;

namespace UnityEditor.AddressableAssets.Tests
{
    public abstract class AddressableAssetTestBase
    {
        protected const string TestConfigName = "AddresableAssetSettings";
        protected const string TestConfigFolder = "Assets/AddressableAssetsData_AddressableAssetSettingsTests";
        protected AddressableAssetSettings settings;
        protected string assetGUID;

        [OneTimeSetUp]
        public void Init()
        {
            if (Directory.Exists(TestConfigFolder))
                AssetDatabase.DeleteAsset(TestConfigFolder);
            if (!Directory.Exists(TestConfigFolder))
            {
                Directory.CreateDirectory(TestConfigFolder);
                AssetDatabase.Refresh();
            }
            settings = AddressableAssetSettings.GetDefault(true, false, TestConfigFolder, TestConfigName);
            settings.labelTable.labelNames.Clear();
            GameObject testObject = new GameObject("TestObject");
            PrefabUtility.CreatePrefab(TestConfigFolder + "/test.prefab", testObject);
            assetGUID = AssetDatabase.AssetPathToGUID(TestConfigFolder + "/test.prefab");
            OnInit();
        }

        protected virtual void OnInit() { }

        [OneTimeTearDown]
        public void Cleanup()
        {
            if (Directory.Exists(TestConfigFolder))
                AssetDatabase.DeleteAsset(TestConfigFolder);
            OnCleanup();
        }

        protected virtual void OnCleanup() { }
    }
}