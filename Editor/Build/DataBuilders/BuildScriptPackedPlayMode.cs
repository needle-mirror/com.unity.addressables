using System;
using System.IO;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace UnityEditor.AddressableAssets
{
    [CreateAssetMenu(fileName = "BuildScriptPackedPlayMode.asset", menuName = "Addressable Assets/Data Builders/Packed Play Mode")]
    class BuildScriptPackedPlayMode : BuildScriptBase
    {
        public override string Name
        {
            get
            {
                return "Packed Play Mode";
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

        public override T BuildData<T>(IDataBuilderContext context)
        {
            var timer = new System.Diagnostics.Stopwatch();
            timer.Start();
            if (!File.Exists(Addressables.BuildPath + "/settings.json"))
            {
                IDataBuilderResult resE = new AddressablesPlayModeBuildResult() { Error = "Player content must be built before entering play mode with packed data.  This can be done from the Addressable Assets window in the Build->Build Player Content menu command." };
                return (T)resE;
            }
            //TODO: detect if the data that does exist is out of date..
            PlayerPrefs.SetString(Addressables.kAddressablesRuntimeDataPath, "{UnityEngine.AddressableAssets.Addressables.RuntimePath}/settings.json");
            IDataBuilderResult res = new AddressablesPlayModeBuildResult() {Duration = timer.Elapsed.TotalSeconds };
            return (T)res;
        }
    }
}