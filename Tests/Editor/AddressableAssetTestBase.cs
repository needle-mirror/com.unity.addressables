using UnityEngine;
using NUnit.Framework;
using System.IO;

namespace UnityEditor.AddressableAssets.Tests
{
    public abstract class AddressableAssetTestBase
    {
        protected const string TestConfigName = "AddressableAssetSettings.Tests";
        protected const string TestConfigFolder = "Assets/AddressableAssetsData_AddressableAssetSettingsTests";
        protected AddressableAssetSettings m_settings;
        protected string assetGUID;
        protected virtual bool PersistSettings { get { return true; } }
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
            
            m_settings = AddressableAssetSettings.Create(TestConfigFolder, TestConfigName, true, PersistSettings);
            m_settings.labelTable.labelNames.Clear();
            GameObject testObject = new GameObject("TestObject");
#if UNITY_2018_3_OR_NEWER
            PrefabUtility.SaveAsPrefabAsset(testObject, TestConfigFolder + "/test.prefab");
#else
            PrefabUtility.CreatePrefab(TestConfigFolder + "/test.prefab", testObject);
#endif
            assetGUID = AssetDatabase.AssetPathToGUID(TestConfigFolder + "/test.prefab");
            OnInit();
        }

        protected virtual void OnInit() { }

        [OneTimeTearDown]
        public void Cleanup()
        {
            OnCleanup();
            if (Directory.Exists(TestConfigFolder))
                AssetDatabase.DeleteAsset(TestConfigFolder);
            EditorBuildSettings.RemoveConfigObject(TestConfigName);
        }

        protected virtual void OnCleanup()
        {
        }
    }
}