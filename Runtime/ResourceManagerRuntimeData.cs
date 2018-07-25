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
        public static string PlayerSettingsLocation { get { return Path.Combine(Addressables.RuntimePath, "settings.json").Replace('\\', '/'); } }
        public static string PlayerCatalogLocation { get { return Path.Combine(Addressables.RuntimePath, "catalog.json").Replace('\\', '/'); } }
        public static string GetPlayerSettingsLoadLocation(EditorPlayMode mode)
        {
            if (mode == EditorPlayMode.PackedMode)
                return "{UnityEngine.AddressableAssets.Addressables.RuntimePath}/settings.json";
            var p = System.IO.Path.GetDirectoryName(UnityEngine.Application.dataPath);
            return "file://" + System.IO.Path.Combine(p, "Library/Addressables/settings_" + mode + ".json");
        }

        public static string GetPlayerCatalogLoadLocation(EditorPlayMode mode)
        {
            if (mode == EditorPlayMode.PackedMode)
                return "{UnityEngine.AddressableAssets.Addressables.RuntimePath}/catalog.json";
            var p = System.IO.Path.GetDirectoryName(UnityEngine.Application.dataPath);
            return "file://" + System.IO.Path.Combine(p, "Library/Addressables/catalog_" + mode + ".json");
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
        [SerializeField]
        string m_settingsHash;
        public string SettingsHash { get { return m_settingsHash; } set { m_settingsHash = value; } }
        /// <summary>
        /// List of catalog locations to download in order (try remote first, then local)
        /// </summary>
        [SerializeField]
        List<ResourceLocationData> m_catalogLocations = new List<ResourceLocationData>();
        public List<ResourceLocationData> CatalogLocations { get { return m_catalogLocations; } }
        /// <summary>
        /// TODO - doc
        /// </summary>
        [SerializeField]
        bool m_usePooledInstanceProvider = false;
        public bool UsePooledInstanceProvider { get { return m_usePooledInstanceProvider; } set { m_usePooledInstanceProvider = value; } }
        /// <summary>
        /// TODO - doc
        /// </summary>
        [SerializeField]
        bool m_profileEvents = false;
        public bool ProfileEvents { get { return m_profileEvents; } set { m_profileEvents = value; } }
        /// <summary>
        /// TODO - doc
        /// </summary>
        [SerializeField]
        string m_contentVersion = "undefined";
        public string ContentVersion { get { return m_contentVersion; } set { m_contentVersion = value; } }
        /// <summary>
        /// TODO - doc
        /// </summary>
        [SerializeField]
        int m_assetCacheSize = 25;
        public int AssetCacheSize { get { return m_assetCacheSize; } set { m_assetCacheSize = value; } }
        /// <summary>
        /// TODO - doc
        /// </summary>
        [SerializeField]
        float m_assetCacheAge = 5;
        public float AssetCacheAge { get { return m_assetCacheAge; } set { m_assetCacheAge = value; } }
        /// <summary>
        /// TODO - doc
        /// </summary>
        [SerializeField]
        int m_bundleCacheSize = 5;
        public int BundleCacheSize { get { return m_bundleCacheSize; } set { m_bundleCacheSize = value; } }

        /// <summary>
        /// TODO - doc
        /// </summary>
        [SerializeField]
        float m_bundleCacheAge = 5;
        public float BundleCacheAge { get { return m_bundleCacheAge; } set { m_bundleCacheAge = value; } }

#if UNITY_EDITOR
        static string LibrarySettingsLocation(EditorPlayMode mode)
        {
            return "Library/Addressables/settings_" + mode + ".json";
        }
        static string LibraryCatalogLocation(EditorPlayMode mode)
        {
            return "Library/Addressables/catalog_" + mode + ".json";
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
