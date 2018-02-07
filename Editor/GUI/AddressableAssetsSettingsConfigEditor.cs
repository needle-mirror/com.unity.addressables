using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;

namespace UnityEditor.AddressableAssets
{
    [System.Serializable]
    internal class AddressableAssetsSettingsConfigEditor
    {
        private AddressableAssetSettings settingsObject { get { return AddressableAssetSettings.GetDefault(false, false); } }
        [SerializeField]
        Vector2 scrollPosition = new Vector2();
        [SerializeField]
        bool expandSimulation = false;
        

        public bool OnGUI(Rect pos)
        {
            if (settingsObject == null)
                return false;

            var bs = settingsObject.buildSettings;
            
            GUILayout.BeginArea(pos);
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, false, false, GUILayout.MaxWidth(pos.width));

            GUILayout.Space(10);
            bool doBuildNow = false;

            EditorGUILayout.LabelField("Play Mode");

            EditorGUI.indentLevel++;
            bs.editorPlayMode = (ResourceManagerRuntimeData.EditorPlayMode)EditorGUILayout.EnumPopup(bs.editorPlayMode);
            

            switch (bs.editorPlayMode)
            {
                default:
                case ResourceManagerRuntimeData.EditorPlayMode.FastMode:
                    EditorGUILayout.HelpBox(new GUIContent("Assets will be loaded directly through the Asset Database.  This mode is for quick iteration and does not try to simulate packed content behavior."), true);
                    break;
                case ResourceManagerRuntimeData.EditorPlayMode.VirtualMode:

                    EditorGUILayout.HelpBox(new GUIContent("Content is analyzed for layout and dependencies, but will not be packed. Assets will load from the Asset Database though the ResourceManager as if they were loaded through bundles. The ResourceManager Profiler shows asset usage without having to fully pack content."), true);

                    //not copied...
                    expandSimulation = EditorGUILayout.Foldout(expandSimulation, "Simulation Settings");
                    if (expandSimulation)
                    {
                        EditorGUI.indentLevel++;
                        bs.localLoadSpeed = (int)(EditorGUILayout.FloatField("Local Load (MB/s)", bs.localLoadSpeed / 1048576f) * 1048576);
                        bs.remoteLoadSpeed = (int)(EditorGUILayout.FloatField("Remote Load (MB/s)", bs.remoteLoadSpeed / 1048576f) * 1048576);
                        EditorGUI.indentLevel--;
                    }

                    break;
                case ResourceManagerRuntimeData.EditorPlayMode.PackedMode:
                    EditorGUILayout.HelpBox(new GUIContent("Content is fully packed when entering play mode. This mode takes the most amount of time to prepare but provides the most accurate behavior for resource loading."), true);
                    break;
            }

           // using (new EditorGUI.DisabledScope(bs.editorPlayMode == ResourceManagerRuntimeData.EditorPlayMode.FastMode))
            {
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("    Build Data Now    "))
                {
                    doBuildNow = true;
                }
                GUILayout.EndHorizontal();
            }
            EditorGUI.indentLevel--;
            /*
            bs.downloadRemoteCatalog = EditorGUILayout.ToggleLeft(new GUIContent("Download Remote Catalog", "Download remote catalog on game startup"), bs.downloadRemoteCatalog);
            using (new EditorGUI.DisabledScope(!bs.downloadRemoteCatalog))
            {
                EditorGUI.indentLevel++;

                ProfileSettingsEditor.ValueGUI(settingsObject, "Remote Location", bs.remoteCatalogLocation);
                ProfileSettingsEditor.ValueGUI(settingsObject, "Build Location", bs.remoteCatalogBuildLocation);
                EditorGUI.indentLevel--;
            }
            */
            bs.postProfilerEvents = EditorGUILayout.ToggleLeft("Send Profiler Events", bs.postProfilerEvents);
            
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            

            if (doBuildNow)
                BuildScript.PrepareRuntimeData(false, true, true, true, false, BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget), EditorUserBuildSettings.activeBuildTarget);

            return false;
        }

        internal void OnEnable()
        {
            if (settingsObject == null)
                return;

            //LoadData...
            var dataPath = System.IO.Path.GetFullPath(".");
            dataPath = dataPath.Replace("\\", "/");
            dataPath += "/Library/AddressablesConfig.dat";

            if (File.Exists(dataPath))
            {
                BinaryFormatter bf = new BinaryFormatter();
                FileStream file = File.Open(dataPath, FileMode.Open);
                var data = bf.Deserialize(file) as LocalConfigData;
                if (data != null)
                {
                    var bs = settingsObject.buildSettings;
                    bs.postProfilerEvents = data.postProfilerEvents;
                    bs.editorPlayMode = data.editorPlayMode;
                    bs.localLoadSpeed = data.localLoadSpeed;
                    bs.remoteLoadSpeed = data.remoteLoadSpeed;
                }
                file.Close();
            }
        }

        internal void OnDisable()
        {
            if (settingsObject == null)
                return;

            var dataPath = System.IO.Path.GetFullPath(".");
            dataPath = dataPath.Replace("\\", "/");
            dataPath += "/Library/AddressablesConfig.dat";

            BinaryFormatter bf = new BinaryFormatter();
            FileStream file = File.Create(dataPath);

            var bs = settingsObject.buildSettings;
            var locData = new LocalConfigData
            {
                postProfilerEvents = bs.postProfilerEvents,
                editorPlayMode = bs.editorPlayMode,
                localLoadSpeed = bs.localLoadSpeed,
                remoteLoadSpeed = bs.remoteLoadSpeed,
            };
            bf.Serialize(file, locData);
            file.Close();
        }
        [System.Serializable]
        internal class LocalConfigData
        {
            internal bool postProfilerEvents;
            internal ResourceManagerRuntimeData.EditorPlayMode editorPlayMode;
            internal int localLoadSpeed;
            internal int remoteLoadSpeed;
        }
    }
}
