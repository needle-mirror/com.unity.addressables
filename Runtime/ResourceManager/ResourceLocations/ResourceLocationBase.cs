using System;
using System.Collections.Generic;

namespace UnityEngine.ResourceManagement.ResourceLocations
{
    /// <summary>
    /// Basic implementation of IResourceLocation
    /// </summary>
    public class ResourceLocationBase : IResourceLocation
    {
        string m_Name;
        string m_Id;
        string m_ProviderId;
        object m_Data;
        List<IResourceLocation> m_Dependencies;
        /// <summary>
        /// Internal id.
        /// </summary>
        public string InternalId { get { return m_Id; } }
        /// <summary>
        /// Provider Id.  This is usually set to the FullName property of the type of the provider class.
        /// </summary>
        public string ProviderId { get { return m_ProviderId; } }
        /// <summary>
        /// List of dependencies that must be loaded before this location.  This value may be null.
        /// </summary>
        public IList<IResourceLocation> Dependencies { get { return m_Dependencies; } }
        /// <summary>
        /// Convenience method to see if there are any dependencies.
        /// </summary>
        public bool HasDependencies { get { return m_Dependencies != null && m_Dependencies.Count > 0; } }
        /// <summary>
        /// Data that is intended for the provider.  Objects can be serialized during the build process to be used by the provider.  An example of usage is cache usage data for AssetBundleProvider.
        /// </summary>
        public object Data { get { return m_Data; } set { m_Data = value; } }
        /// <summary>
        /// Returns the name of the location. This is usally set to the primary key of the location, or its "address".
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return m_Name;
        }
        /// <summary>
        /// Construct a new ResourceLocationBase.
        /// </summary>
        /// <param name="name">The name of the location.  This is usually set to the primary key, or "address" of the location.</param>
        /// <param name="id">The internal id of the location.  This is used by the IResourceProvider to identify the object to provide.  For example this may contain the file path or url of an asset.</param>
        /// <param name="providerId">The provider id.  This is set to the FullName of the type of the provder class.</param>
        /// <param name="dependencies">Locations for the dependencies of this location.</param>
        public ResourceLocationBase(string name, string id, string providerId, params IResourceLocation[] dependencies)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentNullException(id);
            if (string.IsNullOrEmpty(providerId))
                throw new ArgumentNullException(providerId);
            m_Name = name;
            m_Id = id;
            m_ProviderId = providerId;
            m_Dependencies = new List<IResourceLocation>(dependencies);
        }
    }

}
