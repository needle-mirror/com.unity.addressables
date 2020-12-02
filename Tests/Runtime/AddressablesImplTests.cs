using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.TestTools;

namespace AddressableAssetsIntegrationTests
{
    internal abstract partial class AddressablesIntegrationTests : IPrebuildSetup
    {
        Action<AsyncOperationHandle, Exception> m_prevHandler;

        [SetUp]
        public void SetUp()
        {
            m_prevHandler = ResourceManager.ExceptionHandler;
        }

        [TearDown]
        public void TestCleanup()
        {
            m_KeysHashSet.Clear();
            if (Directory.Exists(kCatalogFolderPath))
                Directory.Delete(kCatalogFolderPath, true);
            PostTearDownEvent = ResetAddressables;
            ResourceManager.ExceptionHandler = m_prevHandler;
        }

        private void AssertDownloadDependencyBundlesAreValid(AsyncOperationHandle op)
        {
            Assert.AreEqual(AsyncOperationStatus.Succeeded, op.Status);
            Assert.IsTrue(op.IsValid());
            var opList = (List<IAssetBundleResource>)op.Result;
            Assert.AreEqual(2, opList.Count);
            if (opList.Count > 0)
            {
                foreach (var resultBundle in opList)
                {
                    Assert.NotNull(resultBundle.GetAssetBundle());
                }
            }
        }

        [UnityTest]
        public IEnumerator CustomExceptionHandler()
        {
            yield return Init();

            var prevHandler = ResourceManager.ExceptionHandler;
            AssetReference ar = new AssetReference();
            bool handlerCalled = false;
            ResourceManager.ExceptionHandler = (handle, exception) => handlerCalled = true;
            var op = ar.InstantiateAsync();
            yield return op;
            Assert.IsTrue(op.IsDone);
            Assert.IsTrue(handlerCalled);
            ResourceManager.ExceptionHandler = prevHandler;
        }

        [UnityTest]
        public IEnumerator AddressablesImpl_ChainOperation_DefaultReturnedWhenNotInit()
        {
            yield return Init();

            AsyncOperationHandle testChainOperation = m_Addressables.ChainOperation;
            Assert.IsFalse(testChainOperation.IsValid());
        }

        [UnityTest]
        public IEnumerator AddressablesImpl_InitializeAsync_CanGetInitializationOp()
        {
            yield return Init();

            var initialOp = m_Addressables.InitializeAsync();
            Assert.AreEqual(AsyncOperationStatus.Succeeded, initialOp.Status);
            Assert.IsTrue(initialOp.IsValid());

            yield return initialOp;
        }

        [UnityTest]
        public IEnumerator AddressablesImpl_InitializeAsync_CanCreateCompleted()
        {
            // Setup
            m_Addressables = null;
            initializationComplete = false;
            yield return InitWithoutInitializeAsync();

            m_Addressables.hasStartedInitialization = true;
            var initialOp = m_Addressables.InitializeAsync();
            yield return initialOp;

            // Test
            Assert.AreEqual(AsyncOperationStatus.Succeeded, initialOp.Status);
            Assert.IsTrue(initialOp.IsValid());

            // Cleanup
            initialOp.Release();
        }

        [UnityTest]
        public IEnumerator AddressablesImpl_LoadContentCatalogAsync_CanLoad()
        {
            // Setup
            yield return Init();

            if (TypeName == "BuildScriptFastMode")
            {
                Assert.Ignore($"Skipping test {nameof(AddressablesImpl_LoadContentCatalogAsync_CanLoad)} for {TypeName}");
            }

            var location = m_Addressables.m_ResourceLocators[0].CatalogLocation;
            var op1 = m_Addressables.LoadContentCatalogAsync(location.InternalId, false);
            yield return op1;

            // Test
            Assert.IsTrue(op1.IsValid());
            Assert.AreEqual(AsyncOperationStatus.Succeeded, op1.Status);
            Assert.NotNull(op1.Result);

            // Cleanup
            op1.Release();
        }

        [UnityTest]
        public IEnumerator AddressablesImpl_LoadContentCatalogAsync_CanLoadReleaseHandle()
        {
            yield return Init();

            // Setup
            if (TypeName == "BuildScriptFastMode")
            {
                Assert.Ignore($"Skipping test {nameof(AddressablesImpl_LoadContentCatalogAsync_CanLoadReleaseHandle)} for {TypeName}");
            }

            var location = m_Addressables.m_ResourceLocators[0].CatalogLocation;
            var op1 = m_Addressables.LoadContentCatalogAsync(location.InternalId, true);
            yield return op1;

            // Test
            Assert.IsFalse(op1.IsValid());
        }

        [UnityTest]
        public IEnumerator AddressablesImpl_DownloadDependenciesAsync_CanDownloadDependenciesFromKey()
        {
            // Setup
            yield return Init();

            if (TypeName == "BuildScriptFastMode" || TypeName == "BuildScriptVirtualMode")
            {
                Assert.Ignore($"Skipping test {nameof(AddressablesImpl_DownloadDependenciesAsync_CanDownloadDependenciesFromKey)} for {TypeName}");
            }

            Caching.ClearCache();
            string label = AddressablesTestUtility.GetPrefabLabel("BASE");
            AsyncOperationHandle op = m_Addressables.DownloadDependenciesAsync(label);
            yield return op;

            // Test
            AssertDownloadDependencyBundlesAreValid(op);

            // Cleanup
            op.Release();
        }

        [UnityTest]
        public IEnumerator AddressablesImpl_DownloadDependenciesAsync_CantDownloadWhenGetResourceLocFailsKey()
        {
            // Setup
            yield return Init();

            string label = "badLabel";
            AsyncOperationHandle op = new AsyncOperationHandle();
            using (new IgnoreFailingLogMessage())
            {
                op = m_Addressables.DownloadDependenciesAsync(label);
                yield return op;
            }

            // Test
            Assert.AreEqual(AsyncOperationStatus.Failed, op.Status);
            Assert.IsTrue(op.OperationException.Message.Contains("InvalidKey"));
            Assert.IsNull(op.Result);

            // Cleanup
            op.Release();
        }

        [UnityTest]
        public IEnumerator AddressablesImpl_DownloadDependenciesAsync_CantDownloadWhenGetResourceLocFailsAutoReleasesKey()
        {
            // Setup
            yield return Init();

            string label = "badLabel";
            bool autoRelease = true;
            AsyncOperationHandle op = new AsyncOperationHandle();
            using (new IgnoreFailingLogMessage())
            {
                op = m_Addressables.DownloadDependenciesAsync(label, autoRelease);
                yield return op;
            }

            // Test
            Assert.IsFalse(op.IsValid());
        }

        [UnityTest]
        public IEnumerator AddressablesImpl_DownloadDependenciesAsync_CanDoWithChainKey()
        {
            // Setup
            if (TypeName == "BuildScriptFastMode" || TypeName == "BuildScriptVirtualMode")
            {
                Assert.Ignore($"Skipping test {nameof(AddressablesImpl_DownloadDependenciesAsync_CanDoWithChainKey)} for {TypeName}");
            }

            yield return Init();

            string label = AddressablesTestUtility.GetPrefabLabel("BASE");
            m_Addressables.hasStartedInitialization = false;
            AsyncOperationHandle op = m_Addressables.DownloadDependenciesAsync(label, false);
            m_Addressables.hasStartedInitialization = true;
            yield return op;

            // Test
            var wrapOp = op.Convert<IList<IAssetBundleResource>>();
            AssertDownloadDependencyBundlesAreValid(wrapOp);

            // Cleanup
            op.Release();
        }

        [UnityTest]
        public IEnumerator AddressablesImpl_DownloadDependenciesAsync_CanDownloadDependenciesFromOpHandle()
        {
            // Setup
            yield return Init();

            if (TypeName == "BuildScriptFastMode" || TypeName == "BuildScriptVirtualMode")
            {
                Assert.Ignore($"Skipping test {nameof(AddressablesImpl_DownloadDependenciesAsync_CanDownloadDependenciesFromOpHandle)} for {TypeName}");
            }

            IList<IResourceLocation> locations;
            var ret = m_Addressables.GetResourceLocations(new object[] { "prefabs_evenBASE" }, typeof(GameObject), Addressables.MergeMode.Intersection, out locations);

            Assert.IsTrue(ret);
            AsyncOperationHandle op = m_Addressables.DownloadDependenciesAsync(locations);
            yield return op;

            // Test
            AssertDownloadDependencyBundlesAreValid(op);

            // Cleanup
            op.Release();
        }

        [UnityTest]
        public IEnumerator AddressablesImpl_DownloadDependenciesAsync_CanDoWithChainOpHandle()
        {
            // Setup
            yield return Init();

            if (TypeName == "BuildScriptFastMode" || TypeName == "BuildScriptVirtualMode")
            {
                Assert.Ignore($"Skipping test {nameof(AddressablesImpl_DownloadDependenciesAsync_CanDoWithChainOpHandle)} for {TypeName}");
            }

            IList<IResourceLocation> locations;
            var ret = m_Addressables.GetResourceLocations(new object[] { "prefabs_evenBASE" }, typeof(GameObject), Addressables.MergeMode.Intersection, out locations);

            Assert.IsTrue(ret);
            m_Addressables.hasStartedInitialization = false;
            AsyncOperationHandle op = m_Addressables.DownloadDependenciesAsync(locations, false);
            m_Addressables.hasStartedInitialization = true;
            yield return op;

            // Test
            var wrapOp = op.Convert<IList<IAssetBundleResource>>();
            AssertDownloadDependencyBundlesAreValid(wrapOp);

            // Cleanup
            op.Release();
        }

        [UnityTest]
        public IEnumerator AddressablesImpl_DownloadDependenciesAsync_CanDownloadDependenciesFromObjectList()
        {
            // Setup
            yield return Init();

            if (TypeName == "BuildScriptFastMode" || TypeName == "BuildScriptVirtualMode")
            {
                Assert.Ignore($"Skipping test {nameof(AddressablesImpl_DownloadDependenciesAsync_CanDownloadDependenciesFromObjectList)} for {TypeName}");
            }

            List<object> deps = new List<object>();
            deps.Add(AddressablesTestUtility.GetPrefabLabel("BASE"));

            AsyncOperationHandle op = m_Addressables.DownloadDependenciesAsync(deps, Addressables.MergeMode.Intersection, false);
            yield return op;

            // Test
            AssertDownloadDependencyBundlesAreValid(op);

            // Cleanup
            op.Release();
        }

        [UnityTest]
        public IEnumerator AddressablesImpl_DownloadDependenciesAsync_CanDownloadDependenciesWithChainFromObjectList()
        {
            // Setup
            yield return Init();
            if (TypeName == "BuildScriptFastMode" || TypeName == "BuildScriptVirtualMode")
            {
                Assert.Ignore($"Skipping test {nameof(AddressablesImpl_DownloadDependenciesAsync_CanDownloadDependenciesWithChainFromObjectList)} for {TypeName}");
            }

            List<object> deps = new List<object>();
            deps.Add(AddressablesTestUtility.GetPrefabLabel("BASE"));

            m_Addressables.hasStartedInitialization = false;
            AsyncOperationHandle op = m_Addressables.DownloadDependenciesAsync(deps, Addressables.MergeMode.Intersection, false);
            yield return op;
            m_Addressables.hasStartedInitialization = true;

            // Test
            var wrapOp = op.Convert<IList<IAssetBundleResource>>();
            AssertDownloadDependencyBundlesAreValid(wrapOp);

            // Cleanup
            op.Release();
        }

        [UnityTest]
        public IEnumerator AddressablesImpl_DownloadDependenciesAsync_CantDownloadWhenGetResourceLocFailsObjectList()
        {
            // Setup
            yield return Init();

            var deps = new List<object>();
            var provideHandle = new ProvideHandle(m_Addressables.ResourceManager, new ProviderOperation<AssetBundleResource>());
            provideHandle.GetDependencies(deps);

            AsyncOperationHandle op = new AsyncOperationHandle();
            using (new IgnoreFailingLogMessage())
            {
                op = m_Addressables.DownloadDependenciesAsync(deps, Addressables.MergeMode.Intersection, false);
                yield return op;
            }

            // Test
            Assert.AreEqual(AsyncOperationStatus.Failed, op.Status);
            Assert.IsTrue(op.OperationException.Message.Contains("InvalidKey"));
            Assert.IsNull(op.Result);

            // Cleanup
            op.Release();
        }

        [UnityTest]
        public IEnumerator AddressablesImpl_DownloadDependenciesAsync_CantDownloadWhenGetResourceLocFailsAutoReleasesObjectList()
        {
            // Setup
            yield return Init();

            var deps = new List<object>();
            var provideHandle = new ProvideHandle(m_Addressables.ResourceManager, new ProviderOperation<AssetBundleResource>());
            provideHandle.GetDependencies(deps);

            bool autoRelease = true;
            AsyncOperationHandle op = new AsyncOperationHandle();
            using (new IgnoreFailingLogMessage())
            {
                op = m_Addressables.DownloadDependenciesAsync(deps, Addressables.MergeMode.Intersection, autoRelease);
                yield return op;
            }

            // Test
            Assert.IsFalse(op.IsValid());
        }
    }
}
