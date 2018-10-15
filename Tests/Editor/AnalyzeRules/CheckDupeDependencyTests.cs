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
        protected override void OnInit()
        {
            base.OnInit();

            GameObject prefabA = new GameObject("PrefabA");
            GameObject prefabB = new GameObject("PrefabB");
            var meshA = prefabA.AddComponent<MeshRenderer>();
            var meshB = prefabB.AddComponent<MeshRenderer>();


            var mat = new Material(Shader.Find("Unlit/Color"));
            AssetDatabase.CreateAsset(mat, TestConfigFolder + "/checkDupe_myMaterial.mat");
            meshA.material = mat;
            meshB.material = mat;

#if UNITY_2018_3_OR_NEWER
            prefabA = PrefabUtility.SaveAsPrefabAsset(prefabA, TestConfigFolder + "/prefabA.prefab");
            prefabB = PrefabUtility.SaveAsPrefabAsset(prefabB, TestConfigFolder + "/prefabB.prefab");
#else
            PrefabUtility.CreatePrefab(TestConfigFolder + "/checkDupe_prefabA.prefab", prefabA);
            PrefabUtility.CreatePrefab(TestConfigFolder + "/checkDupe_prefabB.prefab", prefabB);
#endif
            AssetDatabase.Refresh();
        }
        /*
        //TODO: fix with newer unity editor
        [Test]
        public void CheckDupeCanFixIssues()
        {
            var group1 = m_settings.CreateGroup("CheckDupeDepencency1", false, false, false, typeof(BundledAssetGroupSchema));
            var group2 = m_settings.CreateGroup("CheckDupeDepencency2", false, false, false, typeof(BundledAssetGroupSchema));

            m_settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(TestConfigFolder + "/checkDupe_prefabA.prefab"), group1, false, false);
            m_settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(TestConfigFolder + "/checkDupe_prefabB.prefab"), group2, false, false);

            var matGuid = AssetDatabase.AssetPathToGUID(TestConfigFolder + "/checkDupe_myMaterial.mat");
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
        */

        [Test]
        public void CheckDupeNoIssuesIfValid()
        {
            var group1 = m_settings.CreateGroup("CheckDupeDepencency1", false, false, false, typeof(BundledAssetGroupSchema));
            var group2 = m_settings.CreateGroup("CheckDupeDepencency2", false, false, false, typeof(BundledAssetGroupSchema));

            m_settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(TestConfigFolder + "/checkDupe_prefabA.prefab"), group1, false, false);
            m_settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(TestConfigFolder + "/checkDupe_prefabB.prefab"), group2, false, false);
            m_settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(TestConfigFolder + "/checkDupe_myMaterial.mat"), group2, false, false);

            var groupCount = m_settings.groups.Count;

            var rule = new CheckDupeDependencies();
            rule.FixIssues(m_settings);

            Assert.AreEqual(groupCount, m_settings.groups.Count);
        }


    }

}