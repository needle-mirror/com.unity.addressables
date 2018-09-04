using System;
using System.Collections.Generic;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.Diagnostics;

namespace UnityEngine.AddressableAssets
{
    class InitializationOperation : AsyncOperationBase<IResourceLocator>
    {
        ResourceManagerRuntimeData.EditorPlayMode m_playMode = ResourceManagerRuntimeData.EditorPlayMode.Invalid;
        public InitializationOperation(ResourceManagerRuntimeData.EditorPlayMode playMode)
        {
            Start(playMode, AddressablesRuntimeProperties.EvaluateString(ResourceManagerRuntimeData.GetPlayerSettingsLoadLocation(playMode)));
        }

        public InitializationOperation(string playerSettingsLocation)
        {
            Start(ResourceManagerRuntimeData.EditorPlayMode.Invalid, playerSettingsLocation);
        }

        void Start(ResourceManagerRuntimeData.EditorPlayMode playMode, string playerSettingsLocation)
        {
            m_playMode = playMode;
            ResourceManager.SceneProvider = new SceneProvider();
            ResourceManager.ResourceProviders.Add(new JsonAssetProvider());
            ResourceManager.ResourceProviders.Add(new TextDataProvider());
            ResourceManager.ResourceProviders.Add(new ContentCatalogProvider());
            ResourceManager.ResourceProviders.Add(new LegacyResourcesProvider());
            //this line should NOT be removed as it is adding a reference to Application.streamingAssetsPath so that it doesnt get stripped
            Addressables.LogFormat("Addressables - initializing system from {0}.", Addressables.RuntimePath);
            var runtimeDataLocation = new ResourceLocationBase("RuntimeData", playerSettingsLocation, typeof(JsonAssetProvider).FullName);
            Context = runtimeDataLocation;
            Key = playMode;
            ResourceManager.ProvideResource<ResourceManagerRuntimeData>(runtimeDataLocation).Completed += OnDataLoaded;
        }


        void OnDataLoaded(IAsyncOperation<ResourceManagerRuntimeData> op)
        {
            Addressables.LogFormat("Addressables - runtime data operation completed with status = {0}, result = {1}.", op.Status, op.Result);
            if (op.Result == null)
            {
                Addressables.LogWarningFormat("Addressables - Unable to load runtime data at location {0}.", (op.Context as IResourceLocation).InternalId);
                SetResult(null);
                InvokeCompletionEvent();
                return;
            }
            var rtd = op.Result;
            if (m_playMode != ResourceManagerRuntimeData.EditorPlayMode.Invalid)
            {
                Addressables.Log("Addressables - data loaded, adding content catalogs.");

                AddResourceProviders(rtd.AssetCacheSize, rtd.AssetCacheAge, rtd.BundleCacheSize, rtd.BundleCacheAge);
                DiagnosticEventCollector.ResourceManagerProfilerEventsEnabled = rtd.ProfileEvents;
                if (rtd.UsePooledInstanceProvider)
                    ResourceManager.InstanceProvider = new PooledInstanceProvider("PooledInstanceProvider", 10);
                else
                    ResourceManager.InstanceProvider = new InstanceProvider();
            }
            var locMap = new ResourceLocationMap(rtd.CatalogLocations);
            Addressables.ResourceLocators.Add(locMap);
            IList<IResourceLocation> catalogs;
            if (!locMap.Locate("catalogs", out catalogs))
            {
                Addressables.LogWarningFormat("Addressables - Unable to find any catalog locations in the runtime data.");
                SetResult(null);
                InvokeCompletionEvent();
            }
            else
            {
                LoadContentCatalog(catalogs, 0);
            }
        }

        void LoadContentCatalog(IList<IResourceLocation> catalogs, int index)
        {
            Addressables.LogFormat("Addressables - loading content catalog from {0}.", catalogs[index].InternalId);
            ResourceManager.ProvideResource<ContentCatalogData>(catalogs[index]).Completed += (op) =>
            {
                if (op.Result != null)
                {
                    var locator = op.Result.CreateLocator();
                    if (m_playMode != ResourceManagerRuntimeData.EditorPlayMode.Invalid)
                        Addressables.ResourceLocators.Add(new AssetReferenceLocator());
                    Addressables.ResourceLocators.Insert(0, locator);
                    SetResult(locator);
                    InvokeCompletionEvent();
                    Addressables.Log("Addressables - initialization complete.");
                }
                else
                {
                    Addressables.LogFormat("Addressables - failed to load content catalog from {0}.", (op.Context as IResourceLocation).InternalId);
                    if (index + 1 >= catalogs.Count)
                    {
                        Addressables.LogWarningFormat("Addressables - initialization failed.", (op.Context as IResourceLocation).InternalId);
                        m_error = op.OperationException;
                        SetResult(null);
                        Status = AsyncOperationStatus.Failed;
                        InvokeCompletionEvent();
                    }
                    else
                    {
                        LoadContentCatalog(catalogs, index + 1);
                    }
                }
            };
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
                        VirtualAssetBundleManager.AddProviders(AddressablesRuntimeProperties.EvaluateString, assetCacheSize, assetCacheAge, bundleCacheSize, bundleCacheAge);
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