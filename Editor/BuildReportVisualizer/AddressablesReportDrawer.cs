#if ENABLE_BUILD_HISTORY_EXTERNAL_BUILDS
using UnityEditor;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Build.Layout;
using UnityEditor.Build;
using UnityEditor.Build.Analysis;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityEditor.AddressableAssets.BuildReportVisualizer
{
    /// <summary>
    /// Draws the Addressables build report in the Build Analysis window. Builds registered in the build history
    /// by <see cref="AddressablesBuildHistorySupport"/> are drawn with the same content view as the standalone
    /// Addressables Report window.
    /// </summary>
    [ExternalBuildDrawer(AddressablesBuildHistorySupport.kPackageName)]
    class AddressablesReportDrawer : ExternalBuildDrawer
    {
        readonly BuildReportContentView m_ContentView = new BuildReportContentView(true);

        GUID m_ShownBuild;

        internal BuildLayout ShownReport => m_ContentView.BuildReport;

        public override VisualElement CreateContent()
        {
            var root = new VisualElement();
            AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(BuildReportUtility.EmbeddedReportUxmlPath).CloneTree(root);
            m_ContentView.CreateGUI(root.Q<VisualElement>(BuildReportUtility.EmbeddedReportContent));
            return root;
        }

        public override void OnBuildSelected(BuildReportSummary summary)
        {
            GUID buildSessionGuid = summary.BuildSessionGUID;
            if (buildSessionGuid == m_ShownBuild)
                return;

            m_ShownBuild = default;

            if (!BuildHistory.TryGetFilePath(buildSessionGuid, AddressablesBuildHistorySupport.kLayoutFileName, out string layoutPath))
            {
                Debug.LogWarning($"No Addressables build report was found for build {buildSessionGuid}.");
                m_ContentView.ClearViews();
                return;
            }

            BuildLayout layout = BuildLayout.Open(layoutPath, readFullFile: true);
            if (layout == null)
            {
                Debug.LogWarning($"Unable to load the Addressables build report at {layoutPath}.");
                m_ContentView.ClearViews();
                return;
            }

            m_ContentView.Consume(layout, summary.BuildName);
            m_ShownBuild = buildSessionGuid;
        }

        public override void OnBuildDeselected()
        {
            m_ShownBuild = default;
            m_ContentView.ClearViews();
        }
    }
}
#endif
