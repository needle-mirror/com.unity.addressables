using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace UnityEditor.AddressableAssets.Diagnostics.Data
{
    [Serializable]
    class EventDataSet
    {
        [FormerlySerializedAs("m_streams")]
        [SerializeField]
        List<EventDataSetStream> m_Streams = new List<EventDataSetStream>();
        int m_FirstSampleFrame = int.MaxValue;
        string m_EventName;
        string m_Graph;
        Dictionary<string, EventDataSet> m_Children;

        public string EventName { get { return m_EventName; } }
        public string Graph { get { return m_Graph; } }
        public IEnumerable<EventDataSet> Children { get { return m_Children.Values; } }
        internal bool HasChildren { get { return m_Children != null && m_Children.Count > 0; } }
        internal int FirstSampleFrame { get { return m_FirstSampleFrame; } }

        internal EventDataSet() { }
        internal EventDataSet(string n, string g)
        {
            m_EventName = n;
            m_Graph = g;
        }

        internal bool HasDataAfterFrame(int frame)
        {
            foreach (var s in m_Streams)
                if (s != null && s.HasDataAfterFrame(frame))
                    return true;
            if (m_Children != null)
            {
                foreach (var c in m_Children)
                    if (c.Value.HasDataAfterFrame(frame))
                        return true;
            }
            return false;
        }

        internal EventDataSet GetDataSet(string entryName, bool create, ref bool entryCreated, string graph)
        {
            if (string.IsNullOrEmpty(entryName))
                return null;
            if (m_Children == null)
            {
                if (!create)
                    return null;
                m_Children = new Dictionary<string, EventDataSet>();
                entryCreated = true;
            }
            EventDataSet entry;
            if (!m_Children.TryGetValue(entryName, out entry) && create)
            {
                m_Children.Add(entryName, entry = new EventDataSet(entryName, graph));
                entryCreated = true;
            }
            return entry;
        }

        internal void AddSample(int stream, int frame, int val)
        {
            if (frame < m_FirstSampleFrame)
                m_FirstSampleFrame = frame;
            while (stream >= m_Streams.Count)
                m_Streams.Add(null);
            if (m_Streams[stream] == null)
                m_Streams[stream] = new EventDataSetStream();
            m_Streams[stream].AddSample(frame, val);
        }

        internal int GetStreamValue(int s, int frame)
        {
            var stream = GetStream(s);
            if (stream == null)
                return 0;
            return stream.GetValue(frame);
        }

        internal EventDataSetStream GetStream(int s)
        {
            if (s >= m_Streams.Count)
                return null;
            return m_Streams[s];
        }

        internal int GetStreamMaxValue(int s)
        {
            var stream = GetStream(s);
            if (stream == null)
                return 0;

            return stream.maxValue;
        }

        internal void Clear()
        {
            m_FirstSampleFrame = int.MaxValue;
            m_Children.Clear();
            m_Streams.Clear();
        }
    }

}