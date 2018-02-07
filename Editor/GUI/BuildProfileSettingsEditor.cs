using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using UnityEditor.IMGUI.Controls;
using System.Linq;
using System;

namespace UnityEditor.AddressableAssets
{
    internal class ProfileSettingsEditor
    {
        public string[] profileDisplayNames;
        [SerializeField]
        TreeViewState treeState;
        [SerializeField]
        MultiColumnHeaderState mchs;
        BuildProfileSettingsTreeView tree;
        Rect popupRect;
        public bool dataNeedsReset = true;
        AddressableAssetSettings settings { get { return AddressableAssetSettings.GetDefault(false, false); } }

        public ProfileSettingsEditor()
        {
            ResetData(settings);
        }

        public void ResetData(AddressableAssetSettings settings)
        {
            var names = settings.profileSettings.profileNames;
            profileDisplayNames = new string[names.Count + 1];
            for (int i = 0; i < names.Count; i++)
                profileDisplayNames[i] = names[i];
            profileDisplayNames[names.Count] = "New...";
        }

        public string editingProfile;
 
        public void OnGUI(Rect position)
        {
            if (string.IsNullOrEmpty(editingProfile))
                editingProfile = settings.activeProfile;
            if (dataNeedsReset)
            {
                ResetData(settings);
                CreateTreeView();
                tree.Reload();
                dataNeedsReset = false;
            }

            var buttonHeight = 40;

            GUILayout.BeginArea(new Rect(position.x, position.y, position.width, buttonHeight));
            EditorGUILayout.LabelField("Profiles", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            var currentProfileIndex = settings.profileSettings.GetIndexOfProfile(editingProfile);
            var newProfileIndex = EditorGUILayout.Popup(currentProfileIndex, profileDisplayNames);
            if (Event.current.type == EventType.Repaint) popupRect = GUILayoutUtility.GetLastRect();
            using (new EditorGUI.DisabledScope(editingProfile == settings.profileSettings.DefaultProfileId))
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Delete"))
                {
                    if (EditorUtility.DisplayDialog("Delete Profile", "Are you sure you would like to delete profile " + settings.profileSettings.GetProfileName(editingProfile) + "?", "Delete", "Cancel"))
                    {
                        settings.profileSettings.RemoveProfile(editingProfile);
                        editingProfile = settings.activeProfile;
                        dataNeedsReset = true;
                    }
                    return;
                }
            }
            EditorGUILayout.EndHorizontal();
            GUILayout.EndArea();

            if (newProfileIndex != currentProfileIndex)
            {
                if (newProfileIndex == profileDisplayNames.Length - 1)
                {
                    int profileCount = settings.profileSettings.profileNames.Count;
                    PopupWindow.Show(popupRect, new NewProfilePopup(this, settings.profileSettings));
                }
                else
                {
                    editingProfile = settings.profileSettings.GetProfileAtIndex(newProfileIndex);
                }
                dataNeedsReset = true;
            }

            tree.OnGUI(new Rect(position.x, position.y + buttonHeight, position.width, position.height - buttonHeight));
        }

        private void CreateTreeView()
        {
            if (tree == null)
            {
                if (treeState == null)
                {
                    treeState = new TreeViewState();

                    var headerState = BuildProfileSettingsTreeView.CreateDefaultMultiColumnHeaderState();
                    if (MultiColumnHeaderState.CanOverwriteSerializedFields(mchs, headerState))
                        MultiColumnHeaderState.OverwriteSerializedFields(mchs, headerState);
                    mchs = headerState;
                }
                tree = new BuildProfileSettingsTreeView(treeState, mchs, this);
            }
        }

        static public bool ValueGUI(AddressableAssetSettings settings, string label, AddressableAssetSettings.ProfileSettings.ProfileValue val)
        {
            bool modified = false;
            var names = settings.profileSettings.GetAllVariableNames().ToList();
            names.Sort();
            names.Add("<Custom>");
            string[] displayNames = names.ToArray();
            int currentIndex = val.custom ? (names.Count - 1) : names.IndexOf(settings.profileSettings.GetVariableNameFromId(val.value));
            EditorGUILayout.BeginHorizontal();
            var newIndex = EditorGUILayout.Popup(label, currentIndex, displayNames);
            if (newIndex != currentIndex)
            {
                if (newIndex == names.Count - 1)
                    modified |= val.SetValue(settings.profileSettings, "[" + displayNames[currentIndex] + "]", true);
                else
                    modified |= val.SetValue(settings.profileSettings, settings.profileSettings.GetVariableIdFromName(names[newIndex]), false);
            }
            if (val.custom)
            {
                modified |= val.SetValue(settings.profileSettings, EditorGUILayout.TextField(val.value), true);
            }
            else
            {
                var result = val.Evaluate(settings.profileSettings, settings.activeProfile);
                if (string.IsNullOrEmpty(result))
                    EditorGUILayout.LabelField("<undefined>");
                else
                    EditorGUILayout.LabelField(result);
            }
            EditorGUILayout.EndHorizontal();
            return modified;
        }
    }

    internal class BuildProfileSettingsTreeView : TreeView
    {
        AddressableAssetSettings settings { get { return AddressableAssetSettings.GetDefault(false, false); } }
        ProfileSettingsEditor m_editor;
        GUIStyle normalTextField, boldTextField;

        public enum SortOption
        {
            Label,
            Value,
            Evaluated
        }

        SortOption[] m_SortOptions =
        {
            SortOption.Label,
            SortOption.Value,
            SortOption.Evaluated
        };


        public BuildProfileSettingsTreeView(TreeViewState state, MultiColumnHeaderState mchs, ProfileSettingsEditor editor) : base(state, new MultiColumnHeader(mchs))
        {
            normalTextField = new GUIStyle(EditorStyles.textField);
            boldTextField = new GUIStyle(EditorStyles.textField);
            boldTextField.font = EditorStyles.boldLabel.font;
            boldTextField.fontStyle = EditorStyles.boldLabel.fontStyle;
            showBorder = true;
            m_editor = editor;
            multiColumnHeader.sortingChanged += OnSortingChanged;
        }

        protected override IList<TreeViewItem> BuildRows(TreeViewItem root)
        {
            var rows = base.BuildRows(root);
            SortIfNeeded(root, rows);
            return rows;
        }

        protected override TreeViewItem BuildRoot()
        {
            var root = new TreeViewItem(-1, -1);
            root.children = new List<TreeViewItem>();

            foreach (var v in settings.profileSettings.GetAllVariableIds())
                root.AddChild(new TreeViewItem(v.GetHashCode(), 0, v));
            root.AddChild(new TreeViewItem(0, 0, string.Empty));
            return root;
        }

        protected override void RowGUI(RowGUIArgs args)
        {
            for (int i = 0; i < args.GetNumVisibleColumns(); ++i)
                CellGUI(args.GetCellRect(i), args.item, args.GetColumn(i), ref args);
        }

        private void CellGUI(Rect cellRect, TreeViewItem item, int column, ref RowGUIArgs args)
        {
            if (string.IsNullOrEmpty(item.displayName))
            {
                if ((SortOption)column == SortOption.Label)
                {
                    var name = EditorGUI.DelayedTextField(cellRect, "new...");
                    if (!string.IsNullOrEmpty(name) && name != "new...")
                    {
                        if (settings.profileSettings.ValidateNewVariableName(name))
                        {
                            settings.profileSettings.SetValueByName(m_editor.editingProfile, name, string.Empty, true);
                            m_editor.dataNeedsReset = true;
                        }
                    }
                }
                return;
            }
            CenterRectUsingSingleLineHeight(ref cellRect);
            var style = settings.profileSettings.IsValueInheritedById(m_editor.editingProfile, item.displayName) ? normalTextField : boldTextField;
            var displayName = settings.profileSettings.GetVariableNameFromId(item.displayName);
            switch ((SortOption)column)
            {
                case SortOption.Label:
                    var newName = EditorGUI.DelayedTextField(cellRect, displayName, style);
                    if (newName != displayName)
                    {
                        item.displayName = settings.profileSettings.GetVariableIdFromName(settings.profileSettings.RenameEntry(displayName, newName));
                        m_editor.dataNeedsReset = true;
                    }
                    break;
                case SortOption.Value:
                    var currVal = settings.profileSettings.GetValueById(m_editor.editingProfile, item.displayName);
                    var newVal = EditorGUI.DelayedTextField(cellRect, currVal, style);
                    if (newVal != currVal)
                    {
                        settings.profileSettings.SetValueById(m_editor.editingProfile, item.displayName, newVal);
                        m_editor.dataNeedsReset = true;
                    }
                    break;
                case SortOption.Evaluated:
                    EditorGUI.LabelField(cellRect, settings.profileSettings.Evaluate(m_editor.editingProfile, settings.profileSettings.GetValueById(m_editor.editingProfile, item.displayName)));
                    break;
            }
        }

        void OnSortingChanged(MultiColumnHeader multiColumnHeader)
        {
            SortIfNeeded(rootItem, GetRows());
        }

        void SortIfNeeded(TreeViewItem root, IList<TreeViewItem> rows)
        {
            if (rows.Count <= 1)
                return;

            if (multiColumnHeader.sortedColumnIndex == -1)
                return;

            SortByColumn(multiColumnHeader.IsSortedAscending(multiColumnHeader.sortedColumnIndex));

            rows.Clear();
            for (int i = 0; i < root.children.Count; i++)
                rows.Add(root.children[i]);

            Repaint();
        }

        void SortByColumn(bool ascending)
        {
            var sortedColumns = multiColumnHeader.state.sortedColumns;

            if (sortedColumns.Length == 0)
                return;

            List<TreeViewItem> assetList = new List<TreeViewItem>();
            foreach (var item in rootItem.children)
                assetList.Add(item);
            var orderedItems = InitialOrder(assetList, sortedColumns, ascending);

            rootItem.children = orderedItems.Cast<TreeViewItem>().ToList();
        }

        IOrderedEnumerable<TreeViewItem> InitialOrder(IEnumerable<TreeViewItem> myTypes, int[] columnList, bool ascending)
        {
            SortOption sortOption = m_SortOptions[columnList[0]];
            Func<TreeViewItem, string> orderFunc = null;
            switch (sortOption)
            {
                case SortOption.Label:
                    orderFunc = (l => settings.profileSettings.GetVariableNameFromId(l.displayName));
                    break;
                case SortOption.Value:
                    orderFunc = (l => settings.profileSettings.GetValueById(m_editor.editingProfile, l.displayName));
                    break;
                case SortOption.Evaluated:
                    orderFunc = l => settings.profileSettings.Evaluate(m_editor.editingProfile, settings.profileSettings.GetValueById(m_editor.editingProfile, l.displayName));
                    break;
                default:
                    orderFunc = l => l.displayName;
                    break;
            }
            if (ascending)
                return myTypes.OrderBy(orderFunc);
            else
                return myTypes.OrderByDescending(orderFunc);
        }

        protected override bool CanBeParent(TreeViewItem item)
        {
            return false;
        }

        public static MultiColumnHeaderState CreateDefaultMultiColumnHeaderState()
        {
            return new MultiColumnHeaderState(GetColumns());
        }

        private static MultiColumnHeaderState.Column[] GetColumns()
        {
            var retVal = new MultiColumnHeaderState.Column[] {
                new MultiColumnHeaderState.Column(),
                new MultiColumnHeaderState.Column(),
                new MultiColumnHeaderState.Column(),
            };

            retVal[0].headerContent = new GUIContent("Name", "");
            retVal[0].minWidth = 100;
            retVal[0].width = 300;
            retVal[0].maxWidth = 500;
            retVal[0].headerTextAlignment = TextAlignment.Left;
            retVal[0].canSort = true;
            retVal[0].autoResize = true;

            retVal[1].headerContent = new GUIContent("Value", "");
            retVal[1].minWidth = 100;
            retVal[1].width = 500;
            retVal[1].maxWidth = 1000;
            retVal[1].headerTextAlignment = TextAlignment.Left;
            retVal[1].canSort = true;
            retVal[1].autoResize = true;

            retVal[2].headerContent = new GUIContent("Evaluated", "");
            retVal[2].minWidth = 100;
            retVal[2].width = 500;
            retVal[2].maxWidth = 1000;
            retVal[2].headerTextAlignment = TextAlignment.Left;
            retVal[2].canSort = true;
            retVal[2].autoResize = true;
            return retVal;
        }
    }

    class NewProfilePopup : PopupWindowContent
    {
        internal NewProfilePopup(ProfileSettingsEditor editor, AddressableAssetSettings.ProfileSettings settings)
        {
            m_settings = settings;
            m_editor = editor;
        }

        AddressableAssetSettings.ProfileSettings m_settings;
        ProfileSettingsEditor m_editor;
        public string profileName;
        string parentId;
        string[] profileNames;
        bool firstUpdate = true;
        bool apply = false;
        public override Vector2 GetWindowSize()
        {
            return new Vector2(300, 200);
        }

        public override void OnOpen()
        {
            profileNames = m_settings.profileNames.ToArray();
        }

        public override void OnClose()
        {
            if (apply && !string.IsNullOrEmpty(profileName) && Array.IndexOf(profileNames, profileName) < 0)
            {
                var newProfileId = m_settings.AddProfile(profileName, parentId);
                if (!string.IsNullOrEmpty(newProfileId))
                {
                    m_editor.editingProfile = newProfileId;
                    m_editor.dataNeedsReset = true;
                }
            }
            else
            {
                profileName = null;
            }
        }

        public override void OnGUI(Rect rect)
        {
            GUI.SetNextControlName("ProfileName");
            var newName = EditorGUILayout.TextField("Profile Name", profileName);
            if (newName != profileName)
                profileName = newName;

            GUI.SetNextControlName("ProfileParent");
            var parentIndex = m_settings.GetIndexOfProfile(parentId);
            var index = EditorGUILayout.Popup("Inherit From", parentIndex, profileNames);
            if (index != parentIndex)
                parentId = m_settings.GetProfileAtIndex(index);

            GUILayout.BeginArea(new Rect(0, 170, 300, 30));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Cancel"))
                editorWindow.Close();
            GUI.SetNextControlName("ProfileApply");
            if (GUILayout.Button("Apply"))
            {
                apply = true;
                editorWindow.Close();
            }
            EditorGUILayout.EndHorizontal();
            GUILayout.EndArea();

            if (firstUpdate)
            {
                EditorGUI.FocusTextInControl("ProfileName");
                firstUpdate = false;
            }
        }
    }
}
