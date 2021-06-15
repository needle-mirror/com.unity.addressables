using System;
using System.Collections.Generic;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace UnityEditor.AddressableAssets.GUI
{
    class LabelMaskPopupContent : PopupWindowContent
    {
        AddressableAssetSettings m_Settings;
        List<AddressableAssetEntry> m_Entries;
        Dictionary<string, int> m_LabelCount;

        GUIStyle m_ToggleMixed;


        int m_LastItemCount = -1;
        Vector2 m_Rect;
        public LabelMaskPopupContent(AddressableAssetSettings settings, List<AddressableAssetEntry> e, Dictionary<string, int> count)
        {
            m_Settings = settings;
            m_Entries = e;
            m_LabelCount = count;
        }

        public override Vector2 GetWindowSize()
        {
            var labelTable = m_Settings.labelTable;
            if (m_LastItemCount != labelTable.labelNames.Count)
            {
                int maxLen = 0;
                string maxStr = "";
                for (int i = 0; i < labelTable.labelNames.Count; i++)
                {
                    var len = labelTable.labelNames[i].Length;
                    if (len > maxLen)
                    {
                        maxLen = len;
                        maxStr = labelTable.labelNames[i];
                    }
                }
                float minWidth, maxWidth;
                var content = new GUIContent(maxStr);
                UnityEngine.GUI.skin.toggle.CalcMinMaxWidth(content, out minWidth, out maxWidth);
                var height = UnityEngine.GUI.skin.toggle.CalcHeight(content, maxWidth) + 3.5f;
                m_Rect = new Vector2(Mathf.Clamp(maxWidth + 35, 125, 600), Mathf.Clamp(labelTable.labelNames.Count * height + 25, 30, 150));
                m_LastItemCount = labelTable.labelNames.Count;
            }
            return m_Rect;
        }

        void SetLabelForEntries(string label, bool value)
        {
            m_Settings.SetLabelValueForEntries(m_Entries, label, value);
            m_LabelCount[label] = value ? m_Entries.Count : 0;
        }

        Vector2 m_ScrollPosition;
        public override void OnGUI(Rect rect)
        {
            if (m_Entries.Count == 0)
                return;

            var labelTable = m_Settings.labelTable;

            var areaRect = new Rect(rect.xMin + 3, rect.yMin + 3, rect.width - 6, rect.height - 6);
            GUILayout.BeginArea(areaRect);
            m_ScrollPosition = GUILayout.BeginScrollView(m_ScrollPosition, false, false);
            Vector2 yPositionDrawRange = new Vector2(m_ScrollPosition.y - 30, m_ScrollPosition.y + rect.height + 30);
            
            foreach (var labelName in labelTable.labelNames)
            {
                var toggleRect = EditorGUILayout.GetControlRect(GUILayout.Width(areaRect.width-20));
                if (toggleRect.height > 1)
                {
                    // only draw toggles if they are in view
                    if (toggleRect.y < yPositionDrawRange.x || toggleRect.y > yPositionDrawRange.y)
                        continue;
                }
                else continue;

                bool newState;
                int count;
                if (m_LabelCount == null)
                    count = m_Entries[0].labels.Contains(labelName) ? m_Entries.Count : 0;
                else
                    m_LabelCount.TryGetValue(labelName, out count);

                bool oldState = count == m_Entries.Count;
                if (!(count == 0 || count == m_Entries.Count))
                    EditorGUI.showMixedValue = true;
                newState = EditorGUI.ToggleLeft(toggleRect, new GUIContent(labelName), oldState);
                EditorGUI.showMixedValue = false;
                
                if (oldState != newState)
                    SetLabelForEntries(labelName, newState);
            }

            if (GUILayout.Button("Manage Labels", UnityEngine.GUI.skin.button, GUILayout.ExpandWidth(false)))
            {
                EditorWindow.GetWindow<LabelWindow>(true).Intialize(m_Settings);
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
