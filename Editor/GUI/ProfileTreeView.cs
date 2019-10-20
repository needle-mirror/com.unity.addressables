using System.Collections.Generic;
using System.Linq;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace UnityEditor.AddressableAssets.GUI
{
    internal class ProfileTreeViewItem : TreeViewItem
    {
        private AddressableAssetProfileSettings.BuildProfile m_buildProfile;

        public AddressableAssetProfileSettings.BuildProfile BuildProfile
        {
            get { return m_buildProfile; }
        }

        public ProfileTreeViewItem(AddressableAssetProfileSettings.BuildProfile buildProfile) : base(buildProfile.GetHashCode(), 0, buildProfile.profileName)
        {
            m_buildProfile = buildProfile;
        }
    }

    internal class ProfileTreeView : TreeView
    {
        const float kRowHeights = 20f;

        MultiColumnHeaderState m_Mchs;
        private ProfileWindow m_Window;

        public ProfileTreeView(TreeViewState state, MultiColumnHeaderState multiColumnHeaderState, ProfileColumnHeader profileColumnHeader, ProfileWindow window) :
            base(state, profileColumnHeader)
        {
            m_Mchs = multiColumnHeaderState;
            m_Window = window;
            rowHeight = kRowHeights;
            showAlternatingRowBackgrounds = true;
            showBorder = true;
            profileColumnHeader.sortingChanged += OnSortingChanged;

            Reload();
        }

        protected override TreeViewItem BuildRoot()
        {
            var root = new TreeViewItem(-1, -1); // dummy root node

            for (int i = 0; i < m_Window.settings.profileSettings.profiles.Count; i++)
            {
                root.AddChild(new ProfileTreeViewItem(m_Window.settings.profileSettings.profiles[i]));
            }
            return root;
        }
        
        public override void OnGUI(Rect rect)
        {
            base.OnGUI(rect);

            //TODO - this occasionally causes a "hot control" issue.
            if (Event.current.type == EventType.MouseDown &&
                Event.current.button == 0 &&
                rect.Contains(Event.current.mousePosition))
            {
                SetSelection(new int[0], TreeViewSelectionOptions.FireSelectionChanged);
            }
        }

        // Build row content
        protected override void RowGUI(RowGUIArgs args)
        {
            var item = args.item as ProfileTreeViewItem;
            if (item == null)
            {
                base.RowGUI(args);
                return;
            }

            // Write content for each row
            for (int i = 0; i < args.GetNumVisibleColumns() && !args.isRenaming; i++)
            {
                CellGUI(args.GetCellRect(i), item, m_Mchs.visibleColumns[i], ref args);
            }
        }

        // Write content for each cell
        GUIStyle m_LabelStyle;
        void CellGUI(Rect cellRect, ProfileTreeViewItem viewItem, int columnIndex, ref RowGUIArgs args)
        {
            if (m_LabelStyle == null)
            {
                m_LabelStyle = UnityEngine.GUI.skin.GetStyle("Label");
            }

            if (columnIndex == 0)
            {
                if (Event.current.type == EventType.Repaint)
                    m_LabelStyle.Draw(cellRect, viewItem.BuildProfile.profileName, false, false, args.selected, args.focused);
            }
            else
            {
                viewItem.BuildProfile.values[(int)columnIndex - 1].value = 
                    EditorGUI.TextField(cellRect, viewItem.BuildProfile.values[(int)columnIndex - 1].value);
            }
        }

        void OnSortingChanged(MultiColumnHeader multiColumnHeader)
        {
            SortChildren(rootItem, multiColumnHeader.IsSortedAscending(0));
            BuildRows(rootItem);
        }

        void SortChildren(TreeViewItem root, bool ascending)
        {
            if (!root.hasChildren)
                return;

            List<ProfileTreeViewItem> kids = new List<ProfileTreeViewItem>();
            foreach (var c in root.children)
            {
                var child = c as ProfileTreeViewItem;
                if (child != null && child.BuildProfile != null)
                    kids.Add(child);
            }

            IEnumerable<ProfileTreeViewItem> orderedKids = kids;
            orderedKids = ascending
                ? kids.OrderBy(l => l.BuildProfile.profileName)
                : kids.OrderByDescending(l => l.BuildProfile.profileName);

            root.children.Clear();
            foreach (var o in orderedKids)
                root.children.Add(o);
        }

        public static MultiColumnHeaderState CreateDefaultMultiColumnHeaderState(AddressableAssetSettings settings)
        {
            return new MultiColumnHeaderState(GetColumns(settings));
        }

        static MultiColumnHeaderState.Column[] GetColumns(AddressableAssetSettings settings)
        {
            var retVal = new MultiColumnHeaderState.Column[settings.profileSettings.profileEntryNames.Count+1]; // account for profile name columns
            for (int i = 0; i < retVal.Length; i++)
            {
                retVal[i] = new MultiColumnHeaderState.Column();

                if (i == 0)
                {
                    retVal[i].headerContent = new GUIContent("Profile Name", "Profile Name");
                    retVal[i].allowToggleVisibility = false;
                    retVal[i].canSort = true;
                }
                else
                {
                    retVal[i].headerContent = new GUIContent(settings.profileSettings.profileEntryNames[i-1].ProfileName, settings.profileSettings.profileEntryNames[i-1].ProfileName);
                    retVal[i].allowToggleVisibility = false;
                    retVal[i].canSort = false;
                }

                retVal[i].minWidth = 50;
                retVal[i].width = 150;
                retVal[i].maxWidth = 1000;
                retVal[i].headerTextAlignment = TextAlignment.Left;
                retVal[i].autoResize = true;
            }

            return retVal;
        }

        ProfileTreeViewItem FindItemInVisibleRows(int id)
        {
            var rows = GetRows();
            foreach (var r in rows)
            {
                if (r.id == id)
                {
                    return r as ProfileTreeViewItem;
                }
            }
            return null;
        }

        protected override void ContextClickedItem(int id)
        {
            List<ProfileTreeViewItem> selectedNodes = new List<ProfileTreeViewItem>();
            GenericMenu menu = new GenericMenu();

            foreach (var nodeId in GetSelection())
            {
                var item = FindItemInVisibleRows(nodeId); //TODO - this probably makes off-screen but selected items not get added to list.
                if (item != null)
                {
                    selectedNodes.Add(item);
                }
            }

            if (selectedNodes.Count > 1)
            {
                bool hasDefault = false;
                foreach(var node in selectedNodes)
                {
                    if (node.displayName == "Default")
                    {
                        hasDefault = true;
                        menu.AddDisabledItem(new GUIContent("Delete Profiles"));
                        break;
                    }
                }
                if(!hasDefault)
                    menu.AddItem(new GUIContent("Delete Profiles"), false, DeleteProfile, selectedNodes);
            }
            else if (selectedNodes.Count == 1)
            {
                menu.AddItem(new GUIContent("Set Active"), false, UseProfile, selectedNodes);

                if(selectedNodes[0].displayName == "Default")
                {
                    menu.AddDisabledItem(new GUIContent("Rename Profile"));
                    menu.AddDisabledItem(new GUIContent("Delete Profile"));
                }
                else
                {
                    menu.AddItem(new GUIContent("Rename Profile"), false, RenameProfile, selectedNodes);
                    menu.AddItem(new GUIContent("Delete Profile"), false, DeleteProfile, selectedNodes);
                }
            }

            menu.ShowAsContext();
        }

        // Built-in TreeView renaming
        protected override bool CanRename(TreeViewItem item)
        {
            return true;
        }

        protected void RenameProfile(object context)
        {
            List<ProfileTreeViewItem> selectedNodes = context as List<ProfileTreeViewItem>;
            if (selectedNodes != null && selectedNodes.Count >= 1)
            {
                var item = selectedNodes.First();
                BeginRename(item);
            }
        }
        protected override void RenameEnded(RenameEndedArgs args)
        {
            var item = FindItemInVisibleRows(args.itemID);

            if (args.originalName.Equals(args.newName) || args.newName.Trim().Length == 0) // new name cannot only contain spaces
                return;

            if (item != null)
            {
                int changeIndex = -1;
                for (int i = 0; i < m_Window.settings.profileSettings.profiles.Count; i++)
                {
                    // Compare new name with existing profile names
                    if (m_Window.settings.profileSettings.profiles[i].profileName.Equals(args.newName))
                    {
                        return;
                    }

                    // Found name to change
                    if (m_Window.settings.profileSettings.profiles[i].profileName.Equals(args.originalName))
                    {
                        changeIndex = i;
                    }
                }

                if (changeIndex == -1) return;

                // Rename the profile
                m_Window.settings.profileSettings.profiles[changeIndex].profileName = args.newName;

                Reload();
            }
        }

        public Rect GetRow(int i)
        {
            return GetRowRect(i);
        }

        void UseProfile(object context)
        {
            List<ProfileTreeViewItem> selectedNodes = context as List<ProfileTreeViewItem>;
            if (selectedNodes != null && selectedNodes.Count >= 1)
            {
                var item = selectedNodes.First();
                m_Window.settings.activeProfileId = m_Window.settings.profileSettings.profiles.Find((s) => s.id.Equals(item.BuildProfile.id)).id;
            }
        }

        void DeleteProfile(object context)
        {
            List<ProfileTreeViewItem> selectedNodes = context as List<ProfileTreeViewItem>;
            foreach (var item in selectedNodes)
            {
                var prof = m_Window.settings.profileSettings.profiles.Find((s) => s.id.Equals(item.BuildProfile.id));
                if (prof != null)
                {
                    m_Window.settings.profileSettings.RemoveProfile(prof.id);
                    AssetDatabase.SaveAssets();
                }
            }
            Reload();
        }

        internal string GetSelectedProfileId()
        {
            foreach (var nodeId in GetSelection())
            {
                var item = FindItemInVisibleRows(nodeId); //TODO - this probably makes off-screen but selected items not get added to list.
                if (item != null)
                {
                    return item.BuildProfile.id;
                }
            }
            return string.Empty;
        }
    }
}
