using UnityEditor.AddressableAssets.Settings;
using UnityEditorInternal;
using UnityEngine;

namespace UnityEditor.AddressableAssets.GUI
{
    /// <summary>
    /// Configuration GUI for addressable labels in <see cref="T:UnityEditor.AddressableAssets.Settings" />
    /// </summary>
    public class LabelWindow : EditorWindow
    {
        ReorderableList m_LabelNamesRl;
        AddressableAssetSettings m_Settings;

        public void Intialize(AddressableAssetSettings settings)
        {
            titleContent = new GUIContent("Addressables Labels");
            m_Settings = settings;
            
            var labels = m_Settings.labelTable.labelNames;
            m_LabelNamesRl = new ReorderableList(labels, typeof(string), true, false, true, true);
            m_LabelNamesRl.drawElementCallback = DrawLabelNamesCallback;
            m_LabelNamesRl.onAddDropdownCallback = OnAddLabel;
            m_LabelNamesRl.onRemoveCallback = OnRemoveLabel;
            m_LabelNamesRl.headerHeight = 0; // hide header completely
        }

        void OnGUI()
        {
            GUILayout.Space(7);
            GUILayout.BeginVertical(EditorStyles.label);
            m_LabelNamesRl.DoLayoutList(); 
            GUILayout.EndVertical();
        }
        
        void DrawLabelNamesCallback(Rect rect, int index, bool isActive, bool isFocused)
        {
            var oldName = m_Settings.labelTable.labelNames[index];
            EditorGUI.LabelField(rect, oldName);
        }

        void OnRemoveLabel(ReorderableList list)
        {
            m_Settings.RemoveLabel(m_Settings.labelTable.labelNames[list.index]);
        }

        void OnAddLabel(Rect buttonRect, ReorderableList list)
        {
            buttonRect.x = 6;
            buttonRect.y -= 13;
            PopupWindow.Show(buttonRect, new LabelNamePopup(position.width, m_LabelNamesRl.elementHeight, m_Settings));
        }
        
        class LabelNamePopup : PopupWindowContent
        {
            internal float windowWidth;
            internal float rowHeight;
            internal string name;
            internal bool needsFocus = true;
            internal AddressableAssetSettings settings;

            public LabelNamePopup(float width, float rowHeight, AddressableAssetSettings settings)
            {
                this.windowWidth = width;
                this.rowHeight = rowHeight;
                this.settings = settings;
                name = this.settings.labelTable.GetUniqueLabelName("New Label");
            }

            public override Vector2 GetWindowSize()
            {
                return new Vector2(windowWidth - 13f, rowHeight * 2.25f);
            }

            public override void OnGUI(Rect windowRect)
            {
                GUILayout.Space(5);
                Event evt = Event.current;
                bool hitEnter = evt.type == EventType.KeyDown && (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter);
                UnityEngine.GUI.SetNextControlName("LabelName");
                EditorGUIUtility.labelWidth = 80;
                name = EditorGUILayout.TextField("Label Name", name);
                if (needsFocus)
                {
                    needsFocus = false;
                    EditorGUI.FocusTextInControl("LabelName");
                }

                UnityEngine.GUI.enabled = name.Length != 0;
                if (GUILayout.Button("Save") || hitEnter)
                {
                    if (string.IsNullOrEmpty(name))
                        Debug.LogError("Cannot add empty label to Addressables label list");
                    else if (name != settings.labelTable.GetUniqueLabelName(name))
                        Debug.LogError("Label name '" + name + "' is already in the labels list.");
                    else
                        settings.AddLabel(name);

                    editorWindow.Close();
                }
            }
        }
    }
}