using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.TestTools;

namespace UnityEditor.AddressableAssets.Tests
{
    public class BuildScriptTests : AddressableAssetTestBase
    {
        [Test]
        public void TestCleanupOfStreamingAssetFolder()
        {
            var context = new AddressablesBuildDataBuilderContext(m_Settings,
                BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget),
                EditorUserBuildSettings.activeBuildTarget,
                false, false, m_Settings.PlayerBuildVersion);
            for (int i = 0; i < m_Settings.DataBuilders.Count; i++)
            {

                var builder = m_Settings.DataBuilders[i] as IDataBuilder;
                if (builder.CanBuildData<AddressablesPlayerBuildResult>())
                {
                    var existingFiles = new HashSet<string>();
                    if (System.IO.Directory.Exists("Assets/StreamingAssets"))
                    {
                        foreach (var f in System.IO.Directory.GetFiles("Assets/StreamingAssets"))
                            existingFiles.Add(f);
                    }
                    builder.BuildData<AddressablesPlayerBuildResult>(context);
                    builder.ClearCachedData();
                    if (System.IO.Directory.Exists("Assets/StreamingAssets"))
                    {
                        foreach (var f in System.IO.Directory.GetFiles("Assets/StreamingAssets"))
                            Assert.IsTrue(existingFiles.Contains(f), string.Format("Data Builder {0} did not clean up file {1}", builder.Name, f));
                    }
                }
            }

        }

        [Test]
        public void BuildCompleteCallbackGetsCalled()
        {
            LogAssert.ignoreFailingMessages = true;
            AddressableAssetSettings oldSettings = AddressableAssetSettingsDefaultObject.Settings;
            AddressableAssetSettingsDefaultObject.Settings = m_Settings;

            bool callbackCalled = false;
            BuildScript.buildCompleted += (result) =>
            {
                callbackCalled = true;
            };
            AddressableAssetSettings.BuildPlayerContent();
            Assert.IsTrue(callbackCalled);

            if (oldSettings != null)
                AddressableAssetSettingsDefaultObject.Settings = oldSettings;
            AddressableAssetSettings.BuildPlayerContent();
            LogAssert.ignoreFailingMessages = false;
        }
    }
}