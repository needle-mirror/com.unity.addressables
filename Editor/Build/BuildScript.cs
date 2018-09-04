using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System.Linq;
using UnityEditor.Build.Utilities;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEditor.Build.Pipeline.Tasks;
using UnityEditor.Build.Pipeline.Utilities;
using UnityEngine.ResourceManagement;
using UnityEngine.AddressableAssets;
using UnityEditor.SceneManagement;
using UnityEditor.IMGUI.Controls;
using System.Runtime.Serialization.Formatters.Binary;
using System;

namespace UnityEditor.AddressableAssets
{
    /// <summary>
    /// Contains information about the status of the build.
    /// </summary>
    public struct AddressableAssetBuildResult
    {
        /// <summary>
        /// Whether the build completed successfully or not.
        /// </summary>
        public bool Completed { get; set; }
        /// <summary>
        /// Duration of build, in seconds.
        /// </summary>
        public double Duration { get; set; }
        /// <summary>
        /// The number of addressable assets contained in the build.
        /// </summary>
        public int LocationCount { get; set; }
        /// <summary>
        /// Whether the data from the build was loaded from cache or not.
        /// </summary>
        public bool Cached { get; set; }
        /// <summary>
        /// Error that caused the build to fail.
        /// </summary>
        public string Error { get; set; }
    }

    internal interface IAddressableAssetsBuildContext : IContextObject
    {
    }

    internal class AddressableAssetsBuildContext : IAddressableAssetsBuildContext
    {
        public AddressableAssetSettings m_settings;
        public ResourceManagerRuntimeData m_runtimeData;
        public List<ContentCatalogDataEntry> m_locations;
        public Dictionary<string, AddressableAssetGroup> m_bundleToAssetGroup;
        public Dictionary<AddressableAssetGroup, List<string>> m_assetGroupToBundles;
        public VirtualAssetBundleRuntimeData m_virtualBundleRuntimeData;
    }
    /// <summary>
    /// [Obsolete] Entry point into building data for the addressables system. NOTE: This API is going to be replaced soon with a more flexible build system.
    /// </summary>
    //[Obsolete("This API is going to be replaced soon with a more flexible build system.")]
    public static class BuildScript
    {
        const int kCodeVersion = 14;
        [InitializeOnLoadMethod]
        static void Init()
        {
            BuildPlayerWindow.RegisterBuildPlayerHandler(BuildPlayer);
            EditorApplication.playModeStateChanged += OnEditorPlayModeChanged;
        }
        static string kStreamingAssetsPath = "Assets/StreamingAssets";
        static bool streamingAssetsExists;

        /// <summary>
        /// Clean up any files created in the streaming assets path or build path such as catalogs and asset bundles.NOTE: This API is going to be replaced soon with a more flexible build system.
        /// </summary>
        /// <param name="deleteStreamingAssetsFolderIfEmpty">If true, the streaming assets folder will be deleted if it is empty after removing the files.</param>
        //[Obsolete("This API is going to be replaced soon with a more flexible build system.")]
        public static void Cleanup(bool deleteStreamingAssetsFolderIfEmpty)
        {
            if (Directory.Exists(Addressables.BuildPath))
            {
                Directory.Delete(Addressables.BuildPath, true);
                if (File.Exists(Addressables.BuildPath + ".meta"))
                    File.Delete(Addressables.BuildPath + ".meta");
            }
            if (deleteStreamingAssetsFolderIfEmpty)
            {
                if (Directory.Exists(kStreamingAssetsPath))
                {
                    var files = Directory.GetFiles(kStreamingAssetsPath);
                    if (files.Length == 0)
                    {
                        Directory.Delete(kStreamingAssetsPath);
                        if (File.Exists(kStreamingAssetsPath + ".meta"))
                            File.Delete(kStreamingAssetsPath + ".meta");

                    }
                }
            }
        }

        static void BuildPlayer(BuildPlayerOptions ops)
        {
            var settings = AddressableAssetSettings.GetDefault(false, false);
            if (settings == null)
            {
                BuildPipeline.BuildPlayer(ops);
            }
            else
            {
                string cacheDataPath;
                if (PrepareRuntimeData(settings, true, (ops.options & BuildOptions.Development) != BuildOptions.None, (ops.options & BuildOptions.ConnectWithProfiler) != BuildOptions.None, false, false, ops.targetGroup, ops.target, null, ResourceManagerRuntimeData.EditorPlayMode.PackedMode, out cacheDataPath))
                {
                    BuildPipeline.BuildPlayer(ops);
                    var newPath = ContentUpdateScript.GetCacheDataPath(false);
                    if (File.Exists(newPath))
                        File.Delete(newPath);
                    File.Copy(cacheDataPath, newPath);
                }
                if (settings.buildSettings.cleanupStreamingAssetsAfterBuilds)
                    Cleanup(!streamingAssetsExists);
            }
        }

        private static void OnEditorPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                if (!BuildDataForPlayMode(ProjectConfigData.editorPlayMode.ToString(), ProjectConfigData.editorPlayMode))
                    EditorApplication.isPlaying = false;
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                SceneManagerState.Restore();
                var settings = AddressableAssetSettings.GetDefault(false, false);
                if (settings != null && settings.buildSettings.cleanupStreamingAssetsAfterBuilds)
                    Cleanup(!streamingAssetsExists);
            }
        }

        /// <summary>
        /// Build runtime data for the specified play mode. NOTE: This API is going to be replaced soon with a more flexible build system.
        /// </summary>
        /// <param name="playerVersion">Player version string.</param>
        /// <param name="mode">Editor play mode.</param>
        /// <returns>True if data is successfully built.</returns>
        //[Obsolete("This API is going to be replaced soon with a more flexible build system.")]
        public static bool BuildDataForPlayMode(string playerVersion, ResourceManagerRuntimeData.EditorPlayMode mode)
        {
            var settings = AddressableAssetSettings.GetDefault(false, false);
            if (settings != null)
            {
                SceneManagerState.Record();
                string unusedCacheDataPath;
                if (!PrepareRuntimeData(settings, false, true, true, false, true, BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget), EditorUserBuildSettings.activeBuildTarget, playerVersion, mode, out unusedCacheDataPath))
                {
                    SceneManagerState.Restore();
                    Cleanup(!streamingAssetsExists);
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Purge all cached data for the Addressables system. NOTE: This API is going to be replaced soon with a more flexible build system.
        /// </summary>
        //[Obsolete("This API is going to be replaced soon with a more flexible build system.")]
        public static void PurgeCache()
        {
            ResourceManagerRuntimeData.DeleteFromLibrary(ResourceManagerRuntimeData.EditorPlayMode.FastMode);
            ResourceManagerRuntimeData.DeleteFromLibrary(ResourceManagerRuntimeData.EditorPlayMode.VirtualMode);
            ResourceManagerRuntimeData.DeleteFromLibrary(ResourceManagerRuntimeData.EditorPlayMode.PackedMode);
            VirtualAssetBundleRuntimeData.DeleteFromLibrary();
            var catalogCacheDir = Application.persistentDataPath + "/com.unity.addressables";
            if (Directory.Exists(catalogCacheDir))
                Directory.Delete(catalogCacheDir, true);
            Cleanup(false);
        }

        static bool LoadFromCache(AddressableAssetSettings aaSettings, string settingsHash, ResourceManagerRuntimeData.EditorPlayMode mode, ref ResourceManagerRuntimeData runtimeData, ref ContentCatalogData contentCatalog)
        {
            if (string.IsNullOrEmpty(settingsHash))
                return false;

            if (!ResourceManagerRuntimeData.LoadFromLibrary(mode, ref runtimeData, ref contentCatalog))
                return false;

            if (runtimeData.SettingsHash != settingsHash)
            {
                ResourceManagerRuntimeData.DeleteFromLibrary(mode);
                if (mode == ResourceManagerRuntimeData.EditorPlayMode.VirtualMode)
                    VirtualAssetBundleRuntimeData.DeleteFromLibrary();
                return false;
            }
            if (mode == ResourceManagerRuntimeData.EditorPlayMode.PackedMode)
            {
                runtimeData.Save(contentCatalog, mode);
                var locator = contentCatalog.CreateLocator();
                var pathsToCheck = new HashSet<string>();
                foreach (var d in locator.Locations)
                {
                    foreach (var l in d.Value)
                    {
                        if (l.ProviderId == typeof(AssetBundleProvider).FullName)
                        {
                            var path = l.InternalId;
                            if (path.StartsWith("http://"))
                                continue;
                            if (path.StartsWith("file://"))
                                path = path.Substring("file://".Length);
                            if (path.StartsWith("{Application.streamingAssetsPath}"))
                                path = path.Replace("{Application.streamingAssetsPath}", Application.streamingAssetsPath);
                            pathsToCheck.Add(path);
                        }
                    }
                }
                foreach (var p in pathsToCheck)
                {
                    if (!File.Exists(p))
                    {
                        return false;
                    }
                }
            }
            return true;
        }


        /// <summary>
        /// Delegate to be invoked upon build completion.NOTE: This API is going to be replaced soon with a more flexible build system.
        /// </summary>
        //[Obsolete("This API is going to be replaced soon with a more flexible build system.")]
        public static Action<AddressableAssetBuildResult> buildCompleted;


        static IList<IBuildTask> RuntimeDataBuildTasks(ResourceManagerRuntimeData.EditorPlayMode playMode, bool compileScripts, bool writeData)
        {
            var buildTasks = new List<IBuildTask>();

            // Setup
            buildTasks.Add(new SwitchToBuildPlatform());
            buildTasks.Add(new RebuildSpriteAtlasCache());

            // Player Scripts
            if (compileScripts || playMode == ResourceManagerRuntimeData.EditorPlayMode.PackedMode)
            {
                buildTasks.Add(new BuildPlayerScripts());
            }

            // Dependency
            if (playMode == ResourceManagerRuntimeData.EditorPlayMode.VirtualMode)
                buildTasks.Add(new PreviewSceneDependencyData());
            else
                buildTasks.Add(new CalculateSceneDependencyData());
            buildTasks.Add(new CalculateAssetDependencyData());
            buildTasks.Add(new StripUnusedSpriteSources());
            buildTasks.Add(new CreateBuiltInShadersBundle("UnityBuiltInShaders"));

            // Packing
            buildTasks.Add(new GenerateBundlePacking());
            buildTasks.Add(new UpdateBundleObjectLayout());
            buildTasks.Add(new GenerateLocationListsTask());
            if (playMode == ResourceManagerRuntimeData.EditorPlayMode.VirtualMode)
            {
                buildTasks.Add(new WriteVirtualBundleDataTask(writeData));
            }
            else
            {
                buildTasks.Add(new GenerateBundleCommands());
                buildTasks.Add(new GenerateSpritePathMaps());
                buildTasks.Add(new GenerateBundleMaps());

                // Writing
                buildTasks.Add(new WriteSerializedFiles());
                buildTasks.Add(new ArchiveAndCompressBundles());
                buildTasks.Add(new PostProcessBundlesTask());
            }
            return buildTasks;
        }

        internal static Hash128 CalculateSettingsHash(AddressableAssetSettings aaSettings)
        {
            MemoryStream stream = new MemoryStream();
            BinaryFormatter formatter = new BinaryFormatter();
            formatter.Serialize(stream, kCodeVersion);
            formatter.Serialize(stream, aaSettings.currentHash);
            ProjectConfigData.SerializeForHash(stream);
            return HashingMethods.Calculate(stream).ToHash128();
        }

        /// <summary>
        /// [obsolete] Prepare data for runtime. NOTE:  This API is going to be replaced soon with a more flexible build system.
        /// </summary>
        /// <param name="aaSettings">The settings object to use.</param>
        /// <param name="isPlayerBuild">If building a player, cached addressable data is not used, play mode is forced to Packed, and content update data is generated.</param>
        /// <param name="isDevBuild">True if the 'Development Build' option is </param>
        /// <param name="allowProfilerEvents">ResourceManager profiler events will be sent.</param>
        /// <param name="forceRebuild">If true, cached data is not used.</param>
        /// <param name="enteringPlayMode">If true and play mode is not Packed, addressable scenes will be added to the BuildPlayer settings scene list temporarily.</param>
        /// <param name="buildTargetGroup">Target group for build.</param>
        /// <param name="buildTarget">Build target.</param>
        /// <param name="playerBuildVersion">Player version.  This is stored in the content update data.</param>
        /// <param name="playMode">Supported modes are Fast for quick iterate, Virtual for moderate iteration and simulated asset bundle behavior, and Packed for running against built asset bundles.  Packed mode is set whenever a player is built as the other modes are not supported in the player.</param>
        /// <param name="cacheDataPath">Path that content update data was stored.  This value is null unless isPlayerBuild is true.</param>
        /// <returns>True if the build succeeds.</returns>
        //[Obsolete("This API is going to be replaced soon with a more flexible build system.")]
        public static bool PrepareRuntimeData(bool isPlayerBuild, bool isDevBuild, bool allowProfilerEvents, bool forceRebuild, bool enteringPlayMode, BuildTargetGroup buildTargetGroup, BuildTarget buildTarget, string playerBuildVersion, ResourceManagerRuntimeData.EditorPlayMode playMode, out string cacheDataPath)
        {
            return PrepareRuntimeData(AddressableAssetSettings.GetDefault(false, false), isPlayerBuild, isDevBuild, allowProfilerEvents, forceRebuild, enteringPlayMode, buildTargetGroup, buildTarget, playerBuildVersion, playMode, out cacheDataPath);
        }

        /// <summary>
        /// [obsolete] Prepare data for runtime. NOTE:  This API is going to be replaced soon with a more flexible build system.
        /// </summary>
        /// <param name="aaSettings">The settings object to use.</param>
        /// <param name="isPlayerBuild">If building a player, cached addressable data is not used, play mode is forced to Packed, and content update data is generated.</param>
        /// <param name="isDevBuild">True if the 'Development Build' option is </param>
        /// <param name="allowProfilerEvents">ResourceManager profiler events will be sent.</param>
        /// <param name="forceRebuild">If true, cached data is not used.</param>
        /// <param name="enteringPlayMode">If true and play mode is not Packed, addressable scenes will be added to the BuildPlayer settings scene list temporarily.</param>
        /// <param name="buildTargetGroup">Target group for build.</param>
        /// <param name="buildTarget">Build target.</param>
        /// <param name="playerBuildVersion">Player version.  This is stored in the content update data.</param>
        /// <param name="playMode">Supported modes are Fast for quick iterate, Virtual for moderate iteration and simulated asset bundle behavior, and Packed for running against built asset bundles.  Packed mode is set whenever a player is built as the other modes are not supported in the player.</param>
        /// <param name="cacheDataPath">Path that content update data was stored.  This value is null unless isPlayerBuild is true.</param>
        /// <returns>True if the build succeeds.</returns>
        //[Obsolete("This API is going to be replaced soon with a more flexible build system.")]
        public static bool PrepareRuntimeData(AddressableAssetSettings aaSettings, bool isPlayerBuild, bool isDevBuild, bool allowProfilerEvents, bool forceRebuild, bool enteringPlayMode, BuildTargetGroup buildTargetGroup, BuildTarget buildTarget, string playerBuildVersion, ResourceManagerRuntimeData.EditorPlayMode playMode, out string cacheDataPath)
        {
            var timer = new System.Diagnostics.Stopwatch();
            timer.Start();

            cacheDataPath = null;

            if (aaSettings == null)
            {
                PlayerPrefs.SetInt("AddressablesPlayMode", (int)ResourceManagerRuntimeData.EditorPlayMode.Invalid);
                return true;
            }

            streamingAssetsExists = Directory.Exists(kStreamingAssetsPath);

            if (string.IsNullOrEmpty(playerBuildVersion))
                playerBuildVersion = aaSettings.PlayerBuildVersion;

            string settingsHash = CalculateSettingsHash(aaSettings).ToString();

            var allEntries = new List<AddressableAssetEntry>();
            aaSettings.GetAllAssets(allEntries);
            if (allEntries.Count == 0)
            {
                if (buildCompleted != null)
                    buildCompleted(new AddressableAssetBuildResult() { Completed = false, Duration = timer.Elapsed.TotalSeconds, Error = "AddressableAssetSettings has 0 entries." });
                PlayerPrefs.SetInt("AddressablesPlayMode", (int)ResourceManagerRuntimeData.EditorPlayMode.Invalid);
                return true;
            }
            ResourceManagerRuntimeData runtimeData = null;
            ContentCatalogData contentCatalog = null;

            if (isPlayerBuild)
                playMode = ResourceManagerRuntimeData.EditorPlayMode.PackedMode;

            var tryCache = (playMode != ResourceManagerRuntimeData.EditorPlayMode.PackedMode || !aaSettings.AssetsModifiedSinceLastPackedBuild);

            PlayerPrefs.SetInt("AddressablesPlayMode", (int)playMode);
            if (tryCache && !isPlayerBuild && !forceRebuild && LoadFromCache(aaSettings, settingsHash, playMode, ref runtimeData, ref contentCatalog))
            {
                if (enteringPlayMode && playMode != ResourceManagerRuntimeData.EditorPlayMode.PackedMode)
                    AddAddressableScenesToEditorBuildSettingsSceneList(allEntries, runtimeData);
                if (buildCompleted != null)
                    buildCompleted(new AddressableAssetBuildResult() { Completed = true, Duration = timer.Elapsed.TotalSeconds, Cached = true, LocationCount = allEntries.Count });
                return true;
            }

            if (playMode == ResourceManagerRuntimeData.EditorPlayMode.PackedMode)
            {
                var catalogCacheDir = Application.persistentDataPath + "/com.unity.addressables";
                if (Directory.Exists(catalogCacheDir))
                    Directory.Delete(catalogCacheDir, true);
            }

            contentCatalog = new ContentCatalogData();
            runtimeData = new ResourceManagerRuntimeData();
            var locations = new List<ContentCatalogDataEntry>();
            runtimeData.ProfileEvents = allowProfilerEvents && ProjectConfigData.postProfilerEvents;
            ExtractDataTask extractData = new ExtractDataTask();
            if (playMode == ResourceManagerRuntimeData.EditorPlayMode.FastMode)
            {
                foreach (var a in allEntries)
                {
                    locations.Add(new ContentCatalogDataEntry(a.address, a.guid, a.AssetPath, typeof(AssetDatabaseProvider), a.labels));
                }
            }
            else
            {
                var allBundleInputDefs = new List<AssetBundleBuild>();
                var bundleToAssetGroup = new Dictionary<string, AddressableAssetGroup>();
                foreach (var assetGroup in aaSettings.groups)
                {
                    var bundleInputDefs = new List<AssetBundleBuild>();
                    assetGroup.Processor.ProcessGroup(assetGroup, bundleInputDefs, locations);
                    for (int i = 0; i < bundleInputDefs.Count; i++)
                    {
                        if (bundleToAssetGroup.ContainsKey(bundleInputDefs[i].assetBundleName))
                        {
                            var bid = bundleInputDefs[i];
                            int count = 1;
                            var newName = bid.assetBundleName;
                            while (bundleToAssetGroup.ContainsKey(newName) && count < 1000)
                                newName = bid.assetBundleName.Replace(".bundle", string.Format("{0}.bundle", count++));
                            bundleInputDefs[i] = new AssetBundleBuild() { assetBundleName = newName, addressableNames = bid.addressableNames, assetBundleVariant = bid.assetBundleVariant, assetNames = bid.assetNames };
                        }

                        bundleToAssetGroup.Add(bundleInputDefs[i].assetBundleName, assetGroup);
                    }
                    allBundleInputDefs.AddRange(bundleInputDefs);
                }

                if (allBundleInputDefs.Count > 0)
                {
                    if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    {
                        if (buildCompleted != null)
                            buildCompleted(new AddressableAssetBuildResult() { Completed = false, Duration = timer.Elapsed.TotalSeconds, Error = "Unsaved scenes" });
                        return false;
                    }

                    var buildParams = new BundleBuildParameters(buildTarget, buildTargetGroup, aaSettings.buildSettings.bundleBuildPath);
                    buildParams.UseCache = !forceRebuild;
                    buildParams.BundleCompression = aaSettings.buildSettings.compression;

                    var buildTasks = RuntimeDataBuildTasks(playMode, aaSettings.buildSettings.compileScriptsInVirtualMode, true);

                    buildTasks.Add(extractData);

                    var aaContext = new AddressableAssetsBuildContext
                    {
                        m_settings = aaSettings,
                        m_runtimeData = runtimeData,
                        m_bundleToAssetGroup = bundleToAssetGroup,
                        m_locations = locations
                    };

                    IBundleBuildResults results;
                    var exitCode = ContentPipeline.BuildAssetBundles(buildParams, new BundleBuildContent(allBundleInputDefs), out results, buildTasks, aaContext);

                    if (exitCode < ReturnCode.Success)
                    {
                        if (buildCompleted != null)
                        {
                            buildCompleted(new AddressableAssetBuildResult
                            {
                                Completed = false,
                                Duration = timer.Elapsed.TotalSeconds,
                                Error = exitCode.ToString()
                            });
                        }
                        return false;
                    }
                }
            }

            if (enteringPlayMode && playMode != ResourceManagerRuntimeData.EditorPlayMode.PackedMode)
                AddAddressableScenesToEditorBuildSettingsSceneList(allEntries, runtimeData);

            runtimeData.SettingsHash = settingsHash;
            contentCatalog.SetData(locations);

            if (playMode == ResourceManagerRuntimeData.EditorPlayMode.PackedMode)
            {
                var catalogLocations = new List<ResourceLocationData>();
                foreach (var assetGroup in aaSettings.groups)
                    assetGroup.Processor.CreateCatalog(assetGroup, contentCatalog, catalogLocations, playerBuildVersion);
                runtimeData.CatalogLocations.AddRange(catalogLocations.OrderBy(s => s.Keys[0]));
                aaSettings.AssetsModifiedSinceLastPackedBuild = false;
            }
            else
            {
                runtimeData.CatalogLocations.Add(new ResourceLocationData(new string[] { "catalogs" }, ResourceManagerRuntimeData.GetPlayerCatalogLoadLocation(playMode), typeof(JsonAssetProvider)));
            }

            runtimeData.Save(contentCatalog, playMode);

            if (isPlayerBuild)
                cacheDataPath = ContentUpdateScript.SaveCacheData(allEntries, extractData.BuildCache, playerBuildVersion);

            Resources.UnloadUnusedAssets();
            if (buildCompleted != null)
                buildCompleted(new AddressableAssetBuildResult() { Completed = true, Duration = timer.Elapsed.TotalSeconds, LocationCount = locations.Count });
            return true;
        }

        private static void AddAddressableScenesToEditorBuildSettingsSceneList(List<AddressableAssetEntry> entries, ResourceManagerRuntimeData runtimeData)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            var sceneSet = new HashSet<string>();
            foreach (var s in scenes)
            {
                if (!sceneSet.Add(s.guid.ToString()))
                    Addressables.LogWarningFormat("Scene {0} is duplicated in EditorBuildSettings.scenes list.", s.path);
            }
            foreach (var entry in entries)
            {
                if (entry.IsScene && !sceneSet.Contains(entry.guid))
                {
                    sceneSet.Add(entry.guid);
                    scenes.Add(new EditorBuildSettingsScene(new GUID(entry.guid), true));
                }
            }
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        internal static ReturnCode PreviewDependencyInfo(out BuildDependencyData depData, out BundleWriteData bundleWriteData)
        {
            var aaSettings = AddressableAssetSettings.GetDefault(false, false);
            if (aaSettings == null)
            {
                depData = null;
                bundleWriteData = null;
                Addressables.LogError("Cannot create preview build due to missing AddressableAssetSettings object.");
                return ReturnCode.MissingRequiredObjects;
            }

            SceneManagerState.Record();

            var runtimeData = new ResourceManagerRuntimeData();
            var locations = new List<ContentCatalogDataEntry>();
            var allBundleInputDefs = new List<AssetBundleBuild>();
            foreach (var assetGroup in aaSettings.groups)
                assetGroup.Processor.ProcessGroup(assetGroup, allBundleInputDefs, locations);

            if (allBundleInputDefs.Count == 0)
            {
                depData = null;
                bundleWriteData = null;
                Addressables.LogError("Cannot create preview due being unable to find content to build.");
                return ReturnCode.MissingRequiredObjects;
            }

            var buildParams = new BundleBuildParameters(EditorUserBuildSettings.activeBuildTarget, BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget), aaSettings.buildSettings.bundleBuildPath);
            buildParams.UseCache = true;
            buildParams.BundleCompression = aaSettings.buildSettings.compression;

            var aaContext = new AddressableAssetsBuildContext
            {
                m_settings = aaSettings,
                m_runtimeData = runtimeData,
                m_locations = locations
            };

            ExtractDataTask extractData = new ExtractDataTask();
            var buildTasks = new List<IBuildTask>();
            buildTasks.Add(new SwitchToBuildPlatform());
            buildTasks.Add(new RebuildSpriteAtlasCache());
            buildTasks.Add(new BuildPlayerScripts());
            buildTasks.Add(new PreviewSceneDependencyData());
            buildTasks.Add(new CalculateAssetDependencyData());
            buildTasks.Add(new StripUnusedSpriteSources());
            buildTasks.Add(new CreateBuiltInShadersBundle("UnityBuiltInShaders"));
            buildTasks.Add(new GenerateBundlePacking());
            buildTasks.Add(new UpdateBundleObjectLayout());
            buildTasks.Add(extractData);

            IBundleBuildResults results;
            var exitCode = ContentPipeline.BuildAssetBundles(buildParams, new BundleBuildContent(allBundleInputDefs), out results, buildTasks, aaContext);

            if (exitCode < ReturnCode.Success)
            {
                depData = null;
                bundleWriteData = null;
                return exitCode;
            }

            depData = (BuildDependencyData)extractData.DependencyData;
            bundleWriteData = (BundleWriteData)extractData.WriteData;
            return ReturnCode.Success;
        }



        static internal HashSet<GUID> ExtractCommonAssets(AddressableAssetSettings aaSettings, List<AddressableAssetGroup> groups)
        {
            var buildTarget = EditorUserBuildSettings.activeBuildTarget;
            var buildTargetGroup = BuildPipeline.GetBuildTargetGroup(buildTarget);

            var allBundleInputDefs = new List<AssetBundleBuild>();
            var bundleToAssetGroup = new Dictionary<string, AddressableAssetGroup>();
            var runtimeData = new ResourceManagerRuntimeData();
            var locations = new List<ContentCatalogDataEntry>();
            foreach (var assetGroup in groups)
            {
                var bundleInputDefs = new List<AssetBundleBuild>();
                assetGroup.Processor.ProcessGroup(assetGroup, bundleInputDefs, locations);
                foreach (var bid in bundleInputDefs)
                    bundleToAssetGroup.Add(bid.assetBundleName, assetGroup);
                allBundleInputDefs.AddRange(bundleInputDefs);
            }

            var duplicatedAssets = new HashSet<GUID>();
            if (allBundleInputDefs.Count > 0)
            {
                var buildParams = new BundleBuildParameters(buildTarget, buildTargetGroup, aaSettings.buildSettings.bundleBuildPath);
                buildParams.UseCache = true; // aaSettings.buildSettings.useCache && !forceRebuild;
                buildParams.BundleCompression = aaSettings.buildSettings.compression;

                var buildTasks = RuntimeDataBuildTasks(ResourceManagerRuntimeData.EditorPlayMode.VirtualMode, false, false);

                var aaContext = new AddressableAssetsBuildContext
                {
                    m_settings = aaSettings,
                    m_runtimeData = runtimeData,
                    m_bundleToAssetGroup = bundleToAssetGroup,
                    m_locations = locations
                };

                IBundleBuildResults results;
                ExtractDataTask extractData = new ExtractDataTask();
                buildTasks.Add(extractData);

                var retCode = ContentPipeline.BuildAssetBundles(buildParams, new BundleBuildContent(allBundleInputDefs), out results, buildTasks, aaContext);
                if (retCode >= ReturnCode.Success)
                {
                    HashSet<GUID> explicitAssets = new HashSet<GUID>();
                    var assetToBundle = new Dictionary<GUID, string>();
                    foreach (var b in aaContext.m_virtualBundleRuntimeData.AssetBundles)
                    {
                        foreach (var a in b.Assets)
                        {
                            var guid = new GUID(AssetDatabase.AssetPathToGUID(a.Name));
                            if (guid.Empty())
                                continue;
                            explicitAssets.Add(guid);
                            assetToBundle.Add(guid, b.Name);
                        }
                    }
                    var depData = extractData.DependencyData;
                    var objectsToBundles = new Dictionary<Build.Content.ObjectIdentifier, HashSet<string>>();
                    foreach (var guid in explicitAssets)
                    {
                        Build.Content.AssetLoadInfo assetLoadInfo;
                        if (depData.AssetInfo.TryGetValue(guid, out assetLoadInfo))
                        {
                            foreach (var o in assetLoadInfo.includedObjects)
                            {
                                HashSet<string> bundleList;
                                if (!objectsToBundles.TryGetValue(o, out bundleList))
                                    objectsToBundles.Add(o, bundleList = new HashSet<string>());
                                string bundle;
                                if (assetToBundle.TryGetValue(assetLoadInfo.asset, out bundle))
                                    bundleList.Add(bundle);
                            }
                            foreach (var o in assetLoadInfo.referencedObjects)
                            {
                                if (!string.IsNullOrEmpty(o.filePath))
                                    continue;
                                HashSet<string> bundleList;
                                if (!objectsToBundles.TryGetValue(o, out bundleList))
                                    objectsToBundles.Add(o, bundleList = new HashSet<string>());
                                string bundle;
                                if (assetToBundle.TryGetValue(assetLoadInfo.asset, out bundle))
                                    bundleList.Add(bundle);
                            }
                        }
                    }

                    foreach (var k in objectsToBundles)
                    {
                        if (k.Value.Count > 1)
                        {
                            if (!explicitAssets.Contains(k.Key.guid))
                            {
                                if (AddressablesUtility.IsPathValidForEntry(AssetDatabase.GUIDToAssetPath(k.Key.guid.ToString())))
                                    duplicatedAssets.Add(k.Key.guid);
                            }
                        }
                    }
                }
            }
            return duplicatedAssets;
        }
    }
}
