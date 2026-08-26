using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets.Build.BuildPipelineTasks;
using UnityEditor.AddressableAssets.Build.Layout;
using UnityEditor.AddressableAssets.BuildReportVisualizer;
using UnityEditor.AddressableAssets.GUIElements;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Tests.Editor.BuildReportVisualizer
{
    public class BuildReportContentViewTests
    {
        static BuildLayout CreateLayout(string profileName)
        {
            return new BuildLayout
            {
                AddressablesEditorSettings = new BuildLayout.AddressablesEditorData
                {
                    ActiveProfile = new BuildLayout.Profile { Name = profileName }
                },
                AddressablesRuntimeSettings = new BuildLayout.AddressablesRuntimeData()
            };
        }

        static BuildReportContentView CreateStandaloneView()
        {
            var view = new BuildReportContentView();
            view.CreateGUI(new VisualElement());
            return view;
        }

#if ENABLE_BUILD_HISTORY_EXTERNAL_BUILDS
        // The embedded design only ships where the Build Analysis window does, so its tests are gated
        // the same way AddressablesReportDrawerTests is.
        static BuildReportContentView CreateView(VisualElement root = null)
        {
            var view = new BuildReportContentView(true);
            view.CreateGUI(root ?? new VisualElement());
            return view;
        }

        class HostWindow : EditorWindow { }

        static BuildReportContentView CreateHostedView(EditorWindow window)
        {
            var root = new VisualElement();
            window.rootVisualElement.Add(root);

            var view = new BuildReportContentView(true);
            view.CreateGUI(root);
            return view;
        }
#endif

        [Test]
        public void Consume_WorksWithoutAnEditorWindow()
        {
            var view = CreateStandaloneView();
            var layout = CreateLayout("profile");

            view.Consume(layout);

            Assert.AreSame(layout, view.BuildReport);
        }

        [Test]
        public void TwoContentViews_HoldIndependentReports()
        {
            var first = CreateStandaloneView();
            var second = CreateStandaloneView();
            var firstLayout = CreateLayout("first");
            var secondLayout = CreateLayout("second");

            first.Consume(firstLayout);
            second.Consume(secondLayout);

            Assert.AreSame(firstLayout, first.BuildReport);
            Assert.AreSame(secondLayout, second.BuildReport);
        }

        [Test]
        public void BrowserView_BuildsFullUIWithoutAnEditorWindow()
        {
            var root = new VisualElement();
            var browser = new BuildReportBrowserView();

            browser.CreateGUI(root);

            Assert.IsNotNull(root.Q<ListView>(BuildReportUtility.ReportsList));
            Assert.IsNotNull(root.Q<VisualElement>(BuildReportUtility.MainToolbar));
            Assert.IsNotNull(root.Q<VisualElement>(BuildReportUtility.MiddleRightPaneSplitter));
        }

        // CBD-2355 redraws the report only where it is embedded in Build Analysis. The standalone
        // window keeps master's chrome: the Ribbon strip and the details button on MainToolbar.
        [Test]
        public void BrowserView_KeepsTheRibbonChromeAndTheToolbarDetailsButton()
        {
            var root = new VisualElement();
            new BuildReportBrowserView().CreateGUI(root);

            Assert.IsNotNull(root.Q<Ribbon>(BuildReportUtility.TabsRibbon), "The window keeps the Ribbon strip.");
            Assert.IsNull(root.Q<TabView>(BuildReportUtility.TabsView), "TabView is for the embedded report only.");

            Assert.IsNotNull(root.Q<ToolbarButton>(BuildReportUtility.MainToolbarCollapseRightPaneButton),
                "The details button belongs on MainToolbar here.");
            Assert.IsNull(root.Q<ToolbarButton>(BuildReportUtility.ReportToolbarHelpButton),
                "The window has no help button, as on master.");
            Assert.IsNull(root.Q<ToolbarToggle>(BuildReportUtility.ReportToolbarToggleDetailsButton));
        }

        [Test]
        public void StandaloneContentView_UsesTheLegacySummaryTabAndKeepsTheDetailsColumn()
        {
            var root = new VisualElement();
            var view = new BuildReportContentView();
            view.CreateGUI(root);
            view.Consume(CreateLayout("profile"));

            // Master's Summary builds rows into a scroll view rather than the card header.
            Assert.IsNotNull(root.Q<VisualElement>(BuildReportUtility.SummaryTab));
            Assert.IsNull(root.Q<Label>(BuildReportUtility.SummaryTabHeaderTitle),
                "The card header belongs to the embedded Summary tab.");

            // Master lets the column be toggled from any tab, so Summary must not force it closed.
            Assert.IsTrue(view.DetailsPaneVisible);
        }

#if ENABLE_BUILD_HISTORY_EXTERNAL_BUILDS

        [Test]
        public void CreateGUI_EndsTheTabStripWithHelpThenTheDetailsToggle()
        {
            var root = new VisualElement();
            var view = new BuildReportContentView(true);
            view.CreateGUI(root);

            VisualElement tabStrip = view.TabView.Q<VisualElement>(className: TabView.viewportUssClassName);
            Assert.IsNotNull(tabStrip, "The buttons belong in the TabView's content viewport.");

            var help = tabStrip.Q<ToolbarButton>(BuildReportUtility.ReportToolbarHelpButton);
            var details = tabStrip.Q<ToolbarToggle>(BuildReportUtility.ReportToolbarToggleDetailsButton);
            Assert.IsNotNull(help);
            Assert.IsNotNull(details);

            var strip = new List<VisualElement>(tabStrip.Children());
            Assert.AreSame(help, strip[strip.Count - 2], "Help should be second to last.");
            Assert.AreSame(details, strip[strip.Count - 1], "The details toggle should be last.");

            string theme = EditorGUIUtility.isProSkin
                ? BuildReportUtility.ReportToolbarDarkSuffix
                : BuildReportUtility.ReportToolbarLightSuffix;
            foreach (VisualElement button in new VisualElement[] { help, details })
            {
                Assert.IsTrue(button.ClassListContains(BuildReportUtility.ReportToolbarButtonClass));

                bool hasThemedIcon = false;
                foreach (string className in button.GetClasses())
                {
                    if (className.EndsWith(theme))
                    {
                        hasThemedIcon = true;
                        break;
                    }
                }
                Assert.IsTrue(hasThemedIcon, $"{button.name} is missing its {theme} icon class.");
            }
        }

        // Summary clears the details pane, so the toggle greys out there rather than disappearing.
        [Test]
        public void DetailsToggle_IsEnabledOnlyOnExploreAndPotentialIssues()
        {
            var view = CreateView();
            view.Consume(CreateLayout("profile"));
            var details = view.TabView.Q<ToolbarToggle>(BuildReportUtility.ReportToolbarToggleDetailsButton);

            Assert.IsFalse(details.enabledSelf, "Summary is the starting tab.");

            view.NavigateToView(ContentViewType.AssetsView);
            Assert.IsTrue(details.enabledSelf);

            view.NavigateToView(PotentialIssuesType.DuplicatedAssetsView);
            Assert.IsTrue(details.enabledSelf);
        }

        [Test]
        public void DetailsToggle_StartsOnAndDrivesTheDetailsPane()
        {
            var window = EditorWindow.GetWindow<HostWindow>();
            try
            {
                var view = CreateHostedView(window);
                var details = view.TabView.Q<ToolbarToggle>(BuildReportUtility.ReportToolbarToggleDetailsButton);

                Assert.IsTrue(details.value, "The details pane starts visible, so the toggle starts on.");

                view.NavigateToView(ContentViewType.AssetsView);

                details.value = false;
                Assert.IsFalse(view.DetailsPaneVisible);

                details.value = true;
                Assert.IsTrue(view.DetailsPaneVisible);
            }
            finally
            {
                window.Close();
            }
        }

        // Hoisting the TabView above the splitter is what lets the strip run over the details column.
        [Test]
        public void CreateGUI_PutsTheTabStripAboveTheSplitterNotInsideIt()
        {
            var root = new VisualElement();
            var view = new BuildReportContentView(true);
            view.CreateGUI(root);

            VisualElement contentRoot = root.Q<VisualElement>(BuildReportUtility.ReportContentRoot);
            Assert.IsNotNull(contentRoot);

            var splitter = contentRoot.Q<TwoPaneSplitView>(BuildReportUtility.MiddleRightPaneSplitter);
            Assert.AreSame(view.TabView, contentRoot[0], "The strip comes first, spanning both panes.");
            Assert.AreSame(splitter, contentRoot[1]);
            Assert.IsNull(view.TabView.Q<TwoPaneSplitView>(BuildReportUtility.MiddleRightPaneSplitter),
                "The splitter must not be inside the TabView, or the strip is confined to MiddlePane again.");
        }

        [Test]
        public void ChangeTab_ShowsOnlyTheSelectedPanel()
        {
            var root = new VisualElement();
            var view = new BuildReportContentView(true);
            view.CreateGUI(root);
            view.Consume(CreateLayout("profile"));

            view.NavigateToView(ContentViewType.AssetsView);

            Assert.AreEqual(DisplayStyle.Flex, root.Q<VisualElement>(BuildReportUtility.ContentTab).style.display.value);
            Assert.AreEqual(DisplayStyle.None, root.Q<VisualElement>(BuildReportUtility.SummaryTab).style.display.value);
            Assert.AreEqual(DisplayStyle.None, root.Q<VisualElement>(BuildReportUtility.PotentialIssuesTab).style.display.value);
        }

        // Summary collapses the column whatever the preference; the preference returns on other tabs.
        [Test]
        public void DetailsPanePreference_SurvivesTabsThatCollapseTheColumn()
        {
            var view = CreateView();
            view.Consume(CreateLayout("profile"));

            view.NavigateToView(ContentViewType.AssetsView);
            view.SetDetailsPaneVisible(false);
            Assert.IsFalse(view.DetailsPaneVisible);

            view.NavigateToView(PotentialIssuesType.DuplicatedAssetsView);
            Assert.IsFalse(view.DetailsPaneVisible, "Switching tabs should not resurrect a hidden column.");

            view.SetDetailsPaneVisible(true);
            view.NavigateToView(ContentViewType.AssetsView);
            Assert.IsTrue(view.DetailsPaneVisible);
        }

#endif

        [Test]
        public void SetDetailsPaneVisible_BeforeLayout_RecordsTheRequestWithoutThrowing()
        {
            var view = CreateStandaloneView();
            Assert.IsTrue(view.DetailsPaneVisible);

            Assert.DoesNotThrow(() => view.SetDetailsPaneVisible(false));
            Assert.IsFalse(view.DetailsPaneVisible);

            Assert.DoesNotThrow(() => view.SetDetailsPaneVisible(true));
            Assert.IsTrue(view.DetailsPaneVisible);
        }

        // The Explore and Potential Issues templates both hold elements named ContentView and
        // SearchField, and TabView keeps inactive tabs in the visual tree, so a view created
        // against the whole tree would bind to whichever tab happens to come first.
        [Test]
        public void EachTab_OwnsItsOwnContentViewAndSearchField()
        {
            var root = new VisualElement();
            var view = new BuildReportContentView();
            view.CreateGUI(root);

            var contentViews = root.Query<VisualElement>(BuildReportUtility.ContentView).ToList();
            var searchFields = root.Query<ToolbarSearchField>(BuildReportUtility.SearchField).ToList();

            Assert.AreEqual(2, contentViews.Count, "Expected one ContentView in the Explore tab and one in Potential Issues.");
            Assert.AreEqual(2, searchFields.Count);
            Assert.AreNotSame(contentViews[0], contentViews[1]);
        }

#if ENABLE_BUILD_HISTORY_EXTERNAL_BUILDS
        [Test]
        public void NavigateToView_MovesTheTabSelection()
        {
            var view = CreateView();
            view.Consume(CreateLayout("profile"));

            view.NavigateToView(ContentViewType.AssetsView);
            Assert.AreEqual((int)RibbonTabType.ContentTab, view.TabView.selectedTabIndex);

            view.NavigateToView(PotentialIssuesType.DuplicatedAssetsView);
            Assert.AreEqual((int)RibbonTabType.PotentialIssues, view.TabView.selectedTabIndex);
        }
#endif

        [Test]
        public void LayoutCompleted_InvokesEverySubscriberAndSkipsUnsubscribed()
        {
            int firstCalls = 0;
            int secondCalls = 0;
            int unsubscribedCalls = 0;
            Action<string, BuildLayout> first = (path, layout) => firstCalls++;
            Action<string, BuildLayout> second = (path, layout) => secondCalls++;
            Action<string, BuildLayout> unsubscribed = (path, layout) => unsubscribedCalls++;

            BuildLayoutGenerationTask.LayoutCompleted += first;
            BuildLayoutGenerationTask.LayoutCompleted += second;
            BuildLayoutGenerationTask.LayoutCompleted += unsubscribed;
            BuildLayoutGenerationTask.LayoutCompleted -= unsubscribed;
            try
            {
                BuildLayoutGenerationTask.RaiseLayoutCompleted("report.json", null);
            }
            finally
            {
                BuildLayoutGenerationTask.LayoutCompleted -= first;
                BuildLayoutGenerationTask.LayoutCompleted -= second;
            }

            Assert.AreEqual(1, firstCalls);
            Assert.AreEqual(1, secondCalls);
            Assert.AreEqual(0, unsubscribedCalls);
        }
    }
}
