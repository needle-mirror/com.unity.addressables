using NUnit.Framework;
using System.IO;
using UnityEngine;

namespace UnityEditor.AddressableAssets.Tests
{
    [TestFixtureSource("HostingServices")]
    public class HostingServiceInterfaceTests
    {
        protected const string TestConfigName = "AddressableAssetSettings.HostingServiceInterfaceTests";
        protected const string TestConfigFolder = "Assets/AddressableAssetsData_HostingServiceInterfaceTests";

        // ReSharper disable once UnusedMember.Local
        private static IHostingService[] HostingServices
        {
            get
            {
                return new[]
                {
                    new HttpHostingService() as IHostingService
                };
            }
        }

        private readonly IHostingService m_service;

        public HostingServiceInterfaceTests(IHostingService svc)
        {
            m_service = svc;
        }

        [OneTimeTearDown]
        public void Cleanup()
        {
            if (Directory.Exists(TestConfigFolder))
                AssetDatabase.DeleteAsset(TestConfigFolder);
            EditorBuildSettings.RemoveConfigObject(TestConfigName);
        }

        private IHostingService GetManagedService(out AddressableAssetSettings settings)
        {
            var m = new HostingServicesManager();
            settings = AddressableAssetSettings.Create(TestConfigFolder, TestConfigName, false, false);
            settings.HostingServicesManager = m;
            var group = settings.CreateGroup("testGroup", false, false, false);
            group.AddSchema<BundledAssetGroupSchema>();
            settings.groups.Add(group);
            m.Initialize(settings);
            return m.AddHostingService(m_service.GetType(), "test");
        }

        // HostingServiceContentRoots

        [Test]
        public void HostingServiceContentRootsShould_ReturnListOfContentRoots()
        {
            AddressableAssetSettings s;
            var svc = GetManagedService(out s);
            Assert.IsNotEmpty(s.groups);
            Assert.IsNotEmpty(svc.HostingServiceContentRoots);
            var schema = s.groups[0].GetSchema<BundledAssetGroupSchema>();
            Assert.Contains(schema.HostingServicesContentRoot, svc.HostingServiceContentRoots);
        }

        // ProfileVariables

        [Test]
        public void ProfileVariablesShould_ReturnProfileVariableKeyValuePairs()
        {
            var vars = m_service.ProfileVariables;
            Assert.IsNotEmpty(vars);
        }

        // IsHostingServiceRunning, StartHostingService, and StopHostingService

        [Test]
        public void IsHostingServiceRunning_StartHostingService_StopHostingService()
        {
            AddressableAssetSettings s;
            var svc = GetManagedService(out s);
            Assert.IsFalse(svc.IsHostingServiceRunning);
            svc.StartHostingService();
            Assert.IsTrue(svc.IsHostingServiceRunning);
            svc.StopHostingService();
            Assert.IsFalse(svc.IsHostingServiceRunning);
        }

        // OnBeforeSerialize

        [Test]
        public void OnBeforeSerializeShould_PersistExpectedDataToKeyDataStore()
        {
            var data = new KeyDataStore();
            m_service.DescriptiveName = "Testing 123";
            m_service.InstanceId = 123;
            m_service.HostingServiceContentRoots.Clear();
            m_service.HostingServiceContentRoots.AddRange(new[] {"/test123", "/test456"});
            m_service.OnBeforeSerialize(data);
            Assert.AreEqual("Testing 123", data.GetData("DescriptiveName", string.Empty));
            Assert.AreEqual(123, data.GetData("InstanceId", 0));
            Assert.AreEqual("/test123;/test456", data.GetData("ContentRoot", string.Empty));
        }

        // OnAfterDeserialize

        [Test]
        public void OnAfterDeserializeShould_RestoreExpectedDataFromKeyDataStore()
        {
            var data = new KeyDataStore();
            data.SetData("DescriptiveName", "Testing 123");
            data.SetData("InstanceId", 123);
            data.SetData("ContentRoot", "/test123;/test456");
            m_service.OnAfterDeserialize(data);
            Assert.AreEqual("Testing 123", m_service.DescriptiveName);
            Assert.AreEqual(123, m_service.InstanceId);
            Assert.Contains("/test123", m_service.HostingServiceContentRoots);
            Assert.Contains("/test456", m_service.HostingServiceContentRoots);
        }

        // EvaluateProfileString

        [Test]
        public void EvaluateProfileStringShould_CorrectlyReplaceKeyValues()
        {
            var vars = m_service.ProfileVariables;
            vars.Add("foo", "bar");
            var val = m_service.EvaluateProfileString("foo");
            Assert.AreEqual("bar", val);
        }

        [Test]
        public void EvaluateProfileStringShould_ReturnNullForNonMatchingKey()
        {
            var val = m_service.EvaluateProfileString("foo2");
            Assert.IsNull(val);
        }

        // RegisterLogger

        [Test]
        public void LoggerShould_UseTheProvidedLogger()
        {
            var l = new Logger(Debug.unityLogger.logHandler);
            m_service.Logger = l;
            Assert.AreEqual(l, m_service.Logger);
        }

        [Test]
        public void RegisterLoggerShould_UseTheDebugUnityLoggerWhenParamIsNull()
        {
            m_service.Logger = null;
            Assert.AreEqual(Debug.unityLogger, m_service.Logger);
        }

        // DescriptiveName

        [Test]
        public void DescriptiveNameShould_AllowGetAndSetOfDescriptiveName()
        {
            m_service.DescriptiveName = "test";
            Assert.AreEqual("test", m_service.DescriptiveName);
        }

        // InstanceId

        [Test]
        public void InstanceIdShould_AllowGetAndSetOfInstanceId()
        {
            m_service.InstanceId = 999;
            Assert.AreEqual(999, m_service.InstanceId);
        }
    }
}