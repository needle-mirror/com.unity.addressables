using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;

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

        public bool DisableCatalogUpdateOnStart = false;
        public bool IsLocalCatalogInBundle = false;

        internal Dictionary<IResourceLocation, InternalOp> m_LocationToCatalogLoadOpMap = new Dictionary<IResourceLocation, InternalOp>();
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

        public override void Release(IResourceLocation location, object obj)
        {
            if (m_LocationToCatalogLoadOpMap.ContainsKey(location))
            {
                m_LocationToCatalogLoadOpMap[location].Release();
                m_LocationToCatalogLoadOpMap.Remove(location);
            }
            base.Release(location, obj);
        }

        internal class InternalOp
        {
         //   int m_StartFrame;
            string m_LocalDataPath;
            string m_RemoteHashValue;
            string m_LocalHashValue;
            ProvideHandle m_ProviderInterface;
            internal ContentCatalogData m_ContentCatalogData;

            public void Start(ProvideHandle providerInterface, bool disableCatalogUpdateOnStart, bool isLocalCatalogInBundle)
            {
                m_ProviderInterface = providerInterface;
                m_LocalDataPath = null;
                m_RemoteHashValue = null;
         
                List<object> deps = new List<object>(); // TODO: garbage. need to pass actual count and reuse the list
                m_ProviderInterface.GetDependencies(deps);
                string idToLoad = DetermineIdToLoad(m_ProviderInterface.Location, deps, disableCatalogUpdateOnStart);

                Addressables.LogFormat("Addressables - Using content catalog from {0}.", idToLoad);

                bool isLocalCatalog = idToLoad.Equals(GetTransformedInternalId(m_ProviderInterface.Location));
                if (isLocalCatalogInBundle && isLocalCatalog)
                {
                    try
                    {
                        var bc = new BundledCatalog(idToLoad);
                        bc.OnLoaded += ccd =>
                        {
                            m_ContentCatalogData = ccd;
                            OnCatalogLoaded(ccd);
                        };
                        bc.LoadCatalogFromBundleAsync();
                    }
                    catch (Exception ex)
                    {
                        m_ProviderInterface.Complete<ContentCatalogData>(null, false, ex);
                    }
                }
                else
                {
                    IResourceLocation location = new ResourceLocationBase(idToLoad, idToLoad, typeof(JsonAssetProvider).FullName, typeof(ContentCatalogData));
                    providerInterface.ResourceManager.ProvideResource<ContentCatalogData>(location).Completed += op =>
                    {
                        m_ContentCatalogData = op.Result;
                        m_ProviderInterface.ResourceManager.Release(op);
                        OnCatalogLoaded(m_ContentCatalogData);
                    };
                }
            }

            public void Release()
            {
                m_ContentCatalogData?.CleanData();
            }

            internal class BundledCatalog
            {
                private readonly string m_BundlePath;
                private bool m_OpInProgress;
                private AssetBundleCreateRequest m_LoadBundleRequest;
                internal AssetBundle m_CatalogAssetBundle;
                private AssetBundleRequest m_LoadTextAssetRequest;
                private ContentCatalogData m_CatalogData;

                public event Action<ContentCatalogData> OnLoaded;

                public bool OpInProgress => m_OpInProgress;
                public bool OpIsSuccess => !m_OpInProgress && m_CatalogData != null;

                public BundledCatalog(string bundlePath)
                {
                    if (string.IsNullOrEmpty(bundlePath))
                    {
                        throw new ArgumentNullException(nameof(bundlePath), "Catalog bundle path is null.");
                    }
                    else if(!bundlePath.EndsWith(".bundle"))
                    {
                        throw new ArgumentException("You must supply a valid bundle file path.");
                    }

                    m_BundlePath = bundlePath;
                }

                ~BundledCatalog()
                {
                    Unload();
                }

                private void Unload()
                {
                    m_CatalogAssetBundle?.Unload(true);
                    m_CatalogAssetBundle = null;
                }

                public void LoadCatalogFromBundleAsync()
                {
                    //Debug.Log($"LoadCatalogFromBundleAsync frame : {Time.frameCount}");
                    if (m_OpInProgress)
                    {
                        Addressables.LogError($"Operation in progress : A catalog is already being loaded. Please wait for the operation to complete.");
                        return;
                    }

                    m_OpInProgress = true;
                    m_LoadBundleRequest = AssetBundle.LoadFromFileAsync(m_BundlePath);
                    m_LoadBundleRequest.completed += loadOp =>
                    {
                        //Debug.Log($"m_LoadBundleRequest.completed frame : {Time.frameCount}");
                        if (loadOp is AssetBundleCreateRequest createRequest && createRequest.assetBundle != null)
                        {
                            m_CatalogAssetBundle = createRequest.assetBundle;
                            m_LoadTextAssetRequest = m_CatalogAssetBundle.LoadAllAssetsAsync<TextAsset>();
                            m_LoadTextAssetRequest.completed += op =>
                            {
                                //Debug.Log($"m_LoadTextAssetRequest.completed frame : {Time.frameCount}");
                                if (op is AssetBundleRequest loadRequest 
                                    && loadRequest.asset is TextAsset textAsset 
                                    && textAsset.text != null)
                                {
                                    m_CatalogData = JsonUtility.FromJson<ContentCatalogData>(textAsset.text);
                                    OnLoaded?.Invoke(m_CatalogData);
                                }
                                else
                                {
                                    Addressables.LogError($"No catalog text assets where found in bundle {m_BundlePath}");
                                }
                                Unload();
                                m_OpInProgress = false;
                            };
                        }
                        else
                        {
                            Addressables.LogError($"Unable to load dependent bundle from location : {m_BundlePath}");
                            m_OpInProgress = false;
                        }
                    };
                }
            }

            string GetTransformedInternalId(IResourceLocation loc)
            {
                if (m_ProviderInterface.ResourceManager == null)
                    return loc.InternalId;
                return m_ProviderInterface.ResourceManager.TransformInternalId(loc);
            }

            internal string DetermineIdToLoad(IResourceLocation location, IList<object> dependencyObjects, bool disableCatalogUpdateOnStart = false)
            {
                //default to load actual local source catalog
                string idToLoad = GetTransformedInternalId(location);
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
                            idToLoad = GetTransformedInternalId(location.Dependencies[(int)DependencyHashIndex.Cache]).Replace(".hash", ".json");
                    }
                    else //online
                    {
                        if (remoteHash == m_LocalHashValue) //cache of remote is good
                        {
                            idToLoad = GetTransformedInternalId(location.Dependencies[(int)DependencyHashIndex.Cache]).Replace(".hash", ".json");
                        }
                        else //remote is different than cache, or no cache
                        {
                            if (disableCatalogUpdateOnStart)
                                m_LocalHashValue = Hash128.Compute(idToLoad).ToString();
                            else
                            {
                                idToLoad = GetTransformedInternalId(location.Dependencies[(int) DependencyHashIndex.Remote]).Replace(".hash", ".json");
                                m_LocalDataPath = GetTransformedInternalId(location.Dependencies[(int) DependencyHashIndex.Cache]).Replace(".hash", ".json");
                            }

                            m_RemoteHashValue = remoteHash;
                        }
                    }
                }
                return idToLoad;
            }

            private void OnCatalogLoaded(ContentCatalogData ccd)
            {
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
                m_ProviderInterface.Complete(ccd, ccd != null, ccd==null?new Exception($"Unable to load ContentCatalogData  from location {m_ProviderInterface.Location}."):null);
            }
        }

        ///<inheritdoc/>
        public override void Provide(ProvideHandle providerInterface)
        {
            if(!m_LocationToCatalogLoadOpMap.ContainsKey(providerInterface.Location))
                m_LocationToCatalogLoadOpMap.Add(providerInterface.Location, new InternalOp());
            m_LocationToCatalogLoadOpMap[providerInterface.Location].Start(providerInterface, DisableCatalogUpdateOnStart, IsLocalCatalogInBundle);
        }
    }
}