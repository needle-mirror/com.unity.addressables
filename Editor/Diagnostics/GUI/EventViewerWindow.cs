using UnityEngine;
using System;
using UnityEditor.IMGUI.Controls;
using UnityEditor.Networking.PlayerConnection;
using System.Collections.Generic;
using UnityEngine.ResourceManagement.Diagnostics;

namespace UnityEditor.AddressableAssets.Diagnostics
{
    internal class EventViewerWindow : EditorWindow, IComparer<EventDataSet>
    {
        [SerializeField]
        EventDataPlayerSessionCollection m_eventData;
        GUIContent m_prevFrameIcon;
        GUIContent m_nextFrameIcon;
        int m_playerSessionIndex = 0;
        int m_inspectFrame = 0;
        bool m_record = true;
        int m_eventListFrame = -1;
        VerticalSplitter m_verticalSplitter = new VerticalSplitter();
        HorizontalSplitter m_horizontalSplitter = new HorizontalSplitter();
        float m_lastEventListUpdate = 0;
        bool m_draggingInspectLine = false;
        bool m_registeredWithRM = false;
        int m_latestFrame = 0;

        TreeViewState m_eventListTreeViewState;
        MultiColumnHeaderState m_eventListMCHS;
        EventListView m_eventList;

        TreeViewState m_graphListTreeViewState;
        MultiColumnHeaderState m_graphListMCHS;
        EventGraphListView m_graphList;

        EventDataPlayerSession activeSession { get { return m_eventData == null ? null : m_eventData.GetSessionByIndex(m_playerSessionIndex); } }
        protected virtual bool ShowEventDetailPanel { get { return false; } }
        protected virtual bool ShowEventPanel { get { return false; } }


        private void OnEnable()
        {
            m_lastEventListUpdate = 0;
            m_prevFrameIcon = EditorGUIUtility.IconContent("Profiler.PrevFrame", "|Go back one frame");
            m_nextFrameIcon = EditorGUIUtility.IconContent("Profiler.NextFrame", "|Go one frame forwards");
            m_eventData = new EventDataPlayerSessionCollection(OnEventProcessed, OnRecordEvent);
            m_eventData.AddSession("Editor", m_playerSessionIndex = 0);
            EditorConnection.instance.Initialize();
            EditorConnection.instance.Register(DiagnosticEventCollector.EditorConnectionMessageId, OnPlayerConnectionMessage);
            EditorConnection.instance.RegisterConnection(OnPlayerConnection);
            EditorConnection.instance.RegisterDisconnection(OnPlayerDisconnection);
            EditorApplication.playModeStateChanged += OnEditorPlayModeChanged;
            RegisterEventHandler(true);
        }

        private void OnDisable()
        {
            EditorConnection.instance.Unregister(DiagnosticEventCollector.EditorConnectionMessageId, OnPlayerConnectionMessage);
            RegisterEventHandler(false);
            EditorApplication.playModeStateChanged -= OnEditorPlayModeChanged;
        }

        void RegisterEventHandler(bool reg)
        {
            if (reg == m_registeredWithRM)
                return;

            if (m_registeredWithRM = reg)
                DiagnosticEventCollector.RegisterEventHandler(OnEvent);
            else
                DiagnosticEventCollector.UnregisterEventHandler(OnEvent);
        }

        public void OnEvent(DiagnosticEvent diagnosticEvent)
        {
            m_eventData.ProcessEvent(diagnosticEvent, 0);
        }

        void OnPlayerConnection(int id)
        {
            m_eventData.GetPlayerSession(id, true).IsActive = true;
        }

        void OnPlayerDisconnection(int id)
        {
            var c = m_eventData.GetPlayerSession(id, false);
            if (c != null)
                c.IsActive = false;
        }

        void OnPlayerConnectionMessage(UnityEngine.Networking.PlayerConnection.MessageEventArgs args)
        {
            if (!m_record)
                return;
            var evt = DiagnosticEvent.Deserialize(args.data);
            m_eventData.ProcessEvent(evt, args.playerId);
        }

        public int Compare(EventDataSet x, EventDataSet y)
        {
            int vx = x == null ? 0 : (x.Graph == "EventCount" ? -10000 : x.FirstSampleFrame);
            int vy = y == null ? 0 : (y.Graph == "EventCount" ? -10000 : y.FirstSampleFrame);

            return vx - vy;
        }

        protected virtual bool CanHandleEvent(string graph)
        {
            if (graph == "EventCount")
                return true;
            return OnCanHandleEvent(graph);
        }

        protected virtual bool OnCanHandleEvent(string graph) { return true; }

        private void OnEditorPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                m_lastEventListUpdate = 0;
                m_inspectFrame = -1;
                m_latestFrame = -1;
                m_playerSessionIndex = 0;
                RegisterEventHandler(true);
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                RegisterEventHandler(false);
            }
        }

        protected virtual bool OnRecordEvent(DiagnosticEvent diagnosticEvent)
        {
            return false;
        }

        void OnEventProcessed(EventDataPlayerSession session, DiagnosticEvent diagnosticEvent, bool entryCreated)
        {
            if (!CanHandleEvent(diagnosticEvent.Graph))
                return;

            bool moveInspectFrame = m_latestFrame < 0 || m_inspectFrame == m_latestFrame;
            m_latestFrame = diagnosticEvent.Frame;
            if (entryCreated)
            {
                if (m_graphList != null)
                    m_graphList.Reload();
            }

            if (moveInspectFrame)
                SetInspectFrame(m_latestFrame);

            if (diagnosticEvent.EventId == "Events")
            {
                Repaint();
            }
        }

        void SetInspectFrame(int frame)
        {
            m_inspectFrame = frame;
            if (m_inspectFrame > m_latestFrame)
                m_inspectFrame = m_latestFrame;
            if (m_inspectFrame < 0)
                m_inspectFrame = 0;

            if(m_eventList != null)
                m_eventList.SetEvents(activeSession == null ? null : activeSession.GetFrameEvents(m_inspectFrame));
            m_lastEventListUpdate = Time.unscaledTime;
            m_eventListFrame = m_inspectFrame;
        }

        private void OnGUI()
        {
            var session = activeSession;
            if (session == null)
                return;
            InitializeGui();

            //this prevent arrow key events from reaching the treeview, so navigation via keys is disabled
            if (Event.current.type == EventType.KeyDown)
            {
                if (Event.current.keyCode == KeyCode.RightArrow)
                {
                    SetInspectFrame(m_inspectFrame + 1);
                    return;
                }
                if (Event.current.keyCode == KeyCode.LeftArrow)
                {
                    SetInspectFrame(m_inspectFrame - 1);
                    return;
                }
            }

            DrawToolBar(session);

            var r = EditorGUILayout.GetControlRect();
            Rect contentRect = new Rect(r.x, r.y, r.width, position.height - (r.y + r.x));
            var graphRect = m_graphList.GraphRect;
            if (ShowEventPanel)
            {
                Rect top, bot;
                bool resizingVer = m_verticalSplitter.OnGUI(contentRect, out top, out bot);

                ProcessInspectFrame(graphRect);

                m_graphList.DrawGraphs(top, m_inspectFrame);

                DrawInspectFrame(graphRect);

                bool resizingHor = false;
                if (ShowEventDetailPanel)
                {
                    Rect left, right;
                    resizingHor = m_horizontalSplitter.OnGUI(bot, out left, out right);
                    m_eventList.OnGUI(left);
                    OnDrawEventDetail(right, m_eventList.selectedEvent);
                }
                else
                {
                    m_eventList.OnGUI(bot);
                }
                if (resizingVer || resizingHor)
                    Repaint();
            }
            else
            {
                ProcessInspectFrame(graphRect);
                m_graphList.DrawGraphs(contentRect, m_inspectFrame);
                DrawInspectFrame(graphRect);
            }
        }

        protected virtual void OnDrawEventDetail(Rect right, DiagnosticEvent selectedEvent)
        {
        }

        private void OnInspectorUpdate()
        {
            Repaint();
        }

        private void ProcessInspectFrame(Rect graphRect)
        {
            if (Event.current.type == EventType.MouseDown && graphRect.Contains(Event.current.mousePosition))
            {
                if (EditorApplication.isPlaying)
                    EditorApplication.isPaused = true;
                m_draggingInspectLine = true;
                SetInspectFrame(m_inspectFrame);
            }

            if (m_draggingInspectLine && (Event.current.type == EventType.MouseDrag || Event.current.type == EventType.Repaint))
                SetInspectFrame(m_graphList.visibleStartTime + (int)GraphUtility.PixelToValue(Event.current.mousePosition.x, graphRect.xMin, graphRect.xMax, m_graphList.visibleDuration));
            if (Event.current.type == EventType.MouseUp)
            {
                m_draggingInspectLine = false;
                SetInspectFrame(m_inspectFrame);
            }
        }

        private void DrawInspectFrame(Rect graphPanelRect)
        {
            if (m_inspectFrame != m_latestFrame)
            {
                var ix = graphPanelRect.xMin + GraphUtility.ValueToPixel(m_inspectFrame, m_graphList.visibleStartTime, m_graphList.visibleStartTime + m_graphList.visibleDuration, graphPanelRect.width);
                EditorGUI.DrawRect(new Rect(ix - 1, graphPanelRect.yMin, 3, graphPanelRect.height), Color.white * .8f);
            }
        }

        private void DrawToolBar(EventDataPlayerSession session)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.FlexibleSpace();
            m_record = GUILayout.Toggle(m_record, "Record", EditorStyles.toolbarButton, GUILayout.ExpandWidth(false));

            if (GUILayout.Button("Clear", EditorStyles.toolbarButton))
                session.Clear();
            if (GUILayout.Button("Load", EditorStyles.toolbarButton))
                EditorUtility.DisplayDialog("Feature not implemented", "Saving and loading profile data is not yet supported", "Close");
            if (GUILayout.Button("Save", EditorStyles.toolbarButton))
                EditorUtility.DisplayDialog("Feature not implemented", "Saving and loading profile data is not yet supported", "Close");

            GUILayout.FlexibleSpace();
            GUILayout.Label(m_inspectFrame == m_latestFrame ? "Frame:     " : "Frame: " + m_inspectFrame + "/" + m_latestFrame, EditorStyles.miniLabel);

            using (new EditorGUI.DisabledScope(m_inspectFrame <= 0))
                if (GUILayout.Button(m_prevFrameIcon, EditorStyles.toolbarButton))
                    SetInspectFrame(m_inspectFrame - 1);


            using (new EditorGUI.DisabledScope(m_inspectFrame >= m_latestFrame))
                if (GUILayout.Button(m_nextFrameIcon, EditorStyles.toolbarButton))
                    SetInspectFrame(m_inspectFrame + 1);


            if (GUILayout.Button("Current", EditorStyles.toolbarButton, GUILayout.ExpandWidth(false)))
                SetInspectFrame(m_latestFrame);

            GUILayout.EndHorizontal();
        }

        protected virtual void OnGetColumns(List<string> columnNames, List<float> columnSizes)
        {
            if (columnNames == null || columnSizes == null)
                return;
            columnNames.Add("Event"); columnSizes.Add(50);
            columnNames.Add("Id"); columnSizes.Add(200);
            columnNames.Add("Data"); columnSizes.Add(400);
        }

        protected virtual bool OnDrawColumnCell(Rect cellRect, DiagnosticEvent diagnosticEvent, int column)
        {
            return false;
        }

        protected virtual void DrawColumnCell(Rect cellRect, DiagnosticEvent diagnosticEvent, int column)
        {
            if (!OnDrawColumnCell(cellRect, diagnosticEvent, column))
            {
                switch (column)
                {
                    case 0: EditorGUI.LabelField(cellRect, diagnosticEvent.Stream.ToString()); break;
                    case 1: EditorGUI.LabelField(cellRect, diagnosticEvent.EventId); break;
                    case 2: EditorGUI.LabelField(cellRect, diagnosticEvent.Data == null ? "null" : diagnosticEvent.Data.ToString()); break;
                }
            }
        }

        private void InitializeGui()
        {
            if (m_graphList == null)
            {
                if (m_graphListTreeViewState == null)
                    m_graphListTreeViewState = new TreeViewState();

                var headerState = EventGraphListView.CreateDefaultHeaderState();
                if (MultiColumnHeaderState.CanOverwriteSerializedFields(m_graphListMCHS, headerState))
                    MultiColumnHeaderState.OverwriteSerializedFields(m_graphListMCHS, headerState);

                m_graphListMCHS = headerState;
                m_graphList = new EventGraphListView(activeSession, m_graphListTreeViewState, m_graphListMCHS, CanHandleEvent, this);
                InitializeGraphView(m_graphList);
                m_graphList.Reload();
            }

            if (m_eventList == null)
            {
                if (m_eventListTreeViewState == null)
                    m_eventListTreeViewState = new TreeViewState();

                var columns = new List<string>();
                var sizes = new List<float>();
                OnGetColumns(columns, sizes);
                var headerState = EventListView.CreateDefaultMultiColumnHeaderState(columns, sizes);
                if (MultiColumnHeaderState.CanOverwriteSerializedFields(m_eventListMCHS, headerState))
                    MultiColumnHeaderState.OverwriteSerializedFields(m_eventListMCHS, headerState);

                m_eventListMCHS = headerState;
                m_eventList = new EventListView(m_eventListTreeViewState, m_eventListMCHS, DrawColumnCell, OnRecordEvent);
                m_eventList.Reload();
            }

            if (m_eventListFrame != m_inspectFrame && m_inspectFrame != m_latestFrame && !m_draggingInspectLine && Time.unscaledTime - m_lastEventListUpdate > .25f)
            {
                m_eventList.SetEvents(activeSession.GetFrameEvents(m_inspectFrame));
                m_lastEventListUpdate = Time.unscaledTime;
                m_eventListFrame = m_inspectFrame;
            }
            
            if (m_graphListMCHS != null && m_graphListMCHS.columns.Length > 2)
            {
                string warningText = string.Empty;
                if (!ProjectConfigData.postProfilerEvents)
                    warningText = "Warning: Profile events must be enabled in your Addressable Asset settings to view profile data";
                    m_graphListMCHS.columns[2].headerContent.text = warningText;
            }
        }

        void InitializeGraphView(EventGraphListView graphView)
        {
            graphView.DefineGraph("EventCount", 0, new GraphLayerVertValueLine(0, "Events", "Event count per frame", Color.green));
            OnInitializeGraphView(graphView);
        }

        virtual protected void OnInitializeGraphView(EventGraphListView graphView) { }
    }

    [Serializable]
    class VerticalSplitter
    {
        [NonSerialized]
        Rect m_rect = new Rect(0, 0, 0, 3);
        public Rect SplitterRect { get { return m_rect; } }
        [SerializeField]
        float m_currentPercent = .8f;

        bool m_resizing = false;
        float m_minPercent = .2f;
        float m_maxPercent = .9f;
        public VerticalSplitter(float percent = .8f, float minPer = .2f, float maxPer = .9f)
        {
            m_currentPercent = percent;
            m_minPercent = minPer;
            m_maxPercent = maxPer;
        }

        public bool OnGUI(Rect content, out Rect top, out Rect bot)
        {
            m_rect.x = content.x;
            m_rect.y = (int)(content.y + content.height * m_currentPercent);
            m_rect.width = content.width;

            EditorGUIUtility.AddCursorRect(m_rect, MouseCursor.ResizeVertical);
            if (Event.current.type == EventType.MouseDown && m_rect.Contains(Event.current.mousePosition))
                m_resizing = true;

            if (m_resizing)
            {
                EditorGUIUtility.AddCursorRect(content, MouseCursor.ResizeVertical);

                var mousePosInRect = Event.current.mousePosition.y - content.y;
                m_currentPercent = Mathf.Clamp(mousePosInRect / content.height, m_minPercent, m_maxPercent);
                m_rect.y = Mathf.Min((int)(content.y + content.height * m_currentPercent), content.yMax - m_rect.height);
                if (Event.current.type == EventType.MouseUp)
                    m_resizing = false;
            }

            top = new Rect(content.x, content.y, content.width, m_rect.yMin - content.yMin);
            bot = new Rect(content.x, m_rect.yMax, content.width, content.yMax - m_rect.yMax);
            return m_resizing;
        }
    }

    [Serializable]
    class HorizontalSplitter
    {
        [NonSerialized]
        Rect m_rect = new Rect(0, 0, 3, 0);
        public Rect SplitterRect { get { return m_rect; } }
        [SerializeField]
        float m_currentPercent = .8f;

        bool m_resizing = false;
        float m_minPercent = .2f;
        float m_maxPercent = .9f;
        public HorizontalSplitter(float percent = .8f, float minPer = .2f, float maxPer = .9f)
        {
            m_currentPercent = percent;
            m_minPercent = minPer;
            m_maxPercent = maxPer;
        }

        public bool OnGUI(Rect content, out Rect left, out Rect right)
        {
            m_rect.y = content.y;
            m_rect.x = (int)(content.x + content.width * m_currentPercent);
            m_rect.height = content.height;

            EditorGUIUtility.AddCursorRect(m_rect, MouseCursor.ResizeHorizontal);
            if (Event.current.type == EventType.MouseDown && m_rect.Contains(Event.current.mousePosition))
                m_resizing = true;

            if (m_resizing)
            {
                EditorGUIUtility.AddCursorRect(content, MouseCursor.ResizeHorizontal);

                var mousePosInRect = Event.current.mousePosition.x - content.x;
                m_currentPercent = Mathf.Clamp(mousePosInRect / content.width, m_minPercent, m_maxPercent);
                m_rect.x = Mathf.Min((int)(content.x + content.width * m_currentPercent), content.xMax - m_rect.width);
                if (Event.current.type == EventType.MouseUp)
                    m_resizing = false;
            }

            left = new Rect(content.x, content.y, m_rect.xMin, content.height);
            right = new Rect(m_rect.xMax, content.y, content.width - m_rect.xMax, content.height);
            return m_resizing;
        }
    }
}
