---
uid: addressables-api-load-content-catalog-async
---
# Addressables.LoadContentCatalogAsync
#### API
- `static AsyncOperationHandle<IResourceLocator> LoadContentCatalogAsync(string catalogPath, string providerSuffix = null)`
- `static AsyncOperationHandle<IResourceLocator> LoadContentCatalogAsync(string catalogPath, bool autoReleaseHandle, string providerSuffix = null)`

#### Returns
`AsyncOperationHandle<IResourceLocator>`: An async operation handle that returns the `IResourceLocator` loaded by the catalog.  After the operation has completed, it is safe to release this handle.

No further action is required to load the content catalog once the operation completes.  The `IResourceLocator` returned from the operation is available but not needed to make use of the newly loaded catalog.

#### Best Practice
- Pass `true` into the `autoReleaseHandle` parameter.

#### Description
`LoadContentCatalogAsync` is used to load a secondary Content Catalog.  The Content Catalog returns an `IResourceLocator`, which maps addresses to asset locations.  Using this API allows you to load assets built by `Addressables` from a project separate than that which contains your runtime player build.

It is a common case for larger projects to split the project with asset content from the project containing runtime scripts; typically this is done to reduce import times.  `Addressables` supports this workflow through the `LoadContentCatalogAsync` API.  Both projects need to contain the `Addressables` package and build their player content for the appropriate platform.  The content project must build all of its content into remote `Addressable` groups.  Also ensure that `Build Remote Catalog` is turned on for the content project.  From there, place the `AssetBundles` from the content project into the desired remote location and load the catalog in your runtime project.

It is also possible to end up with multiple secondary catalogs if you're using a custom build script to build your player content.

Once the secondary catalog is loaded, you can use the keys built into that catalog with the `Addressables` APIs.  Also, once a secondary catalog is loaded it cannot be unloaded.  The `AsyncOperationHandle` can safely be released or you can pass in `true` to the `autoReleaseHandle` parameter.

Once a secondary catalog is downloaded, it is cached locally if a corresponding `.hash` file is located alongside the catalog's `.json` file.  The API looks for a `.hash` file of the same name as the catalog in the same directory as the catalog itself.  The `.hash` file contains the current hash of the content catalog.  If no `.hash` file can be found, the remote catalog is downloaded on all future requests.  Otherwise, when loading the remote catalog on subsequent application starts, the local hash of the catalog is compared against the hash of the remote catalog at the given location. If a different hash is detected, the remote catalog is downloaded, cached, and used. If the remote and local hash match, or if there is no internet connection, the cached catalog is used.

If you need to update catalogs (either primary or secondary) during runtime, refer to the [UpdateCatalogs](UpdateCatalogs.md) documentation.

The `providerSuffix` parameter can be used to ensure unique IDs for the resource providers loaded from a given catalog.  The `string` passed in will be appended to all provider IDs.  

#### Code Sample
```
public IEnumerator Start()
{
    //Load a catalog and automatically release the operation handle.
    AsyncOperationHandle<IResourceLocator> handle = Addressables.LoadContentCatalogAsync("path_to_secondary_catalog", true);
    yield return handle;
    
    //...
}
```