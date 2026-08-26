using System.Collections.Generic;
using UnityEditor.AddressableAssets.GUIElements;
using UnityEngine.UIElements;

namespace UnityEditor.AddressableAssets.BuildReportVisualizer
{
    internal enum DetailsViewTab
    {
        ReferencesTo,
        ReferencedBy
    }

    class DetailsView : IAddressableView
    {
        DetailsViewTab m_ActiveContentsTab;
        VisualElement m_Root;
        IBuildReportHost m_Host;

        DetailsContentView m_Contents;
        DetailsSummaryView m_Summary;

        object m_DetailsRootObject;
        object m_DetailsActiveObject;

        internal DetailsView(IBuildReportHost host)
        {
            m_Host = host;
            m_ActiveContentsTab = DetailsViewTab.ReferencesTo;
        }

        public void CreateGUI(VisualElement rootVisualElement)
        {
            m_Root = rootVisualElement;

            m_Summary = new DetailsSummaryView(rootVisualElement, m_Host);
            m_Contents = new DetailsContentView(rootVisualElement, m_Host);

            rootVisualElement.Q<RibbonButton>("ReferencesToTab").clicked += () =>
            {
                m_ActiveContentsTab = DetailsViewTab.ReferencesTo;
                DetailsStack.Clear();

                DisplayContents(m_DetailsActiveObject);

            };

            rootVisualElement.Q<RibbonButton>("ReferencedByTab").clicked += () =>
            {
                m_ActiveContentsTab = DetailsViewTab.ReferencedBy;
                DetailsStack.Clear();

                DisplayContents(m_DetailsActiveObject);

            };
        }

        public void OnSelected(IEnumerable<object> items)
        {
            ClearGUI();
            DetailsStack.Clear();

            foreach (object item in items)
            {
                DisplayItemSummary(item);
                DisplayContents(item);
                m_DetailsRootObject = m_DetailsActiveObject = item;
            }
        }

        public void DisplayItemSummary(object item)
        {
            m_Summary.UpdateSummary(item);
        }

        public void DisplayContents(object contents)
        {
            m_Contents.DisplayContents(contents, m_ActiveContentsTab);
        }

        public void ClearGUI()
        {
            m_Summary.ClearSummary();
            m_Contents.ClearContents();
            DisplayContents(null);
        }
    }
}
