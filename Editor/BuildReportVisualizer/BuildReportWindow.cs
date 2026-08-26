using UnityEditor.AddressableAssets.Build.BuildPipelineTasks;
using UnityEditor.AddressableAssets.Build.Layout;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityEditor.AddressableAssets.BuildReportVisualizer
{
    class BuildReportWindow : EditorWindow
    {
        [SerializeField]
        BuildReportBrowserView m_BrowserView;

        void OnEnable()
        {
            if (m_BrowserView == null)
                m_BrowserView = new BuildReportBrowserView();

            BuildLayoutGenerationTask.LayoutCompleted += OnLayoutCompleted;
        }

        void OnDisable()
        {
            BuildLayoutGenerationTask.LayoutCompleted -= OnLayoutCompleted;
        }

        void OnLayoutCompleted(string path, BuildLayout layout)
        {
            m_BrowserView.OnLayoutCompleted(path, layout);
        }

        [MenuItem("Window/Asset Management/Addressables/Addressables Report", priority = 2051)]
        public static void ShowWindow()
        {
            CreateWindow();
            AddressableAnalytics.ReportUsageEvent(AddressableAnalytics.UsageEventType.OpenBuildReportManually);
        }

        private static void CreateWindow()
        {
            // Opens the window, otherwise focuses it if it's already open.
            var window = GetWindow<BuildReportWindow>();

            // Adds a title to the window.
            window.titleContent = new GUIContent("Addressables Report");

            // Sets a minimum size to the window.
            window.minSize = new Vector2(280, 50);

            window.m_BrowserView.LoadNewestReport();
        }

        internal static void ShowWindowAfterBuild()
        {
            CreateWindow();
        }

        private void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            if (root == null)
                return;

            m_BrowserView.CreateGUI(root);
        }
    }
}
