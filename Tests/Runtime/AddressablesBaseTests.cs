using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

public abstract class AddressablesBaseTests : IPrebuildSetup//, IPostBuildCleanup
{
    protected string RootFolder { get { return string.Format("Assets/{0}_AssetsToDelete", GetType().Name); } }
#if UNITY_EDITOR
    private List<EditorBuildSettingsScene> scenes = null;
#endif

    public void Setup()
    {
#if UNITY_EDITOR
        AssetDatabase.Refresh();
        var sceneRoot = RootFolder;
        if (!Directory.Exists(sceneRoot))
            Directory.CreateDirectory(sceneRoot);

        var scenePath = sceneRoot + "/test_scene.unity";
        //SceneManagerState.Record(RootFolder + "/scenes.json");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        EditorSceneManager.SaveScene(scene, scenePath);
        EditorSceneManager.CloseScene(scene, false);

        scenes = new List<EditorBuildSettingsScene>();
        foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
        {
            if(!String.IsNullOrEmpty(s.path))
                scenes.Add(s);
        }

        foreach (var s in scenes)
            if (s.path == scenePath)
                return;

        EditorBuildSettingsScene sceneToAdd = new EditorBuildSettingsScene(scenePath, true);
        scenes.Add(sceneToAdd);
        EditorBuildSettings.scenes = scenes.ToArray();
#endif
    }

    [OneTimeTearDown]
    public void DeleteTempFiles()
    {
#if UNITY_EDITOR
        //SceneManagerState.Restore(RootFolder + "/scenes.json");
        AssetDatabase.DeleteAsset(RootFolder);
#endif
    }

    Dictionary<object, int> m_KeysHashSet = new Dictionary<object, int>();
    List<object> m_KeysList = new List<object>();

    protected void CreateAsset(string assetPath, string objectName)
    {
#if UNITY_EDITOR
        if (!Directory.Exists(Path.GetDirectoryName(assetPath)))
            Directory.CreateDirectory(Path.GetDirectoryName(assetPath));

        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
#if UNITY_2018_3_OR_NEWER
        PrefabUtility.SaveAsPrefabAsset(go, assetPath);
#else
        PrefabUtility.CreatePrefab(assetPath, go);
#endif
        go.name = objectName;
        Object.DestroyImmediate(go, false);
#endif
    }

    protected void AddLocation(ResourceLocationMap locations, string assetPrefix, string objectName, string loadPath, Type provider, params object[] keys)
    {
        CreateAsset(RootFolder + "/" + assetPrefix + objectName + ".prefab", objectName);
        AddLocation(locations, new ResourceLocationBase(objectName, loadPath, provider.FullName), keys);
    }

    protected void AddLocation(ResourceLocationMap locations, IResourceLocation loc, IEnumerable<object> keys)
    {
        foreach (var key in keys)
        {
            if (!m_KeysHashSet.ContainsKey(key))
            {
                m_KeysList.Add(key);
                m_KeysHashSet.Add(key, 0);
            }
            m_KeysHashSet[key] = m_KeysHashSet[key] + 1;
            locations.Add(key, loc);
        }
    }

    //we must wait for Addressables initialization to complete since we are clearing out all of its data for the tests.
    public bool initializationComplete;
    IEnumerator Init()
    {
        if (!initializationComplete)
        {
            while (!Addressables.InitializationOperation.IsDone)
                yield return null;

            ResourceManager.ResourceProviders.Clear();
            ResourceManager.InstanceProvider = null;
            ResourceManager.SceneProvider = new SceneProvider();
            AsyncOperationCache.Instance.Clear();
            DelayedActionManager.Clear();

#if UNITY_EDITOR
            AssetDatabase.StartAssetEditing();
#endif
            var locations = new ResourceLocationMap(100);
            CreateLocations(locations);
#if UNITY_EDITOR
            AssetDatabase.StopAssetEditing();
#endif
            var sceneLoc = new ResourceLocationBase("testscene", RootFolder + "/test_scene.unity", typeof(SceneProvider).FullName);
            locations.Add("testscene", sceneLoc);
            Addressables.ResourceLocators.Add(locations);

            initializationComplete = true;
            Debug.Log("Initialization Complete");
        }
    }

    protected abstract void CreateLocations(ResourceLocationMap locations);

    [UnityTest]
    public IEnumerator CanLoadScene()
    {
        yield return Init();
        var loadOp = Addressables.LoadScene("testscene", LoadSceneMode.Additive);
        while (!loadOp.IsDone)
            yield return null;

        Assert.IsTrue(loadOp.Result.isLoaded, "Scene isn't loaded");

        var unloadOp = Addressables.UnloadScene(loadOp.Result);
        while (!unloadOp.IsDone)
            yield return null;

        Assert.IsFalse(unloadOp.Result.isLoaded, "Scene wasn't unloaded"); yield return null;
    }

#if UNITY_EDITOR
    [UnityTest]
    public IEnumerator AssetReferenceCanLoadAndUnloadAssetTest()
    {
        yield return Init();
        IList<IResourceLocation> locs;
        Addressables.ResourceLocators[0].Locate(m_KeysList[0], out locs);
        var guidString = AssetDatabase.AssetPathToGUID(locs[0].InternalId);
        Assert.IsTrue(Addressables.ResourceLocators[0] is ResourceLocationMap);
        (Addressables.ResourceLocators[0] as ResourceLocationMap).Add(Hash128.Parse(guidString), locs[0]);
        var ar = new AssetReference(guidString);

        Assert.IsNull(ar.Asset);
        var op = ar.LoadAsset<Object>();
        Assert.IsNull(ar.Asset);
        yield return op;
        Assert.IsNotNull(ar.Asset);
        ar.ReleaseAsset<Object>();
        Assert.IsNull(ar.Asset);
        yield return null;
    }
#endif

    [UnityTest]
    public IEnumerator VerifyProfileVariableEvaluation()
    {
        yield return Init();
        Assert.AreEqual(string.Format("{0}", Addressables.RuntimePath), AddressablesRuntimeProperties.EvaluateString("{UnityEngine.AddressableAssets.Addressables.RuntimePath}"));
        yield return null;
    }

    [UnityTest]
    public IEnumerator CanGetResourceLocationsWithSingleKey()
    {
        yield return Init();
        foreach (var k in m_KeysHashSet)
        {
            Addressables.LoadAssets<IResourceLocation>(k.Key, op1 => Assert.IsNotNull(op1.Result)).Completed += op =>
            {
                Assert.IsNotNull(op.Result);
                Assert.AreEqual(k.Value, op.Result.Count);
            };
            yield return null;
        }
    }

    [UnityTest]
    public IEnumerator CanGetResourceLocationsWithMultipleKeysMerged([Values(Addressables.MergeMode.UseFirst, Addressables.MergeMode.Intersection, Addressables.MergeMode.Union)]Addressables.MergeMode mode)
    {
        yield return Init();
        for (int i = 0; i < 50; i++)
        {
            HashSet<IResourceLocation> set1 = new HashSet<IResourceLocation>();
            HashSet<IResourceLocation> set2 = new HashSet<IResourceLocation>();
            var key1 = m_KeysList[Random.Range(0, m_KeysList.Count / 2)];
            var key2 = m_KeysList[Random.Range(m_KeysList.Count / 2, m_KeysList.Count)];
            var op1 = Addressables.LoadAssets<IResourceLocation>(key1, op => set1.Add(op.Result));
            var op2 = Addressables.LoadAssets<IResourceLocation>(key2, op => set2.Add(op.Result));
            yield return op1;
            yield return op2;
            List<object> keys = new List<object>();
            keys.Add(key1);
            keys.Add(key2);
            var op3 = Addressables.LoadAssets<IResourceLocation>(keys, op => { Assert.IsNotNull(op.Result); Assert.AreEqual(keys, op.Key); }, mode);
            yield return op3;
            Assert.NotNull(op3.Result);
            switch (mode)
            {
                case Addressables.MergeMode.UseFirst:
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
        yield return Init();
        GameObject go = Object.Instantiate(GameObject.CreatePrimitive(PrimitiveType.Cube));
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
        yield return Init();
        int loaded = 0;
        var assets = new List<object>();
        foreach (var key in m_KeysList)
            Addressables.LoadAsset<object>(key).Completed += op =>
            {
                loaded++;
                Assert.IsNotNull(op.Result);
                assets.Add(op.Result);
            };

        while (loaded < m_KeysList.Count)
            yield return null;
        foreach (var a in assets)
            Addressables.ReleaseAsset(a);
    }

    [UnityTest]
    public IEnumerator KeyIsPassedThroughAsyncOperation()
    {
        yield return Init();
        object asset = null;
        Addressables.LoadAsset<object>(m_KeysList[0]).Completed += op =>
        {
            Assert.IsNotNull(op.Result);
            Assert.AreEqual(m_KeysList[0], op.Key);
            asset = op.Result;
        };

        while (asset == null)
            yield return null;
        Addressables.ReleaseAsset(asset);
    }

    [UnityTest]
    public IEnumerator CanReleaseInCallback()
    {
        yield return Init();
        bool complete = false;
        Addressables.LoadAsset<object>(m_KeysList[0]).Completed += op =>
        {
            Assert.IsNotNull(op.Result);
            Addressables.ReleaseAsset(op.Result);
            complete = true;
        };

        while (!complete)
            yield return null;
    }

    [UnityTest]
    public IEnumerator CanLoadAssetsWithCallback()
    {
        yield return Init();
        int loaded = 0;
        var assets = new List<object>();
        foreach (var key in m_KeysList)
        {
            Addressables.LoadAssets<object>(key, a => { Assert.IsNotNull(a.Result); assets.Add(a.Result); }).Completed += op =>
             {
                 loaded++;
                 Assert.IsNotNull(op.Result);
                 foreach (var a in op.Result)
                     Assert.IsNotNull(a);
             };
        }
        while (loaded < m_KeysList.Count)
            yield return null;
        foreach (var a in assets)
            Addressables.ReleaseAsset(a);
    }


    [UnityTest]
    public IEnumerator CanLoadAssetsWithMultipleKeysMerged([Values(Addressables.MergeMode.UseFirst, Addressables.MergeMode.Intersection, Addressables.MergeMode.Union)]Addressables.MergeMode mode)
    {
        yield return Init();
        int loaded = 0;
        var assets = new List<Object>();
        for (int i = 0; i < 50; i++)
        {
            List<object> keys = new List<object>(new[] { m_KeysList[Random.Range(0, m_KeysList.Count / 2)], m_KeysList[Random.Range(m_KeysList.Count / 2, m_KeysList.Count)] });
            var op3 = Addressables.LoadAssets<Object>(keys, op => { Assert.IsNotNull(op.Result); assets.Add(op.Result); }, mode);
            yield return op3;
            Assert.NotNull(op3.Result);
            Addressables.LoadAssets<IResourceLocation>(keys, op => Assert.IsNotNull(op.Result), mode).Completed += checkOp =>
            {
                loaded++;
                Assert.AreEqual(op3.Result.Count, checkOp.Result.Count);
            };
        }
        while (loaded < m_KeysList.Count)
            yield return null;
        foreach (var a in assets)
            Addressables.ReleaseAsset(a);
    }

    [UnityTest]
    public IEnumerator CanLoadPreloadDependenciesForSingleKey()
    {
        yield return Init();
        int loaded = 0;
        foreach (var key in m_KeysList)
        {
            Addressables.PreloadDependencies<object>(key, c => Assert.IsNotNull(c.Result)).Completed += op =>
              {
                  loaded++;
                  Assert.IsNotNull(op.Result);
                  foreach (var d in op.Result)
                  {
                      Assert.IsNotNull(d);
                  }
              };
        }

        while (loaded < m_KeysList.Count)
            yield return null;
    }

    [UnityTest]
    public IEnumerator CanLoadPreloadDependenciesForMutlipleKeys([Values(Addressables.MergeMode.UseFirst, Addressables.MergeMode.Intersection, Addressables.MergeMode.Union)]Addressables.MergeMode mode)
    {
        yield return Init();
        for (int i = 0; i < 50; i++)
        {
            List<object> keys = new List<object>(new[] { m_KeysList[Random.Range(0, m_KeysList.Count / 2)], m_KeysList[Random.Range(m_KeysList.Count / 2, m_KeysList.Count)] });
            var op3 = Addressables.PreloadDependencies<object>(keys, op => Assert.IsNotNull(op.Result), mode);
            yield return op3;
            Assert.NotNull(op3.Result);
            foreach (var d in op3.Result)
                Assert.IsNotNull(d);
        }
    }


    [UnityTest]
    public IEnumerator StressInstantiation()
    {
        yield return Init();
        for (int i = 0; i < 100; i++)
        {
            var key = m_KeysList[Random.Range(0, m_KeysList.Count)];
            Addressables.Instantiate<GameObject>(key, new InstantiationParameters(null, true)).Completed += op =>
            {
                Assert.IsNotNull(op.Result);
                DelayedActionManager.AddAction((Action<Object, float>)Addressables.ReleaseInstance, Random.Range(.25f, .5f), op.Result, 0);
            };

            if (Random.Range(0, 100) > 20)
                yield return null;
        }

        while (DelayedActionManager.IsActive)
            yield return null;

        var objs = SceneManager.GetActiveScene().GetRootGameObjects();
        foreach (var r in objs)
            Assert.False(r.name.EndsWith("(Clone)"), "All instances were not cleaned up");
    }

}
