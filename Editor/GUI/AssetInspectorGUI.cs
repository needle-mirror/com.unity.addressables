using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

            var targetInfos = new List<TargetInfo>();
            foreach (var t in targets)
            {
                if (AddressableAssetUtility.GetPathAndGUIDFromTarget(t, out var path, out var guid, out var mainAssetType))
                {
                    targetInfos.Add(new TargetInfo(){Guid = guid, Path = path, MainAssetType = mainAssetType});
                }
            }

            if (!create)
            {
                targetInfos.ForEach(ti =>
                {
                    AddressableAssetGroup group = aaSettings.FindAssetEntry(ti.Guid).parentGroup;
                    aaSettings.RemoveAssetEntry(ti.Guid);
                    AddressableAssetUtility.OpenAssetIfUsingVCIntegration(group);
                });
            }
            else
            {
                var resourceTargets = targetInfos.Where(ti => AddressableAssetUtility.IsInResources(ti.Path));
                if (resourceTargets.Any())
                {
                    var resourcePaths = resourceTargets.Select(t => t.Path).ToList();
                    var resourceGuids = resourceTargets.Select(t => t.Guid).ToList();
                    AddressableAssetUtility.SafeMoveResourcesToGroup(aaSettings, aaSettings.DefaultGroup, resourcePaths, resourceGuids);
                }

                var entriesAdded = new List<AddressableAssetEntry>();
                var modifiedGroups = new HashSet<AddressableAssetGroup>();
                var otherTargetInfos = targetInfos.Except(resourceTargets);
                foreach (var info in otherTargetInfos)
                {
                    var e = aaSettings.CreateOrMoveEntry(info.Guid, aaSettings.DefaultGroup, false, false);
                    entriesAdded.Add(e);
                    modifiedGroups.Add(e.parentGroup);
                }

                foreach (var g in modifiedGroups)
                {
                    g.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entriesAdded, false, true);
                    AddressableAssetUtility.OpenAssetIfUsingVCIntegration(g);
                }

                aaSettings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entriesAdded, true, false);
            }
        }

        static void OnPostHeaderGUI(Editor editor)
        {
            var aaSettings = AddressableAssetSettingsDefaultObject.Settings;
            AddressableAssetEntry entry = null;

            if (editor.targets.Length > 0)
            {
                int addressableCount = 0;
                bool foundValidAsset = false;
                bool foundAssetGroup = false;
                foreach (var t in editor.targets)
                {
                    foundAssetGroup |= t is AddressableAssetGroup;
                    foundAssetGroup |= t is AddressableAssetGroupSchema;
                    if (AddressableAssetUtility.GetPathAndGUIDFromTarget(t, out var path, out var guid, out var mainAssetType))
                    {
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
                    {
                        SetAaEntry(aaSettings, editor.targets, false);
                        GUIUtility.ExitGUI();
                    }

                    if (editor.targets.Length == 1 && entry != null)
                    {
                        string newAddress = EditorGUILayout.DelayedTextField(entry.address, GUILayout.ExpandWidth(true));
                        if (newAddress != entry.address)
                        {
                            if (newAddress.Contains("[") && newAddress.Contains("]"))
                                Debug.LogErrorFormat("Rename of address '{0}' cannot contain '[ ]'.", entry.address);
                            else
                            {
                                entry.address = newAddress;
                                AddressableAssetUtility.OpenAssetIfUsingVCIntegration(entry.parentGroup, true);
                            }
                        }
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

        class TargetInfo
        {
            public string Guid;
            public string Path;
            public Type MainAssetType;
        }
    }
}
