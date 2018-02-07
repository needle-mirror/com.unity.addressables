using UnityEngine;
using UnityEditor;

namespace UnityEditor.AddressableAssets
{
    internal class AddressableAssetsWindow : EditorWindow
    {
        [SerializeField]
        AddressableAssetsSettingsGroupEditor m_groupEditor = null;
        [SerializeField]
        AddressableAssetsSettingsConfigEditor m_configEditor = null;
        [SerializeField]
        ProfileSettingsEditor m_profileEditor = null;
        [SerializeField]
        AssetSettingsPreview m_previewEditor = null;
        [SerializeField]
        AssetPublishEditor m_publishEditor = null;

        enum TabList
        {
            Assets = 0,
            Config,
            Profile,
            Preview,
            Publish,
        }
        [SerializeField]
        int currentTab = 0;
        string[] labels = new string[5] { "Assets", "Config", "Profiles", "Preview", "Publish" };


        [SerializeField]
        bool m_ignoreLegacyBundles = false;  

        [MenuItem("Window/Addressable Assets", priority = 2050)]
        static void Init()
        {
            var window = GetWindow<AddressableAssetsWindow>();
            window.titleContent = new GUIContent("Addressables");
            window.Show();
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
            if (m_configEditor != null)
                m_configEditor.OnEnable();
            if (m_publishEditor != null)
                m_publishEditor.OnEnable();
        }

        public void OnDisable()
        {
            if (m_groupEditor != null)
                m_groupEditor.OnDisable();
            if (m_configEditor != null)
                m_configEditor.OnDisable();
            if (m_publishEditor != null)
                m_publishEditor.OnDisable();
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
                if (GUILayout.Button("Begin Using The Addressables System"))
                {
                    settingsObject = AddressableAssetSettings.GetDefault(true, true);
                }
                GUILayout.Space(20);
                GUILayout.BeginHorizontal();
                GUILayout.Space(50);
                GUI.skin.label.wordWrap = true;
                GUILayout.Label("Click the \"Begin\" button above or simply drag an asset into this window to start using Addressables.  Once you begin, the Addressables system will save some assets to your project to keep up with its data");
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
                var profileNames = settingsObject.profileSettings.profileNames;
                int currentProfileIndex = settingsObject.profileSettings.GetIndexOfProfile(settingsObject.activeProfile);
                int newProfileIndex = EditorGUILayout.Popup("Active Profile", currentProfileIndex, profileNames.ToArray());
                if (newProfileIndex != currentProfileIndex)
                    settingsObject.activeProfile = settingsObject.profileSettings.GetProfileAtIndex(newProfileIndex);

                currentTab = GUILayout.Toolbar(currentTab, labels);

                Rect contentRect = new Rect(0, 38, position.width, position.height - 38);
                switch (currentTab)
                {
                    case (int)TabList.Assets:
                        if (m_groupEditor == null)
                        {
                            m_groupEditor = new AddressableAssetsSettingsGroupEditor(this);
                            m_groupEditor.OnEnable();
                        }
                        if (m_groupEditor.OnGUI(contentRect))
                            Repaint();
                        break;

                    case (int)TabList.Config:
                        if (m_configEditor == null)
                        {
                            m_configEditor = new AddressableAssetsSettingsConfigEditor();
                            m_configEditor.OnEnable();
                        }
                        if (m_configEditor.OnGUI(contentRect))
                            Repaint();
                        break;

                    case (int)TabList.Profile:
                        if (m_profileEditor == null)
                            m_profileEditor = new ProfileSettingsEditor();
                        m_profileEditor.OnGUI(contentRect);
                        break;

                    case (int)TabList.Preview:
                        if (m_previewEditor == null)
                            m_previewEditor = new AssetSettingsPreview();
                        m_previewEditor.OnGUI(contentRect);
                        break;

                    case (int)TabList.Publish:
                        if (m_publishEditor == null)
                        {
                            m_publishEditor = new AssetPublishEditor();
                            m_publishEditor.OnEnable();
                        }
                        m_publishEditor.OnGUI(contentRect);
                        break;
                }
            }
        }
    }
}
