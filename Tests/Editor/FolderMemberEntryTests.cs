using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor.AddressableAssets.GUI;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using UnityEngine.AddressableAssets.ResourceLocators;

namespace UnityEditor.AddressableAssets.Tests
{
    // Each test starts with Hero moved out of the folder into m_OtherGroup, the way a content update moves it.
    public class FolderMemberEntryTests : AddressableAssetTestBase
    {
        const string k_FolderAddress = "Characters";
        const string k_FolderLabel = "characterLabel";

        AddressableAssetGroup m_HomeGroup;
        AddressableAssetGroup m_OtherGroup;
        string m_FolderPath;
        AddressableAssetEntry m_FolderEntry;
        AddressableAssetEntry m_Member;
        string m_HeroGuid;
        string m_VillainGuid;

        protected override void OnInit()
        {
            m_FolderPath = GetAssetPath("MemberFolder");
            CreateAsset(m_FolderPath + "/Hero.prefab", "Hero");
            CreateAsset(m_FolderPath + "/Villain.prefab", "Villain");
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            m_HeroGuid = AssetDatabase.AssetPathToGUID(m_FolderPath + "/Hero.prefab");
            m_VillainGuid = AssetDatabase.AssetPathToGUID(m_FolderPath + "/Villain.prefab");
        }

        [SetUp]
        public void SetUpGroups()
        {
            m_HomeGroup = Settings.CreateGroup("MemberHomeGroup", false, false, false, null, typeof(BundledAssetGroupSchema));
            m_OtherGroup = Settings.CreateGroup("MemberOtherGroup", false, false, false, null, typeof(BundledAssetGroupSchema));
            m_FolderEntry = Settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(m_FolderPath), m_HomeGroup);
            m_FolderEntry.SetAddress(k_FolderAddress);
            m_FolderEntry.SetLabel(k_FolderLabel, true, true);

            m_Member = GetFolderChild(m_HeroGuid);
            m_Member.ParentEntry = null;
            m_Member.SetHomeFolder(m_FolderEntry, false);
            Settings.MoveEntry(m_Member, m_OtherGroup);
        }

        [TearDown]
        public void TearDownGroups()
        {
            if (Settings.groups.Contains(m_OtherGroup))
                Settings.RemoveGroup(m_OtherGroup);
            if (Settings.groups.Contains(m_HomeGroup))
                Settings.RemoveGroup(m_HomeGroup);
            Settings.RemoveAssetEntry(m_HeroGuid);
            Settings.RemoveAssetEntry(m_VillainGuid);
        }

        AddressableAssetEntry GetFolderChild(string guid)
        {
            var children = new List<AddressableAssetEntry>();
            m_FolderEntry.GatherAllAssets(children, false, true, false);
            return children.Find(e => e.guid == guid);
        }

        static List<object> GetCatalogKeys(AddressableAssetEntry entry, BundledAssetGroupSchema schema)
        {
            var catalogEntries = new List<ContentCatalogDataEntry>();
            entry.CreateCatalogEntries(catalogEntries, false, "provider", null, null, null, new HashSet<Type>(),
                schema.IncludeAddressInCatalog, schema.IncludeGUIDInCatalog, schema.IncludeLabelsInCatalog, null,
                schema.IncludeFolderKeysInCatalog, schema.IncludeAddressesForFolderChildren);
            return new List<object>(catalogEntries[0].Keys);
        }

        void DragInGroupsWindow(AssetEntryTreeViewItem draggedRow, AddressableAssetGroup target)
        {
            InGroupsWindow(target, (tree, targetRow) => tree.HandleDragAndDropItems(new List<AssetEntryTreeViewItem> { draggedRow }, targetRow, targetRow, 0, true));
        }

        void InGroupsWindow(AddressableAssetGroup target, Action<AddressableAssetEntryTreeView, AssetEntryTreeViewItem> drop)
        {
            var editor = new AddressableAssetsSettingsGroupEditor(null) { settings = Settings };
            editor.InitialiseEntryTree();
            try
            {
                drop(editor.m_EntryTree, new AssetEntryTreeViewItem(target, 0));
            }
            finally
            {
                editor.OnDisable();
            }
        }

        [Test]
        public void FolderMember_AddressAndLabels_FollowTheFolder()
        {
            m_FolderEntry.SetAddress("Renamed");
            m_FolderEntry.SetLabel("addedLabel", true, true);
            m_FolderEntry.SetLabel(k_FolderLabel, false);
            Settings.RenameLabel("addedLabel", "renamedLabel");

            Assert.AreEqual("Renamed/Hero.prefab", m_Member.address);
            CollectionAssert.AreEquivalent(new[] { "renamedLabel" }, m_Member.labels);
        }

        [Test]
        public void FolderGather_LeavesOutFolderMembers()
        {
            Assert.IsNull(GetFolderChild(m_HeroGuid));
            Assert.IsNotNull(GetFolderChild(m_VillainGuid));
        }

        [Test]
        public void FolderMember_CatalogKeys_TakeFolderKeySettingsFromTheFolderGroup()
        {
            var memberSchema = m_OtherGroup.GetSchema<BundledAssetGroupSchema>();
            memberSchema.IncludeFolderKeysInCatalog = false;
            memberSchema.IncludeLabelsInCatalog = false;
            m_HomeGroup.GetSchema<BundledAssetGroupSchema>().IncludeAddressesForFolderChildren = false;

            var keys = GetCatalogKeys(m_Member, memberSchema);

            CollectionAssert.Contains(keys, k_FolderAddress);
            CollectionAssert.DoesNotContain(keys, m_Member.address);
            CollectionAssert.DoesNotContain(keys, k_FolderLabel);
        }

        [Test]
        public void MoveEntry_WhenMemberAndFolderEndUpInOneGroup_ReturnsTheMemberToTheFolder([Values] bool moveFolder)
        {
            if (moveFolder)
                Settings.MoveEntry(m_FolderEntry, m_OtherGroup);
            else
                Settings.MoveEntry(m_Member, m_HomeGroup);

            Assert.IsNull(m_OtherGroup.GetAssetEntry(m_HeroGuid));
            Assert.IsNull(m_HomeGroup.GetAssetEntry(m_HeroGuid));
            Assert.IsNotNull(GetFolderChild(m_HeroGuid));
        }

        [Test]
        public void FolderMember_WhenReaddressedOrMadeAddressableInAnotherGroup_BecomesAnOrdinaryEntry([Values] bool viaSetAddress)
        {
            if (viaSetAddress)
                m_Member.SetAddress("CustomHero");
            else
                Assert.AreSame(m_Member, Settings.CreateOrMoveEntry(m_HeroGuid, m_HomeGroup));

            Assert.IsFalse(m_Member.IsFolderMember);
            Assert.AreSame(viaSetAddress ? m_OtherGroup : m_HomeGroup, m_Member.parentGroup);
            Assert.AreEqual(viaSetAddress ? "CustomHero" : k_FolderAddress + "/Hero.prefab", m_Member.address);
        }

        [Test]
        public void CreateOrMoveEntry_IntoTheMembersOwnGroup_KeepsItInTheFolder([Values] bool batch)
        {
            if (batch)
                Settings.CreateOrMoveEntries(new[] { m_HeroGuid }, m_OtherGroup);
            else
                Settings.CreateOrMoveEntry(m_HeroGuid, m_OtherGroup);

            Assert.AreSame(m_OtherGroup, m_Member.parentGroup);
            Assert.IsTrue(m_Member.IsFolderMember);
        }

        [Test]
        public void MarkingAMemberAddressable_InItsOwnGroup_MakesItAnOrdinaryEntry([Values] bool fromInspector)
        {
            var previousDefaultGroup = Settings.DefaultGroup;
            try
            {
                if (fromInspector)
                {
                    Settings.DefaultGroup = m_OtherGroup;
                    var target = new AddressableAssetInspectorGUI.TargetInfo { Guid = m_HeroGuid, Path = AssetDatabase.GUIDToAssetPath(m_HeroGuid), IsMainAsset = true, MainAssetEntry = m_Member };
                    AddressableAssetInspectorGUI.SetAaEntry(Settings, new List<AddressableAssetInspectorGUI.TargetInfo> { target }, true);
                }
                else
                {
                    InGroupsWindow(m_OtherGroup, (tree, targetRow) => tree.HandleDragAndDropPaths(new[] { AssetDatabase.GUIDToAssetPath(m_HeroGuid) }, targetRow, true));
                }

                Assert.AreSame(m_OtherGroup, m_Member.parentGroup);
                Assert.IsFalse(m_Member.IsFolderMember);
            }
            finally
            {
                Settings.DefaultGroup = previousDefaultGroup;
            }
        }

        [Test]
        public void GroupsWindowDrop_FromProjectOntoFolderHeading_AddsTheAssetToTheHeadingsGroup()
        {
            InGroupsWindow(m_OtherGroup, (tree, groupRow) =>
                tree.HandleDragAndDropPaths(new[] { AssetDatabase.GUIDToAssetPath(m_VillainGuid) }, new AssetEntryTreeViewItem(m_FolderEntry, m_OtherGroup, 1), true));

            Assert.AreSame(m_OtherGroup, Settings.FindAssetEntry(m_VillainGuid)?.parentGroup);
        }

        [Test]
        public void FolderMember_WhileItsFolderEntryIsMissing_IsAnOrdinaryEntry()
        {
            m_HomeGroup.RemoveAssetEntry(m_FolderEntry, false);

            Assert.IsFalse(m_Member.IsFolderMember);
            Assert.IsNull(m_Member.ParentFolderAddress);

            m_HomeGroup.AddAssetEntry(m_FolderEntry, false);

            Assert.IsTrue(m_Member.IsFolderMember, "The link should come back once the folder entry does.");
        }

        [Test]
        public void MoveEntriesToGroup_OnFolderAsset_MakesItAnOrdinaryEntry([Values] bool intoFolderGroup)
        {
            var child = GetFolderChild(m_VillainGuid);
            Assert.IsTrue(child.ReadOnly);
            var targetGroup = intoFolderGroup ? m_HomeGroup : m_OtherGroup;

            AddressableAssetUtility.MoveEntriesToGroup(Settings, new List<AddressableAssetEntry> { child }, targetGroup);

            var entry = Settings.FindAssetEntry(m_VillainGuid);
            CollectionAssert.Contains(targetGroup.entries, entry);
            Assert.IsFalse(entry.ReadOnly, "A folder asset moved out from under its folder should be editable.");
            Assert.IsFalse(entry.IsFolderMember);
            Assert.IsNull(entry.ParentFolderAddress, "It is no longer part of the folder, so it should not get the folder key.");
            Assert.AreEqual(k_FolderAddress + "/Villain.prefab", entry.address);
            Assert.IsNull(GetFolderChild(m_VillainGuid));
        }

        [Test]
        public void GroupsWindowDrag_FolderHeading_MovesItsMembersStillLinked()
        {
            var thirdGroup = Settings.CreateGroup("MemberThirdGroup", false, false, false, null, typeof(BundledAssetGroupSchema));
            try
            {
                DragInGroupsWindow(new AssetEntryTreeViewItem(m_FolderEntry, m_OtherGroup, 1), thirdGroup);

                Assert.AreSame(thirdGroup, m_Member.parentGroup);
                Assert.IsTrue(m_Member.IsFolderMember);
            }
            finally
            {
                Settings.RemoveGroup(thirdGroup);
            }
        }

        [Test]
        public void GroupsWindowDrag_SingleMember_TakesItOutOfTheFolder([Values] bool sameGroup)
        {
            var target = sameGroup ? m_OtherGroup : m_HomeGroup;

            DragInGroupsWindow(new AssetEntryTreeViewItem(m_Member, 2), target);

            CollectionAssert.Contains(target.entries, m_Member);
            Assert.IsFalse(m_Member.IsFolderMember);
            Assert.IsFalse(m_Member.ReadOnly);
        }

        [Test]
        public void RemovingTheFolder_RemovesMembersLikeFolderAssets([Values] bool removeGroup)
        {
            if (removeGroup)
                Settings.RemoveGroup(m_HomeGroup);
            else
                Settings.RemoveAssetEntry(m_FolderEntry.guid);

            CollectionAssert.DoesNotContain(m_OtherGroup.entries, m_Member);
            Assert.IsNull(Settings.FindAssetEntry(m_HeroGuid, true));
            Assert.IsNull(Settings.FindAssetEntry(m_VillainGuid, true));
        }

        [TestCase(true, k_FolderAddress + "/Sub/Hero.prefab")]
        [TestCase(false, k_FolderAddress + "/Hero.prefab")]
        public void OnPostprocessAllAssets_WhenMemberIsMoved_FollowsOrLeavesTheFolder(bool withinFolder, string expectedAddress)
        {
            if (withinFolder)
                AssetDatabase.CreateFolder(m_FolderPath, "Sub");
            string oldPath = m_FolderPath + "/Hero.prefab";
            string newPath = withinFolder ? m_FolderPath + "/Sub/Hero.prefab" : GetAssetPath("Hero.prefab");
            Assert.IsEmpty(AssetDatabase.MoveAsset(oldPath, newPath));
            try
            {
                m_Member.RepointToNewPath(newPath);
                Settings.OnPostprocessAllAssets(new string[0], new string[0], new[] { newPath }, new[] { oldPath });

                Assert.AreEqual(withinFolder, m_Member.IsFolderMember);
                Assert.AreEqual(expectedAddress, m_Member.address);
            }
            finally
            {
                AssetDatabase.MoveAsset(newPath, oldPath);
                if (withinFolder)
                    AssetDatabase.DeleteAsset(m_FolderPath + "/Sub");
            }
        }
    }
}
