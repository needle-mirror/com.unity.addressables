using NUnit.Framework;

namespace UnityEditor.AddressableAssets.Tests
{
    public class ProfileSettingsTests : AddressableAssetTestBase
    {
        [Test]
        public void AddRemoveProfile()
        {
            Assert.IsNotNull(settings.profileSettings);
            var dpid = settings.activeProfile = settings.profileSettings.Reset();
            var pid = settings.profileSettings.AddProfile("TestProfile", dpid);
            Assert.Contains("TestProfile", settings.profileSettings.profileNames);
            Assert.IsNotEmpty(pid);
            settings.profileSettings.RemoveProfile(pid);
            Assert.IsFalse(settings.profileSettings.profileNames.Contains("TestProfile"));
        }

        [Test]
        public void SetGetValue()
        {
            Assert.IsNotNull(settings.profileSettings);
            var dpid = settings.activeProfile = settings.profileSettings.Reset();
            var pid = settings.profileSettings.AddProfile("Test", dpid);
            settings.profileSettings.SetValueByName(dpid, "TestTag", "DefaultTestVal");
            settings.profileSettings.SetValueByName(pid, "TestTag", "OverrideTestVal");
            Assert.AreEqual("DefaultTestVal", settings.profileSettings.GetValueByName(dpid, "TestTag"));
            Assert.AreEqual("OverrideTestVal", settings.profileSettings.GetValueByName(pid, "TestTag"));
        }

        [Test]
        public void VariableNames()
        {
            Assert.IsNotNull(settings.profileSettings);
            var dpid = settings.activeProfile = settings.profileSettings.Reset();
            var pid = settings.profileSettings.AddProfile("Test", dpid);
            settings.profileSettings.SetValueByName(dpid, "TestTag", "DefaultTestVal");
            settings.profileSettings.SetValueByName(pid, "TestTag", "OverrideTestVal");
            var allNames = settings.profileSettings.GetAllVariableNames();
            Assert.IsTrue(allNames.Contains("TestTag"));
            var testNames = settings.profileSettings.GetAllVariableNames(pid);
            Assert.IsTrue(testNames.Contains("TestTag"));
            Assert.IsFalse(testNames.Contains("SomethingElse"));
        }

    }
}