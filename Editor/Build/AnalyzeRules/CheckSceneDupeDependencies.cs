using System.Collections.Generic;
using System.Linq;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace UnityEditor.AddressableAssets.Build.AnalyzeRules
{
    class CheckSceneDupeDependencies : BundleRuleBase
    {
        public override bool CanFix
        {
            get { return false; }
        }

        public override string ruleName
        {
            get { return "Check Scene to Addressable Duplicate Dependencies"; }
        }

        public override List<AnalyzeResult> RefreshAnalysis(AddressableAssetSettings settings)
        {
            ClearAnalysis();

            string[] scenePaths = (from editorScene in EditorBuildSettings.scenes
                                   select editorScene.path).ToArray();

            return CalculateBuiltInResourceDependenciesToBundleDependecies(settings, scenePaths);
        }

        public override void FixIssues(AddressableAssetSettings settings)
        {
            //Do nothing.  There's nothing to fix.
        }
    }

    [InitializeOnLoad]
    class RegisterCheckSceneDupeDependencies
    {
        static RegisterCheckSceneDupeDependencies()
        {
            AnalyzeSystem.RegisterNewRule<CheckSceneDupeDependencies>();
        }
    }
}
