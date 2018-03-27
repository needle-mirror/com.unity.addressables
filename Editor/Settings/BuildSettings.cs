using System;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using UnityEditor.Build.Content;
using UnityEngine.AddressableAssets;

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
            [NonSerialized]
            public bool postProfilerEvents = true;
            /// <summary>
            /// TODO - doc
            /// </summary>
            [NonSerialized]
            public ResourceManagerRuntimeData.EditorPlayMode editorPlayMode = ResourceManagerRuntimeData.EditorPlayMode.VirtualMode;
            /// <summary>
            /// TODO - doc
            /// </summary>
            [NonSerialized]
            public uint localLoadSpeed = 1024 * 1024 * 10;
            /// <summary>
            /// TODO - doc
            /// </summary>
            [NonSerialized]
            public uint remoteLoadSpeed = 1024 * 1024 * 1;
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
            private BuildCompression m_compression = BuildCompression.DefaultLZ4;
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
                formatter.Serialize(stream, postProfilerEvents);
                formatter.Serialize(stream, compression);
                formatter.Serialize(stream, localLoadSpeed);
                formatter.Serialize(stream, remoteLoadSpeed);
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
