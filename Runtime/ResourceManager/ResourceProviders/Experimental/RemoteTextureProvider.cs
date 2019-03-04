using System;
using System.Collections.Generic;
using UnityEngine.Networking;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.Util;

namespace UnityEngine.ResourceManagement.ResourceProviders.Experimental
{
    /// <summary>
    /// Provides a Texture2d object from a remote url using UnityWebRequestTexture.GetTexture.
    /// </summary>
    public class RemoteTextureProvider : ResourceProviderBase
    {
        /// <inheritdoc/>
        public override bool CanProvide<TObject>(IResourceLocation location)
        {
            return base.CanProvide<TObject>(location) && ResourceManagerConfig.IsInstance<TObject, Texture2D>();
        }

        class InternalOp<TObject> : InternalProviderOperation<TObject>
            where TObject : class
        {
            AsyncOperation m_RequestOperation;
            public InternalProviderOperation<TObject> Start(IResourceLocation location, IList<object> deps)
            {
                m_Result = null;
                m_RequestOperation = null;
                m_RequestOperation = UnityWebRequestTexture.GetTexture(location.InternalId).SendWebRequest();
                if (m_RequestOperation.isDone)
                    DelayedActionManager.AddAction((Action<AsyncOperation>)OnComplete, 0, m_RequestOperation);
                else
                    m_RequestOperation.completed += OnComplete;
                return base.Start(location);
            }
            public override float PercentComplete
            {
                get
                {
                    if (IsDone)
                        return 1;
                    return m_RequestOperation != null ? m_RequestOperation.progress : 0.0f;
                }
            }
            internal override TObject ConvertResult(AsyncOperation op)
            {
                if (op is UnityWebRequestAsyncOperation)
                {
                    var textureHandler = ((op as UnityWebRequestAsyncOperation).webRequest.downloadHandler as DownloadHandlerTexture);
                    if(textureHandler != null)
                        return textureHandler.texture as TObject;
                }

                return null;
            }
        }

        /// <inheritdoc/>
        public override IAsyncOperation<TObject> Provide<TObject>(IResourceLocation location, IList<object> deps)
        {
            var operation = AsyncOperationCache.Instance.Acquire<InternalOp<TObject>>();
            return operation.Start(location, deps);
        }

        /// <inheritdoc/>
        public override bool Release(IResourceLocation location, object asset)
        {
            if (location == null)
                throw new ArgumentNullException("location");
            return true;
        }
    }
}
