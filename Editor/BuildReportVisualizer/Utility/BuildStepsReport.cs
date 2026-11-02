using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor.AddressableAssets.Build.Layout;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEngine;

namespace UnityEditor.AddressableAssets.BuildReportVisualizer
{
    /// <summary>What a build wrote out about its own steps, kept beside its report.</summary>
    [Serializable]
    internal class BuildStepsFile
    {
        // Bumped when the written shape changes; a file from another version is ignored.
        internal const int CurrentVersion = 1;

        public int Version;
        public BuildStepData[] Steps;
    }

    internal static class BuildStepsReport
    {
        internal static void Write(ILogBuildSteps log, string path)
        {
            var file = new BuildStepsFile { Version = BuildStepsFile.CurrentVersion, Steps = log.GetBuildSteps() };
            File.WriteAllText(path, JsonUtility.ToJson(file));
        }

        internal static bool TryRead(string path, out BuildStepData[] steps)
        {
            steps = Array.Empty<BuildStepData>();
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;

            BuildStepsFile file;
            try
            {
                file = JsonUtility.FromJson<BuildStepsFile>(File.ReadAllText(path));
            }
            catch (Exception)
            {
                // Unreadable is the same as absent. The build still has its profile.
                return false;
            }

            if (file == null || file.Version != BuildStepsFile.CurrentVersion || file.Steps == null || file.Steps.Length == 0)
                return false;

            steps = file.Steps;
            return true;
        }

        // The steps sit beside the layout in its Build History entry.
        internal static bool TryGetFilePath(BuildLayout layout, out string path)
        {
            path = string.Empty;
            if (layout == null || string.IsNullOrEmpty(layout.m_FilePath))
                return false;

            string directory = Path.GetDirectoryName(layout.m_FilePath);
            if (string.IsNullOrEmpty(directory) || Path.GetFileName(layout.m_FilePath) != BuildReportUtility.EmbeddedLayoutFileName)
                return false;

            path = Path.Combine(directory, BuildReportUtility.StepsFileName);
            return File.Exists(path);
        }
    }
}
