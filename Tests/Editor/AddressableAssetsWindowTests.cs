using NUnit.Framework;
using System.Collections.Generic;
using System.IO;
using UnityEditor.AddressableAssets.GUI;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEngine.UIElements;
using TreeView = UnityEngine.UIElements.TreeView;

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
            treeView.SimplifyAddressesImpl(new List<AssetEntryTreeViewItem>() {new AssetEntryTreeViewItem(entry, 1)});

            Assert.AreEqual(Path.GetFileNameWithoutExtension(assetPath), entry.address);
        }

        [Test]
        public void AddressableAssetWindow_RemovedEntries_AreNoLongerPresent()
        {
            var entry = Settings.CreateOrMoveEntry(m_AssetGUID, Settings.DefaultGroup);

            AddressableAssetEntryTreeView treeView = new AddressableAssetEntryTreeView(Settings);
            treeView.RemoveEntryImpl(new List<AssetEntryTreeViewItem>() {new AssetEntryTreeViewItem(entry, 1)}, true);

            Assert.IsNull(Settings.FindAssetEntry(m_AssetGUID));
        }

        [Test]
        public void AddressableAssetWindow_RemoveGroup_GroupGetsRemovedCorrectly()
        {
            var group = Settings.CreateGroup("RemoveMeGroup", false, false, true, new List<AddressableAssetGroupSchema>());
            AddressableAssetEntryTreeView treeView = new AddressableAssetEntryTreeView(Settings);
            treeView.RemoveGroupImpl(new List<AssetEntryTreeViewItem>() {new AssetEntryTreeViewItem(group, 1)}, true);
            Assert.IsNull(Settings.FindGroup("RemoveMeGroup"));
        }

        [Test]
        public void AddressableAssetWindow_RemoveMissingReferences_RemovesAllNullReferences()
        {
            Settings.groups.Add(null);
            Settings.groups.Add(null);

            AddressableAssetEntryTreeView treeView = new AddressableAssetEntryTreeView(Settings);
            treeView.RemoveMissingReferencesImpl();
            foreach (var group in Settings.groups)
                Assert.IsNotNull(group);
        }

        [Test]
        public void AddressableAssetWindow_SetDefaultGroup_SetsTheSpecifiedGroupToDefault()
        {
            var savedDefaultGroup = Settings.DefaultGroup;
            var newDefaultGroup = Settings.CreateGroup("NewDefaultGroup", false, false, true, new List<AddressableAssetGroupSchema>());
            AddressableAssetEntryTreeView treeView = new AddressableAssetEntryTreeView(Settings);

            treeView.SetGroupAsDefault(new List<AssetEntryTreeViewItem>() {new AssetEntryTreeViewItem(newDefaultGroup, 1)});

            Assert.AreEqual(newDefaultGroup, Settings.DefaultGroup);

            Settings.DefaultGroup = savedDefaultGroup;
            Settings.RemoveGroup(newDefaultGroup);
        }

        private AddressableAssetEntryTreeView InitGroupEditorWithState(AddressableAssetsWindow aaWindow, AddressableAssetEntryTreeViewState treeState, MultiColumnHeaderState mchs)
        {
            aaWindow.m_GroupEditor = new AddressableAssetsSettingsGroupEditor(aaWindow);
            aaWindow.m_GroupEditor.OnDisable();
            aaWindow.m_GroupEditor.settings = Settings;
            aaWindow.m_GroupEditor.m_TreeState = treeState;
            aaWindow.m_GroupEditor.m_Mchs = mchs;
            aaWindow.m_GroupEditor.InitialiseEntryTree();
            return aaWindow.m_GroupEditor.m_EntryTree;
        }

        [Test]
        public void AddressableAssetWindow_GroupWindow_ColumnWidthsAreSetWhenValid()
        {
            var mchs = AddressableAssetEntryTreeView.CreateDefaultMultiColumnHeaderState();
            AddressableAssetEntryTreeViewState treeState = new AddressableAssetEntryTreeViewState();
            treeState.columnWidths = new float[mchs.columns.Length];
            for (var i = 0; i < treeState.columnWidths.Length; i++)
            {
                treeState.columnWidths[i] = 9999.0f;
            }

            AddressableAssetsWindow aaWindow = ScriptableObject.CreateInstance<AddressableAssetsWindow>();
            var treeView = InitGroupEditorWithState(aaWindow, treeState, mchs);
            foreach (var col in treeView.multiColumnHeader.state.columns)
            {
                Assert.AreEqual(9999.0f, col.width);

            }
        }


        [Test]
        public void AddressableAssetWindow_GroupWindow_ColumnWidthsAreDefaultWhenInvalid()
        {
            // in this case we'll only set one header column so the widths don't match and nothing is done
            var defaultMchs = AddressableAssetEntryTreeView.CreateDefaultMultiColumnHeaderState();
            var mchs = AddressableAssetEntryTreeView.CreateDefaultMultiColumnHeaderState();
            AddressableAssetEntryTreeViewState treeState = new AddressableAssetEntryTreeViewState();
            treeState.columnWidths = new float[]{9999.0f};

            AddressableAssetsWindow aaWindow = ScriptableObject.CreateInstance<AddressableAssetsWindow>();
            var treeView = InitGroupEditorWithState(aaWindow, treeState, mchs);
            Assert.AreEqual(defaultMchs.columns.Length, treeView.multiColumnHeader.state.columns.Length);
            for (var i = 0; i < defaultMchs.columns.Length; i++)
            {
                Assert.AreNotEqual(9999.0f, defaultMchs.columns[i].width);
                Assert.AreEqual(treeView.multiColumnHeader.state.columns[i].width, defaultMchs.columns[i].width);
            }
        }

        [Test]
        public void AddressableAssetWindow_GroupWindow_AddGroupUpdatesSortSettings()
        {
            var defaultMchs = AddressableAssetEntryTreeView.CreateDefaultMultiColumnHeaderState();
            var mchs = AddressableAssetEntryTreeView.CreateDefaultMultiColumnHeaderState();
            AddressableAssetEntryTreeViewState treeState = new AddressableAssetEntryTreeViewState();

            AddressableAssetsWindow aaWindow = ScriptableObject.CreateInstance<AddressableAssetsWindow>();
            var treeView = InitGroupEditorWithState(aaWindow, treeState, mchs);

            AddressableAssetGroup group1 = null, group2 = null;
            try
            {
                Settings.OnModification += aaWindow.m_GroupEditor.OnSettingsModification;

                var defaultGroup = Settings.DefaultGroup;
                group1 = Settings.CreateGroup("Group 1", false, false, true, new List<AddressableAssetGroupSchema>());
                group2 = Settings.CreateGroup("Group 2", false, false, true, new List<AddressableAssetGroupSchema>());
                Assert.AreEqual(3, Settings.groups.Count);
                Assert.AreEqual(3, treeState.sortOrderList.Count);
            }
            finally
            {
                if (group1 != null)
                {
                    treeView.RemoveGroupImpl(new List<AssetEntryTreeViewItem>() { new AssetEntryTreeViewItem(group1, 1) }, true);
                }
                if (group2 != null)
                {
                    treeView.RemoveGroupImpl(new List<AssetEntryTreeViewItem>() { new AssetEntryTreeViewItem(group2, 1) }, true);
                }
                Settings.OnModification -= aaWindow.m_GroupEditor.OnSettingsModification;
            }
        }

//I think there's a bug in AssetDatabase in 2023.1 that's preventing the test from passing when creating a new Settings Scriptable Object.
#if UNITY_2023_3_OR_NEWER
        [Test]
        public void AddressableAssetWindow_GroupWindow_DeleteGroupUpdatesSortSettings()
        {
            var defaultMchs = AddressableAssetEntryTreeView.CreateDefaultMultiColumnHeaderState();
            var mchs = AddressableAssetEntryTreeView.CreateDefaultMultiColumnHeaderState();
            AddressableAssetEntryTreeViewState treeState = new AddressableAssetEntryTreeViewState();

            AddressableAssetsWindow aaWindow = ScriptableObject.CreateInstance<AddressableAssetsWindow>();
            var treeView = InitGroupEditorWithState(aaWindow, treeState, mchs);

            AddressableAssetGroup group1 = null, group2 = null;
            try
            {
                Settings.OnModification += aaWindow.m_GroupEditor.OnSettingsModification;

                var defaultGroup = Settings.DefaultGroup;
                group1 = Settings.CreateGroup("Group 1", false, false, true, new List<AddressableAssetGroupSchema>());
                Assert.AreEqual(2, Settings.groups.Count);
                Assert.AreEqual(2, treeState.sortOrderList.Count);
                Settings.RemoveGroup(group1);
                Assert.AreEqual(1, Settings.groups.Count);
                Assert.AreEqual(1, treeState.sortOrderList.Count);
            }
            finally
            {
                if (group1 != null)
                {
                    treeView.RemoveGroupImpl(new List<AssetEntryTreeViewItem>() { new AssetEntryTreeViewItem(group1, 1) }, true);
                }
                Settings.OnModification -= aaWindow.m_GroupEditor.OnSettingsModification;
            }
        }
#endif

        [Test]
        public void AddressableAssetWindow_CanSelectGroupTreeViewByAddressableAssetEntries()
        {
            //Setup
            var defaultGroup = Settings.DefaultGroup;
            Assert.IsNotNull(defaultGroup, "Default Group is not found");
            ProjectConfigData.ShowSubObjectsInGroupView = true;

            string path0 = GetAssetPath("test.prefab");
            string p0 = AssetDatabase.AssetPathToGUID(path0);
            Assert.IsFalse(string.IsNullOrEmpty(p0), "Could not setup for Asset \"test.prefab\"");
            Texture t = new Texture2D(4, 4);
            t.name = "tex";
            AssetDatabase.AddObjectToAsset(t, path0);
            AssetDatabase.SaveAssets();
            string p1 = AssetDatabase.AssetPathToGUID(GetAssetPath("test 1.prefab"));
            Assert.IsFalse(string.IsNullOrEmpty(p1), "Could not setup for Asset \"test 1.prefab\"");
            string p2 = AssetDatabase.AssetPathToGUID(GetAssetPath("test 2.prefab"));
            Assert.IsFalse(string.IsNullOrEmpty(p2), "Could not setup for Asset \"test 2.prefab\"");

            var e0 = Settings.CreateOrMoveEntry(p0, defaultGroup);
            List<AddressableAssetEntry> gathered = new List<AddressableAssetEntry>();
            e0.GatherAllAssets(gathered, false, true, true);
            Assert.AreEqual(1, gathered.Count, "Incorrect subObject count for Asset at " + path0);

            var e1 = Settings.CreateOrMoveEntry(p1, defaultGroup);
            var e2 = Settings.CreateOrMoveEntry(p2, defaultGroup);

            AddressableAssetsWindow aaWindow = ScriptableObject.CreateInstance<AddressableAssetsWindow>();
            aaWindow.m_GroupEditor = new AddressableAssetsSettingsGroupEditor(aaWindow);
            aaWindow.m_GroupEditor.OnDisable();
            aaWindow.m_GroupEditor.settings = Settings;
            var entryTree = aaWindow.m_GroupEditor.InitialiseEntryTree();

            //Test
            Assert.AreEqual(0, entryTree.GetSelection().Count, "entryTree is not expected to have anything select at creation");
            aaWindow.SelectAssetsInGroupEditor(new List<AddressableAssetEntry>() {e1});
            Assert.AreEqual(1, entryTree.GetSelection().Count, "Expecting to have \"test 1.prefab\" selected.");
            aaWindow.SelectAssetsInGroupEditor(new List<AddressableAssetEntry>() {e2});
            Assert.AreEqual(1, entryTree.GetSelection().Count, "Expecting to have \"test 2.prefab\" selected.");
            aaWindow.SelectAssetsInGroupEditor(new List<AddressableAssetEntry>() {e1, e2});
            Assert.AreEqual(2, entryTree.GetSelection().Count, "Expecting to have \"test 1.prefab\" and \"test 2.prefab\" selected.");

            Assert.IsTrue(ProjectConfigData.ShowSubObjectsInGroupView, "Need to display subObjects to test that they are being shown");
            aaWindow.SelectAssetsInGroupEditor(new List<AddressableAssetEntry>() {gathered[0]});
            Assert.AreEqual(1, entryTree.GetSelection().Count, "Expecting to have \"test.prefab[SubObject]\" selected.");

            //Cleanup
            Assert.IsTrue(Settings.RemoveAssetEntry(e1, false), "Failed to cleanup AssetEntry \"test 1.prefab\" from test settings.");
            Assert.IsTrue(Settings.RemoveAssetEntry(e2, false), "Failed to cleanup AssetEntry \"test 2.prefab\" from test settings.");
            Object.DestroyImmediate(aaWindow);
        }
    }
}
