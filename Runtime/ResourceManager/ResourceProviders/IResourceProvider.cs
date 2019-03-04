using System;
using System.Collections.Generic;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.Util;

namespace UnityEngine.ResourceManagement.ResourceProviders
{
    public enum ProviderBehaviourFlags
    {
        None = 0,
        CanProvideWithFailedDependencies = 1
    }

    /// <summary>
    /// Resoure Providers handle loading (Provide) and unloading (Release) of objects
    /// </summary>
    public interface IResourceProvider : IInitializableObject
    {
        /// <summary>
        /// Unique identifier for this provider, used by Resource Locations to find a suitable Provider
        /// </summary>
        /// <value>The provider identifier.</value>
        string ProviderId { get; }

        /// <summary>
        /// Synchronously load the resource at the given location. An asynchronous load operation for any dependencies should be passed as an argument.
        /// </summary>
        /// <returns>An asynchronous operation to load the object.</returns>
        /// <param name="location">Location to load.</param>
        /// <param name="loadDependencyOperation">Aynchronous dependency load operation.</param>
        /// <typeparam name="TObject">Object type to be loaded and returned.</typeparam>
        IAsyncOperation<TObject> Provide<TObject>(IResourceLocation location, IList<object> dependencies)
        where TObject : class;

        /// <summary>
        /// Evaluate whether or not the provider can load the given location.
        /// </summary>
        /// <returns><c>true</c>, if provide can load the location. <c>false</c> otherwise.</returns>
        /// <param name="location">Location to evaluate.</param>
        /// <typeparam name="TObject">The object type for the given location.</typeparam>
        bool CanProvide<TObject>(IResourceLocation location)
        where TObject : class;

        /// <summary>
        /// Release and/or unload the given resource location and asset
        /// </summary>
        /// <returns><c>true</c>, if release was successful. <c>false</c> otherwise.</returns>
        /// <param name="location">Location to release.</param>
        /// <param name="asset">Asset to unload.</param>
        bool Release(IResourceLocation location, object asset);

        ProviderBehaviourFlags BehaviourFlags { get; }
    }
}
