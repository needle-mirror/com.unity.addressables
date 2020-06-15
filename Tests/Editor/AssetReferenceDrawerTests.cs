using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEditor.AddressableAssets.GUI;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.IMGUI.Controls;
using UnityEditor.VersionControl;
using UnityEngine.AddressableAssets;

namespace UnityEditor.AddressableAssets.Tests
{
    public class AssetReferenceDrawerTests : AddressableAssetTestBase
    {
        AssetReferenceDrawer m_AssetReferenceDrawer;

        class TestARDObjectBlank : TestObject
        {
            [SerializeField]
            private AssetReference Reference = new AssetReference();
        }
        class TestARDObject : TestObject
        {
            [SerializeField]
            [AssetReferenceUILabelRestriction(new[] { "HD" })]
            private AssetReference Reference = new AssetReference();
        }

        class TestARDObjectMultipleLabels : TestObject
        {
            [SerializeField]
            [AssetReferenceUILabelRestriction(new[] { "HDR", "test", "default" })]
            private AssetReference ReferenceMultiple = new AssetReference();
        }


        class TestAssetReferenceDrawer : AssetReferenceDrawer
        {
            TestAssetReferencePopup _popup;
            internal void SetAssetReference(AssetReference ar)
            {
                m_AssetRefObject = ar;
            }

            internal void TreeSetup(TreeViewState treeState)
            {
                _popup = new TestAssetReferencePopup(this, "testpopup", "");
                _popup.TreeSetup(treeState, this, _popup);
            }

            internal void TreeSelectionChangedHelper(IList<int> selectedIds)
            {
                _popup.TreeSelectionChangedHelper(selectedIds);
            }

            class TestAssetReferencePopup : AssetReferencePopup
            {
                TestSelectionTree _testTree;
                internal TestAssetReferencePopup(AssetReferenceDrawer drawer, string guid, string nonAddressedAsset)
                    : base(drawer, guid, nonAddressedAsset) {}

                internal void TreeSetup(TreeViewState treeState, AssetReferenceDrawer testARD, AssetReferencePopup popup)
                {
                    _testTree = new TestSelectionTree(treeState, testARD, popup, "testtree", "");
                    _testTree.Reload();
                }

                internal void TreeSelectionChangedHelper(IList<int> selectedIds)
                {
                    _testTree.SelectionChangedHelper(selectedIds);
                }

                class TestSelectionTree : AssetReferencePopup.AssetReferenceTreeView
                {
                    internal TestSelectionTree(TreeViewState state, AssetReferenceDrawer drawer,
                                               AssetReferencePopup popup, string guid, string nonAddressedAsset)
                        : base(state, drawer, popup, guid, nonAddressedAsset) {}

                    internal void SelectionChangedHelper(IList<int> selectedIds)
                    {
                        SelectionChanged(selectedIds);
                    }
                }
            }
        }

        public SerializedProperty SetupForSetObjectTests()
        {
            // Setup Original AssetReference to not be null
            m_AssetReferenceDrawer = new AssetReferenceDrawer();
            AssetReference ar = new AssetReference();
            var assetPath = AssetDatabase.GUIDToAssetPath(m_AssetGUID);
            ar.SetEditorAsset(AssetDatabase.LoadMainAssetAtPath(assetPath));
            m_AssetReferenceDrawer.m_AssetRefObject = ar;
            m_AssetReferenceDrawer.m_AssetRefObject.SubObjectName = "test";

            // Setup property
            TestARDObjectBlank obj = ScriptableObject.CreateInstance<TestARDObjectBlank>();
            var so = new SerializedObject(obj);
            var property = so.FindProperty("Reference");
            m_AssetReferenceDrawer.GatherFilters(property);

            return property;
        }

        [Test]
        public void CanRestrictLabel()
        {
            m_AssetReferenceDrawer = new AssetReferenceDrawer();
            TestARDObject obj = ScriptableObject.CreateInstance<TestARDObject>();
            var so = new SerializedObject(obj);
            var property = so.FindProperty("Reference");
            m_AssetReferenceDrawer.GatherFilters(property);
            Assert.AreEqual(m_AssetReferenceDrawer.Restrictions.Count, 1);
            List<AssetReferenceUIRestrictionSurrogate> restrictions = m_AssetReferenceDrawer.Restrictions;
            Assert.True(restrictions.First().ToString().Contains("HD"));
        }

        [Test]
        public void CanRestrictMultipleLabels()
        {
            m_AssetReferenceDrawer = new AssetReferenceDrawer();
            TestARDObjectMultipleLabels obj = ScriptableObject.CreateInstance<TestARDObjectMultipleLabels>();
            var so = new SerializedObject(obj);
            var property = so.FindProperty("ReferenceMultiple");
            m_AssetReferenceDrawer.GatherFilters(property);
            List<AssetReferenceUIRestrictionSurrogate> restrictions = m_AssetReferenceDrawer.Restrictions;
            string restriction = restrictions.First().ToString();
            Assert.True(restriction.Contains("HDR"));
            Assert.True(restriction.Contains("test"));
            Assert.True(restriction.Contains("default"));
        }

        [Test]
        public void AssetReferenceDrawer_SelectionChanged_CanSelectSameNameAssetsInDifferentGroups()
        {
            // Drawer Setup
            var testARD = new TestAssetReferenceDrawer();
            testARD.SetAssetReference(new AssetReference());
            TestARDObjectBlank obj = ScriptableObject.CreateInstance<TestARDObjectBlank>();
            var so = new SerializedObject(obj);
            var property = so.FindProperty("Reference");
            testARD.GatherFilters(property);

            // Entries setup
            var newEntryPath = ConfigFolder + "/test" + "/test.prefab";
            var testEntry = Settings.CreateOrMoveEntry(m_AssetGUID, Settings.groups[0]);
            GameObject testObject = new GameObject("TestObject");
            Directory.CreateDirectory(ConfigFolder + "/test");
            PrefabUtility.SaveAsPrefabAsset(testObject, newEntryPath);
            var newEntryGuid = AssetDatabase.AssetPathToGUID(newEntryPath);
            var secondTestEntry = Settings.CreateOrMoveEntry(newEntryGuid, Settings.groups[1]);

            // Tree setup
            var testId = testEntry.AssetPath.GetHashCode();
            List<int> selectedIds = new List<int>() { testId };
            var treeState = new TreeViewState();
            treeState.selectedIDs = selectedIds;
            Directory.CreateDirectory("Assets/AddressableAssetsData");
            AddressableAssetSettingsDefaultObject.Settings = Settings;
            testARD.TreeSetup(treeState);

            // Test
            testARD.TreeSelectionChangedHelper(selectedIds);
            Assert.AreEqual(m_AssetGUID, testARD.newGuid);
            selectedIds[0] = secondTestEntry.AssetPath.GetHashCode();
            testARD.TreeSelectionChangedHelper(selectedIds);
            Assert.AreEqual(AssetDatabase.AssetPathToGUID(newEntryPath), testARD.newGuid);

            // Cleanup
            if (Directory.Exists("Assets/AddressableAssetsData"))
                AssetDatabase.DeleteAsset("Assets/AddressableAssetsData");
            EditorBuildSettings.RemoveConfigObject("Assets/AddressableAssetsData");
            Settings.RemoveAssetEntry(AssetDatabase.AssetPathToGUID(newEntryPath));
            Settings.RemoveAssetEntry(m_AssetGUID);
        }

        [Test]
        public void AssetReferenceDrawer_SetObject_CanSetObject()
        {
            // Setup AssetReference
            m_AssetReferenceDrawer = new AssetReferenceDrawer();
            AssetReference ar = new AssetReference();
            var assetPath = AssetDatabase.GUIDToAssetPath(m_AssetGUID);
            m_AssetReferenceDrawer.m_AssetRefObject = ar;
            m_AssetReferenceDrawer.m_AssetRefObject.SubObjectName = "test";
            var testObject = AssetDatabase.LoadMainAssetAtPath(assetPath);

            // Setup property
            TestARDObjectBlank obj = ScriptableObject.CreateInstance<TestARDObjectBlank>();
            var so = new SerializedObject(obj);
            var property = so.FindProperty("Reference");
            m_AssetReferenceDrawer.GatherFilters(property);

            // Test
            string guid;
            Assert.IsTrue(m_AssetReferenceDrawer.SetObject(property, testObject, out guid));
            Assert.AreEqual(m_AssetGUID, m_AssetReferenceDrawer.m_AssetRefObject.AssetGUID);
            Assert.AreEqual(m_AssetGUID, guid);
            Assert.AreEqual(testObject.name, m_AssetReferenceDrawer.m_AssetRefObject.SubObjectName);
        }

        [Test]
        public void AssetReferenceDrawer_SetObject_CanSetToNull()
        {
            // Setup Original AssetReference to not be null and property
            var property = SetupForSetObjectTests();

            // Test
            string guid;
            m_AssetReferenceDrawer.SetObject(property, null, out guid);
            Assert.AreEqual(null, m_AssetReferenceDrawer.m_AssetRefObject.SubObjectName);
            Assert.AreEqual(string.Empty, m_AssetReferenceDrawer.m_AssetRefObject.AssetGUID);
        }

#if UNITY_2019_2_OR_NEWER
        [Test]
        public void AssetReferenceDrawer_SetObject_SetToNullDirtiesObject()
        {
            // Setup Original AssetReference to not be null and property
            var property = SetupForSetObjectTests();

            // Test
            string guid;
            EditorUtility.ClearDirty(property.serializedObject.targetObject);
            var prevDirty = EditorUtility.IsDirty(property.serializedObject.targetObject);
            m_AssetReferenceDrawer.SetObject(property, null, out guid);
            Assert.IsFalse(prevDirty);
            Assert.IsTrue(EditorUtility.IsDirty(property.serializedObject.targetObject));
        }

#endif
    }
}
