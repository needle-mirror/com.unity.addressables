using System;
using UnityEditor.AddressableAssets.Build.Layout;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityEditor.AddressableAssets.BuildReportVisualizer
{
    /// <summary>
    /// The complete report browser: the main toolbar, the reports list, and the report
    /// content view, composed into one reusable view. The standalone Addressables Report
    /// window is a shell around this; other hosts can embed it the same way.
    /// </summary>
    [Serializable]
    class BuildReportBrowserView : IAddressableView
    {
        [SerializeField]
        BuildReportContentView m_ContentView;

        BuildReportListView m_ReportListView;
        MainToolbar m_MainToolbar;

        public void CreateGUI(VisualElement rootVisualElement)
        {
            var browserTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(BuildReportUtility.BuildReportBrowserUxmlPath);
            browserTree.CloneTree(rootVisualElement);

            if (m_ContentView == null)
                m_ContentView = new BuildReportContentView();
            m_ReportListView = new BuildReportListView(m_ContentView);
            m_MainToolbar = new MainToolbar(m_ReportListView, m_ContentView);

            m_ContentView.CreateGUI(rootVisualElement.Q<VisualElement>(BuildReportUtility.MiddleRightPanesContainer));
            m_MainToolbar.CreateGUI(rootVisualElement);
            m_ReportListView.CreateGUI(rootVisualElement.Q<VisualElement>(BuildReportUtility.LeftPane));
        }

        internal void LoadNewestReport()
        {
            m_ReportListView?.LoadNewestReport();
        }

        internal void OnLayoutCompleted(string path, BuildLayout layout)
        {
            m_ReportListView?.AddReport(path, layout);
        }
    }
}
