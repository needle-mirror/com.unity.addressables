using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.ResourceManagement.Util;
using UnityEngine.U2D;

namespace UnityEngine.AddressableAssets
{
    internal class DynamicResourceLocator : IResourceLocator
    {
        AddressablesImpl m_Addressables;
        public string LocatorId => nameof(DynamicResourceLocator);
        public virtual IEnumerable<object> Keys => new object[0];

        public DynamicResourceLocator(AddressablesImpl addr)
        {
            m_Addressables = addr;
        }

        public bool Locate(object key, Type type, out IList<IResourceLocation> locations)
        {
            locations = null;
            if (ResourceManagerConfig.ExtractKeyAndSubKey(key, out string mainKey, out string subKey))
            {
                if (!m_Addressables.GetResourceLocations(mainKey, type, out IList<IResourceLocation> locs))
                {
                    if (type == typeof(Sprite))
                        m_Addressables.GetResourceLocations(mainKey, typeof(SpriteAtlas), out locs);
                }

                if (locs != null && locs.Count > 0)
                {
                    locations = new List<IResourceLocation>(locs.Count);
                    foreach (var l in locs)
                        CreateDynamicLocations(type, locations, key as string, subKey, l);
                    return true;
                }
            }
            return false;
        }

        void CreateDynamicLocations(Type type, IList<IResourceLocation> locations, string locName, string subKey, IResourceLocation mainLoc)
        {
            if (type == typeof(Sprite) && mainLoc.ResourceType == typeof(U2D.SpriteAtlas))
            {
                locations.Add(new ResourceLocationBase(locName, $"{mainLoc.InternalId}[{subKey}]", typeof(AtlasSpriteProvider).FullName, type, new IResourceLocation[] { mainLoc }));
            }
            else
            {
                if (mainLoc.HasDependencies)
                    locations.Add(new ResourceLocationBase(locName, $"{mainLoc.InternalId}[{subKey}]", mainLoc.ProviderId, type, mainLoc.Dependencies.ToArray()));
                else
                    locations.Add(new ResourceLocationBase(locName, $"{mainLoc.InternalId}[{subKey}]", mainLoc.ProviderId, type));
            }
        }
    }
}