using System.Collections.Generic;
using System.IO;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.Diagnostics;

namespace UnityEngine.AddressableAssets
{
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
                        if (remoteHash == localHash || string.IsNullOrEmpty(remoteHash))
                        {
                            if (string.IsNullOrEmpty(remoteHash))
                                Debug.LogFormat("Unable to load remote catalog hash: {0}.", op.OperationException);
                            var depOps = op.Context as IList<IResourceLocation>;
                            var localDataPath = depOps[0].InternalId.Replace(".hash", ".json");
                            ResourceManager.ProvideResource<ContentCatalogData>(new ResourceLocationBase(localDataPath, localDataPath, typeof(JsonAssetProvider).FullName)).Completed += OnCatalogLoaded;
                        }
                        else
                        {
                            var depOps = op.Context as IList<IResourceLocation>;
                            var remoteDataPath = depOps[1].InternalId.Replace(".hash", ".json");
                            m_localDataPath = depOps[0].InternalId.Replace(".hash", ".json");
                            m_hashValue = remoteHash;
                            ResourceManager.ProvideResource<ContentCatalogData>(new ResourceLocationBase(remoteDataPath, remoteDataPath, typeof(JsonAssetProvider).FullName)).Completed += OnCatalogLoaded;
                        }
                    }
                    else
                    {
                        Debug.LogWarningFormat("Unable to laod dependencies for content catalog at location {0}", op.Context);
                    }
                };
            }

            public IAsyncOperation<TObject> Start(IResourceLocation location, IAsyncOperation<IList<object>> loadDependencyOperation)
            {
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
                    File.WriteAllText(localCachePath, JsonUtility.ToJson(op.Result));
                    File.WriteAllText(localCachePath.Replace(".json", ".hash"), m_hashValue);
                }
            }
        }

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