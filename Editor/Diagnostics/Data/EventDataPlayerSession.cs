using System;
using System.Collections.Generic;
using UnityEngine.ResourceManagement.Diagnostics;

namespace UnityEditor.AddressableAssets.Diagnostics
{
    [Serializable]
    internal class EventDataPlayerSession
    {
        EventDataSet m_rootStreamEntry = new EventDataSet("Root", "");
        string m_name;
        int m_playerId;
        bool m_isActive;
        int m_latestFrame = 0;
        int m_startFrame = 0;
        int m_frameCount = 300;
        Dictionary<int, List<DiagnosticEvent>> m_frameEvents = new Dictionary<int, List<DiagnosticEvent>>();

        public EventDataSet RootStreamEntry { get { return m_rootStreamEntry; } }
        public string Name { get { return m_name; } }
        public int PlayerId { get { return m_playerId; } }
        public bool IsActive { get { return m_isActive; } set { m_isActive = value; } }
        public int LatestFrame { get { return m_latestFrame; } }
        public int StartFrame { get { return m_startFrame; } }
        public int FrameCount { get { return m_frameCount; } }


        public EventDataPlayerSession() { }
        public EventDataPlayerSession(string name, int playerId)
        {
            m_name = name;
            m_playerId = playerId;
            m_isActive = true;
        }

        internal void Clear()
        {
            RootStreamEntry.Clear();
            m_frameEvents.Clear();
        }

        internal List<DiagnosticEvent> GetFrameEvents(int frame)
        {
            List<DiagnosticEvent> frameEvents = null;
            if (m_frameEvents.TryGetValue(frame, out frameEvents))
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
            m_latestFrame = evt.Frame;
            m_startFrame = m_latestFrame - m_frameCount;

            if (recordEvent)
            {
                List<DiagnosticEvent> frameEvents = null;
                if (!m_frameEvents.TryGetValue(evt.Frame, out frameEvents))
                    m_frameEvents.Add(evt.Frame, frameEvents = new List<DiagnosticEvent>());
                frameEvents.Add(evt);
            }

            var ds = GetDataSet(evt.Parent, evt.EventId, ref entryCreated, evt.Graph);
            ds.AddSample(evt.Stream, evt.Frame, evt.Value);
        }

    }
}