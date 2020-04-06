using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AddressableAssets.Initialization;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace UnityEditor.AddressableAssets.Tests
{
    public class BuildScriptPackedTests : AddressableAssetTestBase
    {
        private AddressablesDataBuilderInput m_BuilderInput;
        private ResourceManagerRuntimeData m_RuntimeData;
        private AddressableAssetsBuildContext m_BuildContext;
        private BuildScriptPackedMode m_BuildScript;
        private AssetBundle m_AssetBundle;
        private EditorBuildSettingsScene[] m_ScenesBkp;

        protected override bool PersistSettings => false;

        private AddressableAssetSettings m_PersistedSettings = null;
        protected new AddressableAssetSettings Settings => m_PersistedSettings != null ? m_PersistedSettings : base.Settings;

        private const string k_SchemaTestFolder = k_TestConfigFolder + "/SchemaTests";

        [SetUp]
        protected void Setup()
        {
            m_BuilderInput = new AddressablesDataBuilderInput(Settings);
            m_BuildScript = ScriptableObject.CreateInstance<BuildScriptPackedMode>();
            m_BuildScript.InitializeBuildContext(m_BuilderInput, out m_BuildContext);
            m_RuntimeData = m_BuildContext.runtimeData;

            m_ScenesBkp = EditorBuildSettings.scenes;
            EditorBuildSettings.scenes = new EditorBuildSettingsScene[0];
        }

        [TearDown]
        protected void TearDown()
        {
            m_BuilderInput = null;
            Object.DestroyImmediate(m_BuildScript);
            m_BuildScript = null;
            m_AssetBundle?.Unload(true);
            m_AssetBundle = null;
            Object.DestroyImmediate(m_PersistedSettings, true);
            m_PersistedSettings = null;
            EditorBuildSettings.scenes = m_ScenesBkp;
            AssetDatabase.Refresh();

            if (Directory.Exists(k_SchemaTestFolder))
                Directory.Delete(k_SchemaTestFolder, true);

            AssetDatabase.Refresh();
        }

        [Test]
        public void PackedModeScript_CannotBuildPlayContent()
        {
            var buildScript = ScriptableObject.CreateInstance<BuildScriptPackedMode>();
            
            Assert.IsFalse(buildScript.CanBuildData<AddressablesPlayModeBuildResult>());
            
            Assert.IsTrue(buildScript.CanBuildData<AddressableAssetBuildResult>());
            Assert.IsTrue(buildScript.CanBuildData<AddressablesPlayerBuildResult>());
        }

        // [Test]
        // public void ProcessPlayerDataSchema_Scenes_IncludeBuildSettingsScenesIsFalse_ShouldNotGenerateLocationsOrProviders()
        // {
            // CreateBuiltInTestScene(k_SchemaTestFolder + "/TestScenes/testScene.unity");

            // var group = Settings.FindGroup(AddressableAssetSettings.PlayerDataGroupName);
            // var schema = group.GetSchema(typeof(PlayerDataGroupSchema)) as PlayerDataGroupSchema;
            // schema.IncludeBuildSettingsScenes = false;

            // var errorStr = m_BuildScript.ProcessPlayerDataSchema(schema, group, m_BuildContext);
            // Assert.True(string.IsNullOrEmpty(errorStr));

            // var actualLocations = m_BuildContext.locations;
            // Assert.AreEqual(0, actualLocations.Count);

            // var expectedProviderCount = 0;
            // var actualProviderIds = m_BuildScript.ResourceProviderData.Select(r => r.Id).ToArray();
            // Assert.AreEqual(expectedProviderCount, actualProviderIds.Length);
        // }

        private static IEnumerable<object[]> PlayerDataGroupSchema_SceneCases()
        {
            string sc1 = k_SchemaTestFolder + "/TestScenes/testScene.unity";
            string sc2 = k_SchemaTestFolder + "/OtherFolder/testScene2.unity";
            
            yield return new object[] { new string[] { }};
            yield return new object[] { new string[] { sc1 }};
            yield return new object[] { new string[] { sc1, sc2}};
            //yield return new object[] { new string[] { sc1, sc1 }};
        }

        // [Test, TestCaseSource(nameof(PlayerDataGroupSchema_SceneCases))]
        // public void ProcessPlayerDataSchema_Scenes_ShouldGenerateCorrectLocationsAndProviders(string[] scenePaths)
        // {
            // foreach (string path in scenePaths)
                // CreateBuiltInTestScene(path);

            // var group = Settings.FindGroup(AddressableAssetSettings.PlayerDataGroupName);
            // var schema = group.GetSchema(typeof(PlayerDataGroupSchema)) as PlayerDataGroupSchema;
            // schema.IncludeBuildSettingsScenes = true;

            // var errorStr = m_BuildScript.ProcessPlayerDataSchema(schema, group, m_BuildContext);
            // Assert.True(string.IsNullOrEmpty(errorStr));

            // var actualLocations = m_BuildContext.locations;
            // var expectedLocationIds = scenePaths.Distinct().Select(s => s.Replace(".unity", "").Replace("Assets/", ""));
            // Assert.AreEqual(expectedLocationIds.Count(), actualLocations.Count);
            // Assert.AreEqual(expectedLocationIds, actualLocations.Select(l => l.InternalId));

            // var expectedProviderCount = 0;
            // var actualProviderIds = m_BuildScript.ResourceProviderData.Select(r => r.Id).ToArray();
            // Assert.AreEqual(expectedProviderCount, actualProviderIds.Length);
        // }

        [Test]
        public void SetAssetEntriesBundleFileIdToCatalogEntryBundleFileId_ModifiedOnlyAssetEntries_ThatAreIncludedInBuildWriteData()
        {
            GUID entry1Guid = GUID.Generate();
            GUID entry2Guid = GUID.Generate();
            string bundleFile = "bundle";
            string internalBundleName = "bundlepath";
            string finalBundleName = "finalBundlePath";
            string bundleCatalogEntryInternalId = "catalogentrybundlefileid";

            AddressableAssetEntry entry1 = new AddressableAssetEntry(entry1Guid.ToString(), "123", null, false);
            AddressableAssetEntry entry2 = new AddressableAssetEntry(entry2Guid.ToString(), "456", null, false);
            ICollection<AddressableAssetEntry> entries = new List<AddressableAssetEntry>()
            {
                entry1, entry2
            };

            Dictionary<string, string> bundleToIdMap = new Dictionary<string, string>()
            {
                {internalBundleName, finalBundleName}
            };

            IBundleWriteData writeData = new BundleWriteData();
            writeData.AssetToFiles.Add(entry1Guid, new List<string>(){ bundleFile });
            writeData.FileToBundle.Add(bundleFile, internalBundleName);

            Dictionary<string, ContentCatalogDataEntry> catalogMap = new Dictionary<string, ContentCatalogDataEntry>()
            {
                {
                    finalBundleName,
                    new ContentCatalogDataEntry(typeof(IAssetBundleResource), bundleCatalogEntryInternalId,
                        typeof(AssetBundleProvider).FullName, new[] {"catalogentry"})
                }
            };

            BuildScriptPackedMode.SetAssetEntriesBundleFileIdToCatalogEntryBundleFileId(entries, bundleToIdMap, writeData, catalogMap);

            Assert.AreEqual(bundleCatalogEntryInternalId, entry1.BundleFileId);
            Assert.IsNull(entry2.BundleFileId);
        }

        [Test]
        public void ProcessPlayerDataSchema_Resources_IncludeResourcesFoldersIsFalse_ShouldNotGenerateLocationsOrProviders()
        {
            CreateTestResourceAsset(k_SchemaTestFolder + "/Resources/testResource1.prefab");

            var group = Settings.FindGroup(AddressableAssetSettings.PlayerDataGroupName);
            var schema = group.GetSchema(typeof(PlayerDataGroupSchema)) as PlayerDataGroupSchema;
            schema.IncludeResourcesFolders = false;

            var errorStr = m_BuildScript.ProcessPlayerDataSchema(schema, group, m_BuildContext);
            Assert.True(string.IsNullOrEmpty(errorStr));

            var actualLocations = m_BuildContext.locations;
            var actualProviderIds = m_BuildScript.ResourceProviderData.Select(r => r.Id).ToArray();

            Assert.AreEqual(0, actualLocations.Count);
            Assert.AreEqual(0, actualProviderIds.Count());
        }

        private static IEnumerable<object[]> PlayerDataGroupSchema_ResourcesCases()
        {
            string res1 = k_SchemaTestFolder + "/Resources/testResource1.prefab";
            string res2 = k_SchemaTestFolder + "/OtherFolder/Resources/testResource2.prefab";
            //string res3 = k_SchemaTestFolder + "/Resources/Resources/testResource3.prefab";
            //string res4 = k_SchemaTestFolder + "/Resources/SubFolder/Resources/testResource4.prefab";

            yield return new object[] { new string[] { } };
            yield return new object[] { new string[] { res1 }};
            yield return new object[] { new string[] { res1, res2 }};
            yield return new object[] { new string[] { res1, res1 }};
            //yield return new object[] { new string[] { res1, res2, res3 }};
            //yield return new object[] { new string[] { res1, res2, res3, res4 } };
        }

        // [Test, TestCaseSource(nameof(PlayerDataGroupSchema_ResourcesCases))]
        // public void ProcessPlayerDataSchema_Resources_ShouldGenerateCorrectLocationsAndProviders(string[] resourcesPaths)
        // {
            // foreach (string path in resourcesPaths)
                // CreateTestResourceAsset(path);

            // var group = Settings.FindGroup(AddressableAssetSettings.PlayerDataGroupName);
            // var schema = group.GetSchema(typeof(PlayerDataGroupSchema)) as PlayerDataGroupSchema;
            // schema.IncludeResourcesFolders = true;

            // var errorStr = m_BuildScript.ProcessPlayerDataSchema(schema, group, m_BuildContext);
            // Assert.True(string.IsNullOrEmpty(errorStr));

            // var actualLocations = m_BuildContext.locations;
            // var actualProviderIds = m_BuildScript.ResourceProviderData.Select(r => r.Id).ToArray();
            
            // var expectedLocationIds = resourcesPaths.Distinct().Select(s => Path.GetFileName(s).Replace(".prefab", ""));
            // Assert.AreEqual(expectedLocationIds.Count(), actualLocations.Count);
            // Assert.AreEqual(expectedLocationIds, actualLocations.Select(l => l.InternalId));

            // if (resourcesPaths.Any())
            // {
                // Assert.AreEqual(1, actualProviderIds.Length);
                // Assert.True(actualProviderIds.Contains(typeof(LegacyResourcesProvider).FullName));
            // }
            // else
            // {
                // Assert.AreEqual(0, actualProviderIds.Length);
            // }
        // }

        [Test]
        public void ErrorCheckBundleSettings_FindsNoProblemsInDefaultScema()
        {
            var group = Settings.CreateGroup("PackedTest", false, false, false, null, typeof(BundledAssetGroupSchema));
            var schema = group.GetSchema<BundledAssetGroupSchema>();
               
            var errorStr = BuildScriptPackedMode.ErrorCheckBundleSettings(schema, group, Settings);
            LogAssert.NoUnexpectedReceived();
            Assert.IsTrue(string.IsNullOrEmpty(errorStr));
        }

        [Test]
        public void ErrorCheckBundleSettings_WarnsOfMismatchedBuildPath()
        {
            var group = Settings.CreateGroup("PackedTest", false, false, false, null, typeof(BundledAssetGroupSchema));
            var schema = group.GetSchema<BundledAssetGroupSchema>();
            schema.BuildPath.Id = "BadPath";
                
            var errorStr = BuildScriptPackedMode.ErrorCheckBundleSettings(schema, group, Settings);
            LogAssert.NoUnexpectedReceived();
            Assert.IsTrue(errorStr.Contains("is set to the dynamic-lookup version of StreamingAssets, but BuildPath is not."));
        }

        [Test]
        public void ErrorCheckBundleSettings_WarnsOfMismatchedLoadPath()
        {
            var group = Settings.CreateGroup("PackedTest", false, false, false, null, typeof(BundledAssetGroupSchema));
            var schema = group.GetSchema<BundledAssetGroupSchema>();
            schema.LoadPath.Id = "BadPath";
                
            var errorStr = BuildScriptPackedMode.ErrorCheckBundleSettings(schema, group, Settings);
            LogAssert.NoUnexpectedReceived();
            Assert.IsTrue(errorStr.Contains("is set to the dynamic-lookup version of StreamingAssets, but LoadPath is not."));
        }

        private static IEnumerable<List<AssetBundleBuild>> DuplicateBundleNamesCases()
        {
            var abb1 = new AssetBundleBuild() { assetBundleName = "name1.bundle" };
            var abb2 = new AssetBundleBuild() { assetBundleName = "name2.bundle" };
            yield return new List<AssetBundleBuild>();
            yield return new List<AssetBundleBuild>() { abb1 };
            yield return new List<AssetBundleBuild>() { abb1, abb1 };
            yield return new List<AssetBundleBuild>() { abb1, abb2 };
            yield return new List<AssetBundleBuild>() { abb1, abb1, abb1 };
        }

        [Test, TestCaseSource(nameof(DuplicateBundleNamesCases))]
        public void HandleBundlesNaming_NamesShouldAlwaysBeUnique(List<AssetBundleBuild> bundleBuilds)
        {
            var group = Settings.CreateGroup("PackedTest", false, false, false, null, typeof(BundledAssetGroupSchema));
            var bundleToAssetGroup = new Dictionary<string, string>();

            m_BuildScript.HandleDuplicateBundleNames(bundleBuilds, bundleToAssetGroup, group.Guid, out var uniqueNames);

            var uniqueNamesInBundleBuilds = bundleBuilds.Select(b => b.assetBundleName).Distinct();
            Assert.AreEqual(bundleBuilds.Count, uniqueNames.Count());
            Assert.AreEqual(bundleBuilds.Count, uniqueNamesInBundleBuilds.Count());
            Assert.AreEqual(bundleBuilds.Count, bundleToAssetGroup.Count);
        }

        [Test]
        public void CreateCatalogFiles_NullArgs_ShouldFail()
        {
            var jsonText = "Some text in catalog file";
            var result = m_BuildScript.CreateCatalogFiles(jsonText, m_BuilderInput, null);
            Assert.IsFalse(result);
            LogAssert.Expect(LogType.Error, new Regex("catalog", RegexOptions.IgnoreCase));

            result = m_BuildScript.CreateCatalogFiles(jsonText, null, m_BuildContext);
            Assert.IsFalse(result);
            LogAssert.Expect(LogType.Error, new Regex("catalog", RegexOptions.IgnoreCase));

            result = m_BuildScript.CreateCatalogFiles(null, m_BuilderInput, m_BuildContext);
            Assert.IsFalse(result);
            LogAssert.Expect(LogType.Error, new Regex("catalog", RegexOptions.IgnoreCase));
        }

        [Test]
        public void CreateCatalogFiles_DefaultOptions_ShouldCreateLocalJsonCatalogFile()
        {
            var fileName = m_BuilderInput.RuntimeCatalogFilename;
            var jsonText = "Some text in catalog file";

            var result = m_BuildScript.CreateCatalogFiles(jsonText, m_BuilderInput, m_BuildContext);

            Assert.IsTrue(result);
            
            // Assert locations
            Assert.IsTrue(m_RuntimeData.CatalogLocations.Count == 1);
            Assert.IsTrue(m_RuntimeData.CatalogLocations.Any(l => l.InternalId.EndsWith(fileName)));

            // Assert file paths
            var registryPaths = m_BuilderInput.Registry.GetFilePaths().ToList();
            Assert.IsTrue(registryPaths.Any(p => p.EndsWith(fileName)));
            Assert.IsTrue(File.Exists(registryPaths.First(p => p.EndsWith(fileName))));
        }

        [Test]
        public void CreateCatalogFiles_BundleLocalCatalog_ShouldCreateLocalCatalogFileInAssetBundle()
        {
            Settings.BundleLocalCatalog = true;

            var defaultFileName = m_BuilderInput.RuntimeCatalogFilename;
            var bundleFileName = defaultFileName.Replace(".json", ".bundle");
            var jsonText = "Some text in catalog file";

            var result = m_BuildScript.CreateCatalogFiles(jsonText, m_BuilderInput, m_BuildContext);

            Assert.IsTrue(result);

            // Assert locations
            Assert.AreEqual(1, m_RuntimeData.CatalogLocations.Count);
            Assert.AreEqual(1, m_RuntimeData.CatalogLocations.Count(l => l.InternalId.EndsWith(bundleFileName)));

            // Assert file paths
            var registryPaths = m_BuilderInput.Registry.GetFilePaths().ToList();
            var registryBundlePath = registryPaths.First(p => p.EndsWith(bundleFileName));
            Assert.AreEqual(1, registryPaths.Count(p => p.EndsWith(bundleFileName)));
            Assert.AreEqual(0, registryPaths.Count(p => p.EndsWith(defaultFileName)));
            Assert.IsTrue(File.Exists(registryBundlePath));

            // Assert catalogs
            m_AssetBundle = AssetBundle.LoadFromFile(registryBundlePath);
            Assert.IsNotNull(m_AssetBundle);

            var assets = m_AssetBundle.LoadAllAssets<TextAsset>();
            Assert.AreEqual(1, assets.Length);
            Assert.AreEqual(jsonText, assets.First().text);
        }

        [Test]
        public void CreateCatalogFiles_BundleLocalCatalog_BuildRemoteCatalog_ShouldCreateCatalogBundleAndRemoteJsonCatalog()
        {
            // Creating a bundle causes a domain reload and settings need to be persisted to be able to access profile variables.
            m_PersistedSettings = AddressableAssetSettings.Create(k_TestConfigFolder, k_TestConfigName, true, true);
            Setup();

            Settings.BundleLocalCatalog = true;
            Settings.BuildRemoteCatalog = true;
            Settings.RemoteCatalogBuildPath = new ProfileValueReference();
            Settings.RemoteCatalogBuildPath.SetVariableByName(Settings, AddressableAssetSettings.kRemoteBuildPath);
            Settings.RemoteCatalogLoadPath = new ProfileValueReference();
            Settings.RemoteCatalogLoadPath.SetVariableByName(Settings, AddressableAssetSettings.kRemoteLoadPath);

            var defaultFileName = m_BuilderInput.RuntimeCatalogFilename;
            var bundleFileName = defaultFileName.Replace(".json", ".bundle");
            var jsonText = "Some text in catalog file";

            var result = m_BuildScript.CreateCatalogFiles(jsonText, m_BuilderInput, m_BuildContext);

            Assert.IsTrue(result);

            // Assert locations
            Assert.AreEqual(3, m_RuntimeData.CatalogLocations.Count);
            Assert.AreEqual(1, m_RuntimeData.CatalogLocations.Count(l => l.InternalId.EndsWith(bundleFileName)));
            Assert.AreEqual(2, m_RuntimeData.CatalogLocations.Count(l => l.InternalId.EndsWith(".hash")));

            // Assert file paths
            var remoteBuildFolder = Settings.RemoteCatalogBuildPath.GetValue(Settings);
            var registryPaths = m_BuilderInput.Registry.GetFilePaths().ToList();
            Assert.AreEqual(3, registryPaths.Count);
            Assert.AreEqual(1, registryPaths.Count(p => p.EndsWith(bundleFileName)));
            Assert.AreEqual(1, registryPaths.Count(p => p.Contains(remoteBuildFolder) && p.EndsWith(".json")));
            Assert.AreEqual(1, registryPaths.Count(p => p.Contains(remoteBuildFolder) && p.EndsWith(".hash")));

            var registryBundlePath = registryPaths.First(p => p.EndsWith(bundleFileName));
            var registryRemoteCatalogPath = registryPaths.First(p => p.Contains(remoteBuildFolder) && p.EndsWith(".json"));
            var registryRemoteHashPath = registryPaths.First(p => p.Contains(remoteBuildFolder) && p.EndsWith(".hash"));
            Assert.IsTrue(File.Exists(registryBundlePath));
            Assert.IsTrue(File.Exists(registryRemoteCatalogPath));
            Assert.IsTrue(File.Exists(registryRemoteHashPath));

            // Assert catalogs
            m_AssetBundle = AssetBundle.LoadFromFile(registryBundlePath);
            Assert.IsNotNull(m_AssetBundle);

            var assets = m_AssetBundle.LoadAllAssets<TextAsset>();
            Assert.AreEqual(1, assets.Length);
            Assert.AreEqual(jsonText, assets.First().text);

            var remoteCatalogText = File.ReadAllText(registryRemoteCatalogPath);
            Assert.AreEqual(jsonText, remoteCatalogText);

            File.Delete(registryRemoteCatalogPath);
            File.Delete(registryRemoteHashPath);
        }

        private void CreateBuiltInTestScene(string scenePath)
        {
            var dir = Path.GetDirectoryName(scenePath);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
                AssetDatabase.ImportAsset(dir, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                AssetDatabase.Refresh();
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            EditorSceneManager.SaveScene(scene, scenePath);

            //Clear out the active scene so it doesn't affect tests
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);

            var newBuildSettingsScene = new EditorBuildSettingsScene(scenePath, true);

            var builtInScenes = EditorBuildSettings.scenes;
            var list = new List<EditorBuildSettingsScene>(builtInScenes);
            list.Add(newBuildSettingsScene);
            EditorBuildSettings.scenes = list.ToArray();
        }

        private void CreateTestResourceAsset(string resourceAssetPath)
        {
            var dir = Path.GetDirectoryName(resourceAssetPath);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
                AssetDatabase.ImportAsset(dir, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                AssetDatabase.Refresh();
            }

            GameObject testResourceObject = new GameObject(Path.GetFileName(resourceAssetPath));
#if UNITY_2018_3_OR_NEWER
            PrefabUtility.SaveAsPrefabAsset(testResourceObject, resourceAssetPath);
#else
            PrefabUtility.CreatePrefab(k_ResourceAssetPath, testResourceObject);
#endif
            AssetDatabase.ImportAsset(resourceAssetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh();
        }
    }
}