using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.Initialization;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace UnityEngine.ResourceManagement.AsyncOperations
{
    internal class InitalizationObjectsOperation : AsyncOperationBase<bool>
    {
        private AsyncOperationHandle<ResourceManagerRuntimeData> m_RtdOp;
        private AddressablesImpl m_Addressables;
        private AsyncOperationHandle<IList<AsyncOperationHandle>> m_DepOp;

        public void Init(AsyncOperationHandle<ResourceManagerRuntimeData> rtdOp, AddressablesImpl addressables)
        {
            m_RtdOp = rtdOp;
            m_Addressables = addressables;
        }

        protected override void Execute()
        {
            var rtd = m_RtdOp.Result;

            List<AsyncOperationHandle> initOperations = new List<AsyncOperationHandle>();
            foreach (var i in rtd.InitializationObjects)
            {
                if (i.ObjectType.Value == null)
                {
                    Addressables.LogFormat("Invalid initialization object type {0}.", i.ObjectType);
                    continue;
                }

                try
                {
                    var o = i.GetAsyncInitHandle(m_Addressables.ResourceManager);
                    initOperations.Add(o);
                    Addressables.LogFormat("Initialization object {0} created instance {1}.", i, o);
                }
                catch (Exception ex)
                {
                    Addressables.LogErrorFormat("Exception thrown during initialization of object {0}: {1}", i,
                        ex.ToString());
                }
            }

            m_DepOp = m_Addressables.ResourceManager.CreateGenericGroupOperation(initOperations, true);
            m_DepOp.Completed += (obj) =>
            {
                bool success = obj.Status == AsyncOperationStatus.Succeeded;
                Complete(true, success, success ? "" : $"{obj.DebugName} failed initialization.");
                m_Addressables.Release(m_DepOp);
            };
        }
    }
}