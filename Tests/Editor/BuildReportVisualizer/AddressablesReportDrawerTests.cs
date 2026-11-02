#if ENABLE_BUILD_ANALYSIS_INTEGRATION
using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Build.Layout;
using UnityEditor.AddressableAssets.BuildReportVisualizer;
using UnityEditor.Build;
using UnityEditor.Build.Analysis;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Tests.Editor.BuildReportVisualizer
{
    public class AddressablesReportDrawerTests
    {
        const string k_ProfileName = "TestProfile";

        GUID m_RegisteredBuild;
        string m_LayoutSourcePath;

        [SetUp]
        public void RegisterAddressablesBuild()
        {
            BuildLayout layout = new BuildLayout
            {
                AddressablesBuildSessionGUID = GUID.Generate(),
                BuildTarget = BuildTarget.Android,
                BuildStart = DateTime.Now,
                Duration = 12.5,
                LocalCatalogBuildPath = "Library/com.unity.addressables/aa/catalog.bin",
                AddressablesEditorSettings = new BuildLayout.AddressablesEditorData
                {
                    ActiveProfile = new BuildLayout.Profile { Name = k_ProfileName }
                },
                AddressablesRuntimeSettings = new BuildLayout.AddressablesRuntimeData()
            };

            m_RegisteredBuild = layout.AddressablesBuildSessionGUID;
            m_LayoutSourcePath = Path.Combine(Path.GetTempPath(), $"testLayout_{m_RegisteredBuild}.json");
            layout.WriteToFile(m_LayoutSourcePath, false);

            AddressablesBuildHistorySupport.RegisterBuild(layout, m_LayoutSourcePath);
        }

        [TearDown]
        public void DeleteAddressablesBuild()
        {
            BuildHistory.DeleteHistory(new[] { m_RegisteredBuild });
            if (File.Exists(m_LayoutSourcePath))
                File.Delete(m_LayoutSourcePath);
        }

        BuildReportSummary RegisteredSummary()
        {
            Assert.IsTrue(BuildHistory.TryGetFilePath(m_RegisteredBuild, "BuildReportSummary.json", out string summaryPath));
            return BuildReportSummary.Load(summaryPath);
        }

        static AddressablesReportDrawer CreateDrawer()
        {
            return CreateDrawer(out _);
        }

        static AddressablesReportDrawer CreateDrawer(out VisualElement content)
        {
            var drawer = new AddressablesReportDrawer();
            content = drawer.CreateContent();
            return drawer;
        }

        static bool IsShown(VisualElement element) => element.style.display.value == DisplayStyle.Flex;

        static BuildReportSummary CreateSummary(GUID buildSessionGuid)
        {
            return new BuildReportSummary
            {
                BuildSessionGUID = buildSessionGuid,
                BuildType = UnityEditor.Build.Reporting.BuildType.AssetBundle
            };
        }

        [Test]
        public void EmbeddedToolbarAssets_CanBeLoaded()
        {
            Assert.IsNotNull(
                AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(BuildReportUtility.EmbeddedReportUxmlPath),
                "Could not load " + BuildReportUtility.EmbeddedReportUxmlPath);
            Assert.IsNotNull(
                AssetDatabase.LoadAssetAtPath<StyleSheet>(BuildReportUtility.BuildReportContentViewEmbeddedUssPath),
                "Could not load " + BuildReportUtility.BuildReportContentViewEmbeddedUssPath);
        }

        [Test]
        public void Drawer_DeclaresThePackageTheRegisteredBuildRecords()
        {
            var attribute = (ExternalBuildDrawerAttribute)Attribute.GetCustomAttribute(
                typeof(AddressablesReportDrawer), typeof(ExternalBuildDrawerAttribute));

            Assert.IsNotNull(attribute, "Without the attribute the Build Analysis window cannot route builds to the drawer.");
            Assert.AreEqual(AddressablesBuildHistorySupport.kPackageName, attribute.ProducerPackage);
            Assert.AreEqual(attribute.ProducerPackage, RegisteredSummary().ProducerPackage,
                "Routing only works while the registered build records the same package the drawer declares.");
        }

        [Test]
        public void ApplyBuild_ConsumesTheLayoutFromTheBuildHistoryEntry()
        {
            var drawer = CreateDrawer();
            BuildReportSummary summary = RegisteredSummary();

            Assert.IsTrue(drawer.ApplyBuild(summary, drawer.LoadBuildAsync(summary, CancellationToken.None).Result),
                "A shown report keeps the window's selection.");

            Assert.IsNotNull(drawer.ShownReport);
            Assert.AreEqual(k_ProfileName, drawer.ShownReport.AddressablesEditorSettings.ActiveProfile.Name);
        }

        [Test]
        public void ApplyBuild_SameBuildTwice_KeepsTheParsedLayout()
        {
            var drawer = CreateDrawer();
            BuildReportSummary summary = RegisteredSummary();

            drawer.ApplyBuild(summary, drawer.LoadBuildAsync(summary, CancellationToken.None).Result);
            BuildLayout first = drawer.ShownReport;
            drawer.ApplyBuild(summary, drawer.LoadBuildAsync(summary, CancellationToken.None).Result);

            Assert.AreSame(first, drawer.ShownReport, "Reselecting the same build should not parse the layout again.");
        }

        [Test]
        public void ApplyBuild_AfterDeselect_ParsesTheLayoutAgain()
        {
            var drawer = CreateDrawer();
            BuildReportSummary summary = RegisteredSummary();

            drawer.ApplyBuild(summary, drawer.LoadBuildAsync(summary, CancellationToken.None).Result);
            BuildLayout first = drawer.ShownReport;
            drawer.OnBuildDeselected();
            drawer.ApplyBuild(summary, drawer.LoadBuildAsync(summary, CancellationToken.None).Result);

            Assert.AreNotSame(first, drawer.ShownReport);
        }

        [Test]
        public void ApplyBuild_FailedBuildWithoutRuntimeSettings_ShowsTheReport()
        {
            // Matches the layout GenerateErrorReport registers for a failed build, which has no runtime settings.
            BuildLayout failedLayout = new BuildLayout
            {
                AddressablesBuildSessionGUID = GUID.Generate(),
                BuildTarget = BuildTarget.Android,
                BuildStart = DateTime.Now,
                BuildError = "Something went wrong",
                AddressablesEditorSettings = new BuildLayout.AddressablesEditorData
                {
                    ActiveProfile = new BuildLayout.Profile { Name = k_ProfileName }
                }
            };
            string failedLayoutPath = Path.Combine(Path.GetTempPath(), $"testLayout_{failedLayout.AddressablesBuildSessionGUID}.json");

            try
            {
                failedLayout.WriteToFile(failedLayoutPath, false);
                AddressablesBuildHistorySupport.RegisterBuild(failedLayout, failedLayoutPath);
                Assert.IsTrue(BuildHistory.TryGetFilePath(failedLayout.AddressablesBuildSessionGUID, "BuildReportSummary.json", out string summaryPath));

                var drawer = CreateDrawer();
                BuildReportSummary summary = BuildReportSummary.Load(summaryPath);
                drawer.ApplyBuild(summary, drawer.LoadBuildAsync(summary, CancellationToken.None).Result);

                Assert.IsNotNull(drawer.ShownReport);
                Assert.AreEqual("Something went wrong", drawer.ShownReport.BuildError);
            }
            finally
            {
                BuildHistory.DeleteHistory(new[] { failedLayout.AddressablesBuildSessionGUID });
                if (File.Exists(failedLayoutPath))
                    File.Delete(failedLayoutPath);
            }
        }

        [Test]
        public void LoadBuildAsync_BuildWithoutAnAddressablesLayout_Warns()
        {
            var drawer = CreateDrawer();
            BuildReportSummary summary = RegisteredSummary();
            drawer.ApplyBuild(summary, drawer.LoadBuildAsync(summary, CancellationToken.None).Result);
            BuildReportSummary missing = CreateSummary(GUID.Generate());

            LogAssert.Expect(LogType.Warning, new Regex("No Addressables build report was found"));
            Assert.IsFalse(drawer.ApplyBuild(missing, drawer.LoadBuildAsync(missing, CancellationToken.None).Result),
                "No report is shown, so selecting the build again retries.");
        }

        [Test]
        public void LoadBuildAsync_CorruptLayout_ErrorNamesTheFile()
        {
            Assert.IsTrue(BuildHistory.TryGetFilePath(m_RegisteredBuild, AddressablesBuildHistorySupport.kLayoutFileName, out string layoutPath));
            File.WriteAllText(layoutPath, "{ not json");

            var drawer = CreateDrawer();
            var e = Assert.Throws<AggregateException>(() => _ = drawer.LoadBuildAsync(RegisteredSummary(), CancellationToken.None).Result);

            StringAssert.Contains(layoutPath, e.InnerException.Message);
        }

        [Test]
        public void LoadBuildAsync_ShowsTheLoadingOverlayUntilTheReportIsApplied()
        {
            var drawer = CreateDrawer();
            Assume.That(drawer.LoadingOverlay, Is.Not.Null, "The embedded layout declares the overlay.");
            BuildReportSummary summary = RegisteredSummary();

            Task<object> load = drawer.LoadBuildAsync(summary, CancellationToken.None);

            Assert.IsTrue(IsShown(drawer.LoadingOverlay), "The read runs in the background, so the report says it is loading.");
            drawer.ApplyBuild(summary, load.Result);
            Assert.IsFalse(IsShown(drawer.LoadingOverlay));
            Assert.IsNotNull(drawer.ShownReport);
        }

        [Test]
        public void OnBuildDeselected_WhileLoading_HidesTheLoadingOverlay()
        {
            var drawer = CreateDrawer();
            Task<object> load = drawer.LoadBuildAsync(RegisteredSummary(), CancellationToken.None);

            drawer.OnBuildDeselected();

            Assert.IsFalse(IsShown(drawer.LoadingOverlay), "The window drops the result, so ApplyBuild never hides it.");

            // Nothing reads load.Result here, so wait for the background read before TearDown deletes the layout.
            load.Wait();
        }

        [Test]
        public void LoadBuildAsync_SameBuildAgain_DoesNotShowTheLoadingOverlay()
        {
            var drawer = CreateDrawer();
            BuildReportSummary summary = RegisteredSummary();
            drawer.ApplyBuild(summary, drawer.LoadBuildAsync(summary, CancellationToken.None).Result);

            Task<object> load = drawer.LoadBuildAsync(summary, CancellationToken.None);

            Assert.IsTrue(load.IsCompleted, "The shown report is reused, so there is nothing to read.");
            Assert.IsFalse(IsShown(drawer.LoadingOverlay));
        }

        [Test]
        public void OnBuildListRefreshed_DropsLinksToBuildsTheHistoryNoLongerHas()
        {
            var drawer = CreateDrawer(out VisualElement content);
            BuildReportSummary summary = RegisteredSummary();
            drawer.ApplyBuild(summary, drawer.LoadBuildAsync(summary, CancellationToken.None).Result);
            // The registered build has no related builds, so this link is stale.
            content.Q<RelatedBuildLinks>(BuildReportUtility.SummaryTabRelatedBuilds).SetBuilds(
                new[] { new BuildReportSummary { BuildSessionGUID = GUID.Generate(), BuildName = "Deleted" } });

            drawer.OnBuildListRefreshed();

            Assert.IsEmpty(content.Query<Label>(className: RelatedBuildLinks.linkUssClassName).ToList());
            Assert.AreEqual(MainPanelSummaryTabEmbedded.k_NoRelatedBuilds,
                content.Q<Label>(MainPanelSummaryTabEmbedded.DetailValueName("Related Builds")).text);
        }

        [Test]
        public void OnBuildListRefreshed_KeepsTheParsedLayout()
        {
            var drawer = CreateDrawer();
            BuildReportSummary summary = RegisteredSummary();
            drawer.ApplyBuild(summary, drawer.LoadBuildAsync(summary, CancellationToken.None).Result);
            BuildLayout shown = drawer.ShownReport;
            Assert.IsNotNull(shown);

            drawer.OnBuildListRefreshed();

            Assert.AreSame(shown, drawer.ShownReport, "Refreshing the row should not parse the layout again.");
        }

        [Test]
        public void OnBuildListRefreshed_AfterAFailedLoad_DoesNothing()
        {
            var drawer = CreateDrawer();
            LogAssert.Expect(LogType.Warning, new Regex("No Addressables build report was found"));
            BuildReportSummary missing = CreateSummary(GUID.Generate());
            drawer.ApplyBuild(missing, drawer.LoadBuildAsync(missing, CancellationToken.None).Result);

            Assert.DoesNotThrow(() => drawer.OnBuildListRefreshed());
            Assert.IsNull(drawer.ShownReport);
        }
    }
}
#endif
