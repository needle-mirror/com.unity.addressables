using System;
using System.Collections.Generic;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.ResourceManagement.Util;

namespace UnityEngine.ResourceManagement.AsyncOperations
{
    class GroupOperation : AsyncOperationBase<IList<AsyncOperationHandle>>, ICachable
    {
        Action<AsyncOperationHandle> m_InternalOnComplete;
        int m_LoadedCount;
        bool m_ReleaseDependenciesOnFailure = true;

        public GroupOperation()
        {
            m_InternalOnComplete = OnOperationCompleted;
            Result = new List<AsyncOperationHandle>();
        }

        int ICachable.Hash { get; set; }

        internal IList<AsyncOperationHandle> GetDependentOps()
        {
            return Result;
        }

        protected override void GetDependencies(List<AsyncOperationHandle> deps)
        {
            deps.AddRange(Result);
        }

        protected override string DebugName { get { return "Dependencies"; } }

        protected override void Execute()
        {
            m_LoadedCount = 0;
            for (int i = 0; i < Result.Count; i++)
            {
                if (Result[i].IsDone)
                    m_LoadedCount++;
                else
                    Result[i].Completed += m_InternalOnComplete;
            }
            CompleteIfDependenciesComplete();
        }

        private void CompleteIfDependenciesComplete()
        {
            if (m_LoadedCount == Result.Count)
            {
                bool success = true;
                string errorMsg = string.Empty;
                for (int i = 0; i < Result.Count; i++)
                {
                    if (Result[i].Status != AsyncOperationStatus.Succeeded)
                    {
                        success = false;
                        errorMsg = Result[i].OperationException != null ? Result[i].OperationException.Message : string.Empty;
                        break;
                    }
                }
                Complete(Result, success, errorMsg, m_ReleaseDependenciesOnFailure);
            }
        }

        protected override void Destroy()
        {
            for (int i = 0; i < Result.Count; i++)
                if (Result[i].IsValid())
                    Result[i].Release();
            Result.Clear();
        }

        protected override float Progress
        {
            get
            {
                float total = 0f;
                for (int i = 0; i < Result.Count; i++)
                {
                    var handle = Result[i];
                    if (!handle.IsDone)
                        total += handle.PercentComplete;
                    else
                        total++;
                }

                return total / Result.Count;
            }
        }


        public void Init(List<AsyncOperationHandle> operations, bool releaseDependenciesOnFailure = true)
        {
            Result = new List<AsyncOperationHandle>(operations);
            m_ReleaseDependenciesOnFailure = releaseDependenciesOnFailure;
        }

        void OnOperationCompleted(AsyncOperationHandle op)
        {
            m_LoadedCount++;
            CompleteIfDependenciesComplete();
        }
    }
}
