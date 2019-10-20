using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.TestTools;

namespace UnityEngine.AddressableAssets.ResourceProviders.Tests
{
    [TestFixture]
    public class DynamicContentUpdateTests
    {
        class TestLocator : IResourceLocator
        {
            Dictionary<object, IList<IResourceLocation>> m_Locations = new Dictionary<object, IList<IResourceLocation>>();
            public IEnumerable<object> Keys => m_Locations.Keys;

            public string LocatorId { get; set; }
            public TestLocator(string id, params ResourceLocationBase[] locs)
            {
                LocatorId = id;
                foreach (var l in locs)
                    m_Locations.Add(l.PrimaryKey, new List<IResourceLocation>(new IResourceLocation[] { l }));
            }

            public bool Locate(object key, Type type, out IList<IResourceLocation> locations)
            {
                return m_Locations.TryGetValue(key, out locations);
            }
        }

        class TestHashProvider : ResourceProviderBase
        {
            string m_Hash;
            public TestHashProvider(string id, string hash)
            {
                m_ProviderId = id;
                m_Hash = hash;
            }
            public override void Provide(ProvideHandle provideHandle)
            {
                provideHandle.Complete(m_Hash, true, null);
            }
        }

        class TestCatalogProvider : ResourceProviderBase
        {
            string m_LocatorId;
            public TestCatalogProvider(string locatorId)
            {
                m_LocatorId = locatorId;
            }
            public override void Provide(ProvideHandle provideHandle)
            {
                var deps = new List<object>();
                provideHandle.GetDependencies(deps);
                provideHandle.Complete(new TestLocator(m_LocatorId), true, null);
            }
        }

        const string kRemoteHashProviderId = "RemoteHashProvider";
        const string kLocalHashProviderId = "LocalHashProvider";
        const string kLocatorId = "Locator";
        const string kNewLocatorId = "NewLocator";

        [UnityTest]
        public IEnumerator CheckForUpdates_Returns_EmptyList_When_HashesMatch()
        {
            var remoteHashLoc = new ResourceLocationBase("RemoteHash", "Remote", kRemoteHashProviderId, typeof(string));
            var localHashLoc = new ResourceLocationBase("LocalHash", "Local", kLocalHashProviderId, typeof(string));
            var catalogLoc = new ResourceLocationBase("cat", "cat_id", nameof(TestCatalogProvider), typeof(IResourceLocator), remoteHashLoc, localHashLoc);
            var aa = new AddressablesImpl(null);
            aa.ResourceManager.ResourceProviders.Add(new TestHashProvider(kRemoteHashProviderId, "same"));
            aa.ResourceManager.ResourceProviders.Add(new TestHashProvider(kLocalHashProviderId, "same"));
            aa.ResourceManager.ResourceProviders.Add(new TestCatalogProvider(kNewLocatorId));
            aa.AddResourceLocator(new TestLocator(kLocatorId), "same", catalogLoc);
            var op = aa.CheckForCatalogUpdates(false);
            yield return op;
            Assert.IsNotNull(op.Result);
            Assert.AreEqual(0, op.Result.Count);
            Assert.AreEqual(0, aa.CatalogsWithAvailableUpdates.Count());
            aa.Release(op);
        }

        [UnityTest]
        public IEnumerator CheckForUpdates_Returns_NonEmptyList_When_HashesDontMatch()
        {
            var remoteHashLoc = new ResourceLocationBase("RemoteHash", "Remote", kRemoteHashProviderId, typeof(string));
            var localHashLoc = new ResourceLocationBase("LocalHash", "Local", kLocalHashProviderId, typeof(string));
            var catalogLoc = new ResourceLocationBase("cat", "cat_id", nameof(TestCatalogProvider), typeof(IResourceLocator), remoteHashLoc, localHashLoc);
            var aa = new AddressablesImpl(null);
            aa.ResourceManager.ResourceProviders.Add(new TestHashProvider(kRemoteHashProviderId, "different"));
            aa.ResourceManager.ResourceProviders.Add(new TestHashProvider(kLocalHashProviderId, "same"));
            aa.ResourceManager.ResourceProviders.Add(new TestCatalogProvider(kNewLocatorId));
            aa.AddResourceLocator(new TestLocator(kLocatorId), "same", catalogLoc);
            var op = aa.CheckForCatalogUpdates(false);
            yield return op;
            Assert.IsNotNull(op.Result);
            Assert.AreEqual(1, op.Result.Count);
            Assert.AreEqual(1, aa.CatalogsWithAvailableUpdates.Count());
            aa.Release(op);
        }

        [UnityTest]
        public IEnumerator CheckForUpdates_Returns_OnlyModifiedResults()
        {
            var remoteHashLoc = new ResourceLocationBase("RemoteHash1", "Remote", kRemoteHashProviderId + 1, typeof(string));
            var localHashLoc = new ResourceLocationBase("LocalHash1", "Local", kLocalHashProviderId + 1, typeof(string));
            var catalogLoc = new ResourceLocationBase("cat1", "cat_id", nameof(TestCatalogProvider), typeof(IResourceLocator), remoteHashLoc, localHashLoc);

            var remoteHashLoc2 = new ResourceLocationBase("RemoteHash2", "Remote", kRemoteHashProviderId + 2, typeof(string));
            var localHashLoc2 = new ResourceLocationBase("LocalHash2", "Local", kLocalHashProviderId + 2, typeof(string));
            var catalogLoc2 = new ResourceLocationBase("cat2", "cat_id", nameof(TestCatalogProvider), typeof(IResourceLocator), remoteHashLoc2, localHashLoc2);

            var aa = new AddressablesImpl(null);
            aa.ResourceManager.ResourceProviders.Add(new TestHashProvider(kRemoteHashProviderId + 1, "same"));
            aa.ResourceManager.ResourceProviders.Add(new TestHashProvider(kLocalHashProviderId + 1, "same"));
            aa.ResourceManager.ResourceProviders.Add(new TestHashProvider(kRemoteHashProviderId + 2, "different"));
            aa.ResourceManager.ResourceProviders.Add(new TestHashProvider(kLocalHashProviderId + 2, "same"));
            aa.ResourceManager.ResourceProviders.Add(new TestCatalogProvider(kNewLocatorId));
            aa.AddResourceLocator(new TestLocator(kLocatorId), "same", catalogLoc);
            aa.AddResourceLocator(new TestLocator(kLocatorId + 2), "same", catalogLoc2);

            var op = aa.CheckForCatalogUpdates(false);
            yield return op;
            Assert.IsNotNull(op.Result);
            Assert.AreEqual(1, op.Result.Count);
            Assert.AreEqual(kLocatorId + 2, op.Result[0]);
            Assert.AreEqual(1, aa.CatalogsWithAvailableUpdates.Count());
            Assert.AreEqual(kLocatorId + 2, aa.CatalogsWithAvailableUpdates.First());
            aa.Release(op);
        }

        [UnityTest]
        public IEnumerator UpdateContent_UpdatesCatalogs_Returns_ListOfLocators()
        {
            var remoteHashLoc = new ResourceLocationBase("RemoteHash", "Remote", kRemoteHashProviderId, typeof(string));
            var localHashLoc = new ResourceLocationBase("LocalHash", "Local", kLocalHashProviderId, typeof(string));
            var catalogLoc = new ResourceLocationBase("cat", "cat_id", typeof(TestCatalogProvider).FullName, typeof(object), remoteHashLoc, localHashLoc);
            var aa = new AddressablesImpl(null);
            aa.ResourceManager.ResourceProviders.Add(new TestHashProvider(kRemoteHashProviderId, "different"));
            aa.ResourceManager.ResourceProviders.Add(new TestHashProvider(kLocalHashProviderId, "same"));
            aa.ResourceManager.ResourceProviders.Add(new TestCatalogProvider(kNewLocatorId));
            aa.AddResourceLocator(new TestLocator(kLocatorId, remoteHashLoc, localHashLoc, catalogLoc), "same", catalogLoc);
            var op = aa.CheckForCatalogUpdates(false);
            yield return op;
            Assert.IsNotNull(op.Result);
            Assert.AreEqual(1, op.Result.Count);
            var updateOp = aa.UpdateCatalogs(op.Result, false);
            aa.Release(op);

            yield return updateOp;
            Assert.IsNotNull(updateOp.Result);
            Assert.AreEqual(1, updateOp.Result.Count);
            Assert.AreEqual(kNewLocatorId, updateOp.Result[0].LocatorId);
            aa.Release(updateOp);
        }
    }
}