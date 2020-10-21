---
uid: addressables-api-instantiate-async
---
# Addressables.InstantiateAsync
#### API
- `static AsyncOperationHandle<GameObject> InstantiateAsync(IResourceLocation location, Transform parent = null, bool instantiateInWorldSpace = false, bool trackHandle = true)`
- `static AsyncOperationHandle<GameObject> InstantiateAsync(IResourceLocation location, Vector3 position, Quaternion rotation, Transform parent = null, bool trackHandle = true)`
- `static AsyncOperationHandle<GameObject> InstantiateAsync(IResourceLocation location, InstantiationParameters instantiateParameters, bool trackHandle = true)`
- `static AsyncOperationHandle<GameObject> InstantiateAsync(object key, Transform parent = null, bool instantiateInWorldSpace = false, bool trackHandle = true)`
- `static AsyncOperationHandle<GameObject> InstantiateAsync(object key, Vector3 position, Quaternion rotation, Transform parent = null, bool trackHandle = true)`
- `static AsyncOperationHandle<GameObject> InstantiateAsync(object key, InstantiationParameters instantiateParameters, bool trackHandle = true)`

#### Returns
`AsyncOperationHandle<GameObject>`: an async operation handle that contains the instantiated `GameObject` as the `Result`.

#### Description
`Addressables.InstantiateAsync` is the `Addressables` mechanism used to instantiate `GameObjects` by either key or direct `IResourceLocation`.

There are multiple ways `Addressables` can be used to instantiate a `GameObject`, each with their own set of pros and cons.  The main two ways are using the `Addressables.InstanitateAsync` API directly, or using the `Addressables.LoadAssetAsync<GameObject>` API and then manually instantiating the `GameObject` yourself.

If you are instantiating a `GameObject` through the `InstanitateAsync` API, you have the convenience of allowing `Addressables` to do all the work of resolving the `IResourceLocation` if the key version of the API is used.  Regardless which version of the API is used, `Addressables` downloads all the required dependencies, and instantiates the object for you.  As a note, the instantation itself is synchronous.  The asynchronous aspect of this API comes from all the loading-related activity `Addressables` does prior to instantiation.  If the `GameObject` has been preloaded using `LoadAssetAsync` or `LoadAssetsAsync` the operation and instantiation becomes synchrounous.  For example:
```
IEnumerator Start()
{
    string key = "myprefab";
    AsyncOperationHandle<GameObject> loadOp = Addressables.LoadAssetAsync<GameObject>(key);
    yield return loadOp;
    if (loadOp.Status == AsyncOperationStatus.Succeeded)
    {
        var op = Addressables.InstantiateAsync(key);
        if (op.IsDone) // <--- this will always be true.  A preloaded asset will instantiate synchronously. 
        {
            //...
        }
        //...
    }
}
```

The downside to using this API is that it incurs overhead that can be mitigated by handing instantiation manually.

Should you decide to synchronously instantiate a `GameObject` manually, you'll need to use the [`LoadAssetAsync`](LoadingAddressableAssets.md) API to first load the required assets and dependencies.  With the loaded `GameObject`, you can manually instantiate the `Result` like so:
```
public IEnumerator Start()
{
    AsyncOperationHandle<GameObject> handle = Addressables.LoadAssetAsync<GameObject>("prefabKey");
    yield return handle;
    if(handle.Result != null)
        Instantiate(handle.Result);
    
    //,,.
}
```
This has the added benefit of being able to keep the load operation handle in memory and instantiating the `Result` as often as needed without incuring additional unwanted overhead.  One downside is it's be possible to release the load handle too early and unload all the data needed by any currently instantiated prefabs.

If there are any issues during the excecution of these operations, an Exception is generated and returned in the operations `OperationException`. That exception is also logged as an error by default. If you would like to see the exceptions when they happen, process them, and choose how to handle them, see [`ResourceManager.ExceptionHandler`](ExceptionHandler.md).

Either use of instantiation can benefit from information found in the [Memory Management](MemoryManagement.md) manual page.

#### Code Sample
```
public IEnumerator Start()
{
    AsyncOperationHandle<GameObject> handle = Addressables.InstantiateAsync("prefabKey");
    yield return handle;
    
    //...
}
```