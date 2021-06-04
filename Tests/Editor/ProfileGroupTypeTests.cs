using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor.AddressableAssets.GUI;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.TestTools;

namespace UnityEditor.AddressableAssets.Tests
{
    public class ProfileGroupTypeTests
    {
        [Test]
        public void CreateEmptyProfileGroupType_Returns_InvalidProfileGroupType()
        {
            ProfileGroupType profileGroupType = new ProfileGroupType();
            Assert.False(profileGroupType.IsValidGroupType());
        }

        [Test]
        public void CreatePrefixedProfileGroupType_Returns_InvalidProfileGroupType()
        {
            ProfileGroupType profileGroupType = new ProfileGroupType("prefix");
            Assert.False(profileGroupType.IsValidGroupType());
        }

        [Test]
        public void CreateValidProfileGroupType_Returns_ValidProfileGroupType()
        {
            ProfileGroupType.GroupTypeVariable buildPath = new ProfileGroupType.GroupTypeVariable("BuildPath", "Test Build Path");
            ProfileGroupType.GroupTypeVariable loadPath = new ProfileGroupType.GroupTypeVariable("LoadPath", "Test Load Path");
            ProfileGroupType profileGroupType = new ProfileGroupType("prefix");
            profileGroupType.AddVariable(buildPath);
            profileGroupType.AddVariable(loadPath);
            Assert.True(profileGroupType.IsValidGroupType());
        }

        [Test]
        public void GetPathValuesBySuffix_Returns_ExpectedPathValues()
        {
            ProfileGroupType.GroupTypeVariable buildPath = new ProfileGroupType.GroupTypeVariable("BuildPath", "Test Build Path");
            ProfileGroupType profileGroupType = new ProfileGroupType("prefix");
            profileGroupType.AddVariable(buildPath);
            Assert.AreEqual("Test Build Path", profileGroupType.GetVariableBySuffix(buildPath.Suffix).Value);
        }

        [Test]
        public void GetName_Returns_ExpectedVariableName()
        {
            ProfileGroupType.GroupTypeVariable buildPath = new ProfileGroupType.GroupTypeVariable("BuildPath", "Test Build Path");
            ProfileGroupType profileGroupType = new ProfileGroupType("prefix");
            profileGroupType.AddVariable(buildPath);
            Assert.AreEqual("prefix.BuildPath", profileGroupType.GetName(buildPath));
        }

        [Test]
        public void AddVariableToGroupType_Returns_ExpectedNotNullVariable()
        {
            ProfileGroupType.GroupTypeVariable buildPath = new ProfileGroupType.GroupTypeVariable("BuildPath", "Test Build Path");
            ProfileGroupType.GroupTypeVariable loadPath = new ProfileGroupType.GroupTypeVariable("LoadPath", "Test Load Path");
            ProfileGroupType profileGroupType = new ProfileGroupType("prefix");

            Assert.NotNull(profileGroupType.AddVariable(buildPath));
            Assert.NotNull(profileGroupType.AddVariable(loadPath));
            Assert.True(profileGroupType.Variables.Count == 2);
        }

        [Test]
        public void AddDuplicateVariableToGroupType_Returns_NullVariable()
        {
            ProfileGroupType.GroupTypeVariable buildPath = new ProfileGroupType.GroupTypeVariable("BuildPath", "Test Build Path");
            ProfileGroupType profileGroupType = new ProfileGroupType("prefix");

            Assert.NotNull(profileGroupType.AddVariable(buildPath));
            LogAssert.Expect(LogType.Error, "prefix.BuildPath already exists.");
            Assert.Null(profileGroupType.AddVariable(buildPath));
        }

        [Test]
        public void RemoveVariable_Returns_ExpectedAction()
        {
            ProfileGroupType.GroupTypeVariable buildPath = new ProfileGroupType.GroupTypeVariable("BuildPath", "Test Build Path");
            ProfileGroupType profileGroupType = new ProfileGroupType("prefix");
            profileGroupType.AddVariable(buildPath);
            profileGroupType.RemoveVariable(buildPath);
            Assert.True(profileGroupType.Variables.Count == 0);
        }

        [Test]
        public void RemoveNonExistentVariable_Returns_ExpectedAction()
        {
            ProfileGroupType.GroupTypeVariable buildPath = new ProfileGroupType.GroupTypeVariable("BuildPath", "Test Build Path");
            ProfileGroupType profileGroupType = new ProfileGroupType("prefix");
            profileGroupType.RemoveVariable(buildPath);
            LogAssert.Expect(LogType.Error, "prefix.BuildPath does not exist.");
        }
    }
}
