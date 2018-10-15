using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement;

namespace UnityEditor.AddressableAssets.GraphBuild
{
    [AttributeUsage(AttributeTargets.Field)]
    internal class InjectInputAttribute : Attribute { }

    internal interface IBuildNodeProcessor
    {
        object Evaluate(BuildNode node, IList<object> inputs, IDataBuilderContext context);
    }

    internal abstract class BuildNodeProcessorBase : IBuildNodeProcessor
    {
        public abstract object Evaluate(BuildNode node, IList<object> inputs, IDataBuilderContext context);
    }
    
    internal class BuildPackedPlayModeData : BuildNodeProcessorBase
    {
        public override object Evaluate(BuildNode node, IList<object> inputs, IDataBuilderContext context)
        {
            return new BuildScriptPackedMode().BuildData<AddressablesPlayModeBuildResult>(context);
        }
    }

    internal class  BuildPackedPlayerData : BuildNodeProcessorBase
    {
        public override object Evaluate(BuildNode node, IList<object> inputs, IDataBuilderContext context)
        {
            return new BuildScriptPackedMode().BuildData<AddressablesPlayerBuildResult>(context);
        }
    }
}