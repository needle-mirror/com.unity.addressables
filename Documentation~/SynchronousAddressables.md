## Synchronous Workflow
Synchronous Addressables APIs help to more closely mirror Unity asset loading workflows.  `AsyncOperationHandles` now have a method called `WaitForCompletion()` that force the async operation to complete and return the `Result` of the operation.

## API
`TObject WaitForCompletion()`

## Result
The result of `WaitForCompletion` is the `Result` of the async operation it is called on.  If the operation fails, this returns `default(TObject)`.

It is possible to get a `default(TObject)` for a result when the operation doesn't fail.  Async operations that auto release their `AsyncOperationHandles` on completion are such cases.  `Addressables.InitializeAsync()` and any API with a `autoReleaseHandle` parameter set to true will return `default(TObject)` even though the operations themselves succeeded.

## Performance
It is worth noting that calling `WaitForCompletion` may have performance implications on your runtime when compared to `Resources.Load` or `Instantiate` calls directly.  If your `AssetBundle` is local or has been previously downloaded and cached, these performance hits are likely to be negligible.  However, this may not be the case for your individual project setup.

All currently active Asset Load operations are completed when `WaitForCompletion` is called on any Asset Load operation, due to how Async operations are handled in the Engine. To avoid unexpected stalls, use `WaitForCompletion` when the current operation count is known, and the intention is for all active operations to complete synchronously.

When using `WaitForCompletion`, there are performance implications. When using 2021.2.0 or newer, these are minimal. Using an older version can result in delays that scale with the number of Engine Asset load calls that are loading when `WaitForCompletion` is called.

It is not recommended that you call `WaitForCompletion` on an operation that is going to fetch and download a remote `AssetBundle`.  Though, it is possible if that fits your specific situation. 

## Code Sample
```
void Start()
{
    //Basic use case of forcing a synchronous load of a GameObject
    var op = Addressables.LoadAssetAsync<GameObject>("myGameObjectKey");
    GameObject go = op.WaitForCompletion();
    
    //Do work...
    
    Addressables.Release(op);
}
```

### Scenes
Due to engine limitations scenes cannot be completed synchronously. Calling WaitForCompletion on an operation returned from [Addressables.LoadSceneAsync] will not completely load the scene, even if activateOnLoad is set to true. It will wait for dependencies and assets to complete but the scene activation must be done asynchronously. This can be done using the sceneHandle, or by the [AsyncOperation] from ActivateAsync on the SceneInstance as shown below.

```c#
IEnumerator LoadScene(string myScene)
{
    var sceneHandle = Addressables.LoadSceneAsync(myScene, LoadSceneMode.Additive);
    SceneInstance sceneInstance = sceneHandle.WaitForCompletion();
    yield return sceneInstance.ActivateAsync();
    
    //Do work... the scene is now complete and integrated
}
```

> [!NOTE]
> Unloading a scene cannot be completed synchronously. Calling WaitForCompleted on a scene unload will not unload the scene or any assets, and a warning will be logged to the console.

### Synchronous Addressables with Custom Operations
Addressables supports custom `AsyncOperations` which support unique implementations of `InvokeWaitForCompletion`.  This overridable method is what you'll use to implement custom synchronous operations.

Custom operations work with `ChainOperations` and `GroupsOperations`.  If you require chained operations to be completed synchronously, ensure that your custom operations implement `InvokeWaitForCompletion` and create a `ChainOperation` using your custom operations.  Similarly, `GroupOperations` are well suited to ensure a collection of `AsyncOperations`, including custom operations, complete together.  Both `ChainOperation` and `GroupOperation` have their own implementations of `InvokeWaitForCompletion` that relies on the `InvokeWaitForCompletion` implementations of the operations they depend on.

### WebGL
WebGL does not support `WaitForCompletion`.  On WebGL, all files are loaded using a web request.  On other platforms, a web request gets started on a background thread and the main thread spins in a tight loop while waiting for the web request to finish.  This is how Addressables does it for `WaitForCompletion` when a web request is used.

Since WebGL is single-threaded, the tight loop blocks the web request and the operation is never allowed to finish.  If a web request finishes the same frame it was created, then `WaitForCompletion` wouldn't have any issue.  However, we cannot guarantee this to be the case, and likely it isn't the case for most instances.

[AsyncOperation.allowSceneActivation]: xref:UnityEngine.AsyncOperation.allowSceneActivation
[AsyncOperation]: xref:UnityEngine.AsyncOperation
[Addressables.LoadSceneAsync]: xref:UnityEngine.AddressableAssets.Addressables.LoadSceneAsync*
[Addressables.UnloadSceneAsync]: xref:UnityEngine.AddressableAssets.Addressables.UnloadSceneAsync