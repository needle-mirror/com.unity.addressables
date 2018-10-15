using NUnit.Framework;

namespace UnityEditor.AddressableAssets.Tests
{
    public class AssetGroupTests : AddressableAssetTestBase
    {
        [Test]
        public void AddRemoveEntry()
        {
            var group = m_settings.FindGroup(AddressableAssetSettings.DefaultLocalGroupName);
            Assert.IsNotNull(group);
            var entry = new AddressableAssetEntry(assetGUID, "test", group, false);
            group.AddAssetEntry(entry);
            Assert.IsNotNull(group.GetAssetEntry(assetGUID));
            group.RemoveAssetEntry(entry);
            Assert.IsNull(group.GetAssetEntry(assetGUID));
        }

        [Test]
        public void RenameSlashesBecomeDashes()
        {
            var group = m_settings.FindGroup(AddressableAssetSettings.DefaultLocalGroupName);
            var oldName = group.Name;
            group.Name = "folder/name";
            Assert.AreEqual("folder-name", group.Name);
            group.Name = oldName;
        }
        [Test]
        public void RenameInvalidCharactersFails()
        {
            var group = m_settings.FindGroup(AddressableAssetSettings.DefaultLocalGroupName);
            var oldName = group.Name;
            string badName = "*#?@>!@*@(#";
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Error, "Rename of Group failed. Invalid file name: '" + badName + ".asset'");
            group.Name = badName;
            Assert.AreEqual(oldName, group.Name);
        }
    }
}