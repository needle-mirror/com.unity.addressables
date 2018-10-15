using UnityEngine;
using System.Collections.Generic;
using UnityEditor.IMGUI.Controls;
using System.Linq;
using System;

namespace UnityEditor.AddressableAssets
{
    internal class ProfilesEditor
    {
        static public string ValueGUILayout(AddressableAssetSettings settings, string label, string currentID)
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
            EditorGUILayout.EndHorizontal();
            EditorGUI.indentLevel += 1;
            if (custom)
                result = EditorGUILayout.TextField(result);
            else if (!string.IsNullOrEmpty(toolTip))
                EditorGUILayout.HelpBox(toolTip, MessageType.None);
            EditorGUI.indentLevel -= 1;
            return result;
        }

        static public float CalcGUIHeight(AddressableAssetSettings settings, string label, string currentID)
        {
            var labelContent = new GUIContent(label);
            var size = EditorStyles.popup.CalcSize(labelContent);
            var height = size.y + EditorGUIUtility.standardVerticalSpacing;
            AddressableAssetProfileSettings.ProfileIDData data = settings.profileSettings.GetProfileDataById(currentID);
            if (data != null)
            {
                var val = data.Evaluate(settings.profileSettings, settings.activeProfileId);
                var h = EditorStyles.helpBox.CalcHeight(new GUIContent(val), EditorGUIUtility.currentViewWidth - 20);
                return height + h;
            }
            return height + EditorStyles.textField.CalcHeight(new GUIContent(currentID), EditorGUIUtility.currentViewWidth - 20);
        }

        static public string ValueGUI(Rect rect, AddressableAssetSettings settings, string label, string currentID)
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

            var labelContent = new GUIContent(label);
            var size = EditorStyles.popup.CalcSize(labelContent);
            var topRect = new Rect(rect.x, rect.y, rect.width, size.y);

            var newIndex = EditorGUI.Popup(topRect, label, currentIndex, displayNames.ToArray());
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
            var bottomRect = new Rect(rect.x + 10, rect.y + size.y + EditorGUIUtility.standardVerticalSpacing, rect.width - 20, rect.height - (size.y + EditorGUIUtility.standardVerticalSpacing));
            if (custom)
                result = EditorGUI.TextField(bottomRect, result);
            else if (!string.IsNullOrEmpty(toolTip))
                EditorGUI.HelpBox(bottomRect, toolTip, MessageType.None);
            return result;
        }

    }
}
