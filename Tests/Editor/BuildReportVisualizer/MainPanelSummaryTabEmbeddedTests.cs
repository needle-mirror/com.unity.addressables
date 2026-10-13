#if ENABLE_BUILD_ANALYSIS_INTEGRATION
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets.Build.Layout;
using UnityEditor.AddressableAssets.BuildReportVisualizer;
using UnityEditor.Build;
using UnityEditor.Build.Analysis;
using UnityEngine;
using UnityEngine.UIElements;

namespace Tests.Editor.BuildReportVisualizer
{
    class PanelHostWindow : EditorWindow
    {
    }

    public class MainPanelSummaryTabEmbeddedTests
    {
        const string k_ImplicitGuid = "implicit-shared";

        VisualElement m_Root;
        BuildReportContentView m_View;

        [SetUp]
        public void CreateContentView()
        {
            m_Root = new VisualElement();
            m_View = new BuildReportContentView(true);
            m_View.CreateGUI(m_Root);
        }

        class HostWindow : EditorWindow { }

        static BuildLayout.Bundle CreateBundle(BuildLayout.Group group, string name, ulong fileSize, int explicitAssets, int implicitAssets)
        {
            var bundle = new BuildLayout.Bundle { Name = name, FileSize = fileSize };
            var file = new BuildLayout.File { Name = $"CAB-{name}", Bundle = bundle };
            bundle.Files.Add(file);

            for (int i = 0; i < explicitAssets; i++)
            {
                file.Assets.Add(new BuildLayout.ExplicitAsset
                {
                    Guid = $"{name}-explicit-{i}",
                    AssetPath = $"Assets/{name}/Prefab{i}.prefab",
                    Bundle = bundle,
                    File = file
                });
            }

            for (int i = 0; i < implicitAssets; i++)
            {
                file.OtherAssets.Add(new BuildLayout.DataFromOtherAsset
                {
                    AssetGuid = $"{name}-implicit-{i}",
                    AssetPath = $"Assets/{name}/Data{i}.asset",
                    File = file
                });
            }

            group.Bundles.Add(bundle);
            return bundle;
        }

        // 1 bundle, 1 Addressable and 3 pulled-in assets, so the percentages are 25% / 75%.
        static BuildLayout CreateLayout()
        {
            var group = new BuildLayout.Group { Name = "Default Local Group" };
            CreateBundle(group, "bundleA", 700, 1, 3);

            var layout = new BuildLayout
            {
                BuildTarget = BuildTarget.StandaloneWindows64,
                BuildStart = new DateTime(2026, 2, 9, 21, 23, 0),
                Duration = 1689,
                PackageVersion = "2.7.4",
                UnityVersion = "6000.3.0f1",
                LocalCatalogBuildPath = "Library/com.unity.addressables/aa/Windows/catalog.bin",
                AddressablesEditorSettings = new BuildLayout.AddressablesEditorData
                {
                    ActiveProfile = new BuildLayout.Profile { Name = "Default" }
                },
                AddressablesRuntimeSettings = new BuildLayout.AddressablesRuntimeData
                {
                    CatalogLoadPaths = new List<string> { "Library/com.unity.addressables/aa/Windows/catalog.bin" }
                }
            };
            layout.Groups.Add(group);
            return layout;
        }

        // One implicit asset pulled into both bundles, recorded in DuplicatedAssets the way a real
        // build writes it, so BuildReportHelperConsumer can resolve it.
        static BuildLayout CreateLayoutWithDuplicatedAsset()
        {
            BuildLayout layout = CreateLayout();
            BuildLayout.Bundle bundleA = layout.Groups[0].Bundles[0];
            BuildLayout.Bundle bundleB = CreateBundle(layout.Groups[0], "bundleB", 324, 1, 0);

            BuildLayout.File fileA = bundleA.Files[0];
            BuildLayout.File fileB = bundleB.Files[0];

            var duplicateFiles = new List<BuildLayout.File>();
            foreach (BuildLayout.File file in new[] { fileA, fileB })
            {
                var duplicated = new BuildLayout.DataFromOtherAsset
                {
                    AssetGuid = k_ImplicitGuid,
                    AssetPath = "Assets/Shared/Shared.asset",
                    File = file,
                    SerializedSize = 500,
                    Objects = new List<BuildLayout.ObjectData>
                    {
                        new BuildLayout.ObjectData { LocalIdentifierInFile = 1 }
                    }
                };

                BuildLayout.ExplicitAsset referencing = file.Assets[0];
                referencing.InternalReferencedOtherAssets.Add(duplicated);
                duplicated.ReferencingAssets.Add(referencing);
                duplicateFiles.Add(file);
            }

            layout.DuplicatedAssets.Add(new BuildLayout.AssetDuplicationData
            {
                AssetGuid = k_ImplicitGuid,
                DuplicatedObjects = new List<BuildLayout.ObjectDuplicationData>
                {
                    new BuildLayout.ObjectDuplicationData
                    {
                        LocalIdentifierInFile = 1,
                        IncludedInBundleFiles = duplicateFiles
                    }
                }
            });

            return layout;
        }

        // BuildHeader and StatCard are internal to the Editor, so tests find them by class.
        const string k_HeaderTitleClass = "build-header__title";
        const string k_HeaderSubtitleClass = "build-header__subtitle";
        const string k_HeaderTimeRangeClass = "build-header__time-range";
        const string k_StatusBadgeClass = "build-header__status";
        const string k_StatusTextClass = "build-header__status-text";
        const string k_StatusSuccessClass = "build-header__status--success";
        const string k_StatusFailedClass = "build-header__status--failed";
        const string k_DetailsLabelClass = "info-label";
        const string k_CardValueClass = "stat-card__value";

        string TextOf(string elementName) => m_Root.Q<Label>(elementName).text;

        VisualElement Header() => m_Root.Q<VisualElement>(BuildReportUtility.SummaryTabHeader);
        string HeaderText(string className) => Header().Q<Label>(className: className).text;
        VisualElement StatusBadge() => Header().Q<VisualElement>(className: k_StatusBadgeClass);
        string CardValue(string cardName) => m_Root.Q<VisualElement>(cardName).Q<Label>(className: k_CardValueClass).text;

        // The tree is not attached to a panel in these tests, so resolvedStyle is never computed.
        // Read the inline style that SetElementDisplay writes instead.
        DisplayStyle DisplayOf(string elementName) => m_Root.Q<VisualElement>(elementName).style.display.value;

        IEnumerable<string> DetailLabels() =>
            m_Root.Query<Label>(className: k_DetailsLabelClass).ToList()
                .Where(label => label.parent.style.display.value != DisplayStyle.None)
                .Select(label => label.text);

        string DetailValueFor(string label) =>
            m_Root.Q<Label>(MainPanelSummaryTabEmbedded.DetailValueName(label))?.text;

        [Test]
        public void Header_ShowsTheBuildNameTheHostSuppliedAndTheBuildIdentity()
        {
            m_View.Consume(CreateLayout(), "AssetBundle_myname", null);

            Assert.AreEqual("AssetBundle_myname", HeaderText(k_HeaderTitleClass));
            Assert.AreEqual("StandaloneWindows64 • Addressables AssetBundle Build",
                HeaderText(k_HeaderSubtitleClass));
            Assert.AreEqual("02/09/2026 • 21:23 to 21:51", HeaderText(k_HeaderTimeRangeClass));
        }

        [Test]
        public void Header_WithoutABuildNameFromTheHost_FallsBackToAGenericTitle()
        {
            m_View.Consume(CreateLayout(), null, null);

            Assert.AreEqual(MainPanelSummaryTabEmbedded.k_DefaultBuildName, HeaderText(k_HeaderTitleClass));
        }

        // IBuildReportHost only has the single-argument Consume, so it must fill the embedded tab.
        [Test]
        public void ConsumeThroughIBuildReportHost_FillsTheEmbeddedSummaryTab()
        {
            IBuildReportHost host = m_View;

            Assert.DoesNotThrow(() => host.Consume(CreateLayout()));

            Assert.AreEqual(MainPanelSummaryTabEmbedded.k_DefaultBuildName, HeaderText(k_HeaderTitleClass));
            AssertRelatedBuildsShowsNone();
        }

        [Test]
        public void Header_SuccessfulBuild_ShowsTheSuccessBadge()
        {
            m_View.Consume(CreateLayout(), null, null);

            VisualElement badge = StatusBadge();
            Assert.AreEqual("Success", HeaderText(k_StatusTextClass));
            Assert.IsTrue(badge.ClassListContains(k_StatusSuccessClass));
            Assert.IsFalse(badge.ClassListContains(k_StatusFailedClass));
        }

        [Test]
        public void Header_FailedBuild_ShowsTheFailureBadge()
        {
            BuildLayout layout = CreateLayout();
            layout.BuildError = "Something went wrong";

            m_View.Consume(layout, null, null);

            VisualElement badge = StatusBadge();
            Assert.AreEqual("Failure", HeaderText(k_StatusTextClass));
            Assert.IsTrue(badge.ClassListContains(k_StatusFailedClass));
            Assert.IsFalse(badge.ClassListContains(k_StatusSuccessClass));
        }

        [Test]
        public void StatCards_ShowBundleSizeDurationAndCounts()
        {
            m_View.Consume(CreateLayout(), null, null);

            Assert.AreEqual("700 B", CardValue(BuildReportUtility.SummaryTabStatCardBundleSize));
            Assert.AreEqual("28m 9s", CardValue(BuildReportUtility.SummaryTabStatCardDuration));
            Assert.AreEqual("1", CardValue(BuildReportUtility.SummaryTabStatCardBundles));
            Assert.AreEqual("4", CardValue(BuildReportUtility.SummaryTabStatCardAssets));
        }

        [Test]
        public void BuildDetails_ShowTheMockupRowsWithPercentagesAndCatalogInfo()
        {
            m_View.Consume(CreateLayout(), null, null);

            Assert.AreEqual("1 (25%)", DetailValueFor("Addressables"));
            Assert.AreEqual("3 (75%)", DetailValueFor("Assets pulled"));
            Assert.AreEqual("2.7.4", DetailValueFor("Package Version"));
            Assert.AreEqual("6000.3.0f1", DetailValueFor("Editor Version"));
            Assert.AreEqual("Default", DetailValueFor("Addressable Profile"));
            Assert.AreEqual("catalog.bin", DetailValueFor("Local Catalog(s)"));
            Assert.AreEqual(MainPanelSummaryTabEmbedded.k_NoRemoteCatalog, DetailValueFor("Remote Catalog Location"));
            Assert.AreEqual("Library/com.unity.addressables/aa/Windows/catalog.bin", DetailValueFor("Local Catalog Location"));
        }

        // BuildLayoutGenerationTask writes "<package name>: <version>"; the name is dropped.
        [TestCase("com.unity.addressables: 4.1.0", "4.1.0")]
        [TestCase("com.unity.addressables:4.1.0", "4.1.0")]
        [TestCase("2.7.4", "2.7.4")]
        [TestCase("", "")]
        [TestCase(null, null)]
        public void FormatPackageVersion_KeepsOnlyTheVersion(string stored, string expected)
        {
            Assert.AreEqual(expected, MainPanelSummaryTabEmbedded.FormatPackageVersion(stored));
        }

        [Test]
        public void BuildDetails_OmitOptionalRowsWhenTheReportHasNoValueForThem()
        {
            m_View.Consume(CreateLayout(), null, null);

            Assert.IsFalse(DetailLabels().Contains("Remote Catalog(s):"));
            Assert.IsFalse(DetailLabels().Contains("Player Build Version:"));
        }

        [Test]
        public void BuildDetails_ShowOptionalRowsWhenTheReportHasValuesForThem()
        {
            BuildLayout layout = CreateLayout();
            layout.PlayerBuildVersion = "0.1";
            layout.RemoteCatalogBuildPath = "ServerData/Windows/catalog.bin";
            layout.AddressablesRuntimeSettings.CatalogLoadPaths.Add("http://localhost/catalog.hash");

            m_View.Consume(layout, null, null);

            Assert.AreEqual("0.1", DetailValueFor("Player Build Version"));
            Assert.AreEqual("catalog.bin", DetailValueFor("Remote Catalog(s)"));
            Assert.AreEqual("ServerData/Windows/catalog.bin", DetailValueFor("Remote Catalog Location"));
        }

#if ENABLE_CONTENT_DIRECTORIES
        [Test]
        public void ContentDirectoriesBanner_AbsentWhenTheBuildHasNoContentDirectories()
        {
            m_View.Consume(CreateLayout(), null, null);

            Assert.Zero(m_Root.Q<VisualElement>(BuildReportUtility.SummaryTabContentDirectories).childCount);
        }

        [Test]
        public void ContentDirectoriesBanner_AbsentWhenEmbeddedEvenIfTheBuildHasContentDirectories()
        {
            BuildLayout layout = CreateLayout();
            layout.ContentDirectories.Add(new BuildLayout.ContentDirectory());

            m_View.Consume(layout, null, null);

            Assert.Zero(m_Root.Q<VisualElement>(BuildReportUtility.SummaryTabContentDirectories).childCount);
        }
#endif

        [Test]
        public void PotentialIssues_HiddenWhenTheBuildHasNoDuplicatedAssets()
        {
            m_View.Consume(CreateLayout(), null, null);

            Assert.AreEqual(DisplayStyle.None, DisplayOf(BuildReportUtility.SummaryTabPotentialIssues));
        }

        [Test]
        public void PotentialIssues_ShownWithTheDuplicateCountAndRecoverableSize()
        {
            m_View.Consume(CreateLayoutWithDuplicatedAsset(), null, null);

            Assert.AreEqual(DisplayStyle.Flex, DisplayOf(BuildReportUtility.SummaryTabPotentialIssues));
            Assert.AreEqual("1 Duplicate Assets were detected in the build.",
                TextOf(BuildReportUtility.SummaryTabIssuesMessage));
            StringAssert.Contains("500 B", TextOf(BuildReportUtility.SummaryTabIssuesDetail));
        }

        [Test]
        public void PotentialIssues_ViewButton_NavigatesToTheDuplicatedAssetsView()
        {
            var window = EditorWindow.GetWindow<HostWindow>();
            try
            {
                var root = new VisualElement();
                window.rootVisualElement.Add(root);

                var view = new BuildReportContentView(true);
                view.CreateGUI(root);
                view.Consume(CreateLayoutWithDuplicatedAsset(), null, null);

                Button viewButton = root.Q<Button>(BuildReportUtility.SummaryTabIssuesViewButton);
                using (var evt = NavigationSubmitEvent.GetPooled())
                {
                    evt.target = viewButton;
                    viewButton.SendEvent(evt);
                }

                Assert.AreEqual((int)RibbonTabType.PotentialIssues, view.TabView.selectedTabIndex);
            }
            finally
            {
                window.Close();
            }
        }

        static BuildReportSummary[] CreateRelatedBuilds(params string[] names) =>
            names.Select(name => new BuildReportSummary { BuildSessionGUID = GUID.Generate(), BuildName = name }).ToArray();

        bool RelatedBuildsRowShown() =>
            DetailLabels().Contains("Related Builds:");

        List<Label> RelatedBuildLinkLabels() =>
            m_Root.Query<Label>(className: RelatedBuildLinks.linkUssClassName).ToList();

        Label RelatedBuildsNoneLabel() =>
            m_Root.Q<Label>(MainPanelSummaryTabEmbedded.DetailValueName("Related Builds"));

        void AssertRelatedBuildsShowsNone()
        {
            Assert.IsTrue(RelatedBuildsRowShown());
            Assert.AreEqual(MainPanelSummaryTabEmbedded.k_NoRelatedBuilds, RelatedBuildsNoneLabel().text);
            Assert.AreEqual(DisplayStyle.Flex, RelatedBuildsNoneLabel().style.display.value);
            Assert.IsEmpty(RelatedBuildLinkLabels());
        }

        [Test]
        public void RelatedBuilds_ShowsNoneWhenTheHostSuppliesNone()
        {
            m_View.Consume(CreateLayout(), "AssetBundle_myname", null);

            AssertRelatedBuildsShowsNone();
        }

        [Test]
        public void RelatedBuilds_ShowALinkPerBuildTheHostSupplied()
        {
            m_View.Consume(CreateLayout(), "Addressables", CreateRelatedBuilds("Cards", "Content Directory"));

            Assert.IsTrue(RelatedBuildsRowShown());
            Assert.AreEqual(2, RelatedBuildLinkLabels().Count);
            Assert.AreEqual(DisplayStyle.None, RelatedBuildsNoneLabel().style.display.value);
        }

        [Test]
        public void RelatedBuilds_ConsumingABuildWithoutRelatedBuilds_ShowsNoneAgain()
        {
            m_View.Consume(CreateLayout(), "Addressables", CreateRelatedBuilds("Cards"));

            m_View.Consume(CreateLayout(), "Addressables", CreateRelatedBuilds());

            AssertRelatedBuildsShowsNone();
        }

        [Test]
        public void ConsumeTwice_DoesNotDuplicateTheBuildDetailRows()
        {
            m_View.Consume(CreateLayout(), null, null);
            int rowsAfterFirst = DetailLabels().Count();

            m_View.Consume(CreateLayout(), null, null);

            Assert.AreEqual(rowsAfterFirst, DetailLabels().Count());
        }

        [Test]
        public void ClearViews_BlanksTheHeaderAndHidesTheContent()
        {
            m_View.Consume(CreateLayout(), "AssetBundle_myname", null);

            m_View.ClearViews();

            Assert.IsEmpty(HeaderText(k_HeaderTitleClass));
            Assert.IsEmpty(DetailValueFor("Addressables"));
            Assert.IsEmpty(DetailValueFor("Local Catalog Location"));
            Assert.IsEmpty(RelatedBuildLinkLabels());
            Assert.AreEqual(Visibility.Hidden,
                m_Root.Q<ScrollView>(BuildReportUtility.SummaryTabScroll).style.visibility.value);
        }

        [Test]
        public void GetBuildTypeDescription_DistinguishesUpdateBuilds()
        {
            Assert.AreEqual("Addressables AssetBundle Build", MainPanelSummaryTabEmbedded.GetBuildTypeDescription(BuildType.NewBuild));
            Assert.AreEqual("Addressables Content Update Build", MainPanelSummaryTabEmbedded.GetBuildTypeDescription(BuildType.UpdateBuild));
        }

        [Test]
        public void FormatAssetCount_WithNoAssets_OmitsThePercentage()
        {
            Assert.AreEqual("0", MainPanelSummaryTabEmbedded.FormatAssetCount(0, 0));
        }
    }
}
#endif
