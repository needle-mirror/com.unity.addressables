using System.Collections.Generic;
using UnityEngine.ResourceManagement;

namespace UnityEngine.AddressableAssets
{
    public class LegacyResourcesLocator : IResourceLocator
    {
        public bool Locate(object key, out IList<IResourceLocation> locations)
        {
            locations = null;
            var strKey = key as string;
            if (strKey == null)
                return false;
            locations = new List<IResourceLocation>();
            locations.Add(new LegacyResourcesLocation(strKey));
            return true;
        }
    }
}
