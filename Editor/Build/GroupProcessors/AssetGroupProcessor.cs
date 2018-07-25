using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine.AddressableAssets;
using System;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEditor.IMGUI.Controls;

namespace UnityEditor.AddressableAssets
{
    /// <summary>
    /// TODO - doc
    /// </summary>
    public class AssetGroupProcessor
    {
        internal string GetDataString(AddressableAssetGroup assetGroup, string dataKey, string profileVarName, string profileVarDefaultValue)
        {
            var profileDataId = assetGroup.Data.GetData(dataKey, "");
            if (string.IsNullOrEmpty(profileDataId))
                assetGroup.Data.SetData(dataKey, profileDataId = assetGroup.Settings.profileSettings.CreateValue(profileVarName, profileVarDefaultValue));
            return AddressableAssetProfileSettings.ProfileIDData.Evaluate(assetGroup.Settings.profileSettings, assetGroup.Settings.activeProfileId, profileDataId);
        }

        internal virtual void ProcessGroup(AddressableAssetGroup assetGroup, List<AssetBundleBuild> bundleInputDefs, List<ContentCatalogDataEntry> locationData)
        {
        }

        internal virtual void CreateResourceLocationData(AddressableAssetGroup assetGroup, string bundleName, List<GUID> assetsInBundle, Dictionary<GUID, List<string>> assetsToBundles, List<ContentCatalogDataEntry> locations)
        {
        }

        internal virtual void PostProcessBundles(AddressableAssetGroup assetGroup, List<string> bundles, IBundleBuildResults buildResult, IWriteData writeData, ResourceManagerRuntimeData runtimeData, List<ContentCatalogDataEntry> locations)
        {
        }

        internal virtual void CreateCatalog(AddressableAssetGroup assetGroup, ContentCatalogData contentCatalog, List<ResourceLocationData> locations)
        {
        }

        internal virtual void CreateDefaultData(AddressableAssetGroup assetGroup)
        {
        }

        internal int GetPriority(AddressableAssetGroup assetGroup)
        {
            return assetGroup.Data.GetData("Priority", 0, true);
        }

        KeyDataStoreTreeView tree = null;
        [SerializeField]
        TreeViewState treeState;
        [SerializeField]
        MultiColumnHeaderState mchs;
        internal virtual void OnDrawGUI(AddressableAssetGroup assetGroup, Rect rect)
        {
            if (tree == null)
            {
                if (treeState == null)
                    treeState = new TreeViewState();

                var headerState = KeyDataStoreTreeView.CreateDefaultMultiColumnHeaderState();
                if (MultiColumnHeaderState.CanOverwriteSerializedFields(mchs, headerState))
                    MultiColumnHeaderState.OverwriteSerializedFields(mchs, headerState);
                mchs = headerState;

                tree = new KeyDataStoreTreeView(assetGroup.Settings, assetGroup.Data, treeState, mchs);
                tree.Reload();
            }

            tree.OnGUI(rect);
        }

    }
}
