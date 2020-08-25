# Addressables.LoadResourceLocationsAsync
#### API
- `static AsyncOperationHandle<IList<IResourceLocation>> LoadResourceLocationsAsync(object key, Type type = null)`
- `static AsyncOperationHandle<IList<IResourceLocation>> LoadResourceLocationsAsync(IEnumerable keys, MergeMode mode, Type type = null)`

#### Returns
`AsyncOperationHandle<IList<IResourceLocation>>`: A list of all `IResourceLocations` that match the given key and `Type` combination.  This handle needs to be released when it is no longer needed.

#### Description
Load a list of `IResourceLocations`.

`Addressables.LoadResourceLocationsAsync` is used to return either an empty list or a list of valid `IResourceLocations` that match the given key(s) and `Type` combination.  The `Result` can contain the `IResourceLocation` for varying `Types` if the given `Type` parameter is `null`.  

To note, this operation cannot fail.  If no matching `IResourceLocations` can be found then the `Result` is an empty list.  This makes the API useful for verifying that a key exists without fear of an `InvalidKeyException` getting thrown.

The `MergeMode` used in `LoadResourceLocationsAsync(IEnumerable keys, MergeMode mode, Type type = null)` helps to control what is returned in the `Result` of the operation.  `MergeMode.None` and `MergeMode.UseFirst` act identically.  Either of these options ensures the `Result` is only populated with the first `IResourceLocation` it could find that matches the given keys and `Type`.  `MergeMode.Union` collects every `IResourceLocation` that matches _any_ of the keys and `Type` provided and retuns it in the `Result`.  `MergeMode.Intersection` returns only the `IResourceLocations` that match _every_ key and `Type` provided in the `Result`.

It may also be desireable to pre-load `IResourceLocations` for use later on.  When the "Key" version of an API
```
static AsyncOperationHandle<IList<IResourceLocation>> LoadResourceLocationsAsync(object key, Type type = null)
``` 
is used (as opposed to passing the `IResourceLocation` directly), the `Addressable` system has to look up the `IResourceLocation` itself.  Pre-loading and then providing the location to an `Addressable` API can have performance benefits.

#### Code Sample
```
IEnumerator Start()
{
    //Returns any IResourceLocations that are mapped to the key "AssetKey"
    AsyncOperationHandle<IList<IResourceLocation>> handle = Addressables.LoadResourceLocationsAsync("AssetKey");
    yield return handle;
    
    //...
    
    Addressables.Release(handle);
}
```
```
IEnumerator Start()
{
    //Returns any IResourceLocations that match the keys "level2" AND "holiday"
    AsyncOperationHandle<IList<IResourceLocation>> handle = Addressables.LoadResourceLocationsAsync(new string[]
    {
        "level2",
        "holiday"
    }, Addressables.MergeMode.Intersection);
    yield return handle;
    
    //...

    Addressables.Release(handle);
}
```
```
IEnumerator Start()
{
    //Returns any IResourceLocations that match the keys "knight" OR "villager"
    AsyncOperationHandle<IList<IResourceLocation>> handle = Addressables.LoadResourceLocationsAsync(new string[]
    {
        "knight",
        "villager"
    }, Addressables.MergeMode.Union);
    yield return handle;
    
    //...

    Addressables.Release(handle);
}
```