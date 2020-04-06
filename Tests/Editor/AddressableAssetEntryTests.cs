using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.AddressableAssets.ResourceLocators;

namespace UnityEditor.AddressableAssets.Tests
{
    public class AddressableAssetEntryTests : AddressableAssetTestBase
    {
        string guid;
        protected override void OnInit()
        {
            var path = k_TestConfigFolder + "/subObjectTest.asset";
            AssetDatabase.CreateAsset(UnityEngine.AddressableAssets.Tests.TestObject.Create("test"), path);

            AssetDatabase.AddObjectToAsset(UnityEngine.AddressableAssets.Tests.TestObject2.Create("test2"), path);
            AssetDatabase.AddObjectToAsset(UnityEngine.AddressableAssets.Tests.TestObject2.Create("test3"), path);
            AssetDatabase.AddObjectToAsset(UnityEngine.AddressableAssets.Tests.TestObject2.Create("test4"), path);
            AssetDatabase.AddObjectToAsset(UnityEngine.AddressableAssets.Tests.TestObject2.Create("test5"), path);
            AssetDatabase.SaveAssets();

            guid = AssetDatabase.AssetPathToGUID(path);
            Settings.CreateOrMoveEntry(guid, Settings.DefaultGroup);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        }

        [Test]
        public void CreateCatelogEntries_WhenObjectHasMultipleSubObjectWithSameType_OnlyOneSubEntryIsCreated()
        {
            var e = Settings.DefaultGroup.GetAssetEntry(guid);
            var entries = new List<ContentCatalogDataEntry>();
            var providerTypes = new HashSet<Type>();
            e.CreateCatalogEntries(entries, false, "doesntMatter", null, null, providerTypes);
            Assert.AreEqual(2, entries.Count);
        }

        [Test]
        public void WhenClassReferencedByAddressableAssetEntryIsReloaded_CachedMainAssetTypeIsReset()
        {
            // Setup
            var path = k_TestConfigFolder + "/resetCachedMainAssetTypeTestGroup.asset";
            AddressableAssetGroup group = ScriptableObject.CreateInstance<AddressableAssetGroup>();
            AddressableAssetEntry entry = new AddressableAssetEntry(guid, "address", null, false);
            group.AddAssetEntry(entry);

            Assert.IsNull(entry.m_cachedMainAssetType);
            Assert.AreEqual(typeof(UnityEngine.AddressableAssets.Tests.TestObject), entry.MainAssetType);

            // Test
            AssetDatabase.CreateAsset(group, path);
            Resources.UnloadAsset(group);

            var reloadedGroup = AssetDatabase.LoadAssetAtPath<AddressableAssetGroup>(path);
            var reloadedEntry = reloadedGroup.GetAssetEntry(guid);
            Assert.IsNull(reloadedEntry.m_cachedMainAssetType);

            // Cleanup
            AssetDatabase.DeleteAsset(path);
        }
    }
} 