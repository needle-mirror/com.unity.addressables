using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace UnityEditor.AddressableAssets.GraphBuild
{
    [Serializable]
    internal class BuildLink
    {
        public Hash128 id;
        public Hash128 source;
        public PortIdentifier target;
        public override int GetHashCode()
        {
            return id.GetHashCode();
        }
    }
}