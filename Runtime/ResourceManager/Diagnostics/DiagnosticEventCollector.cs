using System;
using System.Collections.Generic;
// ReSharper disable DelegateSubtraction

#if !UNITY_EDITOR
using UnityEngine.Networking.PlayerConnection;
#endif

namespace UnityEngine.ResourceManagement.Diagnostics
{
    /// <summary>
    /// Collects ResourceManager events and passed them on the registered event handlers.  In editor play mode, events are passed directly to the ResourceManager profiler window.  
    /// In player builds, events are sent to the editor via the EditorConnection API.
    /// </summary>
    public class DiagnosticEventCollector : MonoBehaviour
    {
        /// <summary>
        /// The message id used to register this class with the EditorConnection
        /// </summary>
        /// <value>Guid of message id</value>
        public static Guid EditorConnectionMessageId { get { return new Guid(1, 2, 3, new byte[] { 20, 1, 32, 32, 4, 9, 6, 44 }); } }
        /// <summary>
        /// Get or set whether ResourceManager events are enabled
        /// </summary>
        /// <value>Enabled state of profiler events</value>
        public static bool ResourceManagerProfilerEventsEnabled { get; set; }

        static readonly List<DiagnosticEvent> k_UnhandledEvents = new List<DiagnosticEvent>();
        static Action<DiagnosticEvent> s_EventHandlers;
        static bool s_Initialized;
        static int s_StartFrame = -1;
        static List<int> s_FrameEventCounts = new List<int>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        internal static void SendFirstFrameEvent()
        {
            if (ResourceManagerProfilerEventsEnabled)
                PostEvent(new DiagnosticEvent("EventCount", "", "Events", 0, 0, 0, null));
        }

        internal static void Initialize()
        {
            if (ResourceManagerProfilerEventsEnabled)
            {
                var ec = FindObjectOfType<DiagnosticEventCollector>();
                if (ec == null)
                {
                    var go = new GameObject("EventCollector", typeof(DiagnosticEventCollector));
                    go.hideFlags = HideFlags.HideAndDontSave;
                }
            }
            s_Initialized = true;
        }

        /// <summary>
        /// Register event handler
        /// </summary>
        /// <param name="handler">Method or delegate that will handle the events</param>
        public static void RegisterEventHandler(Action<DiagnosticEvent> handler)
        {
            Debug.Assert(k_UnhandledEvents != null, "DiagnosticEventCollector.RegisterEventHandler - s_unhandledEvents == null.");
            if (handler == null)
                throw new ArgumentNullException("handler");
            s_EventHandlers += handler;
            foreach (var e in k_UnhandledEvents)
                handler(e);
            k_UnhandledEvents.Clear();
        }

        /// <summary>
        /// Unregister event hander
        /// </summary>
        /// <param name="handler">Method or delegate that will handle the events</param>
        public static void UnregisterEventHandler(Action<DiagnosticEvent> handler)
        {
            if (handler == null)
                throw new ArgumentNullException("handler");
            s_EventHandlers -= handler;
        }

        static void CountFrameEvent(int frame)
        {
            Debug.Assert(s_FrameEventCounts != null, "DiagnosticEventCollector.CountFrameEvent - s_frameEventCounts == null.");
            if (frame < s_StartFrame)
                return;
            var index = frame - s_StartFrame;
            while (index >= s_FrameEventCounts.Count)
                s_FrameEventCounts.Add(0);
            s_FrameEventCounts[index]++;
        }

        /// <summary>
        /// Send a <see cref="DiagnosticEvent"/> event to all registered handlers
        /// </summary>
        /// <param name="diagnosticEvent">The event to send</param>
        public static void PostEvent(DiagnosticEvent diagnosticEvent)
        {
            if (!s_Initialized)
                Initialize();

            if (!ResourceManagerProfilerEventsEnabled)
                return;

            Debug.Assert(k_UnhandledEvents != null, "DiagnosticEventCollector.PostEvent - s_unhandledEvents == null.");

            if (s_EventHandlers != null)
                s_EventHandlers(diagnosticEvent);
            else
                k_UnhandledEvents.Add(diagnosticEvent);

            if (diagnosticEvent.EventId != "EventCount")
                CountFrameEvent(diagnosticEvent.Frame);
        }

        void Awake()
        {
#if !UNITY_EDITOR
            RegisterEventHandler((DiagnosticEvent diagnosticEvent) => {PlayerConnection.instance.Send(EditorConnectionMessageId, diagnosticEvent.Serialize()); });
#endif
            SendEventCounts();
            DontDestroyOnLoad(gameObject);
            InvokeRepeating("SendEventCounts", 0, .25f);
        }

        void SendEventCounts()
        {
            Debug.Assert(s_FrameEventCounts != null, "DiagnosticEventCollector.SendEventCounts - s_frameEventCounts == null.");

            int latestFrame = Time.frameCount;

            if (s_StartFrame >= 0)
            {
                while (s_FrameEventCounts.Count < latestFrame - s_StartFrame)
                    s_FrameEventCounts.Add(0);
                for (int i = 0; i < s_FrameEventCounts.Count; i++)
                    PostEvent(new DiagnosticEvent("EventCount", "", "Events", 0, s_StartFrame + i, s_FrameEventCounts[i], null));
            }
            s_StartFrame = latestFrame;
            s_FrameEventCounts.Clear();
        }
    }
}
