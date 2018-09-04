using System.Collections.Generic;
using System.IO;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.Diagnostics;

namespace UnityEngine.AddressableAssets
{
    /// <summary>
    /// Provider for content catalogs.  This provider makes use of a hash file to determine if a newer version of the catalog needs to be downloaded.
    /// </summary>
    public class ContentCatalogProvider : ResourceProviderBase
    {
        internal class InternalOp<TObject> : AsyncOperationBase<TObject> where TObject : class
        {
            int startFrame;
            System.Action<IAsyncOperation<IList<object>>> action;
            string m_localDataPath;
            string m_hashValue;
            public InternalOp()
            {
                action = (op) =>
                {
                    if (op.Result.Count == 2)
                    {
                        var localHash = op.Result[0] as string;
                        var remoteHash = op.Result[1] as string;
                        Addressables.LogFormat("Addressables - ContentCatalogProvider LocalHash = {0}, RemoteHash = {1}.", localHash, remoteHash);

                        if (remoteHash == localHash || string.IsNullOrEmpty(remoteHash))
                        {
                            if (string.IsNullOrEmpty(remoteHash))
                                Addressables.LogFormat("Addressables - Unable to load remote catalog hash: {0}.", op.OperationException);
                            var depOps = op.Context as IList<IResourceLocation>;
                            var localDataPath = depOps[0].InternalId.Replace(".hash", ".json");
                            Addressables.LogFormat("Addressables - Using content catalog from {0}.", localDataPath);
                            ResourceManager.ProvideResource<ContentCatalogData>(new ResourceLocationBase(localDataPath, localDataPath, typeof(JsonAssetProvider).FullName)).Completed += OnCatalogLoaded;
                        }
                        else
                        {
                            var depOps = op.Context as IList<IResourceLocation>;
                            var remoteDataPath = depOps[1].InternalId.Replace(".hash", ".json");
                            m_localDataPath = depOps[0].InternalId.Replace(".hash", ".json");
                            m_hashValue = remoteHash;
                            Addressables.LogFormat("Addressables - Using content catalog from {0}.", remoteDataPath);
                            ResourceManager.ProvideResource<ContentCatalogData>(new ResourceLocationBase(remoteDataPath, remoteDataPath, typeof(JsonAssetProvider).FullName)).Completed += OnCatalogLoaded;
                        }
                    }
                    else
                    {
                        Addressables.LogWarningFormat("Addressables - Unable to laod dependencies for content catalog at location {0}", op.Context);
                    }
                };
            }

            public IAsyncOperation<TObject> Start(IResourceLocation location, IAsyncOperation<IList<object>> loadDependencyOperation)
            {
                Addressables.LogFormat("Addressables - Loading content catalog from {0}.", location.InternalId);
                Validate();
                m_localDataPath = null;
                m_hashValue = null;
                startFrame = Time.frameCount;
                m_result = null;
                Context = location;
                loadDependencyOperation.Completed += action;
                return this;
            }

            private void OnCatalogLoaded(IAsyncOperation<ContentCatalogData> op)
            {
                Addressables.LogFormat("Addressables - Content catalog load result = {0}.", op.Result);
                Validate();
                SetResult(op.Result as TObject);
                ResourceManagerEventCollector.PostEvent(ResourceManagerEventCollector.EventType.LoadAsyncCompletion, Context, Time.frameCount - startFrame);
                InvokeCompletionEvent();
                if (op.Result != null && !string.IsNullOrEmpty(m_hashValue) && !string.IsNullOrEmpty(m_localDataPath))
                {
                    var dir = Path.GetDirectoryName(m_localDataPath);
                    if (!Directory.Exists(dir))
                        Directory.CreateDirectory(dir);
                    var localCachePath = m_localDataPath;
                    Addressables.LogFormat("Addressables - Saving cached content catalog to {0}.", localCachePath);
                    File.WriteAllText(localCachePath, JsonUtility.ToJson(op.Result));
                    File.WriteAllText(localCachePath.Replace(".json", ".hash"), m_hashValue);
                }
            }
        }

        ///<inheritdoc/>
        public override IAsyncOperation<TObject> Provide<TObject>(IResourceLocation location, IAsyncOperation<IList<object>> loadDependencyOperation)
        {
            if (location == null)
                throw new System.ArgumentNullException("location");
            if (loadDependencyOperation == null)
                throw new System.ArgumentNullException("loadDependencyOperation");
            var operation = AsyncOperationCache.Instance.Acquire<InternalOp<TObject>>();
            return operation.Start(location, loadDependencyOperation);
        }
    }
}