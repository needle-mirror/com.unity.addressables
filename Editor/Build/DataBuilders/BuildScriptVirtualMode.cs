using System.Collections.Generic;
using System.IO;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEditor.Build.Pipeline.Tasks;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement;

namespace UnityEditor.AddressableAssets
{
    internal enum BundleMode
    {
        PackTogether,
        PackSeparately
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

    [CreateAssetMenu(fileName = "BuildScriptVirtual.asset", menuName = "Addressable Assets/Data Builders/Virtual Mode")]
    internal class BuildScriptVirtualMode : BuildScriptBase
    {
        public override string Name
        {
            get
            {
                return "Virtual Mode";
            }
        }

        public override IDataBuilderGUI CreateGUI(IDataBuilderContext context)
        {
            return null;
        }

        public override bool CanBuildData<T>()
        {
            return typeof(T) == typeof(AddressablesPlayModeBuildResult);
        }
        string pathFormat = "{0}Library/com.unity.addressables/{1}_BuildScriptVirtualMode.json";
        public override T BuildData<T>(IDataBuilderContext context)
        {
            var timer = new System.Diagnostics.Stopwatch();
            timer.Start();
            var aaSettings = context.GetValue<AddressableAssetSettings>(AddressablesBuildDataBuilderContext.BuildScriptContextConstants.kAddressableAssetSettings);

            var scenesToAdd = new List<EditorBuildSettingsScene>();

            //gather entries
            var locations = new List<ContentCatalogDataEntry>();
            var allBundleInputDefs = new List<AssetBundleBuild>();
            var bundleToAssetGroup = new Dictionary<string, AddressableAssetGroup>();
            var runtimeData = new ResourceManagerRuntimeData();
            runtimeData.ProfileEvents = ProjectConfigData.postProfilerEvents;
            runtimeData.LogResourceManagerExceptions = aaSettings.buildSettings.LogResourceManagerExceptions;
            bool needsLegacyProvider = false;
            foreach (var assetGroup in aaSettings.groups)
            {
                if (assetGroup.HasSchema<PlayerDataGroupSchema>())
                {
                    needsLegacyProvider = CreateLocationsForPlayerData(assetGroup, locations);
                    continue;
                }

                var schema = assetGroup.GetSchema<BundledAssetGroupSchema>();
                if (schema == null)
                    continue;

                var packTogether = schema.BundleMode == BundledAssetGroupSchema.BundlePackingMode.PackTogether;

                var bundleInputDefs = new List<AssetBundleBuild>();
                ProcessGroup(assetGroup, bundleInputDefs, locations, packTogether, scenesToAdd);
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
                    return AddressableAssetBuildResult.CreateResult<T>(0, timer.Elapsed.TotalSeconds, "Unsaved scenes");

                var buildTarget = context.GetValue<BuildTarget>(AddressablesBuildDataBuilderContext.BuildScriptContextConstants.kBuildTarget);
                var buildTargetGroup = context.GetValue<BuildTargetGroup>(AddressablesBuildDataBuilderContext.BuildScriptContextConstants.kBuildTargetGroup);
                var buildParams = new BundleBuildParameters(buildTarget, buildTargetGroup, aaSettings.buildSettings.bundleBuildPath);
                buildParams.UseCache = true;
                buildParams.BundleCompression = aaSettings.buildSettings.compression;

                var buildTasks = RuntimeDataBuildTasks(aaSettings.buildSettings.compileScriptsInVirtualMode, true);
                ExtractDataTask extractData = new ExtractDataTask();
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
                    return AddressableAssetBuildResult.CreateResult<T>(0, timer.Elapsed.TotalSeconds, "SBP Error" + exitCode);
            }
            //save catalog
            WriteFile(string.Format(pathFormat, "", "catalog"), JsonUtility.ToJson(new ContentCatalogData(locations)));

            //create runtime data
            runtimeData.CatalogLocations.Add(new ResourceLocationData(new string[] { "catalogs" }, string.Format(pathFormat, "file://{UnityEngine.Application.dataPath}/../", "catalog"), typeof(JsonAssetProvider)));

            { //serialize the bundle provider
                var bapData = ObjectInitializationData.CreateSerializedInitializationData<VirtualAssetBundleProvider>(typeof(AssetBundleProvider).FullName);
                var cpData = ObjectInitializationData.CreateSerializedInitializationData<CachedProvider>(typeof(AssetBundleProvider).FullName,
                    new CachedProvider.Settings() { MaxLRUAge = 1, MaxLRUCount = 10, InternalProviderData = bapData });
                runtimeData.ResourceProviderData.Add(cpData);
            }

            { //serialize the bundled asset provider
                var bapData = ObjectInitializationData.CreateSerializedInitializationData<VirtualBundledAssetProvider>(typeof(BundledAssetProvider).FullName);
                var cpData = ObjectInitializationData.CreateSerializedInitializationData<CachedProvider>(typeof(BundledAssetProvider).FullName,
                    new CachedProvider.Settings() { MaxLRUAge = 1, MaxLRUCount = 10, InternalProviderData = bapData });
                runtimeData.ResourceProviderData.Add(cpData);
            }


            //            runtimeData.ResourceProviderData.Add(CachedProvider.CreateProviderData<VirtualAssetBundleProvider>(typeof(AssetBundleProvider).FullName, 5, 1));
            if (needsLegacyProvider)
                runtimeData.ResourceProviderData.Add(ObjectInitializationData.CreateSerializedInitializationData(typeof(LegacyResourcesProvider)));
            runtimeData.InstanceProviderData = ObjectInitializationData.CreateSerializedInitializationData<InstanceProvider>();
            runtimeData.SceneProviderData = ObjectInitializationData.CreateSerializedInitializationData<SceneProvider>();
            foreach (IObjectInitializationDataProvider io in aaSettings.InitializationObjects)
                runtimeData.InitializationObjects.Add(io.CreateObjectInitializationData());
            WriteFile(string.Format(pathFormat, "", "settings"), JsonUtility.ToJson(runtimeData));

            //inform runtime of the init data path
            var settingsPath = string.Format(pathFormat, "file://{UnityEngine.Application.dataPath}/../", "settings");
            PlayerPrefs.SetString(Addressables.kAddressablesRuntimeDataPath, settingsPath);

            IDataBuilderResult res = new AddressablesPlayModeBuildResult() { ScenesToAdd = scenesToAdd, Duration = timer.Elapsed.TotalSeconds, LocationCount = locations.Count };
            return (T)res;
        }

        static internal void ProcessGroup(AddressableAssetGroup assetGroup, List<AssetBundleBuild> bundleInputDefs, List<ContentCatalogDataEntry> locationData, bool packTogether, List<EditorBuildSettingsScene> scenesToAdd)
        {
            if (packTogether)
            {
                var allEntries = new List<AddressableAssetEntry>();
                foreach (var a in assetGroup.entries)
                    a.GatherAllAssets(allEntries, true, true);
                GenerateBuildInputDefinitions(allEntries, bundleInputDefs, assetGroup.Name, "all", scenesToAdd);
            }
            else
            {
                foreach (var a in assetGroup.entries)
                {
                    var allEntries = new List<AddressableAssetEntry>();
                    a.GatherAllAssets(allEntries, true, true);
                    GenerateBuildInputDefinitions(allEntries, bundleInputDefs, assetGroup.Name, a.address, scenesToAdd);
                }
            }
        }

        static private void GenerateBuildInputDefinitions(List<AddressableAssetEntry> allEntries, List<AssetBundleBuild> buildInputDefs, string groupName, string address, List<EditorBuildSettingsScene> scenesToAdd)
        {
            var scenes = new List<AddressableAssetEntry>();
            var assets = new List<AddressableAssetEntry>();
            foreach (var e in allEntries)
            {
                if (e.IsScene)
                {
                    scenes.Add(e);
                    scenesToAdd.Add(new EditorBuildSettingsScene(new GUID(e.guid), true));
                }
                else
                    assets.Add(e);
            }
            if (assets.Count > 0)
                buildInputDefs.Add(GenerateBuildInputDefinition(assets, groupName + "_assets_" + address + ".bundle"));
            if (scenes.Count > 0)
                buildInputDefs.Add(GenerateBuildInputDefinition(scenes, groupName + "_scenes_" + address + ".bundle"));
        }

        static private AssetBundleBuild GenerateBuildInputDefinition(List<AddressableAssetEntry> assets, string name)
        {
            var assetsInputDef = new AssetBundleBuild();
            assetsInputDef.assetBundleName = name.ToLower().Replace(" ", "").Replace('\\', '/').Replace("//", "/");
            var assetIds = new List<string>(assets.Count);
            var assetGuids = new List<string>(assets.Count);
            foreach (var a in assets)
            {
                assetIds.Add(a.AssetPath);
                assetGuids.Add(a.guid);
            }
            assetsInputDef.assetNames = assetIds.ToArray();
            assetsInputDef.addressableNames = new string[0];
            return assetsInputDef;
        }

        static IList<IBuildTask> RuntimeDataBuildTasks(bool compileScripts, bool writeData)
        {
            var buildTasks = new List<IBuildTask>();

            // Setup
            buildTasks.Add(new SwitchToBuildPlatform());
            buildTasks.Add(new RebuildSpriteAtlasCache());

            // Player Scripts
            if (compileScripts)
                buildTasks.Add(new BuildPlayerScripts());

            // Dependency
            buildTasks.Add(new PreviewSceneDependencyData());
            buildTasks.Add(new CalculateAssetDependencyData());
            buildTasks.Add(new StripUnusedSpriteSources());
            buildTasks.Add(new CreateBuiltInShadersBundle("UnityBuiltInShaders"));

            // Packing
            buildTasks.Add(new GenerateBundlePacking());
            buildTasks.Add(new UpdateBundleObjectLayout());
            buildTasks.Add(new GenerateLocationListsTask());
            buildTasks.Add(new WriteVirtualBundleDataTask(writeData));
            return buildTasks;
        }
    }
}
