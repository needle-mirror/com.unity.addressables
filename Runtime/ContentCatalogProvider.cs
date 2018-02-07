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
                            var depOps = op.Context as IList<IResourceLocation>;
                            var localDataPath = depOps[0].InternalId.Replace(".hash", ".json");
                            ResourceManager.LoadAsync<ResourceLocationList>(new ResourceLocationBase<string>(localDataPath, localDataPath, typeof(JsonAssetProvider).FullName)).Completed += OnCatalogLoaded;
                        }
                        else
                        {
                            var depOps = op.Context as IList<IResourceLocation>;
                            var remoteDataPath = depOps[1].InternalId.Replace(".hash", ".json");
                            m_localDataPath = depOps[0].InternalId.Replace(".hash", ".json");
                            m_hashValue = remoteHash;
                            ResourceManager.LoadAsync<ResourceLocationList>(new ResourceLocationBase<string>(remoteDataPath, remoteDataPath, typeof(JsonAssetProvider).FullName)).Completed += OnCatalogLoaded;
                        }
                    }
                };
            }

            public IAsyncOperation<TObject> Start(IResourceLocation location, IAsyncOperation<IList<object>> loadDependencyOperation)
            {
                Validate();
                m_localDataPath = null;
                m_hashValue = null;
                startFrame = Time.frameCount;
                Result = null;
                Context = location;
                loadDependencyOperation.Completed += action;
                return this;
            }

            private void OnCatalogLoaded(IAsyncOperation<ResourceLocationList> op)
            {
                Validate();
                SetResult(op.Result as TObject);
                ResourceManagerEventCollector.PostEvent(ResourceManagerEventCollector.EventType.LoadAsyncCompletion, Context, Time.frameCount - startFrame);
                InvokeCompletionEvent();
                if (op.Result != null && !string.IsNullOrEmpty(m_hashValue) && !string.IsNullOrEmpty(m_localDataPath))
                {
                    var localCachePath = ResourceManagerConfig.ExpandPathWithGlobalVariables(m_localDataPath);
                    File.WriteAllText(localCachePath, JsonUtility.ToJson(op.Result));
                    File.WriteAllText(localCachePath.Replace(".json", ".hash"), m_hashValue);
                }
            }
        }

        public override IAsyncOperation<TObject> ProvideAsync<TObject>(IResourceLocation location, IAsyncOperation<IList<object>> loadDependencyOperation)
        {
            if (location == null)
                throw new System.ArgumentNullException("location");
            if (loadDependencyOperation == null)
                throw new System.ArgumentNullException("loadDependencyOperation");
            var operation = AsyncOperationCache.Instance.Acquire<InternalOp<TObject>, TObject>();
            return operation.Start(location, loadDependencyOperation);
        }
    }
}