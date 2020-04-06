using System;
using System.Collections.Generic;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace UnityEditor.AddressableAssets.GUI
{
    using Object = UnityEngine.Object;
    
    [InitializeOnLoad]
    static class AddressableAssetInspectorGUI
    {
        static GUIStyle s_ToggleMixed;
        static GUIContent s_AddressableAssetToggleText;

        static AddressableAssetInspectorGUI()
        {
            s_ToggleMixed = null;
            s_AddressableAssetToggleText = new GUIContent("Addressable", "Check this to mark this asset as an Addressable Asset, which includes it in the bundled data and makes it loadable via script by its address.");
            Editor.finishedDefaultHeaderGUI += OnPostHeaderGUI;
        }

        static void SetAaEntry(AddressableAssetSettings aaSettings, Object[] targets, bool create)
        {

            if (create && aaSettings.DefaultGroup.ReadOnly)
            {
                Debug.LogError("Current default group is ReadOnly.  Cannot add addressable assets to it");
                return;
            }
            
            Undo.RecordObject(aaSettings, "AddressableAssetSettings");
            string path;
            var guid = string.Empty;
            //if (create || EditorUtility.DisplayDialog("Remove Addressable Asset Entries", "Do you want to remove Addressable Asset entries for " + targets.Length + " items?", "Yes", "Cancel"))
            {
                var entriesAdded = new List<AddressableAssetEntry>();
                var modifiedGroups = new HashSet<AddressableAssetGroup>();
                Type mainAssetType;
                foreach (var t in targets)
                {
                    if (AddressableAssetUtility.GetPathAndGUIDFromTarget(t, out path, ref guid, out mainAssetType))
                    {
                        if (create)
                        {
                            if (AddressableAssetUtility.IsInResources(path))
                                AddressableAssetUtility.SafeMoveResourcesToGroup(aaSettings, aaSettings.DefaultGroup, new List<string> { path });
                            else
                            {
                                var e = aaSettings.CreateOrMoveEntry(guid, aaSettings.DefaultGroup, false, false);
                                entriesAdded.Add(e);
                                modifiedGroups.Add(e.parentGroup);
                            }
                        }
                        else
                            aaSettings.RemoveAssetEntry(guid);
                    }
                }

                if (create)
                {
                    foreach (var g in modifiedGroups)
                        g.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entriesAdded, false, true);
                    aaSettings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entriesAdded, true, false);
                }
            }
        }

        static void OnPostHeaderGUI(Editor editor)
        {
            var aaSettings = AddressableAssetSettingsDefaultObject.Settings;
            var guid = string.Empty;
            AddressableAssetEntry entry = null;

            if (editor.targets.Length > 0)
            {
                int addressableCount = 0;
                bool foundValidAsset = false;
                bool foundAssetGroup = false;
                foreach (var t in editor.targets)
                {
                    string path;
                    Type mainAssetType;
                    if (AddressableAssetUtility.GetPathAndGUIDFromTarget(t, out path, ref guid, out mainAssetType) &&
                        path.ToLower().Contains("assets") &&
                        mainAssetType != null)
                    {
                        // Is addressable group
                        if (path.ToLower().Contains("addressableassetsdata/assetgroups"))
                        {
                            foundAssetGroup = true;
                        }
                        // Is asset
                        if (!BuildUtility.IsEditorAssembly(mainAssetType.Assembly))
                        {
                            foundValidAsset = true;

                            if (aaSettings != null)
                            {
                                entry = aaSettings.FindAssetEntry(guid);
                                if (entry != null && !entry.IsSubAsset)
                                {
                                    addressableCount++;
                                }
                            }
                        }
                    }
                }

                if (foundAssetGroup)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Profile: " + AddressableAssetSettingsDefaultObject.GetSettings(true).profileSettings.
                                        GetProfileName(AddressableAssetSettingsDefaultObject.GetSettings(true).activeProfileId));
                    
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("System Settings", "MiniButton"))
                    {
                        EditorGUIUtility.PingObject(AddressableAssetSettingsDefaultObject.Settings);
                        Selection.activeObject = AddressableAssetSettingsDefaultObject.Settings;
                    }
                    GUILayout.EndHorizontal();
                }

                if (!foundValidAsset)
                    return;

                if (addressableCount == 0)
                {
                    if (GUILayout.Toggle(false, s_AddressableAssetToggleText, GUILayout.ExpandWidth(false)))
                        SetAaEntry(AddressableAssetSettingsDefaultObject.GetSettings(true), editor.targets, true);
                }
                else if (addressableCount == editor.targets.Length)
                {
                    GUILayout.BeginHorizontal();
                    if (!GUILayout.Toggle(true, s_AddressableAssetToggleText, GUILayout.ExpandWidth(false)))
                        SetAaEntry(aaSettings, editor.targets, false);

                    if (editor.targets.Length == 1 && entry != null)
                    {
                        entry.address = EditorGUILayout.DelayedTextField(entry.address, GUILayout.ExpandWidth(true));
                    }
                    GUILayout.EndHorizontal();
                }
                else
                {
                    GUILayout.BeginHorizontal();
                    if (s_ToggleMixed == null)
                        s_ToggleMixed = new GUIStyle("ToggleMixed");
                    if (GUILayout.Toggle(false, s_AddressableAssetToggleText, s_ToggleMixed, GUILayout.ExpandWidth(false)))
                        SetAaEntry(AddressableAssetSettingsDefaultObject.GetSettings(true), editor.targets, true);
                    EditorGUILayout.LabelField(addressableCount + " out of " + editor.targets.Length + " assets are addressable.");
                    GUILayout.EndHorizontal();
                }
            }
        }
    }
}
