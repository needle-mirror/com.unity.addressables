using System.Collections.Generic;
using System.Linq;
using UnityEngine.AddressableAssets.ResourceProviders;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace UnityEngine.AddressableAssets
{
    class CheckCatalogsOperation : AsyncOperationBase<List<string>>
    {
        AddressablesImpl m_Addressables;
        List<string> m_LocalHashes;
        List<AddressablesImpl.ResourceLocatorInfo> m_LocatorInfos;
        AsyncOperationHandle<IList<AsyncOperationHandle>> m_DepOp;
        public CheckCatalogsOperation(AddressablesImpl aa)
        {
            m_Addressables = aa;
        }

        public AsyncOperationHandle<List<string>> Start(List<AddressablesImpl.ResourceLocatorInfo> locatorInfos)
        {
            m_LocatorInfos = new List<AddressablesImpl.ResourceLocatorInfo>(locatorInfos.Count);
            m_LocalHashes = new List<string>(locatorInfos.Count);
            var locations = new List<IResourceLocation>(locatorInfos.Count);
            foreach (var rl in locatorInfos)
            {
                if (rl.CanUpdateContent)
                {
                    locations.Add(rl.HashLocation);
                    m_LocalHashes.Add(rl.LocalHash);
                    m_LocatorInfos.Add(rl);
                }
            }

            ContentCatalogProvider ccp = m_Addressables.ResourceManager.ResourceProviders
                .FirstOrDefault(rp => rp.GetType() == typeof(ContentCatalogProvider)) as ContentCatalogProvider;
            if(ccp != null)
                ccp.DisableCatalogUpdateOnStart = false;

            m_DepOp = m_Addressables.ResourceManager.CreateGroupOperation<string>(locations);
            return m_Addressables.ResourceManager.StartOperation(this, m_DepOp);
        }

        protected override void Destroy()
        {
            m_Addressables.Release(m_DepOp);
        }

        protected override void GetDependencies(List<AsyncOperationHandle> dependencies)
        {
            dependencies.Add(m_DepOp);
        }

        protected override void Execute()
        {
            var result = new List<string>();
            for (int i = 0; i < m_DepOp.Result.Count; i++)
            {
                var remHashOp = m_DepOp.Result[i];
                var remoteHash = remHashOp.Result as string;
                if (!string.IsNullOrEmpty(remoteHash) && remoteHash != m_LocalHashes[i])
                {
                    result.Add(m_LocatorInfos[i].Locator.LocatorId);
                    m_LocatorInfos[i].ContentUpdateAvailable = true;
                }
            }
            Complete(result, true, null);
        }
    }

}