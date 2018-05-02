using UnityEngine;

namespace UnityEditor.AddressableAssets
{
    internal class AddressableAssetsWindow : EditorWindow
    {
        [SerializeField]
        AddressableAssetsSettingsGroupEditor m_groupEditor = null;

        enum TabList
        {
            Assets = 0,
            Config,
            Profile,
            Preview,
            Publish,
        }

        [SerializeField]
        bool m_ignoreLegacyBundles = false;  

        [MenuItem("Window/Asset Management/Addressable Assets", priority = 2050)]
        static void Init()
        {
            var window = GetWindow<AddressableAssetsWindow>();
            window.titleContent = new GUIContent("Addressables");
            window.Show();
        }
        public static Vector2 GetWindowPosition()
        {
            var window = GetWindow<AddressableAssetsWindow>();
            return new Vector2(window.position.x, window.position.y);
        }

        public void OnEnable()
        {
            if (!m_ignoreLegacyBundles)
            {
                var bundleList = AssetDatabase.GetAllAssetBundleNames();
                if (bundleList != null && bundleList.Length > 0)
                    OfferToConvert();
            }
            if (m_groupEditor != null)
                m_groupEditor.OnEnable();
        }

        public void OnDisable()
        {
            if (m_groupEditor != null)
                m_groupEditor.OnDisable();
        }

        internal void OfferToConvert()
        {
            if (EditorUtility.DisplayDialog("Legacy Bundles Detected", "We have detected the use of legacy bundles in this project.  Would you like to auto-convert those into Addressables?", "Convert", "Ignore"))
            {
                AddressablesUtility.ConvertAssetBundlesToAddressables();
            }
            else
                m_ignoreLegacyBundles = true;
        }

        public void OnGUI()
        {
    
            var settingsObject = AddressableAssetSettings.GetDefault(false, false);
            if (settingsObject == null)
            {
                GUILayout.Space(50);
                if (GUILayout.Button("Create Addressables Settings"))
                {
                    settingsObject = AddressableAssetSettings.GetDefault(true, true);
                }
                if (GUILayout.Button("Import Addressables Settings"))
                {
                    var path = EditorUtility.OpenFilePanel("Addressables Settings Object", AddressableAssetSettings.DefaultConfigFolder, "asset");
                    if (!string.IsNullOrEmpty(path))
                    {
                        var i = path.ToLower().IndexOf("/assets/");
                        if (i > 0)
                        {
                            path = path.Substring(i+1);
                            Debug.LogFormat("Loading Addressables Settings from {0}", path);
                            var obj = AssetDatabase.LoadAssetAtPath<AddressableAssetSettings>(path);
                            if (obj != null)
                            {
                                EditorBuildSettings.AddConfigObject(AddressableAssetSettings.DefaultConfigName, obj, true);
                                settingsObject = AddressableAssetSettings.GetDefault(true, true);
                            }
                        }
                    }
                }
                GUILayout.Space(20);
                GUILayout.BeginHorizontal();
                GUILayout.Space(50);
                GUI.skin.label.wordWrap = true;
                GUILayout.Label("Click the \"Create\" or \"Import\" button above or simply drag an asset into this window to start using Addressables.  Once you begin, the Addressables system will save some assets to your project to keep up with its data");
                GUILayout.Space(50);
                GUILayout.EndHorizontal();
                switch (Event.current.type)
                {
                    case EventType.DragPerform:
                        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                        foreach (var path in DragAndDrop.paths)
                        {
                            if(AddressablesUtility.IsPathValidForEntry(path))
                            {
                                var guid = AssetDatabase.AssetPathToGUID(path);
                                if (!string.IsNullOrEmpty(guid))
                                {
                                    if(settingsObject == null)
                                        settingsObject = AddressableAssetSettings.GetDefault(true, true);
                                    Undo.RecordObject(settingsObject, "AddressableAssetSettings");
                                    settingsObject.CreateOrMoveEntry(guid, settingsObject.DefaultGroup);
                                }
                            }
                        }
                        break;
                    case EventType.DragUpdated:
                    case EventType.DragExited:
                        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                        break;
                }
            }
            else
            {
                Rect contentRect = new Rect(0, 0, position.width, position.height);
 
                    if (m_groupEditor == null)
                {
                    m_groupEditor = new AddressableAssetsSettingsGroupEditor(this);
                    m_groupEditor.OnEnable();
                }
                if (m_groupEditor.OnGUI(contentRect))
                            Repaint();
            }
        }
    }
}
