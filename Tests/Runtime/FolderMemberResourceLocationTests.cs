using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
#endif

namespace FolderMemberResourceLocationTests
{
    // Hero and Villain are in an Addressable folder in a Prevent Updates group. After a full build, Hero is modified and the
    // restrictions check moves it into a content update group, and the tests load from a build of the resulting settings.
    public abstract class FolderMemberResourceLocationTestsBase : AddressablesTestFixture
    {
        const string k_FolderAddress = "FolderMemberFolder";
        const string k_HeroAddress = k_FolderAddress + "/Hero.prefab";
        const string k_UpdateGroupName = "FolderMemberContentUpdate";

        protected virtual bool IncludeAddressesForFolderChildren => true;

#if UNITY_EDITOR
        internal override void Setup(AddressableAssetSettings settings, string tempAssetFolder)
        {
            string folderPath = $"{tempAssetFolder}/{k_FolderAddress}";
            Directory.CreateDirectory(folderPath);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            string heroPath = $"{folderPath}/Hero.prefab";
            string heroGuid = CreatePrefab(heroPath);
            CreatePrefab($"{folderPath}/Villain.prefab");

            var staticGroup = settings.CreateGroup("FolderMemberStatic", false, false, true, null, typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            var schema = staticGroup.GetSchema<BundledAssetGroupSchema>();
            schema.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
            schema.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
            schema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
            schema.IncludeFolderKeysInCatalog = true;
            schema.IncludeAddressesForFolderChildren = IncludeAddressesForFolderChildren;
            staticGroup.GetSchema<ContentUpdateGroupSchema>().StaticContent = true;

            var folder = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(folderPath), staticGroup);
            folder.address = k_FolderAddress;
            AssetDatabase.SaveAssets();

            var fullBuild = settings.ActivePlayerDataBuilder.BuildData<AddressablesPlayerBuildResult>(new AddressablesDataBuilderInput(settings));
            Assert.IsTrue(string.IsNullOrEmpty(fullBuild.Error), fullBuild.Error);

            var hero = AssetDatabase.LoadAssetAtPath<GameObject>(heroPath);
            hero.transform.position += Vector3.one;
            PrefabUtility.SavePrefabAsset(hero);
            AssetDatabase.SaveAssets();

            string contentStatePath = ContentUpdateScript.GetContentStateDataPath(false, settings);
            ContentUpdateScript.CreateContentUpdateGroup(settings, ContentUpdateScript.GatherModifiedEntries(settings, contentStatePath), k_UpdateGroupName);
            Assert.IsTrue(settings.FindAssetEntry(heroGuid).IsFolderMember, "The restrictions check should have moved Hero out of the folder.");

            // The update group is remote by default; load it locally so the tests need no server.
            var updateSchema = settings.FindGroup(k_UpdateGroupName).GetSchema<BundledAssetGroupSchema>();
            updateSchema.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
            updateSchema.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
        }
#endif

        [UnityTest]
        public IEnumerator LoadAssetsAsync_WithFolderAddress_LoadsTheMovedAssetFromItsOwnBundle()
        {
            var locationsHandle = m_Addressables.LoadResourceLocationsAsync(k_FolderAddress, typeof(GameObject));
            yield return locationsHandle;
            var bundles = new Dictionary<string, string>();
            foreach (var location in locationsHandle.Result)
                bundles.Add(Path.GetFileNameWithoutExtension(location.InternalId), location.Dependencies != null && location.Dependencies.Count > 0 ? location.Dependencies[0].InternalId : null);
            if (BuildScriptMode != TestBuildScriptMode.Fast)
                Assert.AreNotEqual(bundles["Villain"], bundles["Hero"]);

            var handle = m_Addressables.LoadAssetsAsync<GameObject>(locationsHandle.Result, null, true);
            yield return handle;

            var names = new List<string>();
            foreach (var go in handle.Result)
                names.Add(go.name);
            CollectionAssert.AreEquivalent(new[] { "Hero", "Villain" }, names);
            handle.Release();
            locationsHandle.Release();
        }

        [UnityTest]
        public IEnumerator LoadResourceLocationsAsync_WithMovedAssetAddress_FollowsTheFolderGroupSetting()
        {
            var handle = m_Addressables.LoadResourceLocationsAsync(k_HeroAddress, typeof(GameObject));
            yield return handle;

            Assert.AreEqual(IncludeAddressesForFolderChildren ? 1 : 0, handle.Result.Count);
            handle.Release();
        }
    }

    public abstract class FolderMemberResourceLocationTests : FolderMemberResourceLocationTestsBase
    {
    }

    public abstract class FolderMemberExcludeAddressResourceLocationTests : FolderMemberResourceLocationTestsBase
    {
        protected override bool IncludeAddressesForFolderChildren => false;
    }

#if UNITY_EDITOR
    class FolderMemberResourceLocationTests_FastMode : FolderMemberResourceLocationTests
    {
        protected override TestBuildScriptMode BuildScriptMode => TestBuildScriptMode.Fast;
    }

    class FolderMemberResourceLocationTests_PackedPlaymodeMode : FolderMemberResourceLocationTests
    {
        protected override TestBuildScriptMode BuildScriptMode => TestBuildScriptMode.PackedPlaymode;
    }

    class FolderMemberExcludeAddressResourceLocationTests_FastMode : FolderMemberExcludeAddressResourceLocationTests
    {
        protected override TestBuildScriptMode BuildScriptMode => TestBuildScriptMode.Fast;
    }

    class FolderMemberExcludeAddressResourceLocationTests_PackedPlaymodeMode : FolderMemberExcludeAddressResourceLocationTests
    {
        protected override TestBuildScriptMode BuildScriptMode => TestBuildScriptMode.PackedPlaymode;
    }
#endif

    [UnityPlatform(exclude = new[] {RuntimePlatform.WindowsEditor, RuntimePlatform.OSXEditor, RuntimePlatform.LinuxEditor})]
    class FolderMemberResourceLocationTests_PackedMode : FolderMemberResourceLocationTests
    {
        protected override TestBuildScriptMode BuildScriptMode => TestBuildScriptMode.Packed;
    }

    [UnityPlatform(exclude = new[] {RuntimePlatform.WindowsEditor, RuntimePlatform.OSXEditor, RuntimePlatform.LinuxEditor})]
    class FolderMemberExcludeAddressResourceLocationTests_PackedMode : FolderMemberExcludeAddressResourceLocationTests
    {
        protected override TestBuildScriptMode BuildScriptMode => TestBuildScriptMode.Packed;
    }
}
