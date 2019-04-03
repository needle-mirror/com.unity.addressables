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

    public interface IAddressableAssetsBuildContext : IContextObject
    {
    }

    public class AddressableAssetsBuildContext : IAddressableAssetsBuildContext
    {
        public AddressableAssetSettings m_settings;
        public ResourceManagerRuntimeData m_runtimeData;
        public Dictionary<object, ContentCatalogData.DataEntry> m_locations;
        public Dictionary<string, AddressableAssetGroup> m_bundleToAssetGroup;
        public Dictionary<AddressableAssetGroup, List<string>> m_assetGroupToBundles;
        public VirtualAssetBundleRuntimeData m_virtualBundleRuntimeData;
    }
    /// <summary>
    /// TODO - doc
    /// </summary>
    public class BuildScript
    {
        static int codeVersion = 13;
        [InitializeOnLoadMethod]
        static void Init()
        {
            BuildPlayerWindow.RegisterBuildPlayerHandler(BuildPlayer);
            EditorApplication.playModeStateChanged += OnEditorPlayModeChanged;
        }

        static void BuildPlayer(BuildPlayerOptions ops)
        {
            if (PrepareRuntimeData(true, (ops.options & BuildOptions.Development) != BuildOptions.None, (ops.options & BuildOptions.ConnectWithProfiler) != BuildOptions.None, false, false, ops.targetGroup, ops.target))
                BuildPipeline.BuildPlayer(ops);
        }

        private static void OnEditorPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                SceneManagerState.Record();
                if (!PrepareRuntimeData(false, true, true, false, true, BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget), EditorUserBuildSettings.activeBuildTarget))
                {
                    EditorApplication.isPlaying = false;
                    SceneManagerState.Restore();
                }
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                SceneManagerState.Restore();
            }
        }

        static bool LoadFromCache(AddressableAssetSettings aaSettings, string settingsHash, ResourceManagerRuntimeData.EditorPlayMode playMode, ref ResourceManagerRuntimeData runtimeData, ref ContentCatalogData contentCatalog)
        {
            if (!ResourceManagerRuntimeData.LoadFromLibrary(ProjectConfigData.editorPlayMode, ref runtimeData, ref contentCatalog))
                return false;

            if (runtimeData.settingsHash != settingsHash)
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
                foreach (var d in locator.m_locations)
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

        public struct BuildResult
        {
            public bool completed;
            public double duration;
            public int locationCount;
            public bool cached;
            public string error;
        }

        public static System.Action<BuildResult> buildCompleted;


        static IList<IBuildTask> RuntimeDataBuildTasks(ResourceManagerRuntimeData.EditorPlayMode playMode, bool compileScripts, bool writeData)
        {
            var buildTasks = new List<IBuildTask>();
            
            // Setup
            buildTasks.Add(new ProjectInCleanState());
            buildTasks.Add(new ValidateBundleAssignments());
            buildTasks.Add(new SwitchToBuildPlatform());
            buildTasks.Add(new RebuildSpriteAtlasCache());

            // Player Scripts
            if (compileScripts || playMode == ResourceManagerRuntimeData.EditorPlayMode.PackedMode)
            {
                buildTasks.Add(new BuildPlayerScripts());
                buildTasks.Add(new SetBundleSettingsTypeDB());
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
                    buildCompleted(new BuildResult() { completed = false, duration = timer.Elapsed.TotalSeconds, error = "AddressableAssetSettings not found." });
                PlayerPrefs.SetInt("AddressablesPlayMode", (int)ResourceManagerRuntimeData.EditorPlayMode.Invalid);
                return true;
            }

            var settingsHash = aaSettings.currentHash.ToString() + codeVersion;
            var allEntries = aaSettings.GetAllAssets();
            if (allEntries.Count == 0)
            {
                if (buildCompleted != null)
                    buildCompleted(new BuildResult() { completed = false, duration = timer.Elapsed.TotalSeconds, error = "AddressableAssetSettings has 0 entries." });
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
                    buildCompleted(new BuildResult() { completed = true, duration = timer.Elapsed.TotalSeconds, cached = true, locationCount = allEntries.Count });
                return true;
            }
            
            bool validated = true;
            foreach (var assetGroup in aaSettings.groups)
            {
                if (!assetGroup.processor.Validate(aaSettings, assetGroup))
                    validated = false;
            }

            if (!validated)
            {
                if (buildCompleted != null)
                    buildCompleted(new BuildResult() { completed = false, duration = timer.Elapsed.TotalSeconds, error = "Validation failed." });
                PlayerPrefs.SetInt("AddressablesPlayMode", (int)ResourceManagerRuntimeData.EditorPlayMode.Invalid);
                return false;
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
            var locations = new Dictionary<object, ContentCatalogData.DataEntry>();
            runtimeData.profileEvents = allowProfilerEvents && ProjectConfigData.postProfilerEvents;
            if (playMode == ResourceManagerRuntimeData.EditorPlayMode.FastMode)
            {
                foreach (var a in allEntries)
                {
                    locations.Add(a.address, new ContentCatalogData.DataEntry(a.address, a.guid, a.GetAssetLoadPath(false), typeof(AssetDatabaseProvider), a.labels));
                }
            }
            else
            {
                var allBundleInputDefs = new List<AssetBundleBuild>();
                var bundleToAssetGroup = new Dictionary<string, AddressableAssetGroup>();
                foreach (var assetGroup in aaSettings.groups)
                {
                    var bundleInputDefs = new List<AssetBundleBuild>();
                    assetGroup.processor.ProcessGroup(aaSettings, assetGroup, bundleInputDefs, locations);
                    foreach (var bid in bundleInputDefs)
                        bundleToAssetGroup.Add(bid.assetBundleName, assetGroup);
                    allBundleInputDefs.AddRange(bundleInputDefs);
                }

                if (allBundleInputDefs.Count > 0)
                {
                    if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    {
                        if (buildCompleted != null)
                            buildCompleted(new BuildResult() { completed = false, duration = timer.Elapsed.TotalSeconds, error = "Unsaved scenes" });
                        return false;
                    }

                    var buildParams = new BuildParameters(buildTarget, buildTargetGroup, aaSettings.buildSettings.bundleBuildPath);
                    buildParams.UseCache = true; // aaSettings.buildSettings.useCache && !forceRebuild;

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
                            buildCompleted(new BuildResult
                            {
                                completed = false,
                                duration = timer.Elapsed.TotalSeconds,
                                error = exitCode.ToString()
                            });
                        }
                        return false;
                    }
                }
            }

            if (enteringPlayMode && playMode != ResourceManagerRuntimeData.EditorPlayMode.PackedMode)
                AddAddressableScenesToEditorBuildSettingsSceneList(allEntries, runtimeData);
            runtimeData.contentVersion = aaSettings.profileSettings.GetValueByName(aaSettings.activeProfileId, "ContentVersion");
            if (string.IsNullOrEmpty(runtimeData.contentVersion))
                runtimeData.contentVersion = "X";

            runtimeData.settingsHash = settingsHash;
            contentCatalog.SetData(locations.Values.ToList());

            if (playMode == ResourceManagerRuntimeData.EditorPlayMode.PackedMode)
            {
                var catalogLocations = new List<ResourceLocationData>();
                foreach (var assetGroup in aaSettings.groups)
                    assetGroup.processor.CreateCatalog(aaSettings, assetGroup, contentCatalog, catalogLocations);
                runtimeData.catalogLocations.AddRange(catalogLocations.OrderBy(s => s.m_address));
            }
            else
            {
                runtimeData.catalogLocations.Add(new ResourceLocationData("Catalog" + playMode, "", ResourceManagerRuntimeData.GetPlayerCatalogLoadLocation(playMode), typeof(JsonAssetProvider)));
            }

            runtimeData.Save(contentCatalog, ProjectConfigData.editorPlayMode);
            Resources.UnloadUnusedAssets();
            if (buildCompleted != null)
                buildCompleted(new BuildResult() { completed = true, duration = timer.Elapsed.TotalSeconds, locationCount = locations.Count });
            return true;
        }

        private static void AddAddressableScenesToEditorBuildSettingsSceneList(List<AddressableAssetEntry> entries, ResourceManagerRuntimeData runtimeData)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var entry in entries)
                if(entry.isScene)
                    scenes.Add(new EditorBuildSettingsScene(new GUID(entry.guid), true));
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
            var locations = new Dictionary<object, ContentCatalogData.DataEntry>();
            var allBundleInputDefs = new List<AssetBundleBuild>();
            foreach (var assetGroup in aaSettings.groups)
                assetGroup.processor.ProcessGroup(aaSettings, assetGroup, allBundleInputDefs, locations);

            if (allBundleInputDefs.Count == 0)
            {
                depData = null;
                bundleWriteData = null;
                Debug.LogError("Cannot create preview due being unable to find content to build.");
                return ReturnCode.MissingRequiredObjects;
            }

            var buildParams = new BuildParameters(EditorUserBuildSettings.activeBuildTarget, BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget), aaSettings.buildSettings.bundleBuildPath);
            buildParams.UseCache = true;

            var aaContext = new AddressableAssetsBuildContext
            {
                m_settings = aaSettings,
                m_runtimeData = runtimeData,
                m_locations = locations
            };

            IBuildContext buildContext = null;
            var buildTasks = new List<IBuildTask>();
            buildTasks.Add(new ProjectInCleanState());
            buildTasks.Add(new ValidateBundleAssignments());
            buildTasks.Add(new SwitchToBuildPlatform());
            buildTasks.Add(new RebuildSpriteAtlasCache());
            buildTasks.Add(new BuildPlayerScripts());
            buildTasks.Add(new SetBundleSettingsTypeDB());
            buildTasks.Add(new PreviewSceneDependencyData());
            buildTasks.Add(new CalculateAssetDependencyData());
            buildTasks.Add(new StripUnusedSpriteSources());
            buildTasks.Add(new GenerateBundlePacking());
            buildTasks.Add(new InlineTaskRunner(context =>
            {
                buildContext = context;
                return ReturnCode.Success;
            }));

            IBundleBuildResults results;
            var exitCode = ContentPipeline.BuildAssetBundles(buildParams, new BundleBuildContent(allBundleInputDefs), out results, buildTasks, aaContext);

            if (exitCode < ReturnCode.Success)
            {
                depData = null;
                bundleWriteData = null;
                return exitCode;
            }
            
            depData = (BuildDependencyData)buildContext.GetContextObject<IDependencyData>();
            bundleWriteData = (BundleWriteData)buildContext.GetContextObject<IWriteData>();
            return ReturnCode.Success;
        }



        static internal HashSet<GUID> ExtractCommonAssets(AddressableAssetSettings aaSettings, List<AddressableAssetGroup> groups)
        {
            var buildTarget = EditorUserBuildSettings.activeBuildTarget;
            var buildTargetGroup = BuildPipeline.GetBuildTargetGroup(buildTarget);

            var allBundleInputDefs = new List<AssetBundleBuild>();
            var bundleToAssetGroup = new Dictionary<string, AddressableAssetGroup>();
            var runtimeData = new ResourceManagerRuntimeData();
            var locations = new Dictionary<object, ContentCatalogData.DataEntry>();
            foreach (var assetGroup in groups)
            {
                var bundleInputDefs = new List<AssetBundleBuild>();
                assetGroup.processor.ProcessGroup(aaSettings, assetGroup, bundleInputDefs, locations);
                foreach (var bid in bundleInputDefs)
                    bundleToAssetGroup.Add(bid.assetBundleName, assetGroup);
                allBundleInputDefs.AddRange(bundleInputDefs);
            }

            var duplicatedAssets = new HashSet<GUID>();
            if (allBundleInputDefs.Count > 0)
            {
                var buildParams = new BuildParameters(buildTarget, buildTargetGroup, aaSettings.buildSettings.bundleBuildPath);
                buildParams.UseCache = true; // aaSettings.buildSettings.useCache && !forceRebuild;

                var buildTasks = RuntimeDataBuildTasks(ResourceManagerRuntimeData.EditorPlayMode.VirtualMode, false, false);

                var aaContext = new AddressableAssetsBuildContext
                {
                    m_settings = aaSettings,
                    m_runtimeData = runtimeData,
                    m_bundleToAssetGroup = bundleToAssetGroup,
                    m_locations = locations
                };

                IBundleBuildResults results;
                IBuildContext buildContext = null;
                buildTasks.Add(new InlineTaskRunner(context =>
                {
                    buildContext = context;
                    return ReturnCode.Success;
                }));

                var retCode = ContentPipeline.BuildAssetBundles(buildParams, new BundleBuildContent(allBundleInputDefs), out results, buildTasks, aaContext);
                if (retCode >= ReturnCode.Success)
                {
                    HashSet<GUID> explicitAssets = new HashSet<GUID>();
                    var assetToBundle = new Dictionary<GUID, string>();
                    foreach (var b in aaContext.m_virtualBundleRuntimeData.AssetBundles)
                    {
                        foreach (var a in b.Assets)
                        {
                            var guid = new GUID(AssetDatabase.AssetPathToGUID(a.m_name));
                            if (guid.Empty())
                                continue;
                            explicitAssets.Add(guid);
                            assetToBundle.Add(guid, b.Name);
                        }
                    }
                    var depData = buildContext.GetContextObject<IDependencyData>();
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
