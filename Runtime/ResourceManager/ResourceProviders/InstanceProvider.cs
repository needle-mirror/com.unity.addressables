using System;
using System.Collections.Generic;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.Util;

namespace UnityEngine.ResourceManagement.ResourceProviders
{
    /// <summary>
    /// Basic implementation of IInstanceProvider.
    /// </summary>
    public class InstanceProvider : IInstanceProvider
    {
        internal class InternalOp<TObject> : AsyncOperationBase<TObject>
            where TObject : Object
        {
            TObject m_PrefabResult;
            int m_StartFrame;
            Action<IAsyncOperation<TObject>> m_CompleteAction;
            InstantiationParameters m_InstParams;

            public InternalOp()
            {
                m_CompleteAction = OnComplete;
            }

            public InternalOp<TObject> Start(IAsyncOperation<TObject> loadOperation, IResourceLocation location, InstantiationParameters instantiateParameters)
            {
                Validate();
                m_PrefabResult = null;
                m_Result = null;
                Context = location;
                m_InstParams = instantiateParameters;
                m_StartFrame = Time.frameCount;
                loadOperation.Completed += m_CompleteAction;
                return this;
            }

            void OnComplete(IAsyncOperation<TObject> operation)
            {
                Validate();
                Debug.Assert(operation != null);
                ResourceManagerEventCollector.PostEvent(ResourceManagerEventCollector.EventType.InstantiateAsyncCompletion, Context, Time.frameCount - m_StartFrame);
                m_PrefabResult = operation.Result;
                if (m_PrefabResult == null)
                {
                    OperationException = new Exception(string.Format("Unable to load asset to instantiate from location {0}", Context));
                    SetResult(null);
                }
                else if (Result == null)
                {
                    SetResult(m_InstParams.Instantiate(m_PrefabResult));
                }
                InvokeCompletionEvent();
            }
        }

        /// <inheritdoc/>
        public bool CanProvideInstance<TObject>(IResourceProvider loadProvider, IResourceLocation location)
            where TObject : Object
        {
            if (loadProvider == null)
                return false;
            return loadProvider.CanProvide<TObject>(location) && ResourceManagerConfig.IsInstance<TObject, GameObject>();
        }

        /// <inheritdoc/>
        public IAsyncOperation<TObject> ProvideInstanceAsync<TObject>(IResourceProvider loadProvider, IResourceLocation location, IList<object> deps, InstantiationParameters instantiateParameters)
            where TObject : Object
        {
            if (location == null)
                throw new ArgumentNullException("location");

            if (loadProvider == null)
                throw new ArgumentNullException("loadProvider");

            var depOp = loadProvider.Provide<TObject>(location, deps);

            var operation = AsyncOperationCache.Instance.Acquire<InternalOp<TObject>>();
            return operation.Start(depOp, location, instantiateParameters);
        }

        /// <inheritdoc/>
        public bool ReleaseInstance(IResourceProvider loadProvider, IResourceLocation location, Object instance)
        {
            if (loadProvider == null)
                throw new ArgumentException("IResourceProvider loadProvider cannot be null.");
            if (Application.isPlaying)
                Object.Destroy(instance);
            else
                Object.DestroyImmediate(instance);
            return true;
        }
    }
}
