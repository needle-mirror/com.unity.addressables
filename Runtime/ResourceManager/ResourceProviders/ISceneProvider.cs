using System;
using System.Collections.Generic;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.SceneManagement;

namespace UnityEngine.ResourceManagement.ResourceProviders
{
    public interface ISceneProvider
    {
        /// <summary>
        /// Asynchronously loads a scene.
        /// </summary>
        /// <returns>An async operation for the scene.</returns>
        /// <param name="location">Location to load.</param>
        /// <param name="loadDependencyOperation">Async load operation for scene dependencies.</param>
        /// <param name="loadMode">Scene load mode.</param>
        IAsyncOperation<Scene> ProvideSceneAsync(IResourceLocation location, IAsyncOperation<IList<object>> loadDependencyOperation, LoadSceneMode loadMode);

#if UNITY_2018_3_OR_NEWER
        /// <summary>
        /// Asynchronously loads a scene.
        /// </summary>
        /// <returns>An async operation for the scene.</returns>
        /// <param name="location">Location to load.</param>
        /// <param name="loadDependencyOperation">Async load operation for scene dependencies.</param>
        /// <param name="loadParams">Scene load parameters.</param>
        IAsyncOperation<Scene> ProvideSceneAsync(IResourceLocation location, IAsyncOperation<IList<object>> loadDependencyOperation, LoadSceneParameters loadParams);
#endif

        /// <summary>
        /// Release any resources associated with the scene at the given location
        /// </summary>
        /// <returns>An async operation for the scene, completed when the scene is unloaded.</returns>
        /// <param name="location">Location to unload.</param>
        /// <param name="scene">Reference to scene to be unloaded.</param>
        IAsyncOperation<Scene> ReleaseSceneAsync(IResourceLocation location, Scene scene);
    }
}
