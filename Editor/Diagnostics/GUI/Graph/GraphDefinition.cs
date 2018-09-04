using UnityEngine;

namespace UnityEditor.AddressableAssets.Diagnostics
{
    internal class GraphDefinition
    {
        int m_maxValueStream;
        internal IGraphLayer[] layers;
        internal GraphDefinition(int maxValueStream, IGraphLayer[] l)
        {
            layers = l;
            m_maxValueStream = maxValueStream;
        }

        internal int GetMaxValue(EventDataSet e)
        {
            var stream = e.GetStream(m_maxValueStream);
            return stream == null ? 1 : Mathf.Max(10, (stream.m_maxValue / 10 + 1) * 10);
        }
    }
}
