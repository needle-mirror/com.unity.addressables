using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor.AddressableAssets.Build.AnalyzeRules;
using UnityEditor.AddressableAssets.Tests.Runtime.TestObjects;
using UnityEngine;

namespace UnityEditor.AddressableAssets.Tests.AnalyzeRules
{
    public class CheckResourcesDupeDependenciesTests : AddressableAssetTestBase
    {
        string k_CheckDupePrefabA => GetAssetPath("checkDupe_prefabA.prefab");
        string k_CheckDupePrefabB => GetAssetPath("checkDupe_prefabB.prefab");
        string k_CheckDupeMyMaterial => GetAssetPath("checkDupe_myMaterial.mat");
        string k_PrefabWithMaterialPath => GetAssetPath("checkDupe_prefabWithMaterial.prefab");

        protected override void OnInit()
        {
            base.OnInit();

            GameObject prefabA = new GameObject("PrefabA");
            GameObject prefabB = new GameObject("PrefabB");
            GameObject prefabWithMaterial = new GameObject("PrefabWithMaterial");
            var meshA = prefabA.AddComponent<MeshRenderer>();
            var meshB = prefabB.AddComponent<MeshRenderer>();

            var mat = new Material(Shader.Find("Unlit/Color"));
            AssetDatabase.CreateAsset(mat, k_CheckDupeMyMaterial);
            meshA.sharedMaterial = mat;
            meshB.sharedMaterial = mat;

            var meshPrefabWithMaterial = prefabWithMaterial.AddComponent<MeshRenderer>();
            meshPrefabWithMaterial.material = AssetDatabase.LoadAssetAtPath<Material>(k_CheckDupeMyMaterial);

            PrefabUtility.SaveAsPrefabAsset(prefabA, k_CheckDupePrefabA);
            PrefabUtility.SaveAsPrefabAsset(prefabB, k_CheckDupePrefabB);
            PrefabUtility.SaveAsPrefabAsset(prefabWithMaterial, k_PrefabWithMaterialPath);
            AssetDatabase.Refresh();
        }

        [Test]
        public void CheckResourcesDupe_GetsCorrectResourcePaths()
        {
            string resourcesPath = GetAssetPath("Resources");
            if (!Directory.Exists(resourcesPath))
                Directory.CreateDirectory(resourcesPath);
            string prefabPath = GetAssetPath("Resources/PrefabA.prefab");
            GameObject prefabA = new GameObject("PrefabA");
            PrefabUtility.SaveAsPrefabAsset(prefabA, prefabPath);

            var rule = new CheckResourcesDupeDependencies();
            var paths = rule.GetResourcePaths();
            bool success = false;
            foreach (var p in paths)
            {
                if (p.EndsWith("PrefabA.prefab"))
                {
                    success = true;
                    break;
                }
            }

            Assert.IsTrue(success, "CheckResourcesDupeDependencies ResourcePaths did not find the created prefab for test as expect.");
        }

        [Test]
        public void CheckResourcesDupe_ResourcesDependenciesMatchWithExplicitBundleDependencies()
        {
            var rule = new CheckResourcesDupeDependencies();
            rule.BuiltInResourcesToDependenciesMap(new string[] {k_CheckDupePrefabA});
            rule.IntersectResourcesDepedenciesWithBundleDependencies(new List<GUID>()
            {
                new GUID(AssetDatabase.AssetPathToGUID(k_CheckDupePrefabA))
            });

            Assert.IsTrue(rule.m_ResourcesToDependencies.ContainsKey(k_CheckDupePrefabA));
            Assert.AreEqual(1, rule.m_ResourcesToDependencies[k_CheckDupePrefabA].Count);
            Assert.AreEqual(AssetDatabase.AssetPathToGUID(k_CheckDupePrefabA), rule.m_ResourcesToDependencies[k_CheckDupePrefabA][0].ToString());
        }

        [Test]
        public void CheckResourcesDupe_ResourcesDependenciesMatchWithImplicitBundleDependencies()
        {
            var rule = new CheckResourcesDupeDependencies();
            rule.BuiltInResourcesToDependenciesMap(new string[] {k_PrefabWithMaterialPath});
            rule.IntersectResourcesDepedenciesWithBundleDependencies(new List<GUID>()
            {
                new GUID(AssetDatabase.AssetPathToGUID(k_CheckDupeMyMaterial))
            });

            Assert.IsTrue(rule.m_ResourcesToDependencies.ContainsKey(k_PrefabWithMaterialPath));
            Assert.AreEqual(1, rule.m_ResourcesToDependencies[k_PrefabWithMaterialPath].Count);
            Assert.AreEqual(AssetDatabase.AssetPathToGUID(k_CheckDupeMyMaterial), rule.m_ResourcesToDependencies[k_PrefabWithMaterialPath][0].ToString());
        }

        [Test]
        public void CheckResourcesDupe_AllResourcesDependenciesAreReturned()
        {
            var rule = new CheckResourcesDupeDependencies();
            rule.BuiltInResourcesToDependenciesMap(new string[] {k_PrefabWithMaterialPath, k_CheckDupePrefabA});
            rule.IntersectResourcesDepedenciesWithBundleDependencies(new List<GUID>()
            {
                new GUID(AssetDatabase.AssetPathToGUID(k_CheckDupeMyMaterial)),
                new GUID(AssetDatabase.AssetPathToGUID(k_CheckDupePrefabA))
            });

            Assert.IsTrue(rule.m_ResourcesToDependencies[k_PrefabWithMaterialPath].Contains(new GUID(AssetDatabase.AssetPathToGUID(k_CheckDupeMyMaterial))));

            Assert.IsTrue(rule.m_ResourcesToDependencies[k_CheckDupePrefabA].Contains(new GUID(AssetDatabase.AssetPathToGUID(k_CheckDupeMyMaterial))));
            Assert.IsTrue(rule.m_ResourcesToDependencies[k_CheckDupePrefabA].Contains(new GUID(AssetDatabase.AssetPathToGUID(k_CheckDupePrefabA))));
        }

        [Test]
        public void CheckResourcesDupe_DoesNotReportDependencyFromRemovedSerializedField()
        {
            string resourcesPath = GetAssetPath("Resources");
            if (!Directory.Exists(resourcesPath))
                Directory.CreateDirectory(resourcesPath);

            var instance = ScriptableObject.CreateInstance<TestObject>();
            string scriptGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(instance)));
            Object.DestroyImmediate(instance);

            string materialGuid = AssetDatabase.AssetPathToGUID(k_CheckDupeMyMaterial);
            string staleAssetPath = GetAssetPath("Resources/checkDupe_staleField.asset");
            File.WriteAllText(staleAssetPath,
                "%YAML 1.1\n" +
                "%TAG !u! tag:unity3d.com,2011:\n" +
                "--- !u!114 &11400000\n" +
                "MonoBehaviour:\n" +
                "  m_ObjectHideFlags: 0\n" +
                "  m_CorrespondingSourceObject: {fileID: 0}\n" +
                "  m_PrefabInstance: {fileID: 0}\n" +
                "  m_PrefabAsset: {fileID: 0}\n" +
                "  m_GameObject: {fileID: 0}\n" +
                "  m_Enabled: 1\n" +
                "  m_EditorHideFlags: 0\n" +
                "  m_Script: {fileID: 11500000, guid: " + scriptGuid + ", type: 3}\n" +
                "  m_Name: checkDupe_staleField\n" +
                "  m_EditorClassIdentifier: \n" +
                "  m_RemovedMaterial: {fileID: 2100000, guid: " + materialGuid + ", type: 2}\n");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            Assert.Contains(k_CheckDupeMyMaterial, AssetDatabase.GetDependencies(staleAssetPath),
                "Expected the stale serialized reference to still be recorded by the AssetDatabase.");

            var rule = new CheckResourcesDupeDependencies();
            rule.BuiltInResourcesToDependenciesMap(new string[] {staleAssetPath});

            Assert.IsTrue(rule.m_ResourcesToDependencies.ContainsKey(staleAssetPath));
            Assert.IsFalse(rule.m_ResourcesToDependencies[staleAssetPath].Contains(new GUID(materialGuid)));
        }
    }
}
