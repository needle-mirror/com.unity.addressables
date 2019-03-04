using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.Initialization;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.AddressableAssets.ResourceProviders;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.ResourceManagement.Util;

namespace UnityEditor.AddressableAssets.Build.DataBuilders
{
    /// <summary>
    /// Build script used for fast iteration in the editor.
    /// </summary>
    [CreateAssetMenu(fileName = "BuildScriptFast.asset", menuName = "Addressable Assets/Data Builders/Fast Mode")]
    public class BuildScriptFastMode : BuildScriptBase
    {
        public override string Name
        {
            get
            {
                return "Fast Mode";
            }
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

        string m_PathFormat = "{0}Library/com.unity.addressables/{1}_BuildScriptFastMode.json";
        public override T BuildData<T>(IDataBuilderContext context)
        {
            var timer = new Stopwatch();
            timer.Start();
            var aaSettings = context.GetValue<AddressableAssetSettings>(AddressablesBuildDataBuilderContext.BuildScriptContextConstants.kAddressableAssetSettings);
            m_PathFormat = context.GetValue("PathFormat", m_PathFormat);

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
                    locations.Add(new ContentCatalogDataEntry(a.GetAssetLoadPath(true), typeof(AssetDatabaseProvider).FullName, a.CreateKeyList()));
                    if (a.IsScene)
                        scenesToAdd.Add(new EditorBuildSettingsScene(new GUID(a.guid), true));
                }
            }


            //create runtime data
            var runtimeData = new ResourceManagerRuntimeData();
            runtimeData.BuildTarget = context.GetValue<BuildTarget>(AddressablesBuildDataBuilderContext.BuildScriptContextConstants.kBuildTarget).ToString();
            runtimeData.LogResourceManagerExceptions = aaSettings.buildSettings.LogResourceManagerExceptions;
            runtimeData.ProfileEvents = ProjectConfigData.postProfilerEvents;
            runtimeData.CatalogLocations.Add(new ResourceLocationData(new[] { InitializationOperation.CatalogAddress }, string.Format(m_PathFormat, "file://{UnityEngine.Application.dataPath}/../", "catalog"), typeof(ContentCatalogProvider)));
            foreach (var io in aaSettings.InitializationObjects)
            {
                if(io is IObjectInitializationDataProvider )
                    runtimeData.InitializationObjects.Add((io as IObjectInitializationDataProvider).CreateObjectInitializationData());
            }
            var settingsPath = string.Format(m_PathFormat, "", "settings");
            WriteFile(settingsPath, JsonUtility.ToJson(runtimeData));

            //save catalog
            var catalogData = new ContentCatalogData(locations);
            if (needsLegacyProvider)
                catalogData.ResourceProviderData.Add(ObjectInitializationData.CreateSerializedInitializationData(typeof(LegacyResourcesProvider)));
            catalogData.ResourceProviderData.Add(ObjectInitializationData.CreateSerializedInitializationData<AssetDatabaseProvider>());
            catalogData.InstanceProviderData = ObjectInitializationData.CreateSerializedInitializationData<InstanceProvider>();
            catalogData.SceneProviderData = ObjectInitializationData.CreateSerializedInitializationData<SceneProvider>();
            WriteFile(string.Format(m_PathFormat, "", "catalog"), JsonUtility.ToJson(catalogData));


            //inform runtime of the init data path
            var runtimeSettingsPath = string.Format(m_PathFormat, "file://{UnityEngine.Application.dataPath}/../", "settings");
            UnityEngine.Debug.LogFormat("Settings runtime path in PlayerPrefs to {0}", runtimeSettingsPath);
            PlayerPrefs.SetString(Addressables.kAddressablesRuntimeDataPath, runtimeSettingsPath);
            IDataBuilderResult res = new AddressablesPlayModeBuildResult { OutputPath = settingsPath, ScenesToAdd = scenesToAdd, Duration = timer.Elapsed.TotalSeconds, LocationCount = locations.Count };

            return (T)res;
        }
    }
}