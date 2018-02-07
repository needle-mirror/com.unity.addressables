using System.Collections.Generic;
using UnityEngine;
using System.ComponentModel;
using UnityEngine.ResourceManagement;
using UnityEngine.AddressableAssets;

namespace UnityEditor.AddressableAssets
{
    /// <summary>
    /// TODO - doc
    /// </summary>
    [Description("")]
    public class PlayerDataAssetGroupProcessor : AssetGroupProcessor
    {
        internal override string displayName { get { return "Player Data"; } }

        internal override void ProcessGroup(AddressableAssetSettings settings, AddressableAssetSettings.AssetGroup assetGroup, List<AssetBundleBuild> bundleInputDefs, List<ResourceLocationData> locationData)
        {
            foreach (var e in assetGroup.entries)
            {
                var assets = new List<AddressableAssetSettings.AssetGroup.AssetEntry>();
                e.GatherAllAssets(assets, settings);
                foreach (var s in assets)
                {
                    var assetPath = s.GetAssetLoadPath(false);
                    if (s.isScene)
                    {
                        locationData.Add(new ResourceLocationData(s.address, s.guid, assetPath, typeof(SceneProvider).FullName, true, ResourceLocationData.LocationType.String, 0, typeof(UnityEngine.SceneManagement.Scene).FullName, null));
                        var indexInSceneList = IndexOfSceneInEditorBuildSettings(new GUID(s.guid));
                        if (indexInSceneList >= 0)
                            locationData.Add(new ResourceLocationData(indexInSceneList.ToString(), s.guid, assetPath, typeof(SceneProvider).FullName, true, ResourceLocationData.LocationType.Int, 0, typeof(UnityEngine.SceneManagement.Scene).FullName, null));
                    }
                    else
                    {
                        locationData.Add(new ResourceLocationData(s.address, s.guid, assetPath, typeof(SceneProvider).FullName, true, ResourceLocationData.LocationType.String, 0, typeof(UnityEngine.SceneManagement.Scene).FullName, null));
                    }
                }
            }
        }

        static int IndexOfSceneInEditorBuildSettings(GUID guid)
        {
            int index = 0;
            for (int i = 0; i < EditorBuildSettings.scenes.Length; i++)
            {
                if (EditorBuildSettings.scenes[i].enabled)
                {
                    if (EditorBuildSettings.scenes[i].guid == guid)
                        return index;
                    index++;
                }
            }
            return -1;
        }

        [SerializeField]
        Vector2 position = new Vector2();
        internal override void OnDrawGUI(AddressableAssetSettings settings, Rect rect)
        {
            GUILayout.BeginArea(rect);
            position = EditorGUILayout.BeginScrollView(position, false, false, GUILayout.MaxWidth(rect.width));

            EditorStyles.label.wordWrap = true;
            EditorGUILayout.LabelField("Player Data Processor");
            EditorGUILayout.LabelField("This processor handles proper building of all assets stored in Resources and the scenes that are included in the build in BuildSettings window. All data built here will be included in \"Player Data\" in the build of the game.");

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
