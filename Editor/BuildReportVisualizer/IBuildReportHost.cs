using UnityEngine;

namespace UnityEditor.AddressableAssets.BuildReportVisualizer
{
    /// <summary>
    /// Host of the build report content that collaborating views (report list, summary tab,
    /// details panel) navigate and feed reports into.
    /// </summary>
    interface IBuildReportHost : IBuildReportConsumer
    {
        ContentView ActiveContentView { get; }
        void ClearViews();
        void NavigateToView(ContentViewType type);
        void NavigateToView(PotentialIssuesType type);
        IAddressablesBuildReportItem SelectItemInView(Hash128 hash, bool expand = false);
    }
}
