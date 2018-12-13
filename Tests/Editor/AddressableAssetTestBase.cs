using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace UnityEditor.AddressableAssets.Tests
{
    public abstract class AddressableAssetTestBase
    {
        protected const string k_TestConfigName = "AddressableAssetSettings.Tests";
        protected const string k_TestConfigFolder = "Assets/AddressableAssetsData_AddressableAssetSettingsTests";
        protected AddressableAssetSettings m_Settings;
        protected string m_AssetGUID;
        protected virtual bool PersistSettings { get { return true; } }
        [OneTimeSetUp]
        public void Init()
        {

            if (Directory.Exists(k_TestConfigFolder))
                AssetDatabase.DeleteAsset(k_TestConfigFolder);
            if (!Directory.Exists(k_TestConfigFolder))
            {
                Directory.CreateDirectory(k_TestConfigFolder);
                AssetDatabase.Refresh();
            }

            m_Settings = AddressableAssetSettings.Create(k_TestConfigFolder, k_TestConfigName, true, PersistSettings);
            m_Settings.labelTable.labelNames.Clear();
            GameObject testObject = new GameObject("TestObject");
#if UNITY_2018_3_OR_NEWER
            PrefabUtility.SaveAsPrefabAsset(testObject, k_TestConfigFolder + "/test.prefab");
#else
            PrefabUtility.CreatePrefab(k_TestConfigFolder + "/test.prefab", testObject);
#endif
            m_AssetGUID = AssetDatabase.AssetPathToGUID(k_TestConfigFolder + "/test.prefab");
            OnInit();
        }

        protected virtual void OnInit() { }

        [OneTimeTearDown]
        public void Cleanup()
        {
            OnCleanup();
            if (Directory.Exists(k_TestConfigFolder))
                AssetDatabase.DeleteAsset(k_TestConfigFolder);
            EditorBuildSettings.RemoveConfigObject(k_TestConfigName);
        }

        protected virtual void OnCleanup()
        {
        }
    }
}