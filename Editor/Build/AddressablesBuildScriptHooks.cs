using System;
using System.IO;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

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

    class CopyContentStateIntoBuild : IPostprocessBuildWithReport
    {
        public int callbackOrder { get { return 0; } }
    
        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.result == BuildResult.Succeeded)
            {
                var libraryPath = Path.GetDirectoryName(Application.dataPath) + "/Library/com.unity.addressables/addressables_content_state.bin";
                if (!string.IsNullOrEmpty(libraryPath))
                {
                    var newPath = ContentUpdateScript.GetContentStateDataPath(false);
                    if (File.Exists(newPath))
                        File.Delete(newPath);
                    File.Copy(libraryPath, newPath);
                }
            }
        }
    }

    static class AddressablesBuildScriptHooks
    {
        [InitializeOnLoadMethod]
        static void Init()
        {
            EditorApplication.playModeStateChanged += OnEditorPlayModeChanged;
        }

        static void OnEditorPlayModeChanged(PlayModeStateChange state)
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
                        BuildScript.buildCompleted(new AddressableAssetBuildResult { Duration = 0, Error = err });
                    return;
                }

                if (!settings.ActivePlayModeDataBuilder.CanBuildData<AddressablesPlayModeBuildResult>())
                {
                    var err = string.Format("Active build script {0} cannot build AddressablesPlayModeBuildResult.", settings.ActivePlayModeDataBuilder);
                    Debug.LogError(err);
                    if (BuildScript.buildCompleted != null)
                        BuildScript.buildCompleted(new AddressableAssetBuildResult { Duration = 0, Error = err });
                    return;
                }

                SceneManagerState.Record();
                var res = settings.ActivePlayModeDataBuilder.BuildData<AddressablesPlayModeBuildResult>(new AddressablesBuildDataBuilderContext(settings));
                if (!string.IsNullOrEmpty(res.Error))
                {
                    Debug.LogError(res.Error);
                    EditorApplication.isPlaying = false;
                }
                else
                {
                    SceneManagerState.AddScenesForPlayMode(res.ScenesToAdd);
                    if (BuildScript.buildCompleted != null)
                        BuildScript.buildCompleted(res);
                    settings.DataBuilderCompleted(settings.ActivePlayModeDataBuilder, res);
                }
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                var settings = AddressableAssetSettingsDefaultObject.Settings;
                if(settings != null && settings.ActivePlayModeDataBuilder != null && settings.ActivePlayModeDataBuilder.CanBuildData<AddressablesPlayModeBuildResult>())
                    SceneManagerState.Restore();
            }
        }
    }
}