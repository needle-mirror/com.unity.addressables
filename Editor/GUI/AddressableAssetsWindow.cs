using UnityEngine;
using UnityEngine.AddressableAssets;

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
            var bundleList = AssetDatabase.GetAllAssetBundleNames();
            if (EditorUtility.DisplayDialog("Legacy Bundles Detected", "We have detected the use of legacy bundles in this project.  Would you like to auto-convert those into Addressables? \nThis will take each asset bundle you have defined (we have detected " + bundleList.Length + " bundles), create an Addressables group with a matching name, then move all assets from those bundles into corresponding groups.  This will remove the asset bundle assignment from all assets, and remove all asset bundle definitions from this project.  This cannot be undone.", "Convert", "Ignore"))
            {
                AddressableAssetUtility.ConvertAssetBundlesToAddressables();
            }
            else
                m_ignoreLegacyBundles = true;
        }

        public void OnGUI()
        {
            if (AddressableAssetSettingsDefaultObject.Settings == null)
            {
                GUILayout.Space(50);
                if (GUILayout.Button("Create Addressables Settings"))
                {
                    AddressableAssetSettingsDefaultObject.Settings = AddressableAssetSettings.Create(AddressableAssetSettingsDefaultObject.kDefaultConfigFolder, AddressableAssetSettingsDefaultObject.kDefaultConfigAssetName, true, true);
                }
                if (GUILayout.Button("Import Addressables Settings"))
                {
                    var path = EditorUtility.OpenFilePanel("Addressables Settings Object", AddressableAssetSettingsDefaultObject.kDefaultConfigFolder, "asset");
                    if (!string.IsNullOrEmpty(path))
                    {
                        var i = path.ToLower().IndexOf("/assets/");
                        if (i > 0)
                        {
                            path = path.Substring(i + 1);
                            Addressables.LogFormat("Loading Addressables Settings from {0}", path);
                            var obj = AssetDatabase.LoadAssetAtPath<AddressableAssetSettings>(path);
                            if (obj != null)
                                AddressableAssetSettingsDefaultObject.Settings = obj;
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
                            if (AddressableAssetUtility.IsPathValidForEntry(path))
                            {
                                var guid = AssetDatabase.AssetPathToGUID(path);
                                if (!string.IsNullOrEmpty(guid))
                                {
                                    if (AddressableAssetSettingsDefaultObject.Settings == null)
                                        AddressableAssetSettingsDefaultObject.Settings = AddressableAssetSettings.Create(AddressableAssetSettingsDefaultObject.kDefaultConfigFolder, AddressableAssetSettingsDefaultObject.kDefaultConfigAssetName, true, true);
                                    Undo.RecordObject(AddressableAssetSettingsDefaultObject.Settings, "AddressableAssetSettings");
                                    AddressableAssetSettingsDefaultObject.Settings.CreateOrMoveEntry(guid, AddressableAssetSettingsDefaultObject.Settings.DefaultGroup);
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
