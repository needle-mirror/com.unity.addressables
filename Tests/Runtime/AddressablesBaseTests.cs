using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.IO;
using UnityEngine.ResourceManagement;
using UnityEngine.AddressableAssets;
using System;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

public abstract class AddressablesBaseTests : IPrebuildSetup, IPostBuildCleanup
{
    protected string RootFolder { get { return string.Format("Assets/{0}_AssetsToDelete", GetType().Name); } }
    public void Setup()
    {
        if (!Directory.Exists(RootFolder))
            Directory.CreateDirectory(RootFolder);
    }

    [OneTimeTearDown]
    public void Cleanup()
    {
        AssetDatabase.DeleteAsset(RootFolder);
    }
    Dictionary<object, int> keysHashSet = new Dictionary<object, int>();
    List<object> keysList = new List<object>();

    protected void CreateAsset(string assetPath, string objectName)
    {
        if (!Directory.Exists(Path.GetDirectoryName(assetPath)))
            Directory.CreateDirectory(Path.GetDirectoryName(assetPath));

        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        PrefabUtility.CreatePrefab(assetPath, go);
        go.name = objectName;
        UnityEngine.Object.Destroy(go);
    }

    protected void AddLocation(ResourceLocationMap locations, string assetPrefix, string objectName, string loadPath, System.Type provider, params object[] keys)
    {
        CreateAsset(RootFolder + "/" + assetPrefix + objectName + ".prefab", objectName);
        AddLocation(locations, new ResourceLocationBase(objectName, loadPath, provider.FullName), keys);
    }

    protected void AddLocation(ResourceLocationMap locations, IResourceLocation loc, IEnumerable<object> keys)
    {
        foreach (var key in keys)
        {
            if (!keysHashSet.ContainsKey(key))
            {
                keysList.Add(key);
                keysHashSet.Add(key, 0);
            }
            keysHashSet[key] = keysHashSet[key] + 1;
            locations.Add(key, loc);
        }
    }

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        ResourceManager.ResourceProviders.Clear();
        ResourceManager.InstanceProvider = null;
        ResourceManager.SceneProvider = null;
        AsyncOperationCache.Instance.Clear();
        DelayedActionManager.Clear();

        AssetDatabase.StartAssetEditing();
        var locations = new ResourceLocationMap(100);
        CreateLocations(locations);
        AssetDatabase.StopAssetEditing();

        Addressables.ResourceLocators.Add(locations);
    }

    protected abstract void CreateLocations(ResourceLocationMap locations);

    [OneTimeTearDown]
    public void TearDown()
    {
        AssetDatabase.DeleteAsset(RootFolder);
    }

    [UnityTest]
    public IEnumerator CanGetResourceLocationsWithSingleKey()
    {
        foreach (var k in keysHashSet)
        {
            Addressables.LoadAssets<IResourceLocation>(k.Key, (op1) => Assert.IsNotNull(op1.Result)).Completed += (op) =>
            {
                Assert.IsNotNull(op.Result);
                Assert.AreEqual(k.Value, op.Result.Count);
            };
            yield return null;
        }
    }
     
    [UnityTest]
    public IEnumerator CanGetResourceLocationsWithMultipleKeysMerged([Values(Addressables.MergeMode.None, Addressables.MergeMode.Intersection, Addressables.MergeMode.Union)]Addressables.MergeMode mode)
    {
        for (int i = 0; i < 50; i++)
        {
            HashSet<IResourceLocation> set1 = new HashSet<IResourceLocation>();
            HashSet<IResourceLocation> set2 = new HashSet<IResourceLocation>();
            var key1 = keysList[UnityEngine.Random.Range(0, keysList.Count/2)];
            var key2 = keysList[UnityEngine.Random.Range(keysList.Count / 2, keysList.Count)];
            var op1 = Addressables.LoadAssets<IResourceLocation>(key1, (op) => set1.Add(op.Result));
            var op2 = Addressables.LoadAssets<IResourceLocation>(key2, (op) => set2.Add(op.Result));
            yield return op1;
            yield return op2;
            List<object> keys = new List<object>();
            keys.Add(key1);
            keys.Add(key2);
            var op3 = Addressables.LoadAssets<IResourceLocation>(keys, (op) => Assert.IsNotNull(op.Result), mode);
            yield return op3;
            Assert.NotNull(op3.Result);
            switch (mode)
            {
                case Addressables.MergeMode.None:
                    break;
                case Addressables.MergeMode.Intersection:
                    set1.IntersectWith(set2);
                    break;
                case Addressables.MergeMode.Union:
                    set1.UnionWith(set2);
                    break;
            }
            Assert.AreEqual(op3.Result.Count, set1.Count);
            var res = new List<IResourceLocation>(set1);
            for (int r = 0; r < res.Count; r++)
                Assert.AreSame(res[r], op3.Result[r]);
        }
        yield return null;
    }
    [UnityTest]
    public IEnumerator CanDestroyNonAddressable()
    {
        GameObject go = GameObject.Instantiate(GameObject.CreatePrimitive(PrimitiveType.Cube));
        go.name = "TestCube";

        Addressables.ReleaseInstance(go); 
        yield return null;

        GameObject foundObj = GameObject.Find("TestCube");
        Assert.IsNull(foundObj);
        yield return null;
    }

    [UnityTest]
    public IEnumerator CanLoadAssetWithCallback()
    {
        int loaded = 0;
        var assets = new List<object>();
        foreach (var key in keysList)
            Addressables.LoadAsset<object>(key).Completed += (op) => { loaded++; Assert.IsNotNull(op.Result); assets.Add(op.Result); };

        while (loaded < keysList.Count)
            yield return null;
        foreach (var a in assets)
            Addressables.ReleaseAsset(a);
    }

    [UnityTest]
    public IEnumerator CanLoadAssetsWithCallback()
    {
        int loaded = 0;
        var assets = new List<object>();
        foreach (var key in keysList)
        {
            Addressables.LoadAssets<object>(key, (a)=> { Assert.IsNotNull(a.Result); assets.Add(a.Result); }).Completed += (op) =>
            {
                loaded++;
                Assert.IsNotNull(op.Result);
                foreach (var a in op.Result)
                    Assert.IsNotNull(a);
            };
        }
        while (loaded < keysList.Count)
            yield return null;
        foreach (var a in assets)
            Addressables.ReleaseAsset(a);
    }


    [UnityTest]
    public IEnumerator CanLoadAssetsWithMultipleKeysMerged([Values(Addressables.MergeMode.None, Addressables.MergeMode.Intersection, Addressables.MergeMode.Union)]Addressables.MergeMode mode)
    {
        int loaded = 0;
        var assets = new List<UnityEngine.Object>();
        for (int i = 0; i < 50; i++)
        {
            List<object> keys = new List<object>(new object[] { keysList[UnityEngine.Random.Range(0, keysList.Count / 2)], keysList[UnityEngine.Random.Range(keysList.Count / 2, keysList.Count)] });
            var op3 = Addressables.LoadAssets<UnityEngine.Object>(keys, (op) => {Assert.IsNotNull(op.Result); assets.Add(op.Result); }, mode);
            yield return op3;
            Assert.NotNull(op3.Result);
            Addressables.LoadAssets<IResourceLocation>(keys, (op) => Assert.IsNotNull(op.Result), mode).Completed += (checkOp) =>
            {
                loaded++;
                Assert.AreEqual(op3.Result.Count, checkOp.Result.Count);
            };
        }
        while (loaded < keysList.Count)
            yield return null;
        foreach (var a in assets)
            Addressables.ReleaseAsset(a);
    }

    [UnityTest]
    public IEnumerator CanLoadPreloadDependenciesForSingleKey()
    {
        int loaded = 0;
        var assets = new List<object>();
        foreach (var key in keysList)
        {
            Addressables.PreloadDependencies(key, (c) => Assert.IsNotNull(c.Result)).Completed += (op) =>
              {
                  loaded++;
                  Assert.IsNotNull(op.Result);
                  foreach (var d in op.Result)
                  {
                      Assert.IsNotNull(d);
                      assets.Add(d);
                  }
              };
        }

        while (loaded < keysList.Count)
            yield return null;
    }

    [UnityTest]
    public IEnumerator CanLoadPreloadDependenciesForMutlipleKeys([Values(Addressables.MergeMode.None, Addressables.MergeMode.Intersection, Addressables.MergeMode.Union)]Addressables.MergeMode mode)
    {
        for (int i = 0; i < 50; i++)
        {
            List<object> keys = new List<object>(new object[] { keysList[UnityEngine.Random.Range(0, keysList.Count / 2)], keysList[UnityEngine.Random.Range(keysList.Count / 2, keysList.Count)] });
            var op3 = Addressables.PreloadDependencies(keys, (op) => { Assert.IsNotNull(op.Result); }, mode);
            yield return op3;
            Assert.NotNull(op3.Result);
            foreach (var d in op3.Result)
                Assert.IsNotNull(d);
        }
    }


    [UnityTest]
    public IEnumerator StressInstantiation()
    {
        for (int i = 0; i < 100; i++)
        {
            var key = keysList[UnityEngine.Random.Range(0, keysList.Count)];
            Addressables.Instantiate<GameObject>(key, new InstantiationParameters(null, true)).Completed += (op) =>
            {
                Assert.IsNotNull(op.Result);
                DelayedActionManager.AddAction((Action<UnityEngine.Object, float>)Addressables.ReleaseInstance, UnityEngine.Random.Range(.25f, .5f), op.Result, 0);
            };

            if (UnityEngine.Random.Range(0, 100) > 20)
                yield return null;
        }

        while (DelayedActionManager.IsActive)
            yield return null;

        var objs = SceneManager.GetActiveScene().GetRootGameObjects();
        foreach (var r in objs)
            Assert.False(r.name.EndsWith("(Clone)"), "All instances were not cleaned up");
    }
}
