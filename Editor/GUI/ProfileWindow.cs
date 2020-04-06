using System;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace UnityEditor.AddressableAssets.GUI
{
    class ProfileWindow : EditorWindow
    {
        [MenuItem("Window/Asset Management/Addressables/Profiles", priority = 2051)]
        internal static void ShowWindow()
        {
            var setting = AddressableAssetSettingsDefaultObject.Settings;
            if (setting == null)
            {
                EditorUtility.DisplayDialog("Error", "Attempting to open Addressables Profiles window, but no Addressables Settings file exists.  \n\nOpen 'Window/Asset Management/Addressables/Groups' for more info.", "Ok");
                return;
            }
            var window = GetWindow<ProfileWindow>();
            window.Show();
        }

        
        private const int k_ToolbarHeight = 20;

        public int ToolbarHeight
        {
            get { return k_ToolbarHeight; }
        }

        private GUIStyle m_ButtonStyle;
        
        [SerializeField]
        private TreeViewState m_TreeState;
        [SerializeField] 
        MultiColumnHeaderState m_Mchs;
        ProfileTreeView m_ProfileTreeView;
        public ProfileTreeView ProfileTreeView => m_ProfileTreeView;

        ProfileColumnHeader m_Pch;

        public ProfileColumnHeader Pch => m_Pch;
        
        internal AddressableAssetSettings settings
        {
            get { return AddressableAssetSettingsDefaultObject.Settings; }
        }

        private Rect m_DisplayAreaRect
        {
            get
            {
                return new Rect(0, 0, position.width, position.height);
            }
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
                    menu.AddItem(new GUIContent("Variable"), false, () =>
                    {
                        EditorApplication.delayCall += NewVariable;
                    });
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
                PopupWindow.Show(
                    new Rect(0, k_ToolbarHeight, position.width, k_ToolbarHeight), 
                    new ProfileNewVariablePopup(position.width, position.height, 0, m_ProfileTreeView, settings));
            }
            catch (ExitGUIException)
            {
                // Exception not being caught through OnGUI call
            }
        }

        void NewProfile()
        {
            var uniqueProfileName = settings.profileSettings.GetUniqueProfileName("New Profile");
            if (!string.IsNullOrEmpty(uniqueProfileName))
            {
                settings.profileSettings.AddProfile(uniqueProfileName, m_ProfileTreeView.GetSelectedProfileId());
                m_ProfileTreeView.Reload();
            }
        }

        void OnEnable()
        {
            titleContent = new GUIContent("Addressables Profiles");
        }

        void OnGUI() 
        {
            TopToolbar(m_DisplayAreaRect);
            
            var headerState = ProfileTreeView.CreateDefaultMultiColumnHeaderState(settings);
            if (MultiColumnHeaderState.CanOverwriteSerializedFields(m_Mchs, headerState))
                MultiColumnHeaderState.OverwriteSerializedFields(m_Mchs, headerState);
            m_Mchs = headerState;
            
            if (m_ProfileTreeView == null || m_Mchs.columns.Length != m_ProfileTreeView.multiColumnHeader.state.columns.Length || 
                HeaderNameChanged())
            {
                m_Pch = new ProfileColumnHeader(m_Mchs, this);
                if(m_TreeState == null)
                    m_TreeState = new TreeViewState();
                m_ProfileTreeView = new ProfileTreeView(m_TreeState, m_Mchs, m_Pch, this);
            }
            m_ProfileTreeView.OnGUI(new Rect(0, k_ToolbarHeight, position.width, position.height - k_ToolbarHeight));
        }

        bool HeaderNameChanged()
        {
            for(int i = 0; i < m_Mchs.columns.Length; i++)
            {
                if (!m_Mchs.columns[i].headerContent.text
                    .Equals(m_ProfileTreeView.multiColumnHeader.state.columns[i].headerContent.text))
                    return true;
            }
            return false;
        }

        class ProfileNewVariablePopup : PopupWindowContent
        {
            internal float windowWidth;
            internal float windowHeight;
            internal float xOffset;
            internal string name;
            internal string value;
            internal bool needsFocus = true;
            internal AddressableAssetSettings settings;

            ProfileTreeView m_profileTreeView;

            public ProfileNewVariablePopup(float width, float height, float xOffset, ProfileTreeView profileTreeView, AddressableAssetSettings settings)
            {
                this.windowWidth = width;
                this.windowHeight = height;
                this.xOffset = xOffset;
                this.settings = settings;
                name = this.settings.profileSettings.GetUniqueProfileEntryName("New Entry");
                value = Application.dataPath;

                m_profileTreeView = profileTreeView;
            }

            public override Vector2 GetWindowSize()
            {
                float width = Mathf.Clamp(windowWidth * 0.375f, Mathf.Min(600, windowWidth - xOffset), windowWidth);
                float height = Mathf.Clamp(65, Mathf.Min(65, windowHeight), windowHeight);
                return new Vector2(width, height);
            }

            public override void OnGUI(Rect windowRect)
            {
                GUILayout.Space(5);
                Event evt = Event.current;
                bool hitEnter = evt.type == EventType.KeyDown && (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter);
                EditorGUIUtility.labelWidth = 90;
                name = EditorGUILayout.TextField("Variable Name", name);
                value = EditorGUILayout.TextField("Default Value", value);

                UnityEngine.GUI.enabled = name.Length != 0;
                if (GUILayout.Button("Save") || hitEnter)
                {
                    if (string.IsNullOrEmpty(name))
                        Debug.LogError("Variable name cannot be empty.");
                    else if (name != settings.profileSettings.GetUniqueProfileEntryName(name))
                        Debug.LogError("Profile variable '" + name + "' already exists.");
                    else
                    {
                        settings.profileSettings.CreateValue(name, value);
                        m_profileTreeView.Reload();
                        editorWindow.Close();
                    }
                }
            }
        }
    }
}
