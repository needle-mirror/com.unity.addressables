using System;
using System.Collections.Generic;
using UnityEditor.AddressableAssets.Build.Layout;
using UnityEditor.AddressableAssets.GUIElements;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using static UnityEditor.AddressableAssets.BuildReportVisualizer.ContentView;

namespace UnityEditor.AddressableAssets.BuildReportVisualizer
{
    /// <summary>
    /// The report content shared by the standalone Addressables Report window and embedding
    /// hosts: the Summary/Explore/Potential Issues tabs plus the details pane.
    /// </summary>
    [Serializable]
    class BuildReportContentView : IBuildReportHost, IAddressableView
    {
        const int k_DetailsPaneIndex = 1;
        const string k_ReportWindowDocsPage = "addressables-report-window.html";

        [SerializeField]
        ContentViewType m_ActiveContentViewType = ContentViewType.BundleView;

        [SerializeField]
        PotentialIssuesType m_ActivePotentialIssuesViewType = PotentialIssuesType.DuplicatedAssetsView;

        [SerializeField]
        bool m_IsEmbedded;

        BuildLayout m_BuildReport;

        VisualElement m_Root;

        TwoPaneSplitView m_ContentSplit;

        bool? m_DetailsPaneVisible;
        bool? m_AppliedDetailsPaneVisible;
        bool m_HasLaidOut;

        ContentView m_ActiveContentView;
        // Exactly one of these is built, picked by m_IsEmbedded.
        MainPanelSummaryTab m_MainPanelSummaryTab;
        MainPanelSummaryTabEmbedded m_MainPanelSummaryTabEmbedded;
        BuildReportHelperConsumer m_HelperConsumer;
        DetailsView m_DetailsView;

        // Roots of each tab's content, indexed by RibbonTabType. The Explore and Potential Issues
        // templates both contain elements named ContentView and SearchField, and both strips keep
        // inactive tabs in the visual tree, so views must be queried against their own tab.
        VisualElement[] m_TabContents = new VisualElement[3];
        // The standalone window keeps master's Ribbon; the embedded report uses the hoisted TabView.
        Ribbon m_TabsRibbon;
        TabView m_TabView;
        ToolbarToggle m_DetailsToggle;
        int m_CurrentTab;

        DropdownField m_ContentViewTypeDropdown;
        DropdownField m_PotentialIssuesViewTypeDropdown;
        int m_PreviousContentDropDownValue;
        int m_PreviousPotentialIssuesDropDownValue;

        Dictionary<ContentViewType, ContentView> m_CachedContentViews = new Dictionary<ContentViewType, ContentView>();
        Dictionary<PotentialIssuesType, ContentView> m_CachedPotentialIssuesViews = new Dictionary<PotentialIssuesType, ContentView>();

        static List<string> s_ContentViewTypes = new List<string>()
        {
            "AssetBundles",
            "Assets",
            "Labels",
            "Groups"
        };

        static List<string> s_PotentialIssuesViewTypes = new List<string>()
        {
            "Duplicated Assets"
        };

        public ContentView ActiveContentView => m_ActiveContentView;

        internal BuildLayout BuildReport => m_BuildReport;

        internal bool DetailsPaneVisible => m_DetailsPaneVisible ?? true;

        internal TabView TabView => m_TabView;

        public BuildReportContentView()
        {
        }

        public BuildReportContentView(bool isEmbedded)
        {
            m_IsEmbedded = isEmbedded;
        }

        public void CreateGUI(VisualElement rootVisualElement)
        {
            string uxmlPath = m_IsEmbedded
                ? BuildReportUtility.BuildReportContentViewEmbeddedUxmlPath
                : BuildReportUtility.BuildReportContentViewUxmlPath;
            var contentViewTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
            contentViewTree.CloneTree(rootVisualElement);

            // Embedded hoists the tab strip above the splitter, so its root has to cover both.
            m_ContentSplit = rootVisualElement.Q<TwoPaneSplitView>(BuildReportUtility.MiddleRightPaneSplitter);
            m_Root = m_IsEmbedded ? rootVisualElement.Q<VisualElement>(BuildReportUtility.ReportContentRoot) : m_ContentSplit;
            m_HasLaidOut = false;
            m_AppliedDetailsPaneVisible = null;
            m_ContentSplit.RegisterCallback<GeometryChangedEvent>(OnFirstGeometry);
            m_Root.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(BuildReportUtility.BuildReportContentViewUssPath));
            if (m_IsEmbedded)
                m_Root.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(BuildReportUtility.BuildReportContentViewEmbeddedUssPath));

            // Resolve the tabs first: the views below are created against their own tab's root.
            m_TabContents[(int)RibbonTabType.SummaryTab] = m_Root.Q<VisualElement>(BuildReportUtility.SummaryTab);
            m_TabContents[(int)RibbonTabType.ContentTab] = m_Root.Q<VisualElement>(BuildReportUtility.ContentTab);
            m_TabContents[(int)RibbonTabType.PotentialIssues] = m_Root.Q<VisualElement>(BuildReportUtility.PotentialIssuesTab);

            if (m_IsEmbedded)
            {
                m_TabView = m_Root.Q<TabView>(BuildReportUtility.TabsView);
                m_CurrentTab = Mathf.Max(0, m_TabView.selectedTabIndex);
            }
            else
            {
                m_TabsRibbon = m_Root.Q<Ribbon>(BuildReportUtility.TabsRibbon);
                m_CurrentTab = m_TabsRibbon.InitialOption;
            }
            ShowTabContent(m_CurrentTab);

            m_HelperConsumer = new BuildReportHelperConsumer();
            m_DetailsView = new DetailsView(this);
            m_ActiveContentView = new BundlesContentView(m_HelperConsumer, m_DetailsView);
            m_CachedContentViews.Clear();
            m_CachedPotentialIssuesViews.Clear();

            // Create panels
            m_ActiveContentView.CreateGUI(m_TabContents[(int)RibbonTabType.ContentTab]);
            m_DetailsView.CreateGUI(m_Root);
            if (m_IsEmbedded)
            {
                m_MainPanelSummaryTabEmbedded = new MainPanelSummaryTabEmbedded(this, m_HelperConsumer, m_IsEmbedded);
                m_MainPanelSummaryTabEmbedded.CreateGUI(m_Root);
            }
            else
            {
                m_MainPanelSummaryTab = new MainPanelSummaryTab(this, m_HelperConsumer, m_IsEmbedded);
                m_MainPanelSummaryTab.CreateGUI(m_Root);
            }

            m_ActiveContentView.ItemsSelected += m_DetailsView.OnSelected;

            // Implement view type dropdown
            m_ContentViewTypeDropdown = m_TabContents[(int)RibbonTabType.ContentTab].Q<DropdownField>(BuildReportUtility.ContentViewTypeDropdown);
            m_ContentViewTypeDropdown.choices = s_ContentViewTypes;
            m_ContentViewTypeDropdown.index = (int)m_ActiveContentViewType;
            m_PreviousContentDropDownValue = m_ContentViewTypeDropdown.index;
            m_ContentViewTypeDropdown.RegisterValueChangedCallback(OnViewDropDownChanged);

            m_PreviousPotentialIssuesDropDownValue = 0;
            m_PotentialIssuesViewTypeDropdown = m_TabContents[(int)RibbonTabType.PotentialIssues].Q<DropdownField>(BuildReportUtility.PotentialIssuesDropdown);
            m_PotentialIssuesViewTypeDropdown.choices = s_PotentialIssuesViewTypes;
            m_PotentialIssuesViewTypeDropdown.RegisterValueChangedCallback(OnViewDropDownChanged);

            if (m_IsEmbedded)
            {
                m_TabView.activeTabChanged += OnActiveTabChanged;

#if ENABLE_BUILD_HISTORY_EXTERNAL_BUILDS
                // Behind the define because viewportUssClassName only exists from 6000.3, and the
                // package supports 6000.0. Nothing is lost: m_IsEmbedded needs the define anyway.
                VisualElement tabStrip = m_TabView.Q<VisualElement>(className: TabView.viewportUssClassName);
                if (tabStrip == null)
                    throw new InvalidOperationException($"TabView content viewport .{TabView.viewportUssClassName} not found.");
                AddHelpAndDetailsButtons(tabStrip);
#endif
            }
            else
            {
                m_TabsRibbon.Clicked += ChangeTab;
            }
        }

        // Matches the Build Analysis window's [?][inspector] pair. Help carries margin-left: auto, so
        // adding it first pushes both to the right end.
        void AddHelpAndDetailsButtons(VisualElement container)
        {
            string theme = EditorGUIUtility.isProSkin
                ? BuildReportUtility.ReportToolbarDarkSuffix
                : BuildReportUtility.ReportToolbarLightSuffix;

            var help = new ToolbarButton(() => Help.BrowseURL(AddressableAssetUtility.GenerateDocsURL(k_ReportWindowDocsPage)))
            {
                name = BuildReportUtility.ReportToolbarHelpButton,
                tooltip = "Open Addressables Report documentation",
            };
            help.AddToClassList(BuildReportUtility.ReportToolbarButtonClass);
            help.AddToClassList(BuildReportUtility.ReportToolbarHelpClass);
            help.AddToClassList(BuildReportUtility.ReportToolbarHelpClass + theme);
            container.Add(help);

            var toggleDetails = new ToolbarToggle
            {
                name = BuildReportUtility.ReportToolbarToggleDetailsButton,
                tooltip = "Toggle Details",
            };
            toggleDetails.AddToClassList(BuildReportUtility.ReportToolbarButtonClass);
            toggleDetails.AddToClassList(BuildReportUtility.ReportToolbarDetailsClass);
            toggleDetails.AddToClassList(BuildReportUtility.ReportToolbarDetailsClass + theme);
            toggleDetails.SetValueWithoutNotify(DetailsPaneVisible);
            toggleDetails.RegisterValueChangedCallback(evt =>
            {
                AddressableAnalytics.ReportUsageEvent(evt.newValue
                    ? AddressableAnalytics.UsageEventType.BuildReportDetailsOpen
                    : AddressableAnalytics.UsageEventType.BuildReportDetailsClose);
                SetDetailsPaneVisible(evt.newValue);
            });
            container.Add(toggleDetails);

            m_DetailsToggle = toggleDetails;
            UpdateDetailsToggleEnabled(m_CurrentTab);
        }

        // Summary clears the details pane, so there the column collapses and the toggle greys out.
        // Embedded only: the standalone window keeps master's always-toggleable behaviour.
        bool CurrentTabUsesDetailsPane =>
            !m_IsEmbedded ||
            (RibbonTabType)m_CurrentTab == RibbonTabType.ContentTab || (RibbonTabType)m_CurrentTab == RibbonTabType.PotentialIssues;

        void UpdateDetailsToggleEnabled(int index)
        {
            if (m_DetailsToggle == null)
                return;

            var tab = (RibbonTabType)index;
            m_DetailsToggle.SetEnabled(tab == RibbonTabType.ContentTab || tab == RibbonTabType.PotentialIssues);
        }

        /// <summary>
        /// Records the preference for the details column. Tabs that do not use it stay collapsed.
        /// </summary>
        public void SetDetailsPaneVisible(bool visible)
        {
            m_DetailsPaneVisible = visible;

            if (!m_HasLaidOut)
                return;

            ApplyDetailsPaneVisibility();
        }

        void OnFirstGeometry(GeometryChangedEvent evt)
        {
            m_ContentSplit.UnregisterCallback<GeometryChangedEvent>(OnFirstGeometry);
            m_HasLaidOut = true;
            ApplyDetailsPaneVisibility();
        }

        void ApplyDetailsPaneVisibility()
        {
            // Tracking the last applied value avoids collapsing or uncollapsing the splitter twice.
            bool visible = DetailsPaneVisible && CurrentTabUsesDetailsPane;
            if (m_AppliedDetailsPaneVisible == visible)
                return;
            m_AppliedDetailsPaneVisible = visible;

            if (visible)
                m_ContentSplit.UnCollapse();
            else
                m_ContentSplit.CollapseChild(k_DetailsPaneIndex);
        }

        public void Consume(BuildLayout report)
        {
            Consume(report, null);
        }

        /// <param name="buildName">Name of the build as the host knows it, shown as the title of
        /// the Summary tab. Pass null when the host has no name for the build.</param>
        internal void Consume(BuildLayout report, string buildName)
        {
            m_BuildReport = report;
            m_HelperConsumer.Consume(m_BuildReport);
            ConsumeSummaryTab(m_BuildReport, buildName);
            m_ActiveContentView?.Consume(m_BuildReport);
            m_CachedPotentialIssuesViews.Clear();
            m_CachedContentViews.Clear();
        }

        void ConsumeSummaryTab(BuildLayout report, string buildName)
        {
            if (m_IsEmbedded)
                m_MainPanelSummaryTabEmbedded.Consume(report, buildName);
            else
                m_MainPanelSummaryTab.Consume(report);
        }

        public void ClearViews()
        {
            m_MainPanelSummaryTab?.ClearGUI();
            m_MainPanelSummaryTabEmbedded?.ClearGUI();
            m_ActiveContentView?.ClearGUI();
            m_DetailsView?.ClearGUI();
        }

        void OnViewDropDownChanged(ChangeEvent<string> evt)
        {
            if (m_CurrentTab == (int)RibbonTabType.ContentTab)
                OnViewDropDownChanged(s_ContentViewTypes.IndexOf(evt.newValue));
            else if (m_CurrentTab == (int)RibbonTabType.PotentialIssues)
                OnViewDropDownChanged(s_PotentialIssuesViewTypes.IndexOf(evt.newValue));
        }

        void OnViewDropDownChanged(int newIndex)
        {
            if (m_ActiveContentView != null)
                m_ActiveContentView.ClearGUI();

            if (m_CurrentTab == (int)RibbonTabType.ContentTab)
            {
                string prevSearchValue = m_ActiveContentView.m_SearchValue;
                m_PreviousContentDropDownValue = newIndex;
                m_ActiveContentViewType = (ContentViewType)newIndex;
                m_ActiveContentView = GetContentView(m_ActiveContentViewType);
                m_ActiveContentView.m_SearchField.Q<TextField>().SetValueWithoutNotify(prevSearchValue);
                m_ActiveContentView.ScheduleDebouncedSearch(prevSearchValue);

                switch (newIndex)
                {
                    case (int) ContentViewType.BundleView:
                        AddressableAnalytics.ReportUsageEvent(AddressableAnalytics.UsageEventType.BuildReportViewByAssetBundle);
                        break;
                    case (int) ContentViewType.AssetsView:
                        AddressableAnalytics.ReportUsageEvent(AddressableAnalytics.UsageEventType.BuildReportViewByAssets);
                        break;
                    case (int) ContentViewType.GroupsView:
                        AddressableAnalytics.ReportUsageEvent(AddressableAnalytics.UsageEventType.BuildReportViewByGroup);
                        break;
                    case (int) ContentViewType.LabelsView:
                        AddressableAnalytics.ReportUsageEvent(AddressableAnalytics.UsageEventType.BuildReportViewByLabels);
                        break;
                }
            }
            else if (m_CurrentTab == (int)RibbonTabType.PotentialIssues)
            {
                string prevSearchValue = m_ActiveContentView.m_SearchValue;
                m_ActivePotentialIssuesViewType = (PotentialIssuesType)newIndex;
                m_ActiveContentView = GetPotentialIssuesView(m_ActivePotentialIssuesViewType);
                m_ActiveContentView.m_SearchField.Q<TextField>().value = prevSearchValue;
                switch (newIndex)
                {
                    case (int) PotentialIssuesType.DuplicatedAssetsView:
                        AddressableAnalytics.ReportUsageEvent(AddressableAnalytics.UsageEventType.BuildReportViewByDuplicatedAssets);
                        break;
                }
            }
        }

        ContentView GetContentView(ContentViewType type)
        {
            VisualElement tabRoot = m_TabContents[(int)RibbonTabType.ContentTab];

            if (m_CachedContentViews.ContainsKey(type))
                return m_CachedContentViews[type].UseCachedView(tabRoot);

            ContentView view = null;

            //Add more content views here
            switch (type)
            {
                case ContentViewType.AssetsView:
                    view = new AssetsContentView(m_HelperConsumer, m_DetailsView);
                    break;
                case ContentViewType.GroupsView:
                    view = new GroupsContentView(m_HelperConsumer, m_DetailsView);
                    break;
                case ContentViewType.LabelsView:
                    view = new LabelsContentView(m_HelperConsumer, m_DetailsView);
                    break;
                case ContentViewType.BundleView:
                default:
                    view = new BundlesContentView(m_HelperConsumer, m_DetailsView);
                    break;

            }
            view.CreateGUI(tabRoot);
            view.Consume(m_BuildReport);
            view.ItemsSelected += m_DetailsView.OnSelected;
            m_CachedContentViews.Add(type, view);
            return view;
        }

        ContentView GetPotentialIssuesView(PotentialIssuesType type)
        {
            VisualElement tabRoot = m_TabContents[(int)RibbonTabType.PotentialIssues];

            if (m_CachedPotentialIssuesViews.ContainsKey(type))
                return m_CachedPotentialIssuesViews[type].UseCachedView(tabRoot);

            ContentView view = null;

            //Add more potential issues views here
            switch (type)
            {
                case PotentialIssuesType.DuplicatedAssetsView:
                default:
                    view = new DuplicatedAssetsContentView(m_HelperConsumer, m_DetailsView);
                    break;
            }

            view.CreateGUI(tabRoot);
            view.Consume(m_BuildReport);
            view.ItemsSelected += m_DetailsView.OnSelected;
            m_CachedPotentialIssuesViews.Add(type, view);
            return view;
        }

        void OnActiveTabChanged(Tab previous, Tab current)
        {
            ChangeTab(m_TabView.selectedTabIndex);
        }

        // The TabView keeps only its strip, so the panels live in MiddlePane and one is shown at a time.
        void ShowTabContent(int index)
        {
            for (int i = 0; i < m_TabContents.Length; i++)
            {
                if (m_TabContents[i] != null)
                    m_TabContents[i].style.display = i == index ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        void ChangeTab(int index)
        {
            if (index < 0 || index == m_CurrentTab)
                return;

            if ((RibbonTabType)index == RibbonTabType.SummaryTab || (RibbonTabType)index == RibbonTabType.PotentialIssues)
                m_DetailsView.ClearGUI();

            m_CurrentTab = index;
            ShowTabContent(index);
            UpdateDetailsToggleEnabled(index);
            if (m_HasLaidOut)
                ApplyDetailsPaneVisibility();

            if ((RibbonTabType)index == RibbonTabType.PotentialIssues)
                OnViewDropDownChanged(m_PreviousPotentialIssuesDropDownValue);
            else if ((RibbonTabType)index == RibbonTabType.ContentTab)
                OnViewDropDownChanged(m_PreviousContentDropDownValue);

            switch (index)
            {
                case (int) RibbonTabType.SummaryTab:
                    AddressableAnalytics.ReportUsageEvent(AddressableAnalytics.UsageEventType.BuildReportSelectedSummaryTab);
                    break;
                case (int) RibbonTabType.ContentTab:
                    AddressableAnalytics.ReportUsageEvent(AddressableAnalytics.UsageEventType.BuildReportSelectedExploreTab);
                    break;
                case (int) RibbonTabType.PotentialIssues:
                    AddressableAnalytics.ReportUsageEvent(AddressableAnalytics.UsageEventType.BuildReportSelectedPotentialIssuesTab);
                    break;
            }
        }

        // Returns true when the selection moved, in which case ChangeTab has already rebuilt the
        // tab's view and the caller does not need to.
        bool TrySelectTab(RibbonTabType tab)
        {
            if (m_CurrentTab == (int)tab)
                return false;

            if (m_IsEmbedded)
            {
                // Raises activeTabChanged, which routes to ChangeTab.
                m_TabView.selectedTabIndex = (int)tab;
            }
            else
            {
                ChangeTab((int)tab);
                m_TabsRibbon.ButtonClicked((int)tab);
            }
            return true;
        }

        public void NavigateToView(ContentViewType type)
        {
            int newIndex = (int)type;
            m_PreviousContentDropDownValue = newIndex;
            m_ContentViewTypeDropdown.SetValueWithoutNotify(s_ContentViewTypes[newIndex]);

            if (!TrySelectTab(RibbonTabType.ContentTab))
                OnViewDropDownChanged(newIndex);
        }

        public void NavigateToView(PotentialIssuesType type)
        {
            int newIndex = (int)type;
            m_PreviousPotentialIssuesDropDownValue = newIndex;
            m_PotentialIssuesViewTypeDropdown.SetValueWithoutNotify(s_PotentialIssuesViewTypes[newIndex]);

            if (!TrySelectTab(RibbonTabType.PotentialIssues))
                OnViewDropDownChanged(newIndex);
        }

        public IAddressablesBuildReportItem SelectItemInView(Hash128 hash, bool expand = false)
        {
            TreeDataReportItem item = m_ActiveContentView.DataHashtoReportItem[hash];
            m_ActiveContentView.ContentTreeView.SetSelectionById(item.Id);
            m_ActiveContentView.ContentTreeView.ScrollToItemById(item.Id);
            if (expand)
                m_ActiveContentView.ContentTreeView.ExpandItem(item.Id);

            m_DetailsView.OnSelected(new List<object>() { item.ReportItem });

            return item.ReportItem;
        }
    }
}
