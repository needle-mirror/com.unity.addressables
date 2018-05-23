using System;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using UnityEditor.Build.Content;
using UnityEngine.AddressableAssets;
using UnityEngine;

namespace UnityEditor.AddressableAssets
{
    /// <summary>
    /// TODO - doc
    /// </summary>
    public partial class AddressableAssetSettings
    {
        /// <summary>
        /// TODO - doc
        /// </summary>
        [Serializable]
        public class BuildSettings
        {
            /// <summary>
            /// TODO - doc
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
            /// TODO - doc
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
            /// TODO - doc
            /// //where to build bundles, this is usually a temporary folder (or a folder in the project).  bundles are copied out of this location to their final destination
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
            private string m_bundleBuildPath = "Temp/AddressableAssetsBundles";

            internal void SerializeForHash(BinaryFormatter formatter, Stream stream)
            {
                formatter.Serialize(stream, compression);
            }

            [NonSerialized]
            AddressableAssetSettings m_Settings;
            void PostModificationEvent()
            {
                if (m_Settings != null)
                    m_Settings.PostModificationEvent(ModificationEvent.BuildSettingsChanged, this);
            }
            internal void OnAfterDeserialize(AddressableAssetSettings settings)
            {
                m_Settings = settings;
            }
        }
    }
}
