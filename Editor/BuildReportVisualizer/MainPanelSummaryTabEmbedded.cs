using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor.AddressableAssets.Build.Layout;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityEditor.AddressableAssets.BuildReportVisualizer
{
    /// <summary>
    /// Draws the report's Summary tab with the anatomy of the Build Analysis window's Overview tab:
    /// a build header, a row of stat cards, a Build Details section and a potential issues card.
    /// </summary>
    internal class MainPanelSummaryTabEmbedded : IAddressableView, IBuildReportConsumer
    {
        internal const string k_DefaultBuildName = "Addressables Build";
        internal const string k_NoRemoteCatalog = "No Remote Catalog Built";
        internal const string k_StatusSuccessClass = "SummaryTabStatusBadge--success";
        internal const string k_StatusFailedClass = "SummaryTabStatusBadge--failed";

        const string k_ContentDirectoriesBannerClass = "SummaryTabContentDirectoriesBanner";
        const string k_DetailsLabelClass = "SummaryTabDetailsLabel";
        const string k_DetailsValueClass = "SummaryTabDetailsValue";
        const string k_DetailsValueContainerClass = "SummaryTabDetailsValueContainer";
        const string k_DetailsActionButtonClass = "SummaryTabDetailsActionButton";

        public VisualElement tabRootElement;
        internal ScrollView scrollbarElement;

        IBuildReportHost m_Host;
        BuildReportHelperConsumer m_HelperConsumer;
        bool m_IsEmbedded;

        Label m_Title;
        Label m_Subtitle;
        Image m_PlatformIcon;
        VisualElement m_StatusBadge;
        Image m_StatusIcon;
        Label m_StatusText;
        Label m_TimeRange;

        Label m_TotalBundleSizeValue;
        Label m_BuildDurationValue;
        Label m_BundleCountValue;
        Label m_AssetCountValue;

        VisualElement m_DetailsLeftLabels;
        VisualElement m_DetailsLeftValues;
        VisualElement m_DetailsRightLabels;
        VisualElement m_DetailsRightValues;

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

            // Clear the style sheets defined in the template uxml file so they can be applied from
            // here in the order of: 1. base, 2. theming.
            tabRootElement.styleSheets.Clear();
            tabRootElement.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(BuildReportUtility.SummaryTabEmbeddedUssPath));
            tabRootElement.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(EditorGUIUtility.isProSkin
                ? BuildReportUtility.SummaryTabEmbeddedDarkUssPath
                : BuildReportUtility.SummaryTabEmbeddedLightUssPath));

            scrollbarElement = tabRootElement.Q<ScrollView>(BuildReportUtility.SummaryTabScroll);
            scrollbarElement.verticalScrollerVisibility = ScrollerVisibility.Auto;
            scrollbarElement.horizontalScrollerVisibility = ScrollerVisibility.Hidden;

            m_Title = tabRootElement.Q<Label>(BuildReportUtility.SummaryTabHeaderTitle);
            m_Subtitle = tabRootElement.Q<Label>(BuildReportUtility.SummaryTabHeaderSubtitle);
            m_PlatformIcon = tabRootElement.Q<Image>(BuildReportUtility.SummaryTabHeaderPlatformIcon);
            m_StatusBadge = tabRootElement.Q<VisualElement>(BuildReportUtility.SummaryTabStatusBadge);
            m_StatusIcon = tabRootElement.Q<Image>(BuildReportUtility.SummaryTabStatusBadgeIcon);
            m_StatusText = tabRootElement.Q<Label>(BuildReportUtility.SummaryTabStatusBadgeText);
            m_TimeRange = tabRootElement.Q<Label>(BuildReportUtility.SummaryTabHeaderTimeRange);

            m_TotalBundleSizeValue = tabRootElement.Q<Label>(BuildReportUtility.SummaryTabTotalBundleSizeValue);
            m_BuildDurationValue = tabRootElement.Q<Label>(BuildReportUtility.SummaryTabBuildDurationValue);
            m_BundleCountValue = tabRootElement.Q<Label>(BuildReportUtility.SummaryTabBundleCountValue);
            m_AssetCountValue = tabRootElement.Q<Label>(BuildReportUtility.SummaryTabAssetCountValue);

            m_DetailsLeftLabels = tabRootElement.Q<VisualElement>(BuildReportUtility.SummaryTabDetailsLeftLabels);
            m_DetailsLeftValues = tabRootElement.Q<VisualElement>(BuildReportUtility.SummaryTabDetailsLeftValues);
            m_DetailsRightLabels = tabRootElement.Q<VisualElement>(BuildReportUtility.SummaryTabDetailsRightLabels);
            m_DetailsRightValues = tabRootElement.Q<VisualElement>(BuildReportUtility.SummaryTabDetailsRightValues);

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
        internal void Consume(BuildLayout buildReport, string buildName)
        {
            ClearGUI();

            BuildLayoutSummary summary = BuildLayoutSummary.GetSummaryWithoutAssetTypes(buildReport);

            BindHeader(buildReport, buildName);
            BindStatCards(buildReport, summary);
            BindBuildDetails(buildReport, summary);
            BindContentDirectories(buildReport);
            BindPotentialIssues(buildReport);

            tabRootElement.visible = true;
            tabRootElement.MarkDirtyRepaint();
            scrollbarElement.visible = true;
            scrollbarElement.MarkDirtyRepaint();
        }

        public void ClearGUI()
        {
            m_ContentDirectories.Clear();

            m_Title.text = string.Empty;
            m_Subtitle.text = string.Empty;
            m_PlatformIcon.image = null;
            m_StatusText.text = string.Empty;
            m_StatusIcon.image = null;
            m_StatusBadge.RemoveFromClassList(k_StatusSuccessClass);
            m_StatusBadge.RemoveFromClassList(k_StatusFailedClass);
            m_TimeRange.text = string.Empty;

            m_TotalBundleSizeValue.text = string.Empty;
            m_BuildDurationValue.text = string.Empty;
            m_BundleCountValue.text = string.Empty;
            m_AssetCountValue.text = string.Empty;

            m_DetailsLeftLabels.Clear();
            m_DetailsLeftValues.Clear();
            m_DetailsRightLabels.Clear();
            m_DetailsRightValues.Clear();
            m_LocalCatalogDirectory = string.Empty;

            BuildReportUtility.SetElementDisplay(m_PotentialIssues, false);

            scrollbarElement.visible = false;
        }

        void BindHeader(BuildLayout report, string buildName)
        {
            m_Title.text = string.IsNullOrEmpty(buildName) ? k_DefaultBuildName : buildName;
            m_Subtitle.text = $"{report.BuildTarget} • {GetBuildTypeDescription(report.BuildType)}";

            Texture2D platformIcon = BuildReportUtility.GetPlatformIcon(report.BuildTarget);
            m_PlatformIcon.image = platformIcon;
            BuildReportUtility.SetElementDisplay(m_PlatformIcon, platformIcon != null);

            bool succeeded = string.IsNullOrEmpty(report.BuildError);
            m_StatusText.text = succeeded ? "Success" : "Failure";
            m_StatusIcon.image = BuildReportUtility.GetBuildStatusIcon(succeeded);
            m_StatusBadge.EnableInClassList(k_StatusSuccessClass, succeeded);
            m_StatusBadge.EnableInClassList(k_StatusFailedClass, !succeeded);
            m_StatusBadge.tooltip = succeeded ? string.Empty : report.BuildError;

            m_TimeRange.text = FormatTimeRange(report.BuildStart, report.Duration);
        }

        void BindStatCards(BuildLayout report, BuildLayoutSummary summary)
        {
            m_TotalBundleSizeValue.text = BuildReportUtility.GetDenominatedBytesString(summary.BundleSummary.TotalCompressedSize);
            m_BuildDurationValue.text = BuildReportUtility.GetDurationString(report.Duration);
            m_BundleCountValue.text = summary.BundleSummary.Count.ToString();
            m_AssetCountValue.text = summary.TotalAssetCount.ToString();
        }

        void BindBuildDetails(BuildLayout report, BuildLayoutSummary summary)
        {
            AddDetailRow(m_DetailsLeftLabels, m_DetailsLeftValues, "Addressables",
                FormatAssetCount(summary.ExplicitAssetCount, summary.TotalAssetCount));
            AddDetailRow(m_DetailsLeftLabels, m_DetailsLeftValues, "Assets pulled",
                FormatAssetCount(summary.ImplicitAssetCount, summary.TotalAssetCount));
            AddDetailRow(m_DetailsLeftLabels, m_DetailsLeftValues, "Package Version",
                FormatPackageVersion(report.PackageVersion));
            AddDetailRow(m_DetailsLeftLabels, m_DetailsLeftValues, "Editor Version", report.UnityVersion);
            if (!string.IsNullOrEmpty(report.PlayerBuildVersion))
                AddDetailRow(m_DetailsLeftLabels, m_DetailsLeftValues, "Player Build Version", report.PlayerBuildVersion);

            AddDetailRow(m_DetailsRightLabels, m_DetailsRightValues, "Addressable Profile",
                report.AddressablesEditorSettings?.ActiveProfile?.Name ?? "None");

            GetCatalogNames(report, out List<string> localCatalogNames, out List<string> remoteCatalogNames);
            if (localCatalogNames.Count > 0)
                AddDetailRow(m_DetailsRightLabels, m_DetailsRightValues, "Local Catalog(s)", string.Join(", ", localCatalogNames));
            if (remoteCatalogNames.Count > 0)
                AddDetailRow(m_DetailsRightLabels, m_DetailsRightValues, "Remote Catalog(s)", string.Join(", ", remoteCatalogNames));

            AddDetailRow(m_DetailsRightLabels, m_DetailsRightValues, "Remote Catalog Location",
                string.IsNullOrEmpty(report.RemoteCatalogBuildPath) ? k_NoRemoteCatalog : report.RemoteCatalogBuildPath);

            m_LocalCatalogDirectory = NormalizeToDirectory(report.LocalCatalogBuildPath);
            AddDetailRow(m_DetailsRightLabels, m_DetailsRightValues, "Local Catalog Location",
                report.LocalCatalogBuildPath, "Show in Explorer", OnShowLocalCatalogClicked,
                !string.IsNullOrEmpty(m_LocalCatalogDirectory));
        }

        void BindContentDirectories(BuildLayout report)
        {
#if ENABLE_CONTENT_DIRECTORIES
            // The banner links to Build Analysis; suppress it when this view is already embedded there.
            if (!m_IsEmbedded && report.ContentDirectories?.Count > 0)
                m_ContentDirectories.Add(CreateContentDirectoriesBanner());
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

        void AddDetailRow(VisualElement labelColumn, VisualElement valueColumn, string label, string value)
        {
            AddDetailRow(labelColumn, valueColumn, label, value, null, null, false);
        }

        void AddDetailRow(VisualElement labelColumn, VisualElement valueColumn, string label, string value,
            string buttonText, Action buttonAction, bool buttonEnabled)
        {
            var labelElement = new Label($"{label}:");
            labelElement.AddToClassList(k_DetailsLabelClass);
            labelColumn.Add(labelElement);

            var valueElement = new Label(value ?? string.Empty);
            valueElement.AddToClassList(k_DetailsValueClass);
            valueElement.tooltip = value;
            valueElement.selection.isSelectable = true;
            RegisterCopyTextToClipboardCallback(valueElement);

            if (buttonText == null)
            {
                valueColumn.Add(valueElement);
                return;
            }

            var container = new VisualElement();
            container.AddToClassList(k_DetailsValueContainerClass);
            container.Add(valueElement);

            var button = new Button(buttonAction) { text = buttonText };
            button.AddToClassList(k_DetailsActionButtonClass);
            button.SetEnabled(buttonEnabled);
            container.Add(button);

            valueColumn.Add(container);
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

        internal static string FormatAssetCount(int count, int totalCount)
        {
            if (totalCount <= 0)
                return count.ToString();

            float percentage = ((float)count / totalCount) * 100f;
            return $"{count} ({percentage.ToString("0.##", CultureInfo.InvariantCulture)}%)";
        }

        internal static string FormatTimeRange(DateTime buildStart, double durationSeconds)
        {
            if (buildStart == DateTime.MinValue)
                return "Unknown";

            DateTime endTime = buildStart.AddSeconds(Math.Max(0, durationSeconds));
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0:MM/dd/yyyy} • {0:HH:mm} to {1:HH:mm}",
                buildStart,
                endTime);
        }

        internal static string GetBuildTypeDescription(BuildType buildType)
        {
            return buildType == BuildType.UpdateBuild
                ? "Addressables Content Update Build"
                : "Addressables AssetBundle Build";
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
        static HelpBox CreateContentDirectoriesBanner()
        {
            HelpBox banner = new HelpBox("This build includes content built using Content Directories. To view that content, use the Build Analysis window.", HelpBoxMessageType.Info);
            banner.AddToClassList(k_ContentDirectoriesBannerClass);
            banner.buttonText = "Open Build Analysis";
            banner.onButtonClicked += BuildReportUtility.OpenBuildAnalysisWindow;
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
