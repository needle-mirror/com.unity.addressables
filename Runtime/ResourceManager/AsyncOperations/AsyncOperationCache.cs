//#define POST_ASYNCOPERATIONCACHE__EVENTS

using System;
using System.Collections.Generic;

namespace UnityEngine.ResourceManagement.AsyncOperations
{
    /// <summary>
    /// This class allows for recycling IAsyncOperation object in order to reduce GC load.
    /// </summary>
    public class AsyncOperationCache
    {
        /// <summary>
        /// The singleton AsyncOperationCache instance.
        /// </summary>
        public static readonly AsyncOperationCache Instance = new AsyncOperationCache();
        readonly Dictionary<Type, Stack<IAsyncOperation>> m_Cache = new Dictionary<Type, Stack<IAsyncOperation>>();
#if POST_ASYNCOPERATIONCACHE__EVENTS
        class Stats
        {
            internal int m_hits;
            internal int m_misses;
            internal string m_name;
            internal int Value { get { return (int)(((float)m_hits / (m_hits + m_misses)) * 100); } }
        }
        Dictionary<Type, Stats> m_stats = new Dictionary<Type, Stats>();
#endif
        /// <summary>
        /// Release a completed IAsyncOperation back into the cache.  ResetStatus will be called on the operation before it is used again.
        /// </summary>
        /// <param name="operation">The operation to release.</param>
        public void Release(IAsyncOperation operation)
        {
            if (operation == null)
                throw new ArgumentNullException("operation");
            operation.Validate();

            Stack<IAsyncOperation> operationStack;
            if (!m_Cache.TryGetValue(operation.GetType(), out operationStack))
                m_Cache.Add(operation.GetType(), operationStack = new Stack<IAsyncOperation>(5));
            operationStack.Push(operation);

#if POST_ASYNCOPERATIONCACHE__EVENTS
            Stats stat;
            if (!m_stats.TryGetValue(operation.GetType(), out stat))
                m_stats.Add(operation.GetType(), stat = new Stats() { m_name = string.Format("AsyncOperationCache[{0}]", operation.GetType().Name) });
            ResourceManagerEventCollector.PostEvent(ResourceManagerEventCollector.EventType.AsyncOpCacheCount, stat.m_name, operationStack.Count);
#endif
        }

        /// <summary>
        /// Acquire an IAsyncOperation.
        /// </summary>
        /// <typeparam name="TAsyncOperation">The type of IAsyncOperation to be returned.</typeparam>
        /// <returns>An IAsyncOperation of type TAsyncOperation.</returns>
        public TAsyncOperation Acquire<TAsyncOperation>()
            where TAsyncOperation : IAsyncOperation, new()
        {
            Debug.Assert(m_Cache != null, "AsyncOperationCache.Acquire - m_cache == null.");

            Stack<IAsyncOperation> operationStack;
#if POST_ASYNCOPERATIONCACHE__EVENTS
            Stats stat;
            if (!m_stats.TryGetValue(typeof(TAsyncOperation), out stat))
                m_stats.Add(typeof(TAsyncOperation), stat = new Stats() { m_name = string.Format("AsyncOperationCache[{0}]", typeof(TAsyncOperation).Name) });
#endif
            if (m_Cache.TryGetValue(typeof(TAsyncOperation), out operationStack) && operationStack.Count > 0)
            {
                var op = (TAsyncOperation)operationStack.Pop();
                op.IsValid = true;
                op.ResetStatus();
#if POST_ASYNCOPERATIONCACHE__EVENTS
                stat.m_hits++;
                ResourceManagerEventCollector.PostEvent(ResourceManagerEventCollector.EventType.AsyncOpCacheHitRatio, stat.m_name, stat.Value);
                ResourceManagerEventCollector.PostEvent(ResourceManagerEventCollector.EventType.AsyncOpCacheCount, stat.m_name, operationStack.Count);
#endif
                return op;
            }
#if POST_ASYNCOPERATIONCACHE__EVENTS
            stat.m_misses++;
            ResourceManagerEventCollector.PostEvent(ResourceManagerEventCollector.EventType.AsyncOpCacheHitRatio, stat.m_name, stat.Value);
#endif
            var op2 = new TAsyncOperation();
            op2.IsValid = true;
            op2.ResetStatus();
            return op2;
        }
        /// <summary>
        /// Clear all cached IAsyncOperation object.
        /// </summary>
        public void Clear()
        {
            Debug.Assert(m_Cache != null, "AsyncOperationCache.Clear - m_cache == null.");
            m_Cache.Clear();
        }
    }
}
