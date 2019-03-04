using System;
using System.Text;
using UnityEngine.ResourceManagement.Diagnostics;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace UnityEngine.ResourceManagement.Util
{
    /// <summary>
    /// This class defines the category and event types for the ResourceManager
    /// </summary>
    public static class ResourceManagerEventCollector
    {
        /// <summary>
        /// Category for all ResourceManager events
        /// </summary>
        public const string EventCategory = "ResourceManagerEvent";

        /// <summary>
        /// Event types that will be sent by the ResourceManager
        /// </summary>
        public enum EventType
        {
            None,
            FrameCount,
            LoadAsyncRequest,
            LoadAsyncCompletion,
            Release,
            InstantiateAsyncRequest,
            InstantiateAsyncCompletion,
            ReleaseInstance,
            LoadSceneAsyncRequest,
            LoadSceneAsyncCompletion,
            ReleaseSceneAsyncRequest,
            ReleaseSceneAsyncCompletion,
            CacheEntryRefCount,
            CacheEntryLoadPercent,
            PoolCount,
            DiagnosticEvents,
            CacheLruCount,
            AsyncOpCacheHitRatio,
            AsyncOpCacheCount
        }

        
        static string PrettyPath(string p, bool keepExtension)
        {
            var slashIndex = p.LastIndexOf('/');
            if (slashIndex > 0)
                p = p.Substring(slashIndex + 1);
            if (!keepExtension)
            {
                slashIndex = p.LastIndexOf('.');
                if (slashIndex > 0)
                    p = p.Substring(0, slashIndex);
            }
            return p;
        }

        /// <summary>
        /// Send an event to all registered event handlers
        /// </summary>
        /// <param name="type">The event type.</param>
        /// <param name="context">The context of the event. If this is an IResourceLocation, information will be passed along in the event data field.</param>
        /// <param name="eventValue">The value of the event.</param>
        public static void PostEvent(EventType type, object context, int eventValue)
        {
            if (!DiagnosticEventCollector.ResourceManagerProfilerEventsEnabled)
                return;
            var parent = "";
            var id = context.ToString();
            byte[] data = null;
            var loc = context as IResourceLocation;
            if (loc != null)
            {
                id = PrettyPath(loc.InternalId, false);
                var sb = new StringBuilder(256);
                sb.Append(loc.ProviderId.Substring(loc.ProviderId.LastIndexOf('.') + 1));
                sb.Append('!');
                sb.Append(loc.InternalId);
                sb.Append('!');
                if (loc.HasDependencies)
                {
                    parent = PrettyPath(loc.Dependencies[0].InternalId, false);
                    for (int i = 0; i < loc.Dependencies.Count; i++)
                    {
                        sb.Append(PrettyPath(loc.Dependencies[i].InternalId, true));
                        sb.Append(',');
                    }
                }
                data = Encoding.ASCII.GetBytes(sb.ToString());
            }
            var category = type >= EventType.DiagnosticEvents ? type.ToString() : EventCategory;
            DiagnosticEventCollector.PostEvent(new DiagnosticEvent(category, parent, id, (int)type, Time.frameCount, eventValue, data));
        }
    }
}
