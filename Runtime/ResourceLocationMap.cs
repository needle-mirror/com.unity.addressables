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
            Locations = new Dictionary<object, IList<IResourceLocation>>(capacity == 0 ? 100 : capacity);
        }

        public ResourceLocationMap(IList<ResourceLocationData> locations)
        {
            if (locations == null)
                return;
            Locations = new Dictionary<object, IList<IResourceLocation>>(locations.Count * 2);
            var locMap = new Dictionary<string, IResourceLocation>();
            var dataMap = new Dictionary<string, ResourceLocationData>();
            //create and collect locations
            for (int i = 0; i < locations.Count; i++)
            {
                var rlData = locations[i];
                if (locMap.ContainsKey(rlData.Address))
                {
                    Debug.LogErrorFormat("Duplicate address '{0}' with id '{1}' found, skipping...", rlData.Address, rlData.InternalId);
                    continue;
                }
                var loc = new ResourceLocationBase(rlData.Address, AAConfig.ExpandPathWithGlobalVariables(rlData.InternalId), rlData.Provider);
                locMap.Add(rlData.Address, loc);
                dataMap.Add(rlData.Address, rlData);
            }

            //fix up dependencies between them
            foreach (var kvp in locMap)
            {
                var deps = kvp.Value.Dependencies;
                var data = dataMap[kvp.Key];
                if (data.Dependencies != null)
                {
                    foreach (var d in data.Dependencies)
                        kvp.Value.Dependencies.Add(locMap[d]);
                }
            }
            foreach (KeyValuePair<string, IResourceLocation> kvp in locMap)
            {
                IResourceLocation loc = kvp.Value;
                ResourceLocationData rlData = dataMap[kvp.Key];
                if(!string.IsNullOrEmpty(rlData.Address))
                    Add(rlData.Address, loc);
                if (!string.IsNullOrEmpty(rlData.Guid))
                    Add(rlData.Guid, loc);
            }
        }


        /// <summary>
        /// TODO - doc
        /// </summary>
        public Dictionary<object, IList<IResourceLocation>> Locations { get; private set; }

        /// <summary>
        /// TODO - doc
        /// </summary>
        public bool Locate(object key, out IList<IResourceLocation> locations)
        {
            return Locations.TryGetValue(key, out locations);
        }

        /// <summary>
        /// TODO - doc
        /// </summary>
        public void Add(object key, IResourceLocation loc)
        {
            IList<IResourceLocation> locations;
            if (!Locations.TryGetValue(key, out locations))
                Locations.Add(key, locations = new List<IResourceLocation>());
            locations.Add(loc);
        }

        public void Add(object key, IList<IResourceLocation> locs)
        {
            Locations.Add(key, locs);
        }
    }
}
