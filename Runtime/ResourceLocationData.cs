using System;
using System.Collections.Generic;
using UnityEngine.ResourceManagement;

namespace UnityEngine.AddressableAssets
{
    /// <summary>
    /// TODO - doc
    /// </summary>
    [Serializable]
    public class ResourceLocationData
    {
        /// <summary>
        /// TODO - doc
        /// </summary>
        [SerializeField]
        private string m_address;
        public string Address { get { return m_address; } }
        /// <summary>
        /// TODO - doc
        /// </summary>
        [SerializeField]
        private string m_guid;
        public string Guid { get { return m_guid; } }
        /// <summary>
        /// TODO - doc
        /// </summary>
        [SerializeField]
        private string m_internalId;
        public string InternalId { get { return m_internalId; } }
        /// <summary>
        /// TODO - doc
        /// </summary>
        [SerializeField]
        private string m_provider;
        public string Provider { get { return m_provider; } }
        /// <summary>
        /// TODO - doc
        /// </summary>
        [SerializeField]
        private string[] m_dependencies;
        public ICollection<string> Dependencies { get { return m_dependencies; } }
        /// <summary>
        /// TODO - doc
        /// </summary>
        public ResourceLocationData(string address, string guid, string id, Type provider, string[] dependencies = null)
        {
            m_address = address;
            m_guid = guid;
            m_internalId = id;
            m_provider = provider == null ? "" : provider.FullName;
            m_dependencies = dependencies == null ? new string[0] : dependencies;
        }

    }
}
