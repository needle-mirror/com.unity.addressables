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
            var group = m_settings.CreateGroup("LocalStuff", false, false, false);
            var schema = group.AddSchema<BundledAssetGroupSchema>();
            schema.BuildPath.SetVariableByName(m_settings, AddressableAssetSettings.kLocalBuildPath);
            schema.LoadPath.SetVariableByName(m_settings, AddressableAssetSettings.kLocalLoadPath);
            schema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
            group.AddSchema<ContentUpdateGroupSchema>().StaticContent = true;

            m_settings.CreateOrMoveEntry(assetGUID, group);
            var context = new AddressablesBuildDataBuilderContext(m_settings,
                BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget),
                EditorUserBuildSettings.activeBuildTarget,
                false, false,
                m_settings.PlayerBuildVersion);

            var op = m_settings.ActivePlayerDataBuilder.BuildData<AddressablesPlayerBuildResult>(context);

            Assert.IsTrue(string.IsNullOrEmpty(op.Error), op.Error);
            var cacheData = ContentUpdateScript.LoadContentState(op.ContentStateDataPath);
            Assert.NotNull(cacheData);
        }

        [Test]
        public void PrepareContentUpdate()
        {
            var group = m_settings.CreateGroup("LocalStuff2", false, false, false);
            var schema = group.AddSchema<BundledAssetGroupSchema>();
            schema.BuildPath.SetVariableByName(m_settings, AddressableAssetSettings.kLocalBuildPath);
            schema.LoadPath.SetVariableByName(m_settings, AddressableAssetSettings.kLocalLoadPath);
            schema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
            group.AddSchema<ContentUpdateGroupSchema>().StaticContent = true;

            var entry = m_settings.CreateOrMoveEntry(assetGUID, group);
            entry.address = "test";

            var context = new AddressablesBuildDataBuilderContext(m_settings,
                BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget),
                EditorUserBuildSettings.activeBuildTarget,
                false, false,
                m_settings.PlayerBuildVersion);

            var op = m_settings.ActivePlayerDataBuilder.BuildData<AddressablesPlayerBuildResult>(context);

            var path = AssetDatabase.GUIDToAssetPath(assetGUID);
            var obj = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            obj.GetComponent<Transform>().SetPositionAndRotation(new Vector3(10, 10, 10), Quaternion.identity);
#if UNITY_2018_3_OR_NEWER
            PrefabUtility.SavePrefabAsset(obj);
#else
            EditorUtility.SetDirty(obj);
#endif
            AssetDatabase.SaveAssets();
            var modifiedEntries = ContentUpdateScript.GatherModifiedEntries(m_settings, op.ContentStateDataPath);
            Assert.IsNotNull(modifiedEntries);
            Assert.GreaterOrEqual(modifiedEntries.Count, 1);
            ContentUpdateScript.CreateContentUpdateGroup(m_settings, modifiedEntries, "Content Update");
            var contentGroup = m_settings.FindGroup("Content Update");
            Assert.IsNotNull(contentGroup);
            var movedEntry = contentGroup.GetAssetEntry(assetGUID);
            Assert.AreSame(movedEntry, entry);
        }

        [Test]
        public void BuildContentUpdate()
        {
            var group = m_settings.CreateGroup("LocalStuff3", false, false, false);
            var schema = group.AddSchema<BundledAssetGroupSchema>();
            schema.BuildPath.SetVariableByName(m_settings, AddressableAssetSettings.kLocalBuildPath);
            schema.LoadPath.SetVariableByName(m_settings, AddressableAssetSettings.kLocalLoadPath);
            schema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
            group.AddSchema<ContentUpdateGroupSchema>().StaticContent = true;
            m_settings.CreateOrMoveEntry(assetGUID, group);
            var context = new AddressablesBuildDataBuilderContext(m_settings,
                BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget),
                EditorUserBuildSettings.activeBuildTarget,
                false, false,
                m_settings.PlayerBuildVersion);

            var op = m_settings.ActivePlayerDataBuilder.BuildData<AddressablesPlayerBuildResult>(context);

            Assert.IsTrue(string.IsNullOrEmpty(op.Error), op.Error);
            var buildOp = ContentUpdateScript.BuildContentUpdate(m_settings, op.ContentStateDataPath);
            Assert.IsNotNull(buildOp);
            Assert.IsTrue(string.IsNullOrEmpty(buildOp.Error));
        }
    }
}