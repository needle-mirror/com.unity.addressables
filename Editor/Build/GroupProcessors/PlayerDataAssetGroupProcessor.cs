using System.Collections.Generic;
using UnityEngine;
using System.ComponentModel;
using UnityEngine.ResourceManagement;
using UnityEngine.AddressableAssets;

namespace UnityEditor.AddressableAssets
{
    internal class PlayerDataAssetGroupProcessor : AssetGroupProcessor
    {
        internal override void ProcessGroup(AddressableAssetGroup assetGroup, List<AssetBundleBuild> bundleInputDefs, List<ContentCatalogDataEntry> locationData)
        {
            foreach (var e in assetGroup.entries)
            {
                var assets = new List<AddressableAssetEntry>();
                e.GatherAllAssets(assets, true, true);
                foreach (var s in assets)
                {
                    var assetPath = s.GetAssetLoadPath(false);
                    if (s.IsScene)
                    {
                        locationData.Add(new ContentCatalogDataEntry(s.address, s.guid, assetPath, typeof(SceneProvider)));
                        var indexInSceneList = IndexOfSceneInEditorBuildSettings(new GUID(s.guid));
                        if (indexInSceneList >= 0)
                            locationData.Add(new ContentCatalogDataEntry(indexInSceneList.ToString(), s.guid, assetPath, typeof(SceneProvider)));
                    }
                    else
                    {
                        locationData.Add(new ContentCatalogDataEntry(s.address, s.guid, assetPath, typeof(LegacyResourcesProvider)));
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
    }
}
