using System.Collections.Generic;
using UnityEngine;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEditor.Build.Pipeline.Tasks;
using UnityEngine.AddressableAssets;
using System;
using NUnit.Framework;
using UnityEditor.AddressableAssets.Tests;

namespace UnityEditor.AddressableAssets
{
    class CheckDupeDependencyTests : AddressableAssetTestBase
    {
        const string k_CheckDupePrefabA = TestConfigFolder + "/checkDupe_prefabA.prefab";
        const string k_CheckDupePrefabB = TestConfigFolder + "/checkDupe_prefabB.prefab";
        const string k_CheckDupeMyMaterial = TestConfigFolder + "/checkDupe_myMaterial.mat";

        protected override void OnInit()
        {
            base.OnInit();

            GameObject prefabA = new GameObject("PrefabA");
            GameObject prefabB = new GameObject("PrefabB");
            var meshA = prefabA.AddComponent<MeshRenderer>();
            var meshB = prefabB.AddComponent<MeshRenderer>();


            var mat = new Material(Shader.Find("Unlit/Color"));
            AssetDatabase.CreateAsset(mat, k_CheckDupeMyMaterial);
            meshA.sharedMaterial = mat;
            meshB.sharedMaterial = mat;

#if UNITY_2018_3_OR_NEWER
            PrefabUtility.SaveAsPrefabAsset(prefabA, k_CheckDupePrefabA);
            PrefabUtility.SaveAsPrefabAsset(prefabB, k_CheckDupePrefabB);
#else
            PrefabUtility.CreatePrefab(k_CheckDupePrefabA, prefabA);
            PrefabUtility.CreatePrefab(k_CheckDupePrefabB, prefabB);
#endif
            AssetDatabase.Refresh();
        }

        [Test]
        public void CheckDupeCanFixIssues()
        {
            var group1 = m_settings.CreateGroup("CheckDupeDepencency1", false, false, false, typeof(BundledAssetGroupSchema));
            var group2 = m_settings.CreateGroup("CheckDupeDepencency2", false, false, false, typeof(BundledAssetGroupSchema));

            m_settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(k_CheckDupePrefabA), group1, false, false);
            m_settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(k_CheckDupePrefabB), group2, false, false);

            var matGuid = AssetDatabase.AssetPathToGUID(k_CheckDupeMyMaterial);
            Assert.IsNull(group1.GetAssetEntry(matGuid));
            Assert.IsNull(group2.GetAssetEntry(matGuid));

            var groupCount = m_settings.groups.Count;

            var rule = new CheckDupeDependencies();
            rule.FixIssues(m_settings);

            Assert.AreEqual(groupCount+1, m_settings.groups.Count);

            //this is potentially unstable.  If another test runs CheckDupeDependencies.FixIssues on this m_settings
            // then this test will create "Duplicate Asset Isolation1".
            var dupeGroup = m_settings.FindGroup("Duplicate Asset Isolation");
            Assert.IsNotNull(dupeGroup);
            Assert.IsNotNull(dupeGroup.GetAssetEntry(matGuid));
        }

        [Test]
        public void CheckDupeNoIssuesIfValid()
        {
            var group1 = m_settings.CreateGroup("CheckDupeDepencency1", false, false, false, typeof(BundledAssetGroupSchema));
            var group2 = m_settings.CreateGroup("CheckDupeDepencency2", false, false, false, typeof(BundledAssetGroupSchema));

            m_settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(k_CheckDupePrefabA), group1, false, false);
            m_settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(k_CheckDupePrefabB), group2, false, false);
            m_settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(k_CheckDupeMyMaterial), group2, false, false);

            var groupCount = m_settings.groups.Count;

            var rule = new CheckDupeDependencies();
            rule.FixIssues(m_settings);

            Assert.AreEqual(groupCount, m_settings.groups.Count);
        }


    }

}