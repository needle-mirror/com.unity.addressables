using System;
using System.Collections.Generic;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace UnityEngine.ResourceManagement.ResourceLocations
{
    /// <summary>
    /// Simple class for loading via the Resources.Load API
    /// </summary>
    public struct LegacyResourcesLocation : IResourceLocation
    {
        string m_Key;
        /// <summary>
        /// Construct a new LegacyResourcesLocation with a specified key.
        /// </summary>
        /// <param name="key">The key of the location.  This should be set to the path relative to the resources folder it is contained within.</param>
        public LegacyResourcesLocation(string key) { m_Key = key; }

        /// <summary>
        /// Returns the path of the asset.
        /// </summary>
        public string Key { get { return m_Key; } }
        /// <summary>
        /// Returns the same value as Key.  
        /// </summary>
        public string InternalId { get { return m_Key; } }
        /// <summary>
        /// returns typeof(LegacyResourcesProvider).FullName.
        /// </summary>
        public string ProviderId { get { return typeof(LegacyResourcesProvider).FullName; } }
        /// <summary>
        /// This value is always null since the Resources API does not deal with dependencies.
        /// </summary>
        public IList<IResourceLocation> Dependencies { get { return null; } }
        /// <summary>
        /// This value is always <c>false</c>.
        /// </summary>
        public bool HasDependencies { get { return false; } }
        /// <summary>
        /// This value is always <c>null</c>.
        /// </summary>
        public object Data { get { return null; } }
    }
}
