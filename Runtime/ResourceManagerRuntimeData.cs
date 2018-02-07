using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ResourceManagement;
using System;
using System.IO;
using UnityEngine.ResourceManagement.Diagnostics;

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
        public static string PlayerCatalogLocation { get { return Path.Combine(Application.streamingAssetsPath, "ResourceManagerRuntimeData_catalog.json").Replace('\\', '/'); } }
        public static string PlayerCatalogLoadLocation { get { return "file://{UnityEngine.Application.streamingAssetsPath}/ResourceManagerRuntimeData_catalog.json"; } }

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
        /// TODO - doc
        /// </summary>
        public EditorPlayMode resourceProviderMode = EditorPlayMode.VirtualMode;
        /// <summary>
        /// List of catalog locations to download in order (try remote first, then local)
        /// </summary>
        //public List<ResourceLocationData> catalogLocations = new List<ResourceLocationData>();
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
        public string contentVersion = "0";

        /// <summary>
        /// TODO - doc
        /// </summary>
        [RuntimeInitializeOnLoadMethod]
        public static void InitializeResourceManager()
        {
            DiagnosticEventCollector.ProfileEvents = true;
            ResourceManager.s_postEvents = true;
            ResourceManager.ResourceLocators.Add(new ResourceLocationLocator());
            ResourceManager.ResourceProviders.Add(new JsonAssetProvider());
            ResourceManager.ResourceProviders.Add(new TextDataProvider());
            ResourceManager.ResourceProviders.Add(new ContentCatalogProvider());
            var runtimeDataLocation = new ResourceLocationBase<string>("RuntimeData", PlayerSettingsLoadLocation, typeof(JsonAssetProvider).FullName);
            var initOperation = ResourceManager.LoadAsync<ResourceManagerRuntimeData, IResourceLocation>(runtimeDataLocation);
            ResourceManager.QueueInitializationOperation(initOperation);
            initOperation.Completed += (op) =>
            {
                if(op.Result != null)
                    op.Result.Init();
            };
        }

        internal void Init()
        {
            if (!Application.isEditor && resourceProviderMode != EditorPlayMode.PackedMode)
                throw new Exception("Unsupported resource provider mode in player: " + resourceProviderMode);

            ResourceManagerConfig.AddCachedValue("version", contentVersion);

            if (usePooledInstanceProvider)
                ResourceManager.InstanceProvider = new PooledInstanceProvider("PooledInstanceProvider", 2);
            else
                ResourceManager.InstanceProvider = new InstanceProvider();

            ResourceManager.SceneProvider = new SceneProvider();

            DiagnosticEventCollector.ProfileEvents = profileEvents;
            ResourceManager.s_postEvents = profileEvents;

            AddResourceProviders();

            ResourceManager.ResourceLocators.Add(new ResourceLocationLocator());
            ResourceManager.ResourceLocators.Add(new AssetReferenceLocator((assetRef) => ResourceManager.GetResourceLocation(assetRef.assetGUID)));
            AddContentCatalogs(catalogLocations);
            LoadContentCatalog(0);
        }

        void LoadContentCatalog(int index)
        {
            while (index < catalogLocations.locations.Count && !catalogLocations.locations[index].m_isLoadable)
                index++;
            var loadOp = ResourceManager.LoadAsync<ResourceLocationList, string>(catalogLocations.locations[index].m_address);
            ResourceManager.QueueInitializationOperation(loadOp);
            loadOp.Completed += (op) =>
            {
                if (op.Result != null)
                {
                    AddContentCatalogs(op.Result);
                }
                else
                {
                    if (index + 1 >= catalogLocations.locations.Count)
                        Debug.LogError("Failed to load content catalog.");
                    else
                        LoadContentCatalog(index + 1);
                }
            }; 
        }

        private void AddResourceProviders()
        {
            switch (resourceProviderMode)
            {
#if UNITY_EDITOR
                case EditorPlayMode.FastMode:
                    ResourceManager.ResourceProviders.Insert(0, new CachedProvider(new AssetDatabaseProvider()));
                    break;
                case EditorPlayMode.VirtualMode:
                    VirtualAssetBundleManager.AddProviders();
                    break;
#endif
                case EditorPlayMode.PackedMode:
                {
                    ResourceManager.ResourceProviders.Insert(0, new CachedProvider(new BundledAssetProvider()));
                    ResourceManager.ResourceProviders.Insert(0, new CachedProvider(new LocalAssetBundleProvider()));
                    ResourceManager.ResourceProviders.Insert(0, new CachedProvider(new RemoteAssetBundleProvider()));
                }
                break;
            }
            //ResourceManager.resourceProviders.Add(new LegacyResourcesProvider());
        }

        private static void AddContentCatalogs(ResourceLocationList locList)
        {
            var locMap = new Dictionary<string, IResourceLocation>();
            var dataMap = new Dictionary<string, ResourceLocationData>();
            //create and collect locations
            for (int i = 0; i < locList.locations.Count; i++)
            {
                var rlData = locList.locations[i];
                var loc = rlData.Create();
                locMap.Add(rlData.m_address, loc);
                dataMap.Add(rlData.m_address, rlData);
            }

            //fix up dependencies between them
            foreach (var kvp in locMap)
            {
                var deps = kvp.Value.Dependencies;
                var data = dataMap[kvp.Key];
                if (data.m_dependencies != null)
                {
                    foreach (var d in data.m_dependencies)
                        kvp.Value.Dependencies.Add(locMap[d]);
                }
            }

            //put them in the correct lookup table
            var ccString = new ResourceLocationMap<string>();
            var ccInt = new ResourceLocationMap<int>();
            var ccEnum = new ResourceLocationMap<Enum>();

            foreach (KeyValuePair<string, IResourceLocation> kvp in locMap)
            {
                IResourceLocation loc = kvp.Value;
                ResourceLocationData rlData = dataMap[kvp.Key];
                if (!rlData.m_isLoadable)
                    continue;

                switch (rlData.m_type)
                {
                    case ResourceLocationData.LocationType.String: AddToCatalog(locList.labels, ccString, loc, rlData.m_labelMask); break;
                    case ResourceLocationData.LocationType.Int: AddToCatalog(locList.labels, ccInt, loc, rlData.m_labelMask); break;
                    case ResourceLocationData.LocationType.Enum: AddToCatalog(locList.labels, ccEnum, loc, rlData.m_labelMask); break;
                }
                if (!string.IsNullOrEmpty(rlData.m_guid) && !ccString.m_addressMap.ContainsKey(rlData.m_guid))
                    ccString.m_addressMap.Add(rlData.m_guid, loc as IResourceLocation<string>);
            }
            if (ccString.m_addressMap.Count > 0)
                ResourceManager.ResourceLocators.Insert(0, ccString);
            if (ccInt.m_addressMap.Count > 0)
                ResourceManager.ResourceLocators.Insert(0, ccInt);
            if (ccEnum.m_addressMap.Count > 0)
                ResourceManager.ResourceLocators.Insert(0, ccEnum);
//            ResourceManager.resourceLocators.Add(new LegacyResourcesLocator());
        }

        private static void AddToCatalog<T>(List<string> labels, ResourceLocationMap<T> locations, IResourceLocation location, long labelMask)
        {
            var locT = location as IResourceLocation<T>;
            locations.m_addressMap.Add(locT.Key, locT);
            for (int t = 0; t < labels.Count; t++)
            {
                if ((labelMask & (1 << t)) != 0)
                {
                    IList<T> results = null;
                    if (!locations.m_labeledGroups.TryGetValue(labels[t], out results))
                        locations.m_labeledGroups.Add(labels[t], results = new List<T>());
                    results.Add(locT.Key);
                }
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// TODO - doc
        /// </summary>
        public ResourceManagerRuntimeData(EditorPlayMode mode)
        {
            resourceProviderMode = mode;
        }

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
            if (File.Exists(PlayerCatalogLocation))
            {
                File.Delete(PlayerCatalogLocation);
                var metaFile = PlayerCatalogLocation + ".meta";
                if (File.Exists(metaFile))
                    File.Delete(metaFile);
            }
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
                File.WriteAllText(PlayerCatalogLocation, data);
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
                
                if (File.Exists(PlayerCatalogLocation))
                    File.Delete(PlayerCatalogLocation);
                dirName = Path.GetDirectoryName(PlayerCatalogLocation);
                if (!string.IsNullOrEmpty(dirName) && !Directory.Exists(dirName))
                    Directory.CreateDirectory(dirName);

                File.Copy(LibraryCatalogLocation(mode), PlayerCatalogLocation);
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
