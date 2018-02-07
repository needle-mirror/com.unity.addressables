using UnityEditor.SceneManagement;
using System;
using System.IO;
using UnityEngine;
using System.Collections.Generic;

namespace UnityEditor.AddressableAssets
{
    [Serializable]
    internal class SceneManagerState
    {
        [Serializable]
        internal class SceneState
        {
            [SerializeField]
            internal bool isActive;
            [SerializeField]
            internal bool isLoaded;
            [SerializeField]
            internal string path;

            internal SceneState() {}
            internal SceneState(SceneSetup s)
            {
                isActive = s.isActive;
                isLoaded = s.isLoaded;
                path = s.path;
            }

            internal SceneSetup ToSceneSetup()
            {
                var ss = new SceneSetup();
                ss.isActive = isActive;
                ss.isLoaded = isLoaded;
                ss.path = path;
                return ss;
            }
        }

        [Serializable]
        internal class EBSSceneState
        {
            [SerializeField]
            internal string guid;
            [SerializeField]
            internal bool enabled;
            internal EBSSceneState() {}
            internal EBSSceneState(EditorBuildSettingsScene s) { guid = s.guid.ToString(); enabled = s.enabled; }
            internal EditorBuildSettingsScene GetBuildSettingsScene() { return new EditorBuildSettingsScene(new GUID(guid), enabled); }
        }

        [SerializeField]
        internal SceneState[] openSceneState;
        [SerializeField]
        internal EBSSceneState[] editorBuildSettingsSceneState;

        static SceneManagerState Create(SceneSetup[] scenes)
        {
            var scenesList = new List<SceneState>();
            var state = new SceneManagerState();
            foreach (var s in scenes)
                scenesList.Add(new SceneState(s));
            state.openSceneState = scenesList.ToArray();
            var edbss = new List<EBSSceneState>();
            foreach (var s in EditorBuildSettings.scenes)
                edbss.Add(new EBSSceneState(s));
            state.editorBuildSettingsSceneState = edbss.ToArray();
            return state;
        }

        internal SceneSetup[] GetSceneSetups()
        {
            var setups = new List<SceneSetup>();
            foreach (var s in openSceneState)
                setups.Add(s.ToSceneSetup());
            return setups.ToArray();
        }

        private EditorBuildSettingsScene[] GetEditorBuildSettingScenes()
        {
            var scenes = new List<EditorBuildSettingsScene>();
            foreach (var s in editorBuildSettingsSceneState)
                scenes.Add(s.GetBuildSettingsScene());
            return scenes.ToArray();
        }

        const string path = "Library/SceneManagerState.json";
        public static void Record()
        {
            try
            {
                File.WriteAllText(path, JsonUtility.ToJson(Create(EditorSceneManager.GetSceneManagerSetup())));
            }
            catch (Exception)
            {
            }
        }

        public static void Restore()
        {
            try
            {
                var state = JsonUtility.FromJson<SceneManagerState>(File.ReadAllText(path));
                // EditorSceneManager.RestoreSceneManagerSetup(state.GetSceneSetups());
                EditorBuildSettings.scenes = state.GetEditorBuildSettingScenes();
            }
            catch (Exception)
            {
            }
        }
    }
}
