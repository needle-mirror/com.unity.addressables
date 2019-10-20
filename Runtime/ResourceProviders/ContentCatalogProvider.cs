using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.ResourceManagement.Util;

namespace UnityEngine.AddressableAssets.ResourceProviders
{
    /// <summary>
    /// Provider for content catalogs.  This provider makes use of a hash file to determine if a newer version of the catalog needs to be downloaded.
    /// </summary>
    [DisplayName("Content Catalog Provider")]
    public class ContentCatalogProvider : ResourceProviderBase
    {
        /// <summary>
        /// An enum used to specify which entry in the catalog dependencies should hold each hash item.
        ///  The Remote should point to the hash on the server.  The Cache should point to the
        ///  local cache copy of the remote data. 
        /// </summary>
        public enum DependencyHashIndex
        {
            Remote = 0,
            Cache,
            Count
        }
        ResourceManager m_RM;
        /// <summary>
        /// Constructor for this provider.
        /// </summary>
        /// <param name="resourceManagerInstance">The resource manager to use.</param>
        public ContentCatalogProvider(ResourceManager resourceManagerInstance )
        {
            m_RM = resourceManagerInstance;
            m_BehaviourFlags = ProviderBehaviourFlags.CanProvideWithFailedDependencies;
        }
        internal class InternalOp
        {
         //   int m_StartFrame;
            string m_LocalDataPath;
            string m_RemoteHashValue;
            string m_LocalHashValue;
            ProvideHandle m_ProviderInterface;
            ResourceManager m_RM;

            public void Start(ProvideHandle providerInterface, ResourceManager rm)
            {
                m_RM = rm;
                m_ProviderInterface = providerInterface;
                m_LocalDataPath = null;
                m_RemoteHashValue = null;
         
                List<object> deps = new List<object>(); // TODO: garbage. need to pass actual count and reuse the list
                m_ProviderInterface.GetDependencies(deps);
                string idToLoad = DetermineIdToLoad(m_ProviderInterface.Location, deps);

                Addressables.LogFormat("Addressables - Using content catalog from {0}.", idToLoad);
                rm.ProvideResource<ContentCatalogData>(new ResourceLocationBase(idToLoad, idToLoad, typeof(JsonAssetProvider).FullName, typeof(ContentCatalogData))).Completed += OnCatalogLoaded;
            }

            internal string DetermineIdToLoad(IResourceLocation location, IList<object> dependencyObjects)
            {
                //default to load actual local source catalog
                string idToLoad = location.InternalId;
                if (dependencyObjects != null &&
                    location.Dependencies != null &&
                    dependencyObjects.Count == (int)DependencyHashIndex.Count &&
                    location.Dependencies.Count == (int)DependencyHashIndex.Count)
                {
                    var remoteHash = dependencyObjects[(int)DependencyHashIndex.Remote] as string;
                    m_LocalHashValue = dependencyObjects[(int)DependencyHashIndex.Cache] as string;
                    Addressables.LogFormat("Addressables - ContentCatalogProvider CachedHash = {0}, RemoteHash = {1}.", m_LocalHashValue, remoteHash);

                    if (string.IsNullOrEmpty(remoteHash)) //offline
                    {
                        if (!string.IsNullOrEmpty(m_LocalHashValue)) //cache exists
                            idToLoad = location.Dependencies[(int)DependencyHashIndex.Cache].InternalId.Replace(".hash", ".json");
                    }
                    else //online
                    {
                        if (remoteHash == m_LocalHashValue) //cache of remote is good
                        {
                            idToLoad = location.Dependencies[(int)DependencyHashIndex.Cache].InternalId.Replace(".hash", ".json");
                        }
                        else //remote is different than cache, or no cache
                        {
                            idToLoad = location.Dependencies[(int)DependencyHashIndex.Remote].InternalId.Replace(".hash", ".json");
                            m_LocalDataPath = location.Dependencies[(int)DependencyHashIndex.Cache].InternalId.Replace(".hash", ".json");
                            m_RemoteHashValue = remoteHash;
                        }
                    }
                }

                return idToLoad;
            }

            void OnCatalogLoaded(AsyncOperationHandle<ContentCatalogData> op)
            {
                var ccd = op.Result;
                m_RM.Release(op);
                Addressables.LogFormat("Addressables - Content catalog load result = {0}.", ccd);
                if (ccd != null)
                {
                    ccd.location = m_ProviderInterface.Location;
                    ccd.localHash = m_LocalHashValue;
                    if (!string.IsNullOrEmpty(m_RemoteHashValue) && !string.IsNullOrEmpty(m_LocalDataPath))
                    {
                        var dir = Path.GetDirectoryName(m_LocalDataPath);
                        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                            Directory.CreateDirectory(dir);
                        var localCachePath = m_LocalDataPath;
                        Addressables.LogFormat("Addressables - Saving cached content catalog to {0}.", localCachePath);
                        File.WriteAllText(localCachePath, JsonUtility.ToJson(ccd));
                        File.WriteAllText(localCachePath.Replace(".json", ".hash"), m_RemoteHashValue);
                        ccd.localHash = m_RemoteHashValue;
                    }
                }
                m_ProviderInterface.Complete(ccd, ccd != null, null);
            }
        }

        ///<inheritdoc/>
        public override void Provide(ProvideHandle providerInterface)
        {
            new InternalOp().Start(providerInterface, m_RM);
        }
    }
}