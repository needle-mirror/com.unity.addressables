using System;
using System.Collections.Generic;

namespace UnityEditor.AddressableAssets
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

        public static TResult CreateResult<TResult>(int locCount, double dur, string err = "") where TResult : IDataBuilderResult
        {
            var opResult = Activator.CreateInstance<TResult>();
            opResult.Duration = dur;
            opResult.Error = err;
            return opResult;
        }
    }

    /// <summary>
    /// Build result for entering play mode in the editor.
    /// </summary>
    public class AddressablesPlayModeBuildResult : AddressableAssetBuildResult
    {
        public List<EditorBuildSettingsScene> ScenesToAdd { get; set; }
    }

    /// <summary>
    /// Build result for building the player.
    /// </summary>
    public class AddressablesPlayerBuildResult : AddressableAssetBuildResult
    {
    }
}