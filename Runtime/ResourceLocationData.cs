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
        public string m_address;
        /// <summary>
        /// TODO - doc
        /// </summary>
        public string m_guid;
        /// <summary>
        /// TODO - doc
        /// </summary>
        public string m_internalId;
        /// <summary>
        /// TODO - doc
        /// </summary>
        public string m_provider;
        /// <summary>
        /// TODO - doc
        /// </summary>
        public string[] m_dependencies;
        /// <summary>
        /// TODO - doc
        /// </summary>
        public object m_data;
        public IEnumerable<string> m_labels;
        public ResourceLocationData(string address, string guid, string id, Type provider, IEnumerable<string> labels = null, string[] dependencies = null, object data = null)
        {
            m_address = address;
            m_labels = labels;
            m_guid = guid;
            m_data = data;
            m_internalId = id;
            m_provider = provider.FullName;
            m_dependencies = dependencies == null ? new string[0] : dependencies;
        }

    }
}
