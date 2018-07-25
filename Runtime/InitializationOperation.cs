using System;
using System.Collections.Generic;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.Diagnostics;

namespace UnityEngine.AddressableAssets
{
    class InitializationOperation : AsyncOperationBase<bool>
    {
        ResourceManagerRuntimeData.EditorPlayMode m_playMode = ResourceManagerRuntimeData.EditorPlayMode.Invalid;
        public InitializationOperation(ResourceManagerRuntimeData.EditorPlayMode playMode)
        {
            Start(playMode, AAConfig.ExpandPathWithGlobalVariables(ResourceManagerRuntimeData.GetPlayerSettingsLoadLocation(playMode)));
        }

        public InitializationOperation(string playerSettingsLocation)
        {
            Start(ResourceManagerRuntimeData.EditorPlayMode.Invalid, playerSettingsLocation);
        }

        public void Start(ResourceManagerRuntimeData.EditorPlayMode playMode, string playerSettingsLocation)
        {
            m_playMode = playMode;
            ResourceManager.SceneProvider = new SceneProvider();
            ResourceManager.ResourceProviders.Add(new JsonAssetProvider());
            ResourceManager.ResourceProviders.Add(new TextDataProvider());
            ResourceManager.ResourceProviders.Add(new ContentCatalogProvider());
            //this line should NOT be removed as it is adding a reference to Application.streamingAssetsPath so that it doesnt get stripped
            Debug.LogFormat("Initializing Addressables system from {0}.", Addressables.RuntimePath);
            var runtimeDataLocation = new ResourceLocationBase("RuntimeData", playerSettingsLocation, typeof(JsonAssetProvider).FullName);
            Context = runtimeDataLocation;
            Key = playMode;
            ResourceManager.ProvideResource<ResourceManagerRuntimeData>(runtimeDataLocation).Completed += OnDataLoaded;
        }


        void OnDataLoaded(IAsyncOperation<ResourceManagerRuntimeData> op)
        {
            if (op.Result == null)
            {
                Debug.LogWarningFormat("Unable to load runtime data at location {0}.", (op.Context as IResourceLocation).InternalId);
                return;
            }
            var rtd = op.Result;
            if (m_playMode != ResourceManagerRuntimeData.EditorPlayMode.Invalid)
            {
                AddResourceProviders(rtd.AssetCacheSize, rtd.AssetCacheAge, rtd.BundleCacheSize, rtd.BundleCacheAge);
                DiagnosticEventCollector.ProfileEvents = rtd.ProfileEvents;
                AAConfig.AddCachedValue("ContentVersion", rtd.ContentVersion);
                if (rtd.UsePooledInstanceProvider)
                    ResourceManager.InstanceProvider = new PooledInstanceProvider("PooledInstanceProvider", 10);
                else
                    ResourceManager.InstanceProvider = new InstanceProvider();
            }

            Addressables.ResourceLocators.Add(new ResourceLocationMap(rtd.CatalogLocations));
            LoadContentCatalog(rtd, 0);
        }

        void LoadContentCatalog(ResourceManagerRuntimeData rtd, int index)
        {
            while (index < rtd.CatalogLocations.Count && rtd.CatalogLocations[index].InternalId.EndsWith(".hash"))
                index++;
            IList<IResourceLocation> locations;
            if (Addressables.GetResourceLocations(rtd.CatalogLocations[index].Address, out locations))
            {
                ResourceManager.ProvideResource<ContentCatalogData>(locations[0]).Completed += (op) =>
                {
                    if (op.Result != null)
                    {
                        Addressables.ResourceLocators.Add(op.Result.CreateLocator());
                        if (m_playMode != ResourceManagerRuntimeData.EditorPlayMode.Invalid)
                            Addressables.ResourceLocators.Add(new AssetReferenceLocator());
                        SetResult(true);
                        InvokeCompletionEvent();
                    }
                    else
                    {
                        if (index + 1 >= rtd.CatalogLocations.Count)
                        {
                            Debug.LogWarningFormat("Addressables initialization failed.", (op.Context as IResourceLocation).InternalId);
                            m_error = op.OperationException;
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
                ResourceManager.ResourceProviders.Insert(0, new CachedProvider(new BundledAssetProvider(), 0, 0));
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
                            ResourceManager.ResourceProviders.Insert(0, new CachedProvider(new BundledAssetProvider(), 0, 0));
                            ResourceManager.ResourceProviders.Insert(0, new CachedProvider(new AssetBundleProvider(), bundleCacheSize, bundleCacheAge));
                        }
                        break;
                }
#endif
            }
        }
    }
}