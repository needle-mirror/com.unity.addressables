#if UNITY_2019_3_OR_NEWER
using System.Collections;
using NUnit.Framework;
using UnityEngine.AddressableAssets;
using UnityEngine.TestTools;

namespace UnityEditor.AddressableAssets.Tests
{
    public class DomainReloadTests : AddressableAssetTestBase
    {
        [Test]
        public void DomainReloadTests_ReInitAddressablesFlagIsSetCorrectly_WhenExitingPlaymode()
        {
            bool savedState = EditorSettings.enterPlayModeOptionsEnabled;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            Addressables.reinitializeAddressables = true;

            EditorApplication.isPlaying = true;
            Addressables.ResolveInternalId("DummyString"); //just need this so m_Addressables property gets called
            Assert.IsFalse(Addressables.reinitializeAddressables);
            EditorApplication.isPlaying = false;
            Assert.IsTrue(Addressables.reinitializeAddressables);

            EditorSettings.enterPlayModeOptionsEnabled = savedState;
        }
    }
}
#endif
