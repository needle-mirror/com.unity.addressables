using NUnit.Framework;

namespace UnityEditor.AddressableAssets.Tests
{
    public class ProfileValueReferenceTests : AddressableAssetTestBase
    {
        [Test]
        public void IsValueValid()
        {
            var group = m_settings.FindGroup(AddressableAssetSettings.DefaultLocalGroupName);
            Assert.IsNotNull(group);
            var schema = group.GetSchema<BundledAssetGroupSchema>();
            Assert.IsNotNull(schema);
            var pid = m_settings.profileSettings.GetProfileDataById(schema.BuildPath.Id);
            Assert.IsNotNull(pid);
            var varVal = m_settings.profileSettings.GetValueById(m_settings.activeProfileId, pid.Id);
            Assert.IsNotNull(varVal);
            var evalVal = m_settings.profileSettings.EvaluateString(m_settings.activeProfileId, varVal);
            var val = schema.BuildPath.GetValue(m_settings);
            Assert.AreEqual(evalVal, val);
        }

        [Test]
        public void CanSetValueByName()
        {
            var group = m_settings.FindGroup(AddressableAssetSettings.DefaultLocalGroupName);
            Assert.IsNotNull(group);
            var schema = group.GetSchema<BundledAssetGroupSchema>();
            Assert.IsNotNull(schema);
            bool callbackInvoked = false;
            schema.BuildPath.OnValueChanged += s => callbackInvoked = true;
            schema.BuildPath.SetVariableByName(m_settings, AddressableAssetSettings.kLocalLoadPath);
            Assert.IsTrue(callbackInvoked);
            Assert.AreEqual(schema.BuildPath.Id, m_settings.profileSettings.GetProfileDataByName(AddressableAssetSettings.kLocalLoadPath).Id);
        }

        [Test]
        public void CanSetValueById()
        {
            var group = m_settings.FindGroup(AddressableAssetSettings.DefaultLocalGroupName);
            Assert.IsNotNull(group);
            var schema = group.GetSchema<BundledAssetGroupSchema>();
            Assert.IsNotNull(schema);
            schema.BuildPath.SetVariableById(m_settings, m_settings.profileSettings.GetProfileDataByName(AddressableAssetSettings.kLocalLoadPath).Id);
            Assert.AreEqual(schema.BuildPath.Id, m_settings.profileSettings.GetProfileDataByName(AddressableAssetSettings.kLocalLoadPath).Id);
        }

        [Test]
        public void CallbackInvokedWhenValueChanged()
        {
            var group = m_settings.FindGroup(AddressableAssetSettings.DefaultLocalGroupName);
            Assert.IsNotNull(group);
            var schema = group.GetSchema<BundledAssetGroupSchema>();
            Assert.IsNotNull(schema);
            bool callbackInvoked = false;
            schema.BuildPath.OnValueChanged += s => callbackInvoked = true;
            schema.BuildPath.SetVariableByName(m_settings, AddressableAssetSettings.kLocalLoadPath);
            Assert.IsTrue(callbackInvoked);
        }

        [Test]
        public void CallbackNotInvokedWhenValueNotChanged()
        {
            var group = m_settings.FindGroup(AddressableAssetSettings.DefaultLocalGroupName);
            Assert.IsNotNull(group);
            var schema = group.GetSchema<BundledAssetGroupSchema>();
            Assert.IsNotNull(schema);
            bool callbackInvoked = false;
            schema.BuildPath.OnValueChanged += s => callbackInvoked = true;
            schema.BuildPath.SetVariableById(m_settings, "invalid id");
            Assert.IsFalse(callbackInvoked);
        }
    }
}