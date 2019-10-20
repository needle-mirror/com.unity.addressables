using System;
using System.Collections.Generic;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

// ReSharper disable DelegateSubtraction

namespace UnityEditor.AddressableAssets.GUI
{
    //[CustomEditor(typeof(AddressableAssetGroup)), CanEditMultipleObjects]
    [CustomEditor(typeof(AddressableAssetGroup))]
    class AddressableAssetGroupInspector : Editor
    {
        AddressableAssetGroup m_GroupTarget;
        List<Type> m_SchemaTypes;
        bool[] m_FoldoutState;

//        // Used for Multi-group editing
//        AddressableAssetGroup[] m_GroupTargets;
//        bool[] m_SchemaState;
//        int m_NumSchemasVisible = -1;
//        // Indicates whether not some schemas are hidden
//        bool m_HiddenSchemas = false;

        // Stores a 2D list of schemas found on the other selected asset groups. 
        // Each schema list contains only schemas of the same type (e.g. BundledAssetGroupSchema).
        List<List<AddressableAssetGroupSchema>> m_GroupSchemas; 

        void OnEnable()
        {
            // Single group editing
            if(targets.Length == 1)
            {
                m_GroupTarget = target as AddressableAssetGroup;
            }
//            // Multi-group editing
//            if (targets.Length > 1)
//            {
//                m_GroupTargets = new AddressableAssetGroup[targets.Length];
//                for(int i = 0; i < targets.Length; i++)
//                {
//                    m_GroupTargets[i] = targets[i] as AddressableAssetGroup;
//                }
//                // use item with largest index as base
//                m_GroupTarget = m_GroupTargets[m_GroupTargets.Length - 1];
//                InitializeMultiSelectGroupSchemas();
//            }
            
            if (m_GroupTarget != null)
            {
                m_GroupTarget.Settings.OnModification += OnSettingsModification;
                m_SchemaTypes = AddressableAssetUtility.GetTypes<AddressableAssetGroupSchema>();
                m_FoldoutState = new bool[m_GroupTarget.Schemas.Count];
            }

            for (int i = 0; i < m_FoldoutState.Length; i++)
                m_FoldoutState[i] = true;
        }

//        void InitializeMultiSelectGroupSchemas()
//        {
//            var schemas = m_GroupTarget.Schemas;
//            if (schemas.Count == 0)
//            {
//                m_HiddenSchemas = false;
//                return;
//            }
//
//            m_SchemaState = new bool[schemas.Count];
//            m_GroupSchemas = new List<List<AddressableAssetGroupSchema>>(schemas.Count);
//
//            // For each m_GroupTarget schema, check if the other selected groups also have the same schema. 
//            bool allGroupsHaveSchema;
//            for(int i = 0; i < schemas.Count; i++)
//            {
//                m_GroupSchemas.Add(new List<AddressableAssetGroupSchema>());
//                Type schema = schemas[i].GetType();
//
//                allGroupsHaveSchema = true;
//                // Skip last group because it's the same group as m_GroupTarget
//                for (int j = 0; j < m_GroupTargets.Length - 1; j++)
//                {
//                    // Group has other schemas, which will not be shown because the m_GroupTarget doesn't have this schema
//                    if (m_GroupTargets[j].Schemas.Count != schemas.Count)
//                        m_HiddenSchemas = true;
//
//                    // Check if other group also has this schema
//                    if (m_GroupTargets[j].HasSchema(schema))
//                        m_GroupSchemas[i].Add(m_GroupTargets[j].GetSchema(schema));
//                    else
//                        allGroupsHaveSchema = false;
//                }
//
//                // All selected groups have this schema
//                if(allGroupsHaveSchema)
//                {
//                    m_NumSchemasVisible++;
//                    m_SchemaState[i] = true;
//                }
//            }
//        }

        void OnDisable()
        {
            if (m_GroupTarget != null) 
                m_GroupTarget.Settings.OnModification -= OnSettingsModification;
        }

        void OnSettingsModification(AddressableAssetSettings settings, AddressableAssetSettings.ModificationEvent evnt, object o)
        {
            switch (evnt)
            {
                case AddressableAssetSettings.ModificationEvent.GroupAdded:
                case AddressableAssetSettings.ModificationEvent.GroupRemoved:
                case AddressableAssetSettings.ModificationEvent.GroupRenamed:
                case AddressableAssetSettings.ModificationEvent.BatchModification:
                case AddressableAssetSettings.ModificationEvent.ActiveProfileSet:
                case AddressableAssetSettings.ModificationEvent.GroupSchemaAdded:
                case AddressableAssetSettings.ModificationEvent.GroupSchemaModified:
                case AddressableAssetSettings.ModificationEvent.GroupSchemaRemoved:
                    Repaint();
                    break;
            }
        }

        void DrawDivider()
        {
            GUILayout.Space(1.5f);
            Rect r = EditorGUILayout.GetControlRect(GUILayout.Height(2.5f));
            r.x = 0;
            r.width = EditorGUIUtility.currentViewWidth;
            r.height = 1;

            Color color = new Color(0.6f, 0.6f, 0.6f, 1.333f);
            if (EditorGUIUtility.isProSkin)
            {
                color.r = 0.12f;
                color.g = 0.12f;
                color.b = 0.12f;
            }
            EditorGUI.DrawRect(r, color);
        }
                
        public override void OnInspectorGUI()
        {
            try
            {
                serializedObject.Update();

                if (targets.Length == 1)
                {
                    DrawSingleGroup();
                }
//                else if(targets.Length > 1)
//                {
//                    DrawMultipleGroups();
//                }

                serializedObject.ApplyModifiedProperties();
            }
            catch (UnityEngine.ExitGUIException )
            {
                throw;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        void DrawSingleGroup()
        {
            GUILayout.Space(6);

            EditorGUILayout.BeginHorizontal();
            var activeProfileName = m_GroupTarget.Settings.profileSettings.GetProfileName(m_GroupTarget.Settings.activeProfileId);
            if (string.IsNullOrEmpty(activeProfileName))
            {
                m_GroupTarget.Settings.activeProfileId = null; //this will reset it to default.
                activeProfileName = m_GroupTarget.Settings.profileSettings.GetProfileName(m_GroupTarget.Settings.activeProfileId);
            }
            EditorGUILayout.PrefixLabel("Active Profile: " + activeProfileName);
            if (GUILayout.Button("Inspect Top Level Settings"))
            {
                EditorGUIUtility.PingObject(AddressableAssetSettingsDefaultObject.Settings);
                Selection.activeObject = AddressableAssetSettingsDefaultObject.Settings;
            }

            EditorGUILayout.EndHorizontal();
            GUILayout.Space(6);
            
            
            if (m_FoldoutState == null || m_FoldoutState.Length != m_GroupTarget.Schemas.Count)
                m_FoldoutState = new bool[m_GroupTarget.Schemas.Count];

            EditorGUILayout.BeginVertical();
            for (int i = 0; i < m_GroupTarget.Schemas.Count; i++)
            {
                var schema = m_GroupTarget.Schemas[i];
                int currentIndex = i;

                DrawDivider();
                EditorGUILayout.BeginHorizontal();
                m_FoldoutState[i] = EditorGUILayout.Foldout(m_FoldoutState[i], AddressableAssetUtility.GetCachedTypeDisplayName(schema.GetType()));
                if (!m_GroupTarget.ReadOnly)
                {
                    GUILayout.FlexibleSpace();
                    GUIStyle gearIconStyle = UnityEngine.GUI.skin.FindStyle("IconButton") ?? EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector).FindStyle("IconButton");

                    if (EditorGUILayout.DropdownButton(EditorGUIUtility.IconContent("_Popup"), FocusType.Keyboard, gearIconStyle))
                    {
                        var menu = new GenericMenu();
                        menu.AddItem(AddressableAssetGroup.RemoveSchemaContent, false, () =>
                        {
                            if (EditorUtility.DisplayDialog("Remove selected schema?", "Are you sure you want to remove " + AddressableAssetUtility.GetCachedTypeDisplayName(schema.GetType()) + " schema?\n\nYou cannot undo this action.", "Yes", "No"))
                            {
                                m_GroupTarget.RemoveSchema(schema.GetType());
                                var newFoldoutstate = new bool[m_GroupTarget.Schemas.Count];
                                for (int j = 0; j < newFoldoutstate.Length; j++)
                                {
                                    if (j < i)
                                        newFoldoutstate[j] = m_FoldoutState[j];
                                    else
                                        newFoldoutstate[j] = m_FoldoutState[i + 1];
                                }

                                m_FoldoutState = newFoldoutstate;
                            }
                        });
                        menu.AddItem(AddressableAssetGroup.MoveSchemaUpContent, false, () =>
                        {
                            if (currentIndex > 0)
                            {
                                m_GroupTarget.Schemas[currentIndex] = m_GroupTarget.Schemas[currentIndex - 1];
                                m_GroupTarget.Schemas[currentIndex - 1] = schema;
                                return;
                            }
                        });
                        menu.AddItem(AddressableAssetGroup.MoveSchemaDownContent, false, () =>
                        {
                            if (currentIndex < m_GroupTarget.Schemas.Count - 1)
                            {
                                m_GroupTarget.Schemas[currentIndex] = m_GroupTarget.Schemas[currentIndex + 1];
                                m_GroupTarget.Schemas[currentIndex + 1] = schema;
                                return;
                            }
                        });
                        menu.AddSeparator("");
                        menu.AddItem(AddressableAssetGroup.ExpandSchemaContent, false, () =>
                        {
                            m_FoldoutState[currentIndex] = true;
                            schema.ShowAllProperties();
                        });
                        menu.ShowAsContext();
                    }
                }

                EditorGUILayout.EndHorizontal();
                if (m_FoldoutState[i])
                {
                    try
                    {
                        EditorGUI.indentLevel++;
                        schema.OnGUI();
                        EditorGUI.indentLevel--;
                    }
                    catch (Exception se)
                    {
                        Debug.LogException(se);
                    }
                }
            }

            if (m_GroupTarget.Schemas.Count > 0)
                DrawDivider();
            GUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();

            GUILayout.FlexibleSpace();
            GUIStyle addSchemaButton = new GUIStyle(UnityEngine.GUI.skin.button);
            addSchemaButton.fontSize = 12;
            addSchemaButton.fixedWidth = 225;
            addSchemaButton.fixedHeight = 22;

            if (!m_GroupTarget.ReadOnly)
            {
                if (EditorGUILayout.DropdownButton(new GUIContent("Add Schema", "Add new schema to this group."), FocusType.Keyboard, addSchemaButton))
                {
                    var menu = new GenericMenu();
                    for (int i = 0; i < m_SchemaTypes.Count; i++)
                    {
                        var type = m_SchemaTypes[i];

                        if (m_GroupTarget.GetSchema(type) == null)
                        {
                            menu.AddItem(new GUIContent(AddressableAssetUtility.GetCachedTypeDisplayName(type), ""), false, () => OnAddSchema(type));
                        }
                        else
                        {
                            menu.AddDisabledItem(new GUIContent(AddressableAssetUtility.GetCachedTypeDisplayName(type), ""), true);
                        }
                    }

                    menu.ShowAsContext();
                }
            }

            GUILayout.FlexibleSpace();

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

//        void DrawMultipleGroups()
//        {
//            InitializeMultiSelectGroupSchemas();
//
//            if (m_FoldoutState == null || m_FoldoutState.Length != m_GroupTarget.Schemas.Count)
//                m_FoldoutState = new bool[m_GroupTarget.Schemas.Count];
//
//            EditorGUILayout.BeginVertical();
//            int lastSchemaDrawn = -1;
//            for (int i = 0; i < m_GroupTarget.Schemas.Count; i++)
//            {
//                if (!m_SchemaState[i]) continue;
//
//                var schema = m_GroupTarget.Schemas[i];
//                int currentIndex = i;
//
//                // Draw divider in between schemas
//                if (lastSchemaDrawn > -1)
//                    DrawDivider();
//                lastSchemaDrawn = i;
//
//                EditorGUILayout.BeginHorizontal();
//                m_FoldoutState[i] = EditorGUILayout.Foldout(m_FoldoutState[i], schema.DisplayName());
//                if (!m_GroupTarget.ReadOnly)
//                {
//                    GUILayout.FlexibleSpace();
//                    GUIStyle gearIconStyle = UnityEngine.GUI.skin.FindStyle("IconButton") ?? EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector).FindStyle("IconButton");
//
//                    if (EditorGUILayout.DropdownButton(EditorGUIUtility.IconContent("_Popup"), FocusType.Keyboard, gearIconStyle))
//                    {
//                        var menu = new GenericMenu();
//                        menu.AddItem(AddressableAssetGroup.RemoveSchemaContent, false, () =>
//                        {
//                            if (EditorUtility.DisplayDialog("Remove selected schema?", "Are you sure you want to remove " + schema.DisplayName() + " schema?\n\nYou cannot undo this action.", "Yes", "No"))
//                            {
//                                Type schemaType = schema.GetType();
//                                m_GroupTarget.RemoveSchema(schemaType);
//                                for (int j = 0; j < m_GroupTargets.Length-1; j++)
//                                {
//                                    m_GroupTargets[j].RemoveSchema(schemaType);
//                                }
//
//                                InitializeMultiSelectGroupSchemas();
//
//                                var newFoldoutstate = new bool[m_GroupTarget.Schemas.Count];
//                                for (int j = 0; j < newFoldoutstate.Length; j++)
//                                {
//                                    if (j < i)
//                                        newFoldoutstate[j] = m_FoldoutState[j];
//                                    else
//                                        newFoldoutstate[j] = m_FoldoutState[i + 1];
//                                }
//
//                                m_FoldoutState = newFoldoutstate;
//                                return;
//                            }
//                        });
//                        menu.AddItem(AddressableAssetGroup.MoveSchemaUpContent, false, () =>
//                        {
//                            foreach (var group in m_GroupTargets)
//                            {
//                                int index = group.FindSchema(schema.GetType());
//                                if (index > 0)
//                                {
//                                    var temp = group.Schemas[index];
//                                    group.Schemas[index] = group.Schemas[index - 1];
//                                    group.Schemas[index - 1] = temp;
//                                }
//                            }
//                            InitializeMultiSelectGroupSchemas();
//                            return;
//                        });
//                        menu.AddItem(AddressableAssetGroup.MoveSchemaDownContent, false, () =>
//                        {
//                            foreach (var group in m_GroupTargets)
//                            {
//                                int index = group.FindSchema(schema.GetType());
//                                if (index >= 0 && index < group.Schemas.Count - 1)
//                                {
//                                    var temp = group.Schemas[index];
//                                    group.Schemas[index] = group.Schemas[index + 1];
//                                    group.Schemas[index + 1] = temp;
//                                }
//                            }
//                            InitializeMultiSelectGroupSchemas();
//                            return;
//                        });
//                        menu.AddSeparator("");
//                        menu.AddItem(AddressableAssetGroup.ExpandSchemaContent, false, () =>
//                        {
//                            m_FoldoutState[currentIndex] = true;
//                            foreach (var group in m_GroupTargets)
//                            {
//                                int index = group.FindSchema(schema.GetType());
//                                if (index != -1)
//                                {
//                                   group.Schemas[index].ShowAllProperties();
//                                }
//                            }
//                        });
//                        menu.ShowAsContext();
//                    }
//                }
//                EditorGUILayout.EndHorizontal();
//                if (m_FoldoutState[i])
//                {
//                    try
//                    {
//                        EditorGUI.indentLevel++;
//                        schema.OnGUIMultiple(m_GroupSchemas[i]);
//                        EditorGUI.indentLevel--;
//                    }
//                    catch (Exception se)
//                    {
//                        Debug.LogException(se);
//                    }
//                }
//            }
//
//            if (m_HiddenSchemas)
//            {
//                if (lastSchemaDrawn > -1)
//                    DrawDivider();
//                EditorGUILayout.HelpBox(new GUIContent("Only schemas that are on all selected groups can be multi-edited."));
//            }
//                       
//            // Draw divider before "Add component" button if schemas were drawn
//            if (m_HiddenSchemas || lastSchemaDrawn > -1)
//                DrawDivider();
//
//            GUILayout.Space(4);
//            EditorGUILayout.BeginHorizontal();
//
//            GUILayout.FlexibleSpace();
//            GUIStyle addSchemaButton = new GUIStyle(UnityEngine.GUI.skin.button);
//            addSchemaButton.fontSize = 12;
//            addSchemaButton.fixedWidth = 225;
//            addSchemaButton.fixedHeight = 22;
//
//            if (!m_GroupTarget.ReadOnly)
//            {
//                if (EditorGUILayout.DropdownButton(new GUIContent("Add Schema", "Add new schema to this group."), FocusType.Keyboard, addSchemaButton))
//                {
//                    var menu = new GenericMenu();
//                    for (int i = 0; i < m_SchemaTypes.Count; i++)
//                    {
//                        var type = m_SchemaTypes[i];
//                        var schema = (AddressableAssetGroupSchema)CreateInstance(type);
//
//                        bool allGroupsDoNotHave = true;
//                        foreach(var group in m_GroupTargets)
//                        {
//                            if (group.HasSchema(type))
//                                allGroupsDoNotHave = false;
//                        }
//
//                        // Only show schemas that none of the selected groups have
//                        if (allGroupsDoNotHave)
//                        {
//                            menu.AddItem(new GUIContent(schema.DisplayName(), ""), false, () => 
//                            {
//                                OnAddSchema(type, true);
//                                return;
//                            });
//                        }
//                        else
//                        {
//                            menu.AddDisabledItem(new GUIContent(schema.DisplayName(), ""), true);
//                        }
//                    }
//
//                    menu.ShowAsContext();
//                }
//            }
//
//            GUILayout.FlexibleSpace();
//
//            EditorGUILayout.EndHorizontal();
//            EditorGUILayout.EndVertical();
//        }

        void OnAddSchema(Type schemaType, bool multiSelect = false)
        {
            m_GroupTarget.AddSchema(schemaType);
//            if (multiSelect)
//            {
//                for (int i = 0; i < m_GroupTargets.Length - 1; i++)
//                {
//                    m_GroupTargets[i].AddSchema(schemaType);
//                }
//                InitializeMultiSelectGroupSchemas();
//            }

            var newFoldoutState = new bool[m_GroupTarget.Schemas.Count];
            for (int i = 0; i < m_FoldoutState.Length; i++)
                newFoldoutState[i] = m_FoldoutState[i];
            m_FoldoutState = newFoldoutState;
            m_FoldoutState[m_FoldoutState.Length - 1] = true;
        }
        
    }

}
