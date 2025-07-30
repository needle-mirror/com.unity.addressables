using System.Collections.Generic;
using System.Linq;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.AddressableAssets.ResourceProviders;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace UnityEngine.AddressableAssets
{
    class UpdateCatalogsOperation : AsyncOperationBase<List<IResourceLocator>>
    {
        AddressablesImpl m_Addressables;
        List<ResourceLocatorInfo> m_LocatorInfos;
        internal AsyncOperationHandle<IList<AsyncOperationHandle>> m_DepOp;
        AsyncOperationHandle<bool> m_CleanCacheOp;
        bool m_AutoCleanBundleCache = false;

        public UpdateCatalogsOperation(AddressablesImpl aa)
        {
            m_Addressables = aa;
        }

        public AsyncOperationHandle<List<IResourceLocator>> Start(IEnumerable<string> catalogIds, bool autoCleanBundleCache)
        {
            m_LocatorInfos = new List<ResourceLocatorInfo>();
            var locations = new List<IResourceLocation>();
            foreach (var c in catalogIds)
            {
                if (c == null)
                    continue;
                var loc = m_Addressables.GetLocatorInfo(c);
                locations.Add(loc.CatalogLocation);
                m_LocatorInfos.Add(loc);
            }

            if (locations.Count == 0)
                return m_Addressables.ResourceManager.CreateCompletedOperation(default(List<IResourceLocator>), "Content update not available.");

            ContentCatalogProvider ccp = m_Addressables.ResourceManager.ResourceProviders
                .FirstOrDefault(rp => rp.GetType() == typeof(ContentCatalogProvider)) as ContentCatalogProvider;
            if (ccp != null)
                ccp.DisableCatalogUpdateOnStart = false;

            m_DepOp = m_Addressables.ResourceManager.CreateGroupOperation<object>(locations);
            m_AutoCleanBundleCache = autoCleanBundleCache;
            return m_Addressables.ResourceManager.StartOperation(this, m_DepOp);
        }

        /// <inheritdoc />
        protected override bool InvokeWaitForCompletion()
        {
            if (IsDone)
                return true;
            if (m_DepOp.IsValid() && !m_DepOp.IsDone)
                m_DepOp.WaitForCompletion();

            m_RM?.Update(Time.unscaledDeltaTime);
            if (!HasExecuted)
                InvokeExecute();

            if (m_CleanCacheOp.IsValid() && !m_CleanCacheOp.IsDone)
                m_CleanCacheOp.WaitForCompletion();

            m_Addressables.ResourceManager.Update(Time.unscaledDeltaTime);
            return IsDone;
        }

        protected override void Destroy()
        {
            m_DepOp.Release();
        }

        /// <inheritdoc />
        public override void GetDependencies(List<AsyncOperationHandle> dependencies)
        {
            dependencies.Add(m_DepOp);
        }

        protected override void Execute()
        {
            var catalogs = new List<IResourceLocator>(m_DepOp.Result.Count);
            for (int i = 0; i < m_DepOp.Result.Count; i++)
            {
                var locator = m_DepOp.Result[i].Result as IResourceLocator;
                string localHash = null;
                IResourceLocation remoteLocation = null;
                if (locator == null)
                {
                    var catData = m_DepOp.Result[i].Result as ContentCatalogData;
                    locator = catData.CreateCustomLocator(catData.location.PrimaryKey, null);
                    localHash = catData.LocalHash;
                    remoteLocation = catData.location;
                }

                m_LocatorInfos[i].UpdateContent(locator, localHash, remoteLocation);
                catalogs.Add(m_LocatorInfos[i].Locator);
            }

            if (!m_AutoCleanBundleCache)
            {
                if (m_DepOp.Status == AsyncOperationStatus.Succeeded)
                    Complete(catalogs, true, null);
                else if (m_DepOp.Status == AsyncOperationStatus.Failed)
                    Complete(catalogs, false, $"Cannot update catalogs. Failed to load catalog: {m_DepOp.OperationException.Message}");
                else
                {
                    var errorMessage = "Cannot update catalogs. Catalog loading operation is still in progress when it should already be completed. ";
                    errorMessage += (m_DepOp.OperationException != null) ? m_DepOp.OperationException.Message : "";
                    Complete(catalogs, false, errorMessage);
                }
            }
            else
            {
                m_CleanCacheOp = m_Addressables.CleanBundleCache(m_DepOp, false);
                OnCleanCacheCompleted(m_CleanCacheOp, catalogs);
            }
        }

        void OnCleanCacheCompleted(AsyncOperationHandle<bool> handle, List<IResourceLocator> catalogs)
        {
            handle.Completed += (obj) =>
            {
                bool success = obj.Status == AsyncOperationStatus.Succeeded;
                Complete(catalogs, success, success ? null : $"{obj.DebugName}, status={obj.Status}, result={obj.Result} catalogs updated, but failed to clean bundle cache.");
            };
        }
    }
}
