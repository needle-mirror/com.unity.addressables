using System;
using System.Collections.Generic;
using UnityEngine.ResourceManagement;

namespace UnityEngine.AddressableAssets
{
    /// <summary>
    /// TODO - doc
    /// </summary>
    public class ResourceLocationMap : IResourceLocator
    {
        /// <summary>
        /// TODO - doc
        /// </summary>
        public Dictionary<object, IList<IResourceLocation>> m_locations = new Dictionary<object, IList<IResourceLocation>>();

        /// <summary>
        /// TODO - doc
        /// </summary>
        public bool Locate(object key, out IList<IResourceLocation> results)
        {
            return m_locations.TryGetValue(key, out results);
        }

        /// <summary>
        /// TODO - doc
        /// </summary>
        public void Add(object key, IResourceLocation loc)
        {
            IList<IResourceLocation> locations;
            if (!m_locations.TryGetValue(key, out locations))
                m_locations.Add(key, locations = new List<IResourceLocation>());
            locations.Add(loc);
        }
    }
}
