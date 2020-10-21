---
uid: addressables-api-load-scene-async
---
# Addressables.LoadSceneAsync
#### API
- `AsyncOperationHandle<SceneInstance> LoadSceneAsync(object key, LoadSceneMode loadMode = LoadSceneMode.Single, bool activateOnLoad = true, int priority = 100)`
- `AsyncOperationHandle<SceneInstance> LoadSceneAsync(IResourceLocation location, LoadSceneMode loadMode = LoadSceneMode.Single, bool activateOnLoad = true, int priority = 100)`

#### Returns
`AsyncOperationHandle<SceneInstance>`: An `AsyncOperationHandle` for the scene load operation.  The result is the loaded `SceneInstance`.

#### Description
Load a scene asynchronously.  See the full API documentation of [`Addressables.LoadSceneAsync`](xref:UnityEngine.AddressableAssets.Addressables).

`Addressables.LoadSceneAsync` uses a key or `IResourceLocation` to load an Addressable scene.  The other parameters, such as `loadMode`, `activateOnLoad`, and `priority` correlate to parameters used by the `SceneManager.LoadSceneAsync`.  More information about `priority` and `activateOnLoad` (called `allowSceneActivation` by `AsyncOperation`) can be found in the full API documentation for [`SceneManagement.SceneManager.LoadSceneAsync`](https://docs.unity3d.com/ScriptReference/SceneManagement.SceneManager.LoadSceneAsync.html),

Be aware, if `activateOnLoad` is set to `false`, the entire async operation queue is stalled behind the blocked scene load operation.  This is not limited to scene load requests.  Any asynchronous request is blocked until the scene is allowed to activate.  This means `yield retruns` and callbacks cannot trigger until the queue is unblocked.  If multiple scene load operations are started with `activateOnLoad=false` it can lead to inconsistent behavior.  For example,

```
IEnumerator Start()
{
    AsyncOperationHandle<SceneInstance> handle1 = Addressables.LoadSceneAsync("level1", LoadSceneMode.Additive, false);
    AsyncOperationHandle<SceneInstance> handle2 = Addressables.LoadSceneAsync("other", LoadSceneMode.Additive, false);

    yield return handle1;  //will sometimes trigger, sometimes not
    yield return handle2;  //will never trigger
}
```
Whichever of the two loads gets to "loaded but not activated" first blocks the queue and fires its completion/yield/task events. In this case, if "level1" loads first, its `yield return` proceeds, but execution is stuck on `handle2` forever.  If "other" loads first, then even though `handle2` is done, `handle1` cannot finish. Which scene loads first has nothing to do with which one is requested first in code.

While `Addressables.LoadSceneAsync` is asynchronous, it should be noted that the final part of scene loading requires operation on the main thread.  This can be blocking.

If the `loadMode` passed in is `LoadSceneMode.Single` then `Resources.UnloadUnusedAssets` is called to clear memory.  This can cause hitching.

#### Code Sample
```
public IEnumerator Start()
{
    //Simple use case for loading a scene with the key "level1"
    AsyncOperationHandle<SceneInstance> handle = Addressables.LoadSceneAsync("level1");
    yield return handle;
    
    //....
}
```
```
IEnumerator Start()
{
    //Not allowing scene activation immediately
    AsyncOperationHandle<SceneInstance> handle = Addressables.LoadSceneAsync("level1", LoadSceneMode.Additive, false);
    yield return handle;

    //...

    //One way to handle manual scene activation.
    if (handle.Status == AsyncOperationStatus.Succeeded)
        yield return handle.Result.ActivateAsync();

    //...
}
```

#### DontDestroyOnLoad
If a scene is loaded through `Addressables` and a `GameObject` is moved out of that loaded scene, know that releasing that scene load handle unloads the underlying `AssetBundle` and can affect the moved `GameObjects`.  A common use case is marking a `GameObject` as `DontDestroyOnLoad`.  Since `Addressables` has no way of knowing an object was moved out of the loaded scene, it is unable to track the moved objects with the underlying reference count on the loaded `AssetBundle`. 

Two solutions for this problem are:
- Make any object you want to mark `DontDestroyOnLoad` its own `Addressable` asset and load it independently.
- Call `Addressables.ResourceManager.Acquire` on the `AsyncOperationHandle<SceneInstance>` used to load the scene before unloading the scene.  This bumps the reference count and keeps the entire `AssetBundle` loaded into memory after the scene is unloaded.  You are responsible for releasing the `AsyncOperationHandle<SceneInstance>` after `Acquire` has been called.