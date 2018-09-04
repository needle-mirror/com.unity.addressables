using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor.AddressableAssets.Diagnostics
{
    [Serializable]
    internal class EventDataSet
    {
        [SerializeField]
        List<EventDataSetStream> m_streams = new List<EventDataSetStream>();
        int m_firstSampleFrame = int.MaxValue;
        string m_name;
        string m_graph;
        Dictionary<string, EventDataSet> m_children = null;

        public string Name { get { return m_name; } }
        public string Graph { get { return m_graph; } }
        public IEnumerable<EventDataSet> Children { get { return m_children.Values; } }
        internal bool HasChildren { get { return m_children != null && m_children.Count > 0; } }
        internal int FirstSampleFrame { get { return m_firstSampleFrame; } }

        internal EventDataSet() { }
        internal EventDataSet(string n, string g)
        {
            m_name = n;
            m_graph = g;
        }

        internal bool HasDataAfterFrame(int frame)
        {
            foreach (var s in m_streams)
                if (s != null && s.HasDataAfterFrame(frame))
                    return true;
            if (m_children != null)
            {
                foreach (var c in m_children)
                    if (c.Value.HasDataAfterFrame(frame))
                        return true;
            }
            return false;
        }

        internal EventDataSet GetDataSet(string entryName, bool create, ref bool entryCreated, string graph)
        {
            if (string.IsNullOrEmpty(entryName))
                return null;
            if (m_children == null)
            {
                if (!create)
                    return null;
                m_children = new Dictionary<string, EventDataSet>();
                entryCreated = true;
            }
            EventDataSet entry = null;
            if (!m_children.TryGetValue(entryName, out entry) && create)
            {
                m_children.Add(entryName, entry = new EventDataSet(entryName, graph));
                entryCreated = true;
            }
            return entry;
        }

        internal void AddSample(int stream, int frame, int val)
        {
            if (frame < m_firstSampleFrame)
                m_firstSampleFrame = frame;
            while (stream >= m_streams.Count)
                m_streams.Add(null);
            if (m_streams[stream] == null)
                m_streams[stream] = new EventDataSetStream();
            m_streams[stream].AddSample(frame, val);
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
            if (s >= m_streams.Count)
                return null;
            return m_streams[s];
        }

        internal int GetStreamMaxValue(int s)
        {
            var stream = GetStream(s);
            if (stream == null)
                return 0;

            return stream.m_maxValue;
        }

        internal void Clear()
        {
            m_firstSampleFrame = int.MaxValue;
            m_children.Clear();
            m_streams.Clear();
        }
    }

}