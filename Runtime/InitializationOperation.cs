using System;
using System.Collections.Generic;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.Diagnostics;

namespace UnityEngine.AddressableAssets
{
    class InitializationOperation : AsyncOperationBase<IResourceLocator>
    {
        bool m_LoadAll;

        internal InitializationOperation(string playerSettingsLocation, bool loadAll)
        {
            m_LoadAll = loadAll;
            ResourceManager.ResourceProviders.Add(new JsonAssetProvider());
            ResourceManager.ResourceProviders.Add(new TextDataProvider());
            ResourceManager.ResourceProviders.Add(new ContentCatalogProvider());
            Addressables.ResourceLocators.Add(new AssetReferenceLocator());

            var runtimeDataLocation = new ResourceLocationBase("RuntimeData", playerSettingsLocation, typeof(JsonAssetProvider).FullName);
            Context = runtimeDataLocation;
            Key = playerSettingsLocation;
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
            if (!rtd.LogResourceManagerExceptions)
                ResourceManager.ExceptionHandler = null;
            if (m_LoadAll)
            {
                DiagnosticEventCollector.ResourceManagerProfilerEventsEnabled = rtd.ProfileEvents;

                Addressables.Log("Addressables - initializing resource providers.");
                foreach (var p in rtd.ResourceProviderData)
                {
                    var provider = p.CreateInstance<IResourceProvider>();
                    if (provider != null)
                    {
                        Addressables.LogFormat("Addressables - added provider {0}.", provider);
                        ResourceManager.ResourceProviders.Add(provider);
                    }
                    else
                    {
                        Addressables.LogWarningFormat("Addressables - Unable to load resource provider from {0}.", p);
                    }
                }
                ResourceManager.InstanceProvider = rtd.InstanceProviderData.CreateInstance<IInstanceProvider>();
                ResourceManager.SceneProvider = rtd.SceneProviderData.CreateInstance<ISceneProvider>();

                Addressables.Log("Addressables - loading initialization objects.");
                foreach (var i in rtd.InitializationObjects)
                {
                    if (i.ObjectType.Value == null)
                    {
                        Addressables.LogFormat("Invalid initialization object type {0}.", i.ObjectType);
                        continue;
                    }
                    try
                    {
                        var o = i.CreateInstance<object>();
                        Addressables.LogFormat("Initialization object {0} created instance {1}.", i, o);
                    }
                    catch (Exception ex)
                    {
                        Addressables.LogErrorFormat("Exception thrown during initialization of object {0}: {1}", i, ex.ToString());
                    }
                }

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
                Addressables.LogFormat("Addressables - loading content catalogs, {0} found.", catalogs.Count);
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
                        OperationException = op.OperationException;
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
    }
}