using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine.AddressableAssets;
using System.Collections.Generic;
using UnityEngine.ResourceManagement;
using System.Linq;
using System.IO;

namespace UnityEditor.AddressableAssets.Tests
{
    public class ContentUpdateTests : AddressableAssetTestBase
    {
        [Test]
        public void CanCreateCachedData()
        {
            var group = settings.CreateGroup("LocalStuff", typeof(BundledAssetGroupProcessor), false, false);
            group.StaticContent = true;
            settings.CreateOrMoveEntry(assetGUID, group);
            string cacheDataPath;
            var buildResult = BuildScript.PrepareRuntimeData(settings, true, false, false, true, false,
                BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget),
                EditorUserBuildSettings.activeBuildTarget,
                settings.PlayerBuildVersion,
                ResourceManagerRuntimeData.EditorPlayMode.PackedMode, out cacheDataPath);
            Assert.IsTrue(buildResult, "PrepareRuntimeData failed.");
            Debug.LogFormat("cache data {0}", cacheDataPath);
            var cacheData = ContentUpdateScript.LoadCacheData(cacheDataPath);
            Assert.NotNull(cacheData);
        }

        [Test]
        public void PrepareContentUpdate()
        {
            var group = settings.CreateGroup("LocalStuff2", typeof(BundledAssetGroupProcessor), false, false);
            group.StaticContent = true;
            var entry = settings.CreateOrMoveEntry(assetGUID, group);
            entry.address = "test";
            string cacheDataPath;
            var buildResult = BuildScript.PrepareRuntimeData(settings, true, false, false, true, false,
                BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget),
                EditorUserBuildSettings.activeBuildTarget,
                settings.PlayerBuildVersion,
                ResourceManagerRuntimeData.EditorPlayMode.PackedMode, out cacheDataPath);
            Assert.IsTrue(buildResult, "PrepareRuntimeData failed.");
            Debug.LogFormat("cache data {0}", cacheDataPath);
            var obj = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(assetGUID));
            obj.AddComponent<Rigidbody>();
            AssetDatabase.SaveAssets();
            var result = ContentUpdateScript.PrepareForContentUpdate(settings, Path.GetDirectoryName(cacheDataPath), false);
            Assert.IsTrue(result);
            var contentGroup = settings.FindGroup("Content Update");
            Assert.IsNotNull(contentGroup);
            var movedEntry = contentGroup.GetAssetEntry(assetGUID);
            Assert.AreSame(movedEntry, entry);
        }

        [Test]
        public void BuildContentUpdate()
        {
            var group = settings.CreateGroup("LocalStuff3", typeof(BundledAssetGroupProcessor), false, false);
            group.StaticContent = true;
            settings.CreateOrMoveEntry(assetGUID, group);
            string cacheDataPath;
            var buildResult = BuildScript.PrepareRuntimeData(settings, true, false, false, true, false,
                BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget),
                EditorUserBuildSettings.activeBuildTarget,
                settings.PlayerBuildVersion,
                ResourceManagerRuntimeData.EditorPlayMode.PackedMode, out cacheDataPath);
            Assert.IsTrue(buildResult, "PrepareRuntimeData failed.");
            var result = ContentUpdateScript.BuildContentUpdate(settings, Path.GetDirectoryName(cacheDataPath));
            Assert.IsTrue(result);
        }
    }
}