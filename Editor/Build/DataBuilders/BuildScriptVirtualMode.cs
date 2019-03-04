using System;
using System.Collections.Generic;
using UnityEditor.AddressableAssets.Build.BuildPipelineTasks;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEditor.Build.Pipeline.Tasks;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.Initialization;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.AddressableAssets.ResourceProviders;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.ResourceManagement.ResourceProviders.Simulation;
using UnityEngine.ResourceManagement.Util;
using System.IO;
using System.Linq;

namespace UnityEditor.AddressableAssets.Build.DataBuilders
{

    interface IAddressableAssetsBuildContext : IContextObject { }

    class AddressableAssetsBuildContext : IAddressableAssetsBuildContext
    {
        public AddressableAssetSettings settings;
        public ResourceManagerRuntimeData runtimeData;
        public List<ContentCatalogDataEntry> locations;
        public Dictionary<string, string> bundleToAssetGroup;
        public Dictionary<AddressableAssetGroup, List<string>> assetGroupToBundles;
    }

    /// <summary>
    /// Build script for creating virtual asset bundle dat for running in the editor.
    /// </summary>
    [CreateAssetMenu(fileName = "BuildScriptVirtual.asset", menuName = "Addressable Assets/Data Builders/Virtual Mode")]
    public class BuildScriptVirtualMode : BuildScriptBase
    {
        public override string Name
        {
            get { return "Virtual Mode"; }
        }

        public override bool CanBuildData<T>()
        {
            return typeof(T) == typeof(AddressablesPlayModeBuildResult);
        }

        public override void ClearCachedData()
        {
            DeleteFile(string.Format(m_PathFormat, "", "catalog"));
            DeleteFile(string.Format(m_PathFormat, "", "settings"));
        }

        string m_PathFormat = "{0}Library/com.unity.addressables/{1}_BuildScriptVirtualMode.json";

        public override T BuildData<T>(IDataBuilderContext context)
        {
            var timer = new System.Diagnostics.Stopwatch();
            timer.Start();
            var aaSettings = context.GetValue<AddressableAssetSettings>(AddressablesBuildDataBuilderContext.BuildScriptContextConstants.kAddressableAssetSettings);

            m_PathFormat = context.GetValue("PathFormat", m_PathFormat);
            var scenesToAdd = new List<EditorBuildSettingsScene>();

            //gather entries
            var locations = new List<ContentCatalogDataEntry>();
            var allBundleInputDefs = new List<AssetBundleBuild>();
            var bundleToAssetGroup = new Dictionary<string, string>();
            var runtimeData = new ResourceManagerRuntimeData();
            runtimeData.BuildTarget = context.GetValue<BuildTarget>(AddressablesBuildDataBuilderContext.BuildScriptContextConstants.kBuildTarget).ToString();
            runtimeData.ProfileEvents = ProjectConfigData.postProfilerEvents;
            runtimeData.LogResourceManagerExceptions = aaSettings.buildSettings.LogResourceManagerExceptions;
            runtimeData.ProfileEvents = ProjectConfigData.postProfilerEvents;

            var createdProviderIds = new Dictionary<string, VirtualAssetBundleRuntimeData>();
            var bundleProviderIdToSchema = new Dictionary<string, BundledAssetGroupSchema>();
            var resourceProviderData = new List<ObjectInitializationData>();
            foreach (var assetGroup in aaSettings.groups)
            {
                if (assetGroup.HasSchema<PlayerDataGroupSchema>())
                {
                    if (CreateLocationsForPlayerData(assetGroup, locations))
                    {
                        if (!createdProviderIds.ContainsKey(typeof(LegacyResourcesProvider).Name))
                        {
                            createdProviderIds.Add(typeof(LegacyResourcesProvider).Name, null);
                            resourceProviderData.Add(ObjectInitializationData.CreateSerializedInitializationData(typeof(LegacyResourcesProvider)));
                        }
                    }
                    continue;
                }

                var schema = assetGroup.GetSchema<BundledAssetGroupSchema>();
                if (schema == null)
                    continue;

                var bundledProviderId = schema.GetBundleCachedProviderId();
                var assetProviderId = schema.GetAssetCachedProviderId();
                if (!createdProviderIds.ContainsKey(bundledProviderId))
                {
                    //TODO: pull from schema instead of ProjectConfigData
                    var virtualBundleRuntimeData = new VirtualAssetBundleRuntimeData(ProjectConfigData.localLoadSpeed, ProjectConfigData.remoteLoadSpeed);
                    //save virtual runtime data to collect assets into virtual bundles
                    createdProviderIds.Add(bundledProviderId, virtualBundleRuntimeData);
                    //save schema for later since we need to collect virtual bundles first
                    bundleProviderIdToSchema.Add(bundledProviderId, schema);
                }

                if (!createdProviderIds.ContainsKey(assetProviderId))
                {
                    createdProviderIds.Add(assetProviderId, null);

                    var assetProviderData = ObjectInitializationData.CreateSerializedInitializationData<VirtualBundledAssetProvider>(assetProviderId);
                    var assetCachedProviderData = ObjectInitializationData.CreateSerializedInitializationData<CachedProvider>(assetProviderId,
                        new CachedProvider.Settings
                        {
                            maxLruAge = schema.AssetCachedProviderMaxLRUAge < 0 ? 0 : schema.AssetCachedProviderMaxLRUAge,
                            maxLruCount = schema.AssetCachedProviderMaxLRUCount < 0 ? 0 : schema.AssetCachedProviderMaxLRUCount,
                            InternalProviderData = assetProviderData
                        });
                    resourceProviderData.Add(assetCachedProviderData);
                }


                var packTogether = schema.BundleMode == BundledAssetGroupSchema.BundlePackingMode.PackTogether;

                var bundleInputDefs = new List<AssetBundleBuild>();
                ProcessGroup(assetGroup, bundleInputDefs, packTogether, scenesToAdd);
                for (int i = 0; i < bundleInputDefs.Count; i++)
                {
                    if (bundleToAssetGroup.ContainsKey(bundleInputDefs[i].assetBundleName))
                    {
                        var bid = bundleInputDefs[i];
                        int count = 1;
                        var newName = bid.assetBundleName;
                        while (bundleToAssetGroup.ContainsKey(newName) && count < 1000)
                            newName = bid.assetBundleName.Replace(".bundle", string.Format("{0}.bundle", count++));
                        bundleInputDefs[i] = new AssetBundleBuild { assetBundleName = newName, addressableNames = bid.addressableNames, assetBundleVariant = bid.assetBundleVariant, assetNames = bid.assetNames };
                    }

                    bundleToAssetGroup.Add(bundleInputDefs[i].assetBundleName, assetGroup.Guid);
                }

                allBundleInputDefs.AddRange(bundleInputDefs);
            }

            if (allBundleInputDefs.Count > 0)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    return AddressableAssetBuildResult.CreateResult<T>(null, 0, timer.Elapsed.TotalSeconds, "Unsaved scenes");

                var buildTarget = context.GetValue<BuildTarget>(AddressablesBuildDataBuilderContext.BuildScriptContextConstants.kBuildTarget);
                var buildTargetGroup = context.GetValue<BuildTargetGroup>(AddressablesBuildDataBuilderContext.BuildScriptContextConstants.kBuildTargetGroup);
                var buildParams = new BundleBuildParameters(buildTarget, buildTargetGroup, aaSettings.buildSettings.bundleBuildPath);
                buildParams.UseCache = true;
                buildParams.BundleCompression = aaSettings.buildSettings.compression;

                var buildTasks = RuntimeDataBuildTasks(aaSettings.buildSettings.compileScriptsInVirtualMode, aaSettings.DefaultGroup.Name + "_UnityBuiltInShaders.bundle");
                ExtractDataTask extractData = new ExtractDataTask();
                buildTasks.Add(extractData);

                var aaContext = new AddressableAssetsBuildContext
                {
                    settings = aaSettings,
                    runtimeData = runtimeData,
                    bundleToAssetGroup = bundleToAssetGroup,
                    locations = locations
                };
                string aaPath = aaSettings.AssetPath;
                IBundleBuildResults results;
                var exitCode = ContentPipeline.BuildAssetBundles(buildParams, new BundleBuildContent(allBundleInputDefs), out results, buildTasks, aaContext);

                if (exitCode < ReturnCode.Success)
                    return AddressableAssetBuildResult.CreateResult<T>(null, 0, timer.Elapsed.TotalSeconds, "SBP Error" + exitCode);
                if (aaSettings == null && !string.IsNullOrEmpty(aaPath))
                    aaSettings = AssetDatabase.LoadAssetAtPath<AddressableAssetSettings>(aaPath);
            }

            var bundledAssets = new Dictionary<object, List<string>>();
            foreach (var loc in locations)
            {
                //if (loc.Provider == typeof(BundledAssetProvider).FullName)
                if (loc.Dependencies != null && loc.Dependencies.Count > 0)
                {
                    //   if (loc.Dependencies == null || loc.Dependencies.Count == 0)
                    //       continue;
                    for (int i = 0; i < loc.Dependencies.Count; i++)
                    {
                        var dep = loc.Dependencies[i];
                        List<string> assetsInBundle;
                        if (!bundledAssets.TryGetValue(dep, out assetsInBundle))
                            bundledAssets.Add(dep, assetsInBundle = new List<string>());
                        if (i == 0) //only add the asset to the first bundle...
                            assetsInBundle.Add(loc.InternalId);
                    }
                }
            }
            foreach (var bd in bundledAssets)
            {
                AddressableAssetGroup group = aaSettings.DefaultGroup;
                string groupGuid;
                if (bundleToAssetGroup.TryGetValue(bd.Key as string, out groupGuid))
                    group = aaSettings.FindGroup(g => g.Guid == groupGuid);

                var schema = group.GetSchema<BundledAssetGroupSchema>();
                if (schema != null)
                {
                    var bundleLocData = locations.First(s => s.Keys[0] == bd.Key);
                    var isLocalBundle = IsInternalIdLocal(bundleLocData.InternalId);
                    uint crc = (uint)UnityEngine.Random.Range(0, int.MaxValue);
                    var hash = Guid.NewGuid().ToString();
                    bundleLocData.InternalId = bundleLocData.InternalId.Replace(".bundle", "_" + hash + ".bundle");

                    var virtualBundleName = AddressablesRuntimeProperties.EvaluateString(bundleLocData.InternalId);
                    var bundleData = new VirtualAssetBundle(virtualBundleName, isLocalBundle, crc, hash);

                    long dataSize = 0;
                    long headerSize = 0;
                    foreach (var a in bd.Value)
                    {
                        var size = ComputeSize(a);
                        bundleData.Assets.Add(new VirtualAssetBundleEntry(a, size));
                        dataSize += size;
                        headerSize += a.Length * 5; //assume 5x path length overhead size per item, probably much less
                    }
                    if (bd.Value.Count == 0)
                    {
                        dataSize = 100 * 1024;
                        headerSize = 1024;
                    }
                    bundleData.SetSize(dataSize, headerSize);


                    var requestOptions = new VirtualAssetBundleRequestOptions
                    {
                        Crc = schema.UseAssetBundleCrc ? crc : 0,
                        Hash = schema.UseAssetBundleCache ? hash : "",
                        ChunkedTransfer = schema.ChunkedTransfer,
                        RedirectLimit = schema.RedirectLimit,
                        RetryCount = schema.RetryCount,
                        Timeout = schema.Timeout,
                        BundleName = Path.GetFileName(bundleLocData.InternalId),
                        BundleSize = dataSize + headerSize
                    };
                    bundleLocData.Data = requestOptions;

                    var bundleProviderId = schema.GetBundleCachedProviderId();
                    var virtualBundleRuntimeData = createdProviderIds[bundleProviderId];
                    virtualBundleRuntimeData.AssetBundles.Add(bundleData);
                }

            }
            foreach (var kvp in createdProviderIds)
            {
                if (kvp.Value != null)
                {
                    var schema = bundleProviderIdToSchema[kvp.Key];
                    var bundleProviderData = ObjectInitializationData.CreateSerializedInitializationData<VirtualAssetBundleProvider>(kvp.Key, kvp.Value);
                    var bundleCachedProviderData = ObjectInitializationData.CreateSerializedInitializationData<CachedProvider>(kvp.Key,
                        new CachedProvider.Settings
                        {
                            maxLruAge = schema.BundleCachedProviderMaxLRUAge < 0 ? 0 : schema.BundleCachedProviderMaxLRUAge,
                            maxLruCount = schema.BundleCachedProviderMaxLRUCount < 0 ? 0 : schema.BundleCachedProviderMaxLRUCount,
                            InternalProviderData = bundleProviderData
                        });
                    resourceProviderData.Add(bundleCachedProviderData);
                }
            }

            var contentCatalog = new ContentCatalogData(locations);
            contentCatalog.ResourceProviderData.AddRange(resourceProviderData);
            contentCatalog.InstanceProviderData = ObjectInitializationData.CreateSerializedInitializationData<InstanceProvider>();
            contentCatalog.SceneProviderData = ObjectInitializationData.CreateSerializedInitializationData<SceneProvider>();
            //save catalog
            WriteFile(string.Format(m_PathFormat, "", "catalog"), JsonUtility.ToJson(contentCatalog));

            //create runtime data
            runtimeData.CatalogLocations.Add(new ResourceLocationData(new[] { InitializationOperation.CatalogAddress}, string.Format(m_PathFormat, "file://{UnityEngine.Application.dataPath}/../", "catalog"), typeof(ContentCatalogProvider)));

            foreach (var io in aaSettings.InitializationObjects)
            {
                if (io is IObjectInitializationDataProvider)
                    runtimeData.InitializationObjects.Add((io as IObjectInitializationDataProvider).CreateObjectInitializationData());
            }

            var settingsPath = string.Format(m_PathFormat, "", "settings");
            WriteFile(settingsPath, JsonUtility.ToJson(runtimeData));

            //inform runtime of the init data path
            var runtimeSettingsPath = string.Format(m_PathFormat, "file://{UnityEngine.Application.dataPath}/../", "settings");
            Debug.LogFormat("Settings runtime path in PlayerPrefs to {0}", runtimeSettingsPath);
            PlayerPrefs.SetString(Addressables.kAddressablesRuntimeDataPath, runtimeSettingsPath);
            IDataBuilderResult res = new AddressablesPlayModeBuildResult { OutputPath = settingsPath, ScenesToAdd = scenesToAdd, Duration = timer.Elapsed.TotalSeconds, LocationCount = locations.Count };
            return (T)res;
        }
        static bool IsInternalIdLocal(string path)
        {
            return path.StartsWith("{UnityEngine.AddressableAssets.Addressables.RuntimePath}");
        }
        static long ComputeSize(string a)
        {
            var guid = AssetDatabase.AssetPathToGUID(a);
            if (string.IsNullOrEmpty(guid) || guid.Length < 2)
                return 1024;
            var path = string.Format("Library/metadata/{0}{1}/{2}", guid[0], guid[1], guid);
            if (!File.Exists(path))
                return 1024;
            return new FileInfo(path).Length;
        }

        static void ProcessGroup(AddressableAssetGroup assetGroup, List<AssetBundleBuild> bundleInputDefs, bool packTogether, List<EditorBuildSettingsScene> scenesToAdd)
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

        static void GenerateBuildInputDefinitions(List<AddressableAssetEntry> allEntries, List<AssetBundleBuild> buildInputDefs, string groupName, string address, List<EditorBuildSettingsScene> scenesToAdd)
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

        static AssetBundleBuild GenerateBuildInputDefinition(List<AddressableAssetEntry> assets, string name)
        {
            var assetsInputDef = new AssetBundleBuild();
            assetsInputDef.assetBundleName = name.ToLower().Replace(" ", "").Replace('\\', '/').Replace("//", "/");
            var assetIds = new List<string>(assets.Count);
            foreach (var a in assets)
            {
                assetIds.Add(a.GetAssetLoadPath(true));
            }

            assetsInputDef.assetNames = assetIds.ToArray();
            assetsInputDef.addressableNames = new string[0];
            return assetsInputDef;
        }

        static IList<IBuildTask> RuntimeDataBuildTasks(bool compileScripts, string builtinShaderBundleName)
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
            buildTasks.Add(new CreateBuiltInShadersBundle(builtinShaderBundleName));

            // Packing
            buildTasks.Add(new GenerateBundlePacking());
            buildTasks.Add(new UpdateBundleObjectLayout());
            buildTasks.Add(new GenerateLocationListsTask());
            return buildTasks;
        }
    }
}
