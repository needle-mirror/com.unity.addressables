using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

// ReSharper disable DelegateSubtraction

namespace UnityEditor.AddressableAssets.GUI
{
    [CustomEditor(typeof(AddressableAssetGroup)), CanEditMultipleObjects]
    class AddressableAssetGroupInspector : Editor
    {
        AddressableAssetGroup m_GroupTarget;
        List<Type> m_SchemaTypes;
        bool[] m_FoldoutState;

        // Used for Multi-group editing
        AddressableAssetGroup[] m_GroupTargets;

        // Stores a 2D list of schemas found on the other selected asset groups. 
        // Each schema list contains only schemas of the same type (e.g. BundledAssetGroupSchema).
        List<List<AddressableAssetGroupSchema>> m_GroupSchemas;

        void OnEnable()
        {
            m_GroupTargets = new AddressableAssetGroup[targets.Length];
            for (int i = 0; i < targets.Length; i++)
            {
                m_GroupTargets[i] = targets[i] as AddressableAssetGroup;
            }

            // use item with largest index as base
            m_GroupTarget = m_GroupTargets[m_GroupTargets.Length - 1];

            if (m_GroupTarget != null)
            {
                m_GroupTarget.Settings.OnModification += OnSettingsModification;
                m_SchemaTypes = AddressableAssetUtility.GetTypes<AddressableAssetGroupSchema>();
                m_FoldoutState = new bool[m_GroupTarget.Schemas.Count];
            }

            for (int i = 0; i < m_FoldoutState.Length; i++)
                m_FoldoutState[i] = true;
        }

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
                DrawSchemas(GetSchemasToDraw());
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

        List<AddressableAssetGroupSchema> GetSchemasToDraw()
        {
            List<AddressableAssetGroupSchema> values = new List<AddressableAssetGroupSchema>();

            if (m_GroupTargets == null || m_GroupTargets.Length == 0)
                return values;

            values.AddRange(m_GroupTarget.Schemas);
            
            foreach (var group in m_GroupTargets)
            {
                if (group != m_GroupTarget)
                    values = values.Intersect(group.Schemas, new GroupSchemasCompare()).ToList();
            }

            return values;
        }

        List<AddressableAssetGroupSchema> GetSchemasForOtherTargets(AddressableAssetGroupSchema schema)
        {
            List<AddressableAssetGroupSchema> values = m_GroupTargets
                                                       .Where(t => t.HasSchema(schema.GetType()) && t != m_GroupTarget)
                                                       .Select(t => t.GetSchema(schema.GetType())).ToList();

            return values;
        }

        void DrawSchemas(List<AddressableAssetGroupSchema> schemas)
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
            
            
            if (m_FoldoutState == null || m_FoldoutState.Length != schemas.Count)
                m_FoldoutState = new bool[schemas.Count];

            EditorGUILayout.BeginVertical();
            for (int i = 0; i < schemas.Count; i++)
            {
                var schema = schemas[i];
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
                                var newFoldoutstate = new bool[schemas.Count];
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
                            foreach(var targetSchema in m_GroupTarget.Schemas)
                                targetSchema.ShowAllProperties();
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
                        if(m_GroupTargets.Length == 1)
                            schema.OnGUI();
                        else
                            schema.OnGUIMultiple(GetSchemasForOtherTargets(schema));
                        EditorGUI.indentLevel--;
                    }
                    catch (Exception se)
                    {
                        Debug.LogException(se);
                    }
                }
            }

            if (schemas.Count > 0)
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

        void OnAddSchema(Type schemaType, bool multiSelect = false)
        {
            if (targets.Length > 1)
            {
                foreach (var t in m_GroupTargets)
                    if(!t.HasSchema(schemaType))
                        t.AddSchema(schemaType);
            }
            else
                m_GroupTarget.AddSchema(schemaType);

            var newFoldoutState = new bool[m_GroupTarget.Schemas.Count];
            for (int i = 0; i < m_FoldoutState.Length; i++)
                newFoldoutState[i] = m_FoldoutState[i];
            m_FoldoutState = newFoldoutState;
            m_FoldoutState[m_FoldoutState.Length - 1] = true;
        }

        class GroupSchemasCompare : IEqualityComparer<AddressableAssetGroupSchema>
        {
            public bool Equals(AddressableAssetGroupSchema x, AddressableAssetGroupSchema y)
            {
                if(x.GetType() == y.GetType())
                    return true;

                return false;
            }

            public int GetHashCode(AddressableAssetGroupSchema obj)
            {
                return obj.GetType().GetHashCode();
            }
        }
    }
}
