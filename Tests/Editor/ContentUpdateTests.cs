using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.TestTools;

namespace UnityEditor.AddressableAssets.Tests
{
    public class ContentUpdateTests : AddressableAssetTestBase
    {
        protected override bool PersistSettings { get { return true; } }

        [Test]
        public void CanCreateContentStateData()
        {
            var group = Settings.CreateGroup("LocalStuff", false, false, false, null);
            var schema = group.AddSchema<BundledAssetGroupSchema>();
            schema.BuildPath.SetVariableByName(Settings, AddressableAssetSettings.kLocalBuildPath);
            schema.LoadPath.SetVariableByName(Settings, AddressableAssetSettings.kLocalLoadPath);
            schema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
            group.AddSchema<ContentUpdateGroupSchema>().StaticContent = true;

            Settings.CreateOrMoveEntry(m_AssetGUID, group);
            var context = new AddressablesDataBuilderInput(Settings);

            var op = Settings.ActivePlayerDataBuilder.BuildData<AddressablesPlayerBuildResult>(context);

            Assert.IsTrue(string.IsNullOrEmpty(op.Error), op.Error);
            var tempPath = Path.GetDirectoryName(Application.dataPath) + "/Library/com.unity.addressables/StreamingAssetsCopy/" + PlatformMappingService.GetPlatform() + "/addressables_content_state.bin";
            var cacheData = ContentUpdateScript.LoadContentState(tempPath);
            Assert.NotNull(cacheData);
            Settings.RemoveGroup(group);
        }

        [Test]
        public void CreateCustomLocator_RetrunsLocatorWithUniqueId()
        {
            ContentCatalogData ccd = new ContentCatalogData();
            ccd.SetData(new List<ContentCatalogDataEntry>());
            ResourceLocationMap map = ccd.CreateCustomLocator("test");
            Assert.AreEqual("test", map.LocatorId);
        }

        [Test]
        public void PrepareContentUpdate()
        {
            var group = Settings.CreateGroup("LocalStuff2", false, false, false, null);
            var schema = group.AddSchema<BundledAssetGroupSchema>();
            schema.BuildPath.SetVariableByName(Settings, AddressableAssetSettings.kLocalBuildPath);
            schema.LoadPath.SetVariableByName(Settings, AddressableAssetSettings.kLocalLoadPath);
            schema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
            group.AddSchema<ContentUpdateGroupSchema>().StaticContent = true;

            var entry = Settings.CreateOrMoveEntry(m_AssetGUID, group);
            entry.address = "test";

            var context = new AddressablesDataBuilderInput(Settings);

            Settings.ActivePlayerDataBuilder.BuildData<AddressablesPlayerBuildResult>(context);

            var path = AssetDatabase.GUIDToAssetPath(m_AssetGUID);
            var obj = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            obj.GetComponent<Transform>().SetPositionAndRotation(new Vector3(10, 10, 10), Quaternion.identity);
#if UNITY_2018_3_OR_NEWER
            PrefabUtility.SavePrefabAsset(obj);
#else
            EditorUtility.SetDirty(obj);
#endif
            AssetDatabase.SaveAssets();
            var tempPath = Path.GetDirectoryName(Application.dataPath) + "/Library/com.unity.addressables/StreamingAssetsCopy/" + PlatformMappingService.GetPlatform() + "/addressables_content_state.bin";
            var modifiedEntries = ContentUpdateScript.GatherModifiedEntries(Settings, tempPath);
            Assert.IsNotNull(modifiedEntries);
            Assert.GreaterOrEqual(modifiedEntries.Count, 1);
            ContentUpdateScript.CreateContentUpdateGroup(Settings, modifiedEntries, "Content Update");
            var contentGroup = Settings.FindGroup("Content Update");
            Assert.IsNotNull(contentGroup);
            var movedEntry = contentGroup.GetAssetEntry(m_AssetGUID);
            Assert.AreSame(movedEntry, entry);
            Settings.RemoveGroup(group);
        }

        [Test]
        public void GetStaticContentDependenciesOfModifiedEntries_FlagsEntryDependencies_WithStaticContentEnabled()
        {
            var mainAssetGroup = Settings.CreateGroup("MainAssetGroup", false, false, false, null,
                typeof(ContentUpdateGroupSchema), typeof(BundledAssetGroupSchema));

            var staticContentGroup = Settings.CreateGroup("StaticContentGroup", false, false, false, null,
                typeof(ContentUpdateGroupSchema), typeof(BundledAssetGroupSchema));

            mainAssetGroup.GetSchema<ContentUpdateGroupSchema>().StaticContent = true;
            staticContentGroup.GetSchema<ContentUpdateGroupSchema>().StaticContent = true;

            GameObject mainObject = new GameObject("mainObject");
            Material mat = new Material(Shader.Find("Transparent/Diffuse"));
            mainObject.AddComponent<MeshRenderer>().material = mat;

            string mainAssetPath = Path.Combine(k_TestConfigFolder, "mainObject.prefab");
            string staticAssetPath = Path.Combine(k_TestConfigFolder, "staticObject.mat");

            AssetDatabase.CreateAsset(mat, staticAssetPath);
            PrefabUtility.SaveAsPrefabAsset(mainObject, mainAssetPath);
            AssetDatabase.SaveAssets();

            var mainEntry = Settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(mainAssetPath), mainAssetGroup);
            var staticEntry = Settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(staticAssetPath), staticContentGroup);

            List<AddressableAssetEntry> modifiedEntries = new List<AddressableAssetEntry>()
            {
                mainEntry
            };

            Dictionary<AddressableAssetEntry, List<AddressableAssetEntry>> staticDependencies = new Dictionary<AddressableAssetEntry, List<AddressableAssetEntry>>()
            {
                {mainEntry, new List<AddressableAssetEntry>() }
            };
            ContentUpdateScript.GetStaticContentDependenciesForEntries(Settings, ref staticDependencies);

            Assert.AreEqual(1, staticDependencies.Count);
            Assert.AreEqual(1, staticDependencies[mainEntry].Count);
            Assert.IsTrue(staticDependencies[mainEntry].Contains(staticEntry));

            //Cleanup
            GameObject.DestroyImmediate(mainObject);

            Settings.RemoveGroup(mainAssetGroup);
            Settings.RemoveGroup(staticContentGroup);

            AssetDatabase.DeleteAsset(mainAssetPath);
            AssetDatabase.DeleteAsset(staticAssetPath);
        }

        [Test]
        public void  GetStaticContentDependenciesOfModifiedEntries_DoesNotFlagEntryDependencies_WithStaticContentDisabled()
        {
            var mainAssetGroup = Settings.CreateGroup("MainAssetGroup", false, false, false, null,
                typeof(ContentUpdateGroupSchema), typeof(BundledAssetGroupSchema));

            var dynamicContentGroup = Settings.CreateGroup("DynamicContentGroup", false, false, false, null,
                typeof(ContentUpdateGroupSchema), typeof(BundledAssetGroupSchema));

            mainAssetGroup.GetSchema<ContentUpdateGroupSchema>().StaticContent = true;
            dynamicContentGroup.GetSchema<ContentUpdateGroupSchema>().StaticContent = false;

            GameObject mainObject = new GameObject("mainObject");
            Material mat = new Material(Shader.Find("Transparent/Diffuse"));
            mainObject.AddComponent<MeshRenderer>().material = mat;

            string mainAssetPath = Path.Combine(k_TestConfigFolder, "mainObject.prefab");
            string dynamicAssetPath = Path.Combine(k_TestConfigFolder, "dynamicObject.mat");

            AssetDatabase.CreateAsset(mat, dynamicAssetPath);
            PrefabUtility.SaveAsPrefabAsset(mainObject, mainAssetPath);
            AssetDatabase.SaveAssets();

            var mainEntry = Settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(mainAssetPath), mainAssetGroup);
            Settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(dynamicAssetPath), dynamicContentGroup);

            List<AddressableAssetEntry> modifiedEntries = new List<AddressableAssetEntry>()
            {
                mainEntry
            };

            Dictionary<AddressableAssetEntry, List<AddressableAssetEntry>> staticDependencies = new Dictionary<AddressableAssetEntry, List<AddressableAssetEntry>>()
            {
                {mainEntry, new List<AddressableAssetEntry>() }
            };
            ContentUpdateScript.GetStaticContentDependenciesForEntries(Settings, ref staticDependencies);

            Assert.AreEqual(1, staticDependencies.Count);
            Assert.AreEqual(0, staticDependencies[mainEntry].Count);

            //Cleanup
            GameObject.DestroyImmediate(mainObject);

            Settings.RemoveGroup(mainAssetGroup);
            Settings.RemoveGroup(dynamicContentGroup);

            AssetDatabase.DeleteAsset(mainAssetPath);
            AssetDatabase.DeleteAsset(dynamicAssetPath);
        }

        [Test]
        public void BuildContentUpdate()
        {
            var group = Settings.CreateGroup("LocalStuff3", false, false, false, null);
            Settings.BuildRemoteCatalog = true;
            Settings.RemoteCatalogBuildPath = new ProfileValueReference();
            Settings.RemoteCatalogBuildPath.SetVariableByName(Settings, AddressableAssetSettings.kRemoteBuildPath);
            Settings.RemoteCatalogLoadPath = new ProfileValueReference();
            Settings.RemoteCatalogLoadPath.SetVariableByName(Settings, AddressableAssetSettings.kRemoteLoadPath);
            var schema = group.AddSchema<BundledAssetGroupSchema>();
            schema.BuildPath.SetVariableByName(Settings, AddressableAssetSettings.kLocalBuildPath);
            schema.LoadPath.SetVariableByName(Settings, AddressableAssetSettings.kLocalLoadPath);
            schema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
            group.AddSchema<ContentUpdateGroupSchema>().StaticContent = true;
            Settings.CreateOrMoveEntry(m_AssetGUID, group);
            var context = new AddressablesDataBuilderInput(Settings);

            var op = Settings.ActivePlayerDataBuilder.BuildData<AddressablesPlayerBuildResult>(context);

            Assert.IsTrue(string.IsNullOrEmpty(op.Error), op.Error);
            var tempPath = Path.GetDirectoryName(Application.dataPath) + "/Library/com.unity.addressables/StreamingAssetsCopy/" + PlatformMappingService.GetPlatform() + "/addressables_content_state.bin";
            var buildOp = ContentUpdateScript.BuildContentUpdate(Settings, tempPath);
            Assert.IsNotNull(buildOp);
            Assert.IsTrue(string.IsNullOrEmpty(buildOp.Error));
            Settings.RemoveGroup(group);
        }

        [Test]
        public void IsCacheDataValid_WhenNoPreviousRemoteCatalogPath_ReturnsFalseWithError()
        {
            AddressablesContentState cacheData = new AddressablesContentState();
            cacheData.editorVersion = Application.unityVersion;
            Assert.IsFalse(ContentUpdateScript.IsCacheDataValid(Settings, cacheData));
            LogAssert.Expect(LogType.Error, new Regex("Previous build had 'Build Remote Catalog' disabled.*"));
        }

        [Test]
        public void IsCacheDataValid_WhenRemoteCatalogDisabled_ReturnsFalseWithError()
        {
            AddressablesContentState cacheData = new AddressablesContentState();
            cacheData.editorVersion = Application.unityVersion;
            cacheData.remoteCatalogLoadPath = "somePath";
            var oldSetting = Settings.BuildRemoteCatalog;
            Settings.BuildRemoteCatalog = false;
            Assert.IsFalse(ContentUpdateScript.IsCacheDataValid(Settings, cacheData));
            LogAssert.Expect(LogType.Error, new Regex("Current settings have 'Build Remote Catalog' disabled.*"));
            Settings.BuildRemoteCatalog = oldSetting;
        }

        [Test]
        public void IsCacheDataValid_WhenMismatchedCatalogPaths_ReturnsFalseWithError()
        {
            AddressablesContentState cacheData = new AddressablesContentState();
            cacheData.editorVersion = Application.unityVersion;
            cacheData.remoteCatalogLoadPath = "somePath";
            var oldSetting = Settings.BuildRemoteCatalog;
            Settings.BuildRemoteCatalog = true;
            Assert.IsFalse(ContentUpdateScript.IsCacheDataValid(Settings, cacheData));
            LogAssert.Expect(LogType.Error, new Regex("Current 'Remote Catalog Load Path' does not match load path of original player.*"));
            Settings.BuildRemoteCatalog = oldSetting;
        }

        [Test]
        public void IsCacheDataValid_WhenMismatchedEditorVersions_LogsWarning()
        {
            AddressablesContentState cacheData = new AddressablesContentState();
            cacheData.editorVersion = "invalid";
            Assert.IsFalse(ContentUpdateScript.IsCacheDataValid(Settings, cacheData));
            LogAssert.Expect(LogType.Warning, new Regex(".*with version `" + cacheData.editorVersion + "`.*"));
            LogAssert.Expect(LogType.Error, new Regex("Previous.*"));
        }

        [Test]
        public void BuildContentUpdate_DoesNotDeleteBuiltData()
        {
            var group = Settings.CreateGroup("LocalStuff3", false, false, false, null);
            var schema = group.AddSchema<BundledAssetGroupSchema>();
            schema.BuildPath.SetVariableByName(Settings, AddressableAssetSettings.kLocalBuildPath);
            schema.LoadPath.SetVariableByName(Settings, AddressableAssetSettings.kLocalLoadPath);
            schema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
            group.AddSchema<ContentUpdateGroupSchema>().StaticContent = true;
            Settings.CreateOrMoveEntry(m_AssetGUID, group);
            var context = new AddressablesDataBuilderInput(Settings);

            var op = Settings.ActivePlayerDataBuilder.BuildData<AddressablesPlayerBuildResult>(context);

            Assert.IsTrue(string.IsNullOrEmpty(op.Error), op.Error);
            var tempPath = Path.GetDirectoryName(Application.dataPath) + "/Library/com.unity.addressables/StreamingAssetsCopy/" + PlatformMappingService.GetPlatform() + "/addressables_content_state.bin";
            ContentUpdateScript.BuildContentUpdate(Settings, tempPath);
            Assert.IsTrue(Directory.Exists(Addressables.BuildPath));
        }

        [Test]
        public void ContentUpdateScenes_PackedTogether_MarksAllScenesModified()
        {
            AddressableAssetGroup group = Settings.CreateGroup("SceneGroup", false, false, false, null, typeof(BundledAssetGroupSchema));
            group.GetSchema<BundledAssetGroupSchema>().BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;

            AddressableAssetEntry scene1 = new AddressableAssetEntry(m_SceneGuids[0], "scene1", group, false);
            AddressableAssetEntry scene2 = new AddressableAssetEntry(m_SceneGuids[1], "scene2", group, false);
            AddressableAssetEntry scene3 = new AddressableAssetEntry(m_SceneGuids[2], "scene3", group, false);

            group.AddAssetEntry(scene1, false);
            group.AddAssetEntry(scene2, false);
            group.AddAssetEntry(scene3, false);

            List<AddressableAssetEntry> modifedEnteries = new List<AddressableAssetEntry>()
            {
                scene1
            };

            ContentUpdateScript.AddAllDependentScenesFromModifiedEnteries(modifedEnteries);

            Assert.AreEqual(3, modifedEnteries.Count);
            Assert.AreEqual(scene1, modifedEnteries[0]);
            Assert.AreEqual(scene2, modifedEnteries[1]);
            Assert.AreEqual(scene3, modifedEnteries[2]);

            Settings.RemoveGroup(group);
        }

        [Test]
        public void ContentUpdateScenes_PackedTogetherByLabel_MarksAllScenesModifiedWithSharedLabel()
        {
            AddressableAssetGroup group = Settings.CreateGroup("SceneGroup", false, false, false, null, typeof(BundledAssetGroupSchema));
            group.GetSchema<BundledAssetGroupSchema>().BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogetherByLabel;

            AddressableAssetEntry scene1 = new AddressableAssetEntry(m_SceneGuids[0], "scene1", group, false);
            AddressableAssetEntry scene2 = new AddressableAssetEntry(m_SceneGuids[1], "scene2", group, false);
            AddressableAssetEntry scene3 = new AddressableAssetEntry(m_SceneGuids[2], "scene3", group, false);

            group.AddAssetEntry(scene1, false);
            group.AddAssetEntry(scene2, false);
            group.AddAssetEntry(scene3, false);

            scene1.SetLabel("label", true);
            scene3.SetLabel("label", true);

            List<AddressableAssetEntry> modifedEnteries = new List<AddressableAssetEntry>()
            {
                scene1
            };

            ContentUpdateScript.AddAllDependentScenesFromModifiedEnteries(modifedEnteries);

            Assert.AreEqual(2, modifedEnteries.Count);
            Assert.AreEqual(scene1, modifedEnteries[0]);
            Assert.AreEqual(scene3, modifedEnteries[1]);

            Settings.RemoveGroup(group);
        }

        [Test]
        public void ContentUpdateScenes_PackedSeperately_MarksNoAdditionalScenes()
        {
            AddressableAssetGroup group = Settings.CreateGroup("SceneGroup", false, false, false, null, typeof(BundledAssetGroupSchema));
            group.GetSchema<BundledAssetGroupSchema>().BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackSeparately;

            AddressableAssetEntry scene1 = new AddressableAssetEntry(m_SceneGuids[0], "scene1", group, false);
            AddressableAssetEntry scene2 = new AddressableAssetEntry(m_SceneGuids[1], "scene2", group, false);
            AddressableAssetEntry scene3 = new AddressableAssetEntry(m_SceneGuids[2], "scene3", group, false);

            group.AddAssetEntry(scene1, false);
            group.AddAssetEntry(scene2, false);
            group.AddAssetEntry(scene3, false);

            List<AddressableAssetEntry> modifedEnteries = new List<AddressableAssetEntry>()
            {
                scene1
            };

            ContentUpdateScript.AddAllDependentScenesFromModifiedEnteries(modifedEnteries);

            Assert.AreEqual(1, modifedEnteries.Count);
            Assert.AreEqual(scene1, modifedEnteries[0]);

            Settings.RemoveGroup(group);
        }

        private ContentUpdateScript.ContentUpdateContext GetContentUpdateContext(string contentUpdateTestAssetGUID, string contentUpdateTestCachedAssetHash,
        string contentUpdateTestNewInternalBundleName, string contentUpdateTestNewBundleName, string contentUpdateTestCachedBundlePath, string contentUpdateTestGroupGuid, string contentUpdateTestFileName)
        {
            Dictionary<string, string> bundleToInternalBundle = new Dictionary<string, string>() { {
                contentUpdateTestNewInternalBundleName,
                contentUpdateTestNewBundleName } };

            Dictionary<string, CachedAssetState> guidToCachedState = new Dictionary<string, CachedAssetState>()
            {
                //entry 1
                {
                    contentUpdateTestAssetGUID, new CachedAssetState()
                    {
                        bundleFileId = contentUpdateTestCachedBundlePath,
                        asset = new AssetState()
                        {
                            guid = new GUID(contentUpdateTestAssetGUID),
                            hash = Hash128.Parse(contentUpdateTestCachedAssetHash)
                        },
                        dependencies = new AssetState[] { },
                        data = null,
                        groupGuid = contentUpdateTestGroupGuid
                    }
                }
            };

            Dictionary<string, ContentCatalogDataEntry> idToCatalogEntryMap = new Dictionary<string, ContentCatalogDataEntry>()
            {
                //bundle entry
                { contentUpdateTestNewBundleName,
                    new ContentCatalogDataEntry(typeof(IAssetBundleResource), contentUpdateTestNewBundleName,
                        typeof(AssetBundleProvider).FullName, new []{ contentUpdateTestNewBundleName})
                },
                //asset entry
                {
                    contentUpdateTestAssetGUID,
                    new ContentCatalogDataEntry(typeof(IResourceLocation), contentUpdateTestAssetGUID, typeof(BundledAssetProvider).FullName, new []{contentUpdateTestAssetGUID})
                }
            };

            IBundleWriteData writeData = new BundleWriteData();
            writeData.AssetToFiles.Add(new GUID(contentUpdateTestAssetGUID), new List<string>() { contentUpdateTestFileName });
            writeData.FileToBundle.Add(contentUpdateTestFileName, contentUpdateTestNewInternalBundleName);

            ContentUpdateScript.ContentUpdateContext context = new ContentUpdateScript.ContentUpdateContext()
            {
                WriteData = writeData,
                BundleToInternalBundleIdMap = bundleToInternalBundle,
                GuidToPreviousAssetStateMap = guidToCachedState,
                IdToCatalogDataEntryMap = idToCatalogEntryMap,
                ContentState = new AddressablesContentState(),
                PreviousAssetStateCarryOver = new List<CachedAssetState>(),
                Registry = new FileRegistry()
            };
            return context;
        }

        private AddressableAssetEntry CreateAssetEntry(string guid, AddressableAssetGroup group)
        {
            return new AddressableAssetEntry(guid, guid, group, false);
        }

        [Test]
        public void DetermineRequiredAssetEntryUpdates_AssetWithoutDependenciesAndChangedHashInStaticGroup_ReturnsOnlyRevertToCachedState()
        {
            string contentUpdateTestAssetGUID = GUID.Generate().ToString();
            string contentUpdateTestCachedAssetHash = "8888";
            string contentUpdateTestNewInternalBundleName = "bundle";
            string contentUpdateTestNewBundleName = "fullbundlepath";
            string contentUpdateTestCachedBundlePath = "cachedBundle";
            string contentUpdateTestFileName = "testfile";

            File.WriteAllText(contentUpdateTestCachedBundlePath, "TestCachedAssetBundle");
            File.WriteAllText(contentUpdateTestNewBundleName, "TestNewBundle");

            var group = Settings.CreateGroup("ContentUpdateTests", false, false, false, null, typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            string contentUpdateTestGroupGuid = group.Guid;

            group.GetSchema<ContentUpdateGroupSchema>().StaticContent = true;
            var assetEntry = CreateAssetEntry(contentUpdateTestAssetGUID, group);
            group.AddAssetEntry(assetEntry);

            var context = GetContentUpdateContext(contentUpdateTestAssetGUID, contentUpdateTestCachedAssetHash,
                contentUpdateTestNewInternalBundleName, contentUpdateTestNewBundleName,
                contentUpdateTestCachedBundlePath, contentUpdateTestGroupGuid, contentUpdateTestFileName);

            var ops = RevertUnchangedAssetsToPreviousAssetState.DetermineRequiredAssetEntryUpdates(group, context);

            Assert.AreEqual(assetEntry, ops[0].AssetEntry);
            Assert.AreEqual(contentUpdateTestNewBundleName, ops[0].CurrentBuildPath);
            Assert.AreEqual(contentUpdateTestCachedBundlePath, ops[0].PreviousBuildPath);

            File.Delete(contentUpdateTestCachedBundlePath);
            File.Delete(contentUpdateTestNewBundleName);
            Settings.RemoveGroup(group);
        }

        [Test]
        public void DetermineRequiredAssetEntryUpdates_AssetWithDependenciesAndChangedHashInStaticGroup_ReturnsRevertToCachedStateAndDependencies()
        {
            string contentUpdateTestAssetGUID = GUID.Generate().ToString();
            string contentUpdateTestCachedAssetHash = "8888";
            string contentUpdateTestNewInternalBundleName = "bundle";
            string contentUpdateTestNewBundleName = "fullbundlepath";
            string contentUpdateTestCachedBundlePath = "cachedBundle";
            string contentUpdateTestFileName = "testfile";

            File.WriteAllText(contentUpdateTestCachedBundlePath, "TestCachedAssetBundle");
            File.WriteAllText(contentUpdateTestNewBundleName, "TestNewBundle");

            var group = Settings.CreateGroup("ContentUpdateTests", false, false, false, null, typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            string contentUpdateTestGroupGuid = group.Guid;

            group.GetSchema<ContentUpdateGroupSchema>().StaticContent = true;
            var assetEntry = CreateAssetEntry(contentUpdateTestAssetGUID, group);
            group.AddAssetEntry(assetEntry);

            var context = GetContentUpdateContext(contentUpdateTestAssetGUID, contentUpdateTestCachedAssetHash,
                contentUpdateTestNewInternalBundleName, contentUpdateTestNewBundleName,
                contentUpdateTestCachedBundlePath, contentUpdateTestGroupGuid, contentUpdateTestFileName);
            context.GuidToPreviousAssetStateMap[contentUpdateTestAssetGUID].dependencies = new AssetState[]
            {
                new AssetState()
                {
                    guid = GUID.Generate(),
                    hash = Hash128.Parse("00000000000000")
                }
            };

            var ops = RevertUnchangedAssetsToPreviousAssetState.DetermineRequiredAssetEntryUpdates(group, context);

            Assert.AreEqual(assetEntry, ops[0].AssetEntry);
            Assert.AreEqual(contentUpdateTestNewBundleName, ops[0].CurrentBuildPath);
            Assert.AreEqual(contentUpdateTestCachedBundlePath, ops[0].PreviousBuildPath);

            File.Delete(contentUpdateTestCachedBundlePath);
            File.Delete(contentUpdateTestNewBundleName);
            Settings.RemoveGroup(group);
        }

        [Test]
        public void DetermineRequiredAssetEntryUpdates_AssetWithChangedGroup_TakesNoAction()
        {
            string contentUpdateTestAssetGUID = GUID.Generate().ToString();
            string contentUpdateTestCachedAssetHash = "8888";
            string contentUpdateTestNewInternalBundleName = "bundle";
            string contentUpdateTestNewBundleName = "fullbundlepath";
            string contentUpdateTestCachedBundlePath = "cachedBundle";
            string contentUpdateTestFileName = "testfile";

            File.WriteAllText(contentUpdateTestCachedBundlePath, "TestCachedAssetBundle");

            var group = Settings.CreateGroup("ContentUpdateTests", false, false, false, null, typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            string contentUpdateTestGroupGuid = GUID.Generate().ToString();

            group.GetSchema<ContentUpdateGroupSchema>().StaticContent = true;
            var assetEntry = CreateAssetEntry(contentUpdateTestAssetGUID, group);
            group.AddAssetEntry(assetEntry);

            var context = GetContentUpdateContext(contentUpdateTestAssetGUID, contentUpdateTestCachedAssetHash,
                contentUpdateTestNewInternalBundleName, contentUpdateTestNewBundleName,
                contentUpdateTestCachedBundlePath, contentUpdateTestGroupGuid, contentUpdateTestFileName);

            var ops = RevertUnchangedAssetsToPreviousAssetState.DetermineRequiredAssetEntryUpdates(group, context);

            Assert.IsTrue(ops.Count == 0);

            File.Delete(contentUpdateTestCachedBundlePath);
            Settings.RemoveGroup(group);
        }

        [Test]
        public void DetermineRequiredAssetEntryUpdates_AssetWithChangedHashInNonStaticGroup_TakesNoAction()
        {
            string contentUpdateTestAssetGUID = GUID.Generate().ToString();
            string contentUpdateTestCachedAssetHash = "8888";
            string contentUpdateTestNewInternalBundleName = "bundle";
            string contentUpdateTestNewBundleName = "fullbundlepath";
            string contentUpdateTestCachedBundlePath = "cachedBundle";
            string contentUpdateTestFileName = "testfile";

            File.WriteAllText(contentUpdateTestCachedBundlePath, "TestCachedAssetBundle");

            var group = Settings.CreateGroup("ContentUpdateTests", false, false, false, null, typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            string contentUpdateTestGroupGuid = GUID.Generate().ToString();

            group.GetSchema<ContentUpdateGroupSchema>().StaticContent = false;
            var assetEntry = CreateAssetEntry(contentUpdateTestAssetGUID, group);
            group.AddAssetEntry(assetEntry);

            var context = GetContentUpdateContext(contentUpdateTestAssetGUID, contentUpdateTestCachedAssetHash,
                contentUpdateTestNewInternalBundleName, contentUpdateTestNewBundleName,
                contentUpdateTestCachedBundlePath, contentUpdateTestGroupGuid, contentUpdateTestFileName);

            var ops = RevertUnchangedAssetsToPreviousAssetState.DetermineRequiredAssetEntryUpdates(group, context);

            Assert.IsTrue(ops.Count == 0);

            File.Delete(contentUpdateTestCachedBundlePath);
            Settings.RemoveGroup(group);
        }

        [Test]
        public void DetermineRequiredAssetEntryUpdates_EntriesThatTakeNoAction_StillSetBundleFileId()
        {
            string contentUpdateTestAssetGUID = GUID.Generate().ToString();
            string contentUpdateTestCachedAssetHash = "8888";
            string contentUpdateTestNewInternalBundleName = "bundle";
            string contentUpdateTestNewBundleName = "fullbundlepath";
            string contentUpdateTestCachedBundlePath = "cachedBundle";
            string contentUpdateTestFileName = "testfile";

            File.WriteAllText(contentUpdateTestCachedBundlePath, "TestCachedAssetBundle");

            var group = Settings.CreateGroup("ContentUpdateTests", false, false, false, null, typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            string contentUpdateTestGroupGuid = GUID.Generate().ToString();

            group.GetSchema<ContentUpdateGroupSchema>().StaticContent = false;
            var assetEntry = CreateAssetEntry(contentUpdateTestAssetGUID, group);
            group.AddAssetEntry(assetEntry);

            var context = GetContentUpdateContext(contentUpdateTestAssetGUID, contentUpdateTestCachedAssetHash,
                contentUpdateTestNewInternalBundleName, contentUpdateTestNewBundleName,
                contentUpdateTestCachedBundlePath, contentUpdateTestGroupGuid, contentUpdateTestFileName);

            var ops = RevertUnchangedAssetsToPreviousAssetState.DetermineRequiredAssetEntryUpdates(group, context);

            Assert.IsTrue(ops.Count == 0);
            Assert.AreEqual(contentUpdateTestNewBundleName, assetEntry.BundleFileId);

            File.Delete(contentUpdateTestCachedBundlePath);
            Settings.RemoveGroup(group);
        }

        [Test]
        public void DetermineRequiredAssetEntryUpdates_WithMissingBundleFileId_LogsErrorAndTakesNoAction()
        {
            string contentUpdateTestAssetGUID = GUID.Generate().ToString();
            string contentUpdateTestCachedAssetHash = "8888";
            string contentUpdateTestNewInternalBundleName = "bundle";
            string contentUpdateTestNewBundleName = "fullbundlepath";
            string contentUpdateTestCachedBundlePath = "cachedBundle";
            string contentUpdateTestFileName = "testfile";

            File.WriteAllText(contentUpdateTestCachedBundlePath, "TestCachedAssetBundle");

            var group = Settings.CreateGroup("ContentUpdateTests", false, false, false, null, typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            string contentUpdateTestGroupGuid = group.Guid;

            group.GetSchema<ContentUpdateGroupSchema>().StaticContent = true;
            var assetEntry = CreateAssetEntry(contentUpdateTestAssetGUID, group);
            group.AddAssetEntry(assetEntry);

            var context = GetContentUpdateContext(contentUpdateTestAssetGUID, contentUpdateTestCachedAssetHash,
                contentUpdateTestNewInternalBundleName, contentUpdateTestNewBundleName,
                "", contentUpdateTestGroupGuid, contentUpdateTestFileName);

            var ops = RevertUnchangedAssetsToPreviousAssetState.DetermineRequiredAssetEntryUpdates(group, context);

            LogAssert.Expect(LogType.Error, $"CachedAssetState found for {assetEntry.AssetPath} but the bundleFileId was never set on the previous build.");
            Assert.IsTrue(ops.Count == 0);

            File.Delete(contentUpdateTestCachedBundlePath);
            Settings.RemoveGroup(group);
        }

        [Test]
        public void DetermineRequiredAssetEntryUpdates_WithMissingPreviousBundle_LogsWarningAndTakesNoAction()
        {
            string contentUpdateTestAssetGUID = GUID.Generate().ToString();
            string contentUpdateTestCachedAssetHash = "8888";
            string contentUpdateTestNewInternalBundleName = "bundle";
            string contentUpdateTestNewBundleName = "fullbundlepath";
            string contentUpdateTestCachedBundlePath = "cachedBundle";
            string contentUpdateTestFileName = "testfile";

            var group = Settings.CreateGroup("ContentUpdateTests", false, false, false, null, typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            string contentUpdateTestGroupGuid = group.Guid;

            group.GetSchema<ContentUpdateGroupSchema>().StaticContent = true;
            var assetEntry = CreateAssetEntry(contentUpdateTestAssetGUID, group);
            group.AddAssetEntry(assetEntry);

            var context = GetContentUpdateContext(contentUpdateTestAssetGUID, contentUpdateTestCachedAssetHash,
                contentUpdateTestNewInternalBundleName, contentUpdateTestNewBundleName,
                contentUpdateTestCachedBundlePath, contentUpdateTestGroupGuid, contentUpdateTestFileName);

            var ops = RevertUnchangedAssetsToPreviousAssetState.DetermineRequiredAssetEntryUpdates(group, context);

            LogAssert.Expect(LogType.Warning, $"CachedAssetState found for {assetEntry.AssetPath} but the previous bundle at {contentUpdateTestCachedBundlePath} cannot be found. " +
                                              $"The modified assets will not be able to use the previously built bundle which will result in new bundles being created " +
                                              $"for these static content groups.  This will point the Content Catalog to local bundles that do not exist on currently " +
                                              $"deployed versions of an application.");
            Assert.IsTrue(ops.Count == 0);

            Settings.RemoveGroup(group);
        }

        [Test]
        public void DetermineRequiredAssetEntryUpdates_AssetWithMatchingCachedInternalBundleId_TakesNoAction()
        {
            string contentUpdateTestAssetGUID = GUID.Generate().ToString();
            string contentUpdateTestCachedAssetHash = "8888";
            string contentUpdateTestNewInternalBundleName = "bundle";
            string contentUpdateTestNewBundleName = "fullbundlepath";
            string contentUpdateTestCachedBundlePath = contentUpdateTestNewBundleName;
            string contentUpdateTestFileName = "testfile";

            File.WriteAllText(contentUpdateTestCachedBundlePath, "TestCachedAssetBundle");

            var group = Settings.CreateGroup("ContentUpdateTests", false, false, false, null, typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            string contentUpdateTestGroupGuid = group.Guid;

            group.GetSchema<ContentUpdateGroupSchema>().StaticContent = true;
            var assetEntry = CreateAssetEntry(contentUpdateTestAssetGUID, group);
            group.AddAssetEntry(assetEntry);

            var context = GetContentUpdateContext(contentUpdateTestAssetGUID, contentUpdateTestCachedAssetHash,
                contentUpdateTestNewInternalBundleName, contentUpdateTestNewBundleName,
                contentUpdateTestCachedBundlePath, contentUpdateTestGroupGuid, contentUpdateTestFileName);

            var ops = RevertUnchangedAssetsToPreviousAssetState.DetermineRequiredAssetEntryUpdates(group, context);

            Assert.IsTrue(ops.Count == 0);

            File.Delete(contentUpdateTestCachedBundlePath);
            Settings.RemoveGroup(group);
        }
    }
}