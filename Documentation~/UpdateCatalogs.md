---
uid: addressables-api-update-catalogs
---
# Addressables.UpdateCatalogs
#### API
- `static AsyncOperationHandle<List<IResourceLocator>> UpdateCatalogs(IEnumerable<string> catalogs = null, bool autoReleaseHandle = true)`

#### Returns
`AsyncOperationHandle<List<IResourceLocator>>`: A list of the `IResourceLocators` loaded from the updated catalogs.

Like with the [`LoadContentCatalogAsync`](LoadContentCatalogAsync.md) API, no further action is needed to use the new content catalogs.  The handle can be safely released after completion.

#### Description
`Addressables.UpdateCatalogs` is used to update the content catalog at runtime.  When `UpdateCatalogs` is called, all `Addressables` requests, such as asset loading and instantiation, are blocked until the `UpdateCatalogs` operation is complete.

There is an option on the `AddressableAssetSettings` object called "Unique Bundle IDs" which forces unique `AssetBundle` IDs.  If enabled, this option may lead to more `AssetBundles` getting rebuilt during build time but makes updating content catalogs safer at runtime.  "Unique Bundle IDs" creates more complex internal IDs for the `AssetBundles`, which prevents internal ID collisions at load time.

The option is located on the `AddressableAssetSettings` object (inside `Assets/AddressableAssetsData/` by default) under the General section.
##### Related API
The list of content catalogs with an available update can be aquired through `Addressables.CheckForCatalogUpdates`.  If no catalog list is passed in, `CheckForCatalogUpdates` is called automatically.

`CheckForCatalogUpdates` iterates through each `ResourceLocator` currently being used by `Addressables` and return a list of `strings` that correlate to the content catalog IDs that have an available update.

It is safe to release the `AsyncOperationHandle` from `Addressables.CheckForCatalogUpdates` after completion, or let it automatically relase with the `autoReleaseHandle` parameter.  If you intend to use the `Result` of `CheckForCatalogUpdates` then store it as part of the `Completed` operation (see Code Sample below).

#### Code Sample
Check for catalog update prior to update operation:
```
IEnumerator UpdateCatalogs()
{
    List<string> catalogsToUpdate = new List<string>();
    AsyncOperationHandle<List<string>> checkForUpdateHandle = Addressables.CheckForCatalogUpdates();
    checkForUpdateHandle.Completed += op =>
    {
        catalogsToUpdate.AddRange(op.Result);
    };
    yield return checkForUpdateHandle;
    if (catalogsToUpdate.Count > 0)
    {
        AsyncOperationHandle<List<IResourceLocator>> updateHandle = Addressables.UpdateCatalogs(catalogsToUpdate);
        yield return updateHandle;
    }
}
```
Allow `Addressables` to handle checking for catalog updates automatically:
```
 IEnumerator UpdateCatalogs()
{
    AsyncOperationHandle<List<IResourceLocator>> updateHandle = Addressables.UpdateCatalogs();
    yield return updateHandle;
}
```