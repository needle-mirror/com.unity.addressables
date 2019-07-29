using System;
using System.Collections.Generic;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.ResourceManagement.Util;

namespace UnityEngine.ResourceManagement.AsyncOperations
{

    class GroupOperation : AsyncOperationBase<IList<AsyncOperationHandle>>, ICachable
    {
        List<AsyncOperationHandle> m_Result;
        Action<AsyncOperationHandle> m_InternalOnComplete;
        int m_LoadedCount;
        public GroupOperation()
        {
            m_InternalOnComplete = OnOperationCompleted;
            m_Result = new List<AsyncOperationHandle>();
        }

        int ICachable.Hash { get; set; }

        internal IList<AsyncOperationHandle> GetDependentOps()
        {
            return m_Result;
        }

        protected override void GetDependencies(List<AsyncOperationHandle> deps)
        {
            deps.AddRange(m_Result);
        }

        protected override string DebugName { get { return "Dependencies"; } }

        protected override void Execute()
        {
            m_LoadedCount = 0;
            for (int i = 0; i < m_Result.Count; i++)
            {
                if (m_Result[i].IsDone)
                    m_LoadedCount++;
                else
                    m_Result[i].Completed += m_InternalOnComplete;
            }
            CompleteIfDependenciesComplete();
        }

        private void CompleteIfDependenciesComplete()
        {
            if (m_LoadedCount == m_Result.Count)
            {
                bool success = true;
                string errorMsg = string.Empty;
                for (int i = 0; i < m_Result.Count; i++)
                {
                    if (m_Result[i].Status != AsyncOperationStatus.Succeeded)
                    {
                        success = false;
                        errorMsg = m_Result[i].OperationException != null ? m_Result[i].OperationException.Message : string.Empty;
                        break;
                    }
                }
                Complete(m_Result, success, errorMsg);
            }
        }

        protected override void Destroy()
        {
            for (int i = 0; i < m_Result.Count; i++)
                m_Result[i].Release();
            m_Result.Clear();
        }

        protected override float Progress
        {
            get
            {
                List<AsyncOperationHandle> allDependentOperations = new List<AsyncOperationHandle>();
                allDependentOperations.AddRange(m_Result);

                foreach(var handle in m_Result)
                    handle.GetDependencies(allDependentOperations);

                if (m_Result.Count < 1)
                    return 1f;
                float total = 0;
                for (int i = 0; i < allDependentOperations.Count; i++)
                    total += allDependentOperations[i].PercentComplete;
                return total / allDependentOperations.Count;
            }
        }


        public void Init(List<AsyncOperationHandle> operations)
        {
            m_Result = new List<AsyncOperationHandle>(operations);
        }

        void OnOperationCompleted(AsyncOperationHandle op)
        {
            m_LoadedCount++;
            CompleteIfDependenciesComplete();
        }
    }
}