using System.Collections.Generic;
using UnityEngine;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEditor.Build.Pipeline.Tasks;
using UnityEngine.AddressableAssets;
using System;

namespace UnityEditor.AddressableAssets
{
    class CheckDupeDependencies : AnalyzeRule
    {
        [NonSerialized]
        HashSet<GUID> m_ImplicitAssets;
        internal override string name
        { get { return "Check Duplicate Bundle Dependencies"; } }

        internal override List<AnalyzeResult> RefreshAnalysis(AddressableAssetSettings settings)
        {
            return DoFakeBuild(settings);
        }

        List<AnalyzeResult> DoFakeBuild(AddressableAssetSettings settings)
        {
            m_ImplicitAssets = new HashSet<GUID>();
            List<AnalyzeResult> emptyResult = new List<AnalyzeResult>();
            emptyResult.Add(new AnalyzeResult(name + " - No issues found"));
            IDataBuilderContext context = new AddressablesBuildDataBuilderContext(settings);
            var timer = new System.Diagnostics.Stopwatch();
            timer.Start();
            var aaSettings = context.GetValue<AddressableAssetSettings>(AddressablesBuildDataBuilderContext.BuildScriptContextConstants.kAddressableAssetSettings);

            //gather entries
            var locations = new List<UnityEngine.AddressableAssets.ContentCatalogDataEntry>();
            var allBundleInputDefs = new List<AssetBundleBuild>();
            var bundleToAssetGroup = new Dictionary<string, AddressableAssetGroup>();
            var runtimeData = new ResourceManagerRuntimeData();
            runtimeData.LogResourceManagerExceptions = aaSettings.buildSettings.LogResourceManagerExceptions;

            foreach (var assetGroup in aaSettings.groups)
            {
                var schema = assetGroup.GetSchema<BundledAssetGroupSchema>();
                if (schema == null)
                    continue;

                var packTogether = schema.BundleMode == BundledAssetGroupSchema.BundlePackingMode.PackTogether;
                var bundleInputDefs = new List<AssetBundleBuild>();
                BuildScriptPackedMode.ProcessGroup(assetGroup, bundleInputDefs, locations, packTogether);
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
                if (!SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    Debug.LogError("Cannot run Analyze with unsaved scenes");
                    return emptyResult;
                }

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

                IBundleBuildResults buildResults;
                var exitCode = ContentPipeline.BuildAssetBundles(buildParams, new BundleBuildContent(allBundleInputDefs), out buildResults, buildTasks, aaContext);

                if (exitCode < ReturnCode.Success)
                {
                    Debug.LogError("Analyze build failed.");
                    return emptyResult;
                }
                
                HashSet<GUID> explicitGuids = new HashSet<GUID>();
                foreach (var atf in extractData.WriteData.AssetToFiles)
                {
                    explicitGuids.Add(atf.Key);
                }

                Dictionary<GUID, List<string>> implicitGuids = new Dictionary<GUID, List<string>>();
                foreach (var fto in extractData.WriteData.FileToObjects)
                {
                    foreach (Build.Content.ObjectIdentifier g in fto.Value)
                    {
                        if (!explicitGuids.Contains(g.guid))
                        {
                            if (!implicitGuids.ContainsKey(g.guid))
                            {
                                implicitGuids.Add(g.guid, new List<string>());
                            }
                            implicitGuids[g.guid].Add(fto.Key);
                        }
                    }
                }

                //dictionary<group, dictionary<bundle, implicit assets >>
                Dictionary<string, Dictionary<string, List<string>>> allIssues = new Dictionary<string, Dictionary<string, List<string>>>();
                foreach (var g in implicitGuids)
                {
                    if (g.Value.Count > 1) //it's duplicated...
                    {
                        var path = AssetDatabase.GUIDToAssetPath(g.Key.ToString());
                        if(!AddressableAssetUtility.IsPathValidForEntry(path) || 
                            path.ToLower().Contains("/resources/") || 
                            path.ToLower().StartsWith("resources/"))
                            continue;

                        foreach (var file in g.Value)
                        {
                            var bun = extractData.WriteData.FileToBundle[file];
                            AddressableAssetGroup group;
                            if (aaContext.m_bundleToAssetGroup.TryGetValue(bun, out group))
                            {
                                Dictionary<string, List<string>> groupData;
                                if (!allIssues.TryGetValue(group.Name, out groupData))
                                {
                                    groupData = new Dictionary<string, List<string>>();
                                    allIssues.Add(group.Name, groupData);
                                }

                                List<string> assets;
                                if (!groupData.TryGetValue(bun, out assets))
                                {
                                    assets = new List<string>();
                                    groupData.Add(bun, assets);
                                }
                                assets.Add(path);

                                m_ImplicitAssets.Add(g.Key);
                            }
                        }
                    }
                }

                List<AnalyzeResult> result = new List<AnalyzeResult>();
                foreach (var group in allIssues)
                {
                    foreach (var bundle in group.Value)
                    {
                        foreach (var item in bundle.Value)
                        {
                            var issueName = name + AnalyzeRule.kDelimiter + group.Key + AnalyzeRule.kDelimiter + bundle.Key + AnalyzeRule.kDelimiter + item;
                            result.Add(new AnalyzeResult(issueName, MessageType.Warning));
                        }
                    }
                }

                if (result.Count > 0)
                    return result;
            }
            return emptyResult;
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

            return buildTasks;
        }

        internal override void FixIssues(AddressableAssetSettings settings)
        {
            if (m_ImplicitAssets == null)
                DoFakeBuild(settings);

            if (m_ImplicitAssets.Count == 0)
                return;

            var group = settings.CreateGroup("Duplicate Asset Isolation", false, false, false);
            group.AddSchema<BundledAssetGroupSchema>();
            foreach (var asset in m_ImplicitAssets)
            {
                settings.CreateOrMoveEntry(asset.ToString(), group);
            }
        }

        internal override void ClearAnalysis()
        {
            m_ImplicitAssets = null;
        }

    }

}