using System;
using UnityEngine;
namespace UnityEditor.AddressableAssets.GraphBuild
{
    [Serializable]
    class BuildLink
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