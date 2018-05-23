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
            else if (r < 60)
            {
                keys.Add((int)(i * 13));
            }
            else if (r < 80)
            {
                keys.Add((uint)(i * 13));
            }
            else
            {
                keys.Add(Hash128.Parse(GUID.Generate().ToString()));
            }
        }
        providers = new List<System.Type>();
        providers.Add(typeof(BundledAssetProvider));
        providers.Add(typeof(RemoteAssetBundleProvider));
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

    [Test]
    public void VerifySerialization()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        sw.Start();
        var catalog = new ContentCatalogData();
        var entries = new List<ContentCatalogData.DataEntry>();
        var availableKeys = new List<object>();

        for (int i = 0; i < 1000; i++)
        {
            var eKeys = GetRandomSubset(keys, Random.Range(1, 5));
            var e = new ContentCatalogData.DataEntry("Assets/TestPath/" + GUID.Generate().ToString() + ".asset", providers[Random.Range(0, providers.Count)].FullName, eKeys, GetRandomSubset(availableKeys, Random.Range(0, 1)));
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

        foreach (var k in locMap.m_locations)
        {
            foreach (var loc in k.Value)
            {
                var entry = entries.Find(e => e.m_internalId == loc.InternalId);
                Assert.AreEqual(entry.m_provider.ToString(), loc.ProviderId);
                var deps = loc.Dependencies;
                if (deps != null)
                {
                    foreach (var ed in entry.m_dependencies)
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
