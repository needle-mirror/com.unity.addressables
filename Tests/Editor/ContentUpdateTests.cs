using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace UnityEditor.AddressableAssets.Tests
{
    public class ContentUpdateTests : AddressableAssetTestBase
    {
        protected override bool PersistSettings { get { return true; } }

        [Test]
        public void CanCreateContentStateData()
        {
            var group = m_Settings.CreateGroup("LocalStuff", false, false, false, null);
            var schema = group.AddSchema<BundledAssetGroupSchema>();
            schema.BuildPath.SetVariableByName(m_Settings, AddressableAssetSettings.kLocalBuildPath);
            schema.LoadPath.SetVariableByName(m_Settings, AddressableAssetSettings.kLocalLoadPath);
            schema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
            group.AddSchema<ContentUpdateGroupSchema>().StaticContent = true;

            m_Settings.CreateOrMoveEntry(m_AssetGUID, group);
            var context = new AddressablesBuildDataBuilderContext(m_Settings,
                BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget),
                EditorUserBuildSettings.activeBuildTarget,
                false, false,
                m_Settings.PlayerBuildVersion);

            var op = m_Settings.ActivePlayerDataBuilder.BuildData<AddressablesPlayerBuildResult>(context);

            Assert.IsTrue(string.IsNullOrEmpty(op.Error), op.Error);
            var tempPath = Path.GetDirectoryName(Application.dataPath) + "/Library/com.unity.addressables/addressables_content_state.bin";
            var cacheData = ContentUpdateScript.LoadContentState(tempPath);
            Assert.NotNull(cacheData);
        }

        [Test]
        public void PrepareContentUpdate()
        {
            var group = m_Settings.CreateGroup("LocalStuff2", false, false, false, null);
            var schema = group.AddSchema<BundledAssetGroupSchema>();
            schema.BuildPath.SetVariableByName(m_Settings, AddressableAssetSettings.kLocalBuildPath);
            schema.LoadPath.SetVariableByName(m_Settings, AddressableAssetSettings.kLocalLoadPath);
            schema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
            group.AddSchema<ContentUpdateGroupSchema>().StaticContent = true;

            var entry = m_Settings.CreateOrMoveEntry(m_AssetGUID, group);
            entry.address = "test";

            var context = new AddressablesBuildDataBuilderContext(m_Settings,
                BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget),
                EditorUserBuildSettings.activeBuildTarget,
                false, false,
                m_Settings.PlayerBuildVersion);

            m_Settings.ActivePlayerDataBuilder.BuildData<AddressablesPlayerBuildResult>(context);

            var path = AssetDatabase.GUIDToAssetPath(m_AssetGUID);
            var obj = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            obj.GetComponent<Transform>().SetPositionAndRotation(new Vector3(10, 10, 10), Quaternion.identity);
#if UNITY_2018_3_OR_NEWER
            PrefabUtility.SavePrefabAsset(obj);
#else
            EditorUtility.SetDirty(obj);
#endif
            AssetDatabase.SaveAssets();
            var tempPath = Path.GetDirectoryName(Application.dataPath) + "/Library/com.unity.addressables/addressables_content_state.bin";
            var modifiedEntries = ContentUpdateScript.GatherModifiedEntries(m_Settings, tempPath);
            Assert.IsNotNull(modifiedEntries);
            Assert.GreaterOrEqual(modifiedEntries.Count, 1);
            ContentUpdateScript.CreateContentUpdateGroup(m_Settings, modifiedEntries, "Content Update");
            var contentGroup = m_Settings.FindGroup("Content Update");
            Assert.IsNotNull(contentGroup);
            var movedEntry = contentGroup.GetAssetEntry(m_AssetGUID);
            Assert.AreSame(movedEntry, entry);
        }

        [Test]
        public void BuildContentUpdate()
        {
            var group = m_Settings.CreateGroup("LocalStuff3", false, false, false, null);
            var schema = group.AddSchema<BundledAssetGroupSchema>();
            schema.BuildPath.SetVariableByName(m_Settings, AddressableAssetSettings.kLocalBuildPath);
            schema.LoadPath.SetVariableByName(m_Settings, AddressableAssetSettings.kLocalLoadPath);
            schema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
            group.AddSchema<ContentUpdateGroupSchema>().StaticContent = true;
            m_Settings.CreateOrMoveEntry(m_AssetGUID, group);
            var context = new AddressablesBuildDataBuilderContext(m_Settings,
                BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget),
                EditorUserBuildSettings.activeBuildTarget,
                false, false,
                m_Settings.PlayerBuildVersion);

            var op = m_Settings.ActivePlayerDataBuilder.BuildData<AddressablesPlayerBuildResult>(context);

            Assert.IsTrue(string.IsNullOrEmpty(op.Error), op.Error);
            var tempPath = Path.GetDirectoryName(Application.dataPath) + "/Library/com.unity.addressables/addressables_content_state.bin";
            var buildOp = ContentUpdateScript.BuildContentUpdate(m_Settings, tempPath);
            Assert.IsNotNull(buildOp);
            Assert.IsTrue(string.IsNullOrEmpty(buildOp.Error));
        }
    }
}