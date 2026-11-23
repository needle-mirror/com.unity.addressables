#if ENABLE_BUILD_ANALYSIS_INTEGRATION
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor.AddressableAssets.Build.Layout;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build;
using UnityEditor.Build.Analysis;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEngine;
using UnityEngine.UIElements;
using BuildResult = UnityEditor.Build.Reporting.BuildResult;

namespace UnityEditor.AddressableAssets.BuildReportVisualizer
{
    /// <summary>
    /// Draws the report's Summary tab with the anatomy of the Build Analysis window's Overview tab:
    /// a build header, a row of stat cards, a Build Details section and a potential issues card.
    /// </summary>
    /// <remarks>The header, stat cards and Build Details are Build Analysis' own elements, declared in SummaryTabEmbedded.uxml.</remarks>
    internal class MainPanelSummaryTabEmbedded : IAddressableView, IBuildReportConsumer
    {
        internal const string k_DefaultBuildName = "Addressables Build";
        internal const string k_NoRemoteCatalog = "No Remote Catalog Built";
        internal const string k_NoBuildSteps = "No build steps data is available for this build.";

        internal const string k_ContentDirectoryStepTooltip =
            "Substeps for " + BuildReportUtility.ContentDirectoryStepName + " are under Build Steps in the related " +
            "content directory build, which you can find in Build Details > Related Builds.";

        // The build's folder is a timestamp and a hash, so point at the menu item rather than at a path.
        internal static readonly string k_BuildTaskStepTooltip =
            "This is a build task. To see its substeps, right-click this build in the Build List, select " +
            $"\"{BuildAnalysisConstants.k_ShowInFileBrowserLabel}\", and open {BuildReportUtility.TepFileName}.";

        internal const string k_DetailValueNameFormat = "SummaryTabDetailValue_{0}";

        const string k_ContentDirectoriesBannerClass = "SummaryTabContentDirectoriesBanner";
        const string k_RelatedBuildsLabel = "Related Builds";
        internal const string k_NoRelatedBuilds = "None";

        public VisualElement tabRootElement;
        internal ScrollView scrollbarElement;

        IBuildReportHost m_Host;
        BuildReportHelperConsumer m_HelperConsumer;
        bool m_IsEmbedded;

        // Labels of the detail rows in SummaryTabEmbedded.uxml.
        static readonly string[] k_DetailLabels =
        {
            "Addressables", "Assets pulled", "Package Version", "Editor Version", "Player Build Version",
            "Addressable Profile", "Local Catalog(s)", "Remote Catalog(s)", "Remote Catalog Location", "Local Catalog Location",
        };

        BuildHeader m_Header;
        StatCard m_BundleSizeCard;
        StatCard m_DurationCard;
        StatCard m_BundlesCard;
        StatCard m_AssetsCard;
        BuildDetailsGrid m_DetailsGrid;
        BuildStepsElement m_BuildSteps;
        readonly Dictionary<string, Label> m_DetailValues = new Dictionary<string, Label>();
        Button m_ShowLocalCatalogButton;
        Label m_RelatedBuildsValue;
        RelatedBuildLinks m_RelatedBuilds;

        VisualElement m_ContentDirectories;
        VisualElement m_PotentialIssues;
        Label m_IssuesMessage;
        Label m_IssuesDetail;

        string m_LocalCatalogDirectory = string.Empty;

        internal MainPanelSummaryTabEmbedded(IBuildReportHost host, BuildReportHelperConsumer helperConsumer, bool isEmbedded)
        {
            m_HelperConsumer = helperConsumer;
            m_Host = host;
            m_IsEmbedded = isEmbedded;
        }

        public void CreateGUI(VisualElement rootVisualElement)
        {
            tabRootElement = rootVisualElement.Q<VisualElement>(BuildReportUtility.SummaryTab);

            // Theme values come from the Build Analysis window, so no per-skin sheet is needed.
            tabRootElement.styleSheets.Clear();
            tabRootElement.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(BuildReportUtility.SummaryTabEmbeddedUssPath));

            scrollbarElement = tabRootElement.Q<ScrollView>(BuildReportUtility.SummaryTabScroll);
            scrollbarElement.verticalScrollerVisibility = ScrollerVisibility.Auto;
            scrollbarElement.horizontalScrollerVisibility = ScrollerVisibility.Hidden;

            CreateLayout();

            m_ContentDirectories = tabRootElement.Q<VisualElement>(BuildReportUtility.SummaryTabContentDirectories);
            m_PotentialIssues = tabRootElement.Q<VisualElement>(BuildReportUtility.SummaryTabPotentialIssues);
            m_IssuesMessage = tabRootElement.Q<Label>(BuildReportUtility.SummaryTabIssuesMessage);
            m_IssuesDetail = tabRootElement.Q<Label>(BuildReportUtility.SummaryTabIssuesDetail);
            tabRootElement.Q<Image>(BuildReportUtility.SummaryTabIssuesIcon).image = BuildReportUtility.GetIcon("console.warnicon");
            tabRootElement.Q<Button>(BuildReportUtility.SummaryTabIssuesViewButton).clicked +=
                () => m_Host.NavigateToView(PotentialIssuesType.DuplicatedAssetsView);

            ClearGUI();
            tabRootElement.visible = false;
        }

        public void Consume(BuildLayout buildReport)
        {
            Consume(buildReport, null);
        }

        /// <param name="buildName">Name of the build as the host knows it, shown as the header
        /// title. Falls back to a generic title when the host has no name for the build.</param>
        /// <param name="relatedBuilds">Builds this one ran nested in or contains, shown as links that open
        /// them in the Build Analysis window. The row reads None when null or empty.</param>
        internal void Consume(BuildLayout buildReport, string buildName, IReadOnlyList<BuildReportSummary> relatedBuilds = null)
        {
            ClearGUI();

            BuildLayoutSummary summary = BuildLayoutSummary.GetSummaryWithoutAssetTypes(buildReport);

            BindHeader(buildReport, summary, buildName);
            BindStatCards(buildReport, summary);
            BindBuildDetails(buildReport, summary);
            BindRelatedBuilds(relatedBuilds ?? Array.Empty<BuildReportSummary>());
            BindBuildSteps(buildReport);
            BindContentDirectories(buildReport);
            BindPotentialIssues(buildReport);

            tabRootElement.visible = true;
            tabRootElement.MarkDirtyRepaint();
            scrollbarElement.visible = true;
            scrollbarElement.MarkDirtyRepaint();
        }

        /// <summary>Replaces the Related Builds row without reconsuming the report.</summary>
        internal void SetRelatedBuilds(IReadOnlyList<BuildReportSummary> relatedBuilds)
        {
            BindRelatedBuilds(relatedBuilds ?? Array.Empty<BuildReportSummary>());
        }

        public void ClearGUI()
        {
            m_ContentDirectories.Clear();
            ClearLayout();
            m_LocalCatalogDirectory = string.Empty;

            BuildReportUtility.SetElementDisplay(m_PotentialIssues, false);

            scrollbarElement.visible = false;
        }

        void CreateLayout()
        {
            m_Header = tabRootElement.Q<BuildHeader>(BuildReportUtility.SummaryTabHeader);
            m_BundleSizeCard = tabRootElement.Q<StatCard>(BuildReportUtility.SummaryTabStatCardBundleSize);
            m_DurationCard = tabRootElement.Q<StatCard>(BuildReportUtility.SummaryTabStatCardDuration);
            m_BundlesCard = tabRootElement.Q<StatCard>(BuildReportUtility.SummaryTabStatCardBundles);
            m_AssetsCard = tabRootElement.Q<StatCard>(BuildReportUtility.SummaryTabStatCardAssets);

            m_BuildSteps = tabRootElement.Q<BuildStepsElement>(BuildReportUtility.SummaryTabBuildSteps);
            m_BuildSteps.emptyStateText = k_NoBuildSteps;

            m_DetailsGrid = tabRootElement.Q<BuildDetailsGrid>(BuildReportUtility.SummaryTabDetailsGrid);
            m_DetailValues.Clear();
            foreach (string label in k_DetailLabels)
            {
                Label value = m_DetailsGrid.Q<Label>(DetailValueName(label));
                RegisterCopyTextToClipboardCallback(value);
                m_DetailValues.Add(label, value);
            }

            m_ShowLocalCatalogButton = m_DetailsGrid.Q<Button>(BuildReportUtility.SummaryTabShowLocalCatalogButton);
            m_ShowLocalCatalogButton.text = BuildAnalysisConstants.k_ShowInFileBrowserLabel;
            m_ShowLocalCatalogButton.clicked += OnShowLocalCatalogClicked;

            // The row shows one link per build, or its value label reading None when there are none.
            m_RelatedBuildsValue = m_DetailsGrid.Q<Label>(DetailValueName(k_RelatedBuildsLabel));
            m_RelatedBuilds = m_DetailsGrid.Q<RelatedBuildLinks>(BuildReportUtility.SummaryTabRelatedBuilds);
        }

        void ClearLayout()
        {
            // BuildHeader has no empty state. The tab is hidden while cleared, so blanking the text is enough.
            m_Header.Bind(new BuildEntry(), string.Empty);

            m_BundleSizeCard.value = string.Empty;
            m_DurationCard.value = string.Empty;
            m_BundlesCard.value = string.Empty;
            m_AssetsCard.value = string.Empty;

            m_BuildSteps.Bind(Array.Empty<BuildAnalysisStep>(), 0);

            foreach (string label in k_DetailLabels)
                SetDetail(label, string.Empty);
            m_ShowLocalCatalogButton.SetEnabled(false);
            BindRelatedBuilds(Array.Empty<BuildReportSummary>());
        }

        void BindHeader(BuildLayout report, BuildLayoutSummary summary, string buildName)
        {
            bool succeeded = string.IsNullOrEmpty(report.BuildError);
            m_Header.Bind(new BuildEntry
            {
                BuildName = string.IsNullOrEmpty(buildName) ? k_DefaultBuildName : buildName,
                Platform = report.BuildTarget,
                BuildResult = succeeded ? BuildResult.Succeeded : BuildResult.Failed,
                BuildStartedAt = report.BuildStart,
                TotalTimeMs = (long)Math.Round(report.Duration * 1000d),
            }, $"{report.BuildTarget} • {GetBuildTypeDescription(report, summary)}");
        }

        void BindStatCards(BuildLayout report, BuildLayoutSummary summary)
        {
            m_BundleSizeCard.value = BuildReportUtility.GetDenominatedBytesString(summary.BundleSummary.TotalCompressedSize);
            m_DurationCard.value = BuildReportUtility.GetDurationString(report.Duration);
            m_BundlesCard.value = summary.BundleSummary.Count.ToString();
            m_AssetsCard.value = summary.TotalAssetCount.ToString();
        }

        // The build wrote its steps beside its report, so there is nothing to parse here.
        void BindBuildSteps(BuildLayout report)
        {
            if (!BuildStepsReport.TryGetFilePath(report, out string path)
                || !BuildStepsReport.TryRead(path, out BuildStepData[] steps))
            {
                m_BuildSteps.Bind(Array.Empty<BuildAnalysisStep>(), 0);
                return;
            }

            double totalMs = report.Duration * 1000d;
            // Read during Bind, so it has to be set first.
            m_BuildSteps.stepTooltip = step => GetStepTooltip(steps, step);
            m_BuildSteps.Bind(ToAnalysisSteps(steps), (long)Math.Round(totalMs));
        }

        internal static string GetStepTooltip(BuildStepData[] steps, BuildAnalysisStep step)
        {
            if (step.Name == BuildReportUtility.ContentDirectoryStepName)
                return k_ContentDirectoryStepTooltip;

            // ToAnalysisSteps numbers steps by their index in this array.
            if (step.Id >= 0 && step.Id < steps.Length && steps[step.Id].IsBuildTask)
                return k_BuildTaskStepTooltip;

            return null;
        }

        static BuildAnalysisStep[] ToAnalysisSteps(BuildStepData[] steps)
        {
            var converted = new BuildAnalysisStep[steps.Length];
            for (int i = 0; i < steps.Length; i++)
            {
                converted[i] = new BuildAnalysisStep
                {
                    Id = i,
                    Name = steps[i].Name,
                    Depth = steps[i].Depth,
                    DurationMs = (long)Math.Round(steps[i].DurationMs)
                };
            }

            return converted;
        }

        void BindBuildDetails(BuildLayout report, BuildLayoutSummary summary)
        {
            SetDetail("Addressables", BuildReportUtility.FormatAssetCount(summary.ExplicitAssetCount, summary.TotalAssetCount));
            SetDetail("Assets pulled", BuildReportUtility.FormatAssetCount(summary.ImplicitAssetCount, summary.TotalAssetCount));
            SetDetail("Package Version", FormatPackageVersion(report.PackageVersion));
            SetDetail("Editor Version", report.UnityVersion);
            SetOptionalDetail("Player Build Version", report.PlayerBuildVersion);

            SetDetail("Addressable Profile", report.AddressablesEditorSettings?.ActiveProfile?.Name ?? "None");

            GetCatalogNames(report, out List<string> localCatalogNames, out List<string> remoteCatalogNames);
            SetOptionalDetail("Local Catalog(s)", string.Join(", ", localCatalogNames));
            SetOptionalDetail("Remote Catalog(s)", string.Join(", ", remoteCatalogNames));

            SetDetail("Remote Catalog Location",
                string.IsNullOrEmpty(report.RemoteCatalogBuildPath) ? k_NoRemoteCatalog : report.RemoteCatalogBuildPath);

            m_LocalCatalogDirectory = NormalizeToDirectory(report.LocalCatalogBuildPath);
            SetDetail("Local Catalog Location", report.LocalCatalogBuildPath);
            m_ShowLocalCatalogButton.SetEnabled(!string.IsNullOrEmpty(m_LocalCatalogDirectory));
        }

        void BindRelatedBuilds(IReadOnlyList<BuildReportSummary> relatedBuilds)
        {
            // With no related builds the links are empty, so the row's value label shows None instead.
            bool hasRelatedBuilds = relatedBuilds.Count > 0;
            m_RelatedBuilds.SetBuilds(relatedBuilds);
            m_RelatedBuildsValue.text = hasRelatedBuilds ? string.Empty : k_NoRelatedBuilds;
            m_RelatedBuildsValue.style.display = hasRelatedBuilds ? DisplayStyle.None : DisplayStyle.Flex;
        }

        void SetDetail(string label, string value)
        {
            Label valueElement = m_DetailValues[label];
            valueElement.text = value ?? string.Empty;
            valueElement.tooltip = value;
        }

        void SetOptionalDetail(string label, string value)
        {
            SetDetail(label, value);
            m_DetailsGrid.SetRowVisible(m_DetailValues[label], !string.IsNullOrEmpty(value));
        }

        void BindContentDirectories(BuildLayout report)
        {
#if ENABLE_CONTENT_DIRECTORIES
            // The banner links to Build Analysis; suppress it when this view is already embedded there.
            if (!m_IsEmbedded && report.ContentDirectories?.Count > 0)
                m_ContentDirectories.Add(CreateContentDirectoriesBanner(report.ContentDirectories[0].BuildSessionGUID));
#endif
        }

        void BindPotentialIssues(BuildLayout report)
        {
            if (report.DuplicatedAssets.Count == 0)
                return;

            ulong duplicatedSize = CalculateDuplicatedSize(m_HelperConsumer.GUIDToDuplicateAssets.Values);

            m_IssuesMessage.text = $"{report.DuplicatedAssets.Count} Duplicate Assets were detected in the build.";
            m_IssuesDetail.text = "Removing duplicated Assets could result in up to " +
                $"{BuildReportUtility.GetDenominatedBytesString(duplicatedSize)} reduced build size.";
            BuildReportUtility.SetElementDisplay(m_PotentialIssues, true);
        }

        internal static string DetailValueName(string label)
        {
            return string.Format(CultureInfo.InvariantCulture, k_DetailValueNameFormat, label);
        }

        void OnShowLocalCatalogClicked()
        {
            if (string.IsNullOrEmpty(m_LocalCatalogDirectory))
                return;

            EditorUtility.OpenWithDefaultApp(m_LocalCatalogDirectory);
        }

        // LocalCatalogBuildPath points at the catalog file itself, but the button opens the folder
        // that holds it.
        internal static string NormalizeToDirectory(string path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;

            return Directory.Exists(path) ? path : (Path.GetDirectoryName(path) ?? string.Empty);
        }

        // BuildLayoutGenerationTask records this as "<package name>: <version>"; the row is already
        // labelled, so drop the name. Anything else is shown as-is.
        internal static string FormatPackageVersion(string packageVersion)
        {
            if (string.IsNullOrEmpty(packageVersion))
                return packageVersion;

            int separator = packageVersion.IndexOf(':');
            if (separator < 0)
                return packageVersion;

            return packageVersion.Substring(separator + 1).Trim();
        }

        // What the build produced, which is not the same question as how it was built.
        internal static string GetBuildTypeDescription(BuildLayout report, BuildLayoutSummary summary)
        {
            if (report.BuildType == BuildType.UpdateBuild)
                return "Addressables Content Update Build";

            bool hasContentDirectories = report.ContentDirectories?.Count > 0;
            if (!hasContentDirectories)
                return "Addressables AssetBundle Build";

            return summary.BundleSummary.Count > 0
                ? "Addressables AssetBundle and Content Directory Build"
                : "Addressables Content Directory Build";
        }

        // Catalogs are listed by file name. A load path with a .hash extension belongs to a remote
        // catalog, whose file carries the catalog extension instead.
        internal static void GetCatalogNames(BuildLayout report, out List<string> localCatalogNames, out List<string> remoteCatalogNames)
        {
            var local = new HashSet<string>();
            var remote = new HashSet<string>();

            localCatalogNames = new List<string>();
            remoteCatalogNames = new List<string>();

            var catalogLoadPaths = report.AddressablesRuntimeSettings?.CatalogLoadPaths;
            if (catalogLoadPaths == null)
                return;

            string catalogFileExt = report.AddressablesEditorSettings?.EnableJsonCatalog == true ? ".json" : ".bin";

            foreach (var catalogPath in catalogLoadPaths)
            {
                string catalogFileName = Path.GetFileName(catalogPath);

                if (Path.GetExtension(catalogFileName).Equals(".hash"))
                    remote.Add(Path.ChangeExtension(catalogFileName, catalogFileExt));
                else
                    local.Add(catalogFileName);
            }

            localCatalogNames.AddRange(local);
            remoteCatalogNames.AddRange(remote);
        }

        void RegisterCopyTextToClipboardCallback(Label element)
        {
            element.RegisterCallback<ContextClickEvent>((args) =>
            {
                GenericMenu menu = new GenericMenu();
                menu.AddItem(new GUIContent("Copy"), false, () =>
                {
                    GUIUtility.systemCopyBuffer = element.text;
                });

                menu.ShowAsContext();
            });
        }

#if ENABLE_CONTENT_DIRECTORIES
        static HelpBox CreateContentDirectoriesBanner(GUID buildSessionGUID)
        {
            HelpBox banner = new HelpBox("This build includes content built using Content Directories. To view that content, use the Build Analysis window.", HelpBoxMessageType.Info);
            banner.AddToClassList(k_ContentDirectoriesBannerClass);
            banner.buttonText = "Open Build Analysis";
            banner.onButtonClicked += () => BuildReportUtility.OpenBuildAnalysisWindow(buildSessionGUID);
            banner.linkText = "Learn More";
            banner.linkHref = AddressableAssetUtility.GenerateContentDirectoriesDocsURL();
            return banner;
        }

#endif

        internal static ulong CalculateDuplicatedSize(IEnumerable<BuildReportHelperDuplicateImplicitAsset> duplicateAssets)
        {
            ulong duplicatedSize = 0;
            foreach (var dupeAsset in duplicateAssets)
            {
                if (dupeAsset.DuplicationCount > 1)
                    duplicatedSize += (ulong)(dupeAsset.DuplicationCount - 1) * (dupeAsset.Asset.SerializedSize + dupeAsset.Asset.StreamedSize);
            }
            return duplicatedSize;
        }
    }
}
#endif
