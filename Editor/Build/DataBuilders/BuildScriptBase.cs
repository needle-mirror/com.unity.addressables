using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace UnityEditor.AddressableAssets.Build.DataBuilders
{
    /// <summary>
    /// Base class for build script assets
    /// </summary>
    public class BuildScriptBase : ScriptableObject, IDataBuilder
    {
        public virtual string Name
        {
            get
            {
                return "Undefined";
            }
        }

        public virtual T BuildData<T>(IDataBuilderContext context) where T : IDataBuilderResult
        {
            return default(T);
        }

        public virtual bool CanBuildData<T>() where T : IDataBuilderResult
        {
            return false;
        }

        protected bool CreateLocationsForPlayerData(AddressableAssetGroup assetGroup, List<ContentCatalogDataEntry> locations)
        {
            bool needsLegacyProvider = false;
            var playerDataSchema = assetGroup.GetSchema<PlayerDataGroupSchema>();
            if (playerDataSchema != null)
            {
                var entries = new List<AddressableAssetEntry>();
                assetGroup.GatherAllAssets(entries, true, true);
                foreach (var a in entries)
                {
                    if (!playerDataSchema.IncludeBuildSettingsScenes && a.IsInSceneList)
                        continue;
                    if (!playerDataSchema.IncludeResourcesFolders && a.IsInResources)
                        continue;
                    string providerId = a.IsScene ? typeof(SceneProvider).FullName : typeof(LegacyResourcesProvider).FullName;
                    locations.Add(new ContentCatalogDataEntry(a.GetAssetLoadPath(false), providerId, a.CreateKeyList()));
                    if (!a.IsScene)
                        needsLegacyProvider = true;
                }
            }
            return needsLegacyProvider;
        }
        protected static void DeleteFile(string path)
        {
            try
            {

                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }
        protected static bool WriteFile(string path, string content)
        {
            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllText(path, content);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return false;
            }

        }

        public virtual void ClearCachedData()
        {

        }
    }
}