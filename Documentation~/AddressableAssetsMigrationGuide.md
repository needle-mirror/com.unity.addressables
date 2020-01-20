# Upgrading to the Addressables system
This article details how to modify your existing Project to take advantage of Addressable Assets. There are three traditional methods for referencing assets:

* **[Direct References](#the-direct-reference-method)**: Add assets directly into components or Scenes, which the application loads automatically. 
* **[Resource Folders](#the-resource-folders-method)**: Add assets to your _Resource_ folder and load them by filename.
* **[Asset Bundles](#the-asset-bundles-method)**: Add assets to asset bundles, then load them with their dependencies by file path.

### The direct reference method
To migrate from this approach, follow these steps:

1. Replace your direct references to objects with asset references (for example, `public GameObject directRefMember;` becomes `public AssetReference AssetRefMember;`).
2. Drag assets onto the appropriate component’s Inspector, as you would for a direct reference.
3. If you'd like to load an asset based on an object rather than a string name, instantiate it directly from the [`AssetReference`](../api/UnityEngine.AddressableAssets.AssetReference.html) object you created in your setup (for example, `AssetRefMember.LoadAssetAsync<GameObject>();` or `AssetRefMember.InstantiateAsync(pos, rot);`).

**Note**: The Addressable Asset system loads assets asynchronously. When you update your direct references to asset references, you must also update your code to operate asynchronously.

### The Resource folders method
When you mark an asset in a _Resources_ folder as Addressable, the system automatically moves the asset from the _Resources_ folder to a new folder in your Project named _Resources_moved_. The default address for a moved asset is the old path, omitting the folder name. For example, your loading code might change from `Resources.LoadAsync<GameObject>("desert/tank.prefab");` to [`Addressables.LoadAssetAsync<GameObject>("desert/tank.prefab");`](../api/UnityEngine.AddressableAssets.Addressables.html#UnityEngine_AddressableAssets_Addressables_LoadAssetsAsync__1_System_Collections_Generic_IList_UnityEngine_ResourceManagement_ResourceLocations_IResourceLocation__System_Action___0__).

### The asset bundles method
When you open the **Addressables Groups** window, Unity offers to convert all asset bundles into Addressable Asset groups. This is the easiest way to migrate your code.

If you choose to convert your Assets manually, click the **Ignore** button. Then, either use the direct reference or resource folder methods previously described.

The default path for the address of an asset is its file path. If you use the path as the asset's address, you'd load the asset in the same manner as you would load from a bundle. The Addressable Asset System handles the loading of the bundle and all its dependencies.
