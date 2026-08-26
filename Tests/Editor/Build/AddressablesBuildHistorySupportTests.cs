#if ENABLE_BUILD_HISTORY_EXTERNAL_BUILDS
using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Build.BuildPipelineTasks;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Build.Layout;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.TestTools;

namespace UnityEditor.AddressableAssets.Tests
{
    public class AddressablesBuildHistorySupportTests : AddressableBuildTaskTestBase
    {
        static BuildLayout CreateLayout(DateTime buildStart, string error = null)
        {
            BuildLayout layout = new BuildLayout();
            layout.AddressablesBuildSessionGUID = GUID.Generate();
            layout.BuildTarget = BuildTarget.Android;
            layout.BuildStart = buildStart;
            layout.Duration = 12.5;
            layout.BuildError = error;
            layout.LocalCatalogBuildPath = "Library/com.unity.addressables/aa/catalog.bin";

            BuildLayout.Group group = new BuildLayout.Group();
            group.Bundles.Add(new BuildLayout.Bundle { Name = "group1.bundle", FileSize = 100 });
            group.Bundles.Add(new BuildLayout.Bundle { Name = "group2.bundle", FileSize = 25 });
            layout.Groups.Add(group);
            layout.BuiltInBundles.Add(new BuildLayout.Bundle { Name = "shaders.bundle", FileSize = 8 });
            return layout;
        }

        [Test]
        public void CreateBuildInfo_MapsLayoutFields()
        {
            BuildLayout layout = CreateLayout(new DateTime(2026, 8, 20, 10, 30, 0));
            ExternalBuildInfo buildInfo = AddressablesBuildHistorySupport.CreateBuildInfo(layout);

            Assert.AreEqual(layout.AddressablesBuildSessionGUID, buildInfo.BuildSessionGUID);
            Assert.AreEqual(UnityEditor.Build.Reporting.BuildType.AssetBundle, buildInfo.BuildType);
            Assert.AreEqual("Addressables", buildInfo.BuildTypeName);
            Assert.AreEqual("Addressables", buildInfo.BuildName);
            Assert.AreEqual(BuildTarget.Android, buildInfo.Platform);
            Assert.AreEqual(UnityEditor.Build.Reporting.BuildResult.Succeeded, buildInfo.BuildResult);
            Assert.AreEqual(new DateTimeOffset(layout.BuildStart), buildInfo.BuildStartedAt);
            Assert.AreEqual(TimeSpan.FromSeconds(12.5), buildInfo.TotalTime);
            Assert.AreEqual(layout.LocalCatalogBuildPath, buildInfo.OutputPath);
            Assert.AreEqual(133, buildInfo.TotalSizeBytes, "TotalSizeBytes should sum group bundles and built-in bundles.");
        }

        [Test]
        public void CreateBuildInfo_FailedBuild_MapsBuildResultFailed()
        {
            BuildLayout layout = CreateLayout(new DateTime(2026, 8, 20, 10, 30, 0), "build error");
            ExternalBuildInfo buildInfo = AddressablesBuildHistorySupport.CreateBuildInfo(layout);
            Assert.AreEqual(UnityEditor.Build.Reporting.BuildResult.Failed, buildInfo.BuildResult);
        }

        [Test]
        public void RegisterBuild_CreatesHistoryEntryWithLayoutAndTepFiles()
        {
            BuildLayout layout = CreateLayout(DateTime.Now);
            GUID guid = layout.AddressablesBuildSessionGUID;
            string layoutPath = Path.Combine(Path.GetTempPath(), $"testLayout_{guid}.json");
            string tepPath = Path.Combine(Path.GetTempPath(), $"testTep_{guid}.json");
            try
            {
                File.WriteAllText(layoutPath, "layout content");
                File.WriteAllText(tepPath, "tep content");

                AddressablesBuildHistorySupport.RegisterBuild(layout, layoutPath);
                Assert.IsTrue(BuildHistory.TryGetBuildReportDirectory(guid, out string buildReportDirectory), "The registered build should have a build report directory.");
                AddressablesBuildHistorySupport.TryCopyTep(tepPath, buildReportDirectory);

                Assert.IsTrue(BuildHistory.TryGetFilePath(guid, "AddressablesBuildLayout.json", out string entryLayoutPath), "Layout file should exist in the build history entry.");
                Assert.AreEqual("layout content", File.ReadAllText(entryLayoutPath));
                Assert.IsTrue(BuildHistory.TryGetFilePath(guid, "AddressablesBuildTEP.json", out string entryTepPath), "TEP file should exist in the build history entry.");
                Assert.AreEqual("tep content", File.ReadAllText(entryTepPath));

                Assert.IsTrue(BuildHistory.TryGetFilePath(guid, "BuildReportSummary.json", out string summaryPath));
                BuildReportSummary summary = BuildReportSummary.Load(summaryPath);
                Assert.AreEqual(guid, summary.BuildSessionGUID);
                Assert.AreEqual(UnityEditor.Build.Reporting.BuildType.AssetBundle, summary.BuildType);
            }
            finally
            {
                BuildHistory.DeleteHistory(new[] { guid });
                File.Delete(layoutPath);
                File.Delete(tepPath);
            }
        }

        [Test]
        public void RegisterBuild_DuplicateSessionGuid_LogsWarningInsteadOfThrowing()
        {
            BuildLayout layout = CreateLayout(DateTime.Now);
            GUID guid = layout.AddressablesBuildSessionGUID;
            string layoutPath = Path.Combine(Path.GetTempPath(), $"testLayout_{guid}.json");
            try
            {
                File.WriteAllText(layoutPath, "layout content");

                AddressablesBuildHistorySupport.RegisterBuild(layout, layoutPath);
                LogAssert.Expect(LogType.Warning, new Regex("Failed to register the Addressables build"));
                AddressablesBuildHistorySupport.RegisterBuild(layout, layoutPath);

                // The first entry is left intact by the failed re-registration.
                Assert.IsTrue(BuildHistory.TryGetFilePath(guid, "AddressablesBuildLayout.json", out _));
            }
            finally
            {
                BuildHistory.DeleteHistory(new[] { guid });
                File.Delete(layoutPath);
            }
        }

        [Test]
        public void TryCopyTep_WithoutBuildReportDirectory_DoesNothing()
        {
            AddressablesBuildHistorySupport.TryCopyTep(Path.Combine(Path.GetTempPath(), "doesNotExist.json"), null);
            AddressablesBuildHistorySupport.TryCopyTep(Path.Combine(Path.GetTempPath(), "doesNotExist.json"), string.Empty);
        }

        [Test]
        public void GenerateErrorReport_RefreshesLegacyJsonLayoutForTheFailedBuild()
        {
            bool previousPreference = ProjectConfigData.GenerateBuildLayout;
            ProjectConfigData.ReportFileFormat previousFormat = ProjectConfigData.BuildLayoutReportFileFormat;
            // Backdated so the recorded duration is unambiguously non-zero.
            DateTime buildStart = DateTime.Now.AddSeconds(-5);
            string legacyReportPath = BuildLayoutGenerationTask.TimeStampedReportPath(buildStart);
            string legacyJsonPath = BuildLayoutGenerationTask.GetLayoutFilePathForFormat(ProjectConfigData.ReportFileFormat.JSON);
            GUID registeredGuid = default;
            try
            {
                ProjectConfigData.GenerateBuildLayout = true;
                ProjectConfigData.BuildLayoutReportFileFormat = ProjectConfigData.ReportFileFormat.JSON;
                AddressableAssetsBuildContext aaContext = new AddressableAssetsBuildContext
                {
                    Settings = m_Settings,
                    buildStartTime = buildStart
                };

                BuildLayoutGenerationTask.GenerateErrorReport("test error", aaContext, null);
                Assert.IsTrue(BuildHistory.TryGetLatestBuild(out registeredGuid));

                Assert.IsTrue(File.Exists(legacyJsonPath), "A failed build should refresh the fixed-path json layout.");
                BuildLayout legacyLayout = BuildLayout.Open(legacyJsonPath, true, false);
                Assert.AreEqual(registeredGuid, legacyLayout.AddressablesBuildSessionGUID,
                    "The fixed-path layout should identify the failed build, not an earlier one.");
                Assert.Greater(legacyLayout.Duration, 0.0, "A failed build should record a duration.");
            }
            finally
            {
                ProjectConfigData.GenerateBuildLayout = previousPreference;
                ProjectConfigData.BuildLayoutReportFileFormat = previousFormat;
                if (!registeredGuid.Empty())
                    BuildHistory.DeleteHistory(new[] { registeredGuid });
                if (File.Exists(legacyReportPath))
                    File.Delete(legacyReportPath);
                if (File.Exists(legacyJsonPath))
                    File.Delete(legacyJsonPath);
            }
        }

        [Test]
        public void GenerateErrorReport_RegistersEntryOnlyWhenGenerateBuildLayoutEnabled([Values(true, false)] bool generateBuildLayout)
        {
            bool previousPreference = ProjectConfigData.GenerateBuildLayout;
            DateTime buildStart = DateTime.Now;
            string legacyReportPath = BuildLayoutGenerationTask.TimeStampedReportPath(buildStart);
            GUID registeredGuid = default;
            try
            {
                ProjectConfigData.GenerateBuildLayout = generateBuildLayout;
                AddressableAssetsBuildContext aaContext = new AddressableAssetsBuildContext
                {
                    Settings = m_Settings,
                    buildStartTime = buildStart
                };

                BuildHistory.TryGetLatestBuild(out GUID latestBefore);
                BuildLayoutGenerationTask.GenerateErrorReport("test error", aaContext, null);
                BuildHistory.TryGetLatestBuild(out GUID latestAfter);

                if (generateBuildLayout)
                {
                    Assert.AreNotEqual(latestBefore, latestAfter, "A failed build should register a build history entry when Generate Build Layout is enabled.");
                    registeredGuid = latestAfter;
                    Assert.IsTrue(BuildHistory.TryGetFilePath(latestAfter, "AddressablesBuildLayout.json", out _));
                }
                else
                {
                    Assert.AreEqual(latestBefore, latestAfter, "A failed build should not register a build history entry when Generate Build Layout is disabled.");
                }
            }
            finally
            {
                ProjectConfigData.GenerateBuildLayout = previousPreference;
                if (!registeredGuid.Empty())
                    BuildHistory.DeleteHistory(new[] { registeredGuid });
                if (File.Exists(legacyReportPath))
                    File.Delete(legacyReportPath);
            }
        }
    }
}
#endif
