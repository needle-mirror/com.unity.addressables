using System;
using System.Collections.Generic;
using UnityEngine.ResourceManagement;

namespace UnityEngine.AddressableAssets
{
    /// <summary>
    /// Entry point for Addressable API, this provides a simpler interface than using ResourceManager directly as it assumes string address type.
    /// </summary>
    public class Addressables
    {
        class AddressListOperation<TAddress> : AsyncOperationBase<IList<TAddress>>
        {
            string m_label;
            Action m_onInitCompleteAction;
            public AddressListOperation()
            {
                m_onInitCompleteAction = OnInitComplete;
            }

            public IAsyncOperation<IList<TAddress>> Start(string label)
            {
                m_label = label;
                ResourceManager.InitializationComplete += m_onInitCompleteAction;
                return this;
            }

            private void OnInitComplete()
            {
                var addresses = GetAddresses(m_label);
                if (addresses == null)
                {
                    Debug.LogWarningFormat("Unable to find addresses from label '{0}'.", m_label);
                    addresses = new List<TAddress>();
                }
                SetResult(addresses);
                InvokeCompletionEvent();
            }

            private IList<TAddress> GetAddresses(string label)
            {
                for (int i = 0; i < ResourceManager.ResourceLocators.Count; i++)
                {
                    var locator = ResourceManager.ResourceLocators[i] as ResourceLocationMap<TAddress>;
                    if (locator == null)
                        continue;

                    var l = locator.GetAddresses(label);
                    if (l != null)
                        return l;
                }
                return null;
            }
        }

        /// <summary>
        /// Returns all addresses that have been marked with a label.
        /// </summary>
        public static IAsyncOperation<IList<TAddress>> GetAddressesAsync<TAddress>(string label)
        {
            return AsyncOperationCache.Instance.Acquire<AddressListOperation<TAddress>, IList<TAddress>>().Start(label).Acquire();
        }

        class LoadAllByLabelOperation<TKey, TObject> : AsyncOperationBase<IList<TObject>> where TObject : class
        {
            Action<IAsyncOperation<TObject>> m_callback;
            Action<IAsyncOperation<IList<TKey>>> m_onAddressesLoadedAction;
            Action<IAsyncOperation<IList<TObject>>> m_onAllAssetsLoadedAction;
            public LoadAllByLabelOperation()
            {
                m_onAddressesLoadedAction = OnAddressesLoaded;
                m_onAllAssetsLoadedAction = OnAllAssetsLoaded;
            }
            public IAsyncOperation<IList<TObject>> Start(string label, Action<IAsyncOperation<TObject>> callback)
            {
                m_callback = callback;
                GetAddressesAsync<TKey>(label).Completed += m_onAddressesLoadedAction;
                return this;
            }

            private void OnAddressesLoaded(IAsyncOperation<IList<TKey>> operation)
            {
                if (operation.Result == null || operation.Result.Count == 0)
                {
                    Debug.LogWarningFormat("No addresses found.");
                    SetResult(new List<TObject>());
                    InvokeCompletionEvent();
                }
                else
                {
                    ResourceManager.LoadAllAsync(operation.Result, m_callback).Completed += m_onAllAssetsLoadedAction;
                }
            }

            private void OnAllAssetsLoaded(IAsyncOperation<IList<TObject>> operation)
            {
                SetResult(operation.Result);
                InvokeCompletionEvent();
            }
        }
        /// <summary>
        /// Loads multiple assets from a label.  A callback can be passed in to handle each asset load as it completes.
        /// </summary>
        public static IAsyncOperation<IList<TObject>> LoadAllByLabelAsync<TObject>(string label, Action<IAsyncOperation<TObject>> callback) where TObject : class
        {
            return AsyncOperationCache.Instance.Acquire<LoadAllByLabelOperation<string, TObject>, IList<TObject>>().Start(label, callback).Acquire();
        }


        class InstantiateAllByLabelOperation<TKey, TObject> : AsyncOperationBase<IList<TObject>> where TObject : Object
        {
            Action<IAsyncOperation<TObject>> m_callback;
            Transform m_parent;
            bool m_instantiateInWorldSpace;
            Action<IAsyncOperation<IList<TKey>>> m_onAddressesLoadedAction;
            Action<IAsyncOperation<IList<TObject>>> m_onInstancesLoadedAction;
            public InstantiateAllByLabelOperation()
            {
                m_onAddressesLoadedAction = OnAddressesLoaded;
                m_onInstancesLoadedAction = OnInstancesLoaded;
            }
            public IAsyncOperation<IList<TObject>> Start(string label, Action<IAsyncOperation<TObject>> callback, Transform parent, bool instantiateInWorldSpace)
            {
                m_parent = parent;
                m_instantiateInWorldSpace = instantiateInWorldSpace;
                m_callback = callback;
                GetAddressesAsync<TKey>(label).Completed += m_onAddressesLoadedAction;
                return this;
            }

            private void OnAddressesLoaded(IAsyncOperation<IList<TKey>> operation)
            {
                if (operation.Result == null || operation.Result.Count == 0)
                {
                    Debug.LogWarningFormat("No addresses found.");
                    SetResult(new List<TObject>());
                    InvokeCompletionEvent();
                }
                else
                {
                    ResourceManager.InstantiateAllAsync(operation.Result, m_callback, m_parent, m_instantiateInWorldSpace).Completed += m_onInstancesLoadedAction;
                }
            }

            private void OnInstancesLoaded(IAsyncOperation<IList<TObject>> obj)
            {
                SetResult(obj.Result);
                InvokeCompletionEvent();
            }
        }

        /// <summary>
        /// Instantiate multiple assets from a label.  A callback can be passed in to handle each asset load as it completes.
        /// </summary>
        public static IAsyncOperation<IList<TObject>> InstantiateAllByLabelAsync<TObject>(string label, Action<IAsyncOperation<TObject>> callback, Transform parent = null, bool instantiateInWorldSpace = false) where TObject : Object
        {
            return AsyncOperationCache.Instance.Acquire<InstantiateAllByLabelOperation<string, TObject>, IList<TObject>>().Start(label, callback, parent, instantiateInWorldSpace).Acquire();
        }

        class PreloadAllByLabelOperation<TKey> : AsyncOperationBase<IList<object>>
        {
            Action<IAsyncOperation<object>> m_callback;
            Action<IAsyncOperation<IList<TKey>>> m_onAddressesLoadedAction;
            Action<IAsyncOperation<IList<object>>> m_onAllAssetsLoadedAction;
            public PreloadAllByLabelOperation()
            {
                m_onAddressesLoadedAction = OnAddressesLoaded;
                m_onAllAssetsLoadedAction = OnAllAssetsLoaded;
            }
            public IAsyncOperation<IList<object>> Start(string label, Action<IAsyncOperation<object>> callback)
            {
                m_callback = callback;
                GetAddressesAsync<TKey>(label).Completed += m_onAddressesLoadedAction;
                return this;
            }

            private void OnAddressesLoaded(IAsyncOperation<IList<TKey>> operation)
            {
                if (operation.Result == null || operation.Result.Count == 0)
                {
                    Debug.LogWarningFormat("No addresses found.");
                    SetResult(new List<object>());
                    InvokeCompletionEvent();
                }
                else
                {
                    ResourceManager.PreloadDependenciesAllAsync<TKey>(operation.Result, m_callback).Completed += m_onAllAssetsLoadedAction;
                }
            }


            private void OnAllAssetsLoaded(IAsyncOperation<IList<object>> operation)
            {
                SetResult(operation.Result);
                InvokeCompletionEvent();
            }
        }

        /// <summary>
        /// Preloads dependencies of multiple assets from a label.  A callback can be passed in to handle each asset load as it completes.
        /// </summary>
        public static IAsyncOperation<IList<object>> PreloadAllByLabelAsync(string label, Action<IAsyncOperation<object>> callback)
        {
            return AsyncOperationCache.Instance.Acquire<PreloadAllByLabelOperation<string>, IList<object>>().Start(label, callback).Acquire();
        }

        /// <summary>
        /// Release a loaded asset.  The asset is ref counted so it may not be unloaded immediately.
        /// </summary>
        public static void Release<TObject>(TObject obj) where TObject : class
        {
            ResourceManager.Release<TObject>(obj);
        }


        /// <summary>
        /// Release an instantiated object.  The object may be released to an object pool so it may not be detroyed immediately.  The asset that it was instantiated from will have its ref count decreased, which may unload the asset.
        /// </summary>
        public static void ReleaseInstance<TObject>(TObject obj) where TObject : Object
        {
            ResourceManager.ReleaseInstance<TObject>(obj);
        }
 
        /// <summary>
        /// Load an asset via a string address.  The IAsyncOperation returned can be yielded upon or a completion handler can be set to wait for the result.
        /// </summary>
        public static IAsyncOperation<TObject> LoadAsync<TObject>(string address) where TObject : class
        {
            return ResourceManager.LoadAsync<TObject, string>(address);
        }
        /// <summary>
        /// Load an asset via an AssetReference.  The IAsyncOperation returned can be yielded upon or a completion handler can be set to wait for the result.
        /// </summary>
        public static IAsyncOperation<TObject> LoadAsync<TObject>(AssetReference assetRef) where TObject : class
        {
            return ResourceManager.LoadAsync<TObject, AssetReference>(assetRef);
        }


        /// <summary>
        /// Load multiple assets from a list of string addresses.  A callback can be passed in to handle each asset load as it completes.
        /// </summary>
        public static IAsyncOperation<IList<TObject>> LoadAllAsync<TObject>(IList<string> addresses, Action<IAsyncOperation<TObject>> callback) where TObject : class
        {
            return ResourceManager.LoadAllAsync<TObject, string>(addresses, callback);
        }
        /// <summary>
        /// Load multiple assets from a list of AssetReference addresses.  A callback can be passed in to handle each asset load as it completes.
        /// </summary>
        public static IAsyncOperation<IList<TObject>> LoadAllAsync<TObject>(IList<AssetReference> assetRefs, Action<IAsyncOperation<TObject>> callback) where TObject : class
        {
            return ResourceManager.LoadAllAsync<TObject, AssetReference>(assetRefs, callback);
        }

        /// <summary>
        /// Instantiate an asset via a string address.  The IAsyncOperation returned can be yielded upon or a completion handler can be set to wait for the result.
        /// </summary>
        public static IAsyncOperation<TObject> InstantiateAsync<TObject>(string address, Transform parent = null, bool instantiateInWorldSpace = false) where TObject : Object
        {
            return ResourceManager.InstantiateAsync<TObject, string>(address, parent, instantiateInWorldSpace);
        }
        /// <summary>
        /// Instantiate an asset via a string address.  The IAsyncOperation returned can be yielded upon or a completion handler can be set to wait for the result.
        /// </summary>
        public static IAsyncOperation<TObject> InstantiateAsync<TObject>(string address, Vector3 position, Quaternion rotation, Transform parent = null) where TObject : Object
        {
            return ResourceManager.InstantiateAsync<TObject, string>(address, position, rotation, parent);
        }

        /// <summary>
        /// Load an asset via an AssetReference address.  The IAsyncOperation returned can be yielded upon or a completion handler can be set to wait for the result.
        /// </summary>
        public static IAsyncOperation<TObject> InstantiateAsync<TObject>(AssetReference address, Transform parent = null, bool instantiateInWorldSpace = false) where TObject : Object
        {
            return ResourceManager.InstantiateAsync<TObject, AssetReference>(address, parent, instantiateInWorldSpace);
        }
        /// <summary>
        /// Load an asset via an AssetReference address.  The IAsyncOperation returned can be yielded upon or a completion handler can be set to wait for the result.
        /// </summary>
        public static IAsyncOperation<TObject> InstantiateAsync<TObject>(AssetReference address, Vector3 position, Quaternion rotation, Transform parent = null) where TObject : Object
        {
            return ResourceManager.InstantiateAsync<TObject, AssetReference>(address, position, rotation, parent);
        }

        /// <summary>
        /// Instantiate multiple assets from a list of string addresses.  A callback can be passed in to handle each asset load as it completes.
        /// </summary>
        public static IAsyncOperation<IList<TObject>> InstantiateAllAsync<TObject>(IList<string> addresses, Action<IAsyncOperation<TObject>> callback) where TObject : Object
        {
            return ResourceManager.InstantiateAllAsync<TObject, string>(addresses, callback);
        }
        /// <summary>
        /// Instantiate multiple assets from a list of AssetReference addresses.  A callback can be passed in to handle each asset load as it completes.
        /// </summary>
        public static IAsyncOperation<IList<TObject>> InstantiateAllAsync<TObject>(IList<AssetReference> addresses, Action<IAsyncOperation<TObject>> callback) where TObject : Object
        {
            return ResourceManager.InstantiateAllAsync<TObject, AssetReference>(addresses, callback);
        }

        /// <summary>
        /// Unload scene via string address.
        /// </summary>
        public static IAsyncOperation<UnityEngine.SceneManagement.Scene> UnloadSceneAsync(string address)
        {
            return ResourceManager.UnloadSceneAsync<string>(address);
        }

        /// <summary>
        /// Preload dependencies for an asset via its string address.
        /// </summary>
        public static IAsyncOperation<IList<object>> PreloadDependenciesAsync(string address, Action<IAsyncOperation<object>> callback)
        {
            return ResourceManager.PreloadDependenciesAsync<string>(address, callback);
        }
        /// <summary>
        /// Preload dependencies for an asset via its AssetReference address.
        /// </summary>
        public static IAsyncOperation<IList<object>> PreloadDependenciesAsync(AssetReference address, Action<IAsyncOperation<object>> callback)
        {
            return ResourceManager.PreloadDependenciesAsync<AssetReference>(address, callback);
        }

        /// <summary>
        /// Preload multiple assets from a list of string addresses.  A callback can be passed in to handle each asset load as it completes.
        /// </summary>
        public static IAsyncOperation<IList<object>> PreloadDependenciesAllAsync(IList<string> addresses, Action<IAsyncOperation<object>> callback)
        {
            return ResourceManager.PreloadDependenciesAllAsync<string>(addresses, callback);
        }

        /// <summary>
        /// Preload multiple assets from a list of AssetReference addresses.  A callback can be passed in to handle each asset load as it completes.
        /// </summary>
        public static IAsyncOperation<IList<object>> PreloadDependenciesAllAsync(IList<AssetReference> addresses, Action<IAsyncOperation<object>> callback)
        {
            return ResourceManager.PreloadDependenciesAllAsync<AssetReference>(addresses, callback);
        }

        /// <summary>
        /// Load a scene via its string address.
        /// </summary>
        public static IAsyncOperation<UnityEngine.SceneManagement.Scene> LoadSceneAsync(string address, UnityEngine.SceneManagement.LoadSceneMode loadMode = UnityEngine.SceneManagement.LoadSceneMode.Single)
        {
            return ResourceManager.LoadSceneAsync(address, loadMode);
        }


    }
}

