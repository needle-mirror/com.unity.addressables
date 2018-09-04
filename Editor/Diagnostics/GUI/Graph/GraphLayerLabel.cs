using UnityEngine;

namespace UnityEditor.AddressableAssets.Diagnostics
{
    internal class GraphLayerLabel : GraphLayerBase
    {
        System.Func<int, string> m_labelFunc;
        Color m_bgColor;
        internal GraphLayerLabel(int stream, string name, string desc, Color color, Color bgColor, System.Func<int, string> func) : base(stream, name, desc, color) { m_labelFunc = func; m_bgColor = bgColor; }
        public override void Draw(EventDataSet dataSet, Rect rect, int startFrame, int frameCount, int inspectFrame, bool expanded, Material material, int maxValue)
        {
            if (dataSet == null)
                return;

            var endTime = startFrame + frameCount;
            var stream = dataSet.GetStream(Stream);
            if (stream != null)
            {
                var prevCol = GUI.color;
                GUI.color = GraphColor;
                if (expanded)
                {
                    var text = new GUIContent(maxValue.ToString());
                    var size = GUI.skin.label.CalcSize(text);
                    var labelRect = new Rect(rect.xMin + 2, rect.yMin, size.x, size.y);
                    EditorGUI.LabelField(labelRect, text);
                    labelRect = new Rect(rect.xMax - size.x, rect.yMin, size.x, size.y);
                    EditorGUI.LabelField(labelRect, text);
                }

                if (inspectFrame != endTime)
                {
                    var val = stream.GetValue(inspectFrame);
                    if (val > 0)
                    {
                        var text = new GUIContent(m_labelFunc(val));
                        var size = GUI.skin.label.CalcSize(text);
                        var x = GraphUtility.ValueToPixel(inspectFrame, startFrame, endTime, rect.width);
                        float pixelVal = GraphUtility.ValueToPixel(val, 0, maxValue, rect.height);
                        var labelRect = new Rect(rect.xMin + x + 5, rect.yMax - (pixelVal + size.y), size.x, size.y);
                        GUI.DrawTexture(labelRect, EditorGUIUtility.whiteTexture, ScaleMode.StretchToFill, true, 0, m_bgColor, 50, 5);
                        EditorGUI.LabelField(labelRect, text, GUI.skin.label);
                    }
                }
                GUI.color = prevCol;
            }
        }
    }
}
