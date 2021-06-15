using System;
using System.Collections.Generic;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEngine.AddressableAssets;
using System.Linq;

namespace UnityEditor.AddressableAssets.GUI
{
    internal class ProfileWindow : EditorWindow
    {
        //Min and Max proportion of the window that ProfilePane can take up
        const float k_MinProfilePaneWidth = 0.10f;
        const float k_MaxProfilePaneWidth = 0.6f;

        private const float k_MinLabelWidth = 155f;
        private const float k_ApproxCharWidth = 8.5f;

        const float k_DefaultHorizontalSplitterRatio = 0.33f;
        const int k_SplitterThickness = 2;
        const int k_ToolbarHeight = 20;
        const int k_ItemRectPadding = 15;

        //amount of padding between variable items
        const float k_VariableItemPadding = 5f;

        //Default length of the Label within the Variables Pane
        private float m_LabelWidth = 155f;
        private float m_FieldBufferWidth = 0f;

        //Separator
        private const char k_PrefixSeparator = '.';

        GUIStyle m_ItemRectPadding;

        float m_HorizontalSplitterRatio = k_DefaultHorizontalSplitterRatio;

        internal AddressableAssetSettings settings
        {
            get { return AddressableAssetSettingsDefaultObject.Settings; }
        }

        private ProfileTreeView m_ProfileTreeView;

        private bool m_IsResizingHorizontalSplitter;
        internal static bool m_Reload = false;

        private Vector2 m_ProfilesPaneScrollPosition;
        private Vector2 m_VariablesPaneScrollPosition;

        private int m_ProfileIndex = -1;
        public int ProfileIndex
        {
            get { return m_ProfileIndex; }
            set { m_ProfileIndex = value; }
        }


        private GUIStyle m_ButtonStyle;


        private Dictionary<string, bool?> m_foldouts = new Dictionary<string, bool?>();

        [MenuItem("Window/Asset Management/Addressables/Profiles", priority = 2051)]
        internal static void ShowWindow()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                EditorUtility.DisplayDialog("Error", "Attempting to open Addressables Profiles window, but no Addressables Settings file exists.  \n\nOpen 'Window/Asset Management/Addressables/Groups' for more info.", "Ok");
                return;
            }
            GetWindow<ProfileWindow>().Show();
        }

        internal static void DrawOutline(Rect rect, float size)
        {
            Color color = new Color(0.6f, 0.6f, 0.6f, 1.333f);
            if (EditorGUIUtility.isProSkin)
            {
                color.r = 0.12f;
                color.g = 0.12f;
                color.b = 0.12f;
            }

            if (Event.current.type != EventType.Repaint)
                return;

            Color orgColor = UnityEngine.GUI.color;
            UnityEngine.GUI.color = UnityEngine.GUI.color * color;
            UnityEngine.GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, size), EditorGUIUtility.whiteTexture);
            UnityEngine.GUI.DrawTexture(new Rect(rect.x, rect.yMax - size, rect.width, size), EditorGUIUtility.whiteTexture);
            UnityEngine.GUI.DrawTexture(new Rect(rect.x, rect.y + 1, size, rect.height - 2 * size), EditorGUIUtility.whiteTexture);
            UnityEngine.GUI.DrawTexture(new Rect(rect.xMax - size, rect.y + 1, size, rect.height - 2 * size), EditorGUIUtility.whiteTexture);

            UnityEngine.GUI.color = orgColor;
        }

        private void OnEnable()
        {
            
            Undo.undoRedoPerformed += MarkForReload;
            titleContent = new GUIContent("Addressables Profiles");
            m_ItemRectPadding = new GUIStyle();
            m_ItemRectPadding.padding = new RectOffset(k_ItemRectPadding, k_ItemRectPadding, k_ItemRectPadding, k_ItemRectPadding);
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= MarkForReload;
        }

        internal static void MarkForReload()
        {
            m_Reload = true;
        }

        GUIStyle GetStyle(string styleName)
        {
            GUIStyle s = UnityEngine.GUI.skin.FindStyle(styleName);
            if (s == null)
                s = EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector).FindStyle(styleName);
            if (s == null)
            {
                Addressables.LogError("Missing built-in guistyle " + styleName);
                s = new GUIStyle();
            }
            return s;
        }

        void DeleteVariable(AddressableAssetProfileSettings.ProfileIdData toBeDeleted)
        {
            Undo.RecordObject(settings, "Profile Variable Deleted");
            settings.profileSettings.RemoveValue(toBeDeleted.Id);
            AddressableAssetUtility.OpenAssetIfUsingVCIntegration(settings);
        }

        void TopToolbar(Rect toolbarPos)
        {
            if (m_ButtonStyle == null)
                m_ButtonStyle = GetStyle("ToolbarButton");

            m_ButtonStyle.alignment = TextAnchor.MiddleLeft;

            GUILayout.BeginArea(new Rect(0, 0, toolbarPos.width, k_ToolbarHeight));
            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            {
                var guiMode = new GUIContent("Create");
                Rect rMode = GUILayoutUtility.GetRect(guiMode, EditorStyles.toolbarDropDown);
                if (EditorGUI.DropdownButton(rMode, guiMode, FocusType.Passive, EditorStyles.toolbarDropDown))
                {
                    var menu = new GenericMenu();
                    menu.AddItem(new GUIContent("Profile"), false, NewProfile);
                    menu.AddItem(new GUIContent("Variable (All Profiles)"), false, () => EditorApplication.delayCall += NewVariable);
                    menu.AddItem(new GUIContent("Build & Load Path Variables (All Profiles)"), false, () => EditorApplication.delayCall += NewPathPair);
                    menu.DropDown(rMode);
                }
                GUILayout.FlexibleSpace();
            }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        void NewVariable()
        {
            try
            {
                PopupWindow.Show(new Rect(position.x, position.y + k_ToolbarHeight, position.width, k_ToolbarHeight),
                    new ProfileNewVariablePopup(position.width, position.height, 0, m_ProfileTreeView, settings));
            }
            catch (ExitGUIException)
            {
                // Exception not being caught through OnGUI call
            }
        }

        void NewPathPair()
        {
            try
            {
                PopupWindow.Show(new Rect(position.x, position.y + k_ToolbarHeight, position.width, k_ToolbarHeight),
                    new ProfileNewPathPairPopup(position.width, position.height, 0, m_ProfileTreeView, settings));
            }
            catch (ExitGUIException)
            {
                // Exception not being caught through OnGUI call
            }
        }

        //Contains all of the profile names, primarily implemented in ProfileTreeView
        void ProfilesPane(Rect profilesPaneRect)
        {
            DrawOutline(profilesPaneRect, 1);
            GUILayout.BeginArea(profilesPaneRect);
            {
                m_ProfilesPaneScrollPosition = GUILayout.BeginScrollView(m_ProfilesPaneScrollPosition);
                Rect r = new Rect(profilesPaneRect);
                r.y = 0;

                var profiles = settings.profileSettings.profiles;

                if (m_ProfileTreeView == null || m_ProfileTreeView.Names.Count != profiles.Count || m_Reload)
                {
                    m_Reload = false;
                    m_ProfileTreeView = new ProfileTreeView(new TreeViewState(), profiles, this, ProfileTreeView.CreateHeader());
                }

                m_ProfileTreeView.OnGUI(r);
                GUILayout.EndScrollView();
            }
            GUILayout.EndArea();
        }

        //Displays all variables for the currently selected profile and initializes each variable's context menu
        void VariablesPane(Rect variablesPaneRect)
        {
            DrawOutline(variablesPaneRect, 1);
            Event evt = Event.current;
            AddressableAssetProfileSettings.BuildProfile selectedProfile = GetSelectedProfile();

            if (selectedProfile == null) return;
            if (evt.isMouse || evt.isKey)
            {
                m_ProfileTreeView.lastClickedProfile = ProfileIndex;
            }

            //ensures amount of visible text is not affected by label width
            float fieldWidth = variablesPaneRect.width - (2 * k_ItemRectPadding) + m_FieldBufferWidth;

            if (!EditorGUIUtility.labelWidth.Equals(m_LabelWidth))
                EditorGUIUtility.labelWidth = m_LabelWidth;

            int maxLabelLen = 0;
            int maxFieldLen = 0;

            GUILayout.BeginArea(variablesPaneRect);
            EditorGUI.indentLevel++;
            List<ProfileGroupType> groupTypes = ProfileGroupType.CreateGroupTypes(selectedProfile);
            HashSet<string> drawnGroupTypes = new HashSet<string>();

            //Displaying Path Groups
            foreach (ProfileGroupType groupType in groupTypes)
            {
                bool? foldout;
                m_foldouts.TryGetValue(groupType.GroupTypePrefix, out foldout);
                GUILayout.Space(5);
                m_foldouts[groupType.GroupTypePrefix] = EditorGUILayout.Foldout(foldout != null ? foldout.Value : true, groupType.GroupTypePrefix, true);
                //Specific Grouped variables
                List<ProfileGroupType.GroupTypeVariable> pathVariables = new List<ProfileGroupType.GroupTypeVariable>();
                pathVariables.Add(groupType.GetVariableBySuffix("BuildPath"));
                drawnGroupTypes.Add(groupType.GetName(groupType.GetVariableBySuffix("BuildPath")));
                pathVariables.Add(groupType.GetVariableBySuffix("LoadPath"));
                drawnGroupTypes.Add(groupType.GetName(groupType.GetVariableBySuffix("LoadPath")));

                if (m_foldouts[groupType.GroupTypePrefix].Value)
                {
                    EditorGUI.indentLevel++;

                    //Displaying Path Groups
                    foreach(var variable in pathVariables)
                    {
                        Rect newPathRect = EditorGUILayout.BeginVertical();
                        string newPath = EditorGUILayout.TextField(groupType.GetName(variable), variable.Value, new GUILayoutOption[]{ GUILayout.Width(fieldWidth)});
                        EditorGUILayout.EndVertical();
                        if (evt.type == EventType.ContextClick)
                        {
                            CreateVariableContextMenu(variablesPaneRect, newPathRect, settings.profileSettings.GetProfileDataByName(groupType.GetName(variable)), evt);
                        }
                        if (newPath != variable.Value && ProfileIndex == m_ProfileTreeView.lastClickedProfile)
                        {
                            Undo.RecordObject(settings, "Variable value changed");
                            settings.profileSettings.SetValue(selectedProfile.id, groupType.GetName(variable), newPath);
                            AddressableAssetUtility.OpenAssetIfUsingVCIntegration(settings);
                        }
                    }
                    EditorGUI.indentLevel--;
                }
            }

            //Display all other variables
            for (var i = 0; i < settings.profileSettings.profileEntryNames.Count; i++)
            {
                AddressableAssetProfileSettings.ProfileIdData curVariable = settings.profileSettings.profileEntryNames[i];
                if (!drawnGroupTypes.Contains(curVariable.ProfileName))
                {
                    GUILayout.Space(5);
                    Rect newValueRect = EditorGUILayout.BeginVertical();
                    string newValue = EditorGUILayout.TextField(curVariable.ProfileName, selectedProfile.values[i].value, new GUILayoutOption[] { GUILayout.Width(fieldWidth) });
                    EditorGUILayout.EndVertical();
                    if (newValue != selectedProfile.values[i].value && ProfileIndex == m_ProfileTreeView.lastClickedProfile)
                    {
                        Undo.RecordObject(settings, "Variable value changed");
                        settings.profileSettings.SetValue(selectedProfile.id, settings.profileSettings.profileEntryNames[i].ProfileName, newValue);
                        AddressableAssetUtility.OpenAssetIfUsingVCIntegration(settings);
                    }

                    if (evt.type == EventType.ContextClick)
                    {
                        CreateVariableContextMenu(variablesPaneRect, newValueRect, curVariable, evt);
                    }
                }
                maxLabelLen = Math.Max(maxLabelLen, curVariable.ProfileName.Length);
            }

            EditorGUI.indentLevel--;
            GUILayout.EndArea();

            //Update the label width to the maximum of the minimum acceptable label width and the amount of
            //space required to contain the longest variable name
            m_LabelWidth = Mathf.Max(maxLabelLen * k_ApproxCharWidth, k_MinLabelWidth);
            m_FieldBufferWidth = Mathf.Clamp((maxFieldLen * k_ApproxCharWidth) - fieldWidth, 0f, float.MaxValue);
        }

        

        //Creates the context menu for the selected variable
        void CreateVariableContextMenu(Rect parentWindow, Rect menuRect, AddressableAssetProfileSettings.ProfileIdData variable, Event evt)
        {
            if (menuRect.Contains(evt.mousePosition))
            {
                GenericMenu menu = new GenericMenu();
                //Displays name of selected variable so user can be confident they're deleting/renaming the right one
                menu.AddDisabledItem(new GUIContent(variable.ProfileName));
                menu.AddSeparator("");
                menu.AddItem(new GUIContent("Rename Variable (All Profiles)"), false, () => { RenameVariable(variable, parentWindow, menuRect); });
                menu.AddItem(new GUIContent("Delete Variable (All Profiles)"), false, () => { DeleteVariable(variable); });
                menu.ShowAsContext();
                evt.Use();
            }
        }

        //Opens ProfileRenameVariablePopup
        void RenameVariable(AddressableAssetProfileSettings.ProfileIdData profileVariable, Rect parentWindow, Rect displayRect)
        {
            try
            {
                //Determines the current variable rect location
                Rect variableRect = new Rect(position.x + parentWindow.x + displayRect.x, position.y + parentWindow.y + displayRect.y, position.width, k_ToolbarHeight);
                PopupWindow.Show(variableRect, new ProfileRenameVariablePopup(displayRect, profileVariable, settings));
            }
            catch (ExitGUIException)
            {
                // Exception not being caught through OnGUI call
            }
        }

        //Returns the BuildProfile currently selected in the ProfilesPane
        AddressableAssetProfileSettings.BuildProfile GetSelectedProfile()
        {
            return m_ProfileTreeView.GetSelectedProfile();
        }

        //Creates a new BuildProfile and reloads the ProfilesPane
        void NewProfile()
        {
            var uniqueProfileName = settings.profileSettings.GetUniqueProfileName("New Profile");
            if (!string.IsNullOrEmpty(uniqueProfileName))
            {
                Undo.RecordObject(settings, "New Profile Created");
                //Either copy values from the selected profile, or if there is no selected profile, copy from the default
                string idToCopyFrom = GetSelectedProfile() != null
                    ? GetSelectedProfile().id
                    : settings.profileSettings.profiles[0].id;
                settings.profileSettings.AddProfile(uniqueProfileName, idToCopyFrom);
                AddressableAssetUtility.OpenAssetIfUsingVCIntegration(settings);
                m_ProfileTreeView.Reload();
            }
        }

        private void OnGUI()
        {
            if (settings == null) return;

            if (m_IsResizingHorizontalSplitter)
                m_HorizontalSplitterRatio = Mathf.Clamp(Event.current.mousePosition.x / position.width,
                    k_MinProfilePaneWidth, k_MaxProfilePaneWidth);

            var toolbarRect = new Rect(0, 0, position.width, position.height);
            var profilesPaneRect = new Rect(0, k_ToolbarHeight, (position.width * m_HorizontalSplitterRatio), position.height);
            var variablesPaneRect = new Rect(profilesPaneRect.width + k_SplitterThickness, k_ToolbarHeight,
                position.width - profilesPaneRect.width - k_SplitterThickness, position.height - k_ToolbarHeight);
            var horizontalSplitterRect = new Rect(profilesPaneRect.width, k_ToolbarHeight, k_SplitterThickness, position.height - k_ToolbarHeight);

            EditorGUIUtility.AddCursorRect(horizontalSplitterRect, MouseCursor.ResizeHorizontal);
            if (Event.current.type == EventType.MouseDown && horizontalSplitterRect.Contains(Event.current.mousePosition))
                m_IsResizingHorizontalSplitter = true;
            else if (Event.current.type == EventType.MouseUp)
                m_IsResizingHorizontalSplitter = false;

            TopToolbar(toolbarRect);

            ProfilesPane(profilesPaneRect);

            VariablesPane(variablesPaneRect);

            if (m_IsResizingHorizontalSplitter)
                Repaint();
        }

        class ProfileRenameVariablePopup : PopupWindowContent
        {
            internal Rect m_WindowRect;
            internal AddressableAssetProfileSettings.ProfileIdData m_ProfileVariable;
            internal AddressableAssetSettings m_Settings;
            internal string m_NewName;
            public ProfileRenameVariablePopup(Rect fieldRect, AddressableAssetProfileSettings.ProfileIdData profileVariable, AddressableAssetSettings settings)
            {
                m_WindowRect = fieldRect;
                m_ProfileVariable = profileVariable;
                m_Settings = settings;
                m_NewName = m_ProfileVariable.ProfileName;
                UnityEngine.GUI.enabled = true;
            }

            public override Vector2 GetWindowSize()
            {
                return new Vector2(m_WindowRect.width, m_WindowRect.height);
            }

            public override void OnGUI(Rect windowRect)
            {
                GUILayout.BeginArea(windowRect);

                Event evt = Event.current;
                bool hitEnter = evt.type == EventType.KeyDown && (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter);

                m_NewName = GUILayout.TextField(m_NewName);
                if (hitEnter)
                {
                    if (string.IsNullOrEmpty(m_NewName))
                        Debug.LogError("Variable name cannot be empty.");
                    else if (m_NewName != m_Settings.profileSettings.GetUniqueProfileEntryName(m_NewName))
                        Debug.LogError("Profile variable '" + m_NewName + "' already exists.");
                    else if (m_NewName.Trim().Length == 0) // new name cannot only contain spaces
                        Debug.LogError("Name cannot be only spaces");
                    else
                    {
                        Undo.RecordObject(m_Settings, "Profile Variable Renamed");
                        m_ProfileVariable.SetName(m_NewName, m_Settings.profileSettings);
                        AddressableAssetUtility.OpenAssetIfUsingVCIntegration(m_Settings, true);
                        editorWindow.Close();
                    }
                }
                GUILayout.EndArea();
            }
        }


        class ProfileNewVariablePopup : PopupWindowContent
        {
            internal float m_WindowWidth;
            internal float m_WindowHeight;
            internal float m_xOffset;
            internal string m_Name;
            internal string m_Value;
            internal bool m_NeedsFocus = true;
            internal AddressableAssetSettings m_Settings;

            ProfileTreeView m_ProfileTreeView;

            public ProfileNewVariablePopup(float width, float height, float xOffset, ProfileTreeView profileTreeView, AddressableAssetSettings settings)
            {
                m_WindowWidth = width;
                m_WindowHeight = height;
                m_xOffset = xOffset;
                m_Settings = settings;
                m_Name = m_Settings.profileSettings.GetUniqueProfileEntryName("New Entry");
                m_Value = Application.dataPath;

                m_ProfileTreeView = profileTreeView;
            }

            public override Vector2 GetWindowSize()
            {
                float width = Mathf.Clamp(m_WindowWidth * 0.375f, Mathf.Min(600, m_WindowWidth - m_xOffset), m_WindowWidth);
                float height = Mathf.Clamp(65, Mathf.Min(65, m_WindowHeight), m_WindowHeight);
                return new Vector2(width, height);
            }

            public override void OnGUI(Rect windowRect)
            {
                GUILayout.Space(5);
                Event evt = Event.current;
                bool hitEnter = evt.type == EventType.KeyDown && (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter);
                EditorGUIUtility.labelWidth = 90;
                m_Name = EditorGUILayout.TextField("Variable Name", m_Name);
                m_Value = EditorGUILayout.TextField("Default Value", m_Value);

                UnityEngine.GUI.enabled = m_Name.Length != 0;
                if (GUILayout.Button("Save") || hitEnter)
                {
                    if (string.IsNullOrEmpty(m_Name))
                        Debug.LogError("Variable name cannot be empty.");
                    else if (m_Name != m_Settings.profileSettings.GetUniqueProfileEntryName(m_Name))
                        Debug.LogError("Profile variable '" + m_Name + "' already exists.");
                    else
                    {
                        Undo.RecordObject(m_Settings, "Profile Variable " + m_Name + " Created");
                        m_Settings.profileSettings.CreateValue(m_Name, m_Value);
                        AddressableAssetUtility.OpenAssetIfUsingVCIntegration(m_Settings);
                        m_ProfileTreeView.Reload();
                        editorWindow.Close();
                    }
                }
            }
        }

        class ProfileNewPathPairPopup : PopupWindowContent
        {
            internal float m_WindowWidth;
            internal float m_WindowHeight;
            internal float m_xOffset;
            internal string m_Name;
            internal string m_BuildPath;
            internal string m_LoadPath;
            internal bool m_NeedsFocus = true;
            internal AddressableAssetSettings m_Settings;

            ProfileTreeView m_ProfileTreeView;

            public ProfileNewPathPairPopup(float width, float height, float xOffset, ProfileTreeView profileTreeView, AddressableAssetSettings settings)
            {
                m_WindowWidth = width;
                m_WindowHeight = height;
                m_xOffset = xOffset;
                m_Settings = settings;
                m_Name = m_Settings.profileSettings.GetUniqueProfileEntryName("New Entry");
                m_BuildPath = Application.dataPath;
                m_LoadPath = Application.dataPath;

                m_ProfileTreeView = profileTreeView;
            }

            public override Vector2 GetWindowSize()
            {
                float width = Mathf.Clamp(m_WindowWidth * 0.375f, Mathf.Min(600, m_WindowWidth - m_xOffset), m_WindowWidth);
                float height = Mathf.Clamp(65, Mathf.Min(65, m_WindowHeight), m_WindowHeight);
                return new Vector2(width, height);
            }

            public override void OnGUI(Rect windowRect)
            {
                GUILayout.Space(5);
                Event evt = Event.current;
                bool hitEnter = evt.type == EventType.KeyDown && (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter);
                EditorGUIUtility.labelWidth = 120;
                m_Name = EditorGUILayout.TextField("Prefix Name", m_Name);
                m_BuildPath = EditorGUILayout.TextField("Build Path Value", m_BuildPath);
                m_LoadPath = EditorGUILayout.TextField("Load Path Value", m_LoadPath);

                UnityEngine.GUI.enabled = m_Name.Length != 0;
                if (GUILayout.Button("Save") || hitEnter)
                {
                    string buildPathName = m_Name + ProfileGroupType.k_PrefixSeparator + "BuildPath";
                    string loadPathName = m_Name + ProfileGroupType.k_PrefixSeparator + "LoadPath";
                    if (string.IsNullOrEmpty(m_Name))
                        Debug.LogError("Variable name cannot be empty.");
                    else if (buildPathName != m_Settings.profileSettings.GetUniqueProfileEntryName(buildPathName))
                        Debug.LogError("Profile variable '" + buildPathName + "' already exists.");
                    else if (loadPathName != m_Settings.profileSettings.GetUniqueProfileEntryName(loadPathName))
                        Debug.LogError("Profile variable '" + loadPathName + "' already exists.");
                    else
                    {
                        Undo.RecordObject(m_Settings, "Profile Path Pair Created");
                        m_Settings.profileSettings.CreateValue(m_Name + ProfileGroupType.k_PrefixSeparator + "BuildPath", m_BuildPath);
                        m_Settings.profileSettings.CreateValue(m_Name + ProfileGroupType.k_PrefixSeparator + "LoadPath", m_LoadPath);
                        AddressableAssetUtility.OpenAssetIfUsingVCIntegration(m_Settings);
                        m_ProfileTreeView.Reload();
                        editorWindow.Close();
                    }
                }
            }
        }
    }
}
