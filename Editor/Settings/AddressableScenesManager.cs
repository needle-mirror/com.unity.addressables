using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor.AddressableAssets
{
    [InitializeOnLoad]
    internal static class AddressableScenesManager
    {
        static AddressableScenesManager()
        {
            EditorBuildSettings.sceneListChanged += OnScenesChanged;
            RegisterForSettingsCallback(AddressableAssetSettingsDefaultObject.Settings);
        }

        internal static void RegisterForSettingsCallback(AddressableAssetSettings settings)
        {
            if (settings != null)
                settings.OnModification += OnSettingsChanged;
        }

        private static void OnSettingsChanged(AddressableAssetSettings settings, AddressableAssetSettings.ModificationEvent evt, object obj)
        {
            switch (evt)
            {
                case AddressableAssetSettings.ModificationEvent.EntryCreated:
                case AddressableAssetSettings.ModificationEvent.EntryAdded:
                case AddressableAssetSettings.ModificationEvent.EntryMoved:
                case AddressableAssetSettings.ModificationEvent.EntryModified:
                    var entries = obj as List<AddressableAssetEntry>;
                    if (entries == null)
                    {
                        entries = new List<AddressableAssetEntry>();
                        entries.Add(obj as AddressableAssetEntry);
                    }
                    CheckForScenesInBuildList(entries);
                    break;
            }
        }

        private static void OnScenesChanged()
        {
            //ignore the play mode changes...
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
                return;
            
            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled)
                {
                    var entry = settings.FindAssetEntry(scene.guid.ToString());
                    if (entry != null)
                    {
                        Debug.LogWarning("An addressable scene was added to the build scenes list and can thus no longer be addressable.  " + scene.path);
                        settings.RemoveAssetEntry(scene.guid.ToString());
                    }
                }
            }


        }
        private static void CheckForScenesInBuildList(IList<AddressableAssetEntry> entries)
        {
            if (entries == null)
                return;

            var sceneListCopy = EditorBuildSettings.scenes;
            bool changed = false;
            foreach (var entry in entries)
            {
                if (entry == null)
                    continue;

                for (int index = 0; index < sceneListCopy.Length; index++)
                {
                    var scene = sceneListCopy[index];
                    if (scene.enabled && entry.AssetPath == scene.path)
                    {
                        Debug.LogWarning("A scene from the EditorBuildScenes list has been marked as addressable. It has thus been disabled in the build scenes list.  " + scene.path);
                        sceneListCopy[index].enabled = false;
                        changed = true;
                    }
                }
            }
            if (changed)
                EditorBuildSettings.scenes = sceneListCopy;
        }
    }
}
