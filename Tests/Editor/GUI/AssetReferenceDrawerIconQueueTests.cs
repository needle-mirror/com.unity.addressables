using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.AddressableAssets.GUI;
using UnityEditor.AddressableAssets.GUI.Adapters;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace UnityEditor.AddressableAssets.Tests
{
    class AssetReferenceDrawerIconQueueTests : AssetReferenceDrawerTestsFixture
    {
        static readonly FieldInfo s_queueField =
            typeof(IconLazyLoad).GetField("m_needsIconRefresh",
                BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly FieldInfo s_assetPathField =
            typeof(IconLazyLoad.IconRequest).GetField("m_AssetPath",
                BindingFlags.NonPublic | BindingFlags.Instance);

        IconLazyLoad m_Loader;
        AssetReferencePopup.AssetReferenceTreeView m_Tree;

        [SetUp]
        public void SetUpTree()
        {
            SetupDefaultSettings();
            var drawer = new AssetReferenceDrawer();
            drawer.m_AssetRefObject = new AssetReference();
            drawer.m_Restrictions = new List<AssetReferenceUIRestrictionSurrogate>();
            m_Loader = new IconLazyLoad();
            var popup = ScriptableObject.CreateInstance<AssetReferencePopup>();
            m_Tree = new AssetReferencePopup.AssetReferenceTreeView(
                new TreeViewStateAdapter(), drawer, popup, "", "");
            m_Tree.SetLazyLoader(m_Loader, () => { });
        }

        [TearDown]
        public void TearDownTree()
        {
            m_Loader.RemoveLazyIconLoadCallback();
        }

        List<string> GetQueuedPaths()
        {
            var queue = s_queueField.GetValue(m_Loader) as System.Collections.IEnumerable;
            var paths = new List<string>();
            foreach (var req in queue)
                paths.Add((string)s_assetPathField.GetValue(req));
            return paths;
        }

        static AssetReferencePopup.AssetRefTreeViewItem MakeItem(string assetPath)
        {
            return new AssetReferencePopup.AssetRefTreeViewItem(
                assetPath.GetHashCode(), 0, assetPath, assetPath);
        }

        // Icons are queued from RowGUI, which only runs for rows that are on screen. Building the rows must not queue
        // anything: the picker lists every drawable entry in the project, so queueing here scaled with project size.
        [Test]
        public void BuildRowsAdapter_DoesNotQueueIcons()
        {
            var path1 = ConfigFolder + "/test/AssetAlpha.prefab";
            var path2 = ConfigFolder + "/test/AssetBeta.prefab";
            var guid1 = CreateAsset(path1);
            var guid2 = CreateAsset(path2);
            Settings.CreateOrMoveEntry(guid1, Settings.DefaultGroup).address = "AlphaAddress";
            Settings.CreateOrMoveEntry(guid2, Settings.DefaultGroup).address = "BetaAddress";

            m_Tree.Reload();

            Assert.IsEmpty(GetQueuedPaths(),
                "Building rows must not queue icons; RowGUI queues them for visible rows only");

            Settings.RemoveAssetEntry(guid1);
            Settings.RemoveAssetEntry(guid2);
            TearDownTestDir();
        }

        [Test]
        public void BuildRowsAdapter_WithSearchString_ReturnsOnlyMatchingRows()
        {
            var path1 = ConfigFolder + "/test/AssetAlpha.prefab";
            var path2 = ConfigFolder + "/test/AssetBeta.prefab";
            var guid1 = CreateAsset(path1);
            var guid2 = CreateAsset(path2);
            Settings.CreateOrMoveEntry(guid1, Settings.DefaultGroup).address = "AlphaAddress";
            Settings.CreateOrMoveEntry(guid2, Settings.DefaultGroup).address = "BetaAddress";

            m_Tree.searchString = "Alpha";
            m_Tree.Reload();

            var displayNames = new List<string>();
            foreach (var row in m_Tree.GetRows())
                displayNames.Add(row.displayName);

            Assert.IsTrue(displayNames.Contains("AlphaAddress"), "Matching row must be present");
            Assert.IsFalse(displayNames.Contains("BetaAddress"), "Non-matching row must be filtered out");

            Settings.RemoveAssetEntry(guid1);
            Settings.RemoveAssetEntry(guid2);
            TearDownTestDir();
        }

        [Test]
        public void ShouldRequestIcon_AssetRow_IsTrue()
        {
            var item = MakeItem("Assets/Some/Asset.prefab");

            Assert.IsTrue(AssetReferencePopup.AssetReferenceTreeView.ShouldRequestIcon(item),
                "A row with an asset path and no icon must request one");
        }

        [Test]
        public void ShouldRequestIcon_ForceAddressableItem_IsFalse()
        {
            var item = MakeItem(AssetReferenceDrawer.forceAddressableString);

            Assert.IsFalse(AssetReferencePopup.AssetReferenceTreeView.ShouldRequestIcon(item),
                "The 'Make Addressable' row carries a sentinel, not an asset path");
        }

        [Test]
        public void ShouldRequestIcon_NoneItem_IsFalse()
        {
            var item = MakeItem(string.Empty);

            Assert.IsFalse(AssetReferencePopup.AssetReferenceTreeView.ShouldRequestIcon(item),
                "The 'None' row has no asset to load an icon for");
        }

        [Test]
        public void ShouldRequestIcon_AlreadyRequested_IsFalse()
        {
            var item = MakeItem("Assets/Some/Asset.prefab");
            item.IconRequested = true;

            Assert.IsFalse(AssetReferencePopup.AssetReferenceTreeView.ShouldRequestIcon(item),
                "A row must not be queued again on every repaint");
        }

        [Test]
        public void ShouldRequestIcon_IconAlreadyResolved_IsFalse()
        {
            var item = MakeItem("Assets/Some/Asset.prefab");
            item.icon = Texture2D.whiteTexture;

            Assert.IsFalse(AssetReferencePopup.AssetReferenceTreeView.ShouldRequestIcon(item),
                "A row that already has its icon must not be queued");
        }
    }
}
