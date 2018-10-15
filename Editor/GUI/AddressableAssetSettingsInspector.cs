using UnityEngine;
using System;
using UnityEditor.IMGUI.Controls;
using System.Collections.Generic;
using UnityEngine.AddressableAssets;
using System.Linq;
using UnityEditorInternal;
using UnityEngine.ResourceManagement;

namespace UnityEditor.AddressableAssets
{
    [CustomEditor(typeof(AddressableAssetSettings))]
    internal class AddressableAssetSettingsInspector : Editor
    {
        AddressableAssetSettings m_aasTarget;

        [SerializeField]
        bool m_generalFoldout = true;
        [SerializeField]
        bool m_groupFoldout = true;
        [SerializeField]
        bool m_profilesFoldout = true;
        [SerializeField]
        bool m_labelsFoldout = true;
        [SerializeField]
        bool m_dataBuildersFoldout = true;
        [SerializeField]
        bool m_schemaTemplatesFoldout = true;
        [SerializeField]
        bool m_initObjectsFoldout = true;

        [SerializeField]
        ReorderableList m_profileEntriesRL;
        [SerializeField]
        ReorderableList m_labelNamesRL;
        [SerializeField]
        ReorderableList m_dataBuildersRL;
        [SerializeField]
        ReorderableList m_schemaTemplatesRL;
        [SerializeField]
        ReorderableList m_initObjectsRL;

        [SerializeField]
        int m_currentProfileIndex = -1;


        private void OnEnable()
        {
            m_aasTarget = target as AddressableAssetSettings;

            var names = m_aasTarget.profileSettings.profileEntryNames;
            m_profileEntriesRL = new ReorderableList(names, typeof(AddressableAssetProfileSettings.ProfileIDData), true, true, true, true);
            m_profileEntriesRL.drawElementCallback = DrawProfileEntriesCallback;
            m_profileEntriesRL.drawHeaderCallback = DrawProfileEntriesHeader;
            m_profileEntriesRL.onAddCallback = OnAddProfileEntry;
            m_profileEntriesRL.onRemoveCallback = OnRemoveProfileEntry;

            var labels = m_aasTarget.labelTable.labelNames;
            m_labelNamesRL = new ReorderableList(labels, typeof(string), true, true, true, true);
            m_labelNamesRL.drawElementCallback = DrawLabelNamesCallback;
            m_labelNamesRL.drawHeaderCallback = DrawLabelNamesHeader;
            m_labelNamesRL.onAddDropdownCallback = OnAddLabel;
            m_labelNamesRL.onRemoveCallback = OnRemoveLabel;

            m_dataBuildersRL = new ReorderableList(m_aasTarget.DataBuilders, typeof(ScriptableObject), true, true, true, true);
            m_dataBuildersRL.drawElementCallback = DrawDataBuilderCallback;
            m_dataBuildersRL.drawHeaderCallback = DrawDataBuilderHeader;
            m_dataBuildersRL.onAddDropdownCallback = OnAddDataBuilder;
            m_dataBuildersRL.onRemoveCallback = OnRemoveDataBuilder;

            m_schemaTemplatesRL = new ReorderableList(m_aasTarget.SchemaTemplates, typeof(ScriptableObject), true, true, true, true);
            m_schemaTemplatesRL.drawElementCallback = DrawSchemaTemplateCallback;
            m_schemaTemplatesRL.drawHeaderCallback = DrawSchemaTemplateHeader;
            m_schemaTemplatesRL.onAddDropdownCallback = OnAddSchemaTemplate;
            m_schemaTemplatesRL.onRemoveCallback = OnRemoveSchemaTemplate;

            m_initObjectsRL = new ReorderableList(m_aasTarget.InitializationObjects, typeof(ScriptableObject), true, true, true, true);
            m_initObjectsRL.drawElementCallback = DrawInitializationObjectCallback;
            m_initObjectsRL.drawHeaderCallback = DrawInitializationObjectHeader;
            m_initObjectsRL.onAddDropdownCallback = OnAddInitializationObject;
            m_initObjectsRL.onRemoveCallback = OnRemoveInitializationObject;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            m_generalFoldout = EditorGUILayout.Foldout(m_generalFoldout, "General");
            if (m_generalFoldout)
            {
                ProjectConfigData.postProfilerEvents = EditorGUILayout.Toggle("Send Profiler Events", ProjectConfigData.postProfilerEvents);
                m_aasTarget.buildSettings.LogResourceManagerExceptions = EditorGUILayout.Toggle("Log Resource Manager Exceptions", m_aasTarget.buildSettings.LogResourceManagerExceptions);
            }
            GUILayout.Space(6);
            m_groupFoldout = EditorGUILayout.Foldout(m_groupFoldout, "Groups");
            if (m_groupFoldout)
            {
                EditorGUILayout.HelpBox("Group data is modified on the group asset, and the groups list is altered from the Addressables window.  The list below is presented for ease of finding group assets, not for direct editing.", MessageType.None);
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUI.indentLevel++;
                    for (int i = 0; i < m_aasTarget.groups.Count; i++)
                    {
                        var newObject = EditorGUILayout.ObjectField(m_aasTarget.groups[i], typeof(AddressableAssetGroup), false);
                        if (newObject != m_aasTarget.groups[i] && newObject is AddressableAssetGroup)
                        {
                            m_aasTarget.groups[i] = newObject as AddressableAssetGroup;
                        }
                    }
                    EditorGUI.indentLevel--;
                }
            }

            GUILayout.Space(6);
            m_profilesFoldout = EditorGUILayout.Foldout(m_profilesFoldout, "Profiles");
            if (m_profilesFoldout)
            {
                if (m_aasTarget.profileSettings.profiles.Count > 0)
                {
                    if (m_currentProfileIndex < 0 || m_currentProfileIndex >= m_aasTarget.profileSettings.profiles.Count)
                        m_currentProfileIndex = 0;
                    var profileNames = m_aasTarget.profileSettings.GetAllProfileNames();
                    m_currentProfileIndex = EditorGUILayout.Popup("Profile To Edit", m_currentProfileIndex, profileNames.ToArray());

                    EditorGUI.indentLevel++;
                    bool doAdd = false;
                    bool doRemove = false;
                    bool canEdit = profileNames[m_currentProfileIndex] == AddressableAssetProfileSettings.k_rootProfileName;
                    using (new EditorGUI.DisabledScope(canEdit))
                    {
                        var newName = EditorGUILayout.DelayedTextField("Name", profileNames[m_currentProfileIndex]);
                        if (newName != profileNames[m_currentProfileIndex])
                        {
                            var profile = m_aasTarget.profileSettings.profiles[m_currentProfileIndex];
                            profile.profileName = newName;
                            m_aasTarget.SetDirty(AddressableAssetSettings.ModificationEvent.ProfileModified, profile.id, true);
                        }
                    }
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    doAdd = GUILayout.Button("+");
                    using (new EditorGUI.DisabledScope(canEdit))
                    {

                        doRemove = GUILayout.Button("-");

                    }
                    EditorGUILayout.EndHorizontal();

                    EditorGUI.indentLevel--;
                    var rect = GUILayoutUtility.GetRect(0, m_profileEntriesRL.GetHeight());
                    rect.width -= 20;
                    rect.x += 20;
                    m_profileEntriesRL.DoList(rect);
                    
                    if (doAdd)
                    {
                        var name = m_aasTarget.profileSettings.GetUniqueProfileName("New Profile");
                        if (!string.IsNullOrEmpty(name))
                            m_aasTarget.profileSettings.AddProfile(name, string.Empty);
                        m_currentProfileIndex = m_aasTarget.profileSettings.profiles.Count - 1;
                    }
                    else if (doRemove)
                    {
                        var prof = m_aasTarget.profileSettings.profiles[m_currentProfileIndex];
                        if (prof != null)
                        {
                            m_aasTarget.profileSettings.RemoveProfile(prof.id);
                        }
                    }
                }
                else
                {
                    EditorGUILayout.LabelField("No valid profiles found");
                }

            }
            GUILayout.Space(6);
            if (m_labelsFoldout = EditorGUILayout.Foldout(m_labelsFoldout, "Labels"))
                m_labelNamesRL.DoLayoutList();

            GUILayout.Space(6);
            if (m_dataBuildersFoldout = EditorGUILayout.Foldout(m_dataBuildersFoldout, "Data Builders"))
                m_dataBuildersRL.DoLayoutList();

            GUILayout.Space(6);
            if (m_schemaTemplatesFoldout = EditorGUILayout.Foldout(m_schemaTemplatesFoldout, "Group Schema Templates"))
                m_schemaTemplatesRL.DoLayoutList();

            GUILayout.Space(6);
            if (m_initObjectsFoldout = EditorGUILayout.Foldout(m_initObjectsFoldout, "Initialization Objects"))
                m_initObjectsRL.DoLayoutList();


            serializedObject.ApplyModifiedProperties();
        }

        private void DrawGroupsHeader(Rect rect)
        {
            EditorGUI.LabelField(rect, "Asset Groups");
        }
        void DrawGroupsCallback(Rect rect, int index, bool isActive, bool isFocused)
        {
            var grp = m_aasTarget.groups[index];

            EditorGUI.LabelField(rect, grp.Name);
        }

        private void DrawProfileEntriesHeader(Rect rect)
        {
            var currProfile = m_aasTarget.profileSettings.profiles[m_currentProfileIndex];
            EditorGUI.LabelField(rect, "Profile Entries");
        }

        private void DrawProfileEntriesCallback(Rect rect, int index, bool isActive, bool isFocused)
        {
            float halfW = rect.width * 0.4f;
            var currentEntry = m_aasTarget.profileSettings.profileEntryNames[index];
            var newName = EditorGUI.DelayedTextField(new Rect(rect.x, rect.y, halfW, rect.height), currentEntry.Name);
            if (newName != currentEntry.Name)
                currentEntry.SetName(newName, m_aasTarget.profileSettings);

            var currProfile = m_aasTarget.profileSettings.profiles[m_currentProfileIndex];
            var oldValue = m_aasTarget.profileSettings.GetValueById(currProfile.id, currentEntry.Id);
            var newValue = EditorGUI.TextField(new Rect(rect.x + halfW, rect.y, rect.width - halfW, rect.height), oldValue);
            if (oldValue != newValue)
            {
                m_aasTarget.profileSettings.SetValue(currProfile.id, currentEntry.Name, newValue);
            }
        }

        private void OnAddProfileEntry(ReorderableList list)
        {
            var name = m_aasTarget.profileSettings.GetUniqueProfileEntryName("New Entry");
            if (!string.IsNullOrEmpty(name))
                m_aasTarget.profileSettings.CreateValue(name, "");
        }
        private void OnRemoveProfileEntry(ReorderableList list)
        {
            if (list.index >= 0 && list.index < m_aasTarget.profileSettings.profileEntryNames.Count)
            {
                var entry = m_aasTarget.profileSettings.profileEntryNames[list.index];
                if (entry != null)
                    m_aasTarget.profileSettings.RemoveValue(entry.Id);
            }
        }
        
        private void DrawLabelNamesHeader(Rect rect)
        {
            EditorGUI.LabelField(rect, "Labels");
        }

        private void DrawLabelNamesCallback(Rect rect, int index, bool isActive, bool isFocused)
        {
            var oldName = m_aasTarget.labelTable.labelNames[index];
            EditorGUI.LabelField(rect, oldName);

        }
        private void OnRemoveLabel(ReorderableList list)
        {
            m_aasTarget.RemoveLabel(m_aasTarget.labelTable.labelNames[list.index], true);
        }

        private void OnAddLabel(Rect buttonRect, ReorderableList list)
        {
            buttonRect.x -= 400;
            buttonRect.y -= 13;

            PopupWindow.Show(buttonRect, new LabelNamePopup(m_labelNamesRL.elementHeight, m_aasTarget));
        }

        class LabelNamePopup : PopupWindowContent
        {
            internal float m_rowHeight;
            internal string m_name;
            internal bool m_needsFocus = true;
            internal AddressableAssetSettings m_settings;

            public LabelNamePopup(float rowHeight, AddressableAssetSettings settings)
            {
                m_rowHeight = rowHeight;
                m_settings = settings;
                m_name = m_settings.labelTable.GetUniqueLabelName("New Label");
            }

            public override Vector2 GetWindowSize()
            {
                return new Vector2(400, m_rowHeight * 3);
            }

            public override void OnGUI(Rect windowRect)
            {
                GUILayout.Space(5);
                Event evt = Event.current;
                bool hitEnter = evt.type == EventType.KeyDown && (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter);
                GUI.SetNextControlName("LabelName");
                m_name = EditorGUILayout.TextField("New Tag Name", m_name);
                if (m_needsFocus)
                {
                    m_needsFocus = false;
                    EditorGUI.FocusTextInControl("LabelName");
                }

                GUI.enabled = m_name.Length != 0;
                if (GUILayout.Button("Save") || hitEnter)
                {
                    if (string.IsNullOrEmpty(m_name))
                        Debug.LogError("Cannot add empty label to Addressables label list");
                    else if (m_name != m_settings.labelTable.GetUniqueLabelName(m_name))
                        Debug.LogError("Label name '" + m_name + "' is already in the labels list.");
                    else
                        m_settings.AddLabel(m_name, true);

                    editorWindow.Close();
                }
            }
        }

        private void DrawDataBuilderHeader(Rect rect)
        {
            EditorGUI.LabelField(rect, "Data Builders");
        }

        private void DrawDataBuilderCallback(Rect rect, int index, bool isActive, bool isFocused)
        {
            var so = m_aasTarget.DataBuilders[index];
            var builder = so as IDataBuilder;
            var label = builder == null ? "" : builder.Name;
            var nb = EditorGUI.ObjectField(rect, label, so, typeof(ScriptableObject), false) as ScriptableObject;
            if (nb != so)
                m_aasTarget.SetDataBuilderAtIndex(index, nb as IDataBuilder);
        }

        private void OnRemoveDataBuilder(ReorderableList list)
        {
            m_aasTarget.RemoveDataBuilder(list.index);
        }

        private void OnAddDataBuilder(Rect buttonRect, ReorderableList list)
        {
            var assetPath = EditorUtility.OpenFilePanelWithFilters("Data Builder", "Assets", new string[] {"Data Builder", "asset" });
            if (string.IsNullOrEmpty(assetPath))
                return;
            var builder = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath.Substring(assetPath.IndexOf("Assets/")));
            if (!typeof(IDataBuilder).IsAssignableFrom(builder.GetType()))
            {
                Debug.LogWarningFormat("Asset at {0} does not implement the IDataBuilder interface.", assetPath);
                return;
            }
            m_aasTarget.AddDataBuilder(builder as IDataBuilder);
        }

        private void DrawSchemaTemplateHeader(Rect rect)
        {
            EditorGUI.LabelField(rect, "Group Schema Templates");
        }

        private void DrawSchemaTemplateCallback(Rect rect, int index, bool isActive, bool isFocused)
        {
            var template = m_aasTarget.SchemaTemplates[index];
            GUI.Label(rect, template.DisplayName);
        }

        private void OnRemoveSchemaTemplate(ReorderableList list)
        {
            m_aasTarget.RemoveSchemaTemplate(list.index);
        }

        private void OnAddSchemaTemplate(Rect buttonRect, ReorderableList list)
        {
            buttonRect.x -= 400;
            buttonRect.y -= 13;

            PopupWindow.Show(buttonRect, new NewScehemaTemplatePopup(m_labelNamesRL.elementHeight, m_aasTarget));
        }

        class NewScehemaTemplatePopup : PopupWindowContent
        {
            internal float m_rowHeight;
            internal string m_name = "";
            internal string m_description = "";
            internal bool m_needsFocus = true;
            internal AddressableAssetSettings m_settings;
            List<Type> schemaTypes;
            List<Type> selectedTypes = new List<Type>();
            public NewScehemaTemplatePopup(float rowHeight, AddressableAssetSettings settings)
            {
                m_rowHeight = rowHeight;
                m_settings = settings;
                schemaTypes = AddressableAssetUtility.GetTypes<AddressableAssetGroupSchema>();
            }

            public override Vector2 GetWindowSize()
            {
                return new Vector2(400, m_rowHeight * (4 + selectedTypes.Count));
            }

            public override void OnGUI(Rect windowRect)
            {
                GUILayout.Space(5);
                Event evt = Event.current;
                bool hitEnter = evt.type == EventType.KeyDown && (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter);
                GUI.SetNextControlName("LabelName");
                m_name = EditorGUILayout.TextField("Schema Template Name", m_name);
                m_description = EditorGUILayout.TextField("Description", m_description);
                if (m_needsFocus)
                {
                    m_needsFocus = false;
                    EditorGUI.FocusTextInControl("LabelName");
                }
                for (int i = 0; i < selectedTypes.Count; i++)
                {
                    var schema = selectedTypes[i];
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label(schema.Name);
                    if (GUILayout.Button("X", GUILayout.Width(40)))
                    {
                        selectedTypes.Remove(schema);
                        EditorGUILayout.EndHorizontal();
                        break;
                    }
                    EditorGUILayout.EndHorizontal();
                }
                if (EditorGUILayout.DropdownButton(new GUIContent("Add Schema", "Add new schema to this group."), FocusType.Keyboard))
                {
                    var menu = new GenericMenu();
                    for (int i = 0; i < schemaTypes.Count; i++)
                    {
                        var type = schemaTypes[i];
                        menu.AddItem(new GUIContent(type.Name, ""), false, OnAddSchema, type);
                    }
                    menu.ShowAsContext();
                }

                GUI.enabled = m_name.Length != 0;
                if (GUILayout.Button("Save") || hitEnter)
                {
                    if (string.IsNullOrEmpty(m_name))
                        Debug.LogError("Schema template must have a valid name.");
                    else
                        m_settings.AddSchemaTemplate(m_name, m_description, selectedTypes.ToArray());

                    editorWindow.Close();
                }
            }
            void OnAddSchema(object context)
            {
                selectedTypes.Add(context as Type);
            }
        }

        private void DrawInitializationObjectHeader(Rect rect)
        {
            EditorGUI.LabelField(rect, "Initialization Objects");
        }

        private void DrawInitializationObjectCallback(Rect rect, int index, bool isActive, bool isFocused)
        {
            var so = m_aasTarget.InitializationObjects[index];
            var initObj = so as IObjectInitializationDataProvider;
            var label = initObj == null ? "" : initObj.Name;
            var nb = EditorGUI.ObjectField(rect, label, so, typeof(ScriptableObject), false) as ScriptableObject;
            if (nb != so)
                m_aasTarget.SetInitializationObjectAtIndex(index, nb as IObjectInitializationDataProvider);
        }

        private void OnRemoveInitializationObject(ReorderableList list)
        {
            m_aasTarget.RemoveInitializationObject(list.index);
        }

        private void OnAddInitializationObject(Rect buttonRect, ReorderableList list)
        {
            var assetPath = EditorUtility.OpenFilePanelWithFilters("Initialization Object", "Assets", new string[] { "Initialization Object", "asset" });
            if (string.IsNullOrEmpty(assetPath))
                return;
            var initObj = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath.Substring(assetPath.IndexOf("Assets/")));
            if (!typeof(IObjectInitializationDataProvider).IsAssignableFrom(initObj.GetType()))
            {
                Debug.LogWarningFormat("Asset at {0} does not implement the IObjectInitializationDataProvider interface.", assetPath);
                return;
            }
            m_aasTarget.AddInitializationObject(initObj as IObjectInitializationDataProvider);
        }

    }

}
