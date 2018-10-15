using System;
using UnityEngine;

namespace UnityEditor.AddressableAssets
{
    internal class HostingServicesAddServiceWindow : EditorWindow
    {
        private MonoScript m_script;
        private string m_name;
        private bool m_useCustomScript;
        private AddressableAssetSettings m_settings;
        private Type[] m_serviceTypes;
        private string[] m_serviceTypeNames;
        private int m_serviceTypeIndex;

        /// <summary>
        /// Initialize the dialog for the given <see cref="AddressableAssetSettings"/>
        /// </summary>
        /// <param name="settings"></param>
        public void Initialize(AddressableAssetSettings settings)
        {
            m_settings = settings;
            m_name = string.Format("My Hosting Service {0}", m_settings.HostingServicesManager.NextInstanceId);
            PopulateServiceTypes();
        }

        private void PopulateServiceTypes()
        {
            if (m_settings == null) return;
            m_serviceTypes = m_settings.HostingServicesManager.RegisteredServiceTypes;
            m_serviceTypeNames = new string[m_serviceTypes.Length];
            for (var i = 0; i < m_serviceTypes.Length; i++)
                m_serviceTypeNames[i] = m_serviceTypes[i].Name;
        }

        private void OnGUI()
        {
            if (m_settings == null) return;
            var toggleState = !m_useCustomScript;

            EditorGUILayout.BeginHorizontal();
            {
                toggleState = GUILayout.Toggle(toggleState, " Service Type", "Radio");
                m_useCustomScript = !toggleState;

                using (new EditorGUI.DisabledScope(m_useCustomScript))
                    m_serviceTypeIndex = EditorGUILayout.Popup(m_serviceTypeIndex, m_serviceTypeNames);
            }
            EditorGUILayout.EndHorizontal();

            toggleState = m_useCustomScript;
            toggleState = GUILayout.Toggle(toggleState, " Custom", "Radio");
            m_useCustomScript = toggleState;

            if (m_useCustomScript)
            {
                EditorGUILayout.HelpBox("Select a script that implements the IHostingService interface.", MessageType.Info);
                var script =
                    EditorGUILayout.ObjectField("Hosting Service Script", m_script, typeof(MonoScript), false) as MonoScript;

                if (script != m_script && script != null)
                {
                    var scriptType = script.GetClass();
                    if (scriptType == null)
                    {
                        EditorUtility.DisplayDialog("Error", "Unable to find a valid type from the specified script.", "Ok");
                        m_script = null;
                    }
                    else if (scriptType.IsAbstract)
                    {
                        EditorUtility.DisplayDialog("Error", "Script cannot be an Abstract class", "Ok");
                        m_script = null;                       
                    }
                    else if (!typeof(IHostingService).IsAssignableFrom(scriptType))
                    {
                        EditorUtility.DisplayDialog("Error", "Selected script does not implement the IHostingService interface", "Ok");
                        m_script = null;
                    }
                    else
                    {
                        m_script = script;
                    }
                }
            }

            m_name = EditorGUILayout.TextField("Descriptive Name", m_name);

            EditorGUILayout.BeginHorizontal();
            {
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Cancel", GUILayout.MaxWidth(75f)))
                {
                    Close();
                    FocusWindowIfItsOpen<HostingServicesWindow>();
                }

                var okDisabled = string.IsNullOrEmpty(m_name) || (m_useCustomScript && m_script == null);
                using (new EditorGUI.DisabledGroupScope(okDisabled))
                {
                    if (GUILayout.Button("Add", GUILayout.MaxWidth(75f)))
                    {
                        try
                        {
                            var t = m_useCustomScript && m_script != null
                                ? m_script.GetClass()
                                : m_serviceTypes[m_serviceTypeIndex];

                            m_settings.HostingServicesManager.AddHostingService(t, m_name);
                        }
                        finally
                        {
                            Close();
                            FocusWindowIfItsOpen<HostingServicesWindow>();
                        }
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void OnEnable()
        {
            PopulateServiceTypes();
        }
    }
}