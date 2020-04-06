using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEngine.ResourceManagement.AsyncOperations
{
    /// <summary>
    /// Handle for internal operations.  This allows for reference counting and checking for valid references.
    /// </summary>
    /// <typeparam name="TObject">The object type of the underlying operation.</typeparam>
    public struct AsyncOperationHandle<TObject> : IEnumerator, IEquatable<AsyncOperationHandle<TObject>>
    {
        internal AsyncOperationBase<TObject> m_InternalOp;
        int m_Version;
        
        /// <summary>
        /// Conversion from typed to non typed handles.  This does not increment the reference count.
        /// To convert from non-typed back, use AsyncOperationHandle.Convert<T>()
        /// </summary>
        /// <param name="obj"></param>
        static public implicit operator AsyncOperationHandle(AsyncOperationHandle<TObject> obj)
        {
            return new AsyncOperationHandle(obj.m_InternalOp, obj.m_Version);
        }
        
        internal AsyncOperationHandle(AsyncOperationBase<TObject> op)
        {
            m_InternalOp = op;
            m_Version = op?.Version ?? 0;
        }

        internal AsyncOperationHandle(IAsyncOperation op)
        {
            m_InternalOp = (AsyncOperationBase<TObject>)op;
            m_Version = op?.Version ?? 0;
        }
        
        internal AsyncOperationHandle(IAsyncOperation op, int version)
        {
            m_InternalOp = (AsyncOperationBase<TObject>)op;
            m_Version = version;
        }
        
        /// <summary>
        /// Acquire a new handle to the internal operation.  This will increment the reference count, therefore the returned handle must also be released.
        /// </summary>
        /// <returns>A new handle to the operation.  This handle must also be released.</returns>
        internal AsyncOperationHandle<TObject> Acquire()
        {
            InternalOp.IncrementReferenceCount();
            return this;
        }
        
        /// <summary>
        /// Completion event for the internal operation.  If this is assigned on a completed operation, the callback is deferred until the LateUpdate of the current frame.
        /// </summary>
        public event Action<AsyncOperationHandle<TObject>> Completed
        {
            add { InternalOp.Completed += value; }
            remove { InternalOp.Completed -= value; }
        }

        /// <summary>
        /// Completion event for non-typed callback handlers.  If this is assigned on a completed operation, the callback is deferred until the LateUpdate of the current frame.
        /// </summary>
        public event Action<AsyncOperationHandle> CompletedTypeless
        {
            add { InternalOp.CompletedTypeless += value; }
            remove { InternalOp.CompletedTypeless -= value; }
        }

        /// <summary>
        /// Debug name of the operation.
        /// </summary>
        public string DebugName
        {
            get
            {
                if (!IsValid())
                    return "InvalidHandle";
                return ((IAsyncOperation)InternalOp).DebugName;
            }
        }
        
        /// <summary>
        /// Event for handling the destruction of the operation.  
        /// </summary>
        public event Action<AsyncOperationHandle> Destroyed
        {
            add { InternalOp.Destroyed += value; }
            remove { InternalOp.Destroyed -= value; }
        }

        /// <summary>
        /// Provide equality for this struct.
        /// </summary>
        /// <param name="other">The operation to compare to.</param>
        /// <returns></returns>
        public bool Equals(AsyncOperationHandle<TObject> other)
        {
            return m_Version == other.m_Version && m_InternalOp == other.m_InternalOp;
        }

        /// <summary>
        /// Get hash code of this struct.
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            return m_InternalOp == null ? 0 : m_InternalOp.GetHashCode() * 17 + m_Version;
        }
        
        AsyncOperationBase<TObject> InternalOp
        {
            get
            {
                if (m_InternalOp == null || m_InternalOp.Version != m_Version)
                    throw new Exception("Attempting to use an invalid operation handle");
                return m_InternalOp;
            }
        }

        /// <summary>
        /// True if the operation is complete.
        /// </summary>
        public bool IsDone
        {
            get { return !IsValid() || InternalOp.IsDone; }
        }

        /// <summary>
        /// Check if the handle references an internal operation.
        /// </summary>
        /// <returns>True if valid.</returns>
        public bool IsValid()
        {
            return m_InternalOp != null && m_InternalOp.Version == m_Version;
        }

        /// <summary>
        /// The exception for a failed operation.  This will be null unless Status is failed.
        /// </summary>
        public Exception OperationException
        {
            get { return InternalOp.OperationException; }
        }

        /// <summary>
        /// The progress of the internal operation.
        /// </summary>
        public float PercentComplete
        {
            get { return InternalOp.PercentComplete; }
        }
        
        /// <summary>
        /// The current reference count of the internal operation.
        /// </summary>
        internal int ReferenceCount
        {
            get { return InternalOp.ReferenceCount; }
        }

        /// <summary>
        /// Release the handle.  If the internal operation reference count reaches 0, the resource will be released.
        /// </summary>
        internal void Release()
        {
            InternalOp.DecrementReferenceCount();
            m_InternalOp = null;
        }

        /// <summary>
        /// The result object of the operations.
        /// </summary>
        public TObject Result
        {
            get { return InternalOp.Result; }
        }
        
        /// <summary>
        /// The status of the internal operation.
        /// </summary>
        public AsyncOperationStatus Status
        {
            get { return InternalOp.Status; }
        }

        /// <summary>
        /// Return a Task object to wait on when using async await.
        /// </summary>
        public System.Threading.Tasks.Task<TObject> Task
        {
            get { return InternalOp.Task; }
        }

        object IEnumerator.Current
        {
            get { return Result; }
        }
        
        bool IEnumerator.MoveNext()
        {
            return !IsDone;
        }

        void IEnumerator.Reset() { }
    }

    /// <summary>
    /// Non typed operation handle.  This allows for reference counting and checking for valid references.
    /// </summary>
    public struct AsyncOperationHandle : IEnumerator
    {
        internal IAsyncOperation m_InternalOp;
        int m_Version;

        internal AsyncOperationHandle(IAsyncOperation op)
        {
            m_InternalOp = op;
            m_Version = op?.Version ?? 0;
        }
        
        internal AsyncOperationHandle(IAsyncOperation op, int version)
        {
            m_InternalOp = op;
            m_Version = version;
        }
        
        /// <summary>
        /// Acquire a new handle to the internal operation.  This will increment the reference count, therefore the returned handle must also be released.
        /// </summary>
        /// <returns>A new handle to the operation.  This handle must also be released.</returns>
        internal AsyncOperationHandle Acquire()
        {
            InternalOp.IncrementReferenceCount();
            return this;
        }
        
        /// <summary>
        /// Completion event for the internal operation.  If this is assigned on a completed operation, the callback is deferred until the LateUpdate of the current frame.
        /// </summary>
        public event Action<AsyncOperationHandle> Completed
        {
            add { InternalOp.CompletedTypeless += value; }
            remove { InternalOp.CompletedTypeless -= value; }
        }
        
        /// <summary>
        /// Converts handle to be typed.  This does not increment the reference count.
        /// To convert back to non-typed, implicit conversion is available. 
        /// </summary>
        /// <typeparam name="T">The type of the handle.</typeparam>
        /// <returns>A new handle that is typed.</returns>
        public AsyncOperationHandle<T> Convert<T>()
        {
            return new AsyncOperationHandle<T>(InternalOp, m_Version);
        }

        /// <summary>
        /// Debug name of the operation.
        /// </summary>
        public string DebugName
        {
            get
            {
                if (!IsValid())
                    return "InvalidHandle";
                return InternalOp.DebugName;
            }
        }
        
        /// <summary>
        /// Event for handling the destruction of the operation.  
        /// </summary>
        public event Action<AsyncOperationHandle> Destroyed
        {
            add { InternalOp.Destroyed += value; }
            remove { InternalOp.Destroyed -= value; }
        }
        
        /// <summary>
        /// Get dependency operations.
        /// </summary>
        /// <param name="deps"></param>
        public void GetDependencies(List<AsyncOperationHandle> deps)
        {
            InternalOp.GetDependencies(deps);
        }
        
        /// <summary>
        /// Get hash code of this struct.
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            return m_InternalOp == null ? 0 : m_InternalOp.GetHashCode() * 17 + m_Version;
        }

        IAsyncOperation InternalOp
        {
            get
            {
                if (m_InternalOp == null || m_InternalOp.Version != m_Version)
                    throw new Exception("Attempting to use an invalid operation handle");
                return m_InternalOp;
            }
        }

        /// <summary>
        /// True if the operation is complete.
        /// </summary>
        public bool IsDone
        {
            get { return !IsValid() || InternalOp.IsDone; }
        }
        
        /// <summary>
        /// Check if the internal operation is not null and has the same version of this handle.
        /// </summary>
        /// <returns>True if valid.</returns>
        public bool IsValid()
        {
            return m_InternalOp != null && m_InternalOp.Version == m_Version;
        }
        
        /// <summary>
        /// The exception for a failed operation.  This will be null unless Status is failed.
        /// </summary>
        public Exception OperationException
        {
            get { return InternalOp.OperationException; }
        }
        
        /// <summary>
        /// The progress of the internal operation.
        /// </summary>
        public float PercentComplete
        {
            get { return InternalOp.PercentComplete; }
        }

        /// <summary>
        /// The current reference count of the internal operation.
        /// </summary>
        internal int ReferenceCount
        {
            get { return InternalOp.ReferenceCount; }
        }
        
        /// <summary>
        /// Release the handle.  If the internal operation reference count reaches 0, the resource will be released.
        /// </summary>
        internal void Release()
        {
            InternalOp.DecrementReferenceCount();
            m_InternalOp = null;
        }

        /// <summary>
        /// The result object of the operations.
        /// </summary>
        public object Result
        {
            get { return InternalOp.GetResultAsObject(); }
        }
        
        /// <summary>
        /// The status of the internal operation.
        /// </summary>
        public AsyncOperationStatus Status
        {
            get { return InternalOp.Status; }
        }
        
        /// <summary>
        /// Return a Task object to wait on when using async await.
        /// </summary>
        public System.Threading.Tasks.Task<object> Task
        {
            get { return InternalOp.Task; }
        }

        object IEnumerator.Current
        {
            get { return Result; }
        }
        
        bool IEnumerator.MoveNext()
        {
            return !IsDone;
        }

        void IEnumerator.Reset() { }
    }
}
