---
uid: addressables-async-operation-handling
---
# Async operation handling
Several methods from the [`Addressables`](xref:UnityEngine.AddressableAssets.Addressables) API return an [`AsyncOperationHandle`](xref:UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle) struct. The main purpose of this handle is to allow access to the status and result of an operation. The result of the operation is valid until you call [`Addressables.Release`](xref:UnityEngine.AddressableAssets.Addressables.Release(UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle)) or [`Addressables.ReleaseInstance`](xref:UnityEngine.AddressableAssets.Addressables.ReleaseInstance(UnityEngine.GameObject)) with the operation (for more information on releasing assets, see documentation on [memory management](MemoryManagement.md).

When the operation completes, the [`AsyncOperationHandle.Status`](xref:UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle) property is either [`AsyncOperationStatus.Succeeded`](xref:UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus) or [`AsyncOperationStatus.Failed`](xref:UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus). If successful, you can access the result through the `AsyncOperationHandle.Result` property.

You can either check the operation status periodically, or register for a completed callback using the `AsyncOperationHandle.Complete` event. When you no longer need the asset provided by a returned `AsyncOperationHandle` struct, you should [release](MemoryManagement.md) it using the [`Addressables.Release`](xref:UnityEngine.AddressableAssets.Addressables.Release(UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle)) method.

### Type vs. typeless handles
Most [`Addressables`](xref:UnityEngine.AddressableAssets.Addressables) API methods return a generic [`AsyncOperationHandle<T>`](xref:UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle`1) struct, allowing type safety for the [`AsyncOperationHandle.Completed`](xref:UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle.Completed) event, and for the [`AsyncOperationHandle.Result`](xref:UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle.Result) object. There is also a non-generic [`AsyncOperationHandle`](xref:UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle) struct, and you can convert between the two handles as desired. 

Note that a runtime exception occurs if you attempt to cast a non-generic handle to a generic handle of an incorrect type. For example:

```
AsyncOperationHandle<Texture2D> textureHandle = Addressables.LoadAssetAsync<Texture2D>("mytexture");

// Convert the AsyncOperationHandle<Texture2D> to an AsyncOperationHandle:
AsyncOperationHandle nonGenericHandle = textureHandle;

// Convert the AsyncOperationHandle to an AsyncOperationHandle<Texture2D>:
AsyncOperationHandle<Texture2D> textureHandle2 = nonGenericHandle.Convert<Texture2D>();

// This will throw and exception because Texture2D is required:
AsyncOperationHandle<Texture> textureHandle3 = nonGenericHandle.Convert<Texture>();
```

### AsyncOperationHandle use case examples
Register a listener for completion events using the [`AsyncOperationHandle.Completed`](xref:UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle.Completed) callback:

```
private void TextureHandle_Completed(AsyncOperationHandle<Texture2D> handle) {
    if (handle.Status == AsyncOperationStatus.Succeeded) {
        Texture2D result = handle.Result;
        // The texture is ready for use.
    }
}

void Start() {
    AsyncOperationHandle<Texture2D> textureHandle = Addressables.LoadAsset<Texture2D>("mytexture");
    textureHandle.Completed += TextureHandle_Completed;
}
```

[`AsyncOperationHandle`](xref:UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle`1) implements [`IEnumerator`](xref:System.Collections.IEnumerator) so it can be yielded in coroutines:

```
public IEnumerator Start() {
    AsyncOperationHandle<Texture2D> handle = Addressables.LoadAssetAsync<Texture2D>("mytexture");
	
	//if the handle is done, the yield return will still wait a frame, but we can skip that with an IsDone check
	if(!handle.IsDone)
		yield return handle;
		
    if (handle.Status == AsyncOperationStatus.Succeeded) {
        Texture2D texture = handle.Result;
        // The texture is ready for use.
        // ...
	// Release the asset after its use:
        Addressables.Release(handle);
    }
}
```

Addressables also supports asynchronous `await` through the [`AsyncOperationHandle.Task`](xref:UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle.Task) property:

```
public async Start() {
    AsyncOperationHandle<Texture2D> handle = Addressables.LoadAssetAsync<Texture2D>("mytexture");
    await handle.Task;
    // The task is complete. Be sure to check the Status is successful before storing the Result.
}
```
The `AsyncOperationHandle.Task` property is not available on `WebGL` as multi-threaded operations are not supported on that platform.

Note that Loading scenes with [`SceneManager.LoadSceneAsync`](https://docs.unity3d.com/ScriptReference/SceneManagement.SceneManager.LoadSceneAsync.html) with `allowSceneActivation` set to false or using [`Addressables.LoadSceneAsync`](xref:UnityEngine.AddressableAssets.Addressables.LoadSceneAsync(System.Object,UnityEngine.SceneManagement.LoadSceneMode,System.Boolean,System.Int32)) and setting false for the `activateOnLoad` parameter can lead to subsequent async operations being blocked and unable to complete.  See the [`allowSceneActivation` documentation](https://docs.unity3d.com/ScriptReference/AsyncOperation-allowSceneActivation.html).

##### Loading Addressable Scenes
When loading an Addressable Scene, all the dependencies for your GameObjects in the scene are accessed through AssetBundles loaded during the Scene load operation.  Assuming no other objects reference the associated AssetBundles, when the Scene is unloaded, all the AssetBundles, both for the Scene and any that were needed for dependencies, are unloaded.

Note: If you mark a GameObject in an Addressable loaded scene as `DontDestroyOnLoad` or move it to another loaded Scene and then unload your original Scene, all dependencies for your GameObject are still unloaded.

If you find yourself in that scenario there are a couple options at your disposal.
- Make the GameObject you want to be `DontDestroyOnLoad` a single Addressable prefab.  Instantiate the prefab when you need it and then mark it as `DontDestroyOnLoad`.
- Before unloading the Scene that contained the GameObject you mark as `DontDestroyOnLoad`, call `Addressables.ResourceManager.Acquire(AsyncOperationHandle)` and pass in the Scene load handle.  This increases the reference count on the Scene, and keeps it and its dependencies loaded until `Release` is called on the acquired handle.

