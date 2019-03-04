using System;
using System.Collections.Generic;
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
        
        public ContentCatalogProvider()
        {
            m_BehaviourFlags = ProviderBehaviourFlags.CanProvideWithFailedDependencies;
        }
        internal class InternalOp<TObject> : AsyncOperationBase<TObject> where TObject : class
        {
            int m_StartFrame;
            string m_LocalDataPath;
            string m_HashValue;

            public IAsyncOperation<TObject> Start(IResourceLocation location, IList<object> deps)
            {
                Validate();
                m_LocalDataPath = null;
                m_HashValue = null;
                m_StartFrame = Time.frameCount;
                m_Result = null;
                Context = location;

                string idToLoad = DetermineIdToLoad(location, deps);
                
                Addressables.LogFormat("Addressables - Using content catalog from {0}.", idToLoad);
                Addressables.ResourceManager.ProvideResource<ContentCatalogData>(new ResourceLocationBase(idToLoad, idToLoad, typeof(JsonAssetProvider).FullName)).Completed += OnCatalogLoaded;
                
                return this;
            }

            internal string DetermineIdToLoad(IResourceLocation location, IList<object> dependencyObjects)
            {
                //default to load actual local source catalog
                string idToLoad = location.InternalId;
                if (dependencyObjects != null && 
                    location.Dependencies != null &&
                    dependencyObjects.Count == (int)DependencyHashIndex.Count && 
                    location.Dependencies.Count == (int)DependencyHashIndex.Count )
                {
                    var remoteHash = dependencyObjects[(int)DependencyHashIndex.Remote] as string;
                    var cachedHash = dependencyObjects[(int)DependencyHashIndex.Cache] as string;
                    Addressables.LogFormat("Addressables - ContentCatalogProvider CachedHash = {0}, RemoteHash = {1}.", cachedHash, remoteHash);

                    if (string.IsNullOrEmpty(remoteHash)) //offline
                    {
                        if(!string.IsNullOrEmpty(cachedHash)) //cache exists
                            idToLoad = location.Dependencies[(int)DependencyHashIndex.Cache].InternalId.Replace(".hash", ".json");
                    }
                    else //online
                    {
                        if (remoteHash == cachedHash) //cache of remote is good
                        {
                            idToLoad = location.Dependencies[(int)DependencyHashIndex.Cache].InternalId.Replace(".hash", ".json");
                        }
                        else //remote is different than cache, or no cache
                        {
                            idToLoad = location.Dependencies[(int)DependencyHashIndex.Remote].InternalId.Replace(".hash", ".json");
                            m_LocalDataPath = location.Dependencies[(int)DependencyHashIndex.Cache].InternalId.Replace(".hash", ".json");
                            m_HashValue = remoteHash;
                        }
                    }
                }

                return idToLoad;
            }

            void OnCatalogLoaded(IAsyncOperation<ContentCatalogData> op)
            {
                Addressables.LogFormat("Addressables - Content catalog load result = {0}.", op.Result);
                Validate();
                SetResult(op.Result as TObject);
                ResourceManagerEventCollector.PostEvent(ResourceManagerEventCollector.EventType.LoadAsyncCompletion, Context, Time.frameCount - m_StartFrame);
                InvokeCompletionEvent();
                if (op.Result != null && !string.IsNullOrEmpty(m_HashValue) && !string.IsNullOrEmpty(m_LocalDataPath))
                {
                    var dir = Path.GetDirectoryName(m_LocalDataPath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                        Directory.CreateDirectory(dir);
                    var localCachePath = m_LocalDataPath;
                    Addressables.LogFormat("Addressables - Saving cached content catalog to {0}.", localCachePath);
                    File.WriteAllText(localCachePath, JsonUtility.ToJson(op.Result));
                    File.WriteAllText(localCachePath.Replace(".json", ".hash"), m_HashValue);
                }
            }
        }

        ///<inheritdoc/>
        public override IAsyncOperation<TObject> Provide<TObject>(IResourceLocation location, IList<object> deps)
        {
            if (location == null)
                throw new ArgumentNullException("location");
            var operation = AsyncOperationCache.Instance.Acquire<InternalOp<TObject>>();
            return operation.Start(location, deps);
        }
    }
}