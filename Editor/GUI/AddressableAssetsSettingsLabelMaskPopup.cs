using System.Collections.Generic;
using UnityEngine;
using UnityEditor.IMGUI.Controls;
using System;
using System.Linq;
using UnityEditor.Build.Pipeline.Utilities;
using UnityEngine.AddressableAssets;

namespace UnityEditor.AddressableAssets
{
    class LabelMaskPopupContent : PopupWindowContent
    {
        AddressableAssetSettings m_settings;
        List<AddressableAssetEntry> m_entries;
        Dictionary<string, int> m_labelCount;

        GUIStyle toggleMixed = null;


        int lastItemCount = -1;
        Vector2 rect = new Vector2();
        public LabelMaskPopupContent(AddressableAssetSettings settings, List<AddressableAssetEntry> e, Dictionary<string, int> count)
        {
            m_settings = settings;
            m_entries = e;
            m_labelCount = count;
        }

        public override Vector2 GetWindowSize()
        {
            var labelTable = m_settings.labelTable;
            if (lastItemCount != labelTable.labelNames.Count)
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
                GUI.skin.toggle.CalcMinMaxWidth(content, out minWidth, out maxWidth);
                var height = GUI.skin.toggle.CalcHeight(content, maxWidth) + 3.5f;
                rect = new Vector2(Mathf.Clamp(maxWidth + 40, 60, 600), Mathf.Clamp(labelTable.labelNames.Count * height, 30, 150));
                lastItemCount = labelTable.labelNames.Count;
            }
            return rect;
        }

        private void SetLabelForEntries(string label, bool value)
        {
            m_settings.SetLabelValueForEntries(m_entries, label, value);
            m_labelCount[label] = value ? m_entries.Count : 0;
        }

        [SerializeField]
        Vector2 m_scrollPosition = new Vector2();
        public override void OnGUI(Rect rect)
        {
            if (m_entries.Count == 0)
                return;


            var labelTable = m_settings.labelTable;

            GUILayout.BeginArea(new Rect(rect.xMin + 3, rect.yMin + 3, rect.width - 6, rect.height - 6));
            m_scrollPosition = GUILayout.BeginScrollView(m_scrollPosition);

            //string toRemove = null;
            foreach (var labelName in labelTable.labelNames)
            {
                EditorGUILayout.BeginHorizontal();

                bool oldState = false;
                bool newState = false;
                int count = 0;
                if (m_labelCount == null)
                    count = m_entries[0].labels.Contains(labelName) ? m_entries.Count : 0;
                else
                    m_labelCount.TryGetValue(labelName, out count);

                if (count == 0)
                {
                    newState = GUILayout.Toggle(oldState, new GUIContent(labelName), GUILayout.ExpandWidth(false));
                }
                else if (count == m_entries.Count)
                {
                    oldState = true;
                    newState = GUILayout.Toggle(oldState, new GUIContent(labelName), GUILayout.ExpandWidth(false));
                }
                else
                {
                    if (toggleMixed == null)
                        toggleMixed = new GUIStyle("ToggleMixed");
                    newState = GUILayout.Toggle(oldState, new GUIContent(labelName), toggleMixed, GUILayout.ExpandWidth(false));
                }
                if (oldState != newState)
                {
                    SetLabelForEntries(labelName, newState);
                }

                EditorGUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
