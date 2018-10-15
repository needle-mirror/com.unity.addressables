using NUnit.Framework;

namespace UnityEditor.AddressableAssets.Tests
{
    public class AddressableAssetSettingsTests : AddressableAssetTestBase
    {
        [Test]
        public void IsValid()
        {
            Assert.IsNotNull(m_settings);
        }

        [Test]
        public void HasDefaultInitialGroups()
        {
            Assert.IsNotNull(m_settings.FindGroup(AddressableAssetSettings.PlayerDataGroupName));
            Assert.IsNotNull(m_settings.FindGroup(AddressableAssetSettings.DefaultLocalGroupName));
        }

        [Test]
        public void AddRemovelabel()
        {
            const string labelName = "Newlabel";
            m_settings.AddLabel(labelName);
            Assert.Contains(labelName, m_settings.labelTable.labelNames);
            m_settings.RemoveLabel(labelName);
            Assert.False(m_settings.labelTable.labelNames.Contains(labelName));
        }

        [Test]
        public void AddRemoveGroup()
        {
            const string groupName = "NewGroup";
            var group = m_settings.CreateGroup(groupName, false, false, false);
            Assert.IsNotNull(group);
            m_settings.RemoveGroup(group);
            Assert.IsNull(m_settings.FindGroup(groupName));
        }

        [Test]
        public void CreateNewEntry()
        {
            var group = m_settings.CreateGroup("NewGroupForCreateOrMoveEntryTest", false, false, false);
            Assert.IsNotNull(group);
            var entry = m_settings.CreateOrMoveEntry(assetGUID, group);
            Assert.IsNotNull(entry);
            Assert.AreSame(group, entry.parentGroup);
            var localDataGroup = m_settings.FindGroup(AddressableAssetSettings.DefaultLocalGroupName);
            Assert.IsNotNull(localDataGroup);
            entry = m_settings.CreateOrMoveEntry(assetGUID, localDataGroup);
            Assert.IsNotNull(entry);
            Assert.AreNotSame(group, entry.parentGroup);
            Assert.AreSame(localDataGroup, entry.parentGroup);
            m_settings.RemoveGroup(group);
            localDataGroup.RemoveAssetEntry(entry);
        }

        [Test]
        public void FindAssetEntry()
        {
            var localDataGroup = m_settings.FindGroup(AddressableAssetSettings.DefaultLocalGroupName);
            Assert.IsNotNull(localDataGroup);
            var entry = m_settings.CreateOrMoveEntry(assetGUID, localDataGroup);
            var foundEntry = m_settings.FindAssetEntry(assetGUID);
            Assert.AreSame(entry, foundEntry);
        }

    }
}