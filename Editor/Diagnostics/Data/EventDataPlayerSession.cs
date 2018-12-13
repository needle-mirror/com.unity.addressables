using System;
using System.Collections.Generic;
using UnityEngine.ResourceManagement.Diagnostics;

namespace UnityEditor.AddressableAssets.Diagnostics
{
    [Serializable]
    class EventDataPlayerSession
    {
        EventDataSet m_RootStreamEntry = new EventDataSet("Root", "");
        string m_EventName;
        int m_PlayerId;
        bool m_IsActive;
        int m_LatestFrame;
        int m_StartFrame;
        int m_FrameCount = 300;
        Dictionary<int, List<DiagnosticEvent>> m_FrameEvents = new Dictionary<int, List<DiagnosticEvent>>();

        public EventDataSet RootStreamEntry { get { return m_RootStreamEntry; } }
        public string EventName { get { return m_EventName; } }
        public int PlayerId { get { return m_PlayerId; } }
        public bool IsActive { get { return m_IsActive; } set { m_IsActive = value; } }
        public int LatestFrame { get { return m_LatestFrame; } }
        public int StartFrame { get { return m_StartFrame; } }
        public int FrameCount { get { return m_FrameCount; } }


        public EventDataPlayerSession() { }
        public EventDataPlayerSession(string eventName, int playerId)
        {
            m_EventName = eventName;
            m_PlayerId = playerId;
            m_IsActive = true;
        }

        internal void Clear()
        {
            RootStreamEntry.Clear();
            m_FrameEvents.Clear();
        }

        internal List<DiagnosticEvent> GetFrameEvents(int frame)
        {
            List<DiagnosticEvent> frameEvents;
            if (m_FrameEvents.TryGetValue(frame, out frameEvents))
                return frameEvents;
            return null;
        }

        EventDataSet GetDataSet(string parentName, string name, ref bool entryCreated, string graph)
        {
            EventDataSet parent = RootStreamEntry.GetDataSet(parentName, true, ref entryCreated, graph);
            if (parent == null)
                parent = RootStreamEntry;
            return parent.GetDataSet(name, true, ref entryCreated, graph);
        }

        internal void AddSample(DiagnosticEvent evt, bool recordEvent, ref bool entryCreated)
        {
            m_LatestFrame = evt.Frame;
            m_StartFrame = m_LatestFrame - m_FrameCount;

            if (recordEvent)
            {
                List<DiagnosticEvent> frameEvents;
                if (!m_FrameEvents.TryGetValue(evt.Frame, out frameEvents))
                    m_FrameEvents.Add(evt.Frame, frameEvents = new List<DiagnosticEvent>());
                frameEvents.Add(evt);
            }

            var ds = GetDataSet(evt.Parent, evt.EventId, ref entryCreated, evt.Graph);
            ds.AddSample(evt.Stream, evt.Frame, evt.Value);
        }

    }
}