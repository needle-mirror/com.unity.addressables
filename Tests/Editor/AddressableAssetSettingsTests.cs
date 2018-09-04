using NUnit.Framework;

namespace UnityEditor.AddressableAssets.Tests
{
    public class AddressableAssetSettingsTests : AddressableAssetTestBase
    {
        [Test]
        public void IsValid()
        {
            Assert.IsNotNull(settings);
        }

        [Test]
        public void HasDefaultInitialGroups()
        {
            Assert.IsNotNull(settings.FindGroup(AddressableAssetSettings.PlayerDataGroupName));
            Assert.IsNotNull(settings.FindGroup(AddressableAssetSettings.DefaultLocalGroupName));
        }

        [Test]
        public void AddRemovelabel()
        {
            const string labelName = "Newlabel";
            settings.AddLabel(labelName);
            Assert.Contains(labelName, settings.labelTable.labelNames);
            settings.RemoveLabel(labelName);
            Assert.False(settings.labelTable.labelNames.Contains(labelName));
        }

        [Test]
        public void AddRemoveGroup()
        {
            const string groupName = "NewGroup";
            var group = settings.CreateGroup(groupName, typeof(BundledAssetGroupProcessor), false, false);
            Assert.IsNotNull(group);
            settings.RemoveGroup(group);
            Assert.IsNull(settings.FindGroup(groupName));
        }

        [Test]
        public void CreateNewEntry()
        {
            var group = settings.CreateGroup("NewGroupForCreateOrMoveEntryTest", typeof(BundledAssetGroupProcessor), false, false);
            Assert.IsNotNull(group);
            var entry = settings.CreateOrMoveEntry(assetGUID, group);
            Assert.IsNotNull(entry);
            Assert.AreSame(group, entry.parentGroup);
            var localDataGroup = settings.FindGroup(AddressableAssetSettings.DefaultLocalGroupName);
            Assert.IsNotNull(localDataGroup);
            entry = settings.CreateOrMoveEntry(assetGUID, localDataGroup);
            Assert.IsNotNull(entry);
            Assert.AreNotSame(group, entry.parentGroup);
            Assert.AreSame(localDataGroup, entry.parentGroup);
            settings.RemoveGroup(group);
            localDataGroup.RemoveAssetEntry(entry);
        }

        [Test]
        public void FindAssetEntry()
        {
            var localDataGroup = settings.FindGroup(AddressableAssetSettings.DefaultLocalGroupName);
            Assert.IsNotNull(localDataGroup);
            var entry = settings.CreateOrMoveEntry(assetGUID, localDataGroup);
            var foundEntry = settings.FindAssetEntry(assetGUID);
            Assert.AreSame(entry, foundEntry);
        }

    }
}