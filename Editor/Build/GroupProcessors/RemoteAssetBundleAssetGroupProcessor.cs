using System.ComponentModel;
using UnityEngine;
using UnityEngine.ResourceManagement;
using System.IO;
using UnityEngine.AddressableAssets;
using System.Collections.Generic;

namespace UnityEditor.AddressableAssets
{
    [Description("Remote Packed Content")]
    public class RemoteAssetBundleAssetGroupProcessor : AssetBundleAssetGroupProcessor
    {
        /// <summary>
        /// TODO - doc
        /// </summary>
        [SerializeField]
        protected AddressableAssetSettings.ProfileSettings.ProfileValue m_buildPath;
        /// <summary>
        /// TODO - doc
        /// </summary>
        [SerializeField]
        protected AddressableAssetSettings.ProfileSettings.ProfileValue m_loadPrefix;
        /// <summary>
        /// TODO - doc
        /// </summary>
        public AddressableAssetSettings.ProfileSettings.ProfileValue buildPath
        {
            get
            {
                var settings = AddressableAssetSettings.GetDefault(false, false);
                if ((m_buildPath == null || string.IsNullOrEmpty(m_buildPath.value)) && settings != null)
                    m_buildPath = settings.profileSettings.CreateProfileValue(settings.profileSettings.GetVariableIdFromName("StreamingAsssetsBuildPath"));
                return m_buildPath;
            }
        }
        /// <summary>
        /// TODO - doc
        /// </summary>
        public AddressableAssetSettings.ProfileSettings.ProfileValue loadPrefix
        {
            get
            {
                var settings = AddressableAssetSettings.GetDefault(false, false);
                if ((m_loadPrefix == null || string.IsNullOrEmpty(m_loadPrefix.value)) && settings != null)
                    m_loadPrefix = settings.profileSettings.CreateProfileValue(settings.profileSettings.GetVariableIdFromName("StreamingAssetsLoadPrefix"));
                return m_loadPrefix;
            }
        }
        public BundleMode bundleMode = BundleMode.PackTogether;

        internal override string displayName { get { return "Remote Packed Content"; } }
        internal override void Initialize(AddressableAssetSettings settings)
        {
            if(m_buildPath == null)
                m_buildPath = settings.profileSettings.CreateProfileValue(settings.profileSettings.GetVariableIdFromName("StreamingAsssetsBuildPath"));
            if(m_loadPrefix == null)
                m_loadPrefix = settings.profileSettings.CreateProfileValue(settings.profileSettings.GetVariableIdFromName("StreamingAssetsLoadPrefix"));
        }

        internal override void SerializeForHash(System.Runtime.Serialization.Formatters.Binary.BinaryFormatter formatter, Stream stream)
        {
            formatter.Serialize(stream, bundleMode);
            formatter.Serialize(stream, buildPath);
            formatter.Serialize(stream, loadPrefix);
        }

        protected override string GetBuildPath(AddressableAssetSettings settings)
        {
            return buildPath.Evaluate(settings.profileSettings, settings.activeProfile);
        }

        protected override string GetBundleLoadPath(AddressableAssetSettings settings, string bundleName)
        {
            return loadPrefix.Evaluate(settings.profileSettings, settings.activeProfile) + "/" + bundleName;
        }

        protected override string GetBundleLoadProvider(AddressableAssetSettings settings)
        {
            return typeof(RemoteAssetBundleProvider).FullName;
        }

        protected override BundleMode GetBundleMode(AddressableAssetSettings settings)
        {
            return bundleMode;
        }

        internal override int GetPriority(AddressableAssetSettings aaSettings, AddressableAssetSettings.AssetGroup group)
        {
            return 0;
        }

        internal override void CreateCatalog(AddressableAssetSettings aaSettings, AddressableAssetSettings.AssetGroup group, ResourceLocationList contentCatalog, List<ResourceLocationData> locations)
        {
            var buildPath = GetBuildPath(aaSettings) + aaSettings.profileSettings.Evaluate(aaSettings.activeProfile, "/catalog_[version].json");
            var remoteHashLoadPath = m_loadPrefix.Evaluate(aaSettings.profileSettings, aaSettings.activeProfile) + "/catalog_{version}.hash";
            var localCacheLoadPath = "{UnityEngine.Application.persistentDataPath}/catalog_{version}.hash";

            var jsonText = JsonUtility.ToJson(contentCatalog);
            var contentHash = Build.Utilities.HashingMethods.CalculateMD5Hash(jsonText).ToString();

            var buildPathDir = Path.GetDirectoryName(buildPath);
            if (!Directory.Exists(buildPathDir))
                Directory.CreateDirectory(buildPathDir);
            File.WriteAllText(buildPath, jsonText);
            File.WriteAllText(buildPath.Replace(".json",".hash"), contentHash);
            var remoteHash = new ResourceLocationData("RemoteCatalogHash" + group.guid, "", remoteHashLoadPath, typeof(TextDataProvider).FullName, false);
            var localHash = new ResourceLocationData("LocalCatalogHash" + group.guid, "", localCacheLoadPath, typeof(TextDataProvider).FullName, false);


            int priority = GetPriority(aaSettings, group);
            locations.Add(new ResourceLocationData(priority + "_RemoteCatalog_" + group.guid, "", remoteHashLoadPath.Replace(".hash", ".json"), typeof(ContentCatalogProvider).FullName, true,
                ResourceLocationData.LocationType.String, 1, "", 
                new string[] { localHash.m_address, remoteHash.m_address}));
            locations.Add(localHash);
            locations.Add(remoteHash);
        }

        [SerializeField]
        Vector2 position = new Vector2();
        internal override void OnDrawGUI(AddressableAssetSettings settings, Rect rect)
        {
            GUILayout.BeginArea(rect);
            position = EditorGUILayout.BeginScrollView(position, false, false, GUILayout.MaxWidth(rect.width));
            EditorGUILayout.LabelField("Assets in this group can either be packed together or separately and will be downloaded from a URL via UnityWebRequest.");
            bool modified = false;
            var newBundleMode = (BundleMode)EditorGUILayout.EnumPopup("Packing Mode", bundleMode);
            if (newBundleMode != bundleMode)
            {
                bundleMode = newBundleMode;
                modified = true;
            }

            modified |= ProfileSettingsEditor.ValueGUI(settings, "Build Path", buildPath);
            modified |= ProfileSettingsEditor.ValueGUI(settings, "Load Prefix", loadPrefix);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            if(modified)
                settings.PostModificationEvent(AddressableAssetSettings.ModificationEvent.GroupProcessorModified, this);
        }
    }
}
