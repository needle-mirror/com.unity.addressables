using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;

#if UNITY_EDITOR
namespace UnityEngine.ResourceManagement.Tests
{
    public class ResourceManagerFastModeTests : ResourceManagerBaseTests
    {
        protected override IResourceLocation CreateLocationForAsset(string name, string path)
        {
            return new ResourceLocationBase(name, path, typeof(AssetDatabaseProvider).FullName);
        }

        protected override void ProcessLocations(List<IResourceLocation> locations)
        {
            m_ResourceManager.ResourceProviders.Add(new AssetDatabaseProvider());
        }
    }
}
#endif
