using NUnit.Framework;
using UnityEngine;

namespace UnityEditor.AddressableAssets.Tests
{
    public class BuildScriptTests : AddressableAssetTestBase
    {
        [Test]
        public void CachedSettingsHash_IsAffectedByProjectConfigData()
        {
            /*
            Hash128 hash = BuildScript.CalculateSettingsHash(settings);
            ProjectConfigData.postProfilerEvents = !ProjectConfigData.postProfilerEvents;
            Hash128 hash2 = BuildScript.CalculateSettingsHash(settings);
            ProjectConfigData.postProfilerEvents = !ProjectConfigData.postProfilerEvents;
            Assert.AreNotEqual(hash, hash2);
            */
        }
    }
}