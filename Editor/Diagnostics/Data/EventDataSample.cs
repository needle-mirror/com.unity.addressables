using System;

namespace UnityEditor.AddressableAssets.Diagnostics
{
    [Serializable]
    internal struct EventDataSample
    {
        int m_frame;
        int m_data;
        internal int frame { get { return m_frame; } }
        internal int data { get { return m_data; } }

        internal EventDataSample(int frame, int value)
        {
            m_frame = frame;
            m_data = value;
        }
    }
}