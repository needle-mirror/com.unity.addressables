using System;
using System.IO;
using NUnit.Framework;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace UnityEditor.AddressableAssets.Tests
{
    public abstract class AddressableAssetTestBase
    {
        protected const string k_TestConfigName = "AddressableAssetSettings.Tests";
        protected string ConfigFolder => $"Assets/{GetType()}_Tests";
        protected string GetAssetPath(string assetName)
        {
            return $"{ConfigFolder}/{assetName}";
        }

        private AddressableAssetSettings m_Settings;

        protected AddressableAssetSettings Settings
        {
            get
            {
                if (m_Settings == null)
                    m_Settings = AddressableAssetSettings.Create(ConfigFolder, k_TestConfigName, true, PersistSettings);
                return m_Settings;
            }
        }
        protected string m_AssetGUID;
        protected string[] m_SceneGuids;

        protected virtual bool PersistSettings { get { return true; } }
        [OneTimeSetUp]
        public void Init()
        {
            //TODO: Remove when NSImage warning issue on bokken is fixed
            Application.logMessageReceived += CheckLogForWarning;

            if (Directory.Exists(ConfigFolder))
            {
                Debug.Log($"{GetType()} (init) - deleting {ConfigFolder}");
                AssetDatabase.DeleteAsset(ConfigFolder);
            }
            if (!Directory.Exists(ConfigFolder))
            {
                Debug.Log($"{GetType()} (init) - creating {ConfigFolder}");
                Directory.CreateDirectory(ConfigFolder);
                AssetDatabase.Refresh();
            }

            Settings.labelTable.labelNames.Clear();
            GameObject testObject = new GameObject("TestObject");
#if UNITY_2018_3_OR_NEWER
            PrefabUtility.SaveAsPrefabAsset(testObject, ConfigFolder + "/test.prefab");
#else
            PrefabUtility.CreatePrefab(k_TestConfigFolder + "/test.prefab", testObject);
#endif
            m_AssetGUID = AssetDatabase.AssetPathToGUID(ConfigFolder + "/test.prefab");

            string scene1Path = ConfigFolder + "/contentUpdateScene1.unity";
            string scene2Path = ConfigFolder + "/contentUpdateScene2.unity";
            string scene3Path = ConfigFolder + "/contentUpdateScene3.unity";

            Scene scene1 = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            EditorSceneManager.SaveScene(scene1, scene1Path);

            Scene scene2 = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            EditorSceneManager.SaveScene(scene2, scene2Path);

            Scene scene3 = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            EditorSceneManager.SaveScene(scene3, scene3Path);

            //Clear out the active scene so it doesn't affect tests
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);

            m_SceneGuids = new string[]
            {
                AssetDatabase.AssetPathToGUID(scene1Path),
                AssetDatabase.AssetPathToGUID(scene2Path),
                AssetDatabase.AssetPathToGUID(scene3Path)
            };

            OnInit();

            //TODO: Remove when NSImage warning issue on bokken is fixed
            //Removing here in the event we didn't recieve any messages during the setup, we can respond appropriately to
            //logs in the tests.
            Application.logMessageReceived -= CheckLogForWarning;
            if (resetFailingMessages)
                LogAssert.ignoreFailingMessages = false;
        }

        private bool resetFailingMessages = false;
        //TODO: Remove when NSImage warning issue on bokken is fixed
        private void CheckLogForWarning(string condition, string stackTrace, LogType type)
        {
            LogAssert.ignoreFailingMessages = true;
            resetFailingMessages = true;
        }

        protected virtual void OnInit() {}

        [OneTimeTearDown]
        public void Cleanup()
        {
            OnCleanup();
            if (Directory.Exists(ConfigFolder))
            {
                Debug.Log($"{GetType()} - (cleanup) deleting {ConfigFolder}");
                AssetDatabase.DeleteAsset(ConfigFolder);
            }
            EditorBuildSettings.RemoveConfigObject(k_TestConfigName);
        }

        protected virtual void OnCleanup()
        {
        }
    }
}
