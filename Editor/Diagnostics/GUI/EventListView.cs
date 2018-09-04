using System.Collections.Generic;
using UnityEngine;
using UnityEditor.IMGUI.Controls;
using System;
using UnityEngine.ResourceManagement.Diagnostics;

namespace UnityEditor.AddressableAssets.Diagnostics
{
    internal class EventListView : TreeView
    {
        class EventTreeViewItem : TreeViewItem
        {
            public DiagnosticEvent m_entry;
            public EventTreeViewItem(DiagnosticEvent e) : base(e.EventId.GetHashCode() + e.Stream, 0)
            {
                m_entry = e;
            }
        }
        List<DiagnosticEvent> m_events;
        Action<Rect, DiagnosticEvent, int> m_onColumnGUI;
        Func<DiagnosticEvent, bool> m_onFilterEvent;
        public DiagnosticEvent selectedEvent { get; private set; }

        public EventListView(TreeViewState tvs, MultiColumnHeaderState mchs, Action<Rect, DiagnosticEvent, int> onColumn, Func<DiagnosticEvent, bool> filter) : base(tvs, new MultiColumnHeader(mchs))
        {
            m_onColumnGUI = onColumn;
            m_onFilterEvent = filter;
            showBorder = true;
            showAlternatingRowBackgrounds = true;
        }

        protected override void SelectionChanged(IList<int> selectedIds)
        {
            selectedEvent = default(DiagnosticEvent);
            if (selectedIds != null && selectedIds.Count > 0)
                selectedEvent = (FindItem(selectedIds[0], rootItem) as EventTreeViewItem).m_entry;
        }

        protected override bool CanMultiSelect(TreeViewItem item)
        {
            return false;
        }

        public void SetEvents(List<DiagnosticEvent> e)
        {
            selectedEvent = default(DiagnosticEvent);
            m_events = e;
            Reload();
        }

        protected override TreeViewItem BuildRoot()
        {
            TreeViewItem root = new TreeViewItem(-1, -1);
            root.children = new List<TreeViewItem>();
            if (m_events != null)
            {
                foreach (var e in m_events)
                {
                    if (m_onFilterEvent(e))
                        root.AddChild(new EventTreeViewItem(e));
                }
            }
            return root;
        }

        protected override IList<TreeViewItem> BuildRows(TreeViewItem root)
        {
            var rows = base.BuildRows(root);
            return rows;
        }

        protected override void RowGUI(RowGUIArgs args)
        {
            for (int i = 0; i < args.GetNumVisibleColumns(); ++i)
                CellGUI(args.GetCellRect(i), args.item as EventTreeViewItem, args.GetColumn(i));
        }

        private void CellGUI(Rect cellRect, EventTreeViewItem item, int column)
        {
            CenterRectUsingSingleLineHeight(ref cellRect);
            m_onColumnGUI(cellRect, item.m_entry, column);
        }

        protected override bool CanBeParent(TreeViewItem item)
        {
            return false;
        }

        public static MultiColumnHeaderState CreateDefaultMultiColumnHeaderState(List<string> dataColumns, List<float> sizes)
        {
            if (dataColumns == null || sizes == null || dataColumns.Count != sizes.Count)
                throw new System.ArgumentException("Column name and size lists are not the same size");
            var columns = new List<MultiColumnHeaderState.Column>();
            for (int i = 0; i < dataColumns.Count; i++)
                AddColumn(columns, dataColumns[i], dataColumns[i], sizes[i]);
            return new MultiColumnHeaderState(columns.ToArray());
        }

        static void AddColumn(List<MultiColumnHeaderState.Column> columns, string name, string tt, float size)
        {
            MultiColumnHeaderState.Column col = new MultiColumnHeaderState.Column();
            col.headerContent = new GUIContent(name, tt);
            col.minWidth = size * .5f;
            col.width = size;
            col.maxWidth = size * 4;
            col.headerTextAlignment = TextAlignment.Left;
            col.canSort = false;
            col.autoResize = false;
            columns.Add(col);
        }
    }
}
