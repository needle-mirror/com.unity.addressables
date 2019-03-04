using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor.AddressableAssets.Build
{

    /// <summary>
    /// Contains information about the status of the build.
    /// </summary>
    public class AddressableAssetBuildResult : IDataBuilderResult
    {
        /// <summary>
        /// Duration of build, in seconds.
        /// </summary>
        public double Duration { get; set; }
        /// <summary>
        /// The number of addressable assets contained in the build.
        /// </summary>
        public int LocationCount { get; set; }
        /// <summary>
        /// Error that caused the build to fail.
        /// </summary>
        public string Error { get; set; }
        /// <summary>
        /// Path of runtime settings file
        /// </summary>
        public string OutputPath { get; set; }

        public static TResult CreateResult<TResult>(string settingsPath, int locCount, double dur, string err = "") where TResult : IDataBuilderResult
        {
            var opResult = Activator.CreateInstance<TResult>();
            opResult.OutputPath = settingsPath;
            opResult.Duration = dur;
            opResult.Error = err;
            return opResult;
        }
    }

    /// <summary>
    /// Build result for entering play mode in the editor.
    /// </summary>
    [Serializable]
    public class AddressablesPlayModeBuildResult : AddressableAssetBuildResult
    {
        [SerializeField]
        List<bool> m_EnabledList;
        [SerializeField]
        List<string> m_PathList;

        public List<EditorBuildSettingsScene> ScenesToAdd
        {
            get
            {
                var result = new List<EditorBuildSettingsScene>();
                for (int index = 0; index < m_EnabledList.Count; index++)
                {
                    result.Add(new EditorBuildSettingsScene(m_PathList[index], m_EnabledList[index]));
                }
                return result;
            }
            set
            {
                m_EnabledList = new List<bool>();
                m_PathList = new List<string>();
                foreach (var v in value)
                {
                    m_EnabledList.Add(v.enabled);
                    m_PathList.Add(v.path);
                }
            }
        }
    }

    /// <summary>
    /// Build result for building the player.
    /// </summary>
    public class AddressablesPlayerBuildResult : AddressableAssetBuildResult
    {
    }
}