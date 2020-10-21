---
uid: addressables-api-load-asset-async
---
# Addressables.LoadAsset(s)Async
### Addressables.LoadAssetAsync
- `static AsyncOperationHandle<TObject> LoadAssetAsync<TObject>(object key)`
- `static AsyncOperationHandle<TObject> LoadAssetAsync<TObject>(IResourceLocation location)`

#### Returns
`AsyncOperationHandle<TObject>` - The operation handle for a single requested asset.

#### Description
Loads a single Addressable Asset.

`Addressables.LoadAssetAsync` uses the key of an Addressable object or direct ResourceLocations to load an Addressable asset of a specified type.  The loaded asset can be accessed through the Result property of the `AsyncOperationHandle` returned by the function.
This API does the following:
1) Gathers all dependencies for the given key or resource location.  The key version of the API incurs an additional step to lookup the first IResourceLocation that matches the provided key.
2) Downloads any remote AssetBundles that are required.
3) Loads the AssetBundles into memory.
4) Populates the `Result` property with the requested object.

The internal operation of the `AsyncOperationHandle` returned will have a reference count of 1 by default.  This handle will need to be manually released in order to decrease the reference count and unload the AssetBundles in memory.

If there are multiple request calls for the same asset, then the reference count for the underlying internal operation increases, and the cached load operation is not used. This means that all newly-created handles need to ensure they are properly released in order to unload the AssetBundles.

Note that there is also the option to load an asset with the helper function `LoadAssetAsync` in the [`AssetReference`](xref:UnityEngine.AddressableAssets.AssetReference.LoadAssetAsync*) class (for example `AssetRefMember.LoadAssetAsync<GameObject>()`) but it has more restrictions to reference count. When called, it caches the handle in the `AssetReference` of the load if successful.  Due to the cached handle, this load cannot be called again to load or increase the reference count. If the second call was simply an attempt to access the result, you can use `myAssetReference.Asset`.  If the second call was an attempt to increase the reference count, you must call directly into addressables, with `Addressables.LoadAssetAsync(myAssetReference)`

If there are any issues during the excecution of these operations, an Exception is generated and returned in the operations `OperationException`. That exception is also logged as an error by default. If you would like to see the exceptions when they happen, process them, and choose how to handle them, see [`ResourceManager.ExceptionHandler`](ExceptionHandler.md).

#### Code Sample
```
IEnumerator LoadGameObjectAndMaterial()
{
    //Load a GameObject
    AsyncOperationHandle<GameObject> goHandle = Addressables.LoadAssetAsync<GameObject>("gameObjectKey");
    yield return goHandle;
    if(goHandle.Status == AsyncOperationStatus.Succeeded)
    {
        GameObject obj = goHandle.Result;
        //etc...
    }

    //Load a Material
    AsyncOperationHandle<IList<IResourceLocation>> locationHandle = Addressables.LoadResourceLocationsAsync("materialKey");
    yield return locationHandle;
    AsyncOperationHandle<Material> matHandle = Addressables.LoadAssetAsync<Material>(locationHandle.Result[0]);
    yield return matHandle;
    if (matHandle.Status == AsyncOperationStatus.Succeeded)
    {
        Material mat = matHandle.Result;
        //etc...
    }

    //Use this only when the objects are no longer needed
    Addressables.Release(goHandle);
    Addressables.Release(matHandle);
}
```
You can use this pattern to load any number of supported runtime types with Addressables.
The benefit loading and caching ResourceLocations is purely a performance consideration.  If you pass in a key, which is the most common use of this API, Addressables needs to iterate through its ResourceLocators to find the corresponding ResourceLocation.  If you directly pass in an `IResourceLocation`, this step is skipped.

Additionally, the Addressables loading APIs benefit from information found in [Memory management](MemoryManagement.md).

#### Pitfalls
Loading a GameObject through Addressables and then instantiating it through the standard `Object.Instantiate(...)` method can have potentially disastrous results.  When loading the asset, only the load operation contains any knowledge of a reference count. If you release this operation handle prior to destroying your GameObject instance, all the data (materials, textures, etc.) is unloaded out from underneath the object when the AssetBundle is unloaded.

### Addressables.LoadAssetsAsync
- `static AsyncOperationHandle<IList<TObject>> LoadAssetsAsync<TObject>(object key, Action<TObject> callback)`
- `static AsyncOperationHandle<IList<TObject>> LoadAssetsAsync<TObject>(object key, Action<TObject> callback, bool releaseDependenciesOnFailure)`
- `static AsyncOperationHandle<IList<TObject>> LoadAssetsAsync<TObject>(IEnumerable keys, Action<TObject> callback, MergeMode mode)`
- `static AsyncOperationHandle<IList<TObject>> LoadAssetsAsync<TObject>(IEnumerable keys, Action<TObject> callback, MergeMode mode, bool releaseDependenciesOnFailure)`
- `static AsyncOperationHandle<IList<TObject>> LoadAssetsAsync<TObject>((IList<IResourceLocation> locations, Action<TObject> callback))`
- `static AsyncOperationHandle<IList<TObject>> LoadAssetsAsync<TObject>((IList<IResourceLocation> locations, Action<TObject> callback, bool releaseDependenciesOnFailure))`

#### Returns
`AsyncOperationHandle<IList<TObject>>` - The operation handle for a list of requested assets.

#### Description
`Addressables.LoadAssetsAsync` uses the keys of multiple Addressable objects or direct ResourceLocations to load an Addressable asset of a specified type.  You can access the loaded asset through the Result property of the `AsyncOperationHandle` returned by the function.  This API does the following:
1) Gathers all dependencies for the given key or resource location.  The key version of the API incurs an additional step to lookup the first IResourceLocation that matches the provided key.
2) Downloads any remote AssetBundles that are required.
3) Loads the AssetBundles into memory.
4) Populates the `Result` property with the requested objects that successfully loaded or null.

Just a quick note about step 4: Whether the `Result` is populated with successful objects or null is dependant on the use of the `bool releaseDependenciesOnFailure` parameter provided.  If you pass `true` into the parameter, the `Result` property is populated with null if any of the requested objects fail to load.  Passing `false` into this parameter populates the `Result` with any objects that were successfully loaded, even if some failed.  If this parameter is not specified then the default value `true` is used.

Another useful parameter that can be used with `LoadAssetsAsync` is the `MergeMode` parameter.  The result of your load operation can change based on what's passed in.

`Addressables.LoadAssetsAsync` is also useful when used in conjunction with the Addressable label feature.  If a label is passed in as the key, `Addressables.LoadAssetsAsync` loads every asset marked with that label.

Similar to the single load APIs, you must properly manage and release the `AsyncOperationHandle` that these calls return when they are no longer needed. This ensures that AssetBundles do not remain loaded in memory unnecessarily. Also, if there are multiple requests for the same assets, then the reference count for the underlying internal operation increases and the cached load operation is used. This means that all newly created handles need to ensure they are properly released in order to have the `AssetBundles` unload.

The `callback` parameter is called individually for each asset loaded by the operation.  Given that the load operation are asynchronous the order of execution is not guaranteed.  Passing in a list of keys `"key1", "key2", "key3"` does not determine the order that the callback operates on each of these loaded assets.

```
MergeMode.None - Takes the results from the first key.
MergeMode.UseFirst - Takes the results from the first key.
MergeMode.Union - Takes results of each key and collects items that matched any key.
MergeMode.Intersection - Takes results of each key, and collects items that matched every key.
```

If there are any issues during the excecution of these operations, an Exception is generated and returned in the operations `OperationException`. That exception is also logged as an error by default. If you would like to see the exceptions when they happen, process them, and choose how to handle them, see [`ResourceManager.ExceptionHandler`](ExceptionHandler.md).

#### Code Sample
```
IEnumerator LoadAllLocations(List<IResourceLocation> locations)
{
    //Will load all assets for the provided IResourceLocations
    AsyncOperationHandle<IList<GameObject>> loadWithIResourceLocations =
            Addressables.LoadAssetsAsync<GameObject>(locations,
                obj =>
                {
                    //Gets called for every loaded asset
                    Debug.Log(obj.name);
                });
        yield return loadWithIResourceLocations;
        IList<GameObject> loadWithLocationsResult = loadWithIResourceLocations.Result;

        //Will load all assets for the provided IResourceLocations
        //With false passed in as the last parameter the Result will be populated with
        //objects that could be successfully loaded, even if others could not.
        AsyncOperationHandle<IList<GameObject>> doNotReleaseOnFailWithIResourceLocations =
            Addressables.LoadAssetsAsync<GameObject>(locations,
                obj =>
                {
                    //Gets called for every loaded asset
                    Debug.Log(obj.name);
                }, false);
        yield return doNotReleaseOnFailWithIResourceLocations;
        IList<GameObject> multipleKeyResult = doNotReleaseOnFailWithIResourceLocations.Result;

        //Use this only when the objects are no longer needed
        Addressables.Release(loadWithIResourceLocations);
        Addressables.Release(doNotReleaseOnFailWithIResourceLocations);
}

IEnumerator LoadAllAssetsByKey()
{
    //Will load all objects that match the given key.
    //If this key is an Addressable label, it will load all assets marked with that label
    AsyncOperationHandle<IList<GameObject>> loadWithSingleKeyHandle = Addressables.LoadAssetsAsync<GameObject>("objectKey", obj =>
    {
        //Gets called for every loaded asset
        Debug.Log(obj.name);
    });
    yield return loadWithSingleKeyHandle;
    IList<GameObject> singleKeyResult = loadWithSingleKeyHandle.Result;

    //Loads all assets that match the list of keys.
    //With no MergeMode parameter specified, the Result will be that of the first key.
    AsyncOperationHandle<IList<GameObject>> loadWithMultipleKeys =
        Addressables.LoadAssetsAsync<GameObject>(new string[] { "key1", "key2" },
            obj =>
            {
                //Gets called for every loaded asset
                Debug.Log(obj.name);
            });
    yield return loadWithMultipleKeys;
    IList<GameObject> multipleKeyResult1 = loadWithMultipleKeys.Result;

    //This will load the assets that match the given keys and populate the Result
    //with only objects that match both of the provided keys
    AsyncOperationHandle<IList<GameObject>> intersectionWithMultipleKeys =
        Addressables.LoadAssetsAsync<GameObject>(new string[] { "key1", "key2" },
            obj =>
            {
                //Gets called for every loaded asset
                Debug.Log(obj.name);
            }, Addressables.MergeMode.Intersection);
    yield return intersectionWithMultipleKeys;
    IList<GameObject> multipleKeyResult2 = intersectionWithMultipleKeys.Result;

    //This will load all objects that match either of the provided keys since we passed in
    //MergeMode.Union.  It will also populate any successfully loaded objects into the
    //Result property even if others fail because of the final parameter being false.
    AsyncOperationHandle<IList<GameObject>> unionWithMultipleKeysDoNotRelease =
        Addressables.LoadAssetsAsync<GameObject>(new string[] { "key1", "key2" },
            obj =>
            {
                //Gets called for every loaded asset
                Debug.Log(obj.name);
            }, Addressables.MergeMode.Union, false);
    yield return unionWithMultipleKeysDoNotRelease;
    IList<GameObject> multipleKeyResult3 = unionWithMultipleKeysDoNotRelease.Result;

    //Use this only when the objects are no longer needed
    Addressables.Release(loadWithSingleKeyHandle);
    Addressables.Release(loadWithMultipleKeys);
    Addressables.Release(intersectionWithMultipleKeys);
    Addressables.Release(unionWithMultipleKeysDoNotRelease);
}
```

#### Pitfalls
Loading a GameObject through Addressables and then instantiating it through the standard `Object.Instantiate(...)` method can have potentially disastrous results.  When loading the asset, only the load operation contains any knowledge of a reference count.  If you release this operation handle prior to destroying your GameObject instance, all the data (materials, textures, etc.) is unloaded out from underneath the object when the AssetBundle is unloaded.

When loading with a list of keys, be sure to pass it in as an `IList<object>` even if all the keys are strings. Because of the method overloads and casting, passing in a list that isn't typed to object uses the static `AsyncOperationHandle<IList<TObject>> LoadAssetsAsync<TObject>(object key, Action<TObject> callback)` overload.

Because of the asynchronous nature of loading, the order of objects in the Result is done in no certain order. Should you need to coordinate the key of an object and its result, you need to correlate those manually.
#### Example
```
IEnumerator LoadAndStoreResult()
    {
        List<GameObject> associationDoesNotMatter = new List<GameObject>();

        AsyncOperationHandle<IList<GameObject>> handle =
            Addressables.LoadAssetsAsync<GameObject>("label", obj => associationDoesNotMatter.Add(obj));
        yield return handle;
    }

    IEnumerator LoadAndAssociateResultWithKey()
    {
        AsyncOperationHandle<IList<IResourceLocation>> locations = Addressables.LoadResourceLocationsAsync("label");
        yield return locations;

        Dictionary<string, GameObject> associationDoesMatter = new Dictionary<string, GameObject>();

        foreach (IResourceLocation location in locations.Result)
        {
            AsyncOperationHandle<GameObject> handle =
                Addressables.LoadAssetAsync<GameObject>(location);
            handle.Completed += obj => associationDoesMatter.Add(location.PrimaryKey, obj.Result);
            yield return handle;
        }
    }
```
