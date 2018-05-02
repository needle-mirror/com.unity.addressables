using System.ComponentModel;
using UnityEngine;
using UnityEngine.ResourceManagement;
using System.IO;

namespace UnityEditor.AddressableAssets
{
    [Description("Advanced Packed Content")]
    public class AdvancedAssetBundleAssetGroupProcessor : AssetBundleAssetGroupProcessor
    {
    
        [SerializeField]
        protected string m_buildPathId;

        [SerializeField]
        protected string m_loadPrefixId;

        [SerializeField]
        protected string m_bundleLoadProvider;

        /// <summary>
        /// TODO - doc
        /// </summary>
        public string buildPathId
        {
            get
            {
                if (string.IsNullOrEmpty(m_buildPathId))
                {
                    m_buildPathId = AddressableAssetSettings.ProfileSettings.TryGetProfileID("LocalBuildPath");
                }
                return m_buildPathId;
            }
        }
        /// <summary>
        /// TODO - doc
        /// </summary>
        public string loadPrefixId
        {
            get
            {
                if (string.IsNullOrEmpty(m_loadPrefixId))
                {
                    m_loadPrefixId = AddressableAssetSettings.ProfileSettings.TryGetProfileID("LocalLoadPrefix");
                }
                return m_loadPrefixId;
            }
        }
        /// <summary>
        /// TODO - doc
        /// </summary>
        public string bundleLoadProvider
        {
            get
            {
                if (string.IsNullOrEmpty(m_bundleLoadProvider))
                {
                    m_bundleLoadProvider = typeof(RemoteAssetBundleProvider).FullName;
                }
                return m_bundleLoadProvider;
            }
        }


        internal override void Initialize(AddressableAssetSettings settings)
        {
        }

        /// <summary>
        /// TODO - doc
        /// </summary>
        public BundleMode bundleMode = BundleMode.PackTogether;
        internal override string displayName { get { return "Advanced Packed Content"; } }
        internal override void SerializeForHash(System.Runtime.Serialization.Formatters.Binary.BinaryFormatter formatter, Stream stream)
        {
            formatter.Serialize(stream, bundleMode);
            formatter.Serialize(stream, buildPathId);
            formatter.Serialize(stream, loadPrefixId);
            formatter.Serialize(stream, bundleLoadProvider);
        }

        protected override string GetBuildPath(AddressableAssetSettings settings)
        {
            return AddressableAssetSettings.ProfileSettings.ProfileIDData.Evaluate(settings.profileSettings, settings.activeProfileId, buildPathId);
        }

        protected override string GetBundleLoadPath(AddressableAssetSettings settings, string postfixPath)
        {
            return AddressableAssetSettings.ProfileSettings.ProfileIDData.Evaluate(settings.profileSettings, settings.activeProfileId, loadPrefixId) + "/" + postfixPath;
        }

        protected override string GetBundleLoadProvider(AddressableAssetSettings settings)
        {
            return bundleLoadProvider;
        }
        
        protected override BundleMode GetBundleMode(AddressableAssetSettings settings)
        {
            return bundleMode;
        }

        [SerializeField]
        Vector2 position = new Vector2();
        internal override void OnDrawGUI(AddressableAssetSettings settings, Rect rect)
        {
            GUILayout.BeginArea(rect);
            position = EditorGUILayout.BeginScrollView(position, false, false, GUILayout.MaxWidth(rect.width));
            bool oldWrap = EditorStyles.label.wordWrap;
            EditorStyles.label.wordWrap = true;
            EditorGUILayout.LabelField("Assets in this group can either be packed together or separately and will be downloaded from a URL via UnityWebRequest.");
            EditorStyles.label.wordWrap = oldWrap;
            var newBundleMode = (BundleMode)EditorGUILayout.EnumPopup("Packing Mode", bundleMode);
            if (newBundleMode != bundleMode)
                bundleMode = newBundleMode;

            var newBP = ProfilesWindow.ValueGUI(settings, "Build Path", buildPathId, AddressableAssetSettings.ProfileSettings.ProfileEntryUsage.BuildPath | AddressableAssetSettings.ProfileSettings.ProfileEntryUsage.Inline);
            var newLP = ProfilesWindow.ValueGUI(settings, "Load Prefix", loadPrefixId, AddressableAssetSettings.ProfileSettings.ProfileEntryUsage.LoadPrefix | AddressableAssetSettings.ProfileSettings.ProfileEntryUsage.Inline);
            var newLM = EditorGUILayout.DelayedTextField(new GUIContent("Load Method"), bundleLoadProvider);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            if (newBP != buildPathId || newLP != loadPrefixId || newLM != bundleLoadProvider)
            {
                m_buildPathId = newBP;
                m_loadPrefixId = newLP;
                m_bundleLoadProvider = newLM;
                settings.PostModificationEvent(AddressableAssetSettings.ModificationEvent.GroupProcessorModified, this);
            }

        }
    }
}
