using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.AddressableAssets.Build.AnalyzeRules;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace UnityEditor.AddressableAssets.GUI
{
    /// <summary>
    /// Window used to execute AnalyzeRule sets.  
    /// </summary>
    public class AnalyzeWindow : EditorWindow
    {
        private static AnalyzeWindow s_Instance = null;
        private static AnalyzeWindow instance
        {
            get
            {
                if (s_Instance == null)
                    s_Instance = GetWindow<AnalyzeWindow>(false, "Analyze", false);
                return s_Instance;
            }
        }
        
        private AddressableAssetSettings m_Settings;

        [SerializeField]
        private AnalyzeRuleGUI m_AnalyzeEditor;
        
        private Rect displayAreaRect
        {
            get
            {
                return new Rect(0, 0, position.width, position.height);
            }
        }

        [MenuItem("Window/Asset Management/Addressables Analyze", priority = 2052)]
        internal static void ShowWindow()
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogError("Unable to load Addressable Asset Settings default object.");
                return;
            }

            instance.titleContent = new GUIContent("Analyze");
            instance.Show();
        }

        void OnEnable()
        {
            if(m_AnalyzeEditor == null)
                m_AnalyzeEditor = new AnalyzeRuleGUI();

        }

        void OnGUI() 
        {
            GUILayout.BeginArea(displayAreaRect);
            m_AnalyzeEditor.OnGUI(displayAreaRect);
            GUILayout.EndArea();
        }
        /// <summary>
        /// Method used to register any custom AnalyzeRules with the window.  The recommended pattern is to create
        /// your rules like so:
        ///   class MyRule : AnalyzeRule {}
        ///   [InitializeOnLoad]
        ///   class RegisterMyRule
        ///   {
        ///       static RegisterMyRule()
        ///       {
        ///           AnalyzeWindow.RegisterNewRule<MyRule>();
        ///       }
        ///   }
        /// </summary>
        public static void RegisterNewRule<TRule>() where TRule : AnalyzeRule, new()
        {
            foreach (var rule in AnalyzeRuleGUI.Rules)
            {
                if (rule.GetType().IsAssignableFrom(typeof(TRule)))
                    return;
            }
            AnalyzeRuleGUI.Rules.Add(new TRule());
        }

    }
}