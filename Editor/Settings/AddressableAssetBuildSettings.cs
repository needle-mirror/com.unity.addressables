using System;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;

#if UNITY_2018_3_OR_NEWER
using BuildCompression = UnityEngine.BuildCompression;
#else
using BuildCompression = UnityEditor.Build.Content.BuildCompression;
#endif

namespace UnityEditor.AddressableAssets
{
    /// <summary>
    /// Build settings for addressables.
    /// </summary>
    [Serializable]
    public class AddressableAssetBuildSettings
    {
        /// <summary>
        /// Build compression.
        /// </summary>
        public BuildCompression compression
        {
            get { return m_compression; }
            set
            {
                m_compression = value;
                PostModificationEvent();
            }
        }

        [UnityEngine.SerializeField]
#if UNITY_2018_3_OR_NEWER
        private BuildCompression m_compression = BuildCompression.LZ4;
#else
        private BuildCompression m_compression = BuildCompression.DefaultLZ4;
#endif
        /// <summary>
        /// Controls whether to compile scripts when running in virtual mode.  When disabled, build times are faster but the simulated bundle contents may not be accurate due to including editor code.
        /// </summary>
        public bool compileScriptsInVirtualMode
        {
            get { return m_compileScriptsInVirtualMode; }
            set
            {
                m_compileScriptsInVirtualMode = value;
                PostModificationEvent();
            }
        }
        [UnityEngine.SerializeField]
        private bool m_compileScriptsInVirtualMode = false;

        /// <summary>
        /// Controls whether to remove temporary files after each build.  When disabled, build times in packed mode are faster, but may not reflect all changes in assets.
        /// </summary>
        public bool cleanupStreamingAssetsAfterBuilds
        {
            get { return m_cleanupStreamingAssetsAfterBuilds; }
            set
            {
                m_cleanupStreamingAssetsAfterBuilds = value;
                PostModificationEvent();
            }
        }
        [UnityEngine.SerializeField]
        private bool m_cleanupStreamingAssetsAfterBuilds = true;


        /// <summary>
        /// //Specifies where to build asset bundles, this is usually a temporary folder (or a folder in the project).  Bundles are copied out of this location to their final destination.
        /// </summary>
        public string bundleBuildPath
        {
            get { return m_bundleBuildPath; }
            set
            {
                m_bundleBuildPath = value;
                PostModificationEvent();
            }
        }

        [UnityEngine.SerializeField]
        private string m_bundleBuildPath = "Temp/com.unity.addressables/AssetBundles";

        internal void SerializeForHash(BinaryFormatter formatter, Stream stream)
        {
            formatter.Serialize(stream, compression);
        }

        [NonSerialized]
        AddressableAssetSettings m_Settings;
        void PostModificationEvent()
        {
            if (m_Settings != null)
                m_Settings.PostModificationEvent(AddressableAssetSettings.ModificationEvent.BuildSettingsChanged, this);
        }
        internal void OnAfterDeserialize(AddressableAssetSettings settings)
        {
            m_Settings = settings;
        }

        internal void Validate(AddressableAssetSettings addressableAssetSettings)
        {

        }
    }
}
