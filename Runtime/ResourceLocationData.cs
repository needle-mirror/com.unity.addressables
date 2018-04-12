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
        public ResourceLocationData(string address, string guid, string id, string provider, bool isLoadable, LocationType locationType = LocationType.String, long labels = 0, string objectType = "", string[] dependencies = null)
        {
            m_isLoadable = isLoadable;
            m_address = address;
            m_guid = guid;
            m_internalId = id;
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
            return new ResourceLocationBase(m_address, AAConfig.ExpandPathWithGlobalVariables(m_internalId), m_provider);
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


        internal IResourceLocator Create()
        {
            var locMap = new Dictionary<string, IResourceLocation>();
            var dataMap = new Dictionary<string, ResourceLocationData>();
            //create and collect locations
            for (int i = 0; i < locations.Count; i++)
            {
                var rlData = locations[i];
                if (locMap.ContainsKey(rlData.m_address))
                {
                    Debug.LogErrorFormat("Duplicate address '{0}' with id '{1}' found, skipping...", rlData.m_address, rlData.m_internalId);
                    continue;
                }
                var loc = rlData.Create();
                locMap.Add(rlData.m_address, loc);
                dataMap.Add(rlData.m_address, rlData);
            }

            //fix up dependencies between them
            foreach (var kvp in locMap)
            {
                var deps = kvp.Value.Dependencies;
                var data = dataMap[kvp.Key];
                if (data.m_dependencies != null)
                {
                    foreach (var d in data.m_dependencies)
                        kvp.Value.Dependencies.Add(locMap[d]);
                }
            }

            var locator = new ResourceLocationMap();

            foreach (KeyValuePair<string, IResourceLocation> kvp in locMap)
            {
                IResourceLocation loc = kvp.Value;
                ResourceLocationData rlData = dataMap[kvp.Key];
                if (!rlData.m_isLoadable)
                    continue;

                //TODO: convert to multiple keys with types
                locator.Add(rlData.m_address, loc);
                locator.Add(rlData.m_guid, loc);
                for (int t = 0; t < labels.Count; t++)
                {
                    if ((rlData.m_labelMask & (1 << t)) != 0)
                        locator.Add(labels[t], loc);
                }
            }
            return locator;
        }

#if UNITY_EDITOR
        public bool Validate()
        {
            bool success = true;
            HashSet<string> addresses = new HashSet<string>();
            foreach (var l in locations)
            {
                if (!addresses.Add(l.m_address))
                {
                    Debug.LogWarningFormat("Duplicate address '{0}' with path '{1}' found in build, runtime data will contain errors...", 
                        l.m_address, 
                        UnityEditor.AssetDatabase.GUIDToAssetPath(l.m_guid));
                    success = false;
                }
            }
            return success;
        }
#endif
    }
}
