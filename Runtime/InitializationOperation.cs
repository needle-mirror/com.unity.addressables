using System;
using System.Collections.Generic;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.Diagnostics;

namespace UnityEngine.AddressableAssets
{
    class InitializationOperation : AsyncOperationBase<bool>
    {
        ResourceManagerRuntimeData.EditorPlayMode m_playMode;
        public InitializationOperation(ResourceManagerRuntimeData.EditorPlayMode playMode)
        {
            m_playMode = playMode;
            ResourceManager.SceneProvider = new SceneProvider();
            ResourceManager.ResourceProviders.Add(new JsonAssetProvider());
            ResourceManager.ResourceProviders.Add(new TextDataProvider());
            ResourceManager.ResourceProviders.Add(new ContentCatalogProvider());
            //needed to prevent stripping for this value
            var sap = Application.streamingAssetsPath;
            var playerSettingsLocation = AAConfig.ExpandPathWithGlobalVariables(ResourceManagerRuntimeData.GetPlayerSettingsLoadLocation(m_playMode));
            var runtimeDataLocation = new ResourceLocationBase("RuntimeData", playerSettingsLocation, typeof(JsonAssetProvider).FullName);
            Context = runtimeDataLocation;
            ResourceManager.ProvideResource<ResourceManagerRuntimeData>(runtimeDataLocation).Completed += OnDataLoaded;
        }

        void OnDataLoaded(IAsyncOperation<ResourceManagerRuntimeData> op)
        {
            if (op.Result == null)
                throw new Exception("Unable to load runtime data.");
            var rtd = op.Result;
            AddResourceProviders(rtd.assetCacheSize, rtd.assetCacheAge, rtd.bundleCacheSize, rtd.bundleCacheAge);


            DiagnosticEventCollector.ProfileEvents = rtd.profileEvents;
            AAConfig.AddCachedValue("ContentVersion", rtd.contentVersion);
            if (rtd.usePooledInstanceProvider)
                ResourceManager.InstanceProvider = new PooledInstanceProvider("PooledInstanceProvider", 10);
            else
                ResourceManager.InstanceProvider = new InstanceProvider();

            Addressables.ResourceLocators.Add(new ResourceLocationMap(rtd.catalogLocations));
            LoadContentCatalog(rtd, 0);
        }

        void LoadContentCatalog(ResourceManagerRuntimeData rtd, int index)
        {
            while (index < rtd.catalogLocations.Count && rtd.catalogLocations[index].m_internalId.EndsWith(".hash"))
                index++;
            IList<IResourceLocation> locations;
            if (Addressables.GetResourceLocations(rtd.catalogLocations[index].m_address, out locations))
            {
                ResourceManager.ProvideResource<ContentCatalogData>(locations[0]).Completed += (op) =>
                {
                    if (op.Result != null)
                    {
                        Addressables.ResourceLocators.Add(op.Result.CreateLocator());
                        Addressables.ResourceLocators.Add(new AssetReferenceLocator());
                        SetResult(true);
                        InvokeCompletionEvent();
                    }
                    else
                    {
                        if (index + 1 >= rtd.catalogLocations.Count)
                        {
                            Debug.Log("Addressables initialization failed.");
                            m_error = new Exception("Failed to load content catalog.");
                            SetResult(false);
                            Status = AsyncOperationStatus.Failed;
                            InvokeCompletionEvent();
                        }
                        else
                        {
                            LoadContentCatalog(rtd, index + 1);
                        }
                    }
                };
            }
        }

        private void AddResourceProviders(int assetCacheSize, float assetCacheAge, int bundleCacheSize, float bundleCacheAge)
        {

            if (!Application.isEditor)
            {
                ResourceManager.ResourceProviders.Insert(0, new CachedProvider(new BundledAssetProvider(), assetCacheSize, assetCacheAge));
                ResourceManager.ResourceProviders.Insert(0, new CachedProvider(new AssetBundleProvider(), bundleCacheSize, bundleCacheAge));
            }
            else
            {
#if UNITY_EDITOR
                switch (m_playMode)
                {
                    case ResourceManagerRuntimeData.EditorPlayMode.FastMode:
                        ResourceManager.ResourceProviders.Insert(0, new CachedProvider(new AssetDatabaseProvider(), assetCacheSize, assetCacheAge));
                        break;
                    case ResourceManagerRuntimeData.EditorPlayMode.VirtualMode:
                        VirtualAssetBundleManager.AddProviders(AAConfig.ExpandPathWithGlobalVariables, assetCacheSize, assetCacheAge, bundleCacheSize, bundleCacheAge);
                        break;
                    case ResourceManagerRuntimeData.EditorPlayMode.PackedMode:
                        {
                            ResourceManager.ResourceProviders.Insert(0, new CachedProvider(new BundledAssetProvider(), assetCacheSize, assetCacheAge));
                            ResourceManager.ResourceProviders.Insert(0, new CachedProvider(new AssetBundleProvider(), bundleCacheSize, bundleCacheAge));
                        }
                        break;
                }
#endif
            }
        }
    }
}