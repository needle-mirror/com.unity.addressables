using System.Collections.Generic;

namespace UnityEngine.ResourceManagement
{
    public interface IResourceLocator
    {
        bool Locate(object key, out IList<IResourceLocation> locations);
    }
}
