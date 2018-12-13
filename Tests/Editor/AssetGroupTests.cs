using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace UnityEditor.AddressableAssets.Tests
{
    public class AssetGroupTests : AddressableAssetTestBase
    {
        [Test]
        public void AddRemoveEntry()
        {
            var group = m_Settings.FindGroup(AddressableAssetSettings.DefaultLocalGroupName);
            Assert.IsNotNull(group);
            var entry = new AddressableAssetEntry(m_AssetGUID, "test", group, false);
            group.AddAssetEntry(entry);
            Assert.IsNotNull(group.GetAssetEntry(m_AssetGUID));
            group.RemoveAssetEntry(entry);
            Assert.IsNull(group.GetAssetEntry(m_AssetGUID));
        }

        [Test]
        public void RenameSlashesBecomeDashes()
        {
            var group = m_Settings.FindGroup(AddressableAssetSettings.DefaultLocalGroupName);
            var oldName = group.Name;
            group.Name = "folder/name";
            Assert.AreEqual("folder-name", group.Name);
            group.Name = oldName;
        }
        [Test]
        public void RenameInvalidCharactersFails()
        {
            var group = m_Settings.FindGroup(AddressableAssetSettings.DefaultLocalGroupName);
            var oldName = group.Name;
            string badName = "*#?@>!@*@(#";
            LogAssert.Expect(LogType.Error, "Rename of Group failed. Invalid file name: '" + badName + ".asset'");
            group.Name = badName;
            Assert.AreEqual(oldName, group.Name);
        }
    }
}