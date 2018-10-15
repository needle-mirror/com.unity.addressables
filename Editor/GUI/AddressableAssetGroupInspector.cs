using UnityEngine;
using System;
using UnityEditor.IMGUI.Controls;
using System.Collections.Generic;
using UnityEngine.AddressableAssets;
using System.Linq;
using UnityEditorInternal;

namespace UnityEditor.AddressableAssets
{
    [CustomEditor(typeof(AddressableAssetGroup))]
    internal class AddressableAssetGroupInspector : Editor
    {
        AddressableAssetGroup groupTarget;
        List<Type> schemaTypes;
        bool[] foldoutState;

        private void OnEnable()
        {
            groupTarget = target as AddressableAssetGroup;
            groupTarget.Settings.OnModification += OnSettingsModification;
            schemaTypes = AddressableAssetUtility.GetTypes<AddressableAssetGroupSchema>();
            foldoutState = new bool[groupTarget.Schemas.Count];
            for (int i = 0; i < foldoutState.Length; i++)
                foldoutState[i] = true;
        }

        private void OnDisable()
        {
            groupTarget.Settings.OnModification -= OnSettingsModification;
        }

        private void OnSettingsModification(AddressableAssetSettings settings, AddressableAssetSettings.ModificationEvent evnt, object o)
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
                default:
                    break;
            }
        }


        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("Addressable Asset Settings", groupTarget.Settings, typeof(AddressableAssetSettings), false);
                var prof = groupTarget.Settings.profileSettings.GetProfile(groupTarget.Settings.activeProfileId);
                EditorGUILayout.TextField("Current Profile", prof.profileName);
            }

            if (foldoutState == null || foldoutState.Length != groupTarget.Schemas.Count)
                foldoutState = new bool[groupTarget.Schemas.Count];

            for (int i = 0; i < groupTarget.Schemas.Count; i++)
            {
                var schema = groupTarget.Schemas[i];
                EditorGUILayout.BeginHorizontal();
                foldoutState[i] = EditorGUILayout.Foldout(foldoutState[i], schema.GetType().Name);
                if (!groupTarget.ReadOnly)
                {
                    if (GUILayout.Button("X", GUILayout.Width(40)))
                    {
                        if (EditorUtility.DisplayDialog("Delete selected schema?", "Are you sure you want to delete the selected schema?\n\nYou cannot undo this action.", "Yes", "No"))
                        {
                            groupTarget.RemoveSchema(schema.GetType());
                            var newFoldoutstate = new bool[groupTarget.Schemas.Count];
                            for (int j = 0; j < newFoldoutstate.Length; j++)
                            {
                                if (j < i)
                                    newFoldoutstate[j] = foldoutState[i];
                                else
                                    newFoldoutstate[j] = foldoutState[i + 1];
                            }
                            foldoutState = newFoldoutstate;
                            EditorGUILayout.EndHorizontal();
                            break;
                        }
                    }
                }
                EditorGUILayout.EndHorizontal();
                if (foldoutState[i])
                    CreateEditor(schema).OnInspectorGUI();
            }
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if(!groupTarget.ReadOnly)
            {
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
            }
            EditorGUILayout.EndHorizontal();

            serializedObject.ApplyModifiedProperties();
        }

        void OnAddSchema(object context)
        {
            groupTarget.AddSchema(context as Type);
            var newFoldoutstate = new bool[groupTarget.Schemas.Count];
            for (int i = 0; i < foldoutState.Length; i++)
                newFoldoutstate[i] = foldoutState[i];
            foldoutState = newFoldoutstate;
            foldoutState[foldoutState.Length - 1] = true;
        }
        
    }

}
