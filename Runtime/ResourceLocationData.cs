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
        public enum LocationType
        {
            String,
            Int,
            Enum,
            Custom // ??? hmmm
        }
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
        public LocationType m_type;
        /// <summary>
        /// TODO - doc
        /// </summary>
        public string m_typeName;
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
        public long m_labelMask;
        /// <summary>
        /// TODO - doc
        /// </summary>
        public string[] m_dependencies;
        /// <summary>
        /// TODO - doc
        /// </summary>
        public bool m_isLoadable;
        public ResourceLocationData(string address, string guid, string id, Type provider, bool isLoadable, LocationType locationType = LocationType.String, long labels = 0, string objectType = "", string[] dependencies = null)
        {
            m_isLoadable = isLoadable;
            m_address = address;
            m_guid = guid;
            m_internalId = id;
            m_provider = provider.FullName;
            m_typeName = objectType;
            m_type = locationType;
            m_dependencies = dependencies == null ? new string[0] : dependencies;
            m_labelMask = labels;
        }

        internal object GetKeyObject()
        {
            switch (m_type)
            {
                case LocationType.String: return m_address;
                case LocationType.Int: return int.Parse(m_address);
            }
            return m_address;
        }
    }
}
