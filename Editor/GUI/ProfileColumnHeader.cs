using UnityEditor.AddressableAssets.Settings;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace UnityEditor.AddressableAssets.GUI
{
    internal class ProfileColumnHeader : MultiColumnHeader
    {
        private ProfileWindow m_Window;
        public ProfileColumnHeader(MultiColumnHeaderState state, ProfileWindow window) : base(state)
        {
            m_Window = window;
        }
        
        protected override void AddColumnHeaderContextMenuItems(GenericMenu menu)
        {
            Event current = Event.current;

            int columnIndex = -1;
            for(int i = 0; i < this.state.visibleColumns.Length; ++i)
            {
                if (GetColumnRect(i).Contains(current.mousePosition))
                {
                    columnIndex = this.state.visibleColumns[i];
                }
            }

            if (columnIndex > 0)
            {
                menu.AddItem(new GUIContent("Rename Variable"), false, () => RenameVariable(columnIndex));
                menu.AddItem(new GUIContent("Delete Variable"), false, () => DeleteVariable(columnIndex));
                menu.AddSeparator("");
                base.AddColumnHeaderContextMenuItems(menu);
            }
        }

        internal void RenameCell(int rowIndex, Rect rowRect)
        {
            Event current = Event.current;
            var rows = m_Window.ProfileTreeView.GetRows();

            for (int i = 0; i < this.state.visibleColumns.Length; ++i)
            {
                Rect cellRect = GetCellRect(i, rowRect);
                if (cellRect.Contains(current.mousePosition))
                {
                    try
                    {
                        ProfileTreeViewItem item = rows[rowIndex] as ProfileTreeViewItem;
                        if (item.displayName == "Default" && i == 0) return; // Can't rename default profile
                        cellRect.y -= cellRect.height;
                        PopupWindow.Show(cellRect, new ProfileRenamePopUp(m_Window, cellRect.width, i, item.BuildProfile.id));
                    }
                    catch (ExitGUIException)
                    {
                        // Exception not being caught through OnGUI call
                    }
                }
            }
        }

        internal void RenameVariable(int columnIndex)
        {
            RenameVariable(columnIndex, GetColumnRect(columnIndex));
        }

        internal void RenameVariable(int columnIndex, Rect colRect)
        {
            float xPos = colRect.position.x;
            float yPos = colRect.position.y + m_Window.ToolbarHeight;

            // Mac resolution fixes
            if (SystemInfo.operatingSystem.Contains("Mac"))
            {
                xPos += m_Window.position.x;
                yPos += m_Window.position.y;
            }

            Rect colToWorldRect = new Rect (new Vector2 (xPos, yPos), colRect.size);

            try
            {
                PopupWindow.Show(colToWorldRect, new ProfileRenamePopUp(m_Window, colRect.width, columnIndex));
            }
            catch (ExitGUIException)
            {
                // Exception not being caught through OnGUI call
            }
        }

        void DeleteVariable(int columnIndex)
        {
            AddressableAssetProfileSettings.ProfileIdData variable =
                m_Window.settings.profileSettings.profileEntryNames.Find((s) =>
                    s.ProfileName.Equals(state.columns[columnIndex].headerContent.text));

            if(variable != null)
                m_Window.settings.profileSettings.RemoveValue(variable.Id);
        }

    }
}