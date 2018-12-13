using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement;

namespace UnityEditor.AddressableAssets
{
    [CreateAssetMenu(fileName = "BuildScriptFast.asset", menuName = "Addressable Assets/Data Builders/Fast Mode")]
    class BuildScriptFastMode : BuildScriptBase
    {
        public override string Name
        {
            get
            {
                return "Fast Mode";
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

        string m_PathFormat = "{0}Library/com.unity.addressables/{1}_BuildScriptFastMode.json";
        public override T BuildData<T>(IDataBuilderContext context)
        {
            var timer = new Stopwatch();
            timer.Start();
            var aaSettings = context.GetValue<AddressableAssetSettings>(AddressablesBuildDataBuilderContext.BuildScriptContextConstants.kAddressableAssetSettings);

            List<EditorBuildSettingsScene> scenesToAdd = new List<EditorBuildSettingsScene>();

            //gather entries
            var locations = new List<ContentCatalogDataEntry>();
            bool needsLegacyProvider = false;
            foreach (var assetGroup in aaSettings.groups)
            {
                if (assetGroup.HasSchema<PlayerDataGroupSchema>())
                {
                    needsLegacyProvider = CreateLocationsForPlayerData(assetGroup, locations);
                    continue;
                }

                var allEntries = new List<AddressableAssetEntry>();
                foreach (var a in assetGroup.entries)
                    a.GatherAllAssets(allEntries, true, true);

                foreach (var a in allEntries)
                {
                    locations.Add(new ContentCatalogDataEntry(a.AssetPath, typeof(AssetDatabaseProvider).FullName, a.CreateKeyList()));
                    if (a.IsScene)
                        scenesToAdd.Add(new EditorBuildSettingsScene(new GUID(a.guid), true));
                }
            }

            //save catalog
            WriteFile(string.Format(m_PathFormat, "", "catalog"), JsonUtility.ToJson(new ContentCatalogData(locations)));

            //create runtime data
            var runtimeData = new ResourceManagerRuntimeData();
            runtimeData.LogResourceManagerExceptions = aaSettings.buildSettings.LogResourceManagerExceptions;
            runtimeData.ProfileEvents = ProjectConfigData.postProfilerEvents;
            runtimeData.CatalogLocations.Add(new ResourceLocationData(new[] { "catalogs" }, string.Format(m_PathFormat, "file://{UnityEngine.Application.dataPath}/../", "catalog"), typeof(JsonAssetProvider)));
            if (needsLegacyProvider)
                runtimeData.ResourceProviderData.Add(ObjectInitializationData.CreateSerializedInitializationData(typeof(LegacyResourcesProvider)));
            runtimeData.ResourceProviderData.Add(ObjectInitializationData.CreateSerializedInitializationData<AssetDatabaseProvider>());
            runtimeData.InstanceProviderData = ObjectInitializationData.CreateSerializedInitializationData<InstanceProvider>();
            runtimeData.SceneProviderData = ObjectInitializationData.CreateSerializedInitializationData<SceneProvider>();
            foreach (var io in aaSettings.InitializationObjects)
            {
                if(io is IObjectInitializationDataProvider )
                    runtimeData.InitializationObjects.Add((io as IObjectInitializationDataProvider).CreateObjectInitializationData());
            }

            WriteFile(string.Format(m_PathFormat, "", "settings"), JsonUtility.ToJson(runtimeData));

            //inform runtime of the init data path
            PlayerPrefs.SetString(Addressables.kAddressablesRuntimeDataPath, string.Format(m_PathFormat, "file://{UnityEngine.Application.dataPath}/../", "settings"));
            IDataBuilderResult res = new AddressablesPlayModeBuildResult { ScenesToAdd = scenesToAdd, Duration = timer.Elapsed.TotalSeconds, LocationCount = locations.Count };
            return (T)res;
        }
    }
}