#if ENABLE_BUILD_ANALYSIS_INTEGRATION
using System;
using System.Threading;
using System.Threading.Tasks;
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
        const string k_LoadingMessage = "Loading Addressables report…";

        readonly BuildReportContentView m_ContentView = new BuildReportContentView(true);

        LoadingOverlay m_LoadingOverlay;

        GUID m_ShownBuild;

        internal BuildLayout ShownReport => m_ContentView.BuildReport;

        internal LoadingOverlay LoadingOverlay => m_LoadingOverlay;

        public override VisualElement CreateContent()
        {
            var root = new VisualElement();
            AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(BuildReportUtility.EmbeddedReportUxmlPath).CloneTree(root);
            m_ContentView.CreateGUI(root.Q<VisualElement>(BuildReportUtility.EmbeddedReportContent));
            m_LoadingOverlay = root.Q<LoadingOverlay>(BuildReportUtility.ReportLoadingOverlay);
            return root;
        }

        public override Task<object> LoadBuildAsync(BuildReportSummary summary, CancellationToken token)
        {
            GUID buildSessionGuid = summary.BuildSessionGUID;
            if (buildSessionGuid == m_ShownBuild)
                return Task.FromResult<object>(m_ContentView.BuildReport);

            // BuildHistory reads editor settings, so the path is resolved on the main thread.
            if (!BuildHistory.TryGetFilePath(buildSessionGuid, AddressablesBuildHistorySupport.kLayoutFileName, out string layoutPath))
            {
                Debug.LogWarning($"No Addressables build report was found for build {buildSessionGuid}.");
                return Task.FromResult<object>(null);
            }

            SetLoading(true);
            return Task.Run<object>(() =>
            {
                BuildLayout layout;
                try
                {
                    layout = BuildLayout.ReadFullThreadSafe(layoutPath);
                }
                catch (Exception e)
                {
                    throw new Exception($"Failed to read the Addressables build report at {layoutPath}: {e.Message}", e);
                }
                if (layout == null)
                    Debug.LogWarning($"Unable to load the Addressables build report at {layoutPath}.");
                return layout;
            }, token);
        }

        public override bool ApplyBuild(BuildReportSummary summary, object loaded)
        {
            SetLoading(false);

            GUID buildSessionGuid = summary.BuildSessionGUID;
            if (buildSessionGuid == m_ShownBuild)
                return true;

            m_ShownBuild = default;

            if (!(loaded is BuildLayout layout))
            {
                m_ContentView.ClearViews();
                return false;
            }

            m_ContentView.Consume(layout, summary.BuildName, BuildAnalysisUtility.GetRelatedBuilds(buildSessionGuid));
            m_ShownBuild = buildSessionGuid;
            return true;
        }

        public override void OnBuildDeselected()
        {
            SetLoading(false);
            m_ShownBuild = default;
            m_ContentView.ClearViews();
        }

        // Related Builds comes from the build history, not the layout, so it can go stale.
        public override void OnBuildListRefreshed()
        {
            if (m_ShownBuild.Empty())
                return;

            m_ContentView.SetRelatedBuilds(BuildAnalysisUtility.GetRelatedBuilds(m_ShownBuild));
        }

        void SetLoading(bool loading)
        {
            if (loading)
                m_LoadingOverlay?.Show(k_LoadingMessage);
            else
                m_LoadingOverlay?.Hide();
            m_ContentView.SetLoading(loading);
        }
    }
}
#endif
