using System;
using UnityEngine;

namespace UnityEditor.AddressableAssets.Diagnostics
{
    interface IGraphLayer
    {
        string LayerName { get; }
        string Description { get; }
        Color GraphColor { get; }
        void Draw(EventDataSet dataSet, Rect rect, int startFrame, int frameCount, int inspectFrame, bool expanded, Material material, int maxValue);
    }
}
