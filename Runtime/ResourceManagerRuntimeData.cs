using System.Collections.Generic;
using UnityEngine.ResourceManagement;
using System;
using System.IO;


namespace UnityEngine.AddressableAssets
{
    /// <summary>
    /// TODO - doc
    /// </summary>
    public class ResourceManagerRuntimeData
    {
        /// <summary>
        /// TODO - doc
        /// </summary>
        public static string PlayerSettingsLocation { get { return Path.Combine(Application.streamingAssetsPath, "ResourceManagerRuntimeData_settings.json").Replace('\\', '/'); } }
        public static string PlayerSettingsLoadLocation { get { return "file://{UnityEngine.Application.streamingAssetsPath}/ResourceManagerRuntimeData_settings.json"; } }

        /// <summary>
        /// TODO - doc
        /// </summary>
        public enum EditorPlayMode
        {
            FastMode,
            VirtualMode,
            PackedMode
        }
        /// <summary>
        /// TODO - doc
        /// </summary>
        public string settingsHash;
        /// <summary>
        /// List of catalog locations to download in order (try remote first, then local)
        /// </summary>
        public ResourceLocationList catalogLocations = new ResourceLocationList();
        /// <summary>
        /// TODO - doc
        /// </summary>
        public bool usePooledInstanceProvider = false;
        /// <summary>
        /// TODO - doc
        /// </summary>
        public bool profileEvents = true;
        /// <summary>
        /// TODO - doc
        /// </summary>
        public string contentVersion = "undefined";
        /// <summary>
        /// TODO - doc
        /// </summary>
        public int assetCacheSize = 25;
        /// <summary>
        /// TODO - doc
        /// </summary>
        public float assetCacheAge = 5;
        /// <summary>
        /// TODO - doc
        /// </summary>
        public int bundleCacheSize = 5;
        /// <summary>
        /// TODO - doc
        /// </summary>
        public float bundleCacheAge = 5;
#if UNITY_EDITOR

        static string LibrarySettingsLocation(string mode)
        {
            return "Library/ResourceManagerRuntimeData_settings_" + mode + ".json";
        }
        static string LibraryCatalogLocation(string mode)
        {
            return "Library/ResourceManagerRuntimeData_catalog_" + mode + ".json";
        }

        /// <summary>
        /// TODO - doc
        /// </summary>
        public static bool LoadFromLibrary(string mode, ref ResourceManagerRuntimeData runtimeData, ref ResourceLocationList catalog)
        {
            try
            {
                runtimeData = JsonUtility.FromJson<ResourceManagerRuntimeData>(File.ReadAllText(LibrarySettingsLocation(mode)));
                catalog = JsonUtility.FromJson<ResourceLocationList>(File.ReadAllText(LibraryCatalogLocation(mode)));
                return runtimeData != null && catalog != null;
            }
            catch (Exception)
            {
            }
            return false;
        }
        /// <summary>
        /// TODO - doc
        /// </summary>
        public static void DeleteFromLibrary(string mode)
        {
            try
            {
                if (File.Exists(LibrarySettingsLocation(mode)))
                    File.Delete(LibrarySettingsLocation(mode));
                if (File.Exists(LibraryCatalogLocation(mode)))
                    File.Delete(LibraryCatalogLocation(mode));
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// TODO - doc
        /// </summary>
        public static void Cleanup()
        {
            if (File.Exists(PlayerSettingsLocation))
            {
                File.Delete(PlayerSettingsLocation);
                var metaFile = PlayerSettingsLocation + ".meta";
                if (File.Exists(metaFile))
                    File.Delete(metaFile);
            }
        }

        /// <summary>
        /// TODO - doc
        /// </summary>
        public void Save(ResourceLocationList catalog, string mode)
        {
            try
            {
                var data = JsonUtility.ToJson(this);
                if (!Directory.Exists(Path.GetDirectoryName(PlayerSettingsLocation)))
                    Directory.CreateDirectory(Path.GetDirectoryName(PlayerSettingsLocation));
                if (!Directory.Exists(Path.GetDirectoryName(LibrarySettingsLocation(mode))))
                    Directory.CreateDirectory(Path.GetDirectoryName(LibrarySettingsLocation(mode)));
                File.WriteAllText(PlayerSettingsLocation, data);
                File.WriteAllText(LibrarySettingsLocation(mode), data);
                data = JsonUtility.ToJson(catalog);
                File.WriteAllText(LibraryCatalogLocation(mode), data);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        public bool CopyFromLibraryToPlayer(string mode)
        {
            try
            {
                if (!File.Exists(LibrarySettingsLocation(mode)) || !File.Exists(LibraryCatalogLocation(mode)))
                    return false;

                if (File.Exists(PlayerSettingsLocation))
                    File.Delete(PlayerSettingsLocation);

                var dirName = Path.GetDirectoryName(PlayerSettingsLocation);
                if (!string.IsNullOrEmpty(dirName) && !Directory.Exists(dirName))
                    Directory.CreateDirectory(dirName);
                File.Copy(LibrarySettingsLocation(mode), PlayerSettingsLocation);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return false;
            }
        }
#endif
    }
}
