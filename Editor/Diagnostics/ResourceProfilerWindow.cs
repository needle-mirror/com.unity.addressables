using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ResourceManagement.Diagnostics;

namespace UnityEditor.AddressableAssets.Diagnostics
{
    /*
     * ResourceManager specific implementation of an EventViewerWindow
     */
    internal class ResourceProfilerWindow : EventViewerWindow
    {
        [MenuItem("Window/Asset Management/Addressable Profiler", priority = 2051)]
        static void ShowWindow()
        {
            var window = GetWindow<ResourceProfilerWindow>();
            window.titleContent = new GUIContent("Addressable Profiler", "Addressable Profiler");
            window.Show();
        }

        protected override bool ShowEventDetailPanel { get { return false; } }
        protected override bool ShowEventPanel { get { return true; } }

        protected static string GetDataStreamName(int stream)
        {
            return ((ResourceManagerEventCollector.EventType)stream).ToString();
        }

        protected override bool OnCanHandleEvent(string graph)
        {
            return graph == ResourceManagerEventCollector.EventCategory;
        }

        protected override bool OnRecordEvent(DiagnosticEvent evt)
        {
            if (evt.Graph == ResourceManagerEventCollector.EventCategory)
            {
                switch ((ResourceManagerEventCollector.EventType)evt.Stream)
                {
                    case ResourceManagerEventCollector.EventType.LoadAsyncRequest:
                    case ResourceManagerEventCollector.EventType.LoadAsyncCompletion:
                    case ResourceManagerEventCollector.EventType.Release:
                    case ResourceManagerEventCollector.EventType.InstantiateAsyncRequest:
                    case ResourceManagerEventCollector.EventType.InstantiateAsyncCompletion:
                    case ResourceManagerEventCollector.EventType.ReleaseInstance:
                    case ResourceManagerEventCollector.EventType.LoadSceneAsyncRequest:
                    case ResourceManagerEventCollector.EventType.LoadSceneAsyncCompletion:
                    case ResourceManagerEventCollector.EventType.ReleaseSceneAsyncRequest:
                    case ResourceManagerEventCollector.EventType.ReleaseSceneAsyncCompletion:
                        return true;
                }
            }
            return base.OnRecordEvent(evt);
        }

        protected override void OnDrawEventDetail(Rect rect, DiagnosticEvent evt)
        {
            if (Event.current.type != EventType.Repaint)
                return;
            var dataListText = System.Text.Encoding.ASCII.GetString(evt.Data);
            if (dataListText == null)
                return;
            var dataList = dataListText.Split(new char[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (dataList[1].EndsWith(".bundle"))
            {
                EditorGUI.TextArea(rect, "No preview available for AssetBundle");
            }
            else
            {
                var obj = AssetDatabase.LoadAssetAtPath<GameObject>(dataList[1]);
                if (obj != null)
                {
                    var tex = AssetPreview.GetAssetPreview(obj);
                    EditorGUI.DrawPreviewTexture(rect, tex);
                }
            }
        }

        protected override void OnGetColumns(List<string> columnNames, List<float> columnSizes)
        {
            if (columnNames == null || columnSizes == null)
                return;
            columnNames.AddRange(new string[] { "Event", "Key", "Provider", "Path", "Dependencies" });
            columnSizes.AddRange(new float[] { 150, 150, 200, 300, 400 });
        }

        protected override bool OnDrawColumnCell(Rect cellRect, DiagnosticEvent evt, int column)
        {
            switch (column)
            {
                case 0: EditorGUI.LabelField(cellRect, ((ResourceManagerEventCollector.EventType)evt.Stream).ToString()); break;
                case 1: EditorGUI.LabelField(cellRect, evt.EventId); break;
                default:
                    {
                        column -= 2;    //need to account for 2 columns that use build in fields
                        if (evt.Data != null && evt.Data.Length > 0)
                        {
                            var dataListText = System.Text.Encoding.ASCII.GetString(evt.Data);
                            if (dataListText == null)
                                return false;
                            var dataList = dataListText.Split(new char[] { '!' }, System.StringSplitOptions.RemoveEmptyEntries);
                            if (dataList == null || column >= dataList.Length)
                                return false;
                            EditorGUI.LabelField(cellRect, dataList[column]);
                        }
                    }
                    break;
            }

            return true;
        }

        protected override void OnInitializeGraphView(EventGraphListView graphView)
        {
            if (graphView == null)
                return;

            Color labelBGColor = GraphColors.LabelGraphLabelBackground;

            Color refCountBGColor = new Color(53 / 255f, 136 / 255f, 167 / 255f, 1);
            Color loadingBGColor = Color.Lerp(refCountBGColor, GraphColors.WindowBackground, 0.5f);

            graphView.DefineGraph(ResourceManagerEventCollector.EventCategory, (int)ResourceManagerEventCollector.EventType.CacheEntryRefCount,
                new GraphLayerBackgroundGraph((int)ResourceManagerEventCollector.EventType.CacheEntryRefCount, refCountBGColor, (int)ResourceManagerEventCollector.EventType.CacheEntryLoadPercent, loadingBGColor, "LoadPercent", "Loaded"),
                new GraphLayerBarChartMesh((int)ResourceManagerEventCollector.EventType.CacheEntryRefCount, "RefCount", "Reference Count", new Color(123 / 255f, 158 / 255f, 6 / 255f, 1)),
                new GraphLayerBarChartMesh((int)ResourceManagerEventCollector.EventType.PoolCount, "PoolSize", "Object Pool Count", new Color(204 / 255f, 113 / 255f, 0, .75f)),
                new GraphLayerEventMarker((int)ResourceManagerEventCollector.EventType.CacheEntryLoadPercent, "", "", Color.white, Color.black),
                new GraphLayerLabel((int)ResourceManagerEventCollector.EventType.CacheEntryRefCount, "RefCount", "Reference Count", new Color(123 / 255f, 158 / 255f, 6 / 255f, 1), labelBGColor, (v) => v.ToString())
                );

            graphView.DefineGraph(ResourceManagerEventCollector.EventType.CacheLRUCount.ToString(), (int)ResourceManagerEventCollector.EventType.CacheLRUCount,
                new GraphLayerBarChartMesh((int)ResourceManagerEventCollector.EventType.CacheLRUCount, "LRU", "LRU Count", new Color(12 / 255f, 6 / 255f, 158 / 255f, 1)),
                new GraphLayerLabel((int)ResourceManagerEventCollector.EventType.CacheLRUCount, "LRU", "LRU Count", new Color(12 / 255f, 6 / 255f, 158 / 255f, 1), labelBGColor, (v) => v.ToString())
                );

            graphView.DefineGraph(ResourceManagerEventCollector.EventType.AsyncOpCacheHitRatio.ToString(), (int)ResourceManagerEventCollector.EventType.AsyncOpCacheHitRatio,
                new GraphLayerBarChartMesh((int)ResourceManagerEventCollector.EventType.AsyncOpCacheHitRatio, "Cache Hit %", "Cache Hit %", new Color(123 / 255f, 158 / 255f, 6 / 255f, 1)),
                new GraphLayerBarChartMesh((int)ResourceManagerEventCollector.EventType.AsyncOpCacheCount, "Cache Count", "Cache Count", new Color(204 / 255f, 113 / 255f, 0, .75f)),
                new GraphLayerLabel((int)ResourceManagerEventCollector.EventType.AsyncOpCacheHitRatio, "Cache Hit %", "Cache Hit %", new Color(123 / 255f, 158 / 255f, 6 / 255f, 1), labelBGColor, (v) => v.ToString() + "%"),
                new GraphLayerLabel((int)ResourceManagerEventCollector.EventType.AsyncOpCacheCount, "Cache Count", "Cache Count", new Color(204 / 255f, 113 / 255f, 0, 1), labelBGColor, (v) => v.ToString())
                );
        }
    }
}
