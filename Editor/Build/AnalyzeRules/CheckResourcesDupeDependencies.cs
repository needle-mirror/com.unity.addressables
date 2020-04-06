using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;


namespace UnityEditor.AddressableAssets.Build.AnalyzeRules
{
    class CheckResourcesDupeDependencies : BundleRuleBase
    {
        public override bool CanFix
        {
            get { return false; }
        }

        public override string ruleName
        {
            get { return "Check Resources to Addressable Duplicate Dependencies"; }
        }

        public override List<AnalyzeResult> RefreshAnalysis(AddressableAssetSettings settings)
        {
            ClearAnalysis();

            string[] resourceDirectory = Directory.GetDirectories("Assets", "Resources", SearchOption.AllDirectories);
            List<string> resourcePaths = new List<string>();
            foreach (string directory in resourceDirectory)
                resourcePaths.AddRange(Directory.GetFiles(directory, "*", SearchOption.AllDirectories));


            return CalculateBuiltInResourceDependenciesToBundleDependecies(settings, resourcePaths.ToArray());
        }

        public override void FixIssues(AddressableAssetSettings settings)
        {
            //Do nothing.  There's nothing to fix.
        }
    }
    
    

    [InitializeOnLoad]
    class RegisterCheckResourcesDupeDependencies
    {
        static RegisterCheckResourcesDupeDependencies()
        {
            AnalyzeSystem.RegisterNewRule<CheckResourcesDupeDependencies>();
        }
    }
}