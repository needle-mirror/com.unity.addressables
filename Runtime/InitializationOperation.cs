using System;
using System.Collections.Generic;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.Diagnostics;

namespace UnityEngine.AddressableAssets
{
    class InitializationOperation : AsyncOperationBase<bool>
    {
        public InitializationOperation()
        {
            ResourceManager.SceneProvider = new SceneProvider();
            ResourceManager.ResourceProviders.Add(new JsonAssetProvider());
            ResourceManager.ResourceProviders.Add(new TextDataProvider());
            ResourceManager.ResourceProviders.Add(new ContentCatalogProvider());

            var playerSettingsLocation = AAConfig.ExpandPathWithGlobalVariables(ResourceManagerRuntimeData.PlayerSettingsLoadLocation);
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

            Addressables.ResourceLocators.Add(rtd.catalogLocations.Create());
            LoadContentCatalog(rtd, 0);
        }

        void LoadContentCatalog(ResourceManagerRuntimeData rtd, int index)
        {
            while (index < rtd.catalogLocations.locations.Count && !rtd.catalogLocations.locations[index].m_isLoadable)
                index++;
            IList<IResourceLocation> locations;
            if (Addressables.GetResourceLocations(rtd.catalogLocations.locations[index].m_address, out locations))
            {
                ResourceManager.ProvideResource<ResourceLocationList>(locations[0]).Completed += (op) =>
                {
                    if (op.Result != null)
                    {
                        Addressables.ResourceLocators.Clear();
                        Addressables.ResourceLocators.Add(op.Result.Create());
                        Addressables.ResourceLocators.Add(new AssetReferenceLocator());
                        SetResult(true);
                        InvokeCompletionEvent();
                    }
                    else
                    {
                        if (index + 1 >= rtd.catalogLocations.locations.Count)
                        {
                            Debug.LogError("Failed to load content catalog.");
                            SetResult(false);
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
                ResourceManager.ResourceProviders.Insert(0, new CachedProvider(new LocalAssetBundleProvider(), bundleCacheSize, bundleCacheAge));
                ResourceManager.ResourceProviders.Insert(0, new CachedProvider(new RemoteAssetBundleProvider(), bundleCacheSize, bundleCacheAge));
            }
            else
            {
#if UNITY_EDITOR
                var playMode = (ResourceManagerRuntimeData.EditorPlayMode)PlayerPrefs.GetInt("AddressablesPlayMode", 0);
                switch (playMode)
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
                            ResourceManager.ResourceProviders.Insert(0, new CachedProvider(new LocalAssetBundleProvider(), bundleCacheSize, bundleCacheAge));
                            ResourceManager.ResourceProviders.Insert(0, new CachedProvider(new RemoteAssetBundleProvider(), bundleCacheSize, bundleCacheAge));
                        }
                        break;
                }
#endif
            }
        }
    }
}