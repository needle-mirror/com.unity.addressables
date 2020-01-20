using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.AddressableAssets.Build.AnalyzeRules;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace UnityEditor.AddressableAssets.Build
{
    /// <summary>
    /// Static system to manage Analyze functionality.
    /// </summary>
    [Serializable]
    public static class AnalyzeSystem
    {
        /// <summary>
        /// Method used to register any custom AnalyzeRules with the AnalyzeSystem.  This replaces calling into the AnalyzeWindow
        ///  directly to remove logic from the GUI.  The recommended pattern is to create
        /// your rules like so:
        ///   class MyRule : AnalyzeRule {}
        ///   [InitializeOnLoad]
        ///   class RegisterMyRule
        ///   {
        ///       static RegisterMyRule()
        ///       {
        ///           AnalyzeSystem.RegisterNewRule<MyRule>();
        ///       }
        ///   }
        /// </summary>
        public static void RegisterNewRule<TRule>() where TRule : AnalyzeRule, new()
        {
            foreach (var rule in Rules)
            {
                if (rule.GetType().IsAssignableFrom(typeof(TRule)))
                    return;
            }
            Rules.Add(new TRule());
        }

        internal static string AnalyzeRuleDataFolder => AddressableAssetSettingsDefaultObject.kDefaultConfigFolder + "/AnalyzeData";
        internal static string AnalyzeRuleDataName => "AnalyzeRuleData.asset";
        internal static string AnalyzeRuleDataPath => AnalyzeRuleDataFolder + "/" + AnalyzeRuleDataName;
        internal static AddressableAssetSettings Settings => AddressableAssetSettingsDefaultObject.Settings;

        internal static List<AnalyzeRule> Rules { get; } = new List<AnalyzeRule>();

        [SerializeField]
        private static AnalyzeResultData m_Data;

        internal static AnalyzeResultData AnalyzeData
        {
            get
            {
                if (m_Data == null)
                {
                    if (!Directory.Exists(AnalyzeRuleDataFolder))
                        Directory.CreateDirectory(AnalyzeRuleDataFolder);

                    if (!File.Exists(AnalyzeRuleDataPath))
                    {
                        AssetDatabase.CreateAsset(ScriptableObject.CreateInstance(typeof(AnalyzeResultData)),
                            AnalyzeRuleDataPath);
                    }

                    m_Data = AssetDatabase.LoadAssetAtPath<AnalyzeResultData>(AnalyzeRuleDataPath);

                    foreach (var rule in Rules)
                    {
                        if (!m_Data.Data.ContainsKey(rule.ruleName))
                            m_Data.Data.Add(rule.ruleName, new List<AnalyzeRule.AnalyzeResult>());
                    }
                }

                return m_Data;
            }
        }

        internal static List<AnalyzeRule.AnalyzeResult> RefreshAnalysis<TRule>() where TRule : AnalyzeRule
        {
            return RefreshAnalysis(FindRule<TRule>());
        }

        internal static List<AnalyzeRule.AnalyzeResult> RefreshAnalysis(AnalyzeRule rule)
        {
            if (rule == null)
                return null;

            if (!AnalyzeData.Data.ContainsKey(rule.ruleName))
                AnalyzeData.Data.Add(rule.ruleName, new List<AnalyzeRule.AnalyzeResult>());

            AnalyzeData.Data[rule.ruleName] = rule.RefreshAnalysis(Settings);

            return AnalyzeData.Data[rule.ruleName];
        }

        internal static void ClearAnalysis<TRule>() where TRule : AnalyzeRule
        {
            ClearAnalysis(FindRule<TRule>());
        }

        internal static void ClearAnalysis(AnalyzeRule rule)
        {
            if (rule == null)
                return;

            if (!AnalyzeData.Data.ContainsKey(rule.ruleName))
                AnalyzeData.Data.Add(rule.ruleName, new List<AnalyzeRule.AnalyzeResult>());

            rule.ClearAnalysis();;
            AnalyzeData.Data[rule.ruleName].Clear();
        }

        internal static void FixIssues<TRule>() where TRule : AnalyzeRule
        {
            FixIssues(FindRule<TRule>());
        }

        internal static void FixIssues(AnalyzeRule rule)
        {
            rule?.FixIssues(Settings);
        }

        private static AnalyzeRule FindRule<TRule>() where TRule : AnalyzeRule
        {
            var rule = Rules.FirstOrDefault(r => r.GetType().IsAssignableFrom(typeof(TRule)));
            if (rule == null)
                throw new ArgumentException($"No rule found corresponding to type {typeof(TRule)}");

            return rule;
        }
    }
}