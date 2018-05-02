using UnityEngine;
using System.Collections.Generic;
using UnityEditor.IMGUI.Controls;
using System.Linq;
using System;

namespace UnityEditor.AddressableAssets
{
    public class ProfilesWindow : EditorWindow
    {
        AddressableAssetSettings settings = null;
        ProfileEditorTree tree = null;
        [SerializeField]
        TreeViewState treeState;
        [SerializeField]
        MultiColumnHeaderState mchs;

        public static void OpenWindow()
        {
            var window = GetWindow<ProfilesWindow>();
            window.titleContent = new GUIContent("AA Profiles");
            window.Show();
        }


        public void OnGUI()
        {
            if (settings == null)
            {
                settings = AddressableAssetSettings.GetDefault(false, false);
            }

            if (settings == null)
            {
                GUILayout.Label("no settings object found. Start using Addressable Assets before using this window.");
                return;
            }
            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Manage Profile list"))
                ManageProfileList();

            if (GUILayout.Button("Add Entry"))
            {
                settings.profileSettings.CreateValue("NewVariable" + UnityEngine.Random.Range(0, 1000).ToString(), "", AddressableAssetSettings.ProfileSettings.ProfileEntryUsage.Other);
                tree.Reload();
            }

            if (GUILayout.Button("Remove Entry"))
            {
                tree.RemoveSelected();
            }

            GUILayout.EndHorizontal();


            if (tree == null || !tree.isValid)
            {
                if (treeState == null)
                    treeState = new TreeViewState();

                var headerState = ProfileEditorTree.CreateDefaultMultiColumnHeaderState(settings.profileSettings);
                if (MultiColumnHeaderState.CanOverwriteSerializedFields(mchs, headerState))
                    MultiColumnHeaderState.OverwriteSerializedFields(mchs, headerState);
                mchs = headerState;

                tree = new ProfileEditorTree(treeState, mchs, settings.profileSettings);
                tree.Reload();
            }


            tree.OnGUI(new Rect(0, 24, position.width, position.height - 24));
        }

        private void ManageProfileList()
        {
            PopupWindow.Show(new Rect(0,0,10,10), new ProfileNamesPopup(settings, tree));
        }
        class ProfileNamesPopup : PopupWindowContent
        {
            private AddressableAssetSettings m_settings;
            private ProfileEditorTree m_tree;
            public ProfileNamesPopup(AddressableAssetSettings settings, ProfileEditorTree tree)
            {
                m_settings = settings;
                m_tree = tree;
            }
            Vector2 m_scrollPos = new Vector2(0, 0);
            public override void OnGUI(Rect rect)
            {
                EditorGUILayout.BeginScrollView(m_scrollPos);
                List<AddressableAssetSettings.ProfileSettings.BuildProfile> toRemove = new List<AddressableAssetSettings.ProfileSettings.BuildProfile>();
                foreach(var prof in m_settings.profileSettings.profiles)
                {
                    EditorGUILayout.BeginHorizontal();
                    prof.profileName = EditorGUILayout.DelayedTextField(prof.profileName);
                    if (EditorGUILayout.DropdownButton(new GUIContent("X"), FocusType.Passive))
                    {
                        toRemove.Add(prof);
                        m_tree.isValid = false;
                    }
                    EditorGUILayout.EndHorizontal();
                }
                foreach (var rem in toRemove)
                    m_settings.profileSettings.profiles.Remove(rem);
                if (EditorGUILayout.DropdownButton(new GUIContent("New"), FocusType.Passive))
                {
                    m_settings.profileSettings.AddProfile("New", m_settings.activeProfileId);
                    m_tree.isValid = false;
                }
                EditorGUILayout.EndScrollView();
            }

        }
        static public string ValueGUI(AddressableAssetSettings settings, string label, string currentID, AddressableAssetSettings.ProfileSettings.ProfileEntryUsage usage)
        {
            string result = currentID;
            
            var names = settings.profileSettings.GetAllVariableNames(usage).ToList();
            names.Sort();
            string[] displayNames = names.ToArray();
            AddressableAssetSettings.ProfileSettings.ProfileIDData data = settings.profileSettings.GetProfileDataById(currentID);
            int currentIndex = names.IndexOf(data == null ? AddressableAssetSettings.ProfileSettings.k_customEntryString : data.name);
            bool custom = data == null && currentIndex >= 0;
            EditorGUILayout.BeginHorizontal();
            var newIndex = 0;
            if(displayNames.Length ==1)
                EditorGUILayout.LabelField(label, displayNames[0]);
            else
                newIndex = EditorGUILayout.Popup(label, currentIndex, displayNames);
            if (newIndex != currentIndex)
            {
                if(displayNames[newIndex] == AddressableAssetSettings.ProfileSettings.k_customEntryString)
                {
                    custom = true;
                    result = "<undefined>";
                }
                else
                {
                    data = settings.profileSettings.GetProfileDataByName(displayNames[newIndex]);
                    if (data != null)
                        result = data.id;
                }
            }
            if (custom)
            {
                result = EditorGUILayout.TextField(result);
            }
            else
            {
                var evaluated = data == null ? settings.profileSettings.EvaluateString(settings.activeProfileId, result) : data.Evaluate(settings.profileSettings, settings.activeProfileId);
                if (string.IsNullOrEmpty(evaluated))
                    EditorGUILayout.LabelField("<undefined>");
                else
                    EditorGUILayout.LabelField(evaluated);
            }
            EditorGUILayout.EndHorizontal();
            return result;
        }

        public class ProfileEditorTree : TreeView
        {
            AddressableAssetSettings.ProfileSettings m_profileSettings;
            List<string> m_columnIds = new List<string>();
            public bool isValid = true;

            public ProfileEditorTree(TreeViewState state, MultiColumnHeaderState mchs, AddressableAssetSettings.ProfileSettings profileSettings) : base(state, new MultiColumnHeader(mchs))
            {
                m_profileSettings = profileSettings;
                showAlternatingRowBackgrounds = true;
                showBorder = true;


                m_columnIds.Clear();
                foreach(var prof in m_profileSettings.profiles)
                {
                    m_columnIds.Add(prof.id);
                }
            }
            protected override TreeViewItem BuildRoot()
            {
                var root = new TreeViewItem(-1, -1);

                foreach (var e in m_profileSettings.profileEntryNames)
                {
                    if (e.name != AddressableAssetSettings.ProfileSettings.k_customEntryString)
                        root.AddChild(new ProfileEditorTreeViewItem(e));
                }

                return root;
            }
            protected override bool CanRename(TreeViewItem item)
            {
                return true;
            }
            protected override void RenameEnded(RenameEndedArgs args)
            {
                var item = FindItem(args.itemID, rootItem) as ProfileEditorTreeViewItem;
                if (item != null)
                {
                    item.entry.name = args.newName;
                    Reload();
                }
            }
            public override void OnGUI(Rect rect)
            {
                base.OnGUI(rect);
                
                if (Event.current.type == EventType.MouseDown &&
                    Event.current.button == 0 &&
                    rect.Contains(Event.current.mousePosition))
                {
                    SetSelection(new int[0], TreeViewSelectionOptions.FireSelectionChanged);
                }
            }

            protected override void RowGUI(RowGUIArgs args)
            {
                for (int i = 0; i < args.GetNumVisibleColumns(); ++i)
                    CellGUI(args.GetCellRect(i), args.item as ProfileEditorTreeViewItem, args.GetColumn(i), ref args);
            }
            const int k_numPrefixColumns = 2;
            private void CellGUI(Rect cellRect, ProfileEditorTreeViewItem item, int column, ref RowGUIArgs args)
            {
                if (item == null)
                    return;

                if (column == 0)
                {
                    EditorGUI.LabelField(cellRect, item.displayName);
                }
                else if(column == 1)
                {
                    item.entry.usage = (AddressableAssetSettings.ProfileSettings.ProfileEntryUsage)EditorGUI.EnumPopup(cellRect, item.entry.usage);
                }
                else
                {
                    var oldVal = m_profileSettings.GetValueById(m_columnIds[column - k_numPrefixColumns], item.entry.id);
                    var newVal = EditorGUI.DelayedTextField(cellRect, oldVal);
                    if (oldVal != newVal)
                        m_profileSettings.SetValue(m_columnIds[column - k_numPrefixColumns], item.entry.name, newVal);
                }
            }

            public static MultiColumnHeaderState CreateDefaultMultiColumnHeaderState(AddressableAssetSettings.ProfileSettings profileSettings)
            {
                return new MultiColumnHeaderState(GetColumns(profileSettings));
            }

            private static MultiColumnHeaderState.Column[] GetColumns(AddressableAssetSettings.ProfileSettings profileSettings)
            {
                var columnCount = profileSettings.profiles.Count+ k_numPrefixColumns;
                var retVal = new MultiColumnHeaderState.Column[columnCount];
                retVal[0] = new MultiColumnHeaderState.Column();
                retVal[0].headerContent = new GUIContent("Entry Name", "Name to lookup profile data");
                retVal[0].minWidth = 100;
                retVal[0].width = 200;
                retVal[0].maxWidth = 500;
                retVal[0].headerTextAlignment = TextAlignment.Left;
                retVal[0].canSort = true;
                retVal[0].autoResize = true;

                retVal[1] = new MultiColumnHeaderState.Column();
                retVal[1].headerContent = new GUIContent("Entry Usage", "What context this variable can be used in");
                retVal[1].minWidth = 100;
                retVal[1].width = 125;
                retVal[1].maxWidth = 250;
                retVal[1].headerTextAlignment = TextAlignment.Left;
                retVal[1].canSort = true;
                retVal[1].autoResize = true;


                int column = k_numPrefixColumns;
                foreach(var prof in profileSettings.profiles)
                {
                    retVal[column] = new MultiColumnHeaderState.Column();
                    retVal[column].headerContent = new GUIContent("Profile: " + prof.profileName, "");
                    retVal[column].minWidth = 50;
                    retVal[column].width = 300;
                    retVal[column].maxWidth = 500;
                    retVal[column].headerTextAlignment = TextAlignment.Left;
                    retVal[column].canSort = true;
                    retVal[column].autoResize = true;

                    column++;
                }

                return retVal;
            }

            internal void RemoveSelected()
            {
                var sel = GetSelection();
                foreach (var s in sel)
                {
                    var item = FindItem(s, rootItem) as ProfileEditorTreeViewItem;
                    if(item != null)
                    {
                        m_profileSettings.RemoveValue(item.entry.id);
                    }
                }
                Reload();
            }

            public class ProfileEditorTreeViewItem : TreeViewItem
            {
                public AddressableAssetSettings.ProfileSettings.ProfileIDData entry;
                public ProfileEditorTreeViewItem(AddressableAssetSettings.ProfileSettings.ProfileIDData e) : base(e.id.GetHashCode(), 0, e.name)
                {
                    entry = e;
                }
            }

            
        }
    }
}
