using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEditor.Build.Pipeline.Tasks;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement;

namespace UnityEditor.AddressableAssets
{

    [CreateAssetMenu(fileName = "BuildScriptPacked.asset", menuName = "Addressable Assets/Data Builders/Packed Mode")]
    internal class BuildScriptPackedMode : BuildScriptBase
    {
        public override string Name
        {
            get
            {
                return "Packed Mode";
            }
        }

        public override IDataBuilderGUI CreateGUI(IDataBuilderContext context)
        {
            return null;
        }

        public override bool CanBuildData<T>()
        {
            return typeof(T) == typeof(AddressablesPlayModeBuildResult) || typeof(T) == typeof(AddressablesPlayerBuildResult);
        }
        static int IndexOfSceneInEditorBuildSettings(GUID guid)
        {
            int index = 0;
            for (int i = 0; i < EditorBuildSettings.scenes.Length; i++)
            {
                if (EditorBuildSettings.scenes[i].enabled)
                {
                    if (EditorBuildSettings.scenes[i].guid == guid)
                        return index;
                    index++;
                }
            }
            return -1;
        }

        public override TResult BuildData<TResult>(IDataBuilderContext context)
        {
            var timer = new System.Diagnostics.Stopwatch();
            timer.Start();
            var aaSettings = context.GetValue<AddressableAssetSettings>(AddressablesBuildDataBuilderContext.BuildScriptContextConstants.kAddressableAssetSettings);

            //gather entries
            var playerBuildVersion = context.GetValue<string>(AddressablesBuildDataBuilderContext.BuildScriptContextConstants.kPlayerBuildVersion);
            var isPlayerBuild = typeof(TResult) == typeof(AddressablesPlayerBuildResult);
            var locations = new List<ContentCatalogDataEntry>();
            var allBundleInputDefs = new List<AssetBundleBuild>();
            var bundleToAssetGroup = new Dictionary<string, AddressableAssetGroup>();
            var runtimeData = new ResourceManagerRuntimeData();
            runtimeData.LogResourceManagerExceptions = aaSettings.buildSettings.LogResourceManagerExceptions;
            runtimeData.ProfileEvents = ProjectConfigData.postProfilerEvents;
            bool needsLegacyProvider = false;
            var assetBundleProviderTypes = new HashSet<Type>();
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

                assetBundleProviderTypes.Add(schema.AssetBundleProviderType.Value);

                var packTogether = schema.BundleMode == BundledAssetGroupSchema.BundlePackingMode.PackTogether;
                var bundleInputDefs = new List<AssetBundleBuild>();
                ProcessGroup(assetGroup, bundleInputDefs, locations, packTogether);
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
            ExtractDataTask extractData = new ExtractDataTask();

            if (allBundleInputDefs.Count > 0)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    return AddressableAssetBuildResult.CreateResult<TResult>(0, timer.Elapsed.TotalSeconds, "Unsaved scenes");

                var buildTarget = context.GetValue<BuildTarget>(AddressablesBuildDataBuilderContext.BuildScriptContextConstants.kBuildTarget);
                var buildTargetGroup = context.GetValue<BuildTargetGroup>(AddressablesBuildDataBuilderContext.BuildScriptContextConstants.kBuildTargetGroup);
                var buildParams = new BundleBuildParameters(buildTarget, buildTargetGroup, aaSettings.buildSettings.bundleBuildPath);
                buildParams.UseCache = true;
                buildParams.BundleCompression = aaSettings.buildSettings.compression;

                var buildTasks = RuntimeDataBuildTasks();
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
                    return AddressableAssetBuildResult.CreateResult<TResult>(0, timer.Elapsed.TotalSeconds, "SBP Error" + exitCode);
                foreach (var assetGroup in aaSettings.groups)
                {
                    List<string> bundles;
                    if (aaContext.m_assetGroupToBundles.TryGetValue(assetGroup, out bundles))
                        PostProcessBundles(assetGroup, bundles, results, extractData.WriteData, runtimeData, locations);
                }
            }
            //save catalog
            var contentCatalog = new ContentCatalogData(locations);
            foreach (var assetGroup in aaSettings.groups)
            {
                CreateCatalog(assetGroup, contentCatalog, runtimeData.CatalogLocations, playerBuildVersion);
            }
			runtimeData.CatalogLocations.Sort((a, b) => a.Keys[0].CompareTo(b.Keys[0]));
			var catalogPath = isPlayerBuild ? Addressables.RuntimePath + "/catalog.json" : "Library/com.unity.addressables/catalog_BuildScriptPackedMode.json";
            var settingsPath = isPlayerBuild ? Addressables.RuntimePath + "/settings.json" : "Library/com.unity.addressables/settings_BuildScriptPackedMode.json";
            var settingsRuntimePath = isPlayerBuild ? "{UnityEngine.AddressableAssets.Addressables.RuntimePath}/settings.json" : "file://{UnityEngine.Application.dataPath}/../Library/com.unity.addressables/settings_BuildScriptPackedMode.json";
            WriteFile(catalogPath, JsonUtility.ToJson(contentCatalog));

            var linker = new LinkXMLGenerator();

            //create provider data for bundled assets if there are any groups that are bundled
            if (assetBundleProviderTypes.Count > 0)
            {
                var bapData = ObjectInitializationData.CreateSerializedInitializationData<BundledAssetProvider>();
                var cpData = ObjectInitializationData.CreateSerializedInitializationData<CachedProvider>(typeof(BundledAssetProvider).FullName, 
                    new CachedProvider.Settings() { MaxLRUAge = 1, MaxLRUCount = 10, InternalProviderData = bapData });
                runtimeData.ResourceProviderData.Add(cpData);
                linker.AddTypes(typeof(BundledAssetProvider));
                linker.AddTypes(typeof(CachedProvider));
                linker.AddTypes(bapData.GetRuntimeTypes());
                linker.AddTypes(cpData.GetRuntimeTypes());
            }

            foreach (var pt in assetBundleProviderTypes)
            {
                linker.AddTypes(pt);
                var bapData = ObjectInitializationData.CreateSerializedInitializationData(pt);
                var cpData = ObjectInitializationData.CreateSerializedInitializationData<CachedProvider>(typeof(BundledAssetProvider).FullName,
                    new CachedProvider.Settings() { MaxLRUAge = 1, MaxLRUCount = 10, InternalProviderData = bapData });
                linker.AddTypes(cpData.GetRuntimeTypes());
                runtimeData.ResourceProviderData.Add(cpData);
            }

            if (needsLegacyProvider)
            {
                runtimeData.ResourceProviderData.Add(ObjectInitializationData.CreateSerializedInitializationData(typeof(LegacyResourcesProvider)));
                linker.AddTypes(typeof(LegacyResourcesProvider));
            }

            runtimeData.InstanceProviderData = ObjectInitializationData.CreateSerializedInitializationData<InstanceProvider>();
            runtimeData.SceneProviderData = ObjectInitializationData.CreateSerializedInitializationData<SceneProvider>();
            linker.AddTypes(typeof(InstanceProvider), typeof(SceneProvider));

            foreach (IObjectInitializationDataProvider io in aaSettings.InitializationObjects)
            {
                var id = io.CreateObjectInitializationData();
                runtimeData.InitializationObjects.Add(id);
                linker.AddTypes(id.ObjectType.Value);
                linker.AddTypes(id.GetRuntimeTypes());
            }
            linker.AddTypes(typeof(Addressables));
            if (isPlayerBuild)
                linker.Save(Addressables.BuildPath + "/link.xml");

            WriteFile(settingsPath, JsonUtility.ToJson(runtimeData));


            //inform runtime of the init data path
            PlayerPrefs.SetString(Addressables.kAddressablesRuntimeDataPath, settingsRuntimePath);

            var opResult = AddressableAssetBuildResult.CreateResult<TResult>(locations.Count, timer.Elapsed.TotalSeconds);

            //save content update data if building for the player
            if (isPlayerBuild)
            {
                var allEntries = new List<AddressableAssetEntry>();
                aaSettings.GetAllAssets(allEntries, g => g.HasSchema<ContentUpdateGroupSchema>() && g.GetSchema<ContentUpdateGroupSchema>().StaticContent);
                if (allEntries.Count > 0)
                    (opResult as AddressablesPlayerBuildResult).ContentStateDataPath = ContentUpdateScript.SaveContentState(allEntries, extractData.BuildCache, playerBuildVersion);
            }
            return opResult;
        }

        static internal void ProcessGroup(AddressableAssetGroup assetGroup, List<AssetBundleBuild> bundleInputDefs, List<ContentCatalogDataEntry> locationData, bool packTogether)
        {
            if (packTogether)
            {
                var allEntries = new List<AddressableAssetEntry>();
                foreach (var a in assetGroup.entries)
                    a.GatherAllAssets(allEntries, true, true);
                GenerateBuildInputDefinitions(allEntries, bundleInputDefs, assetGroup.Name, "all");
            }
            else
            {
                foreach (var a in assetGroup.entries)
                {
                    var allEntries = new List<AddressableAssetEntry>();
                    a.GatherAllAssets(allEntries, true, true);
                    GenerateBuildInputDefinitions(allEntries, bundleInputDefs, assetGroup.Name, a.address);
                }
            }
        }

        static private void GenerateBuildInputDefinitions(List<AddressableAssetEntry> allEntries, List<AssetBundleBuild> buildInputDefs, string groupName, string address)
        {
            var scenes = new List<AddressableAssetEntry>();
            var assets = new List<AddressableAssetEntry>();
            foreach (var e in allEntries)
            {
                if (e.AssetPath.EndsWith(".unity"))
                    scenes.Add(e);
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

        static internal void CreateCatalog(AddressableAssetGroup group, ContentCatalogData contentCatalog, List<ResourceLocationData> locations, string playerVersion)
        {
            var schema = group.GetSchema<BundledAssetGroupSchema>();
            if (schema == null)
                return;

            var aaSettings = group.Settings;
            var bPath = schema.BuildPath.GetValue(group.Settings);


            var buildPath = bPath + aaSettings.profileSettings.EvaluateString(aaSettings.activeProfileId, "/catalog_" + playerVersion + ".json");
            var remoteHashLoadPath = group.GetSchema<BundledAssetGroupSchema>().LoadPath.GetValue(group.Settings) + "/" + "catalog_" + playerVersion + ".hash";

            var localCacheLoadPath = "{UnityEngine.Application.persistentDataPath}/com.unity.addressables/catalog_" + playerVersion + ".hash";

            var jsonText = JsonUtility.ToJson(contentCatalog);
            var contentHash = UnityEditor.Build.Pipeline.Utilities.HashingMethods.Calculate(jsonText).ToString();

            WriteFile(buildPath, jsonText);
            WriteFile(buildPath.Replace(".json", ".hash"), contentHash);

            var depKeys = new string[] { "RemoteCatalogHash" + group.Guid, "LocalCatalogHash" + group.Guid };

            var remoteHash = new ResourceLocationData(new string[] { depKeys[0] }, remoteHashLoadPath, typeof(TextDataProvider));
            var localHash = new ResourceLocationData(new string[] { depKeys[1] }, localCacheLoadPath, typeof(TextDataProvider));

            var internalId = remoteHashLoadPath.Replace(".hash", ".json");
            var loadOrder = IsInternalIdLocal(internalId) ? "1" : "0";
            locations.Add(new ResourceLocationData(new string[] { loadOrder + "_RemoteCatalog_" + group.Guid, "catalogs" }, internalId, typeof(ContentCatalogProvider), depKeys));
            locations.Add(localHash);
            locations.Add(remoteHash);
        }

        static IList<IBuildTask> RuntimeDataBuildTasks()
        {
            var buildTasks = new List<IBuildTask>();

            // Setup
            buildTasks.Add(new SwitchToBuildPlatform());
            buildTasks.Add(new RebuildSpriteAtlasCache());

            // Player Scripts
            buildTasks.Add(new BuildPlayerScripts());

            // Dependency
            buildTasks.Add(new CalculateSceneDependencyData());
            buildTasks.Add(new CalculateAssetDependencyData());
            buildTasks.Add(new StripUnusedSpriteSources());
            buildTasks.Add(new CreateBuiltInShadersBundle("UnityBuiltInShaders"));

            // Packing
            buildTasks.Add(new GenerateBundlePacking());
            buildTasks.Add(new UpdateBundleObjectLayout());
            buildTasks.Add(new GenerateLocationListsTask());

            buildTasks.Add(new GenerateBundleCommands());
            buildTasks.Add(new GenerateSpritePathMaps());
            buildTasks.Add(new GenerateBundleMaps());

            // Writing
            buildTasks.Add(new WriteSerializedFiles());
            buildTasks.Add(new ArchiveAndCompressBundles());
            //   buildTasks.Add(new PostProcessBundlesTask());

            return buildTasks;
        }

        static bool IsInternalIdLocal(string path)
        {
            return path.StartsWith("{UnityEngine.AddressableAssets.Addressables.RuntimePath}");
        }

        static internal void PostProcessBundles(AddressableAssetGroup assetGroup, List<string> bundles, IBundleBuildResults buildResult, IWriteData writeData, ResourceManagerRuntimeData runtimeData, List<ContentCatalogDataEntry> locations)
        {
            var schema = assetGroup.GetSchema<BundledAssetGroupSchema>();
            if (schema == null)
                return;

            var path = schema.BuildPath.GetValue(assetGroup.Settings);
            if (string.IsNullOrEmpty(path))
                return;

            foreach (var bundleName in bundles)
            {
                var info = buildResult.BundleInfos[bundleName];
                ContentCatalogDataEntry dataEntry = locations.First(s => bundleName == (string)s.Keys[0]);
                bool isLocalBundle = true;
                if (dataEntry != null)
                {
                    isLocalBundle = IsInternalIdLocal(dataEntry.InternalId);
                    if (!isLocalBundle)
                    {
                        var requestOptions = new AssetBundleRequestOptions()
                        {
                            Crc =  schema.UseAssetBundleCache ? info.Crc : 0,
                            Hash = schema.UseAssetBundleCache ? info.Hash.ToString() : "",
                            ChunkedTransfer = schema.ChunkedTransfer,
                            RedirectLimit = schema.RedirectLimit,
                            RetryCount = schema.RetryCount,
                            Timeout = schema.Timeout
                        };
                        dataEntry.Data = requestOptions;
                        dataEntry.InternalId = dataEntry.InternalId.Replace(".bundle", "_" + info.Hash + ".bundle");
                    }
                }
                else
                {
                    Debug.LogWarningFormat("Unable to find ContentCatalogDataEntry for bundle {0}.", bundleName);
                }

                var targetPath = Path.Combine(path, isLocalBundle ? bundleName : bundleName.Replace(".bundle", "_" + info.Hash + ".bundle"));
                if (!Directory.Exists(Path.GetDirectoryName(targetPath)))
                    Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
                File.Copy(Path.Combine(assetGroup.Settings.buildSettings.bundleBuildPath, bundleName), targetPath, true);
            }
        }
    }
}