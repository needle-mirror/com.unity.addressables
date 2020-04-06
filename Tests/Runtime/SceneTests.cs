#if UNITY_2019_3_OR_NEWER
using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.SceneManagement;
#endif
using UnityEngine;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace SceneTests
{
    abstract class SceneTests : AddressablesTestFixture
    {
        int m_StartingSceneCount;
        const int numScenes = 2;
        List<String> sceneKeys;
        const string prefabKey = "prefabKey";        
        public SceneTests()
        {
            sceneKeys = new List<string>();
            for(int i=0;i<numScenes;i++)
            {
                sceneKeys.Add("SceneTests_Scene" + i);
            }
        }
#if UNITY_EDITOR
        internal override void Setup(AddressableAssetSettings settings, string tempAssetFolder)
        { 
            var group = settings.CreateGroup("TestGroup", true, false, false, null, typeof(BundledAssetGroupSchema));

            // Create prefab
            var prefabGuid = CreatePrefab(Path.Combine(tempAssetFolder, String.Concat(prefabKey, ".prefab")));
            var prefabEntry = settings.CreateOrMoveEntry(prefabGuid, group, false, false);
            prefabEntry.address = Path.GetFileNameWithoutExtension(prefabEntry.AssetPath);

            // Create scenes
            for (int i = 0; i < numScenes; i++)
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Additive);
                EditorSceneManager.SaveScene(scene, Path.Combine(tempAssetFolder, String.Concat(sceneKeys[i], ".unity")));
                var guid = AssetDatabase.AssetPathToGUID(scene.path);
                var entry = settings.CreateOrMoveEntry(guid, group, false, false);
                entry.address = Path.GetFileNameWithoutExtension(entry.AssetPath);
            }
        }
        static string CreatePrefab(string assetPath)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            PrefabUtility.SaveAsPrefabAsset(go, assetPath);
            Object.DestroyImmediate(go, false);
            return AssetDatabase.AssetPathToGUID(assetPath);
        }
#endif
        [SetUp]
        public void SetUp()
        {
            m_StartingSceneCount = m_Addressables.SceneOperationCount;
        }

        [TearDown]
        public void TearDown()
        {
            Assert.AreEqual(m_StartingSceneCount, m_Addressables.SceneOperationCount);
        }

        IEnumerator UnloadSceneFromHandler(AsyncOperationHandle<SceneInstance> op)
        {
            string sceneName = op.Result.Scene.name;
            Assert.IsNotNull(sceneName);
            var unloadOp = m_Addressables.UnloadSceneAsync(op, false);
            yield return unloadOp;
            Assert.AreEqual(AsyncOperationStatus.Succeeded, unloadOp.Status);
            Assert.IsFalse(unloadOp.Result.Scene.isLoaded);
            m_Addressables.Release(unloadOp);
            Assert.IsNull(SceneManager.GetSceneByName(sceneName).name);
        }

        [UnityTest]
        public IEnumerator CanLoadMultipleScenesAdditively()
        {
            var op = m_Addressables.LoadSceneAsync(sceneKeys[0], LoadSceneMode.Additive);
            yield return op;
            Assert.AreEqual(AsyncOperationStatus.Succeeded, op.Status);
            Assert.AreEqual(sceneKeys[0], SceneManager.GetSceneByName(sceneKeys[0]).name);
            
            var op1 = m_Addressables.LoadSceneAsync(sceneKeys[1], LoadSceneMode.Additive);
            yield return op1;
            Assert.AreEqual(AsyncOperationStatus.Succeeded, op1.Status);
            Assert.AreEqual(sceneKeys[1], SceneManager.GetSceneByName(sceneKeys[1]).name);

            yield return UnloadSceneFromHandler(op);
            yield return UnloadSceneFromHandler(op1);
        }

        [UnityTest]
        public IEnumerator WhenSceneUnloaded_InstanitatedObjectsInThatSceneAreReleased()
        {
            var op = m_Addressables.LoadSceneAsync(sceneKeys[0], LoadSceneMode.Additive);
            yield return op;
            Assert.AreEqual(AsyncOperationStatus.Succeeded, op.Status);
            Assert.AreEqual(sceneKeys[0], SceneManager.GetSceneByName(sceneKeys[0]).name);
            SceneManager.SetActiveScene(op.Result.Scene);
            Assert.AreEqual(sceneKeys[0], SceneManager.GetActiveScene().name);

            var instOp = m_Addressables.InstantiateAsync(prefabKey);
            yield return instOp;
            Assert.AreEqual(AsyncOperationStatus.Succeeded,instOp.Status);
            Assert.AreEqual(sceneKeys[0], instOp.Result.scene.name);

            yield return UnloadSceneFromHandler(op);
            Assert.IsFalse(instOp.IsValid());
        }


        [UnityTest]
        public IEnumerator WhenSceneUnloadedWithSceneManager_InstanitatedObjectsInThatSceneAreReleased()
        {
            var op = m_Addressables.LoadSceneAsync(sceneKeys[0], LoadSceneMode.Additive);
            yield return op;
            Assert.AreEqual(AsyncOperationStatus.Succeeded, op.Status);
            Assert.AreEqual(sceneKeys[0], SceneManager.GetSceneByName(sceneKeys[0]).name);
            SceneManager.SetActiveScene(op.Result.Scene);
            Assert.AreEqual(sceneKeys[0], SceneManager.GetActiveScene().name);

            var instOp = m_Addressables.InstantiateAsync(prefabKey);
            yield return instOp;
            Assert.AreEqual(AsyncOperationStatus.Succeeded, instOp.Status);
            Assert.AreEqual(sceneKeys[0], instOp.Result.scene.name);

            var unloadOp = SceneManager.UnloadSceneAsync(op.Result.Scene);
            yield return unloadOp;
            Assert.IsTrue(unloadOp.isDone);
            Assert.IsNull(SceneManager.GetSceneByName(sceneKeys[0]).name);
            Assert.IsFalse(instOp.IsValid());
        }


        [UnityTest]
        public IEnumerator WhenSceneUnloaded_InstantiatedObjectsInOtherScenesAreNotReleased()
        {
            var op = m_Addressables.LoadSceneAsync(sceneKeys[0], LoadSceneMode.Additive);
            yield return op;
            Assert.AreEqual(AsyncOperationStatus.Succeeded, op.Status);
            Assert.AreEqual(sceneKeys[0], SceneManager.GetSceneByName(sceneKeys[0]).name);

            var activeScene = m_Addressables.LoadSceneAsync(sceneKeys[1], LoadSceneMode.Additive);
            yield return activeScene;
            Assert.AreEqual(AsyncOperationStatus.Succeeded, activeScene.Status);
            Assert.AreEqual(sceneKeys[1], SceneManager.GetSceneByName(sceneKeys[1]).name);
            SceneManager.SetActiveScene(activeScene.Result.Scene);
            Assert.AreEqual(sceneKeys[1], SceneManager.GetActiveScene().name);

            Assert.IsNull(GameObject.Find(prefabKey));
            var instOp = m_Addressables.InstantiateAsync(prefabKey);
            yield return instOp;
            Assert.AreEqual(AsyncOperationStatus.Succeeded, instOp.Status);
            Assert.AreEqual(sceneKeys[1], instOp.Result.scene.name);

            yield return UnloadSceneFromHandler(op);
            Assert.NotNull(GameObject.Find(instOp.Result.name));

            yield return UnloadSceneFromHandler(activeScene);
            Assert.IsFalse(instOp.IsValid());
        }

        /* Regression test for https://jira.unity3d.com/browse/ADDR-1032
         * 
         * Bug occurs when an instantiation happens after a previously completed instantiation.
         * The InstanceOperation is recycled from the previous instantiation, but its m_scene field is not cleaned.
         *
         * Test ensures that when instantiating a prefab and the InstanceOperation is recycled from a previous instantiation,
         * the m_Scene (field in InstanceOperation) should be null until the InstanceOperation is completed.  
         */
        [UnityTest]
        public IEnumerator WhenInstantiatingPrefab_AndOperationIsRecycled_SceneIsNullUntilCompletion()
        {
            // Previous instantiation
            var instOp = m_Addressables.InstantiateAsync(prefabKey);
            var internalInstanceOp1 = instOp.m_InternalOp;
            yield return instOp;
            instOp.Release();

            // InstanceOperation we want to test
            var instOp2 = m_Addressables.InstantiateAsync(prefabKey);
            var internalInstanceOp2 = (ResourceManager.InstanceOperation)instOp2.m_InternalOp;

            // Test
            Assert.False(internalInstanceOp2.IsDone, "InstanceOperation2 is not yet completed.");
            Assert.AreEqual(internalInstanceOp1, internalInstanceOp2, "The operation was not recycled");
            Assert.True(string.IsNullOrEmpty(internalInstanceOp2.InstanceScene().name), "Scene was not cleared from InstanceOperation");

            // Cleanup
            yield return internalInstanceOp2;
            instOp2.Release();
        }

        [UnityTest]
        public IEnumerator ActivateSceneAsync_ReturnsOperation()
        {
            var op = m_Addressables.LoadSceneAsync(sceneKeys[0], LoadSceneMode.Additive);
            yield return op;

            var activateScene = op.Result.ActivateAsync();
            yield return activateScene;

            Assert.AreEqual(op.Result.m_Operation, activateScene);

            yield return UnloadSceneFromHandler(op);
        }
    }

#if UNITY_EDITOR
    class SceneTests_FastMode : SceneTests { protected override TestBuildScriptMode BuildScriptMode { get { return TestBuildScriptMode.Fast; } } }

    class SceneTests_VirtualMode : SceneTests { protected override TestBuildScriptMode BuildScriptMode { get { return TestBuildScriptMode.Virtual; } } }

    class SceneTests_PackedPlaymodeMode : SceneTests { protected override TestBuildScriptMode BuildScriptMode { get { return TestBuildScriptMode.PackedPlaymode; } } }
#endif

    [UnityPlatform(exclude = new[] { RuntimePlatform.WindowsEditor, RuntimePlatform.OSXEditor, RuntimePlatform.LinuxEditor })]
    class SceneTests_PackedMode : SceneTests { protected override TestBuildScriptMode BuildScriptMode { get { return TestBuildScriptMode.Packed; } } }
}
#endif