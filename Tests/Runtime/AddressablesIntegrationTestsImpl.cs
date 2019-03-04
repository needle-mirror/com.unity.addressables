using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;
using System.IO;
using UnityEngine.SceneManagement;
using UnityEngine.AddressableAssets.Initialization;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.ResourceManagement.Util;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace AddressableAssetsIntegrationTests
{
    public abstract partial class AddressablesIntegrationTests : IPrebuildSetup
    {
        [UnityTest]
        public IEnumerator VerifyProfileVariableEvaluation()
        {
            yield return Init();
            Assert.AreEqual(string.Format("{0}", Addressables.RuntimePath), AddressablesRuntimeProperties.EvaluateString("{UnityEngine.AddressableAssets.Addressables.RuntimePath}"));
            yield return Complete();
        }

        [UnityTest]
        public IEnumerator VerifyDownloadSize()
        {
            yield return Init();
            long expectedSize = 0;
            var locMap = new ResourceLocationMap();

            var bundleLoc1 = new ResourceLocationBase("sizeTestBundle1", "http://nowhere.com/mybundle1.bundle", typeof(AssetBundleProvider).FullName);
            var sizeData1 = (bundleLoc1.Data = CreateLocationSizeData("sizeTestBundle1", 1000, 123, "hashstring1")) as ILocationSizeData;
            if (sizeData1 != null)
                expectedSize += sizeData1.ComputeSize(bundleLoc1);

            var bundleLoc2 = new ResourceLocationBase("sizeTestBundle2", "http://nowhere.com/mybundle2.bundle", typeof(AssetBundleProvider).FullName);
            var sizeData2 = (bundleLoc2.Data = CreateLocationSizeData("sizeTestBundle2", 500, 123, "hashstring2")) as ILocationSizeData;
            if (sizeData2 != null)
                expectedSize += sizeData2.ComputeSize(bundleLoc2);

            var assetLoc = new ResourceLocationBase("sizeTestAsset", "myASset.asset", typeof(BundledAssetProvider).FullName, bundleLoc1, bundleLoc2);

            locMap.Add("sizeTestBundle1", bundleLoc1);
            locMap.Add("sizeTestBundle2", bundleLoc2);
            locMap.Add("sizeTestAsset", assetLoc);
            Addressables.ResourceLocators.Add(locMap);

            Addressables.GetDownloadSize("sizeTestAsset").Completed += op =>
            {
                Assert.AreEqual(expectedSize, op.Result);
            };
            yield return Complete();
        }

        [UnityTest]
        public IEnumerator CanGetResourceLocationsWithSingleKey()
        {
            yield return Init();
            int loadCount = 0;
            int loadedCount = 0;
            foreach (var k in m_KeysHashSet)
            {
                loadCount++;
                Addressables.LoadAssets<IResourceLocation>(k.Key, op1 => Assert.IsNotNull(op1.Result)).Completed += op =>
                {
                    loadedCount++;
                    Assert.IsNotNull(op.Result);
                    Assert.AreEqual(k.Value, op.Result.Count);
                };
                yield return null;
            }
            while (loadedCount < loadCount)
                yield return null;
            yield return Complete();
        }

        [UnityTest]
        public IEnumerator CanGetResourceLocationsWithMultipleKeysMerged([Values(Addressables.MergeMode.UseFirst, Addressables.MergeMode.Intersection, Addressables.MergeMode.Union)]Addressables.MergeMode mode)
        {
            yield return Init();

            for (int i = 0; i < m_KeysList.Count; i++)
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
            yield return Complete();
        }


        [UnityTest]
        public IEnumerator CanLoadAssetsWithMultipleKeysMerged([Values(Addressables.MergeMode.UseFirst, Addressables.MergeMode.Intersection, Addressables.MergeMode.Union)]Addressables.MergeMode mode)
        {
            yield return Init();
            int loaded = 0;
            var assets = new List<Object>();
            int loadCount = 0;
            for (int i = 0; i < m_KeysList.Count; i++)
            {
                List<object> keys = new List<object>(new[] { m_KeysList[Random.Range(0, m_KeysList.Count / 2)], m_KeysList[Random.Range(m_KeysList.Count / 2, m_KeysList.Count)] });
                var op3 = Addressables.LoadAssets<Object>(keys, op => { Assert.IsNotNull(op.Result); assets.Add(op.Result); }, mode);
                yield return op3;
                Assert.NotNull(op3.Result);
                loadCount++;
                Addressables.LoadAssets<IResourceLocation>(keys, op => Assert.IsNotNull(op.Result), mode).Completed += checkOp =>
                {
                    loaded++;
                    Assert.AreEqual(op3.Result.Count, checkOp.Result.Count);
                };
            }
            while (loaded < loadCount)
                yield return null;
            foreach (var a in assets)
                Addressables.ReleaseAsset(a);
            yield return Complete();
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
            yield return Complete();
        }

        [UnityTest]
        public IEnumerator CanLoadAssetWithCallback()
        {
            yield return Init();
            int loaded = 0;
            int loadCount = 0;
            var assets = new List<object>();
            foreach (var key in m_KeysList)
            {
                loadCount++;
                Addressables.LoadAsset<object>(key).Completed += op =>
                {
                    loaded++;
                    Assert.IsNotNull(op.Result);
                    assets.Add(op.Result);
                };
            }
            while (loaded < loadCount)
                yield return null;
            foreach (var a in assets)
                Addressables.ReleaseAsset(a);
            yield return Complete();
        }
        
        [UnityTest]
        public IEnumerator CanLoadAssetFromExtraCatalogs()
        {
            yield return Init();
            int loaded = 0;
            int loadCount = 0;
            var assets = new List<object>();
            foreach (var key in m_KeysList)
            {
                if (key.GetType() == typeof(string) && (key as string).EndsWith("EXTRA"))
                {
                    loadCount++;
                    Addressables.LoadAsset<object>(key).Completed += op =>
                    {
                        loaded++;
                        Assert.IsNotNull(op.Result);
                        assets.Add(op.Result);
                    };
                }
            }
            while (loaded < loadCount)
                yield return null;
            foreach (var a in assets)
                Addressables.ReleaseAsset(a);
            yield return Complete();
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
            yield return Complete();
        }

        [UnityTest]
        public IEnumerator CanReleaseInCallback()
        {
            yield return Init();
            int loaded = 0;
            int loadCount = 0;

            bool complete = false;
            loadCount++;
            Addressables.LoadAsset<object>(m_KeysList[0]).Completed += op =>
            {
                loaded++;
                Assert.IsNotNull(op.Result);
                Addressables.ReleaseAsset(op.Result);
                complete = true;
            };
            while (loaded < loadCount)
                yield return null;
            while (!complete)
                yield return null;
            yield return Complete();
        }

        [UnityTest]
        public IEnumerator CanLoadAssetsWithMultipleTypes()
        {
            yield return Init();
            int loaded = 0;
            int loadCount = 0;
            var assets = new List<object>();
            foreach (var key in m_KeysList)
            {
                loadCount++;
                Addressables.LoadAssets<object>(key, a => { Assert.IsNotNull(a.Result); assets.Add(a.Result); }).Completed += op =>
                {
                    loaded++;
                    Assert.IsNotNull(op.Result);
                    foreach (var a in op.Result)
                        Assert.IsNotNull(a);
                };
            }
            while (loaded < loadCount)
                yield return null;

            var gameObjects = new List<GameObject>();
            foreach (var key in m_KeysList)
            {
                loadCount++;
                Addressables.LoadAssets<GameObject>(key, a => { Assert.IsNotNull(a.Result); gameObjects.Add(a.Result); }).Completed += op =>
                {
                    loaded++;
                    Assert.IsNotNull(op.Result);
                    foreach (var a in op.Result)
                        Assert.IsNotNull(a);
                };
            }
            while (loaded < loadCount)
                yield return null;

            foreach (var a in gameObjects)
                Addressables.ReleaseAsset(a);
            foreach (var a in assets)
                Addressables.ReleaseAsset(a);
            yield return Complete();
        }

        [UnityTest]
        public IEnumerator CanLoadAssetsWithCallback()
        {
            yield return Init();
            int loaded = 0;
            int loadCount = 0;
            var assets = new List<object>();
            foreach (var key in m_KeysList)
            {
                loadCount++;
                Addressables.LoadAssets<object>(key, a => { Assert.IsNotNull(a.Result); assets.Add(a.Result); }).Completed += op =>
                {
                    loaded++;
                    Assert.IsNotNull(op.Result);
                    foreach (var a in op.Result)
                        Assert.IsNotNull(a);
                };
            }
            while (loaded < loadCount)
                yield return null;
            foreach (var a in assets)
                Addressables.ReleaseAsset(a);
            yield return Complete();
        }


        [UnityTest]
        public IEnumerator CanLoadPreloadDependenciesForSingleKey()
        {
            yield return Init();
            int loaded = 0;
            int loadCount = 0;
            foreach (var key in m_KeysList)
            {
                loadCount++;
                Addressables.DownloadDependencies(key).Completed += op =>
                {
                    loaded++;
                    Assert.IsTrue(op.IsDone);
                    Assert.IsTrue(op.IsValid);
                    Assert.IsNull(op.OperationException);
                };
            }

            while (loaded < loadCount)
                yield return null;
            yield return Complete();
        }

        [UnityTest]
        public IEnumerator CanLoadPreloadDependenciesForMutlipleKeys([Values(Addressables.MergeMode.UseFirst, Addressables.MergeMode.Intersection, Addressables.MergeMode.Union)]Addressables.MergeMode mode)
        {
            yield return Init();
            int loaded = 0;
            int loadCount = 0;
            for (int i = 0; i < m_KeysList.Count; i++)
            {
                loadCount++;
                List<object> keys = new List<object>(new[] { m_KeysList[Random.Range(0, m_KeysList.Count / 2)], m_KeysList[Random.Range(m_KeysList.Count / 2, m_KeysList.Count)] });
                Addressables.DownloadDependencies(keys, mode).Completed += op3 =>
                {
                    Assert.IsTrue(op3.IsDone);
                    Assert.IsTrue(op3.IsValid);
                    Assert.IsNull(op3.OperationException);
                    loaded++;
                };
            }
            while (loaded < loadCount)
                yield return null;
            yield return Complete();
        }


        [UnityTest]
        public IEnumerator StressInstantiation()
        {
            yield return Init();
            var objs = SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var r in objs)
                Assert.False(r.name.EndsWith("(Clone)"), "All instances from previous test were not cleaned up");

            int loaded = 0;
            int loadCount = 0;
            for (int i = 0; i < 50; i++)
            {
                loadCount++;
                var key = m_KeysList[Random.Range(0, m_KeysList.Count)];
                Addressables.Instantiate(key, new InstantiationParameters(null, true)).Completed += op =>
                {
                    Assert.IsNotNull(op.Result);
                    DelayedActionManager.AddAction((System.Action<GameObject, float>)Addressables.ReleaseInstance, Random.Range(.25f, .5f), op.Result, 0);
                    loaded++;
                };

                if (Random.Range(0, 100) > 20)
                    yield return null;
            }

            while (loaded < loadCount)
                yield return null;

            while (DelayedActionManager.IsActive)
                yield return null;

            objs = SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var r in objs)
                Assert.False(r.name.EndsWith("(Clone)"), "All instances from this test were not cleaned up");


            yield return Complete();
        }


        private const string SceneName = "DontDestroyOnLoadScene";
        private Scene _scene;
        [UnityTest]
        public IEnumerator DontDestroyOnLoad_DoesntDestroyAddressablesAssets()
        {
            yield return Init();
            string cloneName = Path.GetFileNameWithoutExtension(m_KeysList[0].ToString()) + "(Clone)";

            var async = Addressables.Instantiate(m_KeysList[0]);
            async.Completed += Async_Completed;

            yield return async;

            GameObject go = GameObject.Find(cloneName);
            Assert.IsNotNull(go);

            yield return SceneManager.UnloadSceneAsync(_scene);
            yield return new WaitForSeconds(1.0f);

            go = GameObject.Find(cloneName);
            Assert.IsNotNull(go);

            Addressables.ReleaseInstance(async.Result);
            yield return Complete();
        }

        private void Async_Completed(IAsyncOperation<GameObject> obj)
        {
            _scene = SceneManager.CreateScene(SceneName);
            Scene oldScene = obj.Result.scene;
            SceneManager.MoveGameObjectToScene(obj.Result, _scene);
            Addressables.RecordInstanceSceneChange(obj.Result, oldScene, obj.Result.scene);

            Object.DontDestroyOnLoad(obj.Result);
        }
    }
}