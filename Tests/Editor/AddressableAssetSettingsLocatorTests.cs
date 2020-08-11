using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AddressableAssets.ResourceLocators;

namespace UnityEditor.AddressableAssets.Tests
{
    public class AddressableAssetSettingsLocatorTests
    {
        AddressableAssetSettings m_Settings;
        const string TempPath = "TempGen";
        string GetPath(string a) => $"Assets/{TempPath}/{a}";
        EditorBuildSettingsScene[] m_PreviousScenes;

        [SetUp]
        public void Setup()
        {
            if (AssetDatabase.IsValidFolder($"Assets/{TempPath}"))
            {
                AssetDatabase.DeleteAsset($"Assets/{TempPath}");
                AssetDatabase.Refresh();
            }
            AssetDatabase.CreateFolder("Assets", "TempGen");
            m_PreviousScenes = EditorBuildSettings.scenes;
            m_Settings = AddressableAssetSettings.Create(GetPath("Settings"), "AddressableAssetSettings.Tests", true, true);
            AssetDatabase.Refresh();
        }

        [TearDown]
        public void Teardown()
        {
            AssetDatabase.Refresh();
            // Many of the tests keep recreating assets in the same path, so we need to unload them completely so they don't get reused by the next test
            AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(m_Settings));
            Resources.UnloadAsset(m_Settings);
            if (AssetDatabase.IsValidFolder($"Assets/{TempPath}"))
                AssetDatabase.DeleteAsset($"Assets/{TempPath}");
            EditorBuildSettings.scenes = m_PreviousScenes;
            AssetDatabase.Refresh();
        }

        string CreateAsset(string assetName, string path)
        {
            AssetDatabase.CreateAsset(UnityEngine.AddressableAssets.Tests.TestObject.Create(assetName), path);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            return AssetDatabase.AssetPathToGUID(path);
        }

        string CreateFolder(string folderName, string[] assetNames)
        {
            var path = GetPath(folderName);
            Directory.CreateDirectory(path);
            foreach (var a in assetNames)
                CreateAsset(a, Path.Combine(path, a));
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            return AssetDatabase.AssetPathToGUID(path);
        }

        void AssertLocateResult(IResourceLocator locator, object key, Type type, params string[] expectedInternalIds)
        {
            Assert.IsTrue(locator.Locate(key, type, out var locations));
            Assert.IsNotNull(locations);
            Assert.AreEqual(expectedInternalIds.Length, locations.Count);
            foreach (var e in expectedInternalIds)
                Assert.NotNull(locations.FirstOrDefault(s => s.InternalId == e), $"Locations do not contain entry with internal id of {e}");
        }

        [Test]
        public void WhenLocatorWithSingleAsset_LocateWithAddressReturnsSingleLocation()
        {
            var path = GetPath("asset1.asset");
            m_Settings.CreateOrMoveEntry(CreateAsset("asset1", path), m_Settings.DefaultGroup).address = "address1";
            AssertLocateResult(new AddressableAssetSettingsLocator(m_Settings), "address1", null, path);
        }

        [Test]
        public void WhenLocatorWithSingleAsset_LocateWithInvalidKeyReturnsFalse()
        {
            m_Settings.CreateOrMoveEntry(CreateAsset("asset1", GetPath("asset1.asset")), m_Settings.DefaultGroup).address = "address1";
            var locator = new AddressableAssetSettingsLocator(m_Settings);
            Assert.IsFalse(locator.Locate("invalid", null, out var locs));
            Assert.IsNull(locs);
        }

        [Test]
        public void WhenLocatorWithSingleAsset_LocateWithGuidReturnsSingleLocation()
        {
            var guid = CreateAsset("asset1", GetPath("asset1.asset"));
            m_Settings.CreateOrMoveEntry(guid, m_Settings.DefaultGroup).address = "address1";
            AssertLocateResult(new AddressableAssetSettingsLocator(m_Settings), guid, null, GetPath("asset1.asset"));
        }

        [Test]
        public void WhenLocatorWithMultipeAssets_LocateWithAddressReturnsSingleLocation()
        {
            m_Settings.CreateOrMoveEntry(CreateAsset("asset1", GetPath("asset1.asset")), m_Settings.DefaultGroup).address = "address1";
            m_Settings.CreateOrMoveEntry(CreateAsset("asset2", GetPath("asset2.asset")), m_Settings.DefaultGroup).address = "address2";
            AssertLocateResult(new AddressableAssetSettingsLocator(m_Settings), "address1", null, GetPath("asset1.asset"));
        }

        [Test]
        public void WhenLocatorWithMultipeAssets_LocateWithSharedAddressReturnsMultipleLocations()
        {
            m_Settings.CreateOrMoveEntry(CreateAsset("asset1", GetPath("asset1.asset")), m_Settings.DefaultGroup).address = "address1";
            m_Settings.CreateOrMoveEntry(CreateAsset("asset2", GetPath("asset2.asset")), m_Settings.DefaultGroup).address = "address1";
            AssertLocateResult(new AddressableAssetSettingsLocator(m_Settings), "address1", null, GetPath("asset1.asset"), GetPath("asset2.asset"));
        }

        [Test]
        public void WhenLocatorWithMultipeAssets_LocateWithSharedLabelReturnsMultipleLocations()
        {
            m_Settings.CreateOrMoveEntry(CreateAsset("asset1", GetPath("asset1.asset")), m_Settings.DefaultGroup).SetLabel("label", true, true);
            m_Settings.CreateOrMoveEntry(CreateAsset("asset2", GetPath("asset2.asset")), m_Settings.DefaultGroup).SetLabel("label", true, true);
            AssertLocateResult(new AddressableAssetSettingsLocator(m_Settings), "label", null, GetPath("asset1.asset"), GetPath("asset2.asset"));
        }

        [Test]
        public void WhenLocatorWithAssetsThatMatchAssetsInFolderAndResources_LocateAllMatches()
        {
            CreateFolder("Resources/TestFolder", new string[] { "asset1.asset"});
            var folderGUID = CreateFolder("TestFolder", new string[] { "asset1" });
            m_Settings.CreateOrMoveEntry(folderGUID, m_Settings.DefaultGroup).address = "TestFolder";
            var assetGUID = CreateAsset("asset1", GetPath("asset1"));
            m_Settings.CreateOrMoveEntry(assetGUID, m_Settings.DefaultGroup).address = "TestFolder/asset1";
            AssertLocateResult(new AddressableAssetSettingsLocator(m_Settings), "TestFolder/asset1", null,
                "TestFolder/asset1",
                GetPath("TestFolder/asset1"),
                GetPath("asset1")
            );
        }

        [Test]
        public void WhenLocatorWithAssetsInFolder_LocateWithAssetKeySucceeds()
        {
            var folderGUID = CreateFolder("TestFolder", new string[] { "asset1.asset", "asset2.asset", "asset3.asset" });
            m_Settings.CreateOrMoveEntry(folderGUID, m_Settings.DefaultGroup).address = "TF";
            AssertLocateResult(new AddressableAssetSettingsLocator(m_Settings), "TF/asset1.asset", null, GetPath("TestFolder/asset1.asset"));
        }

        [Test]
        public void WhenLocatorWithAssetsInFolder_LocateWithFolderKeyFails()
        {
            var folderGUID = CreateFolder("TestFolder", new string[] { "asset1.asset", "asset2.asset", "asset3.asset" });
            m_Settings.CreateOrMoveEntry(folderGUID, m_Settings.DefaultGroup).address = "TF";
            var locator = new AddressableAssetSettingsLocator(m_Settings);
            Assert.IsFalse(locator.Locate("TF", null, out var locations));
        }

        [Test]
        public void WhenLocatorWithAssetsInFolder_LocateWithFolderLabelSucceeds()
        {
            var folderGUID = CreateFolder("TestFolder", new string[] { "asset1.asset", "asset2.asset", "asset3.asset" });
            var folderEntry = m_Settings.CreateOrMoveEntry(folderGUID, m_Settings.DefaultGroup);
            folderEntry.address = "TF";
            folderEntry.SetLabel("FolderLabel", true, true, true);
            AssertLocateResult(new AddressableAssetSettingsLocator(m_Settings), "FolderLabel", null, GetPath("TestFolder/asset1.asset"), GetPath("TestFolder/asset2.asset"), GetPath("TestFolder/asset3.asset"));
        }

        [Test]
        public void WhenLocatorWithAssetsInFolderWithSimilarNames_LocateWithAssetKeySucceeds()
        {
            var folderGUID = CreateFolder("TestFolder", new string[] { "asset1", "asset", "asset1_more" });
            m_Settings.CreateOrMoveEntry(folderGUID, m_Settings.DefaultGroup).address = "TF";
            AssertLocateResult(new AddressableAssetSettingsLocator(m_Settings), "TF/asset1", null, GetPath("TestFolder/asset1"));
        }

        [Test]
        public void WhenLocatorWithAssetsInNestedFolders_LocateWithAssetKeySucceeds()
        {
            var folderGUID1 = CreateFolder("TestFolder", new string[] { "asset1.asset", "asset2.asset", "asset3.asset" });
            var folderGUID2 = CreateFolder("TestFolder/TestSubFolder1", new string[] { "asset1.asset", "asset2.asset", "asset3.asset" });
            var folderGUID3 = CreateFolder("TestFolder/TestSubFolder1/TestSubFolder2", new string[] { "asset1.asset", "asset2.asset", "asset3.asset" });
            m_Settings.CreateOrMoveEntry(folderGUID1, m_Settings.DefaultGroup).address = "TF";
            var locator = new AddressableAssetSettingsLocator(m_Settings);
            AssertLocateResult(locator, "TF/asset1.asset", null, GetPath("TestFolder/asset1.asset"));
            AssertLocateResult(locator, "TF/TestSubFolder1/asset1.asset", null, GetPath("TestFolder/TestSubFolder1/asset1.asset"));
            AssertLocateResult(locator, "TF/TestSubFolder1/TestSubFolder2/asset1.asset", null, GetPath("TestFolder/TestSubFolder1/TestSubFolder2/asset1.asset"));
        }

        [Test]
        public void WhenLocatorWithAssetsInNestedFoldersThatAreBothAddressable_LocateWithAssetKeySucceeds()
        {
            var folderGUID1 = CreateFolder("TestFolder", new string[] { "asset1.asset", "asset2.asset", "asset3.asset" });
            var folderGUID2 = CreateFolder("TestFolder/TestSubFolder1", new string[] { "asset1.asset", "asset2.asset", "asset3.asset" });
            var folderGUID3 = CreateFolder("TestFolder/TestSubFolder1/TestSubFolder2", new string[] { "asset1.asset", "asset2.asset", "asset3.asset" });
            m_Settings.CreateOrMoveEntry(folderGUID1, m_Settings.DefaultGroup).address = "TF";
            m_Settings.CreateOrMoveEntry(folderGUID2, m_Settings.DefaultGroup).address = "TF2";
            var locator = new AddressableAssetSettingsLocator(m_Settings);
            AssertLocateResult(locator, "TF/asset1.asset", null, GetPath("TestFolder/asset1.asset"));
            AssertLocateResult(locator, "TF2/asset1.asset", null, GetPath("TestFolder/TestSubFolder1/asset1.asset"));
            AssertLocateResult(locator, "TF2/TestSubFolder2/asset1.asset", null, GetPath("TestFolder/TestSubFolder1/TestSubFolder2/asset1.asset"));
        }

        void CreateAndAddScenesToEditorBuildSettings(string scenePrefix, int count)
        {
            var sceneList = new List<EditorBuildSettingsScene>();
            for (int i = 0; i < count; i++)
            {
                var path = GetPath($"{scenePrefix}{i}.unity");
                var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, i == 0 ? NewSceneMode.Single : NewSceneMode.Additive);
                EditorSceneManager.SaveScene(scene, path);
                var guid = AssetDatabase.AssetPathToGUID(path);
                sceneList.Add(new EditorBuildSettingsScene(path, true));
            }
            EditorBuildSettings.scenes = sceneList.ToArray();
        }

        [Test]
        public void WhenLocatorWithScenesInSceneList_LocateWithSceneIndexKeyReturnsLocationForScene()
        {
            CreateAndAddScenesToEditorBuildSettings("testScene", 3);
            var locator = new AddressableAssetSettingsLocator(m_Settings);
            for (int i = 0; i < 3; i++)
                AssertLocateResult(locator, i, null, GetPath($"testScene{i}.unity"));
        }

        [Test]
        public void WhenLocatorWithScenesInSceneList_LocateWithSceneGUIDKeyReturnsLocationForScene()
        {
            CreateAndAddScenesToEditorBuildSettings("testScene", 3);
            var locator = new AddressableAssetSettingsLocator(m_Settings);
            for (int i = 0; i < EditorBuildSettings.scenes.Length; i++)
                AssertLocateResult(locator, EditorBuildSettings.scenes[i].guid.ToString(), null, GetPath($"testScene{i}.unity"));
        }

        [Test]
        public void WhenLocatorWithScenesInSceneList_LocateWithSceneNameKeyReturnsLocationForScene()
        {
            CreateAndAddScenesToEditorBuildSettings("testScene", 3);
            var locator = new AddressableAssetSettingsLocator(m_Settings);
            for (int i = 0; i < EditorBuildSettings.scenes.Length; i++)
                AssertLocateResult(locator, $"testScene{i}", null, GetPath($"testScene{i}.unity"));
        }

        public void RunResourcesTestWithAsset(bool IncludeResourcesFolders)
        {
            var path = GetPath("Resources/test.asset");
            var dir = Path.GetDirectoryName(path);
            Directory.CreateDirectory(dir);
            AssetDatabase.CreateAsset(UnityEngine.AddressableAssets.Tests.TestObject.Create("test"), path);
            AssetDatabase.SaveAssets();
            m_Settings.FindGroup(g => g.HasSchema<PlayerDataGroupSchema>()).GetSchema<PlayerDataGroupSchema>().IncludeResourcesFolders = IncludeResourcesFolders;
            var locator = new AddressableAssetSettingsLocator(m_Settings);
            var res = locator.Locate("test", null, out var locations);
            Assert.AreEqual(res, IncludeResourcesFolders);
            if (IncludeResourcesFolders)
                Assert.AreEqual(1, locations.Count);
            else
                Assert.IsNull(locations);
            AssetDatabase.DeleteAsset(path);
            Directory.Delete(dir);
        }

        [Test]
        public void WhenAGroupHasIncludeResourcesFoldersEnabled_LocateFindsAssetInResourcesFolder()
        {
            RunResourcesTestWithAsset(true);
        }

        [Test]
        public void WhenAGroupHasIncludeResourcesFoldersDisabled_LocateFailesForAssetInResourcesFolder()
        {
            RunResourcesTestWithAsset(false);
        }
    }
}
