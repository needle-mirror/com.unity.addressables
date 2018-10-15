using UnityEngine;
using System;
using UnityEditor.IMGUI.Controls;
using System.Collections.Generic;
using UnityEngine.AddressableAssets;
using System.Linq;

namespace UnityEditor.AddressableAssets
{
    [CustomPropertyDrawer(typeof(AssetLabelReference), true)]
    internal class AssetLabelReferenceDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var currentLabel = property.FindPropertyRelative("m_labelString");
            var smallPos = EditorGUI.PrefixLabel(position, label);
            if (AddressableAssetSettingsDefaultObject.Settings == null)
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUI.LabelField(smallPos, new GUIContent(currentLabel.stringValue));
            }
            else
            {
                var labelList = AddressableAssetSettingsDefaultObject.Settings.labelTable.labelNames.ToArray();
                var currIndex = Array.IndexOf(labelList, currentLabel.stringValue);
                var newIndex = EditorGUI.Popup(smallPos, currIndex, labelList);
                if (newIndex != currIndex)
                {
                    currentLabel.stringValue = labelList[newIndex];
                }
            }
            EditorGUI.EndProperty();
        }

    }

}
