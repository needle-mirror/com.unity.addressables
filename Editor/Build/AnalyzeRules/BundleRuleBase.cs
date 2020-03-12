using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.AddressableAssets.Build.BuildPipelineTasks;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Build.Content;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEditor.Build.Pipeline.Tasks;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AddressableAssets.Initialization;
using UnityEngine.AddressableAssets.ResourceLocators;

namespace UnityEditor.AddressableAssets.Build.AnalyzeRules
{
    class BundleRuleBase : AnalyzeRule
    {
        [NonSerialized]
        internal List<GUID> m_AddressableAssets = new List<GUID>();
        [NonSerialized]
        internal Dictionary<string, List<GUID>> m_ResourcesToDependencies = new Dictionary<string, List<GUID>>();
        [NonSerialized]
        internal readonly List<ContentCatalogDataEntry> m_Locations = new List<ContentCatalogDataEntry>();
        [NonSerialized]
        internal readonly List<AssetBundleBuild> m_AllBundleInputDefs = new List<AssetBundleBuild>();
        [NonSerialized]
        internal readonly Dictionary<string, string> m_BundleToAssetGroup = new Dictionary<string, string>();
        [NonSerialized]
        internal ExtractDataTask m_ExtractData = new ExtractDataTask();

        internal IList<IBuildTask> RuntimeDataBuildTasks(string builtinShaderBundleName)
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
            buildTasks.Add(new CreateBuiltInShadersBundle(builtinShaderBundleName));

            // Packing
            buildTasks.Add(new GenerateBundlePacking());
            buildTasks.Add(new UpdateBundleObjectLayout());

            buildTasks.Add(new GenerateBundleCommands());
            buildTasks.Add(new GenerateSubAssetPathMaps());
            buildTasks.Add(new GenerateBundleMaps());

            return buildTasks;
        }

        internal AddressableAssetsBuildContext GetBuildContext(AddressableAssetSettings settings)
        {
            ResourceManagerRuntimeData runtimeData = new ResourceManagerRuntimeData();
            runtimeData.LogResourceManagerExceptions = settings.buildSettings.LogResourceManagerExceptions;

            var aaContext = new AddressableAssetsBuildContext
            {
                settings = settings,
                runtimeData = runtimeData,
                bundleToAssetGroup = m_BundleToAssetGroup,
                locations = m_Locations,
                providerTypes = new HashSet<Type>()
            };
            return aaContext;
        }
        protected bool IsValidPath(string path)
        {
            return AddressableAssetUtility.IsPathValidForEntry(path) &&
                   !path.ToLower().Contains("/resources/") &&
                   !path.ToLower().StartsWith("resources/");
        }

        internal ReturnCode RefreshBuild(AddressableAssetsBuildContext buildContext)
        {
            var settings = buildContext.settings;
            var context = new AddressablesDataBuilderInput(settings);

            var buildTarget = context.Target;
            var buildTargetGroup = context.TargetGroup;
            var buildParams = new AddressableAssetsBundleBuildParameters(settings, m_BundleToAssetGroup, buildTarget,
                buildTargetGroup, settings.buildSettings.bundleBuildPath);
            var builtinShaderBundleName =
                settings.DefaultGroup.Name.ToLower().Replace(" ", "").Replace('\\', '/').Replace("//", "/") +
                "_unitybuiltinshaders.bundle";
            var buildTasks = RuntimeDataBuildTasks(builtinShaderBundleName);
            buildTasks.Add(m_ExtractData);

            IBundleBuildResults buildResults;
            var exitCode = ContentPipeline.BuildAssetBundles(buildParams, new BundleBuildContent(m_AllBundleInputDefs),
                out buildResults, buildTasks, buildContext);

            using (var progressTracker = new UnityEditor.Build.Pipeline.Utilities.ProgressTracker())
            {
                progressTracker.UpdateTask("Generating Addressables Locations");
                GenerateLocationListsTask.Run(buildContext, m_ExtractData.WriteData);
            }

            return exitCode;
        }

        internal List<GUID> GetAllBundleDependencies()
        {
            var explicitGuids = m_ExtractData.WriteData.AssetToFiles.Keys;
            var implicitGuids = GetImplicitGuidToFilesMap().Keys;
            var allBundleGuids = explicitGuids.Union(implicitGuids);

            return allBundleGuids.ToList();
        }

        internal void IntersectResourcesDepedenciesWithBundleDependencies(List<GUID> bundleDependencyGuids)
        {
            foreach (var key in m_ResourcesToDependencies.Keys)
            {
                var bundleDependencies = bundleDependencyGuids.Intersect(m_ResourcesToDependencies[key]).ToList();

                m_ResourcesToDependencies[key].Clear();
                m_ResourcesToDependencies[key].AddRange(bundleDependencies);
            }
        }

        internal virtual void BuiltInResourcesToDependenciesMap(string[] resourcePaths)
        {
            foreach (string path in resourcePaths)
            {
                string[] dependencies = AssetDatabase.GetDependencies(path);

                if (!m_ResourcesToDependencies.ContainsKey(path))
                    m_ResourcesToDependencies.Add(path, new List<GUID>());

                m_ResourcesToDependencies[path].AddRange(from dependency in dependencies
                    select new GUID(AssetDatabase.AssetPathToGUID(dependency)));
            }
        }

        internal void ConvertBundleNamesToGroupNames(AddressableAssetsBuildContext buildContext)
        {
            Dictionary<string, string> bundleNamesToUpdate = new Dictionary<string, string>();

            foreach (var assetGroup in buildContext.settings.groups)
            {
                if (assetGroup == null)
                    continue;

                List<string> bundles;
                if (buildContext.assetGroupToBundles.TryGetValue(assetGroup, out bundles))
                {
                    foreach (string bundle in bundles)
                    {
                        var keys = m_ExtractData.WriteData.FileToBundle.Keys.Where(key => m_ExtractData.WriteData.FileToBundle[key] == bundle);
                        foreach (string key in keys)
                            bundleNamesToUpdate.Add(key, assetGroup.Name);
                    }
                }
            }

            foreach (string key in bundleNamesToUpdate.Keys)
            {
                var bundle = m_ExtractData.WriteData.FileToBundle[key];
                var inputDef = m_AllBundleInputDefs.FirstOrDefault(b => b.assetBundleName == bundle);
                int index = m_AllBundleInputDefs.IndexOf(inputDef);
                if (index >= 0)
                {
                    inputDef.assetBundleName = ConvertBundleName(inputDef.assetBundleName, bundleNamesToUpdate[key]);
                    m_AllBundleInputDefs[index] = inputDef;
                    m_ExtractData.WriteData.FileToBundle[key] = inputDef.assetBundleName;
                }
            }
        }

        internal void CalculateInputDefinitions(AddressableAssetSettings settings)
        {
            foreach (AddressableAssetGroup group in settings.groups)
            {
                if (group == null)
                    continue;

                if (group.HasSchema<BundledAssetGroupSchema>())
                {
                    var schema = group.GetSchema<BundledAssetGroupSchema>();
                    List<AssetBundleBuild> bundleInputDefinitions = new List<AssetBundleBuild>();
                    BuildScriptPackedMode.PrepGroupBundlePacking(group, bundleInputDefinitions, schema.BundleMode);

                    for (int i = 0; i < bundleInputDefinitions.Count; i++)
                    {
                        if (m_BundleToAssetGroup.ContainsKey(bundleInputDefinitions[i].assetBundleName))
                            bundleInputDefinitions[i] = CreateUniqueBundle(bundleInputDefinitions[i]);

                        m_BundleToAssetGroup.Add(bundleInputDefinitions[i].assetBundleName, schema.Group.Guid);
                    }

                    m_AllBundleInputDefs.AddRange(bundleInputDefinitions);
                }
            }
        }

        internal AssetBundleBuild CreateUniqueBundle(AssetBundleBuild bid)
        {
            int count = 1;
            var newName = bid.assetBundleName;
            while (m_BundleToAssetGroup.ContainsKey(newName) && count < 1000)
                newName = bid.assetBundleName.Replace(".bundle", string.Format("{0}.bundle", count++));
            return new AssetBundleBuild
            {
                assetBundleName = newName, addressableNames = bid.addressableNames,
                assetBundleVariant = bid.assetBundleVariant, assetNames = bid.assetNames
            };
        }

        internal List<GUID> GetImplicitGuidsForBundle(string fileName)
        {
            List<GUID> guids = (from id in m_ExtractData.WriteData.FileToObjects[fileName]
                                where !m_ExtractData.WriteData.AssetToFiles.Keys.Contains(id.guid)
                                select id.guid).ToList();
            return guids;
        }

        internal Dictionary<GUID, List<string>> GetImplicitGuidToFilesMap()
        {
            Dictionary<GUID, List<string>> implicitGuids = new Dictionary<GUID, List<string>>();
            IEnumerable<KeyValuePair<ObjectIdentifier, string>> validImplicitGuids =
                from fileToObject in m_ExtractData.WriteData.FileToObjects
                from objectId in fileToObject.Value
                where !m_ExtractData.WriteData.AssetToFiles.Keys.Contains(objectId.guid)
                select new KeyValuePair<ObjectIdentifier, string>(objectId, fileToObject.Key);

            //Build our Dictionary from our list of valid implicit guids (guids not already in explicit guids)
            foreach (var objectIdToFile in validImplicitGuids)
            {
                if (!implicitGuids.ContainsKey(objectIdToFile.Key.guid))
                    implicitGuids.Add(objectIdToFile.Key.guid, new List<string>());
                implicitGuids[objectIdToFile.Key.guid].Add(objectIdToFile.Value);
            }

            return implicitGuids;
        }

        internal List<AnalyzeResult> CalculateBuiltInResourceDependenciesToBundleDependecies(AddressableAssetSettings settings, string[] builtInResourcesPaths)
        {
            List<AnalyzeResult> results = new List<AnalyzeResult>();

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.LogError("Cannot run Analyze with unsaved scenes");
                results.Add(new AnalyzeResult{resultName = ruleName + "Cannot run Analyze with unsaved scenes"});
                return results;
            }

            m_AddressableAssets = (from aaGroup in settings.groups
                                   where aaGroup != null
                                   from entry in aaGroup.entries
                                   select new GUID(entry.guid)).ToList();

            
            BuiltInResourcesToDependenciesMap(builtInResourcesPaths);
            CalculateInputDefinitions(settings);

            var context = GetBuildContext(settings);
            ReturnCode exitCode = RefreshBuild(context);
            if (exitCode < ReturnCode.Success)
            {
                Debug.LogError("Analyze build failed. " + exitCode);
                results.Add(new AnalyzeResult{resultName = ruleName + "Analyze build failed. " + exitCode});
                return results;
            }

            IntersectResourcesDepedenciesWithBundleDependencies(GetAllBundleDependencies());

            ConvertBundleNamesToGroupNames(context);

            results = (from resource in m_ResourcesToDependencies.Keys
                       from dependency in m_ResourcesToDependencies[resource]

                       let assetPath = AssetDatabase.GUIDToAssetPath(dependency.ToString())
                       let files = m_ExtractData.WriteData.FileToObjects.Keys

                       from file in files
                       where m_ExtractData.WriteData.FileToObjects[file].Any(oid => oid.guid == dependency)
                       where m_ExtractData.WriteData.FileToBundle.ContainsKey(file)
                       let bundle = m_ExtractData.WriteData.FileToBundle[file]
                       
                       select new AnalyzeResult { resultName =
                           resource + kDelimiter +
                           bundle + kDelimiter +
                           assetPath,
                           severity = MessageType.Warning }).ToList();

            if (results.Count == 0)
                results.Add(new AnalyzeResult{resultName = ruleName + " - No issues found."});

            return results;
        }

        protected string ConvertBundleName(string bundleName, string groupName)
        {
            string[] bundleNameSegments = bundleName.Split('_');
            bundleNameSegments[0] = groupName.Replace(" ", "").ToLower();
            return string.Join("_", bundleNameSegments);
        }

        public override void ClearAnalysis()
        {
            m_Locations.Clear();
            m_AddressableAssets.Clear();
            m_AllBundleInputDefs.Clear();
            m_BundleToAssetGroup.Clear();
            m_ResourcesToDependencies.Clear();
            m_ExtractData = new ExtractDataTask();

            base.ClearAnalysis();
        }
    }
}
