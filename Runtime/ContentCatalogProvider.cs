using System;
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
            int m_StartFrame;
            Action<IAsyncOperation<IList<object>>> m_Action;
            string m_LocalDataPath;
            string m_HashValue;
            public InternalOp()
            {
                m_Action = op =>
                {
                    if (op.Result.Count == 2)
                    {
                        var remoteHash = op.Result[0] as string;
						var localHash = op.Result[1] as string;
						Addressables.LogFormat("Addressables - ContentCatalogProvider LocalHash = {0}, RemoteHash = {1}.", localHash, remoteHash);

                        if (remoteHash == localHash || string.IsNullOrEmpty(remoteHash))
                        {
                            if (string.IsNullOrEmpty(localHash))
                                Addressables.LogFormat("Addressables - Unable to load localHash catalog hash: {0}.", op.OperationException);
                            var depOps = op.Context as IList<IResourceLocation>;
                            if (depOps == null)
                                return;
                            var localDataPath = depOps[1].InternalId.Replace(".hash", ".json");
                            Addressables.LogFormat("Addressables - Using content catalog from {0}.", localDataPath);
                            ResourceManager.ProvideResource<ContentCatalogData>(new ResourceLocationBase(localDataPath, localDataPath, typeof(JsonAssetProvider).FullName)).Completed += OnCatalogLoaded;
                        }
                        else
                        {
                            var depOps = op.Context as IList<IResourceLocation>;
                            if (depOps == null)
                                return;
                            var remoteDataPath = depOps[0].InternalId.Replace(".hash", ".json");
                            m_LocalDataPath = depOps[1].InternalId.Replace(".hash", ".json");
                            m_HashValue = remoteHash;
                            Addressables.LogFormat("Addressables - Using content catalog from {0}.", remoteDataPath);
                            ResourceManager.ProvideResource<ContentCatalogData>(new ResourceLocationBase(remoteDataPath, remoteDataPath, typeof(JsonAssetProvider).FullName)).Completed += OnCatalogLoaded;
                        }
                    }
                    else
                    {
                        Addressables.LogWarningFormat("Addressables - Unable to load dependencies for content catalog at location {0}", op.Context);
                    }
                };
            }

            public IAsyncOperation<TObject> Start(IResourceLocation location, IAsyncOperation<IList<object>> loadDependencyOperation)
            {
                Validate();
                m_LocalDataPath = null;
                m_HashValue = null;
                m_StartFrame = Time.frameCount;
                m_Result = null;
                Context = location;
                if (loadDependencyOperation == null)
                {
                    Addressables.LogWarningFormat("Addressables -Invalid dependencies for content catalog at location {0}", location);
                    SetResult(default(TObject));
                    DelayedActionManager.AddAction((Action)InvokeCompletionEvent);
                }
                else
                {
                    loadDependencyOperation.Completed += m_Action;
                }
                return this;
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
        public override IAsyncOperation<TObject> Provide<TObject>(IResourceLocation location, IAsyncOperation<IList<object>> loadDependencyOperation)
        {
            if (location == null)
                throw new ArgumentNullException("location");
            var operation = AsyncOperationCache.Instance.Acquire<InternalOp<TObject>>();
            return operation.Start(location, loadDependencyOperation);
        }
    }
}