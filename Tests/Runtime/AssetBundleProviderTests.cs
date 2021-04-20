using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
#if UNITY_EDITOR
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
#endif
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AddressableTests.SyncAddressables
{
    public abstract class AssetBundleProviderTests : AddressablesTestFixture
    {
        protected string m_PrefabKey = "syncprefabkey";
        protected string m_InvalidKey = "notarealkey";
        protected string m_SceneKey = "syncscenekey";

        const int kForceUWRBundleCount = 10;
        const int kMaxConcurrentRequests = 3;
        string GetForceUWRAddrName(int i) { return $"forceuwrasset{i}"; }

#if UNITY_EDITOR
        internal override void Setup(AddressableAssetSettings settings, string tempAssetFolder)
        {
            AddressableAssetGroup regGroup = settings.CreateGroup("localNoUWRGroup", false, false, true,
                new List<AddressableAssetGroupSchema>(), typeof(BundledAssetGroupSchema));
            regGroup.GetSchema<BundledAssetGroupSchema>().BundleNaming = BundledAssetGroupSchema.BundleNamingStyle.OnlyHash;

            AddressableAssetGroup forceUWRGroup = settings.CreateGroup("ForceUWRGroup", false, false, true,
                new List<AddressableAssetGroupSchema>(), typeof(BundledAssetGroupSchema));
            forceUWRGroup.GetSchema<BundledAssetGroupSchema>().UseUnityWebRequestForLocalBundles = true;
            forceUWRGroup.GetSchema<BundledAssetGroupSchema>().BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackSeparately;
            forceUWRGroup.GetSchema<BundledAssetGroupSchema>().BundleNaming = BundledAssetGroupSchema.BundleNamingStyle.OnlyHash;

            settings.MaxConcurrentWebRequests = kMaxConcurrentRequests;

            for (int i = 0; i < kForceUWRBundleCount; i++)
            {
                string s = GetForceUWRAddrName(i);
                string guid = CreatePrefab(tempAssetFolder + $"/{s}.prefab");
                AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, forceUWRGroup);
                entry.address = s;
            }

            {
                string guid = CreatePrefab(tempAssetFolder + $"/testprefab.prefab");
                AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, regGroup);
                entry.address = "testprefab";
            }
        }
#endif

        [UnityTest]
        public IEnumerator WhenUWRExceedsMaxLimit_UWRAreQueued()
        {
            List<AsyncOperationHandle<GameObject>> l =
                Enumerable.Range(0, kForceUWRBundleCount).Select(x => m_Addressables.LoadAssetAsync<GameObject>(GetForceUWRAddrName(x))).ToList();
            Assert.AreEqual(kMaxConcurrentRequests, WebRequestQueue.s_ActiveRequests.Count);
            Assert.AreEqual((kForceUWRBundleCount - kMaxConcurrentRequests), WebRequestQueue.s_QueuedOperations.Count);
            foreach (AsyncOperationHandle<GameObject> h in l)
            {
                yield return h;
                h.Release();
            }
        }

        [UnityTest]
        public IEnumerator WhenAssetBundleIsLocal_AndForceUWRIsEnabled_UWRIsUsed()
        {
            AsyncOperationHandle<GameObject> h = m_Addressables.LoadAssetAsync<GameObject>(GetForceUWRAddrName(0));
            Assert.AreEqual(1, WebRequestQueue.s_ActiveRequests.Count);
            yield return h;
            h.Release();
        }

        [UnityTest]
        public IEnumerator WhenAssetBundleIsLocal_AndForceUWRIsDisabled_UWRIsNotUsed()
        {
            AsyncOperationHandle<GameObject> h = m_Addressables.LoadAssetAsync<GameObject>("testprefab");
            Assert.AreEqual(0, WebRequestQueue.s_ActiveRequests.Count);
            yield return h;
            h.Release();
        }
    }

#if UNITY_EDITOR
    class AssetBundleProviderTests_PackedPlaymodeMode : AssetBundleProviderTests { protected override TestBuildScriptMode BuildScriptMode { get { return TestBuildScriptMode.PackedPlaymode; } } }
#endif

    [UnityPlatform(exclude = new[] { RuntimePlatform.WindowsEditor, RuntimePlatform.OSXEditor, RuntimePlatform.LinuxEditor })]
    class AssetBundleProviderTests_PackedMode : AssetBundleProviderTests { protected override TestBuildScriptMode BuildScriptMode { get { return TestBuildScriptMode.Packed; } } }
}
