using NUnit.Framework;
using System.Collections.Generic;
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
