using System;
using System.Collections.Generic;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.AddressableAssets.ResourceProviders;
using UnityEngine.AddressableAssets.Utility;
using UnityEngine.Networking;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.Diagnostics;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.ResourceManagement.Util;

namespace UnityEngine.AddressableAssets.Initialization
{
    internal class InitializationOperation : AsyncOperationBase<IResourceLocator>
    {
        AsyncOperationHandle<ResourceManagerRuntimeData> m_rtdOp;
        string m_ProviderSuffix;
        IResourceLocator m_Result;
        AddressablesImpl m_Addressables;
        ResourceManagerDiagnostics m_Diagnostics;

        public InitializationOperation(AddressablesImpl aa)
        {
            m_Addressables = aa;
            m_Diagnostics = new ResourceManagerDiagnostics(aa.ResourceManager);
        }

        internal static AsyncOperationHandle<IResourceLocator> CreateInitializationOperation(AddressablesImpl aa, string playerSettingsLocation, string providerSuffix)
        {
            var jp = new JsonAssetProvider();
            jp.IgnoreFailures = true;
            aa.ResourceManager.ResourceProviders.Add(jp);
            var tdp = new TextDataProvider();
            tdp.IgnoreFailures = true;
            aa.ResourceManager.ResourceProviders.Add(tdp);
            aa.ResourceManager.ResourceProviders.Add(new ContentCatalogProvider(aa.ResourceManager));

            var runtimeDataLocation = new ResourceLocationBase("RuntimeData", playerSettingsLocation, typeof(JsonAssetProvider).FullName, typeof(ResourceManagerRuntimeData));

            var initOp = new InitializationOperation(aa);
            initOp.m_rtdOp = aa.ResourceManager.ProvideResource<ResourceManagerRuntimeData>(runtimeDataLocation);
            initOp.m_ProviderSuffix = providerSuffix;
            return aa.ResourceManager.StartOperation<IResourceLocator>(initOp, initOp.m_rtdOp);
        }

        protected override void Execute()
        {
            Addressables.LogFormat("Addressables - runtime data operation completed with status = {0}, result = {1}.", m_rtdOp.Status, m_rtdOp.Result);
            if (m_rtdOp.Result == null)
            {
                Addressables.LogWarningFormat("Addressables - Unable to load runtime data at location {0}.", m_rtdOp);
                Complete(m_Result, false, string.Format("Addressables - Unable to load runtime data at location {0}.", m_rtdOp));
                return;
            }
            var rtd = m_rtdOp.Result;
            m_Addressables.Release(m_rtdOp);
            if(rtd.CertificateHandlerType != null)
                m_Addressables.ResourceManager.CertificateHandlerInstance = Activator.CreateInstance(rtd.CertificateHandlerType) as CertificateHandler;

#if UNITY_EDITOR
            if (UnityEditor.EditorUserBuildSettings.activeBuildTarget.ToString() != rtd.BuildTarget)
                Addressables.LogErrorFormat("Addressables - runtime data was built with a different build target.  Expected {0}, but data was built with {1}.  Certain assets may not load correctly including shaders.  You can rebuild player content via the Addressable Assets window.", UnityEditor.EditorUserBuildSettings.activeBuildTarget, rtd.BuildTarget);
#endif
            if (!rtd.LogResourceManagerExceptions)
                ResourceManager.ExceptionHandler = null;

            if (!rtd.ProfileEvents)
            {
                m_Diagnostics.Dispose();
                m_Diagnostics = null;
            }

            //   DiagnosticEventCollector.ResourceManagerProfilerEventsEnabled = rtd.ProfileEvents;
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
            m_Addressables.ResourceLocators.Add(locMap);
            IList<IResourceLocation> catalogs;
            if (!locMap.Locate(ResourceManagerRuntimeData.kCatalogAddress, typeof(ContentCatalogData), out catalogs))
            {
                Addressables.LogWarningFormat("Addressables - Unable to find any catalog locations in the runtime data.");
                m_Addressables.ResourceLocators.Remove(locMap);
                Complete(m_Result, false, "Addressables - Unable to find any catalog locations in the runtime data.");
            }
            else
            {
                Addressables.LogFormat("Addressables - loading content catalogs, {0} found.", catalogs.Count);
                LoadContentCatalogInternal(catalogs, 0, locMap);
            }
        }


        static void LoadProvider(AddressablesImpl addressables, ObjectInitializationData providerData, string providerSuffix)
        {                
            //don't add providers that have the same id...
            var indexOfExistingProvider = -1;
            var newProviderId = string.IsNullOrEmpty(providerSuffix) ? providerData.Id : (providerData.Id + providerSuffix);
            for (int i = 0; i < addressables.ResourceManager.ResourceProviders.Count; i++)
            {
                var rp = addressables.ResourceManager.ResourceProviders[i];
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
                    addressables.ResourceManager.ResourceProviders.Add(provider);
                }
                else
                {
                    Addressables.LogFormat("Addressables - replacing provider {0} at index {1}.", provider, indexOfExistingProvider);
                    addressables.ResourceManager.ResourceProviders[indexOfExistingProvider] = provider;
                }
            }
            else
            {
                Addressables.LogWarningFormat("Addressables - Unable to load resource provider from {0}.", providerData);
            }

        }

        static AsyncOperationHandle<IResourceLocator> OnCatalogDataLoaded(AddressablesImpl addressables, AsyncOperationHandle<ContentCatalogData> op, string providerSuffix)
        {
            var data = op.Result;
            if (data == null)
            {
                return addressables.ResourceManager.CreateCompletedOperation<IResourceLocator>(null, new Exception("Failed to load content catalog.").Message);
            }
            else
            {
                if (data.ResourceProviderData != null)
                    foreach (var providerData in data.ResourceProviderData)
                        LoadProvider(addressables, providerData, providerSuffix);
                if (addressables.InstanceProvider == null)
                {
                    var prov = data.InstanceProviderData.CreateInstance<IInstanceProvider>();
                    if (prov != null)
                        addressables.InstanceProvider = prov;
                }
                if (addressables.SceneProvider == null)
                {
                    var prov = data.SceneProviderData.CreateInstance<ISceneProvider>();
                    if (prov != null)
                        addressables.SceneProvider = prov;
                }

                ResourceLocationMap locMap = data.CreateLocator(providerSuffix);

                addressables.ResourceLocators.Add(locMap);
                return addressables.ResourceManager.CreateCompletedOperation<IResourceLocator>(locMap, string.Empty);
            }
        }
        public static AsyncOperationHandle<IResourceLocator> LoadContentCatalog(AddressablesImpl addressables, IResourceLocation loc, string providerSuffix)
        {
            var loadOp = addressables.LoadAssetAsync<ContentCatalogData>(loc);
            var chainOp = addressables.ResourceManager.CreateChainOperation(loadOp, res => OnCatalogDataLoaded(addressables, res, providerSuffix));
            addressables.Release(loadOp);
            return chainOp;
        }

        public AsyncOperationHandle<IResourceLocator> LoadContentCatalog(IResourceLocation loc, string providerSuffix)
        {
            var loadOp = m_Addressables.LoadAssetAsync<ContentCatalogData>(loc);
            var chainOp = m_Addressables.ResourceManager.CreateChainOperation(loadOp, res => OnCatalogDataLoaded(m_Addressables, res, providerSuffix));
            m_Addressables.Release(loadOp);
            return chainOp;
        }

        //Attempts to load each catalog in order, stopping at first success. 
        void LoadContentCatalogInternal(IList<IResourceLocation> catalogs, int index, ResourceLocationMap locMap)
        {
            Addressables.LogFormat("Addressables - loading content catalog from {0}.", catalogs[index].InternalId);
            LoadContentCatalog(catalogs[index], m_ProviderSuffix).Completed += op =>
            {
                if (op.Result != null)
                {
                    m_Addressables.ResourceLocators.Remove(locMap);
                    m_Result = op.Result;
                    Complete(m_Result, true, string.Empty);
                    m_Addressables.Release(op);
                    Addressables.Log("Addressables - initialization complete.");
                }
                else
                {
                    Addressables.LogFormat("Addressables - failed to load content catalog from {0}.", op);
                    if (index + 1 >= catalogs.Count)
                    {
                        Addressables.LogWarningFormat("Addressables - initialization failed.", op);
                        m_Addressables.ResourceLocators.Remove(locMap);
                        Complete(m_Result, false, op.OperationException != null ? op.OperationException.Message : "LoadContentCatalogInternal");
                        m_Addressables.Release(op);
                    }
                    else
                    {
                        LoadContentCatalogInternal(catalogs, index + 1, locMap);
                        m_Addressables.Release(op);
                    }
                }
            };
        }
    }
}