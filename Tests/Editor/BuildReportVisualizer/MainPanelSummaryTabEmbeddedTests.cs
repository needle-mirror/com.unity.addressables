#if ENABLE_BUILD_HISTORY_EXTERNAL_BUILDS
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets.Build.Layout;
using UnityEditor.AddressableAssets.BuildReportVisualizer;
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

        string TextOf(string elementName) => m_Root.Q<Label>(elementName).text;

        // The tree is not attached to a panel in these tests, so resolvedStyle is never computed.
        // Read the inline style that SetElementDisplay writes instead.
        DisplayStyle DisplayOf(string elementName) => m_Root.Q<VisualElement>(elementName).style.display.value;

        IEnumerable<string> DetailLabels() =>
            m_Root.Q<VisualElement>(BuildReportUtility.SummaryTabDetailsLeftLabels).Children()
                .Concat(m_Root.Q<VisualElement>(BuildReportUtility.SummaryTabDetailsRightLabels).Children())
                .Cast<Label>()
                .Select(label => label.text);

        string DetailValueFor(string label)
        {
            foreach ((string labels, string values) in new[]
            {
                (BuildReportUtility.SummaryTabDetailsLeftLabels, BuildReportUtility.SummaryTabDetailsLeftValues),
                (BuildReportUtility.SummaryTabDetailsRightLabels, BuildReportUtility.SummaryTabDetailsRightValues)
            })
            {
                List<VisualElement> labelColumn = m_Root.Q<VisualElement>(labels).Children().ToList();
                int index = labelColumn.FindIndex(element => ((Label)element).text == $"{label}:");
                if (index < 0)
                    continue;

                VisualElement value = m_Root.Q<VisualElement>(values).Children().ElementAt(index);
                return (value as Label ?? value.Q<Label>()).text;
            }

            return null;
        }

        [Test]
        public void Header_ShowsTheBuildNameTheHostSuppliedAndTheBuildIdentity()
        {
            m_View.Consume(CreateLayout(), "AssetBundle_myname");

            Assert.AreEqual("AssetBundle_myname", TextOf(BuildReportUtility.SummaryTabHeaderTitle));
            Assert.AreEqual("StandaloneWindows64 • Addressables AssetBundle Build",
                TextOf(BuildReportUtility.SummaryTabHeaderSubtitle));
            Assert.AreEqual("02/09/2026 • 21:23 to 21:51", TextOf(BuildReportUtility.SummaryTabHeaderTimeRange));
        }

        [Test]
        public void Header_WithoutABuildNameFromTheHost_FallsBackToAGenericTitle()
        {
            m_View.Consume(CreateLayout());

            Assert.AreEqual(MainPanelSummaryTabEmbedded.k_DefaultBuildName, TextOf(BuildReportUtility.SummaryTabHeaderTitle));
        }

        [Test]
        public void Header_SuccessfulBuild_ShowsTheSuccessBadge()
        {
            m_View.Consume(CreateLayout());

            VisualElement badge = m_Root.Q<VisualElement>(BuildReportUtility.SummaryTabStatusBadge);
            Assert.AreEqual("Success", TextOf(BuildReportUtility.SummaryTabStatusBadgeText));
            Assert.IsTrue(badge.ClassListContains(MainPanelSummaryTabEmbedded.k_StatusSuccessClass));
            Assert.IsFalse(badge.ClassListContains(MainPanelSummaryTabEmbedded.k_StatusFailedClass));
        }

        [Test]
        public void Header_FailedBuild_ShowsTheFailureBadgeAndTheErrorAsATooltip()
        {
            BuildLayout layout = CreateLayout();
            layout.BuildError = "Something went wrong";

            m_View.Consume(layout);

            VisualElement badge = m_Root.Q<VisualElement>(BuildReportUtility.SummaryTabStatusBadge);
            Assert.AreEqual("Failure", TextOf(BuildReportUtility.SummaryTabStatusBadgeText));
            Assert.IsTrue(badge.ClassListContains(MainPanelSummaryTabEmbedded.k_StatusFailedClass));
            Assert.IsFalse(badge.ClassListContains(MainPanelSummaryTabEmbedded.k_StatusSuccessClass));
            Assert.AreEqual("Something went wrong", badge.tooltip);
        }

        [Test]
        public void StatCards_ShowBundleSizeDurationAndCounts()
        {
            m_View.Consume(CreateLayout());

            Assert.AreEqual("700 B", TextOf(BuildReportUtility.SummaryTabTotalBundleSizeValue));
            Assert.AreEqual("28m 9s", TextOf(BuildReportUtility.SummaryTabBuildDurationValue));
            Assert.AreEqual("1", TextOf(BuildReportUtility.SummaryTabBundleCountValue));
            Assert.AreEqual("4", TextOf(BuildReportUtility.SummaryTabAssetCountValue));
        }

        [Test]
        public void BuildDetails_ShowTheMockupRowsWithPercentagesAndCatalogInfo()
        {
            m_View.Consume(CreateLayout());

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
            m_View.Consume(CreateLayout());

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

            m_View.Consume(layout);

            Assert.AreEqual("0.1", DetailValueFor("Player Build Version"));
            Assert.AreEqual("catalog.bin", DetailValueFor("Remote Catalog(s)"));
            Assert.AreEqual("ServerData/Windows/catalog.bin", DetailValueFor("Remote Catalog Location"));
        }

#if ENABLE_CONTENT_DIRECTORIES
        [Test]
        public void ContentDirectoriesBanner_AbsentWhenTheBuildHasNoContentDirectories()
        {
            m_View.Consume(CreateLayout());

            Assert.Zero(m_Root.Q<VisualElement>(BuildReportUtility.SummaryTabContentDirectories).childCount);
        }

        [Test]
        public void ContentDirectoriesBanner_AbsentWhenEmbeddedEvenIfTheBuildHasContentDirectories()
        {
            BuildLayout layout = CreateLayout();
            layout.ContentDirectories.Add(new BuildLayout.ContentDirectory());

            m_View.Consume(layout);

            Assert.Zero(m_Root.Q<VisualElement>(BuildReportUtility.SummaryTabContentDirectories).childCount);
        }
#endif

        [Test]
        public void PotentialIssues_HiddenWhenTheBuildHasNoDuplicatedAssets()
        {
            m_View.Consume(CreateLayout());

            Assert.AreEqual(DisplayStyle.None, DisplayOf(BuildReportUtility.SummaryTabPotentialIssues));
        }

        [Test]
        public void PotentialIssues_ShownWithTheDuplicateCountAndRecoverableSize()
        {
            m_View.Consume(CreateLayoutWithDuplicatedAsset());

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
                view.Consume(CreateLayoutWithDuplicatedAsset());

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

        [Test]
        public void ConsumeTwice_DoesNotDuplicateTheBuildDetailRows()
        {
            m_View.Consume(CreateLayout());
            int rowsAfterFirst = DetailLabels().Count();

            m_View.Consume(CreateLayout());

            Assert.AreEqual(rowsAfterFirst, DetailLabels().Count());
        }

        [Test]
        public void ClearViews_BlanksTheHeaderAndHidesTheContent()
        {
            m_View.Consume(CreateLayout(), "AssetBundle_myname");

            m_View.ClearViews();

            Assert.IsEmpty(TextOf(BuildReportUtility.SummaryTabHeaderTitle));
            Assert.IsEmpty(DetailLabels());
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
        public void FormatTimeRange_WithoutABuildStart_ReportsUnknown()
        {
            Assert.AreEqual("Unknown", MainPanelSummaryTabEmbedded.FormatTimeRange(DateTime.MinValue, 12));
        }

        [Test]
        public void FormatAssetCount_WithNoAssets_OmitsThePercentage()
        {
            Assert.AreEqual("0", MainPanelSummaryTabEmbedded.FormatAssetCount(0, 0));
        }
    }
}
#endif
