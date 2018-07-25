using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine.AddressableAssets;
using System.Collections.Generic;
using UnityEngine.ResourceManagement;
using System.Linq;

public class ContentCatalogTests
{
    List<object> keys;
    List<System.Type> providers;

    [System.Serializable]
    public class SerializableKey
    {
        public int index;
        public string path;
    }

    [OneTimeSetUp]
    public void Init()
    {
        keys = new List<object>();
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        for (int i = 0; i < 1000; i++)
        {
            var r = Random.Range(0, 100);
            if (r < 20)
            {
                int len = Random.Range(1, 5);
                for (int j = 0; j < len; j++)
                    sb.Append(GUID.Generate().ToString());
                keys.Add(sb.ToString());
                sb.Length = 0;
            }
            else if (r < 40)
            {
                keys.Add((ushort)(i * 13));
            }
            else if (r < 50)
            {
                keys.Add((int)(i * 13));
            }
            else if (r < 60)
            {
                keys.Add((uint)(i * 13));
            }
            else if (r < 80)
            {
                keys.Add(new SerializableKey() { index = i, path = GUID.Generate().ToString() });
            }
            else
            {
                keys.Add(Hash128.Parse(GUID.Generate().ToString()));
            }
        }
        providers = new List<System.Type>();
        providers.Add(typeof(BundledAssetProvider));
        providers.Add(typeof(AssetBundleProvider));
        providers.Add(typeof(AssetDatabaseProvider));
        providers.Add(typeof(LegacyResourcesProvider));
        providers.Add(typeof(JsonAssetProvider));
        providers.Add(typeof(RawDataProvider));
        providers.Add(typeof(TextDataProvider));
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

    [System.Serializable]
    public class EvenData
    {
        public int index;
        public string path;
    }

    [System.Serializable]
    public class OddData
    {
        public int index;
        public string path;
    }

    [Test]
    public void VerifySerialization()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        sw.Start();
        var catalog = new ContentCatalogData();
        var entries = new List<ContentCatalogDataEntry>();
        var availableKeys = new List<object>();

        for (int i = 0; i < 1000; i++)
        {
            var internalId = "Assets/TestPath/" + GUID.Generate().ToString() + ".asset";
            var eKeys = GetRandomSubset(keys, Random.Range(1, 5));
            object data = null;
            if (i % 2 == 0)
                data = new EvenData() { index = i, path = internalId };
            else
                data = new OddData() { index = i, path = internalId };

            var e = new ContentCatalogDataEntry(internalId, providers[Random.Range(0, providers.Count)].FullName, eKeys, GetRandomSubset(availableKeys, Random.Range(0, 1)), data);
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
                Assert.AreEqual(entry.Provider.ToString(), loc.ProviderId);
                
                var deps = loc.Dependencies;
                if (deps != null)
                {
                    foreach (var ed in entry.Dependencies)
                    {
                        IList<IResourceLocation> depList;
                        Assert.IsTrue(locMap.Locate(ed, out depList));
                        for (int i = 0; i < depList.Count; i++)
                            Assert.AreEqual(depList[i].InternalId, deps[i].InternalId);
                    }
                }
            }
        }
    }
}
