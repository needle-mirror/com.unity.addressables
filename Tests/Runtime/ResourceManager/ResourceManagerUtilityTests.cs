using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.ResourceManagement.Util;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;

namespace UnityEngine.ResourceManagement.Tests
{
    public class DelayedActionManagerTests
    {
        class DamTest
        {
            public bool methodInvoked;
            public int frameInvoked;
            public float timeInvoked;

            public void Method()
            {
                frameInvoked = Time.frameCount;
                timeInvoked = Time.unscaledTime;
                methodInvoked = true;
            }

            public void MethodWithParams(int p1, string p2, bool p3, float p4)
            {
                Assert.AreEqual(p1, 5);
                Assert.AreEqual(p2, "testValue");
                Assert.AreEqual(p3, true);
                Assert.AreEqual(p4, 3.14f);
            }
        }

        [UnityTest]
        public IEnumerator DelayedActionManagerInvokeSameFrame()
        {
            var testObj = new DamTest();
            int frameCalled = Time.frameCount;
            DelayedActionManager.AddAction((Action)testObj.Method);
            yield return null;
            Assert.AreEqual(frameCalled, testObj.frameInvoked);
        }

        [UnityTest]
        public IEnumerator DelayedActionManagerInvokeDelayed()
        {
            var testObj = new DamTest();
            float timeCalled = Time.unscaledTime;
            DelayedActionManager.AddAction((Action)testObj.Method, 2);
            while (!testObj.methodInvoked)
                yield return null;
            //make sure delay was at least 1 second (to account for test slowness)
            Assert.Greater(testObj.timeInvoked, timeCalled + 1);
        }

        [UnityTest]
        public IEnumerator DelayedActionManagerInvokeWithParameters()
        {
            var testObj = new DamTest();
            DelayedActionManager.AddAction((Action<int, string, bool, float>)testObj.MethodWithParams, 0, 5, "testValue", true, 3.14f);
            yield return null;
        }
    }

    public class LinkedListNodeCacheTests
    {
        LinkedListNodeCache<T> CreateCache<T>(int count)
        {
            var cache = new LinkedListNodeCache<T>();
            var temp = new List<LinkedListNode<T>>();
            for (int i = 0; i < count; i++)
                temp.Add(cache.Acquire(default(T)));
            Assert.AreEqual(count, cache.CreatedNodeCount);
            foreach (var t in temp)
                cache.Release(t);
            Assert.AreEqual(count, cache.CachedNodeCount);
            return cache;
        }

        void PopulateCache_AddRemove<T>()
        {
            var cache = CreateCache<T>(1);
            Assert.That(() => { cache.Release(cache.Acquire(default(T))); }, TestTools.Constraints.Is.Not.AllocatingGCMemory(), "GC Allocation detected");
            Assert.AreEqual(1, cache.CreatedNodeCount);
            Assert.AreEqual(1, cache.CachedNodeCount);
        }

        [Test]
        public void WhenRefTypeAndCacheNotEmpty_AddRemove_DoesNotAlloc()
        {
            PopulateCache_AddRemove<string>();
        }

        [Test]
        public void WhenValueTypeAndCacheNotEmpty_AddRemove_DoesNotAlloc()
        {
            PopulateCache_AddRemove<int>();
        }

        [Test]
        public void Release_ResetsValue()
        {
            var cache = new LinkedListNodeCache<string>();
            var node = cache.Acquire(null);
            Assert.IsNull(node.Value);
            node.Value = "TestString";
            cache.Release(node);
            Assert.IsNull(node.Value);
        }
    }

    public class DelegateListTests
    {
        [Test]
        public void WhenDelegateRemoved_DelegateIsNotInvoked()
        {
            var cache = new LinkedListNodeCache<Action<string>>();
            var delList = new DelegateList<string>(cache.Acquire, cache.Release);
            bool called = false;
            Action<string> del = s => { called = true; };
            delList.Add(del);
            delList.Remove(del);
            delList.Invoke(null);
            Assert.IsFalse(called);
            Assert.AreEqual(cache.CreatedNodeCount, cache.CreatedNodeCount);
        }

        [Test]
        public void WhenAddInsideInvoke_NewDelegatesAreCalled()
        {
            bool addedDelegateCalled = false;
            var delList = CreateDelegateList<string>();
            delList.Add(s => delList.Add(s2 => addedDelegateCalled = true));
            delList.Invoke(null);
            Assert.IsTrue(addedDelegateCalled);
        }

        [Test]
        public void WhenCleared_DelegateIsNotInvoked()
        {
            var delList = CreateDelegateList<string>();
            int invocationCount = 0;
            delList.Add(s => invocationCount++);
            delList.Clear();
            delList.Invoke(null);
            Assert.AreEqual(0, invocationCount);
        }

        [Test]
        public void DuringInvoke_CanRemoveNextDelegate()
        {
            var delList = CreateDelegateList<string>();
            bool del1Called = false;
            Action<string> del1 = s => { del1Called = true; };
            Action<string> del2 = s => delList.Remove(del1);
            delList.Add(del2);
            delList.Add(del1);
            delList.Invoke(null);
            Assert.IsFalse(del1Called);
        }

        DelegateList<T> CreateDelegateList<T>()
        {
            var cache = new LinkedListNodeCache<Action<T>>();
            return new DelegateList<T>(cache.Acquire, cache.Release);
        }

        void InvokeAllocTest<T>(T p)
        {
            var delList = CreateDelegateList<T>();
            delList.Add(s => { });
            Assert.That(() => { delList.Invoke(p); }, TestTools.Constraints.Is.Not.AllocatingGCMemory(), "GC Allocation detected");
        }

        [Test]
        public void DelegateNoGCWithRefType()
        {
            InvokeAllocTest<string>(null);
        }

        [Test]
        public void DelegateNoGCWithValueType()
        {
            InvokeAllocTest<int>(0);
        }

        static object[] KeyResultData =
        {
            new object[] {null, false, null, null},
            new object[] {"", false, null, null},
            new object[] {5, false, null, null},
            new object[] {"k", false, null, null},
            new object[] {"[k]", false, null, null},
            new object[] {"k]s[", false, null, null},
            new object[] {"k[s", false, null, null},
            new object[] {"[s]k", false, null, null},
            new object[] {"k]s", false, null, null},
            new object[] {"k[s]", true, "k", "s"},
            new object[] {"k[[s]", true, "k", "[s"},
            new object[] {"k[s[]", true, "k", "s["},
            new object[] {"k[s]]", true, "k", "s]"},
            new object[] {"k[]s]", true, "k", "]s"},
        };

        [TestCaseSource(nameof(KeyResultData))]
        public void ResourceManagerConfigExtractKeyAndSubKey_WhenPassedKey_ReturnsExpectedValue(object key, bool expectedReturn, string expectedMainKey, string expectedSubKey)
        {
            Assert.AreEqual(expectedReturn, ResourceManagerConfig.ExtractKeyAndSubKey(key, out string mainKey, out string subKey));
            Assert.AreEqual(expectedMainKey, mainKey);
            Assert.AreEqual(expectedSubKey, subKey);
        }

        [TestCase(RuntimePlatform.WebGLPlayer, false)]
        [TestCase(RuntimePlatform.OSXEditor, true)]
        public void CanIdentifyMultiThreadedPlatforms(RuntimePlatform platform, bool usesMultiThreading)
        {
            Assert.AreEqual(usesMultiThreading, PlatformUtilities.PlatformUsesMultiThreading(platform));
        }
    }

    /// <summary>
    /// Sample paths shared by the catalog path fixtures.
    /// </summary>
    static class CatalogPathSamples
    {
        // The load path Addressables generates for CCD. The catalog filename is
        // appended last, so it lands inside the ?path= query value (UUM-153444).
        public const string CcdPrefix =
            "https://p.client-api.unity3dusercontent.com/client_api/v1/environments/prod/" +
            "buckets/b1/release_by_badge/latest/entry_by_path/content/?path=/";
    }

    [TestFixture]
    class AddressablesImplGetCatalogExtensionTests
    {
        static readonly TestCaseData[] k_Cases =
        {
            // HTTP URLs with query strings — the core fix case
            new TestCaseData("http://127.0.0.1/catalog.bin?param1=value1&param2=value2",                 ".bin"),
            new TestCaseData("http://127.0.0.1/catalog.json?param=value",                                ".json"),
            new TestCaseData("http://127.0.0.1/catalog.bin?date=20240101",                               ".bin"),
            // HTTP URLs without query strings
            new TestCaseData("http://127.0.0.1/catalog.bin",                                             ".bin"),
            new TestCaseData("http://127.0.0.1/catalog.json",                                            ".json"),
            // Relative file paths
            new TestCaseData("Assets/catalog.bin",                                                       ".bin"),
            new TestCaseData("Assets/catalog.json",                                                      ".json"),
            new TestCaseData("catalog.bin",                                                              ".bin"),
            // Unsupported extensions — returned as-is; caller validates
            new TestCaseData("http://127.0.0.1/catalog.xyz?param=value",                                 ".xyz"),
            new TestCaseData("catalog.xyz",                                                              ".xyz"),
            // No extension at all
            new TestCaseData("http://127.0.0.1/catalog?param=value",                                     ""),
            new TestCaseData("catalog",                                                                  ""),
            // CCD puts the catalog filename in a query value, still last (UUM-153444)
            new TestCaseData(CatalogPathSamples.CcdPrefix + "catalog_a1b2.bin",                          ".bin"),
            new TestCaseData(CatalogPathSamples.CcdPrefix + "catalog_a1b2.json",                         ".json"),
            new TestCaseData(CatalogPathSamples.CcdPrefix + "catalog_a1b2.hash",                         ".hash"),
            // .bundle is not a provider extension, so an extension allow-list would miss it
            new TestCaseData("http://127.0.0.1/catalog.bundle",                                          ".bundle"),
            new TestCaseData(CatalogPathSamples.CcdPrefix + "catalog_a1b2.bundle",                       ".bundle"),
            // if the CCD URL does not have a trailing slash 'content' is treated as the filename and no extension
            new TestCaseData("https://p.client-api.unity3dusercontent.com/client_api/v1/environments/prod/" +
                             "buckets/b1/release_by_badge/latest/entry_by_path/content" +
                             "?path=/catalog_a1b2.bundle",                                               ""),
            // A dotted query value never outranks a filename in the path itself, so signed
            // URLs and version parameters keep working.
            new TestCaseData("http://127.0.0.1/catalog.bin?v=1.2",                                       ".bin"),
            new TestCaseData("http://127.0.0.1/catalog.bin?token=header.payload.sig",                    ".bin"),
            new TestCaseData("http://127.0.0.1/catalog.bin?backup=/old.json",                            ".bin"),
            // A host is not a file name, even when the endpoint has no path at all
            new TestCaseData("https://example.com?path=/catalog.bin",                                    ".bin"),
            new TestCaseData("https://example.com/?path=/catalog.bin",                                   ".bin"),
            // Contract: the path must end with the catalog filename. A fragment after it
            // does not, so the trailing value wins. Pinned to keep that deliberate.
            new TestCaseData("http://127.0.0.1/catalog.bin#/dir/section.json",                           ".bin"),
            new TestCaseData("http://127.0.0.1/#/dir/section.json",                                      ".json"),
            // jar files are not "remote" so we just pickup the last path
            new TestCaseData("jar:file:///StreamingAssets/file.jar!/dir/section.json",                   ".json"),
            new TestCaseData("jar:file:///StreamingAssets/file.jar!/section.json?path=/dir/section.bin", ".bin"),
            new TestCaseData("jar:file:///StreamingAssets/file.jar!/?path=/dir/section.bin",             ".bin"),
        };

        [Test, TestCaseSource(nameof(k_Cases))]
        public void GetCatalogExtension_ReturnsCorrectExtension(string path, string expected)
        {
            Assert.AreEqual(expected, CatalogUtilities.GetCatalogExtension(path));
        }
    }

    [TestFixture]
    class AddressablesImplGetHashFilePathTests
    {
        static readonly TestCaseData[] k_Cases =
        {
            // Query string with colon — the exact pattern that broke Path.ChangeExtension
            new TestCaseData("http://127.0.0.1/catalog.bin?param1=value1&param2=value2:date=number",
                             "http://127.0.0.1/catalog.hash?param1=value1&param2=value2:date=number"),
            // Regular query string
            new TestCaseData("http://127.0.0.1/catalog.json?param=value",
                             "http://127.0.0.1/catalog.hash?param=value"),
            // URL without query string
            new TestCaseData("http://127.0.0.1/catalog.bin",
                             "http://127.0.0.1/catalog.hash"),
            // Relative paths
            new TestCaseData("Assets/catalog.bin",  "Assets/catalog.hash"),
            new TestCaseData("catalog.json",         "catalog.hash"),
            // URL with no extension — ChangeExtension adds .hash
            new TestCaseData("http://127.0.0.1/catalog?param=value",
                             "http://127.0.0.1/catalog.hash?param=value"),
            new TestCaseData("catalog",              "catalog.hash"),
            // CCD: only the filename in the query value is rewritten (UUM-153444)
            new TestCaseData(CatalogPathSamples.CcdPrefix + "catalog_a1b2.bin",
                             CatalogPathSamples.CcdPrefix + "catalog_a1b2.hash"),
            new TestCaseData(CatalogPathSamples.CcdPrefix + "catalog_a1b2.json",
                             CatalogPathSamples.CcdPrefix + "catalog_a1b2.hash"),
            // A dotted query value must not be rewritten in place of the catalog
            new TestCaseData("http://127.0.0.1/catalog.bin?v=1.2",
                             "http://127.0.0.1/catalog.hash?v=1.2"),
            new TestCaseData("http://127.0.0.1/catalog.bin?token=header.payload.sig",
                             "http://127.0.0.1/catalog.hash?token=header.payload.sig"),
            // Contract violation: the catalog is not last, so the trailing value is
            // rewritten instead. Pinned - it is a no-op here, never a corrupted URL.
            new TestCaseData(CatalogPathSamples.CcdPrefix + "catalog_a1b2.bin&fallback=/old.hash",
                             CatalogPathSamples.CcdPrefix + "catalog_a1b2.bin&fallback=/old.hash"),
        };

        [Test, TestCaseSource(nameof(k_Cases))]
        public void GetHashFilePath_ReturnsCorrectHashPath(string catalogPath, string expected)
        {
            Assert.AreEqual(expected, CatalogUtilities.GetHashFilePath(catalogPath));
        }

        [Test]
        public void GetHashFilePath_ThenGetCatalogFilePath_RoundTripsCcdUrl()
        {
            const string catalog = CatalogPathSamples.CcdPrefix + "catalog_a1b2.bin";

            var hashPath = CatalogUtilities.GetHashFilePath(catalog);

            Assert.AreEqual(CatalogPathSamples.CcdPrefix + "catalog_a1b2.hash", hashPath);
            Assert.AreEqual(catalog, CatalogUtilities.GetCatalogFilePath(hashPath, ".bin"));
        }
    }

    [TestFixture]
    class GetCatalogFilePathTests
    {
        static readonly TestCaseData[] k_Cases =
        {
            // Simple extension swap: .hash → .bin / .json
            new TestCaseData("http://127.0.0.1/catalog.hash",   ".bin",  "http://127.0.0.1/catalog.bin"),
            new TestCaseData("http://127.0.0.1/catalog.hash",   ".json", "http://127.0.0.1/catalog.json"),
            // Relative and bare filenames
            new TestCaseData("Assets/catalog.hash", ".bin",  "Assets/catalog.bin"),
            new TestCaseData("C:/MyGame/Assets/catalog.hash", ".bin",  "C:/MyGame/Assets/catalog.bin"),
            new TestCaseData("catalog.hash",         ".json", "catalog.json"),
            // Query string is preserved
            new TestCaseData("http://127.0.0.1/catalog.hash?param=value", ".bin",
                             "http://127.0.0.1/catalog.bin?param=value"),
            // Query string with colon — the edge case that breaks Path.ChangeExtension directly
            new TestCaseData("http://127.0.0.1/catalog.hash?param1=value1&param2=value2:date=number", ".bin",
                             "http://127.0.0.1/catalog.bin?param1=value1&param2=value2:date=number"),
            // .hash appearing earlier in the path must NOT be touched (the original bug)
            new TestCaseData("http://h/my.hash.dir/catalog.hash",   ".bin",
                             "http://h/my.hash.dir/catalog.bin"),
            new TestCaseData("http://h/my.hash.dir/catalog.hash?q=1", ".json",
                             "http://h/my.hash.dir/catalog.json?q=1"),
            new TestCaseData("http://h/my.hash.dir/catalog.hash?path=catalog.hash", ".json",
                             "http://h/my.hash.dir/catalog.json?path=catalog.hash"),
            new TestCaseData("http://h/my.hash.dir/?nochange=catalog.hash&path=catalog.hash", ".json",
                             "http://h/my.hash.dir/?nochange=catalog.hash&path=catalog.json"),
            new TestCaseData("http://h/my.hash.dir/catalog%20v1.hash", ".bin",
                             "http://h/my.hash.dir/catalog%20v1.bin"),
            new TestCaseData("https://h/my.hash.dir/catalog v1.hash", ".bin",
                             "https://h/my.hash.dir/catalog%20v1.bin"),
            new TestCaseData("http://example.com:8000/catalog v1.bin", ".hash",
                             "http://example.com:8000/catalog%20v1.hash"),
            // CCD: the .hash sits in a query value and only it is swapped (UUM-153444)
            new TestCaseData(CatalogPathSamples.CcdPrefix + "catalog_a1b2.hash", ".bin",
                             CatalogPathSamples.CcdPrefix + "catalog_a1b2.bin"),
            new TestCaseData(CatalogPathSamples.CcdPrefix + "catalog_a1b2.hash", ".json",
                             CatalogPathSamples.CcdPrefix + "catalog_a1b2.json"),
            // A bundled catalog on CCD keeps its .bundle filename addressable
            new TestCaseData(CatalogPathSamples.CcdPrefix + "catalog_a1b2.hash", ".bundle",
                             CatalogPathSamples.CcdPrefix + "catalog_a1b2.bundle"),
        };

        [Test, TestCaseSource(nameof(k_Cases))]
        public void GetCatalogFilePath_ReturnsCorrectCatalogPath(string hashPath, string catalogExt, string expected)
        {
            Assert.AreEqual(expected, CatalogUtilities.GetCatalogFilePath(hashPath, catalogExt));
        }
    }

    [TestFixture]
    class GetCacheKeySourceTests
    {
        [Test]
        public void GetCacheKeySource_KeepsTheWholeQueryPathWhenItNamesTheCatalog()
        {
            var plain = CatalogUtilities.GetCacheKeySource(CatalogPathSamples.CcdPrefix + "catalog_a1b2.hash");
            var other = CatalogUtilities.GetCacheKeySource(CatalogPathSamples.CcdPrefix + "catalog_c3d4.hash");

            Assert.IsTrue(plain.EndsWith("?path=/catalog_a1b2.hash"), plain);
            Assert.IsTrue(other.EndsWith("?path=/catalog_c3d4.hash"), other);
            // Two catalogs in one CCD bucket used to strip to the same string and share a cache file.
            Assert.AreNotEqual(plain, other);
        }

        [Test]
        public void GetCacheKeySource_KeepsFoldersInsideTheQueryPath()
        {
            // CCD addresses entries by path within a bucket, so the same file name can sit
            // under different folders. Keeping only the file name would give both the same
            // cache file, and an offline load could then return the wrong catalog.
            var tenantA = CatalogUtilities.GetCacheKeySource(CatalogPathSamples.CcdPrefix + "tenantA/catalog.hash");
            var tenantB = CatalogUtilities.GetCacheKeySource(CatalogPathSamples.CcdPrefix + "tenantB/catalog.hash");

            Assert.AreNotEqual(tenantA, tenantB);
            Assert.IsTrue(tenantA.EndsWith("/tenantA/catalog.hash"), tenantA);
        }

        [Test]
        public void GetCacheKeySource_IgnoresAQueryThatDoesNotNameTheCatalog()
        {
            // A query after the file name is not part of the catalog's identity, so a
            // rotating value in it must not send the catalog to a new cache file.
            Assert.AreEqual("http://127.0.0.1/catalog.hash",
                CatalogUtilities.GetCacheKeySource("http://127.0.0.1/catalog.hash?param1=value1&param2=value2:date=number"));
            Assert.AreEqual("http://127.0.0.1/catalog.hash",
                CatalogUtilities.GetCacheKeySource("http://127.0.0.1/catalog.hash?v=2"));
            Assert.AreEqual("Assets/catalog.hash",
                CatalogUtilities.GetCacheKeySource("Assets/catalog.hash"));
        }

        private static readonly TestCaseData[] k_Cases =
        {
            // HTTP URLs with query strings — the core fix case
            new TestCaseData("http://127.0.0.1/catalog.bin?param1=value1&param2=value2", "http://127.0.0.1/catalog.bin"),
            new TestCaseData("http://127.0.0.1/?param1=value1&param2=catalog.bin", "http://127.0.0.1/?param2=catalog.bin"),
            new TestCaseData("http://127.0.0.1?param1=value1&param2=catalog.bin", "http://127.0.0.1/?param2=catalog.bin"),
            new TestCaseData("http://127.0.0.1?param1=", "http://127.0.0.1/"),
            new TestCaseData("http://127.0.0.1/?param1=value1&param2=value2#catalog.bin", "http://127.0.0.1/#catalog.bin"),
            new TestCaseData("http://127.0.0.1/subpath/catalog.bin?param1=value1&param2=value2", "http://127.0.0.1/subpath/catalog.bin"),
            new TestCaseData("http://127.0.0.1/?param1=value1&param2=/subpath/catalog.bin", "http://127.0.0.1/?param2=/subpath/catalog.bin"),
            new TestCaseData("http://127.0.0.1/?param1=value1&param2=value2#/subpath/catalog.bin", "http://127.0.0.1/#/subpath/catalog.bin"),
            new TestCaseData("http://127.0.0.1?param1=value1&param2=/subpath/catalog.bin", "http://127.0.0.1/?param2=/subpath/catalog.bin"),
        };

        [Test, TestCaseSource(nameof(k_Cases))]
        public void GetCacheKeySource_ReturnsCorrectCacheKey(string path, string expected)
        {
            Assert.AreEqual(expected, CatalogUtilities.GetCacheKeySource(path));
        }
    }
}
