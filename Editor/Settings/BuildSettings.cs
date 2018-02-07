using System;
using UnityEditor.Experimental.Build.AssetBundle;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
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
            public int localLoadSpeed = 1024 * 1024 * 10;
            /// <summary>
            /// TODO - doc
            /// </summary>
            [NonSerialized]
            public int remoteLoadSpeed = 1024 * 1024 * 1;
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
            
            /// <summary>
            /// TODO - doc
            /// </summary>
            public bool downloadRemoteCatalog
            {
                get { return m_downloadRemoteCatalog; }
                set
                {
                    m_downloadRemoteCatalog = value;
                    PostModificationEvent();
                }
            }
            [UnityEngine.SerializeField]
            private bool m_downloadRemoteCatalog = false;
            /// <summary>
            /// TODO - doc
            /// </summary>
            public bool useCache
            {
                get { return m_useCache; }
                set
                {
                    m_useCache = value;
                    PostModificationEvent();
                }
            }
            [UnityEngine.SerializeField]
            private bool m_useCache = false;
            /// <summary>
            /// TODO - doc
            /// </summary>
            public ProfileSettings.ProfileValue remoteCatalogLocation
            {
                get
                {
                    if (m_remoteCatalogLocation == null || string.IsNullOrEmpty(m_remoteCatalogLocation.value))
                    {
                        if (m_Settings != null)
                            remoteCatalogLocation = m_Settings.profileSettings.CreateProfileValue(m_Settings.profileSettings.GetVariableIdFromName("RemoteCatalogURL"));
                        else
                            remoteCatalogLocation = new ProfileSettings.ProfileValue();
                    }
                    return m_remoteCatalogLocation;
                }
                set
                {
                    m_remoteCatalogLocation = value;
                    PostModificationEvent();
                }
            }
            [UnityEngine.SerializeField]
            private ProfileSettings.ProfileValue m_remoteCatalogLocation = null;

            /// <summary>
            /// TODO - doc
            /// </summary>
            public ProfileSettings.ProfileValue remoteCatalogBuildLocation
            {
                get {

                    if (m_remoteCatalogBuildLocation == null || string.IsNullOrEmpty(m_remoteCatalogBuildLocation.value))
                    {
                        if (m_Settings != null)
                            remoteCatalogBuildLocation = m_Settings.profileSettings.CreateProfileValue(m_Settings.profileSettings.GetVariableIdFromName("RemoteBuildPath"));
                        else
                            remoteCatalogBuildLocation = new ProfileSettings.ProfileValue();
                    }
                    return m_remoteCatalogBuildLocation; }
                set
                {
                    PostModificationEvent();
                    m_remoteCatalogBuildLocation = value;
                }
            }
            [UnityEngine.SerializeField]
            private ProfileSettings.ProfileValue m_remoteCatalogBuildLocation = null;

            internal void SerializeForHash(BinaryFormatter formatter, Stream stream)
            {
                formatter.Serialize(stream, postProfilerEvents);
                formatter.Serialize(stream, compression);
                formatter.Serialize(stream, localLoadSpeed);
                formatter.Serialize(stream, remoteLoadSpeed);
                formatter.Serialize(stream, downloadRemoteCatalog);
                formatter.Serialize(stream, remoteCatalogLocation);
                formatter.Serialize(stream, remoteCatalogBuildLocation);
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
