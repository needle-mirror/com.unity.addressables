using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets.Initialization;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.AddressableAssets.Utility;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.ResourceManagement.Util;
using UnityEngine.TestTools;

namespace UnityEditor.AddressableAssets.Tests
{
    using Debug = UnityEngine.Debug;
    using Random = UnityEngine.Random;

    public class ContentCatalogTests
    {
        List<object> m_Keys;
        List<Type> m_Providers;

        [Serializable]
        public class SerializableKey
        {
            public int index;
            public string path;
        }

        [OneTimeSetUp]
        public void Init()
        {
            m_Keys = new List<object>();
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < 1000; i++)
            {
                var r = Random.Range(0, 100);
                if (r < 20)
                {
                    int len = Random.Range(1, 5);
                    for (int j = 0; j < len; j++)
                        sb.Append(GUID.Generate().ToString());
                    m_Keys.Add(sb.ToString());
                    sb.Length = 0;
                }
                else if (r < 40)
                {
                    m_Keys.Add((ushort)(i * 13));
                }
                else if (r < 50)
                {
                    m_Keys.Add(i * 13);
                }
                else if (r < 60)
                {
                    m_Keys.Add((uint)(i * 13));
                }
                else if (r < 80)
                {
                    m_Keys.Add(new SerializableKey { index = i, path = GUID.Generate().ToString() });
                }
                else
                {
                    m_Keys.Add(Hash128.Parse(GUID.Generate().ToString()));
                }
            }

            m_Providers = new List<Type>();
            m_Providers.Add(typeof(BundledAssetProvider));
            m_Providers.Add(typeof(AssetBundleProvider));
            m_Providers.Add(typeof(AssetDatabaseProvider));
            m_Providers.Add(typeof(JsonAssetProvider));
            m_Providers.Add(typeof(TextDataProvider));
            m_Providers.Add(typeof(TextDataProvider));
            m_Providers.Add(typeof(BinaryAssetProvider<BinaryContentCatalogData.Serializer>));
        }

        List<T> GetRandomSubset<T>(List<T> keys, int count)
        {
            if (keys.Count == 0 || count == 0)
                return new List<T>();
            var entryKeys = new HashSet<T>();
            for (int k = 0; k < count; k++)
                entryKeys.Add(keys[Random.Range(0, keys.Count)]);
            return entryKeys.ToList();
        }
        [Serializable]
        public class EvenData
        {
            public int index;
            public string path;
        }

        [Serializable]
        public class OddData
        {
            public int index;
            public string path;
        }

        [UnityTest]
        public IEnumerator RunStressContinuously([Values(100)] int locateCallCount, [Values(1000)] int locCount, [Values(128, 256, 512)] int bufferCacheSize)
        {
            var locType = typeof(UnityEngine.Object);
            var catalog = new BinaryContentCatalogData();
            var entries = new List<ContentCatalogDataEntry>();
            var allKeys = new List<object>();

            var deps = new List<List<object>>();
            for (int j = 0; j < 10; j++)
            {
                var depKeys = new List<object>();
                for (int i = 0; i < 5; i++)
                {
                    var d = new ContentCatalogDataEntry(
                        typeof(AssetBundle),
                        $"https://mysuperlongwebservername.com/internalId/path/blah/subdir with a very long name that should get cached and reused/urlstuffetc/assetbundle2345324d2354f3425g345g345g345g{i}.bundle",
                        "AssetBundleProvider",
                        new object[] { $"AssetBundleName_23d234d34f32gf243f23f235g2543g25g123d24{i}.bundle" },
                        null,
                        new AssetBundleRequestOptions { BundleName = "derwrgwergwetrhewrtherth" });
                    entries.Add(d);
                    depKeys.Add(d.Keys[0]);
                }
                deps.Add(depKeys);
            }

            for (int i = 0; i < locCount; i++)
            {
                var entryKeys = new object[] { $"CommonPartOfKey{i%100}/WithALongPath{i%10}/UniquePathOfKey-{i}", $"LabelNameA.{i / 10}", $"LabelNameB.{i / 100}", "CommonLabelA", "CommonLabelB" };
                entries.Add(new ContentCatalogDataEntry(
                    locType,
                    $"InternalAsset/PathInside/AssetBundle/filename{i}.fileExtension",
                    "BundledAssetProvider",
                    entryKeys,
                    deps[Random.Range(0, deps.Count)]));
                allKeys.Add(entryKeys[0]);
            }
            catalog.SetData(entries);
            var data = catalog.SerializeToByteArray();
            var loadedCatalog = new BinaryContentCatalogData(new BinaryStorageBuffer.Reader(data, bufferCacheSize, 0, new BinaryContentCatalogData.Serializer()));
            var locator = loadedCatalog.CreateCustomLocator("", null) as BinaryContentCatalogData.ResourceLocator;
            yield return null;
            int frameCount = 1000;
            for (int x = 0; x < frameCount; x++)
            {
                for (int i = 0; i < locateCallCount; i++)
                {
                    locator.Locate(allKeys[Random.Range(0, allKeys.Count)], locType, out var locs);
                    foreach(var l in locs)
                    {
                        var id = l.InternalId;
                        var pk = l.PrimaryKey;
                        var t = l.ResourceType;
                        var p = l.ProviderId;
                        var h = l.DependencyHashCode;
                        if (l.HasDependencies)
                        {
                            foreach (var d in l.Dependencies)
                            {
                                id = d.InternalId;
                                pk = d.PrimaryKey;
                                t = d.ResourceType;
                                p = d.ProviderId;
                                h = d.DependencyHashCode;
                                var o = d.Data as AssetBundleRequestOptions;
                            }
                        }
                    }
                }
                yield return null;
            }
        }

        static BinaryContentCatalogData CatalogFrom(List<ContentCatalogDataEntry> entries)
        {
            var catalog = new BinaryContentCatalogData();
            catalog.SetData(entries);
            return catalog;
        }

        // Builds a catalog shaped like a real one: far more asset entries than bundles, assets
        // reaching bundles through dependencies, and three distinct resource types.
        static BinaryContentCatalogData BuildMixedCatalog(int assetCount, int bundleCount)
        {
            var entries = new List<ContentCatalogDataEntry>();
            var bundleKeys = new List<object>();

            for (int i = 0; i < bundleCount; i++)
            {
                var bundleName = $"bundle{i}.bundle";
                entries.Add(new ContentCatalogDataEntry(typeof(IAssetBundleResource),
                    $"http://bundles.test/{bundleName}", "AssetBundleProvider",
                    new object[] {bundleName}, null,
                    new AssetBundleRequestOptions {BundleName = bundleName, BundleSize = 100 + i}));
                bundleKeys.Add(bundleName);
            }

            for (int i = 0; i < assetCount; i++)
            {
                entries.Add(new ContentCatalogDataEntry(typeof(GameObject),
                    $"Assets/asset{i}.prefab", "BundledAssetProvider",
                    new object[] {$"asset{i}", "sharedLabel"},
                    new List<object> {bundleKeys[i % bundleCount]}));
            }

            entries.Add(new ContentCatalogDataEntry(typeof(TextAsset),
                "Assets/text.txt", "BundledAssetProvider",
                new object[] {"textKey"}, new List<object> {bundleKeys[0]}));

            return CatalogFrom(entries);
        }

        const string kDuplicatedId = "Assets/duplicated.prefab";
        const string kMultiTypeId = "Assets/multiType.asset";

        // Two shapes the writer keeps as separate records because their primary keys or
        // types differ, even though the location they describe does not: one asset
        // addressed twice, and one asset serialized under two types.
        static BinaryContentCatalogData BuildCatalogWithSharedInternalIds()
        {
            var entries = new List<ContentCatalogDataEntry>
            {
                // Same id and same type, so these are one location.
                new ContentCatalogDataEntry(typeof(GameObject), kDuplicatedId,
                    "BundledAssetProvider", new object[] {"firstKey"}),
                new ContentCatalogDataEntry(typeof(GameObject), kDuplicatedId,
                    "BundledAssetProvider", new object[] {"secondKey"}),

                // Same id but different types, so these are two locations.
                new ContentCatalogDataEntry(typeof(GameObject), kMultiTypeId,
                    "BundledAssetProvider", new object[] {"multiTypeGameObject"}),
                new ContentCatalogDataEntry(typeof(TextAsset), kMultiTypeId,
                    "BundledAssetProvider", new object[] {"multiTypeText"}),
            };

            return CatalogFrom(entries);
        }

        const string kTokenizedBundleId = "{UnityEngine.AddressableAssets.Addressables.RuntimePath}/shared.bundle";

        // One bundle path spelled two ways. The writer keeps two records because the raw
        // strings differ, but reading the catalog resolves both to the same path.
        static BinaryContentCatalogData BuildCatalogWithTokenizedAndLiteralInternalIds()
        {
            var entries = new List<ContentCatalogDataEntry>
            {
                new ContentCatalogDataEntry(typeof(IAssetBundleResource), kTokenizedBundleId,
                    "AssetBundleProvider", new object[] {"tokenizedKey"}),
                new ContentCatalogDataEntry(typeof(IAssetBundleResource),
                    UnityEngine.AddressableAssets.Addressables.RuntimePath + "/shared.bundle",
                    "AssetBundleProvider", new object[] {"literalKey"}),
            };

            return CatalogFrom(entries);
        }

        readonly List<BinaryStorageBuffer.Reader> m_CatalogReaders = new List<BinaryStorageBuffer.Reader>();

        [TearDown]
        public void DisposeCatalogReaders()
        {
            // Each reader pins its buffer with a GCHandle, so leaving them to finalization holds
            // the catalog bytes for the rest of the run.
            foreach (var reader in m_CatalogReaders)
                reader.Dispose();
            m_CatalogReaders.Clear();
        }

        BinaryStorageBuffer.Reader ReaderOver(byte[] data)
        {
            var reader = new BinaryStorageBuffer.Reader(data, 512, 0, new BinaryContentCatalogData.Serializer());
            m_CatalogReaders.Add(reader);
            return reader;
        }

        BinaryStorageBuffer.Reader ReadCatalog(BinaryContentCatalogData catalog)
        {
            return ReaderOver(catalog.SerializeToByteArray());
        }

        BinaryContentCatalogData.ResourceLocator LoadLocator(BinaryContentCatalogData catalog, out BinaryStorageBuffer.Reader reader)
        {
            reader = ReadCatalog(catalog);
            var loaded = new BinaryContentCatalogData(reader);
            return loaded.CreateCustomLocator("", null) as BinaryContentCatalogData.ResourceLocator;
        }

        static List<string> Ordered(IEnumerable<string> ids)
        {
            return ids.OrderBy(s => s, StringComparer.Ordinal).ToList();
        }

        // No Distinct, so a repeated location would show up as a longer list.
        static List<string> AllLocationIds(IResourceLocator locator)
        {
            return Ordered(locator.AllLocations.Select(l => l.InternalId));
        }

        // What AllLocations used to do: resolve every key and dedupe the results.
        static HashSet<IResourceLocation> KeyResolvedLocations(IResourceLocator locator)
        {
            var locations = new HashSet<IResourceLocation>(new ResourceLocationComparer());
            foreach (var key in locator.Keys)
            {
                if (locator.Locate(key, null, out var found))
                {
                    foreach (var location in found)
                        locations.Add(location);
                }
            }

            return locations;
        }

        static List<string> KeyResolvedLocationIds(IResourceLocator locator)
        {
            return Ordered(KeyResolvedLocations(locator).Select(l => l.InternalId));
        }

        // Two entries can share an internal id, so these views keep the type as well.
        static string IdAndType(IResourceLocation location)
        {
            return $"{location.InternalId}|{location.ResourceType.Name}";
        }

        static List<string> AllLocationIdsAndTypes(IResourceLocator locator)
        {
            return Ordered(locator.AllLocations.Select(IdAndType));
        }

        static List<string> KeyResolvedIdsAndTypes(IResourceLocator locator)
        {
            return Ordered(KeyResolvedLocations(locator).Select(IdAndType));
        }

        // The filter the download-size docs recommend: what GetDownloadSizeAsync counts.
        static List<string> SizeBearingIds(IResourceLocator locator)
        {
            return Ordered(locator.AllLocations
                .Where(l => l.Data is ILocationSizeData)
                .Select(l => l.InternalId));
        }

        [Test]
        public void AllLocations_MatchesKeyResolvedLocations()
        {
            const int assetCount = 200;
            const int bundleCount = 10;
            var locator = LoadLocator(BuildMixedCatalog(assetCount, bundleCount), out _);

            var scanned = AllLocationIds(locator);

            // The scan never resolves a key, so this is what proves it still reaches every
            // location - including bundles only an asset's dependencies name.
            Assert.AreEqual(KeyResolvedLocationIds(locator), scanned);

            // One yield per entry: the assets, the bundles, and the text asset.
            Assert.AreEqual(assetCount + bundleCount + 1, scanned.Count);
            Assert.AreEqual(scanned.Distinct().Count(), scanned.Count, "AllLocations repeated a location.");
        }

        [Test]
        public void AllLocations_MatchesKeyResolvedLocations_OnOptimizedCatalog()
        {
            const int assetCount = 200;
            const int bundleCount = 10;
            var original = LoadLocator(BuildMixedCatalog(assetCount, bundleCount), out _);

            // Optimizing rewrites dependency keys to short hex, so a location can no longer be
            // recognised by its key. The scan reads records instead.
            var optimizedCatalog = BinaryContentCatalogData.CreateOptimizedCopy(
                new BinaryContentCatalogData(ReadCatalog(BuildMixedCatalog(assetCount, bundleCount))));
            var optimized = LoadLocator(optimizedCatalog, out _);

            var scanned = AllLocationIds(optimized);

            Assert.AreEqual(KeyResolvedLocationIds(optimized), scanned);

            // Optimizing rewrites keys, not content, so the locations are unchanged.
            Assert.AreEqual(AllLocationIds(original), scanned);
            Assert.AreEqual(SizeBearingIds(original), SizeBearingIds(optimized));
        }

        // Counts how many locations an enumeration actually builds. The reader's own cache
        // stats compile out unless BINARY_STORAGE_BUFFER_STATS is defined, so they always
        // report zero here.
        class CountingLocationAdapter :
            BinaryStorageBuffer.ISerializationAdapter<BinaryContentCatalogData.ResourceLocator.ResourceLocation>
        {
            readonly BinaryStorageBuffer.ISerializationAdapter m_Inner =
                new BinaryContentCatalogData.ResourceLocator.ResourceLocation.Serializer(true);

            public int Built;

            public IEnumerable<BinaryStorageBuffer.ISerializationAdapter> Dependencies => m_Inner.Dependencies;

            public uint Serialize(BinaryStorageBuffer.Writer writer, object val)
            {
                return m_Inner.Serialize(writer, val);
            }

            public object Deserialize(BinaryStorageBuffer.Reader reader, Type t, uint offset, out uint size)
            {
                Built++;
                return m_Inner.Deserialize(reader, t, offset, out size);
            }
        }

        // Registered last so it overrides the location adapter the catalog serializer pulls in.
        BinaryContentCatalogData.ResourceLocator LoadCountingLocator(BinaryContentCatalogData catalog, out CountingLocationAdapter counter)
        {
            counter = new CountingLocationAdapter();
            var reader = new BinaryStorageBuffer.Reader(catalog.SerializeToByteArray(), 512, 0,
                new BinaryContentCatalogData.Serializer(), counter);
            m_CatalogReaders.Add(reader);
            var loaded = new BinaryContentCatalogData(reader);
            return loaded.CreateCustomLocator("", null) as BinaryContentCatalogData.ResourceLocator;
        }

        [Test]
        public void AllLocations_BuildsOnlyWhatIsEnumerated()
        {
            var locator = LoadCountingLocator(BuildMixedCatalog(200, 10), out var counter);

            // Loading a binary catalog reads keys, not locations.
            Assert.AreEqual(0, counter.Built);

            var first = locator.AllLocations.Take(1).ToList();

            // Asking for one location builds one.
            // the old implementation expanded every location into a set before it
            // returned anything.
            Assert.AreEqual(1, first.Count);
            Assert.AreEqual(1, counter.Built);
        }

        [Test]
        public void AllLocations_BuildsEachLocationOnce()
        {
            const int assetCount = 200;
            const int bundleCount = 10;
            var locator = LoadCountingLocator(BuildMixedCatalog(assetCount, bundleCount), out var counter);

            var scanned = AllLocationIds(locator);

            // One build per entry, however many keys name it
            Assert.AreEqual(assetCount + bundleCount + 1, scanned.Count);
            Assert.AreEqual(scanned.Count, counter.Built);
        }

        [Test]
        public void AllLocations_FilteredBySizeData_ListsEveryBundleOnce()
        {
            const int bundleCount = 10;
            var locator = LoadLocator(BuildMixedCatalog(200, bundleCount), out _);

            // The bundles are the only entries in this catalog that report a size.
            var expected = Ordered(Enumerable.Range(0, bundleCount)
                .Select(i => $"http://bundles.test/bundle{i}.bundle"));

            Assert.AreEqual(expected, SizeBearingIds(locator));
        }

        [Test]
        public void ResourceLocationMap_AllLocations_RepeatsALocationPerKey()
        {
            var map = new ResourceLocationMap("TestLocator");
            var bundle = new ResourceLocationBase("single.bundle", "http://bundles.test/single.bundle",
                "AssetBundleProvider", typeof(IAssetBundleResource));
            bundle.Data = new AssetBundleRequestOptions {BundleName = "single.bundle", BundleSize = 100};
            var asset = new ResourceLocationBase("asset", "Assets/asset.prefab",
                "BundledAssetProvider", typeof(GameObject));

            map.Add("single.bundle", bundle);
            map.Add("alias", bundle);
            map.Add("asset", asset);

            Assert.AreEqual(2, SizeBearingIds(map).Count);
            Assert.AreEqual(1, SizeBearingIds(map).Distinct().Count());
        }

        // A provider's own bundle resource type: the shape an exact-type filter misses.
        interface ITestBundleSubtype : IAssetBundleResource
        {
        }

        // Size data that is not AssetBundleRequestOptions, standing in for remote content
        // served by a custom provider.
        sealed class TestRemoteSizeData : ILocationSizeData
        {
            public long ComputeSize(IResourceLocation location,
                UnityEngine.ResourceManagement.ResourceManager resourceManager)
            {
                return 1;
            }
        }

        // Every catalog fixture types its bundles as IAssetBundleResource exactly, so this is
        // the only test that can tell the documented filter apart from a type comparison.
        [Test]
        public void SizeBearingFilter_KeepsSubtypedBundlesAndCustomSizeProviders()
        {
            var map = new ResourceLocationMap("TestLocator");

            // A bundle a custom provider types as its own IAssetBundleResource.
            var subtypedBundle = new ResourceLocationBase("subtyped.bundle",
                "http://bundles.test/subtyped.bundle", "AssetBundleProvider", typeof(ITestBundleSubtype));
            subtypedBundle.Data = new AssetBundleRequestOptions {BundleName = "subtyped.bundle", BundleSize = 100};

            // Remote content that is not a bundle but reports a size, which the size path counts.
            var customContent = new ResourceLocationBase("custom", "http://content.test/custom.dat",
                "CustomRemoteProvider", typeof(TextAsset));
            customContent.Data = new TestRemoteSizeData();

            // An asset location reports no size of its own.
            var asset = new ResourceLocationBase("asset", "Assets/asset.prefab",
                "BundledAssetProvider", typeof(GameObject));

            map.Add("subtyped.bundle", subtypedBundle);
            map.Add("custom", customContent);
            map.Add("asset", asset);

            Assert.AreEqual(
                Ordered(new[] {"http://bundles.test/subtyped.bundle", "http://content.test/custom.dat"}),
                SizeBearingIds(map));
        }

        [Test]
        public void AllLocations_CollapsesEntriesSharingInternalIdAndType()
        {
            var locator = LoadCountingLocator(BuildCatalogWithSharedInternalIds(), out var counter);

            var scanned = AllLocationIdsAndTypes(locator);

            // Read before the key resolution below, which builds locations of its own.
            var builtByScan = counter.Built;

            // Three locations out of four entries: the two that differ only in primary
            // key describe one location, so scanning records must not report both.
            Assert.AreEqual(3, scanned.Count);
            Assert.AreEqual(1, scanned.Count(s => s == $"{kDuplicatedId}|{nameof(GameObject)}"));

            // Only the resolved id shows the duplicate, so every record is built.
            Assert.AreEqual(4, builtByScan);

            // The answer resolving every key gave before the scan replaced it.
            Assert.AreEqual(KeyResolvedIdsAndTypes(locator), scanned);
        }

        [Test]
        public void AllLocations_KeepsEntriesSharingInternalIdWithDifferentTypes()
        {
            var locator = LoadLocator(BuildCatalogWithSharedInternalIds(), out _);

            var typesAtSharedId = locator.AllLocations
                .Where(l => l.InternalId == kMultiTypeId)
                .Select(l => l.ResourceType)
                .ToList();

            // A catalog holds one entry per serialized type at an asset path, so
            // collapsing on the id alone would throw one of them away.
            CollectionAssert.AreEquivalent(new[] {typeof(GameObject), typeof(TextAsset)}, typesAtSharedId);
        }

        [Test]
        public void AllLocations_CollapsesEntriesWhoseIdsResolveToOnePath()
        {
            var locator = LoadCountingLocator(BuildCatalogWithTokenizedAndLiteralInternalIds(), out var counter);

            var scanned = AllLocationIdsAndTypes(locator);

            // Read before the key resolution below, which builds locations of its own.
            var builtByScan = counter.Built;

            // A record stores the id as written, and the two records here are written
            // differently, so only the path they resolve to shows they are one location.
            var resolvedId = UnityEngine.AddressableAssets.Addressables.ResolveInternalId(kTokenizedBundleId);
            Assert.AreEqual(new List<string> {$"{resolvedId}|{nameof(IAssetBundleResource)}"}, scanned);

            // Recognising this duplicate takes the built location, not just its record.
            Assert.AreEqual(2, builtByScan);

            // The answer resolving every key gave before the scan replaced it.
            Assert.AreEqual(KeyResolvedIdsAndTypes(locator), scanned);
        }

        const string kAliasedId = "Assets/aliased.prefab";

        // One asset named three ways alongside a second asset, both under a shared label.
        // The two aliases give the first asset identical location sets.
        static BinaryContentCatalogData BuildCatalogWithAliasedKeys()
        {
            const string bundleKey = "aliased.bundle";
            var entries = new List<ContentCatalogDataEntry>
            {
                new ContentCatalogDataEntry(typeof(IAssetBundleResource),
                    "http://bundles.test/aliased.bundle", "AssetBundleProvider",
                    new object[] {bundleKey}, null,
                    new AssetBundleRequestOptions {BundleName = bundleKey, BundleSize = 100}),

                new ContentCatalogDataEntry(typeof(GameObject), kAliasedId,
                    "BundledAssetProvider",
                    new object[] {"aliasedAddress", "aliasedGuid", "sharedLabel"},
                    new List<object> {bundleKey}),

                new ContentCatalogDataEntry(typeof(GameObject), "Assets/other.prefab",
                    "BundledAssetProvider",
                    new object[] {"otherAddress", "sharedLabel"},
                    new List<object> {bundleKey}),
            };

            return CatalogFrom(entries);
        }

        // Every location BuildCatalogWithAliasedKeys holds, however many keys name it.
        static List<string> AliasedCatalogIds()
        {
            return Ordered(new[]
            {
                "http://bundles.test/aliased.bundle",
                kAliasedId,
                "Assets/other.prefab"
            });
        }

        // Maps each key to the offset of the location set it points at.
        static Dictionary<object, uint> ReadKeyLocationSetOffsets(BinaryStorageBuffer.Reader reader)
        {
            var header = reader.ReadValue<BinaryContentCatalogData.ResourceLocator.Header>(0, out _);
            var keyDataArray = reader.ReadValueArray<BinaryContentCatalogData.ResourceLocator.KeyData>(
                header.keysOffset, out _, false);

            var offsets = new Dictionary<object, uint>();
            foreach (var keyData in keyDataArray)
                offsets.Add(reader.ReadObject(keyData.keyNameOffset, out _), keyData.locationSetOffset);

            return offsets;
        }

        [Test]
        public void BinaryCatalog_KeysWithIdenticalLocationSets_ShareOneLocationSetOffset()
        {
            var reader = ReadCatalog(BuildCatalogWithAliasedKeys());

            var offsets = ReadKeyLocationSetOffsets(reader);

            // The writer hashes array content, so two keys naming one location share an offset.
            // AllLocations leans on this to skip an alias without reading its set.
            Assert.AreEqual(offsets["aliasedAddress"], offsets["aliasedGuid"]);

            // A key whose set differs keeps its own offset, so the skip cannot swallow it.
            Assert.AreNotEqual(offsets["aliasedAddress"], offsets["otherAddress"]);
            Assert.AreNotEqual(offsets["aliasedAddress"], offsets["sharedLabel"]);
        }

        [Test]
        public void AllLocations_AliasedKeys_ListsEachLocationOnce()
        {
            var locator = LoadLocator(BuildCatalogWithAliasedKeys(), out _);

            var scanned = AllLocationIds(locator);

            // Three entries, however many keys name them.
            Assert.AreEqual(AliasedCatalogIds(), scanned);

            // The shared label's set holds two locations, so skipping sets must not lose one.
            Assert.AreEqual(KeyResolvedLocationIds(locator), scanned);
        }

        // Points the second record of a location set past the end of the buffer, so reading
        // that set fails after the first record has already been collected.
        static byte[] WithCorruptSecondLocationInSet(byte[] data, uint locationSetOffset)
        {
            var corrupted = (byte[])data.Clone();
            Array.Copy(BitConverter.GetBytes((uint)(corrupted.Length - sizeof(uint))), 0,
                corrupted, (int)(locationSetOffset + sizeof(uint)), sizeof(uint));
            return corrupted;
        }

        [Test]
        public void Locate_AfterAFailedQuery_DoesNotLeakItsLocationsIntoTheNextQuery()
        {
            var data = BuildCatalogWithAliasedKeys().SerializeToByteArray();
            var setOffsets = ReadKeyLocationSetOffsets(ReaderOver(data));

            // sharedLabel names both assets, so the first is collected before the second fails.
            var catalog = new BinaryContentCatalogData(
                ReaderOver(WithCorruptSecondLocationInSet(data, setOffsets["sharedLabel"])));
            var locator = catalog.CreateCustomLocator("", null) as BinaryContentCatalogData.ResourceLocator;

            LogAssert.Expect(LogType.Exception, new Regex("Data offset .* out of bounds"));
            LogAssert.Expect(LogType.Exception, new Regex("NullReferenceException"));

            // Asking for a concrete type makes the unreadable record throw rather than be collected.
            Assert.IsFalse(locator.Locate("sharedLabel", typeof(GameObject), out _));

            // otherAddress names one asset and its own set is intact, so a leaked list shows up
            // here as the failed query's location arriving alongside it.
            Assert.IsTrue(locator.Locate("otherAddress", typeof(GameObject), out var locations));
            Assert.AreEqual(new[] {"Assets/other.prefab"}, locations.Select(l => l.InternalId).ToArray());
        }

        // Points one key's location set past the end of the buffer, so reading that set
        // throws instead of handing back records.
        byte[] WithCorruptLocationSetOffset(byte[] data, object key)
        {
            var reader = ReaderOver(data);
            var header = reader.ReadValue<BinaryContentCatalogData.ResourceLocator.Header>(0, out _);
            var keyDataArray = reader.ReadValueArray<BinaryContentCatalogData.ResourceLocator.KeyData>(
                header.keysOffset, out _, false);
            var index = Array.FindIndex(keyDataArray,
                k => Equals(reader.ReadObject(k.keyNameOffset, out _), key));

            // KeyData is two uints, and locationSetOffset is the second. One past the end
            // of the buffer, not uint.MaxValue, which the reader reads as an empty set.
            var corrupted = (byte[])data.Clone();
            Array.Copy(BitConverter.GetBytes((uint)(data.Length + sizeof(uint))), 0,
                corrupted, (int)header.keysOffset + index * 2 * sizeof(uint) + sizeof(uint), sizeof(uint));
            return corrupted;
        }

        [Test]
        public void AllLocations_UnreadableLocationSet_KeepsListingTheRest()
        {
            var data = BuildCatalogWithAliasedKeys().SerializeToByteArray();
            var catalog = new BinaryContentCatalogData(
                ReaderOver(WithCorruptLocationSetOffset(data, "sharedLabel")));
            var locator = catalog.CreateCustomLocator("", null);

            LogAssert.Expect(LogType.Exception, new Regex("Data offset .* out of bounds"));

            // Every location sharedLabel names is named by an intact key too, so skipping
            // its set loses nothing. The throw used to end the enumeration outright, which
            // left CleanBundleCacheOperation with no cache dirs rather than most of them.
            Assert.AreEqual(AliasedCatalogIds(), AllLocationIds(locator));
        }

        [Test]
        public void AllLocations_UnreadableRecordInASet_KeepsListingTheRest()
        {
            var data = BuildCatalogWithAliasedKeys().SerializeToByteArray();
            var setOffsets = ReadKeyLocationSetOffsets(ReaderOver(data));
            var catalog = new BinaryContentCatalogData(
                ReaderOver(WithCorruptSecondLocationInSet(data, setOffsets["sharedLabel"])));
            var locator = catalog.CreateCustomLocator("", null);

            LogAssert.Expect(LogType.Exception, new Regex("Data offset .* out of bounds"));

            // The reader logs a bad record and hands back null, so the scan drops that one
            // record and reads on. The remaining keys still name all three locations.
            Assert.AreEqual(AliasedCatalogIds(), AllLocationIds(locator));
        }

        [Test]
        public void BinaryCatalogSerializerWithInternalIdResolvingDisabled_DoesNotModifyInternalIds()
        {
            var internalId = "{UnityEngine.AddressableAssets.Addressables.RuntimePath}/file.path";
            var locType = typeof(UnityEngine.Object);
            var catalog = new BinaryContentCatalogData();
            var entries = new List<ContentCatalogDataEntry>();
            entries.Add(new ContentCatalogDataEntry(locType, internalId, "", new string[] { "a" }));
            catalog.SetData(entries);
            var data = catalog.SerializeToByteArray();
            {
                var resolvedCatalog = new BinaryContentCatalogData(new BinaryStorageBuffer.Reader(data, 128, 0, new BinaryContentCatalogData.Serializer()));
                var resolvedLocator = resolvedCatalog.CreateCustomLocator("", null) as BinaryContentCatalogData.ResourceLocator;
                resolvedLocator.Locate("a", locType, out var locs);
                Assert.AreEqual($"{UnityEngine.AddressableAssets.Addressables.RuntimePath}/file.path", locs[0].InternalId);
            }

            {
                var nonresolvedCatalog = new BinaryContentCatalogData(new BinaryStorageBuffer.Reader(data, 128, 0, new BinaryContentCatalogData.Serializer().WithInternalIdResolvingDisabled()));
                var nonresolvedLocator = nonresolvedCatalog.CreateCustomLocator("", null) as BinaryContentCatalogData.ResourceLocator;
                nonresolvedLocator.Locate("a", locType, out var locs);
                Assert.AreEqual(internalId, locs[0].InternalId);
            }

        }

        [Test]
        public void BinaryCatalogCacheStress([Values(1000)] int locateCallCount, [Values(1000)] int locCount, [Values(64, 256, 1024, 4096)] int bufferCacheSize)
        {
            var locType = typeof(UnityEngine.Object);
            var catalog = new BinaryContentCatalogData();
            var entries = new List<ContentCatalogDataEntry>();
            var allKeys = new List<object>();
            Func<int, string> internalIdFunc = i => $"https://mysuperlongwebservername.com/internalId/path/blah/subdir/urlstuffetc/{i}.fileextension";
            Func<int, object[]> keysFunc = i => new object[] { $"LongKeyName.{i}", $"LabelNameA.{i / 10}", $"LabelNameB.{i / 100}", "CommonLabelA", "CommonLabelB" };
            var providerId = "provider Id goes here";
            for (int i = 0; i < locCount; i++)
            {
                var entryKeys = keysFunc(i);
                entries.Add(new ContentCatalogDataEntry(
                    locType,
                    internalIdFunc(i),
                    providerId,
                    entryKeys,
                    null));
                allKeys.AddRange(entryKeys);
            }
            catalog.SetData(entries);
            var data = catalog.SerializeToByteArray();
            var loadedCatalog = new BinaryContentCatalogData(new BinaryStorageBuffer.Reader(data, bufferCacheSize, 0, new BinaryContentCatalogData.Serializer()));
            var locator = loadedCatalog.CreateCustomLocator("", null) as BinaryContentCatalogData.ResourceLocator;
            var sw = new Stopwatch();
            sw.Start();
            for (int i = 0; i < locateCallCount; i++)
            {
                var index = Random.Range(0, allKeys.Count);

                Assert.IsTrue(locator.Locate(allKeys[index], locType, out var locs));
                for (int j = 0; j < 10; j++)
                {
                    var l = locs[Random.Range(0, locs.Count)];
                    var locIndex = int.Parse(l.PrimaryKey.Substring(l.PrimaryKey.LastIndexOf('.')+1));
                    Assert.AreEqual(internalIdFunc(locIndex), l.InternalId);
                    Assert.AreEqual(keysFunc(locIndex)[0], l.PrimaryKey);
                    Assert.AreEqual(locType, l.ResourceType);
                    Assert.AreEqual(providerId, l.ProviderId);
                    Assert.AreEqual(-1, l.DependencyHashCode);
                }
            }
            sw.Stop();
        }

        // Builds a catalog where every asset entry shares one dependency set (a single bundle),
        // mirroring the UUM-148750 repro: many Addressables packed into the same bundle.
        // depSetCount > 1 produces that many distinct bundles, assets spread evenly across them.
        static BinaryContentCatalogData.ResourceLocator CreateSharedDependencyCatalog(
            int assetCount, int depsPerSet, out List<object> assetKeys, int depSetCount = 1, int bufferCacheSize = 1024)
        {
            var locType = typeof(UnityEngine.Object);
            var entries = new List<ContentCatalogDataEntry>();
            var depKeysPerSet = new List<List<object>>();

            for (int s = 0; s < depSetCount; s++)
            {
                var depKeys = new List<object>();
                for (int i = 0; i < depsPerSet; i++)
                {
                    var d = new ContentCatalogDataEntry(
                        typeof(AssetBundle),
                        $"internalId/set{s}/bundle{i}.bundle",
                        "AssetBundleProvider",
                        new object[] { $"set{s}_bundle{i}.bundle" },
                        null,
                        new AssetBundleRequestOptions { BundleName = $"set{s}_bundle{i}" });
                    entries.Add(d);
                    depKeys.Add(d.Keys[0]);
                }
                depKeysPerSet.Add(depKeys);
            }

            assetKeys = new List<object>();
            for (int i = 0; i < assetCount; i++)
            {
                var key = $"asset_{i}";
                entries.Add(new ContentCatalogDataEntry(
                    locType,
                    $"internalId/asset_{i}.asset",
                    "BundledAssetProvider",
                    new object[] { key },
                    depKeysPerSet[i % depSetCount]));
                assetKeys.Add(key);
            }

            var catalog = new BinaryContentCatalogData();
            catalog.SetData(entries);
            var data = catalog.SerializeToByteArray();
            var loaded = new BinaryContentCatalogData(new BinaryStorageBuffer.Reader(
                data, bufferCacheSize, 0, new BinaryContentCatalogData.Serializer()));
            return loaded.CreateCustomLocator("", null) as BinaryContentCatalogData.ResourceLocator;
        }

        static IResourceLocation LocateSingle(BinaryContentCatalogData.ResourceLocator locator, object key)
        {
            Assert.IsTrue(locator.Locate(key, typeof(UnityEngine.Object), out var locs), $"failed to locate {key}");
            Assert.AreEqual(1, locs.Count, $"expected exactly one location for {key}");
            return locs[0];
        }

        // UUM-148750: locations packed into the same bundle each rebuilt their own copy of the
        // identical dependency list. 400 locations produced 400 list instances; they should
        // share one.
        [Test]
        public void BinaryCatalog_LocationsSharingDependencySet_ShareDependencyListInstance()
        {
            const int assetCount = 400;
            var locator = CreateSharedDependencyCatalog(assetCount, 34, out var assetKeys);

            var distinct = new HashSet<IList<IResourceLocation>>(new ReferenceEqualityComparer());
            foreach (var key in assetKeys)
                distinct.Add(LocateSingle(locator, key).Dependencies);

            Assert.AreEqual(1, distinct.Count,
                $"expected 1 shared dependency list across {assetCount} locations, found {distinct.Count}");
        }

        [Test]
        public void BinaryCatalog_DistinctDependencySets_DoNotShareListInstance()
        {
            const int depSetCount = 4;
            var locator = CreateSharedDependencyCatalog(40, 6, out var assetKeys, depSetCount);

            var distinct = new HashSet<IList<IResourceLocation>>(new ReferenceEqualityComparer());
            foreach (var key in assetKeys)
                distinct.Add(LocateSingle(locator, key).Dependencies);

            Assert.AreEqual(depSetCount, distinct.Count,
                "each distinct dependency set should get its own list instance");
        }

        // Sharing must not disturb contents or ordering: DependenciesEqual compares index-wise.
        [Test]
        public void BinaryCatalog_DependencyContents_UnchangedWhenShared()
        {
            const int depsPerSet = 12;
            var locator = CreateSharedDependencyCatalog(10, depsPerSet, out var assetKeys);

            var expected = LocateSingle(locator, assetKeys[0]).Dependencies;
            Assert.AreEqual(depsPerSet, expected.Count);

            foreach (var key in assetKeys)
            {
                var deps = LocateSingle(locator, key).Dependencies;
                Assert.AreEqual(depsPerSet, deps.Count);
                for (int i = 0; i < deps.Count; i++)
                {
                    Assert.AreEqual(expected[i].InternalId, deps[i].InternalId, $"{key} dep {i} InternalId");
                    Assert.AreEqual(expected[i].PrimaryKey, deps[i].PrimaryKey, $"{key} dep {i} PrimaryKey");
                    Assert.AreEqual(expected[i].ProviderId, deps[i].ProviderId, $"{key} dep {i} ProviderId");
                    Assert.AreEqual(expected[i].ResourceType, deps[i].ResourceType, $"{key} dep {i} ResourceType");
                }
            }
        }

        // ReadObjectList returns null for the uint.MaxValue sentinel; the getter must still
        // hand back an empty list, as it did before the shared-list change.
        [Test]
        public void BinaryCatalog_LocationsWithoutDependencies_ReturnEmptyList()
        {
            var locType = typeof(UnityEngine.Object);
            var catalog = new BinaryContentCatalogData();
            catalog.SetData(new List<ContentCatalogDataEntry>
            {
                new ContentCatalogDataEntry(locType, "internalId/no_deps.asset", "BundledAssetProvider", new object[] { "no_deps" })
            });
            var loaded = new BinaryContentCatalogData(new BinaryStorageBuffer.Reader(
                catalog.SerializeToByteArray(), 128, 0, new BinaryContentCatalogData.Serializer()));
            var locator = loaded.CreateCustomLocator("", null) as BinaryContentCatalogData.ResourceLocator;

            var loc = LocateSingle(locator, "no_deps");
            Assert.IsFalse(loc.HasDependencies);
            Assert.IsNotNull(loc.Dependencies);
            Assert.AreEqual(0, loc.Dependencies.Count);
        }

        // The payoff: DependenciesEqual leads with ReferenceEquals, so sharing turns the
        // operation-cache probe back into a reference compare instead of walking every entry
        // and string-comparing it.
        [Test]
        public void BinaryCatalog_DependenciesEqual_UsesReferenceFastPath()
        {
            var locator = CreateSharedDependencyCatalog(2, 34, out var assetKeys);

            var a = LocateSingle(locator, assetKeys[0]);
            var b = LocateSingle(locator, assetKeys[1]);

            Assert.AreNotSame(a, b);
            Assert.AreSame(a.Dependencies, b.Dependencies);
            Assert.AreEqual(a.DependencyHashCode, b.DependencyHashCode);
            Assert.IsTrue(LocationUtils.DependenciesEqual(a.Dependencies, b.Dependencies));
        }

        // Guards the invariant itself: a refactor that reverts to per-location lists would
        // silently reintroduce UUM-148750 without this failing.
        [Test]
        public void BinaryCatalog_DependencyListIsSharedNotCopied()
        {
            var locator = CreateSharedDependencyCatalog(3, 5, out var assetKeys);

            var first = LocateSingle(locator, assetKeys[0]).Dependencies;
            var second = LocateSingle(locator, assetKeys[1]).Dependencies;
            var firstAgain = LocateSingle(locator, assetKeys[0]).Dependencies;

            Assert.AreSame(first, second, "locations sharing a dependency set must share the list");
            Assert.AreSame(first, firstAgain, "repeated access must not rebuild the list");
        }

        sealed class ReferenceEqualityComparer : IEqualityComparer<IList<IResourceLocation>>
        {
            public bool Equals(IList<IResourceLocation> x, IList<IResourceLocation> y) => ReferenceEquals(x, y);
            public int GetHashCode(IList<IResourceLocation> obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }

        [Test]
        public void AssetBundleRequestOptionsTest()
        {
            var options = new AssetBundleRequestOptions
            {
                ChunkedTransfer = true,
                Crc = 123,
                Hash = new Hash128(1, 2, 3, 4).ToString(),
                RedirectLimit = 4,
                RetryCount = 7,
                Timeout = 12,
                AssetLoadMode = AssetLoadMode.AllPackedAssetsAndDependencies,
                UseCrcForCachedBundle = true,
                ClearOtherCachedVersionsWhenLoaded = true,
                CacheProbeMode = CacheProbeMode.IsVersionCached
            };
            var dataEntry = new ContentCatalogDataEntry(typeof(ContentCatalogData), "internalId", "provider", new object[] { 1 }, null, options);
            var entries = new List<ContentCatalogDataEntry>();
            entries.Add(dataEntry);
            var ccData = new BinaryContentCatalogData("TestCatalog");
            ccData.SetData(entries);
            var data = ccData.SerializeToByteArray();
            ccData = new BinaryContentCatalogData(new BinaryStorageBuffer.Reader(data, 1024, 0, new BinaryContentCatalogData.Serializer()));
            var locator = ccData.CreateCustomLocator("");
            IList<IResourceLocation> locations;
            if (!locator.Locate(1, typeof(object), out locations))
                Assert.Fail("Unable to locate resource location");
            var loc = locations[0];
            var locOptions = loc.Data as AssetBundleRequestOptions;
            Assert.IsNotNull(locOptions);
            Assert.AreEqual(locOptions.ChunkedTransfer, options.ChunkedTransfer);
            Assert.AreEqual(locOptions.Crc, options.Crc);
            Assert.AreEqual(locOptions.Hash, options.Hash);
            Assert.AreEqual(locOptions.RedirectLimit, options.RedirectLimit);
            Assert.AreEqual(locOptions.RetryCount, options.RetryCount);
            Assert.AreEqual(locOptions.Timeout, options.Timeout);
            Assert.AreEqual(locOptions.AssetLoadMode, options.AssetLoadMode);
            Assert.AreEqual(locOptions.UseCrcForCachedBundle, options.UseCrcForCachedBundle);
            Assert.AreEqual(locOptions.ClearOtherCachedVersionsWhenLoaded, options.ClearOtherCachedVersionsWhenLoaded);
            Assert.AreEqual(locOptions.CacheProbeMode, options.CacheProbeMode);
        }

        // The copy constructor is used by content update to duplicate remote builtin entries,
        // so a field missing here is silently reset rather than reported.
        [Test]
        public void AssetBundleRequestOptions_CopyConstructor_CopiesEveryField()
        {
            var source = new AssetBundleRequestOptions
            {
                Hash = new Hash128(5, 6, 7, 8).ToString(),
                Crc = 456,
                Timeout = 21,
                ChunkedTransfer = true,
                RedirectLimit = 9,
                RetryCount = 3,
                BundleName = "bundle",
                AssetLoadMode = AssetLoadMode.AllPackedAssetsAndDependencies,
                BundleSize = 4096,
                UseCrcForCachedBundle = true,
                UseUnityWebRequestForLocalBundles = true,
                ClearOtherCachedVersionsWhenLoaded = true,
                CacheProbeMode = CacheProbeMode.IsVersionCached
            };

            var copy = new AssetBundleRequestOptions(source);

            Assert.AreEqual(source.Hash, copy.Hash);
            Assert.AreEqual(source.Crc, copy.Crc);
            Assert.AreEqual(source.Timeout, copy.Timeout);
            Assert.AreEqual(source.ChunkedTransfer, copy.ChunkedTransfer);
            Assert.AreEqual(source.RedirectLimit, copy.RedirectLimit);
            Assert.AreEqual(source.RetryCount, copy.RetryCount);
            Assert.AreEqual(source.BundleName, copy.BundleName);
            Assert.AreEqual(source.AssetLoadMode, copy.AssetLoadMode);
            Assert.AreEqual(source.BundleSize, copy.BundleSize);
            Assert.AreEqual(source.UseCrcForCachedBundle, copy.UseCrcForCachedBundle);
            Assert.AreEqual(source.UseUnityWebRequestForLocalBundles, copy.UseUnityWebRequestForLocalBundles);
            Assert.AreEqual(source.ClearOtherCachedVersionsWhenLoaded, copy.ClearOtherCachedVersionsWhenLoaded);
            Assert.AreEqual(source.CacheProbeMode, copy.CacheProbeMode);
        }

        // GetCacheStatus reads CacheProbeMode off the options, so it must reject null first.
        // Entries built with StripDownloadOptions carry no options at all.
        [Test]
        public void GetCacheStatus_WithNullOptions_ReturnsUnknown()
        {
            Assert.AreEqual(AssetBundleResource.CacheStatus.Unknown, AssetBundleResource.GetCacheStatus(null));
        }

        // Exposes BinaryContentCatalogData's protected header constants (as a subclass) so
        // tests below don't hardcode the live magic/version values.
        class TestableBinaryContentCatalogData : BinaryContentCatalogData
        {
            public const int LiveMagic = kMagic;
            public const int LiveVersion = kVersion;
        }

        static byte[] BuildMinimalSerializedCatalog()
        {
            var dataEntry = new ContentCatalogDataEntry(typeof(ContentCatalogData), "internalId", "provider", new object[] {1});
            var ccData = new BinaryContentCatalogData("TestCatalog");
            ccData.SetData(new List<ContentCatalogDataEntry> {dataEntry});
            return ccData.SerializeToByteArray();
        }

        [Test]
        public void BinaryCatalog_LogsExceptionAndReturnsNull_OnVersionMismatch()
        {
            // The public write path always stamps the current version, so simulate a catalog
            // written by an older package version by corrupting the header after serializing.
            // BinaryStorageBuffer.Reader.ReadObject<T> catches deserialization exceptions,
            // logs them via Debug.LogException, and returns default -- it does not rethrow.
            var data = BuildMinimalSerializedCatalog();
            var corruptedVersion = TestableBinaryContentCatalogData.LiveVersion + 1;
            Array.Copy(BitConverter.GetBytes(corruptedVersion), 0, data, 4, 4);

            var reader = new BinaryStorageBuffer.Reader(data, 1024, 0, new BinaryContentCatalogData.Serializer());
            LogAssert.Expect(LogType.Exception,
                $"Exception: Catalog data version mismatch: expected {TestableBinaryContentCatalogData.LiveVersion}, found {corruptedVersion}. Rebuild your Addressables content with the current package version.");
            var result = reader.ReadObject<BinaryContentCatalogData>(0, out _, false);
            Assert.IsNull(result);
        }

        [Test]
        public void BinaryCatalog_LogsExceptionAndReturnsNull_OnMagicMismatch()
        {
            var data = BuildMinimalSerializedCatalog();
            Array.Copy(BitConverter.GetBytes(TestableBinaryContentCatalogData.LiveMagic + 1), 0, data, 0, 4);

            var reader = new BinaryStorageBuffer.Reader(data, 1024, 0, new BinaryContentCatalogData.Serializer());
            LogAssert.Expect(LogType.Exception, "Exception: Invalid header data!!!");
            var result = reader.ReadObject<BinaryContentCatalogData>(0, out _, false);
            Assert.IsNull(result);
        }

        [Test]
        public void VerifySerialization()
        {
            var sw = Stopwatch.StartNew();
            sw.Start();
            var catalog = new JsonContentCatalogData();
            var entries = new List<ContentCatalogDataEntry>();
            var availableKeys = new List<object>();

            for (int i = 0; i < 1000; i++)
            {
                var internalId = "Assets/TestPath/" + GUID.Generate() + ".asset";
                var eKeys = GetRandomSubset(m_Keys, Random.Range(1, 5));
                object data;
                if (i % 2 == 0)
                    data = new EvenData {index = i, path = internalId};
                else
                    data = new OddData {index = i, path = internalId};

                var e = new ContentCatalogDataEntry(typeof(ContentCatalogData), internalId, m_Providers[Random.Range(0, m_Providers.Count)].FullName, eKeys,
                    GetRandomSubset(availableKeys, Random.Range(0, 1)), data);
                availableKeys.Add(eKeys[0]);
                entries.Add(e);
            }

            catalog.SetData(entries);
            sw.Stop();
            var t = sw.Elapsed.TotalMilliseconds;
            sw.Reset();
            sw.Start();
            var locMap = catalog.CreateLocator();
            sw.Stop();
            Debug.LogFormat("Create: {0}ms, Load: {1}ms", t, sw.Elapsed.TotalMilliseconds);

            foreach (var k in locMap.Locations)
            {
                foreach (var loc in k.Value)
                {
                    var entry = entries.Find(e => e.InternalId == loc.InternalId);
                    Assert.AreEqual(entry.Provider, loc.ProviderId);

                    var deps = loc.Dependencies;
                    if (deps != null)
                    {
                        foreach (var ed in entry.Dependencies)
                        {
                            IList<IResourceLocation> depList;
                            Assert.IsTrue(locMap.Locate(ed, typeof(object), out depList));
                            for (int i = 0; i < depList.Count; i++)
                                Assert.AreEqual(depList[i].InternalId, deps[i].InternalId);
                        }
                    }
                }
            }
        }

        [Test]
        public void VerifyDependencyHashCalculation()
        {
            var catalog = new JsonContentCatalogData();
            Dictionary<int, object> hashSources = new Dictionary<int, object>();

            var dummyValues = new List<object>()
            {
                "<WILL-BE-REPLACED>",
                "startup-shared_assets_assets/fx_data/textures.bundle",
                "shared_assets_assets/fx_data/materials.bundle",
                "shaders_assets_all.bundle",
                "music_assets_music/maptheme6final.bundle",
                "fx_tex_assets_all.bundle",
                "shared_assets_assets/textures/ui/campain_act02.bundle",
                "shared_assets_assets/fx_data/meshes.bundle",
                "startup-shared_assets_assets/textures/ui/campain_act02.bundle",
                "startup-shared_assets_assets/textures/ui/campainart.bundle",
                "startup-shared_assets_assets/fx_data/materials.bundle",
                "shared_assets_assets/textures/ui/valleyoftreasures.bundle",
                "startup-shared_assets_assets/fx_data/meshes.bundle",
                "startup_UnityBuiltInAssets.bundle"
            };

            dummyValues[0] = "maps_assets_ref/valley1.bundle";
            var hashPart1 = dummyValues[0].GetHashCode();
            var hashSum1 = catalog.CalculateCollectedHash(dummyValues, hashSources);

            var dummyValues2 = new List<object>()
            {
                "maps_assets_ref/valley1.bundle",
                "startup-shared_assets_assets/fx_data/textures.bundle",
                "shared_assets_assets/fx_data/materials.bundle",
                "shaders_assets_all.bundle",
                "music_assets_music/maptheme6final.bundle",
                "fx_tex_assets_all.bundle",
                "shared_assets_assets/textures/ui/campain_act02.bundle",
                "shared_assets_assets/fx_data/meshes.bundle",
                "startup-shared_assets_assets/textures/ui/campain_act02.bundle",
                "startup-shared_assets_assets/textures/ui/campainart.bundle",
                "startup-shared_assets_assets/fx_data/materials.bundle",
                "shared_assets_assets/textures/ui/valleyoftreasures.bundle",
                "startup-shared_assets_assets/fx_data/meshes.bundle",
                "startup_UnityBuiltInAssets.bundle"
            };

            var hashSum1DifferentList = catalog.CalculateCollectedHash(dummyValues2, hashSources);

            dummyValues[0] = "maps_assets_ref/valley3.bundle";
            var hashPart2 = dummyValues[0].GetHashCode();
            var hashSum2 = catalog.CalculateCollectedHash(dummyValues, hashSources);

            Assert.AreEqual(hashSum1, hashSum1DifferentList);
            Assert.AreNotEqual(hashPart1, hashPart2);
            Assert.AreNotEqual(hashSum1, hashSum2);
        }

        [Test]
        public void VerifyEnumerableHashCalculation()
        {
            var dummyValues = new List<object>()
            {
                "maps_assets_ref/valley1.bundle",
                "startup-shared_assets_assets/fx_data/textures.bundle",
                "shared_assets_assets/fx_data/materials.bundle",
                "shaders_assets_all.bundle",
                "music_assets_music/maptheme6final.bundle",
                "fx_tex_assets_all.bundle",
                "shared_assets_assets/textures/ui/campain_act02.bundle",
                "shared_assets_assets/fx_data/meshes.bundle",
                "startup-shared_assets_assets/textures/ui/campain_act02.bundle",
                "startup-shared_assets_assets/textures/ui/campainart.bundle",
                "startup-shared_assets_assets/fx_data/materials.bundle",
                "shared_assets_assets/textures/ui/valleyoftreasures.bundle",
                "startup-shared_assets_assets/fx_data/meshes.bundle",
                "startup_UnityBuiltInAssets.bundle"
            };

            var dummyValues2 = new List<object>()
            {
                "maps_assets_ref/valley1.bundle",
                "startup-shared_assets_assets/fx_data/textures.bundle",
                "shared_assets_assets/fx_data/materials.bundle",
                "shaders_assets_all.bundle",
                "music_assets_music/maptheme6final.bundle",
                "fx_tex_assets_all.bundle",
                "shared_assets_assets/textures/ui/campain_act02.bundle",
                "shared_assets_assets/fx_data/meshes.bundle",
                "startup-shared_assets_assets/textures/ui/campain_act02.bundle",
                "startup-shared_assets_assets/textures/ui/campainart.bundle",
                "startup-shared_assets_assets/fx_data/materials.bundle",
                "shared_assets_assets/textures/ui/valleyoftreasures.bundle",
                "startup-shared_assets_assets/fx_data/meshes.bundle",
                "startup_UnityBuiltInAssets.bundle"
            };

            var hash1 = JsonContentCatalogData.GetHashCodeForEnumerable(dummyValues);
            var hash2 = JsonContentCatalogData.GetHashCodeForEnumerable(dummyValues2);
            Assert.AreEqual(hash1, hash2);

            dummyValues[0] = "maps_assets_ref/valley3.bundle";
            var hash3 = JsonContentCatalogData.GetHashCodeForEnumerable(dummyValues);
            Assert.AreNotEqual(hash1, hash3);
        }

        [TestCase("0#b", "ab", new string[] {"a"})]
        [TestCase("1#b", "bb", new string[] {"a", "b"})]
        [TestCase("b", "b", new string[] {"a"})]
        [TestCase("b", "b", new string[] {})]
        [TestCase("b", "b", null)]
        [TestCase("x#b", "x#b", new string[] {"a"})]
        [Test]
        public void ContentCatalogData_ExpandInternalId_GeneratesExpectedResults(string input, string expected, string[] prefixes)
        {
            Assert.AreEqual(expected, JsonContentCatalogData.ExpandInternalId(prefixes, input));
        }

        [Test]
        public void SerializationUtility_ReadWrite_Int32()
        {
            var data = new byte[100];
            for (int i = 0; i < 1000; i++)
            {
                var val = Random.Range(int.MinValue, int.MaxValue);
                var off = Random.Range(0, data.Length - sizeof(int));
                Assert.AreEqual(off + sizeof(int), SerializationUtilities.WriteInt32ToByteArray(data, val, off));
                Assert.AreEqual(val, SerializationUtilities.ReadInt32FromByteArray(data, off));
            }
        }

        string testData =
            @"{""m_LocatorId"":""AddressablesMainContentCatalog"",""m_InstanceProviderData"":{""m_Id"":""UnityEngine.ResourceManagement.ResourceProviders.InstanceProvider"",""m_ObjectType"":{""m_AssemblyName"":""Unity.ResourceManager, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null"",""m_ClassName"":""UnityEngine.ResourceManagement.ResourceProviders.InstanceProvider""},""m_Data"":""""},""m_SceneProviderData"":{""m_Id"":""UnityEngine.ResourceManagement.ResourceProviders.SceneProvider"",""m_ObjectType"":{""m_AssemblyName"":""Unity.ResourceManager, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null"",""m_ClassName"":""UnityEngine.ResourceManagement.ResourceProviders.SceneProvider""},""m_Data"":""""},""m_ResourceProviderData"":[{""m_Id"":""UnityEngine.ResourceManagement.ResourceProviders.AssetBundleProvider"",""m_ObjectType"":{""m_AssemblyName"":""Unity.ResourceManager, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null"",""m_ClassName"":""UnityEngine.ResourceManagement.ResourceProviders.AssetBundleProvider""},""m_Data"":""""},{""m_Id"":""UnityEngine.ResourceManagement.ResourceProviders.BundledAssetProvider"",""m_ObjectType"":{""m_AssemblyName"":""Unity.ResourceManager, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null"",""m_ClassName"":""UnityEngine.ResourceManagement.ResourceProviders.BundledAssetProvider""},""m_Data"":""""},{""m_Id"":""UnityEngine.ResourceManagement.ResourceProviders.BundledAssetProvider"",""m_ObjectType"":{""m_AssemblyName"":""Unity.ResourceManager, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null"",""m_ClassName"":""UnityEngine.ResourceManagement.ResourceProviders.BundledAssetProvider""},""m_Data"":""""}],""m_ProviderIds"":[""UnityEngine.ResourceManagement.ResourceProviders.AssetBundleProvider"",""UnityEngine.ResourceManagement.ResourceProviders.BundledAssetProvider""],""m_InternalIds"":[""{UnityEngine.AddressableAssets.Addressables.RuntimePath}/StandaloneWindows64/defaultlocalgroup_assets_all_d4ed3973c342e6f06795a0f8daaebaad.bundle"",""{UnityEngine.AddressableAssets.Addressables.RuntimePath}/StandaloneWindows64/defaultlocalgroup_unitybuiltinassets_8f144cd21867dc83f60ecd3c93095b52.bundle"",""{UnityEngine.AddressableAssets.Addressables.RuntimePath}/StandaloneWindows64/defaultlocalgroup_scenes_all_e91ebe7804da861b4deb67a340282541.bundle"",""Assets/New Material.mat"",""Assets/swef.unity""],""m_KeyDataString"":""CQAAAABEAAAAZGVmYXVsdGxvY2FsZ3JvdXBfYXNzZXRzX2FsbF9kNGVkMzk3M2MzNDJlNmYwNjc5NWEwZjhkYWFlYmFhZC5idW5kbGUATQAAAGRlZmF1bHRsb2NhbGdyb3VwX3VuaXR5YnVpbHRpbnNoYWRlcnNfOGYxNDRjZDIxODY3ZGM4M2Y2MGVjZDNjOTMwOTViNTIuYnVuZGxlAEQAAABkZWZhdWx0bG9jYWxncm91cF9zY2VuZXNfYWxsX2U5MWViZTc4MDRkYTg2MWI0ZGViNjdhMzQwMjgyNTQxLmJ1bmRsZQAXAAAAQXNzZXRzL05ldyBNYXRlcmlhbC5tYXQAIAAAADNlN2JmNTA3OTRhNzEyMjQ2YWU0ZGNiZTdhODQyOGM4ABEAAABBc3NldHMvc3dlZi51bml0eQAgAAAAYjY4MDdmODNlMWU0ODc2NGM4MjMyM2ZkNTExZTY0NjgEKToMuAQiVa/u"",""m_BucketDataString"":""CQAAAAQAAAABAAAAAAAAAE0AAAABAAAAAQAAAJ8AAAABAAAAAgAAAOgAAAABAAAAAwAAAAQBAAABAAAAAwAAACkBAAABAAAABAAAAD8BAAABAAAABAAAAGQBAAACAAAAAAAAAAEAAABpAQAAAgAAAAIAAAABAAAA"",""m_EntryDataString"":""BQAAAAAAAAAAAAAA/////wAAAAAAAAAAAAAAAAAAAAABAAAAAAAAAP////8AAAAAhQIAAAEAAAAAAAAAAgAAAAAAAAD/////AAAAADQFAAACAAAAAAAAAAMAAAABAAAABwAAACk6DLj/////AwAAAAEAAAAEAAAAAQAAAAgAAAAiVa/u/////wUAAAACAAAA"",""m_ExtraDataString"":""B0xVbml0eS5SZXNvdXJjZU1hbmFnZXIsIFZlcnNpb249MC4wLjAuMCwgQ3VsdHVyZT1uZXV0cmFsLCBQdWJsaWNLZXlUb2tlbj1udWxsSlVuaXR5RW5naW5lLlJlc291cmNlTWFuYWdlbWVudC5SZXNvdXJjZVByb3ZpZGVycy5Bc3NldEJ1bmRsZVJlcXVlc3RPcHRpb25z6AEAAHsAIgBtAF8ASABhAHMAaAAiADoAIgBkADQAZQBkADMAOQA3ADMAYwAzADQAMgBlADYAZgAwADYANwA5ADUAYQAwAGYAOABkAGEAYQBlAGIAYQBhAGQAIgAsACIAbQBfAEMAcgBjACIAOgAyADAAMgAxADcANAA3ADAAOQA5ACwAIgBtAF8AVABpAG0AZQBvAHUAdAAiADoAMAAsACIAbQBfAEMAaAB1AG4AawBlAGQAVAByAGEAbgBzAGYAZQByACIAOgBmAGEAbABzAGUALAAiAG0AXwBSAGUAZABpAHIAZQBjAHQATABpAG0AaQB0ACIAOgAtADEALAAiAG0AXwBSAGUAdAByAHkAQwBvAHUAbgB0ACIAOgAwACwAIgBtAF8AQgB1AG4AZABsAGUATgBhAG0AZQAiADoAIgA5ADIAZAAwAGYAOABiAGMAOQBkAGYAZABjADAAMwBlADEAMABkAGYAMgBmADMAYgAzAGIANABjADgAMgA3AGUAIgAsACIAbQBfAEIAdQBuAGQAbABlAFMAaQB6AGUAIgA6ADIANQAyADgALAAiAG0AXwBVAHMAZQBDAHIAYwBGAG8AcgBDAGEAYwBoAGUAZABCAHUAbgBkAGwAZQBzACIAOgB0AHIAdQBlAH0AB0xVbml0eS5SZXNvdXJjZU1hbmFnZXIsIFZlcnNpb249MC4wLjAuMCwgQ3VsdHVyZT1uZXV0cmFsLCBQdWJsaWNLZXlUb2tlbj1udWxsSlVuaXR5RW5naW5lLlJlc291cmNlTWFuYWdlbWVudC5SZXNvdXJjZVByb3ZpZGVycy5Bc3NldEJ1bmRsZVJlcXVlc3RPcHRpb25zEgIAAHsAIgBtAF8ASABhAHMAaAAiADoAIgA4AGYAMQA0ADQAYwBkADIAMQA4ADYANwBkAGMAOAAzAGYANgAwAGUAYwBkADMAYwA5ADMAMAA5ADUAYgA1ADIAIgAsACIAbQBfAEMAcgBjACIAOgAzADgAMQAzADcAMgA0ADgANQA5ACwAIgBtAF8AVABpAG0AZQBvAHUAdAAiADoAMAAsACIAbQBfAEMAaAB1AG4AawBlAGQAVAByAGEAbgBzAGYAZQByACIAOgBmAGEAbABzAGUALAAiAG0AXwBSAGUAZABpAHIAZQBjAHQATABpAG0AaQB0ACIAOgAtADEALAAiAG0AXwBSAGUAdAByAHkAQwBvAHUAbgB0ACIAOgAwACwAIgBtAF8AQgB1AG4AZABsAGUATgBhAG0AZQAiADoAIgBmAGMAOAAyAGEAMAAxAGUAYgAwAGEAMgA0AGIAOQBiAGQAOQBjADAAZQBjADEAZAAzAGEAOQBiADIANgA1ADUAXwB1AG4AaQB0AHkAYgB1AGkAbAB0AGkAbgBzAGgAYQBkAGUAcgBzACIALAAiAG0AXwBCAHUAbgBkAGwAZQBTAGkAegBlACIAOgA0ADQANAA1ADQALAAiAG0AXwBVAHMAZQBDAHIAYwBGAG8AcgBDAGEAYwBoAGUAZABCAHUAbgBkAGwAZQBzACIAOgB0AHIAdQBlAH0AB0xVbml0eS5SZXNvdXJjZU1hbmFnZXIsIFZlcnNpb249MC4wLjAuMCwgQ3VsdHVyZT1uZXV0cmFsLCBQdWJsaWNLZXlUb2tlbj1udWxsSlVuaXR5RW5naW5lLlJlc291cmNlTWFuYWdlbWVudC5SZXNvdXJjZVByb3ZpZGVycy5Bc3NldEJ1bmRsZVJlcXVlc3RPcHRpb25z6AEAAHsAIgBtAF8ASABhAHMAaAAiADoAIgBlADkAMQBlAGIAZQA3ADgAMAA0AGQAYQA4ADYAMQBiADQAZABlAGIANgA3AGEAMwA0ADAAMgA4ADIANQA0ADEAIgAsACIAbQBfAEMAcgBjACIAOgAzADQAMAA1ADQAMwA2ADQANQAxACwAIgBtAF8AVABpAG0AZQBvAHUAdAAiADoAMAAsACIAbQBfAEMAaAB1AG4AawBlAGQAVAByAGEAbgBzAGYAZQByACIAOgBmAGEAbABzAGUALAAiAG0AXwBSAGUAZABpAHIAZQBjAHQATABpAG0AaQB0ACIAOgAtADEALAAiAG0AXwBSAGUAdAByAHkAQwBvAHUAbgB0ACIAOgAwACwAIgBtAF8AQgB1AG4AZABsAGUATgBhAG0AZQAiADoAIgA5ADEANwBlADUANQAzAGQAZQBiAGQAOAAyADMAOABkAGMAMgBjADIAZAA2ADIANQBkADAAZgA4ADUAOQA0AGMAIgAsACIAbQBfAEIAdQBuAGQAbABlAFMAaQB6AGUAIgA6ADgANwA4ADIALAAiAG0AXwBVAHMAZQBDAHIAYwBGAG8AcgBDAGEAYwBoAGUAZABCAHUAbgBkAGwAZQBzACIAOgB0AHIAdQBlAH0A"",""m_Keys"":[""defaultlocalgroup_assets_all_d4ed3973c342e6f06795a0f8daaebaad.bundle"",""defaultlocalgroup_unitybuiltinassets_8f144cd21867dc83f60ecd3c93095b52.bundle"",""defaultlocalgroup_scenes_all_e91ebe7804da861b4deb67a340282541.bundle"",""Assets/New Material.mat"",""3e7bf50794a712246ae4dcbe7a8428c8"",""Assets/swef.unity"",""b6807f83e1e48764c82323fd511e6468"",""-1207158231"",""-290499294""],""m_resourceTypes"":[{""m_AssemblyName"":""Unity.ResourceManager, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null"",""m_ClassName"":""UnityEngine.ResourceManagement.ResourceProviders.IAssetBundleResource""},{""m_AssemblyName"":""UnityEngine.CoreModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null"",""m_ClassName"":""UnityEngine.Material""},{""m_AssemblyName"":""Unity.ResourceManager, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null"",""m_ClassName"":""UnityEngine.ResourceManagement.ResourceProviders.SceneInstance""}]}";

        [Test]
        public void CanLoad_OldCatalogFormat()
        {
            var ccd = JsonUtility.FromJson<JsonContentCatalogData>(testData);
            Assert.IsNotNull(ccd);
            var loc = ccd.CreateLocator();
            Assert.IsNotNull(loc);
            Assert.AreEqual(9, loc.Keys.Count());
            foreach (var k in loc.Keys)
            {
                Assert.IsTrue(loc.Locate(k, null, out var res));
                Assert.IsNotEmpty(res[0].PrimaryKey);
                Assert.IsNotEmpty(res[0].InternalId);
                Assert.IsNotEmpty(res[0].ProviderId);
                Assert.IsNotNull(res[0].ResourceType);
            }
        }

        // JSON-format counterparts to the cross-runtime TypeNameResolver coverage in
        // BinaryStorageBufferTests.cs. These drive real JsonUtility.ToJson/FromJson round-trips
        // (not just in-memory SetData/CreateLocator) to prove JsonContentCatalogData shares the
        // same runtime-portable type resolution as the binary catalog format.

        [Test]
        public void JsonCatalog_ResolvesType_WhenAssemblyNotFound()
        {
            // Simulate a catalog written by a different runtime: the assembly name on disk
            // can't be loaded here, but the corelib class name alone is enough to resolve.
            var catalog = new JsonContentCatalogData();
            var entry = new ContentCatalogDataEntry(typeof(string), "Assets/foo.asset", "SomeProvider", new object[] {"key"});
            catalog.SetData(new List<ContentCatalogDataEntry> {entry});

            var json = JsonUtility.ToJson(catalog);
            Assert.IsTrue(json.Contains("\"m_ClassName\":\"System.String\""), "test JSON missing expected resource type entry");
            json = json.Replace("\"m_AssemblyName\":\"\",\"m_ClassName\":\"System.String\"",
                "\"m_AssemblyName\":\"NonExistentAssembly.ForTesting\",\"m_ClassName\":\"System.String\"");

            var loaded = JsonUtility.FromJson<JsonContentCatalogData>(json);
            var loc = loaded.CreateLocator();
            Assert.IsTrue(loc.Locate("key", null, out var res));
            Assert.AreEqual(typeof(string), res[0].ResourceType);
        }

        [Test]
        public void JsonCatalog_RoundTrip_NonCore_StripsVersionInfo()
        {
            var catalog = new JsonContentCatalogData();
            var entry = new ContentCatalogDataEntry(typeof(Vector3), "Assets/foo.asset", "SomeProvider", new object[] {"key"});
            catalog.SetData(new List<ContentCatalogDataEntry> {entry});

            var json = JsonUtility.ToJson(catalog);
            Assert.IsFalse(json.Contains("Version="), "version info must be stripped");
            Assert.IsFalse(json.Contains("PublicKeyToken="), "public key token must be stripped");
            Assert.IsTrue(json.Contains("\"m_AssemblyName\":\"UnityEngine.CoreModule\""), "non-corelib assembly should be the simple name only");

            var loaded = JsonUtility.FromJson<JsonContentCatalogData>(json);
            var loc = loaded.CreateLocator();
            Assert.IsTrue(loc.Locate("key", null, out var res));
            Assert.AreEqual(typeof(Vector3), res[0].ResourceType);
        }

        [Test]
        public void JsonCatalog_RoundTrip_Corelib_UsesNullAssemblySentinel()
        {
            var catalog = new JsonContentCatalogData();
            var entry = new ContentCatalogDataEntry(typeof(string), "Assets/foo.asset", "SomeProvider", new object[] {"key"});
            catalog.SetData(new List<ContentCatalogDataEntry> {entry});

            var json = JsonUtility.ToJson(catalog);
            Assert.IsTrue(json.Contains("\"m_AssemblyName\":\"\",\"m_ClassName\":\"System.String\""),
                "corelib assembly should be encoded as the empty/null sentinel");

            var loaded = JsonUtility.FromJson<JsonContentCatalogData>(json);
            var loc = loaded.CreateLocator();
            Assert.IsTrue(loc.Locate("key", null, out var res));
            Assert.AreEqual(typeof(string), res[0].ResourceType);
        }
    }
}
