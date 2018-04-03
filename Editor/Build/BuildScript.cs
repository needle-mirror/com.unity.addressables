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
        public ResourceLocationList m_contentCatalog;
        public Dictionary<string, AddressableAssetSettings.AssetGroup> m_bundleToAssetGroup;
        public Dictionary<AddressableAssetSettings.AssetGroup, List<string>> m_assetGroupToBundles;
    }
    /// <summary>
    /// TODO - doc
    /// </summary>
    public class BuildScript
    {
        static int codeVersion = 3;
        [InitializeOnLoadMethod]
        static void Init()
        {
            BuildPlayerWindow.RegisterBuildPlayerHandler(BuildPlayer);
            EditorApplication.playModeStateChanged += OnEditorPlayModeChanged;
        }

        static void BuildPlayer(BuildPlayerOptions ops)
        {
            if(PrepareRuntimeData(true, (ops.options & BuildOptions.Development) != BuildOptions.None, (ops.options & BuildOptions.ConnectWithProfiler) != BuildOptions.None, false, false, ops.targetGroup, ops.target))
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
                    ResourceManagerRuntimeData.Cleanup();
                    VirtualAssetBundleRuntimeData.Cleanup();
                    SceneManagerState.Restore();
                }
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                ResourceManagerRuntimeData.Cleanup();
                VirtualAssetBundleRuntimeData.Cleanup();
                SceneManagerState.Restore();
            }
        }

        static bool LoadFromCache(AddressableAssetSettings aaSettings, string settingsHash, ref ResourceManagerRuntimeData runtimeData, ref ResourceLocationList contentCatalog)
        {
            if (!ResourceManagerRuntimeData.LoadFromLibrary(aaSettings.buildSettings.editorPlayMode.ToString(), ref runtimeData, ref contentCatalog))
                return false;

            if (runtimeData.settingsHash != settingsHash)
            {
                ResourceManagerRuntimeData.DeleteFromLibrary(aaSettings.buildSettings.editorPlayMode.ToString());
                if (runtimeData.resourceProviderMode == ResourceManagerRuntimeData.EditorPlayMode.VirtualMode)
                    VirtualAssetBundleRuntimeData.DeleteFromLibrary();
                return false;
            }

            if (runtimeData.resourceProviderMode == ResourceManagerRuntimeData.EditorPlayMode.VirtualMode)
            {
                if (!VirtualAssetBundleRuntimeData.CopyFromLibraryToPlayer())
                    WriteVirtualBundleDataTask.Run(aaSettings, runtimeData, contentCatalog, null);
            }

            var catalogLocations = new List<ResourceLocationData>();
            foreach (var assetGroup in aaSettings.groups)
                assetGroup.processor.CreateCatalog(aaSettings, assetGroup, contentCatalog, catalogLocations);
            runtimeData.catalogLocations.locations.AddRange(catalogLocations.OrderBy(s => s.m_address));

            return runtimeData.CopyFromLibraryToPlayer(aaSettings.buildSettings.editorPlayMode.ToString());
        }

        public struct BuildResult
        {
            public bool completed;
            public double duration;
            public int locationCount;
            public string error;
        }

        public static System.Action<BuildResult> buildCompleted;

        public static bool PrepareRuntimeData(bool isPlayerBuild, bool isDevBuild, bool allowProfilerEvents, bool forceRebuild, bool enteringPlayMode, BuildTargetGroup buildTargetGroup, BuildTarget buildTarget)
        {
            var timer = new System.Diagnostics.Stopwatch();
            timer.Start();

            var aaSettings = AddressableAssetSettings.GetDefault(false, false);
            if (aaSettings == null)
            {
                if (buildCompleted != null)
                    buildCompleted(new BuildResult() { completed = false, duration = timer.Elapsed.TotalSeconds, error = "AddressableAssetSettings not found." });
                return true;
            }

            var settingsHash = aaSettings.currentHash.ToString() + codeVersion;
            ResourceManagerRuntimeData runtimeData = null;
            ResourceLocationList contentCatalog = null;

            if (!forceRebuild && LoadFromCache(aaSettings, settingsHash, ref runtimeData, ref contentCatalog))
            {
                if (enteringPlayMode && runtimeData.resourceProviderMode != ResourceManagerRuntimeData.EditorPlayMode.PackedMode)
                    AddAddressableScenesToEditorBuildSettingsSceneList(aaSettings, runtimeData);
                if (buildCompleted != null)
                    buildCompleted(new BuildResult() { completed = true, duration = timer.Elapsed.TotalSeconds });
                return true;
            }

            bool validated = true;
            foreach (var assetGroup in aaSettings.groups)
            {
                if (!assetGroup.processor.Validate(aaSettings, assetGroup))
                    validated = false;
            }

            if (!validated)
                return false;


            runtimeData = new ResourceManagerRuntimeData(isPlayerBuild ? ResourceManagerRuntimeData.EditorPlayMode.PackedMode : aaSettings.buildSettings.editorPlayMode);
            contentCatalog = new ResourceLocationList();
            contentCatalog.labels = aaSettings.labelTable.labelNames;
            runtimeData.profileEvents = allowProfilerEvents && aaSettings.buildSettings.postProfilerEvents;
            if (runtimeData.resourceProviderMode == ResourceManagerRuntimeData.EditorPlayMode.FastMode)
            {
                foreach (var a in aaSettings.GetAllAssets(true, true))
                {
                    var t = AssetDatabase.GetMainAssetTypeAtPath(a.assetPath);
                    if (t == null)
                        continue;

                    contentCatalog.locations.Add(new ResourceLocationData(a.address, a.guid, a.GetAssetLoadPath(false), typeof(AssetDatabaseProvider).FullName, true, ResourceLocationData.LocationType.String, aaSettings.labelTable.GetMask(a.labels), t.FullName, null));
                }
            }
            else
            {
                var allBundleInputDefs = new List<AssetBundleBuild>();
                var bundleToAssetGroup = new Dictionary<string, AddressableAssetSettings.AssetGroup>();
                foreach (var assetGroup in aaSettings.groups)
                {
                    var bundleInputDefs = new List<AssetBundleBuild>();
                    assetGroup.processor.ProcessGroup(aaSettings, assetGroup, bundleInputDefs, contentCatalog.locations);
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

                    var bundleWriteData = new BundleWriteData();
                    var bundleBuildResults = new BundleBuildResults();
                    var dependencyData = new BuildDependencyData();
                   // using (var progressTracker = new TimeThrottledProgressTracker(100))
                    {
                        var buildParams = new BuildParameters(buildTarget, buildTargetGroup, aaSettings.buildSettings.bundleBuildPath);
                        buildParams.UseCache = true;// aaSettings.buildSettings.useCache && !forceRebuild;
                        Directory.CreateDirectory(buildParams.TempOutputFolder);

                        var buildContext = new BuildContext(new BundleBuildContent(allBundleInputDefs), new Unity5PackedIdentifiers(), buildParams, dependencyData, bundleWriteData, bundleBuildResults);//, progressTracker);
                        buildContext.SetContextObject(new AddressableAssetsBuildContext() { m_settings = aaSettings, m_runtimeData = runtimeData, m_bundleToAssetGroup = bundleToAssetGroup, m_contentCatalog = contentCatalog });

                        var buildTasks = new List<IBuildTask>();
                        buildTasks.Add(new ProjectInCleanState());
                        buildTasks.Add(new ValidateBundleAssignments());
                        buildTasks.Add(new SwitchToBuildPlatform());
                        buildTasks.Add(new RebuildAtlasCache());
                        buildTasks.Add(new BuildPlayerScripts());
                        buildTasks.Add(new SetBundleSettingsTypeDB());

                        if (runtimeData.resourceProviderMode == ResourceManagerRuntimeData.EditorPlayMode.VirtualMode)
                            buildTasks.Add(new PreviewSceneDependencyData());
                        else
                            buildTasks.Add(new CalculateSceneDependencyData());

                        buildTasks.Add(new CalculateAssetDependencyData());
                        buildTasks.Add(new StripUnusedSpriteSources());
                        buildTasks.Add(new GenerateBundlePacking());
                        buildTasks.Add(new GenerateLocationListsTask());
                        if (runtimeData.resourceProviderMode == ResourceManagerRuntimeData.EditorPlayMode.VirtualMode)
                        {
                            buildTasks.Add(new WriteVirtualBundleDataTask());
                        }
                        else
                        {
                            buildTasks.Add(new GenerateBundleCommands());
                            buildTasks.Add(new GenerateBundleMaps());
                            buildTasks.Add(new WriteSerializedFiles());
                            buildTasks.Add(new ArchiveAndCompressBundles());
                            buildTasks.Add(new PostProcessBundlesTask());
                        }

                        var result = BuildTasksRunner.Run(buildTasks, buildContext);

                        if (Directory.Exists(buildParams.TempOutputFolder))
                            Directory.Delete(buildParams.TempOutputFolder, true);

                        if (result < ReturnCode.Success)
                        {
                            if (buildCompleted != null)
                                buildCompleted(new BuildResult() { completed = false, duration = timer.Elapsed.TotalSeconds, error = result.ToString() });
                            return false;
                        }
                    }
                }
            }

            if (enteringPlayMode && runtimeData.resourceProviderMode != ResourceManagerRuntimeData.EditorPlayMode.PackedMode)
                AddAddressableScenesToEditorBuildSettingsSceneList(aaSettings, runtimeData);
            runtimeData.contentVersion = aaSettings.profileSettings.GetValueByName(aaSettings.activeProfile, "ContentVersion");
            if (string.IsNullOrEmpty(runtimeData.contentVersion))
                runtimeData.contentVersion = "X";

            runtimeData.settingsHash = settingsHash;
            var catalogLocations = new List<ResourceLocationData>();
            foreach (var assetGroup in aaSettings.groups)
                assetGroup.processor.CreateCatalog(aaSettings, assetGroup, contentCatalog, catalogLocations);
            runtimeData.catalogLocations.locations.AddRange(catalogLocations.OrderBy(s => s.m_address));

            contentCatalog.Validate();

            runtimeData.Save(contentCatalog, aaSettings.buildSettings.editorPlayMode.ToString());

            Resources.UnloadUnusedAssets();
            if (buildCompleted != null)
                buildCompleted(new BuildResult() { completed = true, duration = timer.Elapsed.TotalSeconds, locationCount = contentCatalog.locations.Count });
            return true;
        }

        private static void AddAddressableScenesToEditorBuildSettingsSceneList(AddressableAssetSettings settings, ResourceManagerRuntimeData runtimeData)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            var sceneEntries = new List<AddressableAssetSettings.AssetGroup.AssetEntry>();
            settings.GetAllSceneEntries(sceneEntries);
            foreach (var entry in sceneEntries)
                scenes.Add(new EditorBuildSettingsScene(new GUID(entry.guid), true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }


        internal static bool PreviewDependencyInfo(out BuildDependencyData depData, out BundleWriteData bundleWriteData)
        {
            depData = null;
            bundleWriteData = null;
            var aaSettings = AddressableAssetSettings.GetDefault(false, false);
            if (aaSettings == null)
                return false;
            SceneManagerState.Record();

            var runtimeData = new ResourceManagerRuntimeData(ResourceManagerRuntimeData.EditorPlayMode.VirtualMode);
            var contentCatalog = new ResourceLocationList();
            var allBundleInputDefs = new List<AssetBundleBuild>();
            foreach (var assetGroup in aaSettings.groups)
                assetGroup.processor.ProcessGroup(aaSettings, assetGroup, allBundleInputDefs, contentCatalog.locations);

            if (allBundleInputDefs.Count == 0)
                return false;

            using (var progressTracker = new ProgressTracker())
            {
                var buildParams = new BuildParameters(EditorUserBuildSettings.activeBuildTarget, BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget), aaSettings.buildSettings.bundleBuildPath);
                buildParams.UseCache = true;

                Directory.CreateDirectory(buildParams.TempOutputFolder);

                var buildContext = new BuildContext(new BundleBuildContent(allBundleInputDefs), new Unity5PackedIdentifiers(), buildParams, depData = new BuildDependencyData(), bundleWriteData = new BundleWriteData(), new BundleBuildResults(), progressTracker);
                buildContext.SetContextObject(new AddressableAssetsBuildContext() { m_settings = aaSettings, m_runtimeData = runtimeData, m_contentCatalog = contentCatalog });

                var buildTasks = new List<IBuildTask>();
                buildTasks.Add(new ProjectInCleanState());
                buildTasks.Add(new ValidateBundleAssignments());
                buildTasks.Add(new SwitchToBuildPlatform());
                buildTasks.Add(new RebuildAtlasCache());
                buildTasks.Add(new BuildPlayerScripts());
                buildTasks.Add(new SetBundleSettingsTypeDB());
                buildTasks.Add(new PreviewSceneDependencyData());
                buildTasks.Add(new CalculateAssetDependencyData());
                buildTasks.Add(new StripUnusedSpriteSources());
                buildTasks.Add(new GenerateBundlePacking());
                var result = BuildTasksRunner.Run(buildTasks, buildContext);
                    
                if (Directory.Exists(buildParams.TempOutputFolder))
                    Directory.Delete(buildParams.TempOutputFolder, true);

                if (result < ReturnCode.Success)
                    return false;
            }
            return true;
        }
    }
}
