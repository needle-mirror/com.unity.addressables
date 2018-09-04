using UnityEngine;
using System.Collections.Generic;

namespace UnityEditor.AddressableAssets.Diagnostics
{
    internal class GraphLayerBarChartMesh : GraphLayerBase
    {
        Mesh m_mesh;
        List<Vector3> m_verts = new List<Vector3>();
        List<int> m_indices = new List<int>();
        List<Color32> m_colors = new List<Color32>();

        Rect m_bounds;
        Vector2 m_gridSize;

        public GraphLayerBarChartMesh(int stream, string name, string description, Color color) : base(stream, name, description, color) { }
        private void AddQuadToMesh(float left, float right, float bot, float top)
        {
            float xLeft = m_bounds.xMin + left * m_gridSize.x;
            float xRight = m_bounds.xMin + right * m_gridSize.x;
            float yBot = m_bounds.yMax - bot * m_gridSize.y;
            float yTop = m_bounds.yMax - top * m_gridSize.y;

            int start = m_verts.Count;
            m_verts.Add(new Vector3(xLeft, yBot, 0));
            m_verts.Add(new Vector3(xLeft, yTop, 0));
            m_verts.Add(new Vector3(xRight, yTop, 0));
            m_verts.Add(new Vector3(xRight, yBot, 0));

            m_indices.Add(start);
            m_indices.Add(start + 1);
            m_indices.Add(start + 2);

            m_indices.Add(start);
            m_indices.Add(start + 2);
            m_indices.Add(start + 3);
        }

        public override void Draw(EventDataSet dataSet, Rect rect, int startFrame, int frameCount, int inspectFrame, bool expanded, Material material, int maxValue)
        {
            if (dataSet == null || material == null)
                return;

            var stream = dataSet.GetStream(Stream);
            if (stream != null && stream.m_samples.Count > 0)
            {
                material.color = GraphColor;

                if (m_mesh == null)
                    m_mesh = new Mesh();
                m_verts.Clear();
                m_indices.Clear();
                m_colors.Clear();
                var endTime = startFrame + frameCount;

                m_bounds = new Rect(rect);
                m_gridSize.x = m_bounds.width / (float)frameCount;
                m_gridSize.y = m_bounds.height / maxValue;

                int previousFrameNumber = endTime;
                int currentFrame = endTime;

                for (int i = stream.m_samples.Count - 1; i >= 0 && currentFrame > startFrame; --i)
                {
                    currentFrame = stream.m_samples[i].frame;
                    var frame = Mathf.Max(currentFrame, startFrame);
                    if (stream.m_samples[i].data > 0)
                    {
                        AddQuadToMesh(frame - startFrame, previousFrameNumber - startFrame, 0, stream.m_samples[i].data);
                    }
                    previousFrameNumber = frame;
                }

                if (m_verts.Count > 0)
                {
                    m_mesh.Clear(true);
                    m_mesh.SetVertices(m_verts);
                    m_mesh.triangles = m_indices.ToArray();
                    material.SetPass(0);
                    Graphics.DrawMeshNow(m_mesh, Vector3.zero, Quaternion.identity);
                }
            }
        }
    }
}