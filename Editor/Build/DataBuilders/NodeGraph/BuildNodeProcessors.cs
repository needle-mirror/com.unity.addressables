using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor.AddressableAssets.GraphBuild
{
    [AttributeUsage(AttributeTargets.Field)]
    class InjectInputAttribute : Attribute { }

    interface IBuildNodeProcessor
    {
        object Evaluate(BuildNode node, IList<object> inputs, IDataBuilderContext context);
    }

    abstract class BuildNodeProcessorBase : IBuildNodeProcessor
    {
        public abstract object Evaluate(BuildNode node, IList<object> inputs, IDataBuilderContext context);
    }

    class BuildPackedPlayModeData : BuildNodeProcessorBase
    {
        public override object Evaluate(BuildNode node, IList<object> inputs, IDataBuilderContext context)
        {
            return ScriptableObject.CreateInstance<BuildScriptPackedMode>().BuildData<AddressablesPlayModeBuildResult>(context);
        }
    }

    class  BuildPackedPlayerData : BuildNodeProcessorBase
    {
        public override object Evaluate(BuildNode node, IList<object> inputs, IDataBuilderContext context)
        {
            return ScriptableObject.CreateInstance < BuildScriptPackedMode>().BuildData<AddressablesPlayerBuildResult>(context);
        }
    }
}