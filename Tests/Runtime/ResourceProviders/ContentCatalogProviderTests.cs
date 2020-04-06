using System;
using System.Collections;
using NUnit.Framework;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
#if UNITY_EDITOR
using UnityEditor.AddressableAssets.Settings;
#endif
using UnityEngine.AddressableAssets.Initialization;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.TestTools;

namespace UnityEngine.AddressableAssets.ResourceProviders.Tests
{
	[TestFixture]
	public class ContentCatalogProviderTests : AddressablesTestFixture
	{
		const string k_LocationName = "TestLocation";
		const string k_LocationId = "TestLocationID";
		const string k_CacheLocationId = "CacheLocationID";
		const string k_RemoteLocationId = "RemoteLocationID";
        private const string k_TempAssetFolder = "Assets/TempFolder";
        private const string k_TempBuildFolder = "TempBuildFolder";
        private readonly string m_RuntimeCatalogFilename;

        public ContentCatalogProviderTests()
        {
            m_RuntimeCatalogFilename = "catalog" + m_UniqueTestName + ".bundle";
		}

		ResourceLocationBase m_SimpleLocation = new ResourceLocationBase(k_LocationName, k_LocationId, typeof(ContentCatalogProvider).FullName, typeof(object));

        protected override TestBuildScriptMode BuildScriptMode => TestBuildScriptMode.Packed;

#if UNITY_EDITOR
		internal override void Setup(AddressableAssetSettings settings, string tempAssetFolder)
        {
            settings.BundleLocalCatalog = true;
        }
#endif

		[Test]
	    public void DetermineIdToLoad_IfLocalCatalogsOnly_ReturnsMainId()
	    {
	        var contentCatalogOp = new ContentCatalogProvider.InternalOp();

	        IResourceLocation[] dependencies = new IResourceLocation[(int)ContentCatalogProvider.DependencyHashIndex.Count];

	        dependencies[(int)ContentCatalogProvider.DependencyHashIndex.Remote] = new ResourceLocationBase(string.Empty, k_RemoteLocationId, typeof(ContentCatalogProvider).FullName, typeof(object));
	        dependencies[(int)ContentCatalogProvider.DependencyHashIndex.Cache] = new ResourceLocationBase(string.Empty, k_CacheLocationId, typeof(ContentCatalogProvider).FullName, typeof(object));

	        var location = new ResourceLocationBase(k_LocationName, k_LocationId, typeof(ContentCatalogProvider).FullName, typeof(object), dependencies);
	        var loadedId = contentCatalogOp.DetermineIdToLoad(location, new List<object> { "hash" , string.Empty}, true);

            Assert.AreEqual(k_LocationId, loadedId);
        }

        [Test]
		public void DetermineIdToLoad_IfNoDependencies_ReturnsMainId()
		{
			var contentCatalogOp = new ContentCatalogProvider.InternalOp();

			var loadedId = contentCatalogOp.DetermineIdToLoad(m_SimpleLocation, null);
			
			Assert.AreEqual(k_LocationId, loadedId);
		}
		
		[Test]
		public void DetermineIdToLoad_IfTooFewDependencies_ReturnsMainId()
		{
			var contentCatalogOp = new ContentCatalogProvider.InternalOp();

			var loadedId = contentCatalogOp.DetermineIdToLoad(m_SimpleLocation, new List<object>{1});
			
			Assert.AreEqual(k_LocationId, loadedId);
		}
		[Test]
		public void DetermineIdToLoad_IfTooManyDependencies_ReturnsMainId()
		{
			var contentCatalogOp = new ContentCatalogProvider.InternalOp();

			var loadedId = contentCatalogOp.DetermineIdToLoad(m_SimpleLocation, new List<object>{1,2,3});
			
			Assert.AreEqual(k_LocationId, loadedId);
		}
		
		[Test]
		public void DetermineIdToLoad_IfOfflineAndNoCache_ReturnsMainId()
		{
			var contentCatalogOp = new ContentCatalogProvider.InternalOp();

			var loadedId = contentCatalogOp.DetermineIdToLoad(m_SimpleLocation, new List<object>{string.Empty, string.Empty});
			
			Assert.AreEqual(k_LocationId, loadedId);
		}

		[Test]
		public void DetermineIdToLoad_IfOfflineAndHasCache_ReturnsCacheId()
		{
			var contentCatalogOp = new ContentCatalogProvider.InternalOp();

			IResourceLocation[] dependencies = new IResourceLocation[(int)ContentCatalogProvider.DependencyHashIndex.Count];

			dependencies[(int)ContentCatalogProvider.DependencyHashIndex.Remote] = new ResourceLocationBase(string.Empty, k_RemoteLocationId, typeof(ContentCatalogProvider).FullName, typeof(object));
			dependencies[(int)ContentCatalogProvider.DependencyHashIndex.Cache] = new ResourceLocationBase(string.Empty, k_CacheLocationId, typeof(ContentCatalogProvider).FullName, typeof(object));

			var location = new ResourceLocationBase(k_LocationName, k_LocationId, typeof(ContentCatalogProvider).FullName, typeof(object), dependencies);
			var loadedId = contentCatalogOp.DetermineIdToLoad(location, new List<object>{string.Empty, "hash"});
			
			Assert.AreEqual(k_CacheLocationId, loadedId);
		}

		[Test]
		public void DetermineIdToLoad_IfOnlineMatchesCache_ReturnsCacheId()
		{
			
			var contentCatalogOp = new ContentCatalogProvider.InternalOp();

			IResourceLocation[] dependencies = new IResourceLocation[(int)ContentCatalogProvider.DependencyHashIndex.Count];

			dependencies[(int)ContentCatalogProvider.DependencyHashIndex.Remote] = new ResourceLocationBase(string.Empty, k_RemoteLocationId, typeof(ContentCatalogProvider).FullName, typeof(object));
			dependencies[(int)ContentCatalogProvider.DependencyHashIndex.Cache] = new ResourceLocationBase(string.Empty, k_CacheLocationId, typeof(ContentCatalogProvider).FullName, typeof(object));

			var location = new ResourceLocationBase(k_LocationName, k_LocationId, typeof(ContentCatalogProvider).FullName, typeof(object), dependencies);
			var loadedId = contentCatalogOp.DetermineIdToLoad(location, new List<object>{"hash", "hash"});
			
			Assert.AreEqual(k_CacheLocationId, loadedId);
		}

		[Test]
		public void DetermineIdToLoad_IfOnlineMismatchesCache_ReturnsRemoteId()
		{
			var contentCatalogOp = new ContentCatalogProvider.InternalOp();

			IResourceLocation[] dependencies = new IResourceLocation[(int)ContentCatalogProvider.DependencyHashIndex.Count];

			dependencies[(int)ContentCatalogProvider.DependencyHashIndex.Remote] = new ResourceLocationBase(string.Empty, k_RemoteLocationId, typeof(ContentCatalogProvider).FullName, typeof(object));
			dependencies[(int)ContentCatalogProvider.DependencyHashIndex.Cache] = new ResourceLocationBase(string.Empty, k_CacheLocationId, typeof(ContentCatalogProvider).FullName, typeof(object));

			var location = new ResourceLocationBase(k_LocationName, k_LocationId, typeof(ContentCatalogProvider).FullName, typeof(object), dependencies);
			
			
			var loadedId = contentCatalogOp.DetermineIdToLoad(location, new List<object>{"newHash", "hash"});
			Assert.AreEqual(k_RemoteLocationId, loadedId);
			
			loadedId = contentCatalogOp.DetermineIdToLoad(location, new List<object>{"newHash", string.Empty});
			Assert.AreEqual(k_RemoteLocationId, loadedId);
		}

        [Test]
        [TestCase(null, typeof(ArgumentNullException))]
        [TestCase("invalid", typeof(ArgumentException))]
        [TestCase("file.txt", typeof(ArgumentException))]
		public void BundledCatalog_LoadCatalogFromBundle_InvalidBundlePath_ShouldThrow(string path, Type exceptionType)
        {
            Assert.Throws(exceptionType, () => new ContentCatalogProvider.InternalOp.BundledCatalog(path));
        }

		[UnityTest]
		public IEnumerator BundledCatalog_LoadCatalogFromBundle_InvalidBundleFileFormat_ShouldFail()
		{
			var bundleFilePath = Path.Combine(k_TempBuildFolder, "catalog.bundle");
            Directory.CreateDirectory(Path.GetDirectoryName(bundleFilePath));

			var bytes = new byte[] { 1, 2, 3, 4, 5, 6 };
			File.WriteAllBytes(bundleFilePath, bytes);

		    var bundledCatalog = new ContentCatalogProvider.InternalOp.BundledCatalog(bundleFilePath);
		    bundledCatalog.LoadCatalogFromBundleAsync();

            yield return new WaitWhile(() => bundledCatalog.OpInProgress);

#if UNITY_2020_1_OR_NEWER
			LogAssert.Expect(LogType.Error, new Regex("Failed to read data for", RegexOptions.IgnoreCase));
#endif
            LogAssert.Expect(LogType.Error, new Regex("Unable to load", RegexOptions.IgnoreCase));
			Assert.IsFalse(bundledCatalog.OpIsSuccess);

            if (Directory.Exists(k_TempBuildFolder))
                Directory.Delete(k_TempBuildFolder, true);
		}

		[UnityTest]
        public IEnumerator BundledCatalog_LoadCatalogFromBundle_ShouldLoadCatalogAndUnloadResources()
        {
			var bundleFilePath = Path.Combine(Addressables.RuntimePath, m_RuntimeCatalogFilename);
			
            var bundledCatalog = new ContentCatalogProvider.InternalOp.BundledCatalog(bundleFilePath);
            bundledCatalog.LoadCatalogFromBundleAsync();
            bundledCatalog.OnLoaded += catalogData =>
            {
                Assert.NotNull(catalogData);
                Assert.AreEqual(ResourceManagerRuntimeData.kCatalogAddress, catalogData.ProviderId);
            };
            
            yield return new WaitWhile(() => bundledCatalog.OpInProgress);

			Assert.IsTrue(bundledCatalog.OpIsSuccess);
			Assert.Null(bundledCatalog.m_CatalogAssetBundle);
		}

        [UnityTest]
        public IEnumerator BundledCatalog_LoadCatalogFromBundle_WhenCalledMultipleTimes_OpNotCompleted_FirstShouldSucceedAndOthersShouldFail()
        {
            var bundleFilePath = Path.Combine(Addressables.RuntimePath, m_RuntimeCatalogFilename);

            var timesCalled = 0;
            var bundledCatalog = new ContentCatalogProvider.InternalOp.BundledCatalog(bundleFilePath);
            bundledCatalog.OnLoaded += catalogData =>
            {
                Assert.NotNull(catalogData);
                Assert.AreEqual(ResourceManagerRuntimeData.kCatalogAddress, catalogData.ProviderId);
                timesCalled++;
            };

            bundledCatalog.LoadCatalogFromBundleAsync();
			bundledCatalog.LoadCatalogFromBundleAsync();
            LogAssert.Expect(LogType.Error, new Regex("progress", RegexOptions.IgnoreCase));

			yield return new WaitWhile(() => bundledCatalog.OpInProgress);

			Assert.AreEqual(1, timesCalled);
		}

        [UnityTest]
        public IEnumerator BundledCatalog_LoadCatalogFromBundle_WhenCalledMultipleTimes_OpCompleted_AllShouldSucceed()
        {
			var bundleFilePath = Path.Combine(Addressables.RuntimePath, m_RuntimeCatalogFilename);

            var timesCalled = 0;
            var bundledCatalog = new ContentCatalogProvider.InternalOp.BundledCatalog(bundleFilePath);
            bundledCatalog.OnLoaded += catalogData =>
            {
                Assert.NotNull(catalogData);
                Assert.AreEqual(ResourceManagerRuntimeData.kCatalogAddress, catalogData.ProviderId);
                timesCalled++;
            };

            bundledCatalog.LoadCatalogFromBundleAsync();
            yield return new WaitWhile(() => bundledCatalog.OpInProgress);

            bundledCatalog.LoadCatalogFromBundleAsync();
            yield return new WaitWhile(() => bundledCatalog.OpInProgress);

            Assert.AreEqual(2, timesCalled);
            Assert.IsTrue(bundledCatalog.OpIsSuccess);
		}
    }
}