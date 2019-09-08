# Async operation handling
Several methods from the `Addressables` API return an `AsyncOperationHandle` struct. The main purpose of this handle is to allow access to the status and result of an operation. The result of the operation is valid until you call `Addressables.Release` or `Addressables.ReleaseInstance` with the operation (for more information on releasing assets, see documentation on [memory management](MemoryManagement.md).

When the operation completes, the `AsyncOperationHandle.Status` property is either `AsyncOperationStatus.Succeeded` or `AsyncOperationStatus.Failed`. If successful, you can access the result through the `AsyncOperationHandle.Result` property.

You can either check the operation status periodically, or register for a completed callback using the `AsyncOperationHandle.Complete` event. When you no longer need the asset provided by a returned `AsyncOperationHandle` struct, you should [release](MemoryManagement.md) it using the `Addressables.Release` method.

### Type vs typeless handles
Most `Addressables` API methods return a generic `AsyncOperationHandle<T>`struct, allowing type safety for the `AsyncOperationHandle.Completed` event, and for the `AsyncOperationHandle.Result` object. There is also a non-generic `AsyncOperationHandle` struct, and you can convert between the two handles as desired. 

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
Register a listener for completion events using the `AsyncOperationHandle.Completed` callback:

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

`AsyncOperationHandle` implements `IEnumerator` so it can be yielded in coroutines:

```
public IEnumerator Start() {
    AsyncOperationHandle<Texture2D> handle = Addressables.LoadAssetAsync<Texture2D>("mytexture");
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

Addressables also supports asynchronous `await` through the `AsyncOperationHandle.Task` property:

```
public async Start() {
    AsyncOperationHandle<Texture2D> handle = Addressables.LoadAssetAsync<Texture2D>("mytexture");
    await handle.Task;
    // The task is complete. Be sure to check the Status is successful before storing the Result.
}
```

#### Please Note:
Loading scenes with `SceneManager.LoadSceneAsync` with `allowSceneActivation` set to false or using `Addressables.LoadSceneAsync` and passing in false for the `activateOnLoad` parameter can lead to subsequent async operations being blocked and unable to complete.  Please checkout the `allowSceneActivation` documentation here: https://docs.unity3d.com/ScriptReference/AsyncOperation-allowSceneActivation.html
