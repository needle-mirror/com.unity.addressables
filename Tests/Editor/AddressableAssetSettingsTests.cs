using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace UnityEditor.AddressableAssets.Tests
{
    public class AddressableAssetSettingsTests : AddressableAssetTestBase
    {
        [Test]
        public void HasDefaultInitialGroups()
        {
            Assert.IsNotNull(m_Settings.FindGroup(AddressableAssetSettings.PlayerDataGroupName));
            Assert.IsNotNull(m_Settings.FindGroup(AddressableAssetSettings.DefaultLocalGroupName));
        }

        [Test]
        public void AddRemovelabel()
        {
            const string labelName = "Newlabel";
            m_Settings.AddLabel(labelName);
            Assert.Contains(labelName, m_Settings.labelTable.labelNames);
            m_Settings.RemoveLabel(labelName);
            Assert.False(m_Settings.labelTable.labelNames.Contains(labelName));
        }

        [Test]
        public void AddRemoveGroup()
        {
            const string groupName = "NewGroup";
            var group = m_Settings.CreateGroup(groupName, false, false, false, null);
            Assert.IsNotNull(group);
            m_Settings.RemoveGroup(group);
            Assert.IsNull(m_Settings.FindGroup(groupName));
        }

        [Test]
        public void CreateNewEntry()
        {
            var group = m_Settings.CreateGroup("NewGroupForCreateOrMoveEntryTest", false, false, false, null);
            Assert.IsNotNull(group);
            var entry = m_Settings.CreateOrMoveEntry(m_AssetGUID, group);
            Assert.IsNotNull(entry);
            Assert.AreSame(group, entry.parentGroup);
            var localDataGroup = m_Settings.FindGroup(AddressableAssetSettings.DefaultLocalGroupName);
            Assert.IsNotNull(localDataGroup);
            entry = m_Settings.CreateOrMoveEntry(m_AssetGUID, localDataGroup);
            Assert.IsNotNull(entry);
            Assert.AreNotSame(group, entry.parentGroup);
            Assert.AreSame(localDataGroup, entry.parentGroup);
            m_Settings.RemoveGroup(group);
            localDataGroup.RemoveAssetEntry(entry);
        }

        [Test]
        public void FindAssetEntry()
        {
            var localDataGroup = m_Settings.FindGroup(AddressableAssetSettings.DefaultLocalGroupName);
            Assert.IsNotNull(localDataGroup);
            var entry = m_Settings.CreateOrMoveEntry(m_AssetGUID, localDataGroup);
            var foundEntry = m_Settings.FindAssetEntry(m_AssetGUID);
            Assert.AreSame(entry, foundEntry);
        }

        [Test]
        public void AddressablesClearCachedData_DoesNotThrowError()
        {
            //individual clean paths
            foreach (ScriptableObject so in m_Settings.DataBuilders)
            {
                BuildScriptBase db = so as BuildScriptBase;
                Assert.DoesNotThrow(() => m_Settings.CleanPlayerContentImpl(db));
            }

            //Clean all path
            Assert.DoesNotThrow(() => m_Settings.CleanPlayerContentImpl());

            //Cleanup
            m_Settings.BuildPlayerContentImpl();
        }

        [Test]
        public void AddressablesCleanCachedData_ClearsData()
        {
            //Setup
            m_Settings.BuildPlayerContentImpl();

            //Check after each clean that the data is not built
            foreach (ScriptableObject so in m_Settings.DataBuilders)
            {
                BuildScriptBase db = so as BuildScriptBase;
                m_Settings.CleanPlayerContentImpl(db);
                Assert.IsFalse(db.IsDataBuilt());
            }
        }

        [Test]
        public void AddressablesCleanAllCachedData_ClearsAllData()
        {
            //Setup
            m_Settings.BuildPlayerContentImpl();

            //Clean ALL data builders
            m_Settings.CleanPlayerContentImpl();

            //Check none have data built
            foreach (ScriptableObject so in m_Settings.DataBuilders)
            {
                BuildScriptBase db = so as BuildScriptBase;
                Assert.IsFalse(db.IsDataBuilt());
            }
        }
    }
}