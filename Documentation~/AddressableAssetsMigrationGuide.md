---
uid: addressables-migration
---
# Upgrading to the Addressables system
This article details how to modify your existing project to take advantage of Addressable Assets. There are three traditional methods for referencing assets:

* **[Direct References](#the-direct-reference-method)**: Add assets directly into components or Scenes, which the application loads automatically. 
* **[Resource Folders](#the-resource-folders-method)**: Add assets to your `Resource` folder and load them by filename.
* **[AssetBundles](#the-assetbundles-method)**: Add assets to AssetBundles, then load them with their dependencies by file path.

### The direct reference method
To migrate from this approach, follow these steps:

1. Replace your direct references to objects with asset references (for example, `public GameObject directRefMember;` becomes `public AssetReference AssetRefMember;`).
2. Drag assets onto the appropriate component’s Inspector, as you would for a direct reference.
3. If you'd like to load an asset based on an object rather than a string name, instantiate it directly from the [`AssetReference`](xref:UnityEngine.AddressableAssets.AssetReference) object you created in your setup (for example, `AssetRefMember.LoadAssetAsync<GameObject>();` or `AssetRefMember.InstantiateAsync(pos, rot);`).

**Note**: The Addressable Asset system loads assets asynchronously. When you update your direct references to asset references, you must also update your code to operate asynchronously.

### The Resource folders method
When you mark an asset in a `Resources` folder as Addressable, the system automatically moves the asset from the `Resources` folder to a new folder in your project named `Resources_moved`. The default address for a moved asset is the old path, omitting the folder name. For example, your loading code might change from `Resources.LoadAsync<GameObject>("desert/tank.prefab");` to [`Addressables.LoadAssetAsync<GameObject>("desert/tank.prefab");`](xref:UnityEngine.AddressableAssets.Addressables.LoadAssetsAsync``1(System.Collections.Generic.IList{UnityEngine.ResourceManagement.ResourceLocations.IResourceLocation},System.Action{``0})).

**Note**: Some functionality of the [Resources](https://docs.unity3d.com/ScriptReference/Resources.html "Resources") class may not be supported directly after modifying your project to use Addressable Assets.

For example, consider the [Resources.LoadAll](https://docs.unity3d.com/ScriptReference/Resources.LoadAll.html "Resources.LoadAll") function. Previously, if you had assets in a folder `Resources/MyPrefabs/`, and ran `Resources.LoadAll<SampleType>("MyPrefabs");`, it would have loaded all the assets in `Resources/MyPrefabs/` matching type `SampleType`. Addressable Assets do not support this functionality.  You could achieve similar results using the Addressable Assets concept of [labels](AddressableAssetsOverview.md), but the two ideas are not directly analogous. 

### The AssetBundles method
When you open the **Addressables Groups** window, Unity offers to convert all AssetBundles into Addressable Asset groups. This is the easiest way to migrate your code.

If you choose to convert your assets manually, click the **Ignore** button. Then, either use the direct reference or resource folder methods previously described.

The default path for the address of an asset is its file path. If you use the path as the asset's address, you'd load the asset in the same manner as you would load from a bundle. The Addressable Asset System handles the loading of the bundle and all its dependencies.