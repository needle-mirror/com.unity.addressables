using System;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using UnityEngine;

namespace UnityEditor.AddressableAssets.Tests
{
    public class HostingServicesManagerTests
    {
        protected const string TestConfigName = "AddressableAssetSettings.HostingServicesManagerTests";
        protected const string TestConfigFolder = "Assets/AddressableAssetsData_HostingServicesManagerTests";

        private HostingServicesManager m_manager;
        private AddressableAssetSettings m_settings;

        [SetUp]
        public void Setup()
        {
            m_manager = new HostingServicesManager();
            m_settings = AddressableAssetSettings.Create(TestConfigFolder, TestConfigName, false, false);
            m_settings.HostingServicesManager = m_manager;
            var group = m_settings.CreateGroup("testGroup", false, false, false);
            group.AddSchema<BundledAssetGroupSchema>();
            m_settings.groups.Add(group);
        }

        [TearDown]
        public void TearDown()
        {
            var services = m_manager.HostingServices.ToArray();
            foreach (var svc in services)
            {
                svc.StopHostingService();
                m_manager.RemoveHostingService(svc);
            }
            if (Directory.Exists(TestConfigFolder))
                AssetDatabase.DeleteAsset(TestConfigFolder);
            EditorBuildSettings.RemoveConfigObject(TestConfigName);
        }

        // GlobalProfileVariables

        [Test]
        public void GlobalProfileVariablesShould_ReturnDictionaryOfKeyValuePairs()
        {
            var vars = m_manager.GlobalProfileVariables;
            Assert.NotNull(vars);
        }

        [Test]
        public void GlobalProfileVariablesShould_ContainPrivateIpAddressKey()
        {
            m_manager.Initialize(m_settings);
            var vars = m_manager.GlobalProfileVariables;
            Assert.NotNull(vars);
            const string key = HostingServicesManager.KPrivateIpAddressKey;
            Assert.Contains(key, vars.Keys);
            Assert.NotNull(vars[key]);
        }

        // IsInitialized

        [Test]
        public void IsInitializedShould_BecomeTrueAfterInitializeCall()
        {
            Assert.IsFalse(m_manager.IsInitialized);
            m_manager.Initialize(m_settings);
            Assert.IsTrue(m_manager.IsInitialized);
        }

        // HostingServices

        [Test]
        public void HostingServicesShould_ReturnListOfManagedServices()
        {
            m_manager.Initialize(m_settings);
            Assert.NotNull(m_manager.HostingServices);
            Assert.IsEmpty(m_manager.HostingServices);
            var svc = m_manager.AddHostingService(typeof(TestHostingService), "test");
            Assert.IsTrue(m_manager.HostingServices.Contains(svc));
        }

        // RegisteredServiceTypes

        [Test]
        public void RegisteredServiceTypesShould_AlwaysContainBuiltinServiceTypes()
        {
            Assert.NotNull(m_manager.RegisteredServiceTypes);
            Assert.Contains(typeof(HttpHostingService), m_manager.RegisteredServiceTypes);
        }

        [Test]
        public void RegisteredServiceTypesShould_NotContainDuplicates()
        {
            m_manager.Initialize(m_settings);
            m_manager.AddHostingService(typeof(TestHostingService), "test1");
            m_manager.AddHostingService(typeof(TestHostingService), "test2");
            Assert.IsTrue(m_manager.RegisteredServiceTypes.Length == 1);
        }

        // NextInstanceId

        [Test]
        public void NextInstanceIdShould_IncrementAfterServiceIsAdded()
        {
            m_manager.Initialize(m_settings);
            Assert.IsTrue(m_manager.NextInstanceId == 0);
            m_manager.AddHostingService(typeof(TestHostingService), "test1");
            Assert.IsTrue(m_manager.NextInstanceId == 1);
        }

        // Initialize

        [Test]
        public void InitializeShould_AssignTheGivenSettingsObject()
        {
            Assert.Null(m_manager.Settings);
            m_manager.Initialize(m_settings);
            Assert.IsTrue(m_manager.IsInitialized);
            Assert.NotNull(m_manager.Settings);
            Assert.AreSame(m_manager.Settings, m_settings);
        }

        [Test]
        public void InitializeShould_SetGlobalProfileVariables()
        {
            Assert.IsTrue(m_manager.GlobalProfileVariables.Count == 0);
            m_manager.Initialize(m_settings);
            Assert.IsTrue(m_manager.IsInitialized);
            Assert.IsTrue(m_manager.GlobalProfileVariables.Count > 0);
        }

        [Test]
        public void InitializeShould_OnlyInitializeOnce()
        {
            var so = ScriptableObject.CreateInstance<AddressableAssetSettings>();
            m_manager.Initialize(m_settings);
            Assert.IsTrue(m_manager.IsInitialized);
            m_manager.Initialize(so);
            Assert.AreSame(m_manager.Settings, m_settings);
            Assert.AreNotSame(m_manager.Settings, so);
        }

        // StopAllService

        [Test]
        public void StopAllServicesShould_StopAllRunningServices()
        {
            m_manager.Initialize(m_settings);
            var svc = m_manager.AddHostingService(typeof(TestHostingService), "test");
            svc.HostingServiceContentRoots.Add("/");
            Assert.IsFalse(svc.IsHostingServiceRunning);
            svc.StartHostingService();
            Assert.IsTrue(svc.IsHostingServiceRunning);
            m_manager.StopAllServices();
            Assert.IsFalse(svc.IsHostingServiceRunning);
        }

        // StartAllServices

        [Test]
        public void StartAllServicesShould_StartAllStoppedServices()
        {
            m_manager.Initialize(m_settings);
            var svc = m_manager.AddHostingService(typeof(TestHostingService), "test");
            svc.HostingServiceContentRoots.Add("/");
            Assert.IsFalse(svc.IsHostingServiceRunning);
            m_manager.StartAllServices();
            Assert.IsTrue(svc.IsHostingServiceRunning);
        }

        // AddHostingService

        [Test]
        public void AddHostingServiceShould_ThrowIfTypeDoesNotImplementInterface()
        {
            Assert.Throws<ArgumentException>(() => { m_manager.AddHostingService(typeof(object), "test"); });
        }

        [Test]
        public void AddHostingServiceShould_ThrowIfTypeIsAbstract()
        {
            Assert.Throws<MissingMethodException>(() =>
            {
                m_manager.AddHostingService(typeof(AbstractTestHostingService), "test");
            });
        }

        [Test]
        public void AddHostingServiceShould_AddTypeToRegisteredServiceTypes()
        {
            m_manager.Initialize(m_settings);
            Assert.NotNull(m_manager.RegisteredServiceTypes);
            m_manager.AddHostingService(typeof(TestHostingService), "test");
            Assert.Contains(typeof(TestHostingService), m_manager.RegisteredServiceTypes);
        }

        [Test]
        public void AddHostingServiceShould_RegisterLoggerForService()
        {
            m_manager.Initialize(m_settings);
            m_manager.AddHostingService(typeof(TestHostingService), "test");
        }

        [Test]
        public void AddHostingServiceShould_SetDescriptiveNameOnService()
        {
            m_manager.Initialize(m_settings);
            var svc = m_manager.AddHostingService(typeof(TestHostingService), "test");
            Assert.AreEqual(svc.DescriptiveName, "test");
        }

        [Test]
        public void AddHostingServiceShould_SetNextInstanceIdOnService()
        {
            m_manager.Initialize(m_settings);
            m_manager.AddHostingService(typeof(TestHostingService), "test");
            m_manager.AddHostingService(typeof(TestHostingService), "test");
            var id = m_manager.NextInstanceId;
            var svc = m_manager.AddHostingService(typeof(TestHostingService), "test");
            Assert.AreEqual(id, svc.InstanceId);
        }

        [Test]
        public void AddHostingServiceShould_SetContentRootsOnService()
        {
            m_manager.Initialize(m_settings);
            var svc = m_manager.AddHostingService(typeof(TestHostingService), "test");
            Assert.IsNotEmpty(svc.HostingServiceContentRoots);
        }

        [Test]
        public void AddHostingServiceShould_PostModificationEventToSettings()
        {
            var wait = new ManualResetEvent(false);

            m_settings.OnModification = null;
            m_settings.OnModification += (s, evt, obj) =>
            {
                if (evt == AddressableAssetSettings.ModificationEvent.HostingServicesManagerModified)
                    wait.Set();
            };

            m_manager.Initialize(m_settings);
            m_manager.AddHostingService(typeof(TestHostingService), "test");
            Assert.IsTrue(wait.WaitOne(100));
        }

        [Test]
        public void AddHostingServiceShould_ReturnServiceInstance()
        {
            m_manager.Initialize(m_settings);
            var svc = m_manager.AddHostingService(typeof(TestHostingService), "test");
            Assert.NotNull(svc);
        }

        [Test]
        public void AddHostingServiceShould_RegisterStringEvalFuncs()
        {
            m_manager.Initialize(m_settings);
            var svc = m_manager.AddHostingService(typeof(TestHostingService), "test");
            Assert.IsTrue(ProfileStringEvalDelegateIsRegistered(m_settings, svc));
        }

        // RemoveHostingService

        [Test]
        public void RemoveHostingServiceShould_StopRunningService()
        {
            m_manager.Initialize(m_settings);
            var svc = m_manager.AddHostingService(typeof(TestHostingService), "test");
            Assert.IsFalse(svc.IsHostingServiceRunning);
            svc.StartHostingService();
            Assert.IsTrue(svc.IsHostingServiceRunning);
            m_manager.RemoveHostingService(svc);
            Assert.IsFalse(svc.IsHostingServiceRunning);
        }

        [Test]
        public void RemoveHostingServiceShould_UnregisterStringEvalFuncs()
        {
            m_manager.Initialize(m_settings);
            var svc = m_manager.AddHostingService(typeof(TestHostingService), "test");
            Assert.IsTrue(ProfileStringEvalDelegateIsRegistered(m_settings, svc));
            m_manager.RemoveHostingService(svc);
            Assert.IsFalse(ProfileStringEvalDelegateIsRegistered(m_settings, svc));
        }

        [Test]
        public void RemoveHostingServiceShould_PostModificationEventToSettings()
        {
            var wait = new ManualResetEvent(false);

            m_settings.OnModification = null;
            m_settings.OnModification += (s, evt, obj) =>
            {
                if (evt == AddressableAssetSettings.ModificationEvent.HostingServicesManagerModified)
                    wait.Set();
            };

            m_manager.Initialize(m_settings);
            var svc = m_manager.AddHostingService(typeof(TestHostingService), "test");
            m_manager.RemoveHostingService(svc);
            Assert.IsTrue(wait.WaitOne(100));
        }

        // OnEnable

        [Test]
        public void OnEnableShould_RegisterForSettingsModificationEvents()
        {
            var len = m_settings.OnModification.GetInvocationList().Length;
            m_manager.Initialize(m_settings);
            m_manager.OnEnable();
            Assert.Greater(m_settings.OnModification.GetInvocationList().Length, len);
        }

        [Test]
        public void OnEnableShould_RegisterProfileStringEvalFuncsForServices()
        {
            m_manager.Initialize(m_settings);
            var svc = m_manager.AddHostingService(typeof(TestHostingService), "test");
            m_settings.profileSettings.onProfileStringEvaluation = null;
            m_manager.OnEnable();
            Assert.IsTrue(ProfileStringEvalDelegateIsRegistered(m_settings, svc));
        }

        [Test]
        public void OnEnableShould_RegisterLoggerWithServices()
        {
            m_manager.Initialize(m_settings);
            var svc = m_manager.AddHostingService(typeof(TestHostingService), "test") as TestHostingService;
            Assert.NotNull(svc);
            svc.Logger = null;
            Assert.Null(svc.Logger);
            m_manager.OnEnable();
            Assert.NotNull(svc.Logger);
        }

        [Test]
        public void OnEnableShould_RegisterProfileStringEvalFuncForManager()
        {
            m_manager.Initialize(m_settings);
            m_manager.OnEnable();
            Assert.IsTrue(ProfileStringEvalDelegateIsRegistered(m_settings, m_manager));
        }

        [Test]
        public void OnEnableShould_RefreshGlobalProfileVariables()
        {
            m_manager.Initialize(m_settings);
            m_manager.GlobalProfileVariables.Clear();
            m_manager.OnEnable();
            Assert.GreaterOrEqual(m_manager.GlobalProfileVariables.Count, 1);
        }

        // OnDisable

        [Test]
        public void OnDisableShould_DeRegisterForSettingsModificationEvents()
        {
            var len = m_settings.OnModification.GetInvocationList().Length;
            m_manager.Initialize(m_settings);
            m_manager.OnEnable();
            m_manager.OnEnable();
            m_manager.OnEnable();
            Assert.Greater(m_settings.OnModification.GetInvocationList().Length, len);
            m_manager.OnDisable();
            Assert.AreEqual(len, m_settings.OnModification.GetInvocationList().Length);
        }

        [Test]
        public void OnEnableShould_UnregisterProfileStringEvalFuncForManager()
        {
            m_manager.Initialize(m_settings);
            m_manager.OnEnable();
            Assert.IsTrue(ProfileStringEvalDelegateIsRegistered(m_settings, m_manager));
            m_manager.OnDisable();
            Assert.IsFalse(ProfileStringEvalDelegateIsRegistered(m_settings, m_manager));
        }

        [Test]
        public void OnDisableShould_RegisterNullLoggerForServices()
        {
            m_manager.Initialize(m_settings);
            var svc = m_manager.AddHostingService(typeof(TestHostingService), "test") as TestHostingService;
            Assert.IsNotNull(svc);
            m_manager.Initialize(m_settings);
            m_manager.OnEnable();
            Assert.IsNotNull(svc.Logger);
            m_manager.OnDisable();
            Assert.IsNull(svc.Logger);
        }

        [Test]
        public void OnDisableShould_DeRegisterProfileStringEvalFuncsForServices()
        {
            m_manager.Initialize(m_settings);
            var svc = m_manager.AddHostingService(typeof(TestHostingService), "test") as TestHostingService;
            Assert.IsNotNull(svc);
            m_manager.Initialize(m_settings);
            m_manager.OnEnable();
            Assert.IsTrue(ProfileStringEvalDelegateIsRegistered(m_settings, svc));
            m_manager.OnDisable();
            Assert.IsFalse(ProfileStringEvalDelegateIsRegistered(m_settings, svc));
        }

        // RegisterLogger

        [Test]
        public void LoggerShould_SetLoggerForManagerAndManagedServices()
        {
            m_manager.Initialize(m_settings);
            var svc = m_manager.AddHostingService(typeof(TestHostingService), "test") as TestHostingService;
            Assert.IsNotNull(svc);
            m_manager.Initialize(m_settings);
            var logger = new Logger(Debug.unityLogger.logHandler);
            Assert.AreNotEqual(logger, svc.Logger);
            Assert.AreNotEqual(logger, m_manager.Logger);
            m_manager.Logger = logger;
            Assert.AreEqual(logger, svc.Logger);
            Assert.AreEqual(logger, m_manager.Logger);
        }

        [Test]
        public void LoggerShould_SetDebugUnityLoggerIfNull()
        {
            m_manager.Initialize(m_settings);
            var svc = m_manager.AddHostingService(typeof(TestHostingService), "test") as TestHostingService;
            Assert.IsNotNull(svc);
            m_manager.Initialize(m_settings);
            var logger = new Logger(Debug.unityLogger.logHandler);
            m_manager.Logger = logger;
            Assert.AreNotEqual(Debug.unityLogger, svc.Logger);
            Assert.AreNotEqual(Debug.unityLogger, m_manager.Logger);
            m_manager.Logger = null;
            Assert.AreEqual(Debug.unityLogger, svc.Logger);
            Assert.AreEqual(Debug.unityLogger, m_manager.Logger);
        }

        // RefreshGLobalProfileVariables

        [Test]
        public void RefreshGlobalProfileVariablesShould_AddOrUpdatePrivateIpAddressVar()
        {
            m_manager.GlobalProfileVariables.Clear();
            Assert.IsEmpty(m_manager.GlobalProfileVariables);
            m_manager.RefreshGlobalProfileVariables();
            Assert.IsNotEmpty(m_manager.GlobalProfileVariables);
        }

        [Test]
        public void RefreshGlobalProfileVariablesShould_RemoveUnknownVars()
        {
            m_manager.GlobalProfileVariables.Add("test", "test");
            Assert.IsTrue(m_manager.GlobalProfileVariables.ContainsKey("test"));
            m_manager.RefreshGlobalProfileVariables();
            Assert.IsFalse(m_manager.GlobalProfileVariables.ContainsKey("test"));
        }

        // BatchMode

        [Test]
        public void BatchModeShould_InitializeManagerWithDefaultSettings()
        {
            Assert.IsFalse(m_manager.IsInitialized);
            HostingServicesManager.BatchMode(m_settings);
            Assert.IsTrue(m_manager.IsInitialized);
        }

        [Test]
        public void BatchModeShould_StartAllServices()
        {
            m_manager.Initialize(m_settings);
            var svc = m_manager.AddHostingService(typeof(TestHostingService), "test");
            Assert.IsFalse(svc.IsHostingServiceRunning);
            HostingServicesManager.BatchMode(m_settings);
            Assert.IsTrue(svc.IsHostingServiceRunning);
        }

        private static bool ProfileStringEvalDelegateIsRegistered(AddressableAssetSettings s, IHostingService svc)
        {
            var del = new AddressableAssetProfileSettings.ProfileStringEvaluationDelegate(svc.EvaluateProfileString);
            var list = s.profileSettings.onProfileStringEvaluation.GetInvocationList();
            return list.Contains(del);
        }

        private static bool ProfileStringEvalDelegateIsRegistered(AddressableAssetSettings s, HostingServicesManager m)
        {
            var del = new AddressableAssetProfileSettings.ProfileStringEvaluationDelegate(m.EvaluateGlobalProfileVariableKey);
            var list = s.profileSettings.onProfileStringEvaluation.GetInvocationList();
            return list.Contains(del);
        }
    }
}