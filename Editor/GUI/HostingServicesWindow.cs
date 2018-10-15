using System;
using System.Collections.Generic;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace UnityEditor.AddressableAssets
{
    /// <summary>
    /// Configuration GUI for <see cref="T:UnityEditor.AddressableAssets.HostingServicesManager" />
    /// </summary>
    public class HostingServicesWindow : EditorWindow, ISerializationCallbackReceiver, ILogHandler
    {
        private const float k_defaultSplitterRatio = 0.67f;
        private const int k_splitterHeight = 15;

        [SerializeField] private string m_logText;
        [SerializeField] private Vector2 m_logScrollPos;
        [SerializeField] private Vector2 m_servicesScrollPos;
        [SerializeField] private bool m_profileVarsFoldout = true;
        [SerializeField] private bool m_servicesFoldout = true;
        [SerializeField] private float m_splitterRatio = k_defaultSplitterRatio;
        [SerializeField] private AddressableAssetSettings m_settings;

        private ILogger m_logger;
        private bool m_newLogContent;
        private bool m_isResizingSplitter;

        private readonly Dictionary<object, HostingServicesProfileVarsTreeView> m_profileVarTables =
            new Dictionary<object, HostingServicesProfileVarsTreeView>();

        private readonly List<IHostingService> m_removalQueue = new List<IHostingService>();
        private HostingServicesProfileVarsTreeView m_globalProfileVarTable;

        /// <summary>
        /// Show the <see cref="HostingServicesWindow"/>, initialized with the given <see cref="AddressableAssetSettings"/>
        /// </summary>
        /// <param name="settings"></param>
        public void Show(AddressableAssetSettings settings)
        {
            Initialize(settings);
            Show();
        }

        private void Initialize(AddressableAssetSettings settings)
        {
            if (m_settings == null)
                m_settings = settings;

            m_settings.HostingServicesManager.Logger = m_logger;
        }

        [MenuItem("Window/Asset Management/Hosting Services", priority = 2052)]
        private static void InitializeWithDefaultSettings()
        {
            var defaultSettings = AddressableAssetSettingsDefaultObject.Settings;
            if (defaultSettings == null)
            {
                EditorUtility.DisplayDialog("Error", "Could not load default Addressable Asset settings.", "Ok");
                return;
            }

            GetWindow<HostingServicesWindow>().Show(defaultSettings);
        }

        private void Awake()
        {
            titleContent = new GUIContent("Hosting");
            m_logger = new Logger(this);
        }

        private void OnGUI()
        {
            if (m_settings == null) return;

            if (m_isResizingSplitter)
                m_splitterRatio = Mathf.Clamp((Event.current.mousePosition.y - k_splitterHeight / 2f) / position.height, 0.2f, 0.9f);

            var itemRect = new Rect(0, 0, position.width, position.height * m_splitterRatio);
            var splitterRect = new Rect(0, itemRect.height, position.width, k_splitterHeight);
            var logRect = new Rect(0, itemRect.height + k_splitterHeight, position.width,
                position.height - itemRect.height - k_splitterHeight);

            EditorGUI.LabelField(splitterRect, string.Empty, GUI.skin.horizontalSlider);
            EditorGUIUtility.AddCursorRect(splitterRect, MouseCursor.ResizeVertical);
            if (Event.current.type == EventType.MouseDown && splitterRect.Contains(Event.current.mousePosition))
                m_isResizingSplitter = true;
            else if (Event.current.type == EventType.MouseUp)
                m_isResizingSplitter = false;

            GUILayout.BeginArea(itemRect);
            {
                EditorGUILayout.Space();

                m_profileVarsFoldout = EditorGUILayout.Foldout(m_profileVarsFoldout, "Global Profile Variables");
                if (m_profileVarsFoldout)
                    DrawGlobalProfileVarsArea();

                EditorGUILayout.Space();

                m_servicesFoldout = EditorGUILayout.Foldout(m_servicesFoldout, "Hosting Services");
                if (m_servicesFoldout)
                    DrawServicesArea();
            }
            GUILayout.EndArea();

            GUILayout.BeginArea(logRect);
            {
                DrawLogArea(logRect);
            }
            GUILayout.EndArea();

            if (m_isResizingSplitter)
                Repaint();
        }

        private void DrawGlobalProfileVarsArea()
        {
            var manager = m_settings.HostingServicesManager;
            DrawProfileVarTable(this, manager.GlobalProfileVariables);

            GUILayout.BeginHorizontal();
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Refresh", GUILayout.MaxWidth(125f)))
                    manager.RefreshGlobalProfileVariables();
            }
            GUILayout.EndHorizontal();
        }

        private void DrawServicesArea()
        {
            var manager = m_settings.HostingServicesManager;
            m_servicesScrollPos = EditorGUILayout.BeginScrollView(m_servicesScrollPos);
            var svcList = manager.HostingServices;

            if (m_removalQueue.Count > 0)
            {
                foreach (var svc in m_removalQueue)
                    manager.RemoveHostingService(svc);

                m_removalQueue.Clear();
            }

            var i = 0;
            foreach (var svc in svcList)
            {
                if (i > 0) EditorGUILayout.Space();
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                DrawServiceElement(svc);
                EditorGUILayout.EndVertical();
                i++;
            }

            GUILayout.BeginHorizontal();
            {
                if (svcList.Count == 0)
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("No Hosting Services configured.");
                }

                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Add Service...", GUILayout.MaxWidth(125f)))
                {
                    GetWindow<HostingServicesAddServiceWindow>(true, "Add Service").Initialize(m_settings);
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.EndScrollView();
        }

        private void DrawServiceElement(IHostingService svc)
        {
            EditorGUILayout.BeginHorizontal();
            {
                svc.DescriptiveName = EditorGUILayout.DelayedTextField("Service Name", svc.DescriptiveName);

                var newIsServiceEnabled = GUILayout.Toggle(svc.IsHostingServiceRunning, "Enable Service", "Button", GUILayout.MaxWidth(150f));

                if (GUILayout.Button("Remove...", GUILayout.MaxWidth(75f)))
                {
                    if (EditorUtility.DisplayDialog("Remove Service", "Are you sure?", "Ok", "Cancel"))
                        m_removalQueue.Add(svc);
                }
                else if (newIsServiceEnabled != svc.IsHostingServiceRunning)
                {
                    if (newIsServiceEnabled)
                        svc.StartHostingService();
                    else
                        svc.StopHostingService();
                }
            }
            EditorGUILayout.EndHorizontal();

            var typeAndId = string.Format("{0} ({1})", svc.GetType().Name, svc.InstanceId.ToString());
            EditorGUILayout.LabelField("Service Type (ID)", typeAndId, GUILayout.MinWidth(225f));

            EditorGUILayout.Space();
            
            using (new EditorGUI.DisabledGroupScope(!svc.IsHostingServiceRunning))
            {
                // Allow service to provide additional GUI configuration elements
                svc.OnGUI();

                EditorGUILayout.Space();

                DrawProfileVarTable(svc, svc.ProfileVariables);
            }
        }

        private void DrawLogArea(Rect rect)
        {
            if (m_newLogContent)
            {
                var height = GUI.skin.GetStyle("Label").CalcHeight(new GUIContent(m_logText), rect.width);
                m_logScrollPos = new Vector2(0f, height);
                m_newLogContent = false;
            }

            m_logScrollPos = EditorGUILayout.BeginScrollView(m_logScrollPos);
            GUILayout.Label(m_logText);
            EditorGUILayout.EndScrollView();
        }

        private void DrawProfileVarTable(object tableKey, IEnumerable<KeyValuePair<string, string>> data)
        {
            HostingServicesProfileVarsTreeView table;
            if (!m_profileVarTables.TryGetValue(tableKey, out table))
            {
                table = new HostingServicesProfileVarsTreeView(new TreeViewState(),
                    HostingServicesProfileVarsTreeView.CreateHeader());
                m_profileVarTables[tableKey] = table;
            }

            var rowHeight = table.RowHeight;
            var tableHeight = table.multiColumnHeader.height + rowHeight; // header + 1 extra line

            table.ClearItems();
            foreach (var kvp in data)
            {
                table.AddOrUpdateItem(kvp.Key, kvp.Value);
                tableHeight += rowHeight;
            }

            table.OnGUI(EditorGUILayout.GetControlRect(false, tableHeight));
        }

        /// <inheritdoc/>
        public void LogFormat(LogType logType, UnityEngine.Object context, string format, params object[] args)
        {
            IHostingService svc = null;

            if (args.Length > 0)
                svc = args[args.Length - 1] as IHostingService;

            if (svc != null)
            {
                m_logText += string.Format("[{0}] ", svc.DescriptiveName) + string.Format(format, args) + "\n";
                m_newLogContent = true;
            }

            Debug.unityLogger.LogFormat(logType, context, format, args);
        }

        /// <inheritdoc/>
        public void LogException(Exception exception, UnityEngine.Object context)
        {
            Debug.unityLogger.LogException(exception, context);
        }

        /// <inheritdoc/>
        public void OnBeforeSerialize()
        {
            // No implementation
        }

        /// <inheritdoc/>
        public void OnAfterDeserialize()
        {
            m_logger = new Logger(this);
            Initialize(m_settings);
        }
    }
}