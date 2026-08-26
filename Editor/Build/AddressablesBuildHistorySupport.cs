#if ENABLE_BUILD_HISTORY_EXTERNAL_BUILDS
using System;
using System.IO;
using UnityEditor.AddressableAssets.Build.Layout;
using UnityEditor.AddressableAssets.BuildReportVisualizer;
using UnityEditor.Build;
using UnityEngine;

namespace UnityEditor.AddressableAssets.Build
{
    /// <summary>
    /// Registers Addressables builds in the Editor's BuildHistory so they appear in the Build Analysis window.
    /// The legacy report locations and the ProjectConfigData index are unaffected; the layout and TEP files are
    /// additionally copied into the BuildHistory entry.
    /// </summary>
    internal static class AddressablesBuildHistorySupport
    {
        internal const string kLayoutFileName = "AddressablesBuildLayout.json";
        internal const string kBuildTypeName = "Addressables";
        internal const string kPackageName = "com.unity.addressables";

        internal static void RegisterBuild(BuildLayout layout, string writtenLayoutPath)
        {
            try
            {
                ExternalBuildInfo buildInfo = CreateBuildInfo(layout);
                string directory = BuildHistory.RegisterExternalBuild(buildInfo);
                TryCopyIntoEntry(writtenLayoutPath, directory, kLayoutFileName);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Failed to register the Addressables build in the build history: {e.Message}");
            }
        }

        internal static ExternalBuildInfo CreateBuildInfo(BuildLayout layout)
        {
            return new ExternalBuildInfo
            {
                BuildName = kBuildTypeName,
                BuildResult = string.IsNullOrEmpty(layout.BuildError)
                    ? UnityEditor.Build.Reporting.BuildResult.Succeeded
                    : UnityEditor.Build.Reporting.BuildResult.Failed,
                BuildSessionGUID = layout.AddressablesBuildSessionGUID,
                // BuildStart is a zone-less local wall-clock time; the DateTimeOffset
                // conversion reads it as local.
                BuildStartedAt = new DateTimeOffset(layout.BuildStart),
                BuildType = UnityEditor.Build.Reporting.BuildType.AssetBundle,
                BuildTypeName = kBuildTypeName,
                OutputPath = layout.LocalCatalogBuildPath,
                Platform = layout.BuildTarget,
                ProducerPackage = kPackageName,
                TotalSizeBytes = (long)BuildLayoutHelpers.GetTotalBuildSize(layout),
                TotalTime = TimeSpan.FromSeconds(layout.Duration),
            };
        }

        internal static void TryCopyTep(string tepSourcePath, string tepDirectory)
        {
            if (string.IsNullOrEmpty(tepDirectory))
                return;
            TryCopyIntoEntry(tepSourcePath, tepDirectory, BuildReportUtility.TepFileName);
        }

        static void TryCopyIntoEntry(string sourcePath, string directory, string fileName)
        {
            try
            {
                File.Copy(sourcePath, Path.Combine(directory, fileName), true);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Failed to copy {fileName} into the build history entry: {e.Message}");
            }
        }
    }
}
#endif
