# Async operation handling
Several methods from the [`Addressables`](../api/UnityEngine.AddressableAssets.Addressables.html) API return an [`AsyncOperationHandle`](../api/UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle-1.html) struct. The main purpose of this handle is to allow access to the status and result of an operation. The result of the operation is valid until you call [`Addressables.Release`](../api/UnityEngine.AddressableAssets.Addressables.html#UnityEngine_AddressableAssets_Addressables_Release_UnityEngine_ResourceManagement_AsyncOperations_AsyncOperationHandle_) or [`Addressables.ReleaseInstance`](../api/UnityEngine.AddressableAssets.Addressables.html#UnityEngine_AddressableAssets_Addressables_ReleaseInstance_GameObject_) with the operation (for more information on releasing assets, see documentation on [memory management](MemoryManagement.md).

When the operation completes, the [`AsyncOperationHandle.Status`](../api/UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle.html) property is either [`AsyncOperationStatus.Succeeded`](../api/UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.html) or [`AsyncOperationStatus.Failed`](../api/UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.html). If successful, you can access the result through the `AsyncOperationHandle.Result` property.

You can either check the operation status periodically, or register for a completed callback using the `AsyncOperationHandle.Complete` event. When you no longer need the asset provided by a returned `AsyncOperationHandle` struct, you should [release](MemoryManagement.md) it using the [`Addressables.Release`](../api/UnityEngine.AddressableAssets.Addressables.html#UnityEngine_AddressableAssets_Addressables_Release_UnityEngine_ResourceManagement_AsyncOperations_AsyncOperationHandle_) method.

### Type vs. typeless handles
Most [`Addressables`](../api/UnityEngine.AddressableAssets.Addressables.html) API methods return a generic [`AsyncOperationHandle<T>`](../api/UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle-1.html) struct, allowing type safety for the [`AsyncOperationHandle.Completed`](../api/UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle-1.html#UnityEngine_ResourceManagement_AsyncOperations_AsyncOperationHandle_1_Completed) event, and for the [`AsyncOperationHandle.Result`](../api/UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle-1.html#UnityEngine_ResourceManagement_AsyncOperations_AsyncOperationHandle_1_Result) object. There is also a non-generic [`AsyncOperationHandle`](../api/UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle.html) struct, and you can convert between the two handles as desired. 

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
Register a listener for completion events using the [`AsyncOperationHandle.Completed`](../api/UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle-1.html#UnityEngine_ResourceManagement_AsyncOperations_AsyncOperationHandle_1_Completed) callback:

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

[`AsyncOperationHandle`](../api/UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle-1.html) implements [`IEnumerator`](../api/UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle-1.html#eii) so it can be yielded in coroutines:

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

Addressables also supports asynchronous `await` through the [`AsyncOperationHandle.Task`](../api/UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle-1.html#UnityEngine_ResourceManagement_AsyncOperations_AsyncOperationHandle_1_Task) property:

```
public async Start() {
    AsyncOperationHandle<Texture2D> handle = Addressables.LoadAssetAsync<Texture2D>("mytexture");
    await handle.Task;
    // The task is complete. Be sure to check the Status is successful before storing the Result.
}
```
The `AsyncOperationHandle.Task` property is not available on `WebGL` as multi-threaded operations are not supported on that platform.

Note that Loading scenes with [`SceneManager.LoadSceneAsync`](https://docs.unity3d.com/ScriptReference/SceneManagement.SceneManager.LoadSceneAsync.html) with `allowSceneActivation` set to false or using [`Addressables.LoadSceneAsync`](../api/UnityEngine.AddressableAssets.Addressables.html#UnityEngine_AddressableAssets_Addressables_LoadSceneAsync_System_Object_LoadSceneMode_System_Boolean_System_Int32_) and setting false for the `activateOnLoad` parameter can lead to subsequent async operations being blocked and unable to complete.  See the [`allowSceneActivation` documentation](https://docs.unity3d.com/ScriptReference/AsyncOperation-allowSceneActivation.html).
