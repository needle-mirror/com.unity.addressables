using System.Collections.Generic;
using UnityEngine.ResourceManagement;
using UnityEngine;
using System;
using System.Collections;

namespace UnityEngine.AddressableAssets
{
    /// <summary>
    /// TODO - doc
    /// </summary>
    public class ResourceLocationMap<T> : IResourceLocator<T>
    {
        /// <summary>
        /// TODO - doc
        /// </summary>
        public Dictionary<T, IResourceLocation<T>> m_addressMap = new Dictionary<T, IResourceLocation<T>>();
        /// <summary>
        /// TODO - doc
        /// </summary>
        public Dictionary<string, IList<T>> m_labeledGroups = new Dictionary<string, IList<T>>();
        /// <summary>
        /// TODO - doc
        /// </summary>
        public ResourceLocationMap()
        {
        }

        /// <summary>
        /// TODO - doc
        /// </summary>
        public IList<T> GetAddresses(string label)
        {
            IList<T> results = null;
            m_labeledGroups.TryGetValue(label, out results);
            return results;
        }

        /// <summary>
        /// TODO - doc
        /// </summary>
        public IResourceLocation Locate(T address)
        {
            IResourceLocation<T> loc = null;
            m_addressMap.TryGetValue(address, out loc);
            return loc;
        }
    }
}
