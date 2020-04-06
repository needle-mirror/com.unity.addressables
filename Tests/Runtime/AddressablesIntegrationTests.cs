using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement;
using UnityEditor;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;
using System;
using System.Linq;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.Util;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace AddressableAssetsIntegrationTests
{
    internal abstract partial class AddressablesIntegrationTests : IPrebuildSetup
    {
        internal protected AddressablesImpl m_Addressables;
        Dictionary<object, int> m_KeysHashSet = new Dictionary<object, int>();
        List<object> m_PrefabKeysList = new List<object>();

        Action<AsyncOperationHandle, Exception> m_PrevHandler;
        
        protected const string k_TestConfigName = "AddressableAssetSettings.Tests";
        protected const string k_TestConfigFolder = "Assets/AddressableAssetsData_AddressableAssetSettingsTests";

#if UNITY_EDITOR
        private UnityEditor.AddressableAssets.Settings.AddressableAssetSettings m_Settings;
        protected UnityEditor.AddressableAssets.Settings.AddressableAssetSettings Settings
        {
            get
            {
                if (m_Settings == null)
                    m_Settings =
                        UnityEditor.AddressableAssets.Settings.AddressableAssetSettings.Create(k_TestConfigFolder, k_TestConfigName, true, true);
                return m_Settings;
            }
        }
#endif
        protected abstract string TypeName { get; }
        protected virtual string PathFormat { get { return "Assets/{0}_AssetsToDelete_{1}"; } }

        protected virtual string GetRuntimePath(string testType, string suffix) { return string.Format("{0}Library/com.unity.addressables/settings_{1}_TEST_{2}.json", "file://{UnityEngine.Application.dataPath}/../", testType, suffix); }
        protected virtual string GetCatalogPath(string testType, string suffix) { return string.Format("{0}Library/com.unity.addressables/catalog_{1}_TEST_{2}.json", "file://{UnityEngine.Application.dataPath}/../", testType, suffix); }
        protected virtual ILocationSizeData CreateLocationSizeData(string name, long size, uint crc, string hash) { return null; }

        private object AssetReferenceObjectKey { get { return m_PrefabKeysList.FirstOrDefault(s => s.ToString().Contains("AssetReferenceBehavior")); }}

        public virtual void Setup()
        {
            AddressablesTestUtility.Setup(TypeName, PathFormat, "BASE");
        }

        [OneTimeTearDown]
        public virtual void DeleteTempFiles()
        {
            ResourceManager.ExceptionHandler = m_PrevHandler;
            AddressablesTestUtility.TearDown(TypeName, PathFormat, "BASE");
        }
        int m_StartingOpCount;
        int m_StartingTrackedHandleCount;
        int m_StartingInstanceCount;

        private Action PostTearDownEvent = null;

        [TearDown]
        public void TearDown()
        {
            Assert.AreEqual(m_StartingOpCount, m_Addressables.ResourceManager.OperationCacheCount);
            Assert.AreEqual(m_StartingTrackedHandleCount, m_Addressables.TrackedHandleCount);
            Assert.AreEqual(m_StartingInstanceCount, m_Addressables.ResourceManager.InstanceOperationCount);

            PostTearDownEvent?.Invoke();
            PostTearDownEvent = null;
        }

        //we must wait for Addressables initialization to complete since we are clearing out all of its data for the tests.
        public bool initializationComplete;
        string currentInitType = null;
        IEnumerator Init()
        {
            if (!initializationComplete || TypeName != currentInitType)
            {
                if (m_Addressables == null)
                    m_Addressables = new AddressablesImpl(new LRUCacheAllocationStrategy(1000, 1000, 100, 10));

                if (TypeName != currentInitType)
                {
                    currentInitType = TypeName;

                    var runtimeSettingsPath = m_Addressables.RuntimePath + "/settingsBASE.json";
#if UNITY_EDITOR
                    
                    runtimeSettingsPath = GetRuntimePath(currentInitType, "BASE");
#endif
                    runtimeSettingsPath = m_Addressables.ResolveInternalId(runtimeSettingsPath);
                    Debug.LogFormat("Initializing from path {0}", runtimeSettingsPath);
                    yield return m_Addressables.InitializeAsync(runtimeSettingsPath, "BASE", false);

                    foreach (var locator in m_Addressables.ResourceLocators)
                    {
                        if (locator.Keys == null)
                            continue;

                        foreach (var key in locator.Keys)
                        {
                            IList<IResourceLocation> locs;
                            if (locator.Locate(key, typeof(object), out locs))
                            {
                                var isPrefab = locs.All(s => s.InternalId.EndsWith(".prefab"));
                                if (!m_KeysHashSet.ContainsKey(key))
                                {
                                    if (isPrefab)
                                        m_PrefabKeysList.Add(key);
                                    m_KeysHashSet.Add(key, locs.Count);
                                }
                                else
                                {
                                    m_KeysHashSet[key] = m_KeysHashSet[key] + locs.Count;
                                }
                            }
                        }
                    }
                    initializationComplete = true;

                    m_PrevHandler = ResourceManager.ExceptionHandler;
                    ResourceManager.ExceptionHandler = null;
                }
            }
            m_Addressables.ResourceManager.ClearDiagnosticCallbacks();
            m_StartingOpCount = m_Addressables.ResourceManager.OperationCacheCount;
            m_StartingTrackedHandleCount = m_Addressables.TrackedHandleCount;
            m_StartingInstanceCount = m_Addressables.ResourceManager.InstanceOperationCount;
        }
        private void ResetAddressables()
        {
            m_Addressables = null;
            currentInitType = null;
            initializationComplete = false;
        }
    }

#if UNITY_EDITOR
    class AddressablesIntegrationTestsFastMode : AddressablesIntegrationTests
    {
        protected override string TypeName { get { return "BuildScriptFastMode"; } }
    }

    class AddressablesIntegrationTestsVirtualMode : AddressablesIntegrationTests
    {
        protected override string TypeName { get { return "BuildScriptVirtualMode"; } }
        protected override ILocationSizeData CreateLocationSizeData(string name, long size, uint crc, string hash)
        {
            return new UnityEngine.ResourceManagement.ResourceProviders.Simulation.VirtualAssetBundleRequestOptions()
            {
                BundleName = name,
                BundleSize = size,
                Crc = crc,
                Hash = hash
            };
        }

    }


    class AddressablesIntegrationTestsPackedPlayMode : AddressablesIntegrationTests
    {
        protected override string TypeName { get { return "BuildScriptPackedPlayMode"; } }
        protected override string GetRuntimePath(string testType, string suffix) { return "{UnityEngine.AddressableAssets.Addressables.RuntimePath}/settings" + suffix + ".json"; }
        protected override string GetCatalogPath(string testType, string suffix) { return "{UnityEngine.AddressableAssets.Addressables.RuntimePath}/catalog" + suffix + ".json"; }

        public override void Setup()
        {
            AddressablesTestUtility.Setup("BuildScriptPackedMode", PathFormat, "BASE");
            AddressablesTestUtility.Setup(TypeName, PathFormat, "BASE");
        }
        public override void DeleteTempFiles()
        {
            AddressablesTestUtility.TearDown("BuildScriptPackedMode", PathFormat, "BASE");
            AddressablesTestUtility.TearDown(TypeName, PathFormat, "BASE");
        }
    }

#endif
    
    class AddressablesIntegrationPlayer : AddressablesIntegrationTests
    {
        protected override string TypeName { get { return "BuildScriptPackedMode"; } }
        protected override string GetRuntimePath(string testType, string suffix) { return "{UnityEngine.AddressableAssets.Addressables.RuntimePath}/settings" + suffix + ".json"; }
        protected override string GetCatalogPath(string testType, string suffix) { return "{UnityEngine.AddressableAssets.Addressables.RuntimePath}/catalog" + suffix + ".json"; }

        protected override ILocationSizeData CreateLocationSizeData(string name, long size, uint crc, string hash)
        {
            return new AssetBundleRequestOptions()
            {
                BundleName = name,
                BundleSize =size,
                Crc = crc,
                Hash = hash
            };
        }

    }

}
