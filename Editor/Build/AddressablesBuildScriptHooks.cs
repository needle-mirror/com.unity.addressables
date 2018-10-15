using UnityEngine;
using UnityEngine.ResourceManagement;
using UnityEngine.AddressableAssets;
using System;
using UnityEngine.Experimental.UIElements;
using System.Collections.Generic;
using System.IO;

namespace UnityEditor.AddressableAssets
{
    /// <summary>
    /// Entry point to set callbacks for builds.
    /// </summary>
    public static class BuildScript
    {
        /// <summary>
        /// Global delegate for handling the result of AddressableAssets builds.  This will get called for player builds and when entering play mode.
        /// </summary>
        public static Action<AddressableAssetBuildResult> buildCompleted;
    }

    internal static class AddressablesBuildScriptHooks
    {
        [InitializeOnLoadMethod]
        static void Init()
        {
            BuildPlayerWindow.RegisterBuildPlayerHandler(BuildPlayer);
            EditorApplication.playModeStateChanged += OnEditorPlayModeChanged;
        }

        static void BuildPlayer(BuildPlayerOptions ops)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                if (BuildScript.buildCompleted != null)
                    BuildScript.buildCompleted(new AddressableAssetBuildResult() { Duration = 0, Error = "AddressableAssetSettings not found." });
                BuildPipeline.BuildPlayer(ops);
            }
            else
            {
                if (settings.ActivePlayerDataBuilder == null)
                {
                    var err = "Active build script is null.";
                    Debug.LogError(err);

                    if (BuildScript.buildCompleted != null)
                        BuildScript.buildCompleted(new AddressableAssetBuildResult() { Duration = 0, Error = err });
                    return;
                }

                if (!settings.ActivePlayerDataBuilder.CanBuildData<AddressablesPlayerBuildResult>())
                {
                    var err = string.Format("Active build script {0} cannot build AddressablesPlayerBuildResult.", settings.ActivePlayerDataBuilder);
                    Debug.LogError(err);
                    if (BuildScript.buildCompleted != null)
                        BuildScript.buildCompleted(new AddressableAssetBuildResult() { Duration = 0, Error = err });
                    return;
                }

                var context = new AddressablesBuildDataBuilderContext(settings, ops.targetGroup, ops.target, (ops.options & BuildOptions.Development) != BuildOptions.None, ((ops.options & BuildOptions.ConnectWithProfiler) != BuildOptions.None) && ProjectConfigData.postProfilerEvents, settings.PlayerBuildVersion);
                var res = settings.ActivePlayerDataBuilder.BuildData<AddressablesPlayerBuildResult>(context);
                BuildPipeline.BuildPlayer(ops);
                if (!string.IsNullOrEmpty(res.ContentStateDataPath))
                {      
                    var newPath = ContentUpdateScript.GetContentStateDataPath(false);
                    if (File.Exists(newPath))
                          File.Delete(newPath);
                    File.Copy(res.ContentStateDataPath, newPath);
                }

                if (BuildScript.buildCompleted != null)
                    BuildScript.buildCompleted(res);
                settings.DataBuilderCompleted(settings.ActivePlayerDataBuilder, res);
            }
        }

        private static void OnEditorPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                var settings = AddressableAssetSettingsDefaultObject.Settings;
                if (settings == null)
                    return;
                if (settings.ActivePlayModeDataBuilder == null)
                {
                    var err = "Active play mode build script is null.";
                    Debug.LogError(err);

                    if (BuildScript.buildCompleted != null)
                        BuildScript.buildCompleted(new AddressableAssetBuildResult() { Duration = 0, Error = err });
                    return;
                }

                if (!settings.ActivePlayerDataBuilder.CanBuildData<AddressablesPlayModeBuildResult>())
                {
                    var err = string.Format("Active build script {0} cannot build AddressablesPlayModeBuildResult.", settings.ActivePlayModeDataBuilder);
                    Debug.LogError(err);
                    if (BuildScript.buildCompleted != null)
                        BuildScript.buildCompleted(new AddressableAssetBuildResult() { Duration = 0, Error = err });
                    return;
                }

                SceneManagerState.Record();
                var res = settings.ActivePlayModeDataBuilder.BuildData<AddressablesPlayModeBuildResult>(new AddressablesBuildDataBuilderContext(settings));
                SceneManagerState.AddScenesForPlayMode(res.ScenesToAdd);
                if (BuildScript.buildCompleted != null)
                    BuildScript.buildCompleted(res);
                settings.DataBuilderCompleted(settings.ActivePlayerDataBuilder, res);
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                var settings = AddressableAssetSettingsDefaultObject.Settings;
                if(settings != null && settings.ActivePlayerDataBuilder != null && settings.ActivePlayerDataBuilder.CanBuildData<AddressablesPlayModeBuildResult>())
                    SceneManagerState.Restore();
            }
        }
    }
}