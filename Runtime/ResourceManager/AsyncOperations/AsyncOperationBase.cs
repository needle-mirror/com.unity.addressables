using System;
using System.Collections.Generic;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.Util;

// ReSharper disable DelegateSubtraction

namespace UnityEngine.ResourceManagement.AsyncOperations
{
    /// <summary>
    /// base class for implemented AsyncOperations, implements the needed interfaces and consolidates redundant code
    /// </summary>
    public abstract class AsyncOperationBase<TObject> : IAsyncOperation<TObject>
    {
        /// <summary>
        /// The result value of the operation.
        /// </summary>
        protected TObject m_Result;
        /// <summary>
        /// The status of the operation.
        /// </summary>
        protected AsyncOperationStatus m_Status;
        /// <summary>
        /// If and error is encountered, an exception is saved in this member.
        /// </summary>
        protected Exception m_Error;
        /// <summary>
        /// Context object.  This is usually set to the IResourceLocation used to start this operation.
        /// </summary>
        protected object m_Context;
        /// <summary>
        /// The key used to start the operation.  This is usually the address.
        /// </summary>
        protected object m_Key;
        /// <summary>
        /// If true, this operation is released to a cache for re-use.  Otherwise, it will be garbage collected.
        /// </summary>
        protected bool m_ReleaseToCacheOnCompletion;
        Action<IAsyncOperation> m_CompletedAction;
        List<Action<IAsyncOperation<TObject>>> m_CompletedActionT;

        /// <summary>
        /// Constructor.
        /// </summary>
        protected AsyncOperationBase()
        {
            IsValid = true;
        }

        System.Threading.EventWaitHandle m_waitHandle;
        public System.Threading.WaitHandle WaitHandle
        {
            get
            {
                if(m_waitHandle == null)
                    m_waitHandle = new System.Threading.EventWaitHandle(false, System.Threading.EventResetMode.ManualReset);
                m_waitHandle.Reset();
                return m_waitHandle;
            }
        }

#if NET_4_6
        public System.Threading.Tasks.Task<TObject> Task
        {
            get
            {
                return System.Threading.Tasks.Task.Factory.StartNew(o =>
                {
                    var asyncOperation = o as IAsyncOperation<TObject>;
                    asyncOperation.WaitHandle.WaitOne();
                    return asyncOperation.Result;
                }, this);
            }
        }
#endif

        /// <inheritdoc />
        public bool IsValid { get; set; }
        /// <inheritdoc />
        public override string ToString()
        {
            var instId = "";
            var or = m_Result as Object;
            if (or != null)
                instId = "(" + or.GetInstanceID() + ")";
            return string.Format("{0}, result='{1}', status='{2}', valid={3}, location={4}.",  base.ToString(), (m_Result + instId), m_Status, IsValid, m_Context);
        }
        /// <inheritdoc />
        public virtual void Release()
        {
            Validate();
            m_ReleaseToCacheOnCompletion = true;
            if (!m_InsideCompletionEvent && IsDone)
                AsyncOperationCache.Instance.Release(this);
        }
        /// <inheritdoc />
        public IAsyncOperation<TObject> Retain()
        {
            Validate();
            m_ReleaseToCacheOnCompletion = false;
            return this;
        }

        /// <inheritdoc />
        IAsyncOperation IAsyncOperation.Retain()
        {
            Validate();
            m_ReleaseToCacheOnCompletion = false;
            return this;
        }


        /// <inheritdoc />
        public virtual void ResetStatus()
        {
            m_ReleaseToCacheOnCompletion = true;
            m_Status = AsyncOperationStatus.None;
            m_Error = null;
            m_Result = default(TObject);
            m_Context = null;
            m_Key = null;
        }
        /// <inheritdoc />
        public bool Validate()
        {
            if (!IsValid)
            {
                Debug.LogError("INVALID OPERATION STATE: " + this);
                return false;
            }
            return true;
        }
        /// <inheritdoc />
        public event Action<IAsyncOperation<TObject>> Completed
        {
            add
            {
                Validate();
                if (IsDone)
                {
                    DelayedActionManager.AddAction(value, 0, this);
                }
                else
                {
                    if (m_CompletedActionT == null)
                        m_CompletedActionT = new List<Action<IAsyncOperation<TObject>>>(2);
                    m_CompletedActionT.Add(value);
                }
            }

            remove
            {
                m_CompletedActionT.Remove(value);
            }
        }
        /// <inheritdoc />
        event Action<IAsyncOperation> IAsyncOperation.Completed
        {
            add
            {
                Validate();
                if (IsDone)
                    DelayedActionManager.AddAction(value, 0, this);
                else
                    m_CompletedAction += value;
            }

            remove
            {
                m_CompletedAction -= value;
            }
        }

        object IAsyncOperation.Result
        {
            get
            {
                Validate();
                return m_Result;
            }
        }
        /// <inheritdoc />
        public AsyncOperationStatus Status
        {
            get
            {
                Validate();
                return m_Status;
            }
            protected set
            {
                Validate();
                m_Status = value;
            }
        }
        /// <inheritdoc />
        public Exception OperationException
        {
            get
            {
                Validate();
                return m_Error;
            }
            protected set
            {
                m_Error = value;
                if (m_Error != null && ResourceManager.ExceptionHandler != null)
                    ResourceManager.ExceptionHandler(this, value);
            }
        }
        /// <inheritdoc />
        public bool MoveNext()
        {
            Validate();
            return !IsDone;
        }
        /// <inheritdoc />
        public void Reset()
        {
        }
        /// <inheritdoc />
        public object Current
        {
            get
            {
                Validate();
                return Result;
            }
        }
        /// <inheritdoc />
        public TObject Result
        {
            get
            {
                Validate();
                return m_Result;
            }
        }
        /// <inheritdoc />
        public virtual bool IsDone
        {
            get
            {
                Validate();
                return Status == AsyncOperationStatus.Failed || Status == AsyncOperationStatus.Succeeded;
            }
        }
        /// <inheritdoc />
        public virtual float PercentComplete
        {
            get
            {
                Validate();
                return IsDone ? 1f : 0f;
            }
        }
        /// <inheritdoc />
        public object Context
        {
            get
            {
                Validate();
                return m_Context;
            }
            protected set
            {
                Validate();
                m_Context = value;
            }
        }
        /// <inheritdoc />
        public virtual object Key
        {
            get
            {
                Validate();
                return m_Key;
            }
            set
            {
                Validate();
                m_Key = value;
            }
        }

        bool m_InsideCompletionEvent;
        /// <summary>
        /// Call the event handlers for this operation.
        /// </summary>
        public void InvokeCompletionEvent()
        {
            Validate();
            m_InsideCompletionEvent = true;

            if (m_CompletedAction != null)
            {
                var tmpEvent = m_CompletedAction;
                m_CompletedAction = null;
                try
                {
                    tmpEvent(this);
                }
                catch (Exception e)
                {
                    m_Status = AsyncOperationStatus.Failed;
                    OperationException = e;
                }
            }

            if (m_CompletedActionT != null)
            {
                for (int i = 0; i < m_CompletedActionT.Count; i++)
                {
                    try
                    {
                        m_CompletedActionT[i](this);
                    }
                    catch (Exception e)
                    {
                        m_Status = AsyncOperationStatus.Failed;
                        OperationException = e;
                    }
                }
                m_CompletedActionT.Clear();
            }
            m_InsideCompletionEvent = false;
            if (m_ReleaseToCacheOnCompletion)
                AsyncOperationCache.Instance.Release(this);
            if (m_waitHandle != null)
                m_waitHandle.Set();
        }
        /// <summary>
        /// Set the result object.  Status will be set to AsyncOperationStatus.Succeeded if the value is non-null, otherwise it is set to AsyncOperationStatus.Failed.
        /// </summary>
        /// <param name="result">The value to set result to.</param>
        public virtual void SetResult(TObject result)
        {
            Validate();
            m_Result = result;
            m_Status = (m_Result == null) ? AsyncOperationStatus.Failed : AsyncOperationStatus.Succeeded;
        }
    }

    /// <summary>
    /// Wrapper operation for completed results or error cases.
    /// </summary>
    /// <typeparam name="TObject"></typeparam>
    public class CompletedOperation<TObject> : AsyncOperationBase<TObject>
    {
         /// <summary>
        /// Starts the operation.
        /// </summary>
        /// <param name="context">Context object.  This is usually set to the IResourceLocation.</param>
        /// <param name="key">Key value.  This is usually set to the address.</param>
        /// <param name="val">Completed result object.  This may be null if error is set.</param>
        /// <param name="error">Optional exception.  This should be set when val is null.</param>       
        public virtual IAsyncOperation<TObject> Start(object context, object key, TObject val, Exception error = null)
        {
            Context = context;
            OperationException = error;
            Key = key;
            SetResult(val);
            Retain();
            DelayedActionManager.AddAction((Action)InvokeCompletionEvent);
            return this;
        }
    }

    /// <summary>
    /// This class can be used to chain operations together in a dependency chain.
    /// </summary>
    /// <typeparam name="TObject">The type of the operation.</typeparam>
    /// <typeparam name="TObjectDependency">The type parameter of the dependency IAsyncOperation.</typeparam>
    public class ChainOperation<TObject, TObjectDependency> : AsyncOperationBase<TObject>
    {
        public delegate IAsyncOperation<TObject> ChainCallbackDelegate(IAsyncOperation<TObjectDependency> op);
        ChainCallbackDelegate m_Func;
        IAsyncOperation m_DependencyOperation;
        IAsyncOperation m_DependentOperation;
        bool m_CallbackOnFailedDependency;
        /// <summary>
        /// Start the operation.
        /// </summary>
        /// <param name="context">Context object. Usually set to the IResourceLocation.</param>
        /// <param name="key">Key object.  Usually set to the primary key or address.</param>
        /// <param name="dependency">The IAsyncOperation that must complete before invoking the Func that generates the dependent operation that will set the result of this operation.</param>
        /// <param name="func">Function that takes as input the dependency operation and returns a new IAsyncOperation with the results needed by this operation.</param>
        /// <returns></returns>
        public virtual IAsyncOperation<TObject> Start(object context, object key, IAsyncOperation<TObjectDependency> dependency, ChainCallbackDelegate func, bool callbackOnFailedDependency = false)
        {
            Debug.Assert(dependency != null);
            m_Func = func;
            Context = context;
            Key = key;
            m_DependencyOperation = dependency;
            m_DependentOperation = null;
            m_CallbackOnFailedDependency = callbackOnFailedDependency;
            dependency.Completed += OnDependencyCompleted;
            
            return this;
        }
        /// <inheritdoc />
        public override float PercentComplete
        {
            get
            {
                if (m_DependentOperation == null)
                {
                    if (m_DependencyOperation == null)
                        return 0;
                            
                    return m_DependencyOperation.PercentComplete * .5f;
                }
                    
                return m_DependentOperation.PercentComplete * .5f + .5f;
            }
        }

        void OnDependencyCompleted(IAsyncOperation<TObjectDependency> op)
        {
            if (op.Status == AsyncOperationStatus.Failed && !m_CallbackOnFailedDependency)
            {
                SetResult(default(TObject));
                InvokeCompletionEvent();
            }
            else
            {
                m_DependencyOperation = null;
                var funcOp = m_Func(op);
                m_DependentOperation = funcOp;
                Context = funcOp.Context;
                funcOp.Key = Key;
                op.Release();
                funcOp.Completed += OnFuncCompleted;
            }
        }

        void OnFuncCompleted(IAsyncOperation<TObject> op)
        {
            SetResult(op.Result);
            InvokeCompletionEvent();
        }
        /// <inheritdoc />
        public override object Key
        {
            get
            {
                Validate();
                return m_Key;
            }
            set
            {
                Validate();
                m_Key = value;
                if (m_DependencyOperation != null)
                    m_DependencyOperation.Key = Key;
            }
        }
    }

   
    /// <summary>
    /// Class used to combine multiple operations into a single one.
    /// </summary>
    /// <typeparam name="TObject"></typeparam>
    public class GroupOperation<TObject> : AsyncOperationBase<IList<TObject>> where TObject : class
    {
        Action<IAsyncOperation<TObject>> m_Callback;
        Action<IAsyncOperation<TObject>> m_InternalOnComplete;
        List<IAsyncOperation<TObject>> m_Operations;
        int m_LoadedCount;
        /// <summary>
        /// Construct a new GroupOperation.
        /// </summary>
        public GroupOperation()
        {
            m_InternalOnComplete = OnOperationCompleted;
            m_Result = new List<TObject>();
        }
        /// <inheritdoc />
        public override void SetResult(IList<TObject> result)
        {
            Validate();
        }
        /// <inheritdoc />
        public override void ResetStatus()
        {
            m_ReleaseToCacheOnCompletion = true;
            m_Status = AsyncOperationStatus.None;
            m_Error = null;
            m_Context = null;

            Result.Clear();
            m_Operations = null;
        }
        /// <inheritdoc />
        public override object Key
        {
            get
            {
                Validate();
                return m_Key;
            }
            set
            {
                Validate();
                m_Key = value;
                if (m_Operations != null)
                {
                    foreach (var op in m_Operations)
                        op.Key = Key;
                }
            }
        }
        /// <summary>
        /// Load a list of assets associated with the provided IResourceLocations.
        /// </summary>
        /// <param name="locations">The list of locations.</param>
        /// <param name="callback">Callback methods that will be called when each sub operation is complete.  Order is not guaranteed.</param>
        /// <param name="func">Function to generated each sub operation from the locations</param>
        /// <returns>This object with the results being set to the results of the sub operations.  The result will match the size and order of the locations list.</returns>
        public virtual IAsyncOperation<IList<TObject>> Start(IList<IResourceLocation> locations, Action<IAsyncOperation<TObject>> callback, Func<IResourceLocation, IAsyncOperation<TObject>> func)
        {
            m_Context = locations;
            m_Callback = callback;
            m_LoadedCount = 0;
            m_Operations = new List<IAsyncOperation<TObject>>(locations.Count);
            foreach (var o in locations)
            {
                Result.Add(default(TObject));
                var op = func(o);
                op.Key = Key;
                m_Operations.Add(op);
                op.Completed += m_InternalOnComplete;
            }
            return this;
        }

        /// <summary>
        /// Load a list of assets associated with the provided IResourceLocations.
        /// </summary>
        /// <param name="locations">The list of locations.</param>
        /// <param name="callback">Callback methods that will be called when each sub operation is complete.  Order is not guaranteed.</param>
        /// <param name="func">Function to generated each sub operation from the locations.  This variation allows for a parameter to be passed to this method of type TParam.</param>
        /// <param name="funcParams">The parameter objec to pass to the func.</param>
        /// <returns>This object with the results being set to the results of the sub operations.  The result will match the size and order of the locations list.</returns>
        public virtual IAsyncOperation<IList<TObject>> Start<TParam>(IList<IResourceLocation> locations, Action<IAsyncOperation<TObject>> callback, Func<IResourceLocation, TParam, IAsyncOperation<TObject>> func, TParam funcParams)
        {
            m_Context = locations;
            m_Callback = callback;
            m_LoadedCount = 0;
            m_Operations = new List<IAsyncOperation<TObject>>(locations.Count);
            foreach (var o in locations)
            {
                Result.Add(default(TObject));
                var op = func(o, funcParams);
                op.Key = Key;
                m_Operations.Add(op);
                op.Completed += m_InternalOnComplete;
            }
            return this;
        }

        /// <summary>
        /// Combines a set of IAsyncOperations into a single operation
        /// </summary>
        /// <param name="context">The context object.</param>
        /// <param name="key">The key object.</param>
        /// <param name="operations">The list of operations to wait on.</param>
        /// <returns></returns>
        public virtual IAsyncOperation<IList<TObject>> Start(object context, object key, List<IAsyncOperation<TObject>> operations)
        {
            m_Context = context;
            m_LoadedCount = 0;
            m_Operations = operations;
            foreach (var op in m_Operations)
            {
                Result.Add(default(TObject));
                op.Key = key;
                op.Completed += m_InternalOnComplete;
            }
            if (m_Operations.Count == 0)
                InvokeCompletionEvent();
            return this;
        }


        /// <inheritdoc />
        public override bool IsDone
        {
            get
            {
                Validate();
                return Result.Count == m_LoadedCount;
            }
        }
        /// <inheritdoc />
        public override float PercentComplete
        {
            get
            {
                if (IsDone || m_Operations.Count < 1)
                    return 1f;
                float total = 0;
                for (int i = 0; i < m_Operations.Count; i++)
                    total += m_Operations[i].PercentComplete;
                return total / m_Operations.Count;
            }
        }

        void OnOperationCompleted(IAsyncOperation<TObject> op)
        {
            if (m_Callback != null)
            {
                op.Retain();
                m_Callback(op);
            }
            m_LoadedCount++;
            for (int i = 0; i < m_Operations.Count; i++)
            {
                if (Result[i] == null && m_Operations[i] == op)
                {
                    Result[i] = op.Result;
                    if (op.Status != AsyncOperationStatus.Succeeded)
                    {
                        Status = op.Status;
                        m_Error = op.OperationException;
                    }
                    break;
                }
            }
            op.Release();
            if (IsDone)
            {
                if (Status != AsyncOperationStatus.Failed)
                    Status = AsyncOperationStatus.Succeeded;
                InvokeCompletionEvent();
            }
        }
    }

}
