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


        private void OnEnable()
        {
            groupTarget = target as AddressableAssetGroup;
            groupTarget.Settings.OnModification += OnSettingsModification;
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
                case AddressableAssetSettings.ModificationEvent.GroupProcessorModified:
                case AddressableAssetSettings.ModificationEvent.BatchModification:
                case AddressableAssetSettings.ModificationEvent.ActiveProfileSet:
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
            groupTarget.Data.OnGUI(groupTarget);
            
            serializedObject.ApplyModifiedProperties();
        }

        
    }

}
