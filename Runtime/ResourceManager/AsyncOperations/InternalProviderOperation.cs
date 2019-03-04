using System;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.Util;

namespace UnityEngine.ResourceManagement.AsyncOperations
{
    abstract class InternalProviderOperation<TObject> : AsyncOperationBase<TObject>
        where TObject : class
    {
        int m_StartFrame;

        internal virtual InternalProviderOperation<TObject> Start(IResourceLocation location)
        {
            Validate();
            if (location == null)
                OperationException = new ArgumentNullException("location");
            m_StartFrame = Time.frameCount;
            Context = location;
            return this;
        }

        protected virtual void OnComplete(IAsyncOperation<TObject> op)
        {
            Validate();
            if (op.Status != AsyncOperationStatus.Succeeded)
                m_Error = op.OperationException;

            SetResult(op.Result);
            OnComplete();
        }

        protected virtual void OnComplete(AsyncOperation op)
        {
            Validate();
            TObject res = default(TObject);
            try
            {
                res = ConvertResult(op);
            }
            catch (Exception ex)
            {
                OperationException = ex;
            }
            SetResult(res);
            OnComplete();
        }

        protected virtual void OnComplete()
        {
            Validate();
            ResourceManagerEventCollector.PostEvent(ResourceManagerEventCollector.EventType.LoadAsyncCompletion, Context, Time.frameCount - m_StartFrame);
            InvokeCompletionEvent();
        }

        internal virtual TObject ConvertResult(AsyncOperation op)
        {
            return default(TObject);
        }
    }
}
