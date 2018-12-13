using System;
using System.Collections.Generic;
using UnityEngine.ResourceManagement;

namespace UnityEngine.AddressableAssets
{
    public interface IResourceLocator
    {
        bool Locate(object key, out IList<IResourceLocation> locations);
    }
}
