using UnityEngine;
using System.Collections.Generic;
using UnityEditor.IMGUI.Controls;
using System.Linq;
using System;

namespace UnityEditor.AddressableAssets
{
    internal class ProfilesEditor
    {
        static public string ValueGUI(AddressableAssetSettings settings, string label, string currentID)
        {
            string result = currentID;
            if (settings == null)
                return result;

            var displayNames = settings.profileSettings.GetVariableNames();
            AddressableAssetProfileSettings.ProfileIDData data = settings.profileSettings.GetProfileDataById(currentID);
            bool custom = data == null;

            int currentIndex = displayNames.Count;
            string toolTip = string.Empty;
            if (!custom)
            {
                currentIndex = displayNames.IndexOf(data.Name);
                toolTip = data.Evaluate(settings.profileSettings, settings.activeProfileId);
            }
            displayNames.Add(AddressableAssetProfileSettings.k_customEntryString);
      

            var content = new GUIContent(label, toolTip);
            EditorGUILayout.BeginHorizontal();
            var newIndex = EditorGUILayout.Popup(content, currentIndex, displayNames.ToArray());
            if (newIndex != currentIndex)
            {
                if (displayNames[newIndex] == AddressableAssetProfileSettings.k_customEntryString)
                {
                    custom = true;
                    result = "<undefined>";
                }
                else
                {
                    data = settings.profileSettings.GetProfileDataByName(displayNames[newIndex]);
                    if (data != null)
                        result = data.Id;
                }
            }
            if (custom)
            {
                result = EditorGUILayout.TextField(result);
            }
            EditorGUILayout.EndHorizontal();
            if(!string.IsNullOrEmpty(toolTip))
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUI.indentLevel += 1;
                EditorGUILayout.HelpBox(toolTip, MessageType.None);
                EditorGUI.indentLevel -= 1;
                EditorGUILayout.EndHorizontal();
            }
            return result;
        }
    }
}
