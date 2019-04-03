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
        public static string PlayerSettingsLocation { get { return Path.Combine(Application.streamingAssetsPath, "Addressables_settings.json").Replace('\\', '/'); } }
        public static string PlayerCatalogLocation { get { return Path.Combine(Application.streamingAssetsPath, "Addressables_catalog.json").Replace('\\', '/'); } }
        public static string GetPlayerSettingsLoadLocation(EditorPlayMode mode)
        {
            if (mode == EditorPlayMode.PackedMode)
                return "{UnityEngine.Application.streamingAssetsPath}/Addressables_settings.json";
            var p = System.IO.Path.GetDirectoryName(UnityEngine.Application.dataPath);
            return "file://" + System.IO.Path.Combine(p, "Library/Addressables_settings_" + mode + ".json");
        }

        public static string GetPlayerCatalogLoadLocation(EditorPlayMode mode)
        {
            if (mode == EditorPlayMode.PackedMode)
                return "{UnityEngine.Application.streamingAssetsPath}/Addressables_catalog.json";
            var p = System.IO.Path.GetDirectoryName(UnityEngine.Application.dataPath);
            return "file://" + System.IO.Path.Combine(p, "Library/Addressables_catalog_" + mode + ".json");
        }

        /// <summary>
        /// TODO - doc
        /// </summary>
        public enum EditorPlayMode
        {
            Invalid,
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
        public List<ResourceLocationData> catalogLocations = new List<ResourceLocationData>();
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

        static string LibrarySettingsLocation(EditorPlayMode mode)
        {
            return "Library/Addressables_settings_" + mode + ".json";
        }
        static string LibraryCatalogLocation(EditorPlayMode mode)
        {
            return "Library/Addressables_catalog_" + mode + ".json";
        }

        /// <summary>
        /// TODO - doc
        /// </summary>
        public static bool LoadFromLibrary(EditorPlayMode mode, ref ResourceManagerRuntimeData runtimeData, ref ContentCatalogData catalog)
        {
            try
            {
                runtimeData = JsonUtility.FromJson<ResourceManagerRuntimeData>(File.ReadAllText(LibrarySettingsLocation(mode)));
                catalog = JsonUtility.FromJson<ContentCatalogData>(File.ReadAllText(LibraryCatalogLocation(mode)));
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
        public static void DeleteFromLibrary(EditorPlayMode mode)
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
        public void Save(ContentCatalogData catalog, EditorPlayMode mode)
        {
            try
            {
                var settingsData = JsonUtility.ToJson(this);
                var catalogData = JsonUtility.ToJson(catalog);
                if (mode == EditorPlayMode.PackedMode)
                {
                    if (!Directory.Exists(Path.GetDirectoryName(PlayerSettingsLocation)))
                        Directory.CreateDirectory(Path.GetDirectoryName(PlayerSettingsLocation));
                    File.WriteAllText(PlayerSettingsLocation, settingsData);
                    File.WriteAllText(PlayerCatalogLocation, catalogData);
                }

                if (!Directory.Exists(Path.GetDirectoryName(LibrarySettingsLocation(mode))))
                    Directory.CreateDirectory(Path.GetDirectoryName(LibrarySettingsLocation(mode)));
                File.WriteAllText(LibrarySettingsLocation(mode), settingsData);
                File.WriteAllText(LibraryCatalogLocation(mode), catalogData);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

#endif
    }
}
