using System.ComponentModel;
using UnityEngine;
using UnityEngine.ResourceManagement;
using UnityEngine.AddressableAssets;
using System.Collections.Generic;
using System.IO;

namespace UnityEditor.AddressableAssets
{
    /// <summary>
    /// TODO - doc
    /// </summary>
    [Description("Local Packed Content")]
    public class LocalAssetBundleAssetGroupProcessor : AssetBundleAssetGroupProcessor
    {
        [SerializeField]
        Vector2 position = new Vector2();
        internal override void OnDrawGUI(AddressableAssetSettings settings, Rect rect)
        {
            GUILayout.BeginArea(rect);
            position = EditorGUILayout.BeginScrollView(position, false, false, GUILayout.MaxWidth(rect.width));
            bool oldWrap = EditorStyles.label.wordWrap;
            EditorStyles.label.wordWrap = true;
            EditorGUILayout.LabelField("Assets in this group will be packed together in the StreamingAssets folder and will be delivered with the game");
            EditorStyles.label.wordWrap = oldWrap;
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        internal override string displayName { get { return "Local Packed Content"; } }


        protected override string GetBuildPath(AddressableAssetSettings settings)
        {
            return "Assets/StreamingAssets";
        }

        protected override string GetBundleLoadPath(AddressableAssetSettings settings, string bundleName)
        {
            return "{UnityEngine.Application.streamingAssetsPath}/" + bundleName;
        }

        protected override System.Type GetBundleLoadProvider(AddressableAssetSettings settings)
        {
            return typeof(LocalAssetBundleProvider);
        }

        protected override BundleMode GetBundleMode(AddressableAssetSettings settings)
        {
            return BundleMode.PackTogether;
        }

        internal override int GetPriority(AddressableAssetSettings aaSettings, AddressableAssetSettings.AssetGroup group)
        {
            return 100;
        }

        internal override void CreateCatalog(AddressableAssetSettings aaSettings, AddressableAssetSettings.AssetGroup group, ContentCatalogData contentCatalog, List<ResourceLocationData> locations)
        {
            var buildPath = GetBuildPath(aaSettings) + aaSettings.profileSettings.EvaluateString(aaSettings.activeProfileId, "/catalog_[ContentVersion].json");
            var remoteHashLoadPath = "file://{UnityEngine.Application.streamingAssetsPath}/catalog_{ContentVersion}.hash";
            var localCacheLoadPath = "{UnityEngine.Application.persistentDataPath}/catalog_{ContentVersion}.hash";
            
            var jsonText = JsonUtility.ToJson(contentCatalog);
            var contentHash = Build.Pipeline.Utilities.HashingMethods.CalculateMD5Hash(jsonText).ToString();

            var buildPathDir = Path.GetDirectoryName(buildPath);
            if (!Directory.Exists(buildPathDir))
                Directory.CreateDirectory(buildPathDir);

            File.WriteAllText(buildPath, jsonText);
            File.WriteAllText(buildPath.Replace(".json", ".hash"), contentHash);
            var remoteHash = new ResourceLocationData("RemoteCatalogHash" + group.guid, "", remoteHashLoadPath, typeof(TextDataProvider), false);
            var localHash = new ResourceLocationData("LocalCatalogHash" + group.guid, "", localCacheLoadPath, typeof(TextDataProvider), false);


            int priority = GetPriority(aaSettings, group);
            locations.Add(new ResourceLocationData(priority + "_RemoteCatalog_" + group.guid, "", remoteHashLoadPath.Replace(".hash", ".json"), typeof(ContentCatalogProvider), true,
                ResourceLocationData.LocationType.String, 1, "",
                new string[] { localHash.m_address, remoteHash.m_address }));
            locations.Add(localHash);
            locations.Add(remoteHash);
        }
    }
}
