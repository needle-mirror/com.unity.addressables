using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Build.Content;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.U2D;

namespace UnityEditor.AddressableAssets.Tests
{
    public class AddressableAssetEntryTests : AddressableAssetTestBase
    {
        string m_guid;
        AddressableAssetGroup m_testGroup;
        protected override void OnInit()
        {
            var path = GetAssetPath("subObjectTest.asset");
            AssetDatabase.CreateAsset(UnityEngine.AddressableAssets.Tests.TestObject.Create("test"), path);

            AssetDatabase.AddObjectToAsset(UnityEngine.AddressableAssets.Tests.TestObject2.Create("test2"), path);
            AssetDatabase.AddObjectToAsset(UnityEngine.AddressableAssets.Tests.TestObject2.Create("test3"), path);
            AssetDatabase.AddObjectToAsset(UnityEngine.AddressableAssets.Tests.TestObject2.Create("test4"), path);
            AssetDatabase.AddObjectToAsset(UnityEngine.AddressableAssets.Tests.TestObject2.Create("test5"), path);
            AssetDatabase.SaveAssets();

            m_guid = AssetDatabase.AssetPathToGUID(path);
            Settings.CreateOrMoveEntry(m_guid, Settings.DefaultGroup);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

            m_testGroup = Settings.CreateGroup("testGroup", false, false, false, null, typeof(BundledAssetGroupSchema));
        }

        protected override void OnCleanup()
        {
            Settings.RemoveGroup(m_testGroup);
        }

        [Test]
        public void CreateCatalogEntries_WhenObjectHasMultipleSubObjectWithSameType_OnlyOneSubEntryIsCreated()
        {
            var e = Settings.DefaultGroup.GetAssetEntry(m_guid);
            var entries = new List<ContentCatalogDataEntry>();
            var providerTypes = new HashSet<Type>();
            e.CreateCatalogEntries(entries, false, "doesntMatter", null, null, providerTypes);
            Assert.AreEqual(2, entries.Count);
        }

        [Test]
        public void CreateCatalogEntries_OverridesMainTypeIfWrong()
        {
            var e = Settings.DefaultGroup.GetAssetEntry(m_guid);
            var entries = new List<ContentCatalogDataEntry>();
            var providerTypes = new HashSet<Type>();
            var savedType = e.m_cachedMainAssetType;
            e.m_cachedMainAssetType = typeof(Texture2D);//something arbitrarily wrong.
            e.CreateCatalogEntries(entries, false, "doesntMatter", null, null, providerTypes);
            e.m_cachedMainAssetType = savedType;
            Assert.AreEqual(2, entries.Count);
            bool foundOnlyTestObjects = true;
            foreach (var entry in entries)
            {
                if (entry.ResourceType != typeof(UnityEngine.AddressableAssets.Tests.TestObject) &&
                    (entry.ResourceType != typeof(UnityEngine.AddressableAssets.Tests.TestObject2)))
                {
                    foundOnlyTestObjects = false;
                }
            }
            Assert.IsTrue(foundOnlyTestObjects);
        }

        [Test]
        public void WhenClassReferencedByAddressableAssetEntryIsReloaded_CachedMainAssetTypeIsReset()
        {
            // Setup
            var path = GetAssetPath("resetCachedMainAssetTypeTestGroup.asset");
            AddressableAssetGroup group = ScriptableObject.CreateInstance<AddressableAssetGroup>();
            AddressableAssetEntry entry = new AddressableAssetEntry(m_guid, "address", null, false);
            group.AddAssetEntry(entry);

            Assert.IsNull(entry.m_cachedMainAssetType);
            Assert.AreEqual(typeof(UnityEngine.AddressableAssets.Tests.TestObject), entry.MainAssetType);

            // Test
            AssetDatabase.CreateAsset(group, path);
            Resources.UnloadAsset(group);

            var reloadedGroup = AssetDatabase.LoadAssetAtPath<AddressableAssetGroup>(path);
            var reloadedEntry = reloadedGroup.GetAssetEntry(m_guid);
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
            AddressableAssetEntry entry = Settings.CreateOrMoveEntry(AddressableAssetEntry.EditorSceneListName, m_testGroup, false);

            //Test
            List<IReferenceEntryData> results = new List<IReferenceEntryData>();
            entry.GatherAllAssetReferenceDrawableEntries(results, Settings);

            //Assert
            Assert.AreEqual(0, results.Count);

            //Cleanup
            BuiltinSceneCache.scenes = savedCache;
            Settings.RemoveAssetEntry(AddressableAssetEntry.EditorSceneListName, false);
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

            AddressableAssetEntry entry = Settings.CreateOrMoveEntry(AddressableAssetEntry.ResourcesName, m_testGroup, false);

            //Test
            List<IReferenceEntryData> results = new List<IReferenceEntryData>();
            entry.GatherAllAssetReferenceDrawableEntries(results, Settings);

            //Assert
            Assert.AreEqual(0, results.Count);

            //Cleanup
            BuiltinSceneCache.scenes = savedCache;
            Settings.RemoveAssetEntry(AddressableAssetEntry.ResourcesName, false);
        }

        [Test]
        public void GatherAllAssetReferenceDrawableEntries_ReturnsFolderSubAssets()
        {
            //Setup
            string testAssetFolder = GetAssetPath("TestFolder");
            string testAssetSubFolder = Path.Combine(testAssetFolder, "SubFolder");

            string mainPrefabPath = Path.Combine(testAssetFolder, "mainFolder.prefab").Replace('\\', '/');
            string subPrefabPath = Path.Combine(testAssetSubFolder, "subFolder.prefab").Replace('\\', '/');

            Directory.CreateDirectory(testAssetFolder);
            PrefabUtility.SaveAsPrefabAsset(new GameObject("mainFolderAsset"), mainPrefabPath);

            Directory.CreateDirectory(testAssetSubFolder);
            PrefabUtility.SaveAsPrefabAsset(new GameObject("subFolderAsset"), subPrefabPath);

            string guid = AssetDatabase.AssetPathToGUID(testAssetFolder);
            AddressableAssetEntry entry = Settings.CreateOrMoveEntry(guid, m_testGroup, false);
            List<IReferenceEntryData> results = new List<IReferenceEntryData>();

            //Test
            entry.GatherAllAssetReferenceDrawableEntries(results, Settings);

            //Assert
            Assert.AreEqual(2, results.Count);
            Assert.AreEqual(mainPrefabPath, results[0].AssetPath);
            Assert.AreEqual(subPrefabPath, results[1].AssetPath);

            //Cleanup
            Directory.Delete(testAssetFolder, true);
            Settings.RemoveAssetEntry(guid, false);
        }

        [Test]
        public void GatherAllAssetReferenceDrawableEntries_DoesNotReturnScenesInFolder_IfSceneIsInBuiltInScenes()
        {
            //Setup
            string testAssetFolder = GetAssetPath("TestFolder");
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

            string guid = AssetDatabase.AssetPathToGUID(testAssetFolder);
            AddressableAssetEntry entry = Settings.CreateOrMoveEntry(guid, m_testGroup, false);
            List<IReferenceEntryData> results = new List<IReferenceEntryData>();

            //Test
            entry.GatherAllAssetReferenceDrawableEntries(results, Settings);

            //Assert
            Assert.AreEqual(2, results.Count);
            Assert.AreEqual(mainPrefabPath, results[0].AssetPath);
            Assert.AreEqual(subPrefabPath, results[1].AssetPath);

            //Cleanup
            Directory.Delete(testAssetFolder, true);
            BuiltinSceneCache.scenes = savedCache;
            Settings.RemoveAssetEntry(guid, false);
        }

        [Test]
        public void GatherAllAssetReferenceDrawableEntries_AddsSimpleAssetEntries()
        {
            //Setup
            string guid = "12345698655";
            AddressableAssetEntry entry = Settings.CreateOrMoveEntry(guid, m_testGroup, false);
            entry.m_cachedAssetPath = "TestPath";
            List<IReferenceEntryData> results = new List<IReferenceEntryData>();

            //Test
            entry.GatherAllAssetReferenceDrawableEntries(results, Settings);

            //Assert
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(entry.AssetPath, results[0].AssetPath);

            //Cleanup
            Settings.RemoveAssetEntry(guid, false);
        }

        [Test]
        public void GatherAllAssetReferenceDrawableEntries_AddsAllEntries_FromAddressableAssetEntryCollection()
        {
            //Setup
            string testAssetFolder = GetAssetPath("TestFolder");
            string collectionPath = Path.Combine(testAssetFolder, "collection.asset").Replace('\\', '/');
            Directory.CreateDirectory(testAssetFolder);

            var collection = ScriptableObject.CreateInstance<AddressableAssetEntryCollection>();
            string guid = "12345698655";
            AddressableAssetEntry entry = Settings.CreateOrMoveEntry(guid, m_testGroup, false);
            entry.m_cachedAssetPath = "TestPath";
            collection.Entries.Add(entry);

            AssetDatabase.CreateAsset(collection, collectionPath);

            string collectionGuid = "CollectionGuid";
            AddressableAssetEntry collectionEntry = Settings.CreateOrMoveEntry(collectionGuid, m_testGroup, false);
            collectionEntry.m_cachedMainAssetType = typeof(AddressableAssetEntryCollection);
            collectionEntry.m_cachedAssetPath = collectionPath;

            //Test
            List<IReferenceEntryData> results = new List<IReferenceEntryData>();
            collectionEntry.GatherAllAssetReferenceDrawableEntries(results, Settings);

            //Assert
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(entry.AssetPath, results[0].AssetPath);

            //Cleanup
            Directory.Delete(testAssetFolder, true);
            Settings.RemoveAssetEntry(guid, false);
            Settings.RemoveAssetEntry(collectionGuid, false);
        }

        [Test]
        public void GetRuntimeProviderType_HandlesEmptyProviderString()
        {
            AddressableAssetEntry entry = new AddressableAssetEntry("12345698655", "Entry", null, false);
            Type providerType = entry.GetRuntimeProviderType(null, typeof(GameObject));
            Assert.IsNull(providerType);
        }

        [Test]
        public void GetRuntimeProviderType_ReturnsAtlasProviderForSpriteAtlas()
        {
            AddressableAssetEntry entry = new AddressableAssetEntry("12345698655", "Entry", null, false);
            Type providerType = entry.GetRuntimeProviderType(typeof(AssetDatabaseProvider).FullName, typeof(SpriteAtlas));
            Assert.AreEqual(typeof(AtlasSpriteProvider), providerType);
        }

        [TestCase(typeof(AssetDatabaseProvider))]
        [TestCase(typeof(JsonAssetProvider))]
        [TestCase(typeof(BundledAssetProvider))]
        [TestCase(typeof(AssetBundleProvider))]
        [TestCase(typeof(TextDataProvider))]
        public void GetRuntimeProviderType_ReturnsProviderTypeForNonAtlas(Type testProviderType)
        {
            AddressableAssetEntry entry = new AddressableAssetEntry("12345698655", "Entry", null, false);
            Type providerType = entry.GetRuntimeProviderType(testProviderType.FullName, typeof(GameObject));
            Assert.AreEqual(testProviderType, providerType);
        }

        [Test]
        public void GetRuntimeProviderType_HandlesInvalidProviderString()
        {
            AddressableAssetEntry entry = new AddressableAssetEntry("12345698655", "Entry", null, false);
            Type providerType = entry.GetRuntimeProviderType("NotARealProvider", typeof(GameObject));
            Assert.IsNull(providerType);
        }

        [TestCase(typeof(AssetDatabaseProvider))]
        [TestCase(typeof(JsonAssetProvider))]
        [TestCase(typeof(BundledAssetProvider))]
        [TestCase(typeof(AssetBundleProvider))]
        [TestCase(typeof(TextDataProvider))]
        public void GetRuntimeProviderType_HandlesNullAssetType(Type testProviderType)
        {
            AddressableAssetEntry entry = new AddressableAssetEntry("12345698655", "Entry", null, false);
            Type providerType = entry.GetRuntimeProviderType(testProviderType.FullName, null);
            Assert.AreEqual(testProviderType, providerType);
        }

        [Test]
        public void WhenAddressHasSquareBracketsAndGuidIsNotEmptyString_CreatingNewEntry_ThrowsError()
        {
            AddressableAssetEntry entry = new AddressableAssetEntry("12345698655", "[Entry]", null, false);
            LogAssert.Expect(LogType.Error, $"Address '{entry.address}' cannot contain '[ ]'.");
        }

        [Test]
        public void WhenAddressHasSquareBracketsAndGuidIsEmptyString_CreatingNewEntry_ThrowsNothing()
        {
            Assert.DoesNotThrow(() => new AddressableAssetEntry("", "[Entry]", null, false));
        }

        [Test]
        public void WhenAddressHasSquareBracketsAndGuidIsNotEmptyString_SettingTheAddressOnExistingEntry_ThrowsError()
        {
            AddressableAssetEntry entry = new AddressableAssetEntry("12345698655", "Entry", null, false);
            entry.SetAddress("[Entry]");
            LogAssert.Expect(LogType.Error, $"Address '{entry.address}' cannot contain '[ ]'.");
        }

        [Test]
        public void WhenAddressHasSquareBracketsAndGuidIsEmptyString_SettingTheAddressOnExistingEntry_ThrowsNothing()
        {
            AddressableAssetEntry entry = new AddressableAssetEntry("", "Entry", null, false);
            Assert.DoesNotThrow(() => entry.SetAddress("[Entry]"));
        }
    }
}
