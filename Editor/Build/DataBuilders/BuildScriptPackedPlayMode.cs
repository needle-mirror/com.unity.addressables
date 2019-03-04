using System;
using System.IO;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace UnityEditor.AddressableAssets.Build.DataBuilders
{
    [CreateAssetMenu(fileName = "BuildScriptPackedPlayMode.asset", menuName = "Addressable Assets/Data Builders/Packed Play Mode")]
    public class BuildScriptPackedPlayMode : BuildScriptBase
    {
        public override string Name
        {
            get
            {
                return "Packed Play Mode";
            }
        }

        public override bool CanBuildData<T>()
        {
            return typeof(T) == typeof(AddressablesPlayModeBuildResult);
        }

        public override T BuildData<T>(IDataBuilderContext context)
        {
            var timer = new System.Diagnostics.Stopwatch();
            timer.Start();
            var settingsPath = Addressables.BuildPath + "/settings.json";
            if (!File.Exists(settingsPath))
            {
                IDataBuilderResult resE = new AddressablesPlayModeBuildResult() { Error = "Player content must be built before entering play mode with packed data.  This can be done from the Addressable Assets window in the Build->Build Player Content menu command." };
                return (T)resE;
            }
            //TODO: detect if the data that does exist is out of date..
            var runtimeSettingsPath = "{UnityEngine.AddressableAssets.Addressables.RuntimePath}/settings.json";
            Debug.LogFormat("Settings runtime path in PlayerPrefs to {0}", runtimeSettingsPath);
            PlayerPrefs.SetString(Addressables.kAddressablesRuntimeDataPath, runtimeSettingsPath);
            IDataBuilderResult res = new AddressablesPlayModeBuildResult() { OutputPath = settingsPath, Duration = timer.Elapsed.TotalSeconds };
            return (T)res;
        }
    }
}