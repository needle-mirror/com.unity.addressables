using System;
using System.Collections.Generic;

namespace UnityEditor.AddressableAssets.Diagnostics
{
    [Serializable]
    internal class EventDataSetStream
    {
        internal int m_maxValue = 0;
        internal List<EventDataSample> m_samples = new List<EventDataSample>();
        internal void AddSample(int frame, int val)
        {
            if (val > m_maxValue)
                m_maxValue = val;
            m_samples.Add(new EventDataSample(frame, val));
        }

        internal int GetValue(int f)
        {
            if (m_samples.Count == 0 || f < m_samples[0].frame)
                return 0;
            if (f >= m_samples[m_samples.Count - 1].frame)
                return m_samples[m_samples.Count - 1].data;
            for (int i = 1; i < m_samples.Count; i++)
            {
                if (m_samples[i].frame > f)
                    return m_samples[i - 1].data;
            }
            return m_samples[0].data;
        }

        internal bool HasDataAfterFrame(int frame)
        {
            if (m_samples.Count == 0)
                return false;
            EventDataSample lastSample = m_samples[m_samples.Count - 1];
            return lastSample.frame > frame || lastSample.data > 0;
        }
    }

}