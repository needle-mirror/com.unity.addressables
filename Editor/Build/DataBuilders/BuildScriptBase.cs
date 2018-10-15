using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor.AddressableAssets;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement;

namespace UnityEditor.AddressableAssets
{
    internal class BuildScriptBase : ScriptableObject, IDataBuilder
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

        public virtual IDataBuilderGUI CreateGUI(IDataBuilderContext context)
        {
            return null;
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
                    string providerId = a.IsScene ? typeof(SceneProvider).FullName : typeof(LegacyResourcesProvider).FullName;
                    locations.Add(new ContentCatalogDataEntry(a.GetAssetLoadPath(false), providerId, a.CreateKeyList()));
                    if (!a.IsScene)
                        needsLegacyProvider = true;
                }
            }
            return needsLegacyProvider;
        }

        static protected bool WriteFile(string path, string content)
        {
            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllText(path, content);
                return true;
            }
            catch (System.Exception ex)
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