using NUnit.Framework;
using System.Collections.Generic;
using System.IO;
using UnityEditor.AddressableAssets.GUI;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace UnityEditor.AddressableAssets.Tests
{
    public class AddressableAssetWindowTests : AddressableAssetTestBase
    {
        [Test]
        public void AddressableAssetWindow_OfferToConvert_CantConvertWithNoBundles()
        {
            AddressableAssetsWindow aaWindow = ScriptableObject.CreateInstance<AddressableAssetsWindow>();
            var prevGroupCount = Settings.groups.Count;
            aaWindow.OfferToConvert(Settings);
            Assert.AreEqual(prevGroupCount, Settings.groups.Count);
            Object.DestroyImmediate(aaWindow);
        }

        [Test]
        public void AddressableAssetWindow_SimplifyAddress_ReturnsFileNameOnly()
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(m_AssetGUID);
            var entry = Settings.CreateOrMoveEntry(m_AssetGUID, Settings.DefaultGroup);
            Assert.AreEqual(assetPath, entry.address);

            AddressableAssetEntryTreeView treeView = new AddressableAssetEntryTreeView(Settings);
            treeView.SimplifyAddressesImpl(new List<AssetEntryTreeViewItem>() { new AssetEntryTreeViewItem(entry, 1) });

            Assert.AreEqual(Path.GetFileNameWithoutExtension(assetPath), entry.address);
        }

        [Test]
        public void AddressableAssetWindow_RemovedEntries_AreNoLongerPresent()
        {
            var entry = Settings.CreateOrMoveEntry(m_AssetGUID, Settings.DefaultGroup);

            AddressableAssetEntryTreeView treeView = new AddressableAssetEntryTreeView(Settings);
            treeView.RemoveEntryImpl(new List<AssetEntryTreeViewItem>() { new AssetEntryTreeViewItem(entry, 1) }, true);

            Assert.IsNull(Settings.FindAssetEntry(m_AssetGUID));
        }

        [Test]
        public void AddressableAssetWindow_RemoveGroup_GroupGetsRemovedCorrectly()
        {
            var group = Settings.CreateGroup("RemoveMeGroup", false, false, true, new List<AddressableAssetGroupSchema>());
            AddressableAssetEntryTreeView treeView = new AddressableAssetEntryTreeView(Settings);
            treeView.RemoveGroupImpl(new List<AssetEntryTreeViewItem>() { new AssetEntryTreeViewItem(group, 1) }, true);
            Assert.IsNull(Settings.FindGroup("RemoveMeGroup"));
        }

        [Test]
        public void AddressableAssetWindow_RemoveMissingReferences_RemovesAllNullReferences()
        {
            Settings.groups.Add(null);
            Settings.groups.Add(null);
            
            AddressableAssetEntryTreeView treeView = new AddressableAssetEntryTreeView(Settings);
            treeView.RemoveMissingReferencesImpl();
            foreach(var group in Settings.groups)
                Assert.IsNotNull(group);
        }

        [Test]
        public void AddressableAssetWindow_SetDefaultGroup_SetsTheSpecifiedGroupToDefault()
        {
            var savedDefaultGroup = Settings.DefaultGroup;
            var newDefaultGroup = Settings.CreateGroup("NewDefaultGroup", false, false, true, new List<AddressableAssetGroupSchema>());
            AddressableAssetEntryTreeView treeView = new AddressableAssetEntryTreeView(Settings);
            
            treeView.SetGroupAsDefault(new List<AssetEntryTreeViewItem>() { new AssetEntryTreeViewItem(newDefaultGroup, 1) });
            
            Assert.AreEqual(newDefaultGroup, Settings.DefaultGroup);

            Settings.DefaultGroup = savedDefaultGroup;
            Settings.RemoveGroup(newDefaultGroup);
        }

        [Test]
        public void AddressableAssetWindow_CanSelectGroupTreeViewByAddressableAssetEntries()
        {
            //Setup
            var defaultGroup = Settings.DefaultGroup;
            Assert.IsNotNull(defaultGroup, "Default Group is not found");
            string p1 = AssetDatabase.AssetPathToGUID(GetAssetPath("test 1.prefab"));
            Assert.IsFalse(string.IsNullOrEmpty(p1), "Could not setup for Asset \"test 1.prefab\"");
            string p2 = AssetDatabase.AssetPathToGUID(GetAssetPath("test 2.prefab"));
            Assert.IsFalse(string.IsNullOrEmpty(p2), "Could not setup for Asset \"test 2.prefab\"");
            var e1 = Settings.CreateOrMoveEntry(p1, defaultGroup);
            var e2 = Settings.CreateOrMoveEntry(p2, defaultGroup);

            AddressableAssetsWindow aaWindow = ScriptableObject.CreateInstance<AddressableAssetsWindow>();
            aaWindow.m_GroupEditor = new AddressableAssetsSettingsGroupEditor(aaWindow);
            aaWindow.m_GroupEditor.OnDisable();
            aaWindow.m_GroupEditor.settings = Settings;
            var entryTree = aaWindow.m_GroupEditor.InitialiseEntryTree();
            
            //Test
            Assert.AreEqual(0, entryTree.GetSelection().Count, "entryTree is not expected to have anything select at creation");
            aaWindow.SelectAssetsInGroupEditor(new List<AddressableAssetEntry>(){e1});
            Assert.AreEqual(1, entryTree.GetSelection().Count, "Expecting to have \"test 1.prefab\" selected.");
            aaWindow.SelectAssetsInGroupEditor(new List<AddressableAssetEntry>(){e2});
            Assert.AreEqual(1, entryTree.GetSelection().Count, "Expecting to have \"test 2.prefab\" selected.");
            aaWindow.SelectAssetsInGroupEditor(new List<AddressableAssetEntry>(){e1, e2});
            Assert.AreEqual(2, entryTree.GetSelection().Count, "Expecting to have \"test 1.prefab\" and \"test 2.prefab\" selected.");

            //Cleanup
            Assert.IsTrue(Settings.RemoveAssetEntry(e1, false), "Failed to cleanup AssetEntry \"test 1.prefab\" from test settings.");
            Assert.IsTrue(Settings.RemoveAssetEntry(e2, false), "Failed to cleanup AssetEntry \"test 2.prefab\" from test settings.");
            Object.DestroyImmediate(aaWindow);
        }
    }
}
