using UnityEngine;
namespace UnityEditor.AddressableAssets.Diagnostics
{
    internal class GraphLayerBase : IGraphLayer
    {
        string m_name;
        string m_description;
        Color m_color;
        public int Stream { get; private set; }

        public GraphLayerBase(int stream, string name, string description, Color color)
        {
            Stream = stream;
            m_name = name;
            m_description = description;
            m_color = color;
        }

        public Color GraphColor { get { return m_color; } }

        public string Name { get { return m_name; } }

        public string Description { get { return m_description; } }

        public virtual void Draw(EventDataSet dataSet, Rect rect, int startFrame, int frameCount, int inspectFrame, bool expanded, Material material, int maxValue)
        {
        }
    }
}
