using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.ResourceManagement.Util;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace UnityEngine.ResourceManagement.Tests
{
    public abstract class ResourceManagerBaseTests
    {
        const int kAssetCount = 25;
        protected string RootFolder { get { return string.Format("Assets/{0}_AssetsToDelete", GetType().Name); } }
        List<IResourceLocation> m_Locations = new List<IResourceLocation>();
        protected virtual string AssetPathPrefix { get { return ""; } }
        protected abstract IResourceLocation CreateLocationForAsset(string name, string path);
        protected abstract void ProcessLocations(List<IResourceLocation> locations);
        string GetAssetPath(int i)
        {
            return RootFolder + "/" + AssetPathPrefix + "asset" + i + ".prefab";
        }

        protected ResourceManager m_ResourceManager;


        [OneTimeTearDown]
        public void Cleanup()
        {
#if UNITY_EDITOR
            AssetDatabase.DeleteAsset(RootFolder);
#endif
        }

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            DelayedActionManager.Wait(0, .1f);
            m_ResourceManager = new ResourceManager();
            m_ResourceManager.InstanceProvider = new InstanceProvider();
            m_ResourceManager.SceneProvider = new SceneProvider();
            AsyncOperationCache.Instance.Clear();
            DelayedActionManager.Clear();

#if UNITY_EDITOR
            AssetDatabase.StartAssetEditing();
            for (int i = 0; i < kAssetCount; i++)
            {
                GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "asset" + i;
                var assetPath = GetAssetPath(i);
                if (!Directory.Exists(Path.GetDirectoryName(assetPath)))
                    Directory.CreateDirectory(Path.GetDirectoryName(assetPath));

#if UNITY_2018_3_OR_NEWER
                PrefabUtility.SaveAsPrefabAsset(go, assetPath);
#else
                PrefabUtility.CreatePrefab(assetPath, go);
#endif
                Object.DestroyImmediate(go, false);
            }

            AssetDatabase.StopAssetEditing();
#endif

            for (int i = 0; i < kAssetCount; i++)
                m_Locations.Add(CreateLocationForAsset("asset" + i, GetAssetPath(i)));

            ProcessLocations(m_Locations);
        }
        
        [UnityTest]
        public IEnumerator CanProvideWithCallback()
        {
            m_ResourceManager.ProvideResource<GameObject>(m_Locations[0]).Completed += op => Assert.IsNotNull(op.Result);
            yield return null;
        }


        [UnityTest]
        public IEnumerator VerifyKey()
        {
            m_ResourceManager.ProvideResource<GameObject>(m_Locations[0]).Completed += op => Assert.IsNotNull(op.Key == m_Locations[0]);
            yield return null;
        }

        [UnityTest]
        public IEnumerator CanProvideWithYield()
        {
            var op = m_ResourceManager.ProvideResource<GameObject>(m_Locations[0]);
            yield return op;
            Assert.IsNotNull(op.Result);
            op.Release();
        }

        [UnityTest]
        public IEnumerator CanProvideMultipleResources()
        {
            m_ResourceManager.ProvideResources<GameObject>(m_Locations, perOp => Assert.IsNotNull(perOp.Result)).Completed += op =>
            {
                Assert.IsNotNull(op.Result);
                Assert.AreEqual(op.Result.Count, m_Locations.Count);
            };
            yield return null;
        }

        [UnityTest]
        public IEnumerator CanProvideInstance()
        {
            var loadOp = m_ResourceManager.ProvideInstance<GameObject>(m_Locations[0], new InstantiationParameters(null, true));
            loadOp.Completed += op =>
            {
                Assert.IsNotNull(op.Result);
                Assert.IsNotNull(GameObject.Find(m_Locations[0] + "(Clone)"));
            };

            yield return loadOp;
            m_ResourceManager.ReleaseInstance(loadOp.Result, m_Locations[0]);
            yield return null;
            Assert.IsNull(GameObject.Find(m_Locations[0] + "(Clone)"));
        }

        [UnityTest]
        public IEnumerator CanProvideMultipleInstances()
        {
            var loadOp = m_ResourceManager.ProvideInstances<GameObject>(m_Locations, perOp => Assert.IsNotNull(perOp.Result), new InstantiationParameters(null, true));
            loadOp.Completed += op =>
            {
                Assert.IsNotNull(op.Result);
                for (int i = 0; i < m_Locations.Count; i++)
                    Assert.IsNotNull(GameObject.Find(m_Locations[i] + "(Clone)"));
            };
            yield return loadOp;
            for (int i = 0; i < loadOp.Result.Count; i++)
                m_ResourceManager.ReleaseInstance(loadOp.Result[i], m_Locations[i]);
            yield return null;
            for (int i = 0; i < m_Locations.Count; i++)
                Assert.IsNull(GameObject.Find(m_Locations[i] + "(Clone)"));
        }

        [UnityTest]
        public IEnumerator StressInstantiation()
        {
            for (int i = 0; i < 100; i++)
            {
                var loc = m_Locations[Random.Range(0, m_Locations.Count)];
                m_ResourceManager.ProvideInstance<GameObject>(loc, new InstantiationParameters(null, true)).Completed += op =>
                {
                    Assert.IsNotNull(op.Result);
                    DelayedActionManager.AddAction((Action<Object, IResourceLocation>)m_ResourceManager.ReleaseInstance, Random.Range(.25f, .5f), op.Result, loc);
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
}
