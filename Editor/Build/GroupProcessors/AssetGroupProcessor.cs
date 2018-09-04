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
    /// [Obsolete] API for creating custom processors for asset groups.  NOTE: This API is going to be replaced soon with a more flexible build system.
    /// </summary>
    //[Obsolete("This API is going to be replaced soon with a more flexible build system.")]
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

        internal virtual void CreateCatalog(AddressableAssetGroup assetGroup, ContentCatalogData contentCatalog, List<ResourceLocationData> locations, string playerVersion)
        {
        }

        internal virtual void CreateDefaultData(AddressableAssetGroup assetGroup)
        {
        }
    }
}
