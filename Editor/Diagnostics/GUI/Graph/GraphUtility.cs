using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor.AddressableAssets.Diagnostics
{
    internal static class GraphUtility
    {
        public static float ValueToPixel(float value, float min, float max, float pixelRange)
        {
            return Mathf.Clamp01((value - min) / (max - min)) * pixelRange;
        }

        public static float ValueToPixelUnclamped(float value, float min, float max, float pixelRange)
        {
            return ((value - min) / (max - min)) * pixelRange;
        }

        public static float PixelToValue(float pixel, float min, float max, float valueRange)
        {
            return Mathf.Clamp01((pixel - min) / (max - min)) * valueRange;
        }

        public struct Segment
        {
            public int frameStart;
            public int frameEnd;
            public int data;
        }

        public delegate bool IsContinuationOfSegmentDelegate(int prevData, int newData);

        public static IEnumerable<Segment> IterateSegments(EventDataSetStream stream, int minFrame, int maxFrame, IsContinuationOfSegmentDelegate segmentCallback)
        {
            if (stream.m_samples.Count > 0)
            {
                // find last visible event. This can be the event that is right before the minFrame
                int segStartIdx;
                for (segStartIdx = stream.m_samples.Count - 1; segStartIdx > 0; segStartIdx--)
                    if (stream.m_samples[segStartIdx].frame < minFrame)
                        break;

                int curIdx = segStartIdx + 1;

                for (; curIdx < stream.m_samples.Count; curIdx++)
                {
                    // keep iterating samples until the callback tells us this should be reported as a new segment
                    if (segmentCallback(stream.m_samples[segStartIdx].data, stream.m_samples[curIdx].data))
                    {
                        Segment segment;
                        segment.frameStart = Math.Max(stream.m_samples[segStartIdx].frame, minFrame);
                        segment.frameEnd = stream.m_samples[curIdx].frame;
                        segment.data = stream.m_samples[segStartIdx].data;
                        yield return segment;
                        // start working on a new segment from the current location
                        segStartIdx = curIdx;
                    }
                }

                // close off the last segment all the way to the end of the maxFrame
                Segment lastSegment;
                lastSegment.frameStart = Math.Max(stream.m_samples[segStartIdx].frame, minFrame);
                lastSegment.frameEnd = maxFrame;
                lastSegment.data = stream.m_samples[segStartIdx].data;
                yield return lastSegment;
            }
        }
    }
}
