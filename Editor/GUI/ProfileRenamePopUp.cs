using UnityEngine;

namespace UnityEditor.AddressableAssets.GUI
{
    
    class ProfileRenamePopUp : PopupWindowContent
    {
        private ProfileWindow m_Window;
        private int m_Index;
        private string m_Text;
        private string m_id;

        private float k_Width;
        private float k_Height = 20;

        private GUIStyle m_FieldStyle;
        private string m_OrigText;

        private RenameMode m_Mode;
        enum RenameMode
        {
            Profile,
            Variable,
            Value
        }

        public ProfileRenamePopUp(ProfileWindow window, float width, int colIndex, string id = "")
        {
            m_Window = window;
            m_id = id;
            k_Width = width;

            m_FieldStyle = new GUIStyle(EditorStyles.textField);
            m_FieldStyle.focused.background = EditorStyles.textField.focused.background;

            // Renaming a profile
            if (colIndex == 0)
            {
                m_Index = GetProfileIndexById(id);
                m_Text = m_Window.settings.profileSettings.GetProfileName(id);
                m_FieldStyle.alignment = TextAnchor.UpperLeft;
                m_Mode = RenameMode.Profile;
            }
            // Renaming a variable
            else if (colIndex > 0 && m_id == "")
            {
                m_Index = colIndex - 1;
                m_Text = m_Window.settings.profileSettings.profileEntryNames[m_Index].ProfileName;
                m_FieldStyle.alignment = TextAnchor.MiddleLeft;
                m_Mode = RenameMode.Variable;
            }
            // Renaming a variable value
            else
            {
                m_Index = colIndex - 1;
                m_Text = m_Window.settings.profileSettings.GetProfile(m_id).values[m_Index].value;
                m_FieldStyle.alignment = TextAnchor.UpperLeft;
                m_Mode = RenameMode.Value;
            }

            m_OrigText = m_Text;
        }

        int GetProfileIndexById(string id)
        {
            var profiles = m_Window.settings.profileSettings.profiles;
            for (int i = 0; i < profiles.Count; i++)
            {
                if (profiles[i].id == id)
                    return i;
            }
            return -1;
        }

        public override Vector2 GetWindowSize()
        {
            return new Vector2(k_Width, k_Height);
        }

        public override void OnGUI(Rect rect)
        {
            Event evt = Event.current;
            bool hitEnter = evt.type == EventType.KeyDown && (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter);

            UnityEngine.GUI.SetNextControlName("TextField");
            m_Text = UnityEngine.GUI.TextField(new Rect(rect.x, rect.y, k_Width, k_Height), m_Text, m_FieldStyle);
            UnityEngine.GUI.FocusControl("TextField");

            if (!string.IsNullOrEmpty(m_Text) && !string.IsNullOrWhiteSpace(m_Text))
            {
                if (m_Mode == RenameMode.Profile)
                {
                    if (m_Text != m_Window.settings.profileSettings.GetUniqueProfileName(m_Text))
                        m_Window.settings.profileSettings.profiles[m_Index].profileName = m_OrigText;
                    else if (!string.IsNullOrEmpty(m_Text))
                        m_Window.settings.profileSettings.profiles[m_Index].profileName = m_Text;
                }
                else if (m_Mode == RenameMode.Variable)
                {
                    if (m_Text != m_Window.settings.profileSettings.GetUniqueProfileEntryName(m_Text))
                        m_Window.settings.profileSettings.profileEntryNames[m_Index].SetName(m_OrigText, m_Window.settings.profileSettings);
                    else if (!string.IsNullOrEmpty(m_Text))
                        m_Window.settings.profileSettings.profileEntryNames[m_Index].SetName(m_Text, m_Window.settings.profileSettings);
                }
                else if (m_Mode == RenameMode.Value)
                {
                    var allProfiles = m_Window.settings.profileSettings.profiles;
                    for (int i = 0; i < allProfiles.Count; i++)
                    {
                        if (allProfiles[i].id == m_id)
                        {
                            m_Window.settings.profileSettings.SetValue(m_id, m_Window.settings.profileSettings.profileEntryNames[m_Index].ProfileName, m_Text);
                            break;
                        }
                    }
                }
            }

            // Close the window
            if (hitEnter)
            {
                m_Window.ProfileTreeView.Reload();
                editorWindow.Close();
                return;
            }
        }
    }
}