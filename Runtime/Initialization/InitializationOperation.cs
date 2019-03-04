using System;
using System.Collections.Generic;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.AddressableAssets.ResourceProviders;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.Diagnostics;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.ResourceManagement.Util;

namespace UnityEngine.AddressableAssets.Initialization
{
    /// <summary>
    /// Operation to set up the Addressables system.
    /// </summary>
    public class InitializationOperation : AsyncOperationBase<IResourceLocator>
    {
        public static string CatalogAddress = "AddressablesMainContentCatalog";
        
        string m_providerSuffix;
        /// <summary>
        ///  Operation to set up the Addressables system.
        /// </summary>
        /// <param name="playerSettingsLocation">The path to load initialization data from.</param>
        /// <param name="providerSuffix">This value, if not null or empty, will be appended to all provider ids loaded from this data.</param>
        public InitializationOperation(string playerSettingsLocation,string providerSuffix)
        {
            m_providerSuffix = providerSuffix;
            var jp = new JsonAssetProvider();
            jp.IgnoreFailures = true;
            Addressables.ResourceManager.ResourceProviders.Add(jp);
            var tdp = new TextDataProvider();
            tdp.IgnoreFailures = true;
            Addressables.ResourceManager.ResourceProviders.Add(tdp);
            Addressables.ResourceManager.ResourceProviders.Add(new ContentCatalogProvider());
            Addressables.ResourceLocators.Add(new AssetReferenceLocator());

            var runtimeDataLocation = new ResourceLocationBase("RuntimeData", playerSettingsLocation, typeof(JsonAssetProvider).FullName);
            Context = runtimeDataLocation;
            Key = playerSettingsLocation;
            Addressables.ResourceManager.ProvideResource<ResourceManagerRuntimeData>(runtimeDataLocation).Completed += OnDataLoaded;
        }

        void OnDataLoaded(IAsyncOperation<ResourceManagerRuntimeData> op)
        {
            Addressables.LogFormat("Addressables - runtime data operation completed with status = {0}, result = {1}.", op.Status, op.Result);
            if (op.Result == null)
            {
                Addressables.LogWarningFormat("Addressables - Unable to load runtime data at location {0}.", ((IResourceLocation)op.Context).InternalId);
                SetResult(null);
                InvokeCompletionEvent();
                return;
            }
            var rtd = op.Result;

#if UNITY_EDITOR
            if (UnityEditor.EditorUserBuildSettings.activeBuildTarget.ToString() != rtd.BuildTarget)
                Addressables.LogErrorFormat("Addressables - runtime data was built with a different build target.  Expected {0}, but data was built with {1}.  Certain assets may not load correctly including shaders.  You can rebuild player content via the Addressable Assets window.", UnityEditor.EditorUserBuildSettings.activeBuildTarget, rtd.BuildTarget);
#endif
            if (!rtd.LogResourceManagerExceptions)
                ResourceManager.ExceptionHandler = null;
            DiagnosticEventCollector.ResourceManagerProfilerEventsEnabled = rtd.ProfileEvents;
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

            var locMap = new ResourceLocationMap(rtd.CatalogLocations);
            Addressables.ResourceLocators.Add(locMap);
            IList<IResourceLocation> catalogs;
            if (!locMap.Locate(CatalogAddress, out catalogs))
            {
                Addressables.LogWarningFormat("Addressables - Unable to find any catalog locations in the runtime data.");
                Addressables.ResourceLocators.Remove(locMap);
                SetResult(null);
                InvokeCompletionEvent();
            }
            else
            {
                Addressables.LogFormat("Addressables - loading content catalogs, {0} found.", catalogs.Count);
                LoadContentCatalog(catalogs, 0, locMap);
            }
        }


        static void LoadProvider(ObjectInitializationData providerData, string providerSuffix)
        {                
            //don't add providers that have the same id...
            var indexOfExistingProvider = -1;
            var newProviderId = string.IsNullOrEmpty(providerSuffix) ? providerData.Id : (providerData.Id + providerSuffix);
            for (int i = 0; i < Addressables.ResourceManager.ResourceProviders.Count; i++)
            {
                var rp = Addressables.ResourceManager.ResourceProviders[i];
                if (rp.ProviderId == newProviderId)
                {
                    indexOfExistingProvider = i;
                    break;
                }
            }

            //if not re-initializing, just use the old provider
            if (indexOfExistingProvider >= 0 && string.IsNullOrEmpty(providerSuffix))
                return;

            var provider = providerData.CreateInstance<IResourceProvider>(newProviderId);
            if (provider != null)
            {
                if (indexOfExistingProvider < 0 || !string.IsNullOrEmpty(providerSuffix))
                {
                    Addressables.LogFormat("Addressables - added provider {0} with id {1}.", provider, provider.ProviderId);
                    Addressables.ResourceManager.ResourceProviders.Add(provider);
                }
                else
                {
                    Addressables.LogFormat("Addressables - replacing provider {0} at index {1}.", provider, indexOfExistingProvider);
                    Addressables.ResourceManager.ResourceProviders[indexOfExistingProvider] = provider;
                }
            }
            else
            {
                Addressables.LogWarningFormat("Addressables - Unable to load resource provider from {0}.", providerData);
            }

        }

        static IAsyncOperation<IResourceLocator> OnCatalogDataLoaded(ContentCatalogData data, string providerSuffix)
        {
            if (data == null)
            {
                return new CompletedOperation<IResourceLocator>().Start(null, null, null, new Exception("Failed to load content catalog."));
            }
            else
            {
                if (data.ResourceProviderData != null)
                    foreach (var providerData in data.ResourceProviderData)
                        LoadProvider(providerData, providerSuffix);
                if (Addressables.ResourceManager.InstanceProvider == null)
                {
                    var prov = data.InstanceProviderData.CreateInstance<IInstanceProvider>();
                    if (prov != null)
                        Addressables.ResourceManager.InstanceProvider = prov;
                }
                if (Addressables.ResourceManager.SceneProvider == null)
                {
                    var prov = data.SceneProviderData.CreateInstance<ISceneProvider>();
                    if (prov != null)
                        Addressables.ResourceManager.SceneProvider = prov;
                }

                return new CompletedOperation<IResourceLocator>().Start(null, null, data.CreateLocator(providerSuffix));
            }
        }

        public static IAsyncOperation<IResourceLocator> LoadContentCatalog(IResourceLocation loc, string providerSuffix)
        {
            return AsyncOperationCache.Instance.Acquire<ChainOperation<IResourceLocator, ContentCatalogData>>().Start(loc, loc, Addressables.LoadAsset<ContentCatalogData>(loc), res => OnCatalogDataLoaded(res.Result, providerSuffix));
        }

        //Attempts to load each catalog in order, stopping at first success. 
        void LoadContentCatalog(IList<IResourceLocation> catalogs, int index, ResourceLocationMap locMap)
        {
            Addressables.LogFormat("Addressables - loading content catalog from {0}.", catalogs[index].InternalId);
            LoadContentCatalog(catalogs[index], m_providerSuffix).Completed += op =>
            {
                if (op.Result != null)
                {
                     Addressables.ResourceLocators.Remove(locMap);
                    Addressables.ResourceLocators.Insert(0, op.Result);
                    SetResult(op.Result); 
                    InvokeCompletionEvent();
                    Addressables.Log("Addressables - initialization complete.");
                }
                else
                {
                    Addressables.LogFormat("Addressables - failed to load content catalog from {0}.", ((IResourceLocation)op.Context).InternalId);
                    if (index + 1 >= catalogs.Count)
                    {
                        Addressables.LogWarningFormat("Addressables - initialization failed.", ((IResourceLocation)op.Context).InternalId);
                        Addressables.ResourceLocators.Remove(locMap);
                        m_Error = op.OperationException;
                        SetResult(null);
                        Status = AsyncOperationStatus.Failed;
                        InvokeCompletionEvent();
                    }
                    else
                    {
                        LoadContentCatalog(catalogs, index + 1, locMap);
                    }
                }
            };
        }
    }
}