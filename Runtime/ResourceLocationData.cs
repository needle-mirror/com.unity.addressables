using System;
using System.Collections.Generic;
using UnityEngine;
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
        public string m_id;
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
        public ResourceLocationData(string address, string guid, string id, string provider, bool isLoadable, LocationType locationType = LocationType.String, long labels = 0, string objectType = "", string[] dependencies = null)
        {
            m_isLoadable = isLoadable;
            m_address = address;
            m_guid = guid;
            m_id = id;
            m_provider = provider;
            m_typeName = objectType;
            m_type = locationType;
            m_dependencies = dependencies == null ? new string[0] : dependencies;
            m_labelMask = labels;
        }

        /// <summary>
        /// TODO - doc
        /// </summary>
        public IResourceLocation Create()
        {
             switch (m_type)
            {
                case LocationType.String: return new ResourceLocationBase<string>(m_address, m_id, m_provider);
                case LocationType.Int: return new ResourceLocationBase<int>(int.Parse(m_address), m_id, m_provider);
                    //case LocationType.Enum: return ResourceLocationBase<Enum>(Enum.Parse(typeof(Enum), address) as typeof, id, provider);
            }
            return null;
        }
    }


    /// <summary>
    /// TODO - doc
    /// </summary>
    [Serializable]
    public class ResourceLocationList
    {
        /// <summary>
        /// TODO - doc
        /// </summary>
        [SerializeField]
        public List<ResourceLocationData> locations = new List<ResourceLocationData>();
        /// <summary>
        /// TODO - doc
        /// </summary>
        [SerializeField]
        public List<string> labels = new List<string>();
        /// <summary>
        /// TODO - doc
        /// </summary>
        public ResourceLocationList() { }
        /// <summary>
        /// TODO - doc
        /// </summary>
        public ResourceLocationList(IEnumerable<ResourceLocationData> locs) { locations.AddRange(locs); }
        /// <summary>
        /// TODO - doc
        /// </summary>
        public bool IsEmpty { get { return locations.Count == 0 && labels.Count == 0; } }
    }
}
