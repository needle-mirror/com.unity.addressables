---
uid: addressables-api-load-resource-locations-async
---
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

#### Sub-Objects
Sub-Objects are a special case.  Locations for Sub-Objects are generated at runtime to keep bloat out of the content catalogs and improve runtime performance, such as entering Play Mode while using the Use Asset Database Playmode script.  This has implications when calling `LoadResourceLocationsAsync` with a Sub-Object key.  If the system is not aware of the desired Type then IResourceLocations is generated for each `Type` of Sub-Object detected.  If an AssetReference with a Sub-Object selection is not set, the system generates IResourceLocations for each Type of detected Sub-Object with the Address of the main object.

For the following examples lets assume we have an FBX asset marked as Addressable that has a Mesh Sub-Object.

##### When passing a string Key:
```
IEnumerator Start()
{
    AsyncOperationHandle<IList<IResourceLocation>> handle = Addressables.LoadResourceLocationsAsync("myFBXObject");
	yield return handle;
	
	//This result contains 3 IResourceLocations.  One with Type GameObject, one with Type Mesh, and one with Type Material.  Since the string Key has no Type information we generate all possible IResourceLocations to match the request.
	IList<IResourceLocation> result = handle.Result;
	
	//...
	
	Addressables.Release(handle);
}
```

```
IEnumerator Start()
{
    AsyncOperationHandle<IList<IResourceLocation>> handle = Addressables.LoadResourceLocationsAsync("myFBXObject[Mesh]");
	yield return handle;
	
	//This result contains 3 IResourceLocations.  One with Type GameObject, one with Type Mesh, and one with Type Material.  Since the string Key has no Type information we generate all possible IResourceLocations to match the request.
	IList<IResourceLocation> result = handle.Result;
	
	//...
	
	Addressables.Release(handle);
}
```

```
IEnumerator Start()
{
    AsyncOperationHandle<IList<IResourceLocation>> handle = Addressables.LoadResourceLocationsAsync("myFBXObject[Mesh]", typeof(Mesh));
	yield return handle;
	
	//This result contains 1 IResourceLocation.  Since the Type parameter has a value passed in we can create the IResourceLocation.
	IList<IResourceLocation> result = handle.Result;
	
	//...
	
	Addressables.Release(handle);
}
```

##### When using an AssetReference:
```
//An AssetReference set to point to the Mesh Sub-Object of a FBX asset
public AssetReference myFBXMeshReference;

IEnumerator Start()
{
    AsyncOperationHandle<IList<IResourceLocation>> handle = Addressables.LoadResourceLocationsAsync(myFBXMeshReference);
	yield return handle;
	
	//This result contains 1 IResourceLocation.  Since the AssetReference contains Type information about the Sub-Object, we can generate the appropriate IResourceLocation.
	IList<IResourceLocation> result = handle.Result;
	
	//...
	
	Addressables.Release(handle);
}
```

```
//An AssetReference that is not set to point at a Sub-Object
public AssetReference myFBXReference;

IEnumerator Start()
{
    AsyncOperationHandle<IList<IResourceLocation>> handle = Addressables.LoadResourceLocationsAsync(myFBXReference);
	yield return handle;
	
	//This result contains 3 IResourceLocation.  Since the AssetReference Sub-Object is not set we generate all possible IResourceLocations with the detected Sub-Object Types.
	IList<IResourceLocation> result = handle.Result;
	
	//...
	
	Addressables.Release(handle);
}
```