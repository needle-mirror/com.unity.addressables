using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine.AddressableAssets;
using System;
using UnityEditor.Build.Pipeline.Interfaces;

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

        internal virtual void ProcessGroup(AddressableAssetSettings settings, AddressableAssetGroup assetGroup, List<AssetBundleBuild> bundleInputDefs, Dictionary<object, ContentCatalogData.DataEntry> locationData)
        {
        }

        internal virtual bool HasSettings() { return true; }

        internal virtual void OnDrawGUI(AddressableAssetSettings settings, Rect rect)
        {
        }

        internal virtual void CreateResourceLocationData(AddressableAssetSettings settings, AddressableAssetGroup assetGroup, string bundleName, List<GUID> assetsInBundle, Dictionary<GUID, List<string>> assetsToBundles, Dictionary<object, ContentCatalogData.DataEntry> locations)
        {
        }

        internal virtual void PostProcessBundles(AddressableAssetSettings aaSettings, AddressableAssetGroup assetGroup, List<string> bundles, IBundleBuildResults buildResult, IWriteData writeData, ResourceManagerRuntimeData runtimeData, Dictionary<object, ContentCatalogData.DataEntry> locations)
        {
        }

        internal virtual void SerializeForHash(BinaryFormatter formatter, Stream stream)
        {
            
        }

        internal virtual void CreateCatalog(AddressableAssetSettings aaSettings, AddressableAssetGroup group, ContentCatalogData contentCatalog, List<ResourceLocationData> locations)
        {
        }

        internal virtual int GetPriority(AddressableAssetSettings aaSettings, AddressableAssetGroup group)
        {
            return int.MaxValue;
        }

        internal virtual bool Validate(AddressableAssetSettings aaSettings, AddressableAssetGroup assetGroup)
        {
            return true;
        }
    }
}
