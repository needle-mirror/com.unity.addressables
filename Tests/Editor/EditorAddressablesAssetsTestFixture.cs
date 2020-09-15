using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityEditor.AddressableAssets.Tests
{
    class EditorAddressableAssetsTestFixture
    {
        protected AddressableAssetSettings m_Settings;

        protected const string TempPath = "Assets/TempGen";

        [SetUp]
        public void Setup()
        {
            if (Directory.Exists(TempPath))
            {
                Directory.Delete(TempPath, true);
                File.Delete(TempPath + ".meta");
            }
            Directory.CreateDirectory(TempPath);

            m_Settings = AddressableAssetSettings.Create(Path.Combine(TempPath, "Settings"), "AddressableAssetSettings.Tests", true, true);
        }

        [TearDown]
        public void Teardown()
        {
            // Many of the tests keep recreating assets in the same path, so we need to unload them completely so they don't get reused by the next test
            AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(m_Settings));
            Resources.UnloadAsset(m_Settings);
            if (Directory.Exists(TempPath))
            {
                Directory.Delete(TempPath, true);
                File.Delete(TempPath + ".meta");
            }
            AssetDatabase.Refresh();
        }

        protected static string CreateAsset(string assetPath, string objectName)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = objectName;
            //this is to ensure that bundles are different for every run.
            go.transform.localPosition = UnityEngine.Random.onUnitSphere;
            PrefabUtility.SaveAsPrefabAsset(go, assetPath);
            UnityEngine.Object.DestroyImmediate(go, false);
            return AssetDatabase.AssetPathToGUID(assetPath);
        }

        protected static string CreateScene(string scenePath, string sceneName)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            scene.name = sceneName;
            EditorSceneManager.SaveScene(scene, scenePath);

            //Clear out the active scene so it doesn't affect tests
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);

            var list = new List<EditorBuildSettingsScene>() { new EditorBuildSettingsScene(scenePath, true)};
            SceneManagerState.AddScenesForPlayMode(list);
            return AssetDatabase.AssetPathToGUID(scenePath);
        }
    }
}
