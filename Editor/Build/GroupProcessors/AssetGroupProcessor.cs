using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEditor.Build.Interfaces;
using UnityEngine.AddressableAssets;
using System;

namespace UnityEditor.AddressableAssets
{
    /// <summary>
    /// TODO - doc
    /// </summary>
    public class AssetGroupProcessor : ScriptableObject
    {
        internal virtual string displayName { get { return GetType().Name; } }

        internal virtual void Initialize(AddressableAssetSettings settings)
        {
        }

        internal virtual void ProcessGroup(AddressableAssetSettings settings, AddressableAssetSettings.AssetGroup assetGroup, List<AssetBundleBuild> bundleInputDefs, List<ResourceLocationData> locationData)
        {
        }

        internal virtual void OnDrawGUI(AddressableAssetSettings settings, Rect rect)
        {
        }

        internal virtual void CreateResourceLocationData(AddressableAssetSettings settings, AddressableAssetSettings.AssetGroup assetGroup, string bundleName, List<GUID> assetsInBundle, Dictionary<GUID, List<string>> assetsToBundles, List<ResourceLocationData> locations)
        {
        }

        internal virtual void PostProcessBundles(AddressableAssetSettings aaSettings, AddressableAssetSettings.AssetGroup assetGroup, List<string> bundles, IBuildResults buildResult, IWriteData writeData, ResourceManagerRuntimeData runtimeData)
        {
        }

        internal virtual void SerializeForHash(BinaryFormatter formatter, Stream stream)
        {
            
        }

        internal virtual void CreateCatalog(AddressableAssetSettings aaSettings, AddressableAssetSettings.AssetGroup group, ResourceLocationList contentCatalog, List<ResourceLocationData> locations)
        {
        }

        internal virtual int GetPriority(AddressableAssetSettings aaSettings, AddressableAssetSettings.AssetGroup group)
        {
            return int.MaxValue;
        }
    }
}
