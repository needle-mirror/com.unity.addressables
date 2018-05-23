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

        public ResourceLocationMap(int capacity = 0)
        {
            m_locations = new Dictionary<object, IList<IResourceLocation>>(capacity == 0 ? 100 : capacity);
        }

        public ResourceLocationMap(IList<ResourceLocationData> locations)
        {
            m_locations = new Dictionary<object, IList<IResourceLocation>>(locations.Count * 2);
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
                var loc = new ResourceLocationBase(rlData.m_address, AAConfig.ExpandPathWithGlobalVariables(rlData.m_internalId), rlData.m_provider);
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
            foreach (KeyValuePair<string, IResourceLocation> kvp in locMap)
            {
                IResourceLocation loc = kvp.Value;
                ResourceLocationData rlData = dataMap[kvp.Key];
                if (!rlData.m_isLoadable)
                    continue;

                Add(rlData.m_address, loc);
                Add(rlData.m_guid, loc);
            }
        }


        /// <summary>
        /// TODO - doc
        /// </summary>
        public Dictionary<object, IList<IResourceLocation>> m_locations;

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

        public void Add(object key, IList<IResourceLocation> locs)
        {
            m_locations.Add(key, locs);
        }
    }
}
