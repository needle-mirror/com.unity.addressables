using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.SceneManagement;

namespace UnityEditor.AddressableAssets.Tests
{
    public class AddressableAssetEntryTests : AddressableAssetTestBase
    {
        string guid;
        protected override void OnInit()
        {
            var path = k_TestConfigFolder + "/subObjectTest.asset";
            AssetDatabase.CreateAsset(UnityEngine.AddressableAssets.Tests.TestObject.Create("test"), path);

            AssetDatabase.AddObjectToAsset(UnityEngine.AddressableAssets.Tests.TestObject2.Create("test2"), path);
            AssetDatabase.AddObjectToAsset(UnityEngine.AddressableAssets.Tests.TestObject2.Create("test3"), path);
            AssetDatabase.AddObjectToAsset(UnityEngine.AddressableAssets.Tests.TestObject2.Create("test4"), path);
            AssetDatabase.AddObjectToAsset(UnityEngine.AddressableAssets.Tests.TestObject2.Create("test5"), path);
            AssetDatabase.SaveAssets();

            guid = AssetDatabase.AssetPathToGUID(path);
            Settings.CreateOrMoveEntry(guid, Settings.DefaultGroup);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        }

        [Test]
        public void CreateCatelogEntries_WhenObjectHasMultipleSubObjectWithSameType_OnlyOneSubEntryIsCreated()
        {
            var e = Settings.DefaultGroup.GetAssetEntry(guid);
            var entries = new List<ContentCatalogDataEntry>();
            var providerTypes = new HashSet<Type>();
            e.CreateCatalogEntries(entries, false, "doesntMatter", null, null, providerTypes);
            Assert.AreEqual(2, entries.Count);
        }

        [Test]
        public void WhenClassReferencedByAddressableAssetEntryIsReloaded_CachedMainAssetTypeIsReset()
        {
            // Setup
            var path = k_TestConfigFolder + "/resetCachedMainAssetTypeTestGroup.asset";
            AddressableAssetGroup group = ScriptableObject.CreateInstance<AddressableAssetGroup>();
            AddressableAssetEntry entry = new AddressableAssetEntry(guid, "address", null, false);
            group.AddAssetEntry(entry);

            Assert.IsNull(entry.m_cachedMainAssetType);
            Assert.AreEqual(typeof(UnityEngine.AddressableAssets.Tests.TestObject), entry.MainAssetType);

            // Test
            AssetDatabase.CreateAsset(group, path);
            Resources.UnloadAsset(group);

            var reloadedGroup = AssetDatabase.LoadAssetAtPath<AddressableAssetGroup>(path);
            var reloadedEntry = reloadedGroup.GetAssetEntry(guid);
            Assert.IsNull(reloadedEntry.m_cachedMainAssetType);

            // Cleanup
            AssetDatabase.DeleteAsset(path);
        }

        [Test]
        public void GatherAllAssetReferenceDrawableEntries_ReturnsBuiltInScenes()
        {
            //Setup
            string scenePath = "TestScenePath";
            var savedCache = BuiltinSceneCache.scenes;
            BuiltinSceneCache.scenes = new EditorBuildSettingsScene[]
            {
                new EditorBuildSettingsScene(scenePath, true)
            };
            AddressableAssetEntry entry = new AddressableAssetEntry(AddressableAssetEntry.EditorSceneListName, "EditorSceneList", null, false);

            //Test
            List<IReferenceEntryData> results = new List<IReferenceEntryData>();
            entry.GatherAllAssetReferenceDrawableEntries(results);

            //Assert
            Assert.AreEqual(0, results.Count);

            //Cleanup
            BuiltinSceneCache.scenes = savedCache;
        }

        [Test]
        public void GatherAllAssetReferenceDrawableEntries_DoesNotReturnResources()
        {
            //Setup
            string scenePath = "TestScenePath";
            var savedCache = BuiltinSceneCache.scenes;
            BuiltinSceneCache.scenes = new EditorBuildSettingsScene[]
            {
                new EditorBuildSettingsScene(scenePath, true)
            };
            AddressableAssetEntry entry = new AddressableAssetEntry(AddressableAssetEntry.ResourcesName, "Resources", null, false);

            //Test
            List<IReferenceEntryData> results = new List<IReferenceEntryData>();
            entry.GatherAllAssetReferenceDrawableEntries(results);

            //Assert
            Assert.AreEqual(0, results.Count);

            //Cleanup
            BuiltinSceneCache.scenes = savedCache;
        }

        [Test]
        public void GatherAllAssetReferenceDrawableEntries_ReturnsFolderSubAssets()
        {
            //Setup
            string testAssetFolder = k_TestConfigFolder + "/TestFolder";
            string testAssetSubFolder = Path.Combine(testAssetFolder, "SubFolder");

            string mainPrefabPath = Path.Combine(testAssetFolder, "mainFolder.prefab").Replace('\\', '/');
            string subPrefabPath = Path.Combine(testAssetSubFolder, "subFolder.prefab").Replace('\\', '/');

            Directory.CreateDirectory(testAssetFolder);
            PrefabUtility.SaveAsPrefabAsset(new GameObject("mainFolderAsset"),mainPrefabPath);

            Directory.CreateDirectory(testAssetSubFolder);
            PrefabUtility.SaveAsPrefabAsset(new GameObject("subFolderAsset"), subPrefabPath);

            //Test
            AddressableAssetEntry entry = new AddressableAssetEntry(AssetDatabase.AssetPathToGUID(testAssetFolder), "Folder", null, false);
            List<IReferenceEntryData> results = new List<IReferenceEntryData>();
            entry.GatherAllAssetReferenceDrawableEntries(results);

            //Assert
            Assert.AreEqual(2, results.Count);
            Assert.AreEqual(mainPrefabPath, results[0].AssetPath);
            Assert.AreEqual(subPrefabPath, results[1].AssetPath);

            //Cleanup
            Directory.Delete(testAssetFolder, true);
        }

        [Test]
        public void GatherAllAssetReferenceDrawableEntries_DoesNotReturnScenesInFolder_IfSceneIsInBuiltInScenes()
        {
            //Setup
            string testAssetFolder = k_TestConfigFolder + "/TestFolder";
            string testAssetSubFolder = Path.Combine(testAssetFolder, "SubFolder");
            Directory.CreateDirectory(testAssetFolder);
            Directory.CreateDirectory(testAssetSubFolder);

            AssetDatabase.ImportAsset(testAssetFolder);
            AssetDatabase.ImportAsset(testAssetSubFolder);

            string mainPrefabPath = Path.Combine(testAssetFolder, "mainFolder.prefab").Replace('\\', '/');
            string subPrefabPath = Path.Combine(testAssetSubFolder, "subFolder.prefab").Replace('\\', '/');
            string scenePath = Path.Combine(testAssetFolder, "TestScenePath.unity").Replace('\\', '/'); 

            var savedCache = BuiltinSceneCache.scenes;
            BuiltinSceneCache.scenes = new EditorBuildSettingsScene[]
            {
                new EditorBuildSettingsScene(scenePath, true)
            };

            PrefabUtility.SaveAsPrefabAsset(new GameObject("mainFolderAsset"), mainPrefabPath);
            PrefabUtility.SaveAsPrefabAsset(new GameObject("subFolderAsset"), subPrefabPath);
            EditorSceneManager.SaveScene(EditorSceneManager.NewScene(NewSceneSetup.EmptyScene), scenePath);

            //Test
            AddressableAssetEntry entry = new AddressableAssetEntry(AssetDatabase.AssetPathToGUID(testAssetFolder), "Folder", null, false);
            List<IReferenceEntryData> results = new List<IReferenceEntryData>();
            entry.GatherAllAssetReferenceDrawableEntries(results);

            //Assert
            Assert.AreEqual(2, results.Count);
            Assert.AreEqual(mainPrefabPath, results[0].AssetPath);
            Assert.AreEqual(subPrefabPath, results[1].AssetPath);

            //Cleanup
            Directory.Delete(testAssetFolder, true);
            BuiltinSceneCache.scenes = savedCache;
        }

        [Test]
        public void GatherAllAssetReferenceDrawableEntries_AddsSimpleAssetEntries()
        {
            AddressableAssetEntry entry = new AddressableAssetEntry("12345698655", "Entry", null, false);
            entry.m_cachedAssetPath = "TestPath";

            List<IReferenceEntryData> results = new List<IReferenceEntryData>();
            entry.GatherAllAssetReferenceDrawableEntries(results);

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(entry.AssetPath, results[0].AssetPath);
        }

        [Test]
        public void GatherAllAssetReferenceDrawableEntries_AddsAllEntries_FromAddressableAssetEntryCollection()
        {
            //Setup
            string testAssetFolder = k_TestConfigFolder + "/TestFolder";
            string collectionPath = Path.Combine(testAssetFolder, "collection.asset").Replace('\\', '/');
            Directory.CreateDirectory(testAssetFolder);

            var collection = ScriptableObject.CreateInstance<AddressableAssetEntryCollection>();
            AddressableAssetEntry entry = new AddressableAssetEntry("12345698655", "Entry", null, false);
            entry.m_cachedAssetPath = "TestPath";
            collection.Entries.Add(entry);

            AssetDatabase.CreateAsset(collection, collectionPath);

            AddressableAssetEntry collectionEntry = new AddressableAssetEntry("", "collection", null, false);
            collectionEntry.m_cachedMainAssetType = typeof(AddressableAssetEntryCollection);
            collectionEntry.m_cachedAssetPath = collectionPath;

            //Test
            List<IReferenceEntryData> results = new List<IReferenceEntryData>();
            collectionEntry.GatherAllAssetReferenceDrawableEntries(results);

            //Assert
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(entry.AssetPath, results[0].AssetPath);

            //Cleanup
            Directory.Delete(testAssetFolder, true);
        }
    }
} 