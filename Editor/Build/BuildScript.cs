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

namespace UnityEditor.AddressableAssets
{
    public struct AddressableAssetBuildResult
    {
        public bool Completed { get; set; }
        public double Duration { get; set; }
        public int LocationCount { get; set; }
        public bool Cached { get; set; }
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
    /// TODO - doc
    /// </summary>
    public static class BuildScript
    {
        static int codeVersion = 13;
        [InitializeOnLoadMethod]
        static void Init()
        {
            BuildPlayerWindow.RegisterBuildPlayerHandler(BuildPlayer);
            EditorApplication.playModeStateChanged += OnEditorPlayModeChanged;
        }
        static string kStreamingAssetsPath = "Assets/StreamingAssets";
        static bool streamingAssetsExists;
        static void Cleanup(bool deleteStreamingAssetsFolderIfEmpty)
        {
            if (Directory.Exists(Addressables.BuildPath))
            {
                Directory.Delete(Addressables.BuildPath, true);
            }
            if (deleteStreamingAssetsFolderIfEmpty)
            {
                if (Directory.Exists(kStreamingAssetsPath))
                {
                    var files = Directory.GetFiles(kStreamingAssetsPath);
                    if (files.Length == 0)
                        Directory.Delete(kStreamingAssetsPath);
                }
            }
        }

        static void BuildPlayer(BuildPlayerOptions ops)
        {
            streamingAssetsExists = Directory.Exists(kStreamingAssetsPath);
            if (PrepareRuntimeData(true, (ops.options & BuildOptions.Development) != BuildOptions.None, (ops.options & BuildOptions.ConnectWithProfiler) != BuildOptions.None, false, false, ops.targetGroup, ops.target))
            {
                BuildPipeline.BuildPlayer(ops);
            }
            Cleanup(!streamingAssetsExists);
        }

        private static void OnEditorPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                streamingAssetsExists = Directory.Exists(kStreamingAssetsPath);
                SceneManagerState.Record();
                if (!PrepareRuntimeData(false, true, true, false, true, BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget), EditorUserBuildSettings.activeBuildTarget))
                {
                    EditorApplication.isPlaying = false;
                    SceneManagerState.Restore();
                    Cleanup(!streamingAssetsExists);
                }
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                SceneManagerState.Restore();
                Cleanup(!streamingAssetsExists);
            }
        }

        static bool LoadFromCache(AddressableAssetSettings aaSettings, string settingsHash, ResourceManagerRuntimeData.EditorPlayMode playMode, ref ResourceManagerRuntimeData runtimeData, ref ContentCatalogData contentCatalog)
        {
            if (!ResourceManagerRuntimeData.LoadFromLibrary(ProjectConfigData.editorPlayMode, ref runtimeData, ref contentCatalog))
                return false;

            if (runtimeData.SettingsHash != settingsHash)
            {
                ResourceManagerRuntimeData.DeleteFromLibrary(ProjectConfigData.editorPlayMode);
                if (playMode == ResourceManagerRuntimeData.EditorPlayMode.VirtualMode)
                    VirtualAssetBundleRuntimeData.DeleteFromLibrary();
                return false;
            }
            if (playMode == ResourceManagerRuntimeData.EditorPlayMode.PackedMode)
            {
                runtimeData.Save(contentCatalog, playMode);
                var locator = contentCatalog.CreateLocator();
                var pathsToCheck = new HashSet<string>();
                foreach (var d in locator.Locations)
                {
                    foreach (var l in d.Value)
                    {
                        if (l.ProviderId == typeof(AssetBundleProvider).FullName)
                        {
                            var path = l.InternalId;
                            if (path.StartsWith("file://"))
                                path = path.Substring("file://".Length);
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


        public static System.Action<AddressableAssetBuildResult> buildCompleted;


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
            //buildTasks.Add(new CreateBuiltInShadersBundle("UnityBuiltInShaders"));
            
            // Packing
            buildTasks.Add(new GenerateBundlePacking());
            //buildTasks.Add(new UpdateBundleObjectLayout());
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

        public static bool PrepareRuntimeData(bool isPlayerBuild, bool isDevBuild, bool allowProfilerEvents, bool forceRebuild, bool enteringPlayMode, BuildTargetGroup buildTargetGroup, BuildTarget buildTarget)
        {
            var timer = new System.Diagnostics.Stopwatch();
            timer.Start();

            var aaSettings = AddressableAssetSettings.GetDefault(false, false);
            if (aaSettings == null)
            {
                if (buildCompleted != null)
                    buildCompleted(new AddressableAssetBuildResult() { Completed = false, Duration = timer.Elapsed.TotalSeconds, Error = "AddressableAssetSettings not found." });
                PlayerPrefs.SetInt("AddressablesPlayMode", (int)ResourceManagerRuntimeData.EditorPlayMode.Invalid);
                return true;
            }

            var settingsHash = aaSettings.currentHash.ToString() + codeVersion;
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

            var playMode = isPlayerBuild ? ResourceManagerRuntimeData.EditorPlayMode.PackedMode : ProjectConfigData.editorPlayMode;
            PlayerPrefs.SetInt("AddressablesPlayMode", (int)playMode);
            
            if (!isPlayerBuild && !forceRebuild && LoadFromCache(aaSettings, settingsHash, playMode, ref runtimeData, ref contentCatalog))
            {
                if (enteringPlayMode && playMode != ResourceManagerRuntimeData.EditorPlayMode.PackedMode)
                    AddAddressableScenesToEditorBuildSettingsSceneList(allEntries, runtimeData);
                if (buildCompleted != null)
                    buildCompleted(new AddressableAssetBuildResult() { Completed = true, Duration = timer.Elapsed.TotalSeconds, Cached = true, LocationCount = allEntries.Count });
                return true;
            }
            
            if (playMode == ResourceManagerRuntimeData.EditorPlayMode.PackedMode)
            {
                var catalogCacheDir = Application.persistentDataPath + "/Unity/AddressablesCatalogCache";
                if (Directory.Exists(catalogCacheDir))
                    Directory.Delete(catalogCacheDir, true);
            }

            contentCatalog = new ContentCatalogData();
            runtimeData = new ResourceManagerRuntimeData();
            // List<ResourceLocationData> locations = new List<ResourceLocationData>();
            var locations = new List<ContentCatalogDataEntry> ();
            runtimeData.ProfileEvents = allowProfilerEvents && ProjectConfigData.postProfilerEvents;
            if (playMode == ResourceManagerRuntimeData.EditorPlayMode.FastMode)
            {
                foreach (var a in allEntries)
                {
                    locations.Add(new ContentCatalogDataEntry(a.address, a.guid, a.GetAssetLoadPath(false), typeof(AssetDatabaseProvider), a.labels));
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
                    foreach (var bid in bundleInputDefs)
                        bundleToAssetGroup.Add(bid.assetBundleName, assetGroup);
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
                    buildParams.UseCache = true; // aaSettings.buildSettings.useCache && !forceRebuild;
                    buildParams.BundleCompression = aaSettings.buildSettings.compression;

                    var buildTasks = RuntimeDataBuildTasks(playMode, aaSettings.buildSettings.compileScriptsInVirtualMode, true); 

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
            runtimeData.ContentVersion = aaSettings.profileSettings.GetValueByName(aaSettings.activeProfileId, "ContentVersion");
            if (string.IsNullOrEmpty(runtimeData.ContentVersion))
                runtimeData.ContentVersion = "X";

            runtimeData.SettingsHash = settingsHash;
            contentCatalog.SetData(locations);

            if (playMode == ResourceManagerRuntimeData.EditorPlayMode.PackedMode)
            {
                var catalogLocations = new List<ResourceLocationData>();
                foreach (var assetGroup in aaSettings.groups)
                    assetGroup.Processor.CreateCatalog(assetGroup, contentCatalog, catalogLocations);
                runtimeData.CatalogLocations.AddRange(catalogLocations.OrderBy(s => s.Address));
            }
            else
            {
                runtimeData.CatalogLocations.Add(new ResourceLocationData("Catalog" + playMode, "", ResourceManagerRuntimeData.GetPlayerCatalogLoadLocation(playMode), typeof(JsonAssetProvider)));
            }

            runtimeData.Save(contentCatalog, playMode);
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
                    Debug.LogWarningFormat("Scene {0} is duplicated in EditorBuildSettings.scenes list.", s.path);
            }
            foreach (var entry in entries)
            {
                if (entry.isScene && !sceneSet.Contains(entry.guid))
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
                Debug.LogError("Cannot create preview build due to missing AddressableAssetSettings object.");
                return ReturnCode.MissingRequiredObjects;
            }

            SceneManagerState.Record();

            var runtimeData = new ResourceManagerRuntimeData();
            //var contentCatalog = new ResourceLocationList();
            var locations = new List<ContentCatalogDataEntry>();
            var allBundleInputDefs = new List<AssetBundleBuild>();
            foreach (var assetGroup in aaSettings.groups)
                assetGroup.Processor.ProcessGroup(assetGroup, allBundleInputDefs, locations);

            if (allBundleInputDefs.Count == 0)
            {
                depData = null;
                bundleWriteData = null;
                Debug.LogError("Cannot create preview due being unable to find content to build.");
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
            buildTasks.Add(new GenerateBundlePacking());
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
                                if(AddressablesUtility.IsPathValidForEntry(AssetDatabase.GUIDToAssetPath(k.Key.guid.ToString())))
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
