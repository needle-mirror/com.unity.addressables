using UnityEditor;
using UnityEngine;
using System.Reflection;
using System;
using Object = UnityEngine.Object;

namespace UnityEditor.AddressableAssets
{
    [InitializeOnLoad]
    internal class AddressableAssetInspectorGUI
    {
        static AddressableAssetInspectorGUI()
        {
            InspectorWindow.OnPostHeaderGUI += OnPostHeaderGUI;
        }

        static GUIContent addressableAssetToggleText = new GUIContent("Address", "Check this to mark this asset as an Addressable Asset, which includes it in the bundled data and makes it loadable via script by its address.");

        static void SetAAEntry(Editor editor, AddressableAssetSettings aaSettings, Object[] targets, bool create)
        {
            Undo.RecordObject(aaSettings, "AddressableAssetSettings");
            string path = string.Empty;
            var guid = string.Empty;
            //if (create || EditorUtility.DisplayDialog("Remove Addressable Asset Entries", "Do you want to remove Addressable Asset entries for " + targets.Length + " items?", "Yes", "Cancel"))
            {
                foreach (var t in targets)
                {
                    if (AddressablesUtility.GetPathAndGUIDFromTarget(t, ref path, ref guid))
                    {
                        if (create)
                            aaSettings.CreateOrMoveEntry(guid, aaSettings.DefaultGroup);
                        else
                            aaSettings.RemoveAssetEntry(guid);
                    }
                }
            }
        }

        static GUIStyle toggleMixed = null;
        static protected void OnPostHeaderGUI(Editor editor)
        {
            var aaSettings = AddressableAssetSettings.GetDefault(false, false);
            string path = string.Empty;
            var guid = string.Empty;
            AddressableAssetSettings.AssetGroup.AssetEntry entry = null;

            if (editor.targets.Length > 0)
            {
                int addressableCount = 0;
                bool foundValidAsset = false;
                foreach (var t in editor.targets)
                {
                    if (AddressablesUtility.GetPathAndGUIDFromTarget(t, ref path, ref guid))
                    {
                        foundValidAsset = true;

                        if (aaSettings != null)
                        {
                            entry = aaSettings.FindAssetEntry(guid);
                            if (entry != null && !entry.isSubAsset)
                            {
                                addressableCount++;
                            }
                        }
                    }
                }


                if (!foundValidAsset)
                    return;

                if (addressableCount == 0)
                {
                    if (GUILayout.Toggle(false, addressableAssetToggleText, GUILayout.ExpandWidth(false)))
                        SetAAEntry(editor, AddressableAssetSettings.GetDefault(true, true), editor.targets, true);
                }
                else if (addressableCount == editor.targets.Length)
                {
                    GUILayout.BeginHorizontal();
                    if (!GUILayout.Toggle(true, addressableAssetToggleText, GUILayout.ExpandWidth(false)))
                        SetAAEntry(editor, aaSettings, editor.targets, false);

                    if (editor.targets.Length == 1)
                    {
                        entry.address = EditorGUILayout.DelayedTextField(entry.address, GUILayout.ExpandWidth(true));
                    }
                    GUILayout.EndHorizontal();
                }
                else
                {
                    GUILayout.BeginHorizontal();
                    if (toggleMixed == null)
                        toggleMixed = new GUIStyle("ToggleMixed");
                    if (GUILayout.Toggle(false, addressableAssetToggleText, toggleMixed, GUILayout.ExpandWidth(false)))
                        SetAAEntry(editor, AddressableAssetSettings.GetDefault(true, true), editor.targets, true);
                    EditorGUILayout.LabelField(addressableCount + " out of " + editor.targets.Length + " assets are addressable.");
                    GUILayout.EndHorizontal();
                }
            }
        }
    }
}
