using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Build.BuildPipelineTasks;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.ResourceProviders;
using static UnityEditor.AddressableAssets.Build.ContentUpdateScript;

/// <summary>
/// RevertUnchangedAssetsToPreviousAssetState uses the asset state from the previous build to determine if any assets
/// need to use their previous settings or use the newly build data.
/// </summary>
internal class RevertUnchangedAssetsToPreviousAssetState
{
    internal struct AssetEntryRevertOperation
    {
        public CachedAssetState PreviousAssetState;
        public AddressableAssetEntry AssetEntry;
        public ContentCatalogDataEntry BundleCatalogEntry;
        public string CurrentBuildPath;
        public string PreviousBuildPath;
    }

    internal static ReturnCode Run(IAddressableAssetsBuildContext aaBuildContext, ContentUpdateContext updateContext)
    {
        var aaContext = aaBuildContext as AddressableAssetsBuildContext;
        var groups = aaContext.settings.groups.Where(group => group != null && group.HasSchema<BundledAssetGroupSchema>());

        foreach (var assetGroup in groups)
        {
            List<AssetEntryRevertOperation> operations = DetermineRequiredAssetEntryUpdates(assetGroup, updateContext);
            ApplyAssetEntryUpdates(operations, GenerateLocationListsTask.GetBundleProviderName(assetGroup), aaContext.locations, updateContext);
        }

        return ReturnCode.Success;
    }

    internal static List<AssetEntryRevertOperation> DetermineRequiredAssetEntryUpdates(AddressableAssetGroup group, ContentUpdateScript.ContentUpdateContext contentUpdateContext)
    {
        if (!group.HasSchema<BundledAssetGroupSchema>())
            return new List<AssetEntryRevertOperation>();

        bool groupIsStaticContentGroup = group.HasSchema<ContentUpdateGroupSchema>() && group.GetSchema<ContentUpdateGroupSchema>().StaticContent;
        List<AssetEntryRevertOperation> operations = new List<AssetEntryRevertOperation>();

        foreach (AddressableAssetEntry entry in group.entries)
        {
            GUID guid = new GUID(entry.guid);
            if (!contentUpdateContext.WriteData.AssetToFiles.ContainsKey(guid))
                continue;

            string file = contentUpdateContext.WriteData.AssetToFiles[guid][0];
            string fullInternalBundleName = contentUpdateContext.WriteData.FileToBundle[file];
            string finalBundleWritePath = contentUpdateContext.BundleToInternalBundleIdMap[fullInternalBundleName];

            //Ensure we can get the catalog entry for the bundle we're looking to replace
            if (!contentUpdateContext.IdToCatalogDataEntryMap.TryGetValue(finalBundleWritePath, out ContentCatalogDataEntry catalogBundleEntry))
                continue;

            //If new entries are added post initial build this will ensure that those new entries have their bundleFileId for SaveContentState
            entry.BundleFileId = catalogBundleEntry.InternalId;

            //If we have no cached state no reason to proceed.  This is new to the build.
            if (!contentUpdateContext.GuidToPreviousAssetStateMap.TryGetValue(entry.guid, out CachedAssetState previousAssetState))
                continue;

            //If the parent group is different we don't want to revert it to its previous state
            if (entry.parentGroup.Guid != previousAssetState.groupGuid)
                continue;

            //If the asset hash has changed and the group is not a static content update group we don't want to revert it to its previous state
            if (AssetDatabase.GetAssetDependencyHash(entry.AssetPath) != previousAssetState.asset.hash && !groupIsStaticContentGroup)
                continue;

            //If the previous asset state has the same bundle file id as the current build we don't want to revert it to its previous state
            if (catalogBundleEntry.InternalId == previousAssetState.bundleFileId)
                continue;

            var schema = group.GetSchema<BundledAssetGroupSchema>();
            string loadPath = schema.LoadPath.GetValue(group.Settings);
            string buildPath = schema.BuildPath.GetValue(group.Settings);

            //Need to check and make sure our cached version exists
            if (string.IsNullOrEmpty(previousAssetState.bundleFileId))
            {
                //Logging this as an error because a CachedAssetState without a set bundleFileId is indicative of a significant issue with the build script.
                Addressables.LogError($"CachedAssetState found for {entry.AssetPath} but the bundleFileId was never set on the previous build.");
                continue;
            }

            string previousBundlePath = previousAssetState.bundleFileId?.Replace(loadPath, buildPath);

            if (!File.Exists(previousBundlePath))
            {
                //Logging this as a warning because users may choose to delete their bundles on disk which will trigger this state.
                Addressables.LogWarning($"CachedAssetState found for {entry.AssetPath} but the previous bundle at {previousBundlePath} cannot be found. " +
                                        $"The modified assets will not be able to use the previously built bundle which will result in new bundles being created " +
                                        $"for these static content groups.  This will point the Content Catalog to local bundles that do not exist on currently " +
                                        $"deployed versions of an application.");
                continue;
            }

            string builtBundlePath = contentUpdateContext.BundleToInternalBundleIdMap[fullInternalBundleName].Replace(loadPath, buildPath);

            AssetEntryRevertOperation operation = new AssetEntryRevertOperation()
            {
                BundleCatalogEntry = catalogBundleEntry,
                AssetEntry = entry,
                CurrentBuildPath = builtBundlePath,
                PreviousAssetState = previousAssetState,
                PreviousBuildPath = previousBundlePath
            };

            operations.Add(operation);
        }
        return operations;
    }

    internal static void ApplyAssetEntryUpdates(
        List<AssetEntryRevertOperation> operations,
        string bundleProviderName,
        List<ContentCatalogDataEntry> locations,
        ContentUpdateContext contentUpdateContext)
    {
        foreach (AssetEntryRevertOperation operation in operations)
        {
            //Check that we can replace the entry in the file registry
            //before continuing.  Past this point destructive actions are taken.
            if (contentUpdateContext.Registry.ReplaceBundleEntry(
                Path.GetFileNameWithoutExtension(operation.PreviousBuildPath), 
                operation.PreviousAssetState.bundleFileId))
            {
                File.Delete(operation.CurrentBuildPath);
                operation.BundleCatalogEntry.InternalId = operation.PreviousAssetState.bundleFileId;
                operation.BundleCatalogEntry.Data = operation.PreviousAssetState.data;

                //If the entry has dependencies, we need to update those to point to their respective cached versions as well.
                if (operation.PreviousAssetState.dependencies.Length > 0)
                {
                    operation.BundleCatalogEntry.Dependencies.Clear();
                    foreach (AssetState state in operation.PreviousAssetState.dependencies)
                    {
                        var cachedDependencyState = contentUpdateContext.GuidToPreviousAssetStateMap[state.guid.ToString()];
                        if (cachedDependencyState != null &&
                            !string.IsNullOrEmpty(cachedDependencyState.bundleFileId))
                        {
                            string modifiedKey = cachedDependencyState.bundleFileId + operation.AssetEntry.GetHashCode();
                            locations.Add(new ContentCatalogDataEntry(
                                typeof(IAssetBundleResource),
                                cachedDependencyState.bundleFileId,
                                bundleProviderName,
                                new List<object>() { modifiedKey }));

                            contentUpdateContext.Registry.AddFile(cachedDependencyState.bundleFileId);
                            if (!operation.BundleCatalogEntry.Dependencies.Contains(modifiedKey))
                                operation.BundleCatalogEntry.Dependencies.Add(modifiedKey);

                            contentUpdateContext.PreviousAssetStateCarryOver.Add(cachedDependencyState);
                        }
                    }
                }
                //sync the internal ids of the catalog entry and asset entry to the cached state
                operation.BundleCatalogEntry.InternalId = operation.AssetEntry.BundleFileId = operation.PreviousAssetState.bundleFileId;
            }
        }
    }
}
