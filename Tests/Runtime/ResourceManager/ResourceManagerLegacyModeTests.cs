using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using System.IO;


#if UNITY_EDITOR
namespace UnityEngine.ResourceManagement.Tests
{
    public class ResourceManagerTestsLegacy : ResourceManagerBaseTests
    {
        protected override string AssetPathPrefix { get { return "Resources/"; } }
        protected override IResourceLocation CreateLocationForAsset(string name, string path)
        {
            return new ResourceLocationBase(name, Path.GetFileNameWithoutExtension(path), typeof(LegacyResourcesProvider).FullName);
        }

        protected override void ProcessLocations(List<IResourceLocation> locations)
        {
            m_ResourceManager.ResourceProviders.Add(new LegacyResourcesProvider());
        }
    }
}
#endif
