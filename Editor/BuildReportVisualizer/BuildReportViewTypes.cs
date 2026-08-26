namespace UnityEditor.AddressableAssets.BuildReportVisualizer
{
    internal enum RibbonTabType
    {
        SummaryTab = 0,
        ContentTab,
        PotentialIssues
    }

    internal enum ContentViewType
    {
        BundleView = 0,
        AssetsView,
        LabelsView,
        GroupsView
    }

    internal enum PotentialIssuesType
    {
        DuplicatedAssetsView
    }
}
