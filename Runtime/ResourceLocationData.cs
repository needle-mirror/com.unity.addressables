using System;
using System.Collections.Generic;
using UnityEngine.ResourceManagement;

namespace UnityEngine.AddressableAssets
{
    /// <summary>
    /// Serializable location data.  This is used for the locations of the content catalogs.
    /// </summary>
    [Serializable]
    public class ResourceLocationData
    {
        [SerializeField]
        private string[] m_keys;
        /// <summary>
        /// The collection of keys for this location.
        /// </summary>
        public string[] Keys { get { return m_keys; } }

        [SerializeField]
        private string m_internalId;
        /// <summary>
        /// The internal id.
        /// </summary>
        public string InternalId { get { return m_internalId; } }

        [SerializeField]
        private string m_provider;
        /// <summary>
        /// The provider id.
        /// </summary>
        public string Provider { get { return m_provider; } }

        [SerializeField]
        private string[] m_dependencies;
        /// <summary>
        /// The collection of dependencies for this location.
        /// </summary>
        public string[] Dependencies { get { return m_dependencies; } }


        /// <summary>
        /// Construct a new ResourceLocationData object.
        /// </summary>
        /// <param name="keys">Array of keys for the location.  This must contain at least one item.</param>
        /// <param name="id">The internal id.</param>
        /// <param name="provider">The provider id.</param>
        /// <param name="dependencies">Optional array of dependencies.</param>
        public ResourceLocationData(string[] keys, string id, Type provider, string[] dependencies = null)
        {
            m_keys = keys;
            m_internalId = id;
            m_provider = provider == null ? "" : provider.FullName;
            m_dependencies = dependencies == null ? new string[0] : dependencies;
        }
    }
}
