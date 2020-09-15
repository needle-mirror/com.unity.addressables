using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.Diagnostics;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace UnityEngine.AddressableAssets.Utility
{
    internal class DiagnosticInfo
    {
        public string DisplayName;
        public int ObjectId;
        public int[] Dependencies;
        public DiagnosticEvent CreateEvent(string category, ResourceManager.DiagnosticEventType eventType, int frame, int val)
        {
            return new DiagnosticEvent(category, DisplayName, ObjectId, (int)eventType, frame, val, Dependencies);
        }
    }

    internal class ResourceManagerDiagnostics : IDisposable
    {
        ResourceManager m_ResourceManager;

        /// <summary>
        /// This class is responsible for passing events from the resource manager to the event collector,
        /// </summary>
        /// <param name="resourceManager"></param>
        public ResourceManagerDiagnostics(ResourceManager resourceManager)
        {
            resourceManager.RegisterDiagnosticCallback(OnResourceManagerDiagnosticEvent);
            m_ResourceManager = resourceManager;
        }

        Dictionary<int, DiagnosticInfo> m_cachedDiagnosticInfo = new Dictionary<int, DiagnosticInfo>();

        internal int SumDependencyNameHashCodes(AsyncOperationHandle handle)
        {
            List<AsyncOperationHandle> deps = new List<AsyncOperationHandle>();
            handle.GetDependencies(deps);
            
            int sumOfDependencyHashes = 0;
            foreach (var d in deps)
                unchecked
                {
                    sumOfDependencyHashes += d.DebugName.GetHashCode() + SumDependencyNameHashCodes(d);
                }
            return sumOfDependencyHashes;
        }

        internal int CalculateHashCode(AsyncOperationHandle handle)
        {
            int sumOfDependencyHashes = SumDependencyNameHashCodes(handle);
            bool nameChangesWithState = handle.DebugName.Contains("result=") && handle.DebugName.Contains("status=");
            
            // We default to the regular hash code in the case of operations with names that change with their state
            // since their names aren't a reliable way to reference them
            
            if (nameChangesWithState)
                return handle.GetHashCode();
            //its okay if this overflows
            unchecked
            {
                return handle.DebugName.GetHashCode() + sumOfDependencyHashes;
            }
        }

        void OnResourceManagerDiagnosticEvent(ResourceManager.DiagnosticEventContext eventContext)
        {
            var hashCode = CalculateHashCode(eventContext.OperationHandle);
            DiagnosticInfo diagInfo = null;

            if (eventContext.Type == ResourceManager.DiagnosticEventType.AsyncOperationDestroy)
            {
                if (m_cachedDiagnosticInfo.TryGetValue(hashCode, out diagInfo))
                    m_cachedDiagnosticInfo.Remove(hashCode);
            }
            else
            {
                if (!m_cachedDiagnosticInfo.TryGetValue(hashCode, out diagInfo))
                {
                    List<AsyncOperationHandle> deps = new List<AsyncOperationHandle>();
                    eventContext.OperationHandle.GetDependencies(deps);
                    var depIds = new int[deps.Count];
                    
                    for (int i = 0; i < depIds.Length; i++)
                        depIds[i] = CalculateHashCode(deps[i]);
                    
                    m_cachedDiagnosticInfo.Add(hashCode, diagInfo = new DiagnosticInfo() { ObjectId = hashCode, DisplayName = eventContext.OperationHandle.DebugName, Dependencies = depIds });
                }
            }

            if (diagInfo != null)
                DiagnosticEventCollectorSingleton.Instance.PostEvent(diagInfo.CreateEvent("ResourceManager", eventContext.Type, Time.frameCount, eventContext.EventValue));
        }

        public void Dispose()
        {
            m_ResourceManager?.UnregisterDiagnosticCallback(OnResourceManagerDiagnosticEvent);
            if (DiagnosticEventCollectorSingleton.Exists)
                DiagnosticEventCollectorSingleton.DestroySingleton();
        }
    }
}
