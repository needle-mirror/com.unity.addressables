using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor.AddressableAssets.GUI;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.AddressableAssets.Tests;
using UnityEngine;

namespace Tests.Editor.GUI
{
    public class GroupsPopupTests : AddressableAssetTestBase
    {
        // The popup tree reads its groups from AddressableAssetSettingsDefaultObject.Settings.
        protected override bool ManageDefaultSettings => true;

        AddressableAssetGroup m_BundledGroup;
        AddressableAssetGroup m_DisabledBundledGroup;
        AddressableAssetGroup m_ContentDirectoryGroup;
        AddressableAssetGroup m_DisabledContentDirectoryGroup;
        AddressableAssetGroup m_NoBuildableSchemaGroup;

        [SetUp]
        public void SetUp()
        {
            m_BundledGroup = Settings.CreateGroup("PopupBundled", false, false, false, null, typeof(BundledAssetGroupSchema));
            m_DisabledBundledGroup = Settings.CreateGroup("PopupDisabledBundled", false, false, false, null, typeof(BundledAssetGroupSchema));
            m_DisabledBundledGroup.GetSchema<BundledAssetGroupSchema>().IsEnabled = false;
            m_ContentDirectoryGroup = Settings.CreateGroup("PopupContentDirectory", false, false, false, null, typeof(ContentDirectoryGroupSchema));
            m_DisabledContentDirectoryGroup = Settings.CreateGroup("PopupDisabledContentDirectory", false, false, false, null, typeof(ContentDirectoryGroupSchema));
            m_DisabledContentDirectoryGroup.GetSchema<ContentDirectoryGroupSchema>().IsEnabled = false;
            m_NoBuildableSchemaGroup = Settings.CreateGroup("PopupNoBuildableSchema", false, false, false, null);
        }

        [TearDown]
        public void TearDown()
        {
            Settings.RemoveGroup(m_BundledGroup);
            Settings.RemoveGroup(m_DisabledBundledGroup);
            Settings.RemoveGroup(m_ContentDirectoryGroup);
            Settings.RemoveGroup(m_DisabledContentDirectoryGroup);
            Settings.RemoveGroup(m_NoBuildableSchemaGroup);
        }

        static List<AddressableAssetGroup> GetListedGroups(bool requireBundledSchema)
        {
            var popup = new GroupsPopupContent(Rect.zero, null, true, requireBundledSchema, false, null, null, null);
            var tree = new GroupsPopupContent.GroupsPopupTreeView(new AddressableAssetEntryTreeViewState(), popup, true);
            tree.Reload();
            return tree.GetRows().OfType<GroupsPopupContent.GroupsPopupTreeItem>().Select(item => item.m_Group).ToList();
        }

        [Test]
        public void GroupsPopup_WhenBundledSchemaNotRequired_ListsGroupsWithEnabledBuildableSchema()
        {
            var groups = GetListedGroups(false);

            CollectionAssert.Contains(groups, m_BundledGroup);
#if ENABLE_CONTENT_DIRECTORIES
            CollectionAssert.Contains(groups, m_ContentDirectoryGroup);
#else
            CollectionAssert.DoesNotContain(groups, m_ContentDirectoryGroup);
#endif
            CollectionAssert.DoesNotContain(groups, m_DisabledBundledGroup);
            CollectionAssert.DoesNotContain(groups, m_DisabledContentDirectoryGroup);
            CollectionAssert.DoesNotContain(groups, m_NoBuildableSchemaGroup);
        }

        [Test]
        public void GroupsPopup_WhenBundledSchemaRequired_ListsOnlyGroupsWithEnabledBundledSchema()
        {
            var groups = GetListedGroups(true);

            CollectionAssert.Contains(groups, m_BundledGroup);
            CollectionAssert.DoesNotContain(groups, m_ContentDirectoryGroup);
            CollectionAssert.DoesNotContain(groups, m_DisabledBundledGroup);
            CollectionAssert.DoesNotContain(groups, m_DisabledContentDirectoryGroup);
            CollectionAssert.DoesNotContain(groups, m_NoBuildableSchemaGroup);
        }
    }
}
