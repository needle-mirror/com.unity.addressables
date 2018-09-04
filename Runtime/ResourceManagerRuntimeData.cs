using System.Collections.Generic;
using UnityEngine.ResourceManagement;
using System;
using System.IO;


namespace UnityEngine.AddressableAssets
{
    /// <summary>
    /// Runtime data that is used to initialize the Addressables system.
    /// </summary>
    public class ResourceManagerRuntimeData
    {
        /// <summary>
        /// The runtime location for player settings.
        /// </summary>
        public static string PlayerSettingsLocation { get { return Path.Combine(Addressables.RuntimePath, "settings.json").Replace('\\', '/'); } }
        
        /// <summary>
        /// The runtime location for the local catalog.
        /// </summary>
        public static string PlayerCatalogLocation { get { return Path.Combine(Addressables.RuntimePath, "catalog.json").Replace('\\', '/'); } }
        
        /// <summary>
        /// Gets the player settings path based on play mode.
        /// </summary>
        /// <param name="mode">The mode that the player is in.</param>
        /// <returns>The player settings load path.</returns>
        public static string GetPlayerSettingsLoadLocation(EditorPlayMode mode)
        {
            if (mode == EditorPlayMode.PackedMode)
                return "{UnityEngine.AddressableAssets.Addressables.RuntimePath}/settings.json";
            var p = System.IO.Path.GetDirectoryName(UnityEngine.Application.dataPath);
            return "file://" + System.IO.Path.Combine(p, "Library/com.unity.addressables/settings_" + mode + ".json");
        }

        /// <summary>
        /// Gets the player catalog load path based on play mode.
        /// </summary>
        /// <param name="mode">The mode that the player is in.</param>
        /// <returns>The player catalog load path.</returns>
        public static string GetPlayerCatalogLoadLocation(EditorPlayMode mode)
        {
            if (mode == EditorPlayMode.PackedMode)
                return "{UnityEngine.AddressableAssets.Addressables.RuntimePath}/catalog.json";
            var p = System.IO.Path.GetDirectoryName(UnityEngine.Application.dataPath);
            return "file://" + System.IO.Path.Combine(p, "Library/com.unity.addressables/catalog_" + mode + ".json");
        }

        /// <summary>
        /// The mode the payer is in.  In built players, the mode is always PackedMode.
        /// </summary>
        public enum EditorPlayMode
        {
            Invalid,
            FastMode,
            VirtualMode,
            PackedMode
        }

        [SerializeField]
        string m_settingsHash;
        /// <summary>
        /// The hash of the settings that generated this runtime data.
        /// </summary>
        public string SettingsHash { get { return m_settingsHash; } set { m_settingsHash = value; } }
        [SerializeField]
        List<ResourceLocationData> m_catalogLocations = new List<ResourceLocationData>();
        /// <summary>
        /// List of catalog locations to download in order (try remote first, then local)
        /// </summary>
        public List<ResourceLocationData> CatalogLocations { get { return m_catalogLocations; } }
        [SerializeField]
        bool m_profileEvents = false;
        /// <summary>
        /// Flag to control whether the ResourceManager sends profiler events.
        /// </summary>
        public bool ProfileEvents { get { return m_profileEvents; } set { m_profileEvents = value; } }
        [SerializeField]
        string m_contentVersion = "undefined";
        /// <summary>
        /// The content version that was used to generate this runtime data.
        /// </summary>
        public string ContentVersion { get { return m_contentVersion; } set { m_contentVersion = value; } }


        [SerializeField]
        bool m_usePooledInstanceProvider = false;
        /// <summary>
        ///  obsolete - this will be refactored out of here.
        /// </summary>
        public bool UsePooledInstanceProvider { get { return m_usePooledInstanceProvider; } set { m_usePooledInstanceProvider = value; } }

        [SerializeField]
        int m_assetCacheSize = 25;
        /// <summary>
        /// obsolete - this will be refactored out of here.
        /// </summary>
        public int AssetCacheSize { get { return m_assetCacheSize; } set { m_assetCacheSize = value; } }
         [SerializeField]
        float m_assetCacheAge = 5;
        /// <summary>
        /// obsolete - this will be refactored out of here.
        /// </summary>
        public float AssetCacheAge { get { return m_assetCacheAge; } set { m_assetCacheAge = value; } }
        [SerializeField]
        int m_bundleCacheSize = 5;
        /// <summary>
        /// obsolete - this will be refactored out of here.
        /// </summary>
        public int BundleCacheSize { get { return m_bundleCacheSize; } set { m_bundleCacheSize = value; } }

        [SerializeField]
        float m_bundleCacheAge = 5;
        /// <summary>
        /// obsolete - this will be refactored out of here.
        /// </summary>
        public float BundleCacheAge { get { return m_bundleCacheAge; } set { m_bundleCacheAge = value; } }
       

#if UNITY_EDITOR
        static string LibrarySettingsLocation(EditorPlayMode mode)
        {
            return "Library/com.unity.addressables/settings_" + mode + ".json";
        }
        static string LibraryCatalogLocation(EditorPlayMode mode)
        {
            return "Library/com.unity.addressables/catalog_" + mode + ".json";
        }

        /// <summary>
        /// Loads the runtime data from the library folder.
        /// </summary>
        /// <param name="mode">The play mode that the editor is running in.</param>
        /// <param name="runtimeData">The runtime data object to load into.</param>
        /// <param name="catalog">The content catalog object to load into</param>
        /// <returns>True if the load succeeds.</returns>
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
        /// Delete all temporary data from the library folder.
        /// </summary>
        /// <param name="mode">Specifies the set of data to delete.</param>
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
        /// Saves the runtime data and the content catalog.
        /// </summary>
        /// <param name="catalog">The content catalog data.</param>
        /// <param name="mode">The play mode.  This is used to determine where the data is saved and loaded from.</param>
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
