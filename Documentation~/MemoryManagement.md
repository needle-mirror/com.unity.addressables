# Memory management
## Mirroring load and unload
When working with Addressable Assets, the primary way to ensure proper memory management is to mirror your load and unload calls correctly. How you do so depends on your asset types and load methods. In all cases, however, the release method can either take the loaded asset, or an operation handle returned by the load. For example, during Scene creation (described below) the load returns a [`AsyncOperationHandle<SceneInstance>`](../api/UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle.html), which you can release via this returned handle, or by keeping up with the `handle.Result` (in this case, a [`SceneInstance`](../api/UnityEngine.ResourceManagement.ResourceProviders.SceneInstance.html)).

### Asset loading
To load an asset, use [`Addressables.LoadAssetAsync`](../api/UnityEngine.AddressableAssets.Addressables.html#UnityEngine_AddressableAssets_Addressables_LoadAssetAsync__1_System_Object_) or [`Addressables.LoadAssetsAsync`](../api/UnityEngine.AddressableAssets.Addressables.html#UnityEngine_AddressableAssets_Addressables_LoadAssetsAsync__1_System_Collections_Generic_IList_System_Object__System_Action___0__UnityEngine_AddressableAssets_Addressables_MergeMode_).

This loads the asset into memory without instantiating it. Every time the load call executes, it adds one to the ref-count for each asset loaded. If you call [`LoadAssetAsync`](../api/UnityEngine.AddressableAssets.Addressables.html#UnityEngine_AddressableAssets_Addressables_LoadAssetAsync__1_System_Object_) three times with the same address, you will get three different instances of an [`AsyncOperationHandle`](../api/UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle.html) struct back, all referencing the same underlying operation. That operation has a ref-count of three for the corresponding asset. If the load succeeds, the resulting [`AsyncOperationHandle`](../api/UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle.html) struct contains the asset in the `.Result` property. You can use the loaded asset to instantiate using Unity's built-in instantiation methods, which does not increment the Addressables ref-count.

To unload the asset, use the [`Addressables.Release`](../api/UnityEngine.AddressableAssets.Addressables.html?q=addressables.release#UnityEngine_AddressableAssets_Addressables_Release__1___0_) method, which decrements the ref-count. When a given asset's ref-count is zero, that asset is ready to be unloaded, and decrements the ref-count of any dependencies. 

**Note**: The asset may or may not be unloaded immediately, contingent on existing dependencies. For more information, read the section on [when memory is cleared](#when-is-memory-cleared?). 

### Scene loading
To load a Scene, use [`Addressables.LoadSceneAsync`](../api/UnityEngine.AddressableAssets.Addressables.html?q=addressables.release#UnityEngine_AddressableAssets_Addressables_LoadSceneAsync_System_Object_LoadSceneMode_System_Boolean_System_Int32_). You can use this method to load a Scene in `Single` mode, which closes all open Scenes, or in `Additive` mode (for more information, see documentation on [Scene mode loading](https://docs.unity3d.com/ScriptReference/SceneManagement.LoadSceneMode.html).  

To unload a Scene, use [`Addressables.UnloadSceneAsync`](../api/UnityEngine.AddressableAssets.Addressables.html?q=addressables.release#UnityEngine_AddressableAssets_Addressables_UnloadSceneAsync_UnityEngine_ResourceManagement_AsyncOperations_AsyncOperationHandle_System_Boolean_), or open a new Scene in `Single` mode. You can open a new Scene by either using the Addressables interface, or using the [`SceneManager.LoadScene`](https://docs.unity3d.com/ScriptReference/SceneManagement.SceneManager.LoadScene.html) or [`SceneManager.LoadSceneAsync`](https://docs.unity3d.com/ScriptReference/SceneManagement.SceneManager.LoadSceneAsync.html) methods. Opening a new Scene closes the current one, properly decrementing the ref-count.

### GameObject instantiation
To load and instantiate a GameObject asset, use [`Addressables.InstantiateAsync`](../api/UnityEngine.AddressableAssets.Addressables.html?q=instantiate.async#UnityEngine_AddressableAssets_Addressables_InstantiateAsync_System_Object_Transform_System_Boolean_System_Boolean_). This instantiates the Prefab located by the specified `location` parameter. The Addressables system will load the Prefab and its dependencies, incrementing the ref-count of all associated assets. 

Calling [`InstantiateAsync`](../api/UnityEngine.AddressableAssets.Addressables.html?q=instantiate.async#UnityEngine_AddressableAssets_Addressables_InstantiateAsync_System_Object_Transform_System_Boolean_System_Boolean_) three times on the same address results in all dependent assets having a ref-count of three. Unlike calling [`LoadAssetAsync`](../api/UnityEngine.AddressableAssets.Addressables.html?q=instantiate.async#UnityEngine_AddressableAssets_Addressables_LoadAssetAsync__1_System_Object_) three times, however, each `InstantiateAsync` call returns an [`AsyncOperationHandle`](../api/UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle-1.html) pointing to a unique operation.  This is because the result of each `InstantiateAsync` is a unique instance. You would need to individually release each returned [`AsyncOperationHandle`](../api/UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle.html) or GameObject instance. Another distinction between `InstantiateAsync` and other load calls is the optional `trackHandle` parameter. When set to `false`, you must keep the [`AsyncOperationHandle`](../api/UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle.html) to use while releasing your instance. This is more efficient, but requires more development effort.

To destroy an instantiated GameObject, use [`Addressables.ReleaseInstance`](../api/UnityEngine.AddressableAssets.Addressables.html?q=instantiate.async#UnityEngine_AddressableAssets_Addressables_ReleaseInstance_GameObject_), or close the Scene that contains the instantiated object. This Scene can have been loaded (and thus closed) in `Additive` or `Single` mode. This Scene can also have been loaded using either the [`Addressables`](../api/UnityEngine.AddressableAssets.Addressables.html) or `SceneManagement` API. As noted above, if you set `trackHandle` to `false`, you can only call `Addressables.ReleaseInstance` with the handle, not with the actual GameObject.

**Note**: If you call [`Addressables.ReleaseInstance`](../api/UnityEngine.AddressableAssets.Addressables.html?q=instantiate.async#UnityEngine_AddressableAssets_Addressables_ReleaseInstance_GameObject_) on an instance that was not created using the [`Addressables`](../api/UnityEngine.AddressableAssets.Addressables.html) API, or was created with `trackHandle==false`, the system detects that and returns `false` to indicate that the method was unable to release the specified instance. The instance will not be destroyed in this case.

[`Addressables.InstantiateAsync`](../api/UnityEngine.AddressableAssets.Addressables.html?q=instantiate.async#UnityEngine_AddressableAssets_Addressables_InstantiateAsync_System_Object_Transform_System_Boolean_System_Boolean_) has some associated overhead, so if you need to instantiate the same objects hundreds of times per frame, consider loading via the [`Addressables`](../api/UnityEngine.AddressableAssets.Addressables.html) API, then instantiating through other methods. In this case, you would call [`Addressables.LoadAssetAsync`](../api/UnityEngine.AddressableAssets.Addressables.html?q=instantiate.async#UnityEngine_AddressableAssets_Addressables_LoadAssetAsync__1_System_Object_), then save the result and call [`GameObject.Instantiate()`](https://docs.unity3d.com/ScriptReference/Object.Instantiate.html) for that result. This allows flexibility to call `Instantiate` in a synchronous way. The downside is that the Addressables system has no knowledge of how many instances you created, which can lead to memory issues if not properly managed. For example, a Prefab referencing a texture would no longer have a valid loaded texture to reference, causing rendering issues (or worse). These sorts of problems can be hard to track down as you may not immediately trigger the memory unload (see section on [clearing memory](#when-is-memory-cleared), below).

### Data loading
Interfaces that do not need their [`AsyncOperationHandle.Result`](../api/UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle-1.html) released, will still need the operation itself to be released. Examples of these would be `Addressables.LoadResourceLocationsAsync` and `Addressables.GetDownloadSizeAsync`. They load data that you can access until the operation is released. This release should be done via `Addressables.Release`.

### Background interactions
Operations that do not return anything in the [`AsyncOperationHandle.Result`](../api/UnityEngine.AddressableAssets.Addressables.html?q=instantiate.async#UnityEngine_AddressableAssets_Addressables_InstantiateAsync_System_Object_Transform_System_Boolean_System_Boolean_) field have have an optional parameter to automatically release the operation handle on completion. If you have no further need for one of these operation handles after it has completed, set the `autoReleaseHandle` parameter to true to make sure the operation handle is cleaned up. The scenario where you would want `autoReleaseHandle` to be false would be if you needed to check the `Status` of the operation handle after it has completed.  Examples of these interfaces are [`Addressables.DownloadDependenciesAsync`](../api/UnityEngine.AddressableAssets.Addressables.html#UnityEngine_AddressableAssets_Addressables_DownloadDependenciesAsync_System_Collections_Generic_IList_System_Object__UnityEngine_AddressableAssets_Addressables_MergeMode_System_Boolean_) and [`Addressables.UnloadScene`](../api/UnityEngine.AddressableAssets.Addressables.html#UnityEngine_AddressableAssets_Addressables_UnloadScene_UnityEngine_ResourceManagement_AsyncOperations_AsyncOperationHandle_System_Boolean_).

## The Addressables Event Viewer
Use the **Addressables Event Viewer** window to monitor the ref-counts of all Addressables system operations. To access the window in the Editor, select **Window** > **Asset Management** > **Addressables** > **Event Viewer**. 

**Important**: In order to view data in the Event Viewer, you must enable the **Send Profiler Events** setting in your [`AddressableAssetSettings`](../api/UnityEditor.AddressableAssets.Settings.AddressableAssetSettings.html) object's Inspector.

The following information is available in the Event Viewer:

* A white vertical line indicates the frame in which a load request occurred.
* A blue background indicates that an asset is currently loaded.  
* The green part of the graph indicates an asset's current ref-count.

Note that the Event Viewer is only concerned with ref-counts, not memory consumption (see section on [clearing memory](#when-is-memory-cleared?), below, for more information).

Listed under the Assets column, you will see a row for each of the following, per frame:

* FPS: The frames per second count.
* MonoHeap: The amount of RAM in use.
* Event Counts: The total number of events in a frame.
* Asset requests: Displays the reference count on an operation over time. If the asset request has any dependencies, a triangle appears that you can click on to view the children's request operations.

You can click the left and right arrows in order to step through the frames one by one, or click **Current** to jump to the latest frame. Press the **+** button to expand a row for more details.

The information displayed in the Event Viewer is related to the [build script](AddressableAssetsDevelopmentCycle.md#build-scripts) you use to create Play mode data.

When using the Event Viewer, avoid the **Use Asset Database** built script because it does not account for any shared dependencies among the assets. Use the **Simulate Groups** script or the **Use Existing Build** script instead, but the latter is better suited for the Event Viewer because it gives a more accurate monitoring of the ref-counts.

## When is memory cleared?
An asset no longer being referenced (indicated by the end of a blue section in the profiler) does not necessarily mean that asset was unloaded. A common applicable scenario involves multiple assets in an asset bundle. For example: 

* You have three assets (`tree`, `tank`, and `cow`) in an asset bundle (`stuff`).  
* When `tree` loads, the profiler displays a single ref-count for `tree`, and one for `stuff`.  
* Later, when `tank` loads, the profiler displays a single ref-count for both `tree` and `tank`, and two ref-counts for the `stuff` bundle.  
* If you release `tree`, it's ref-count becomes zero, and the blue bar goes away. 

In this example, the `tree` asset is not actually unloaded at this point. You can load an asset bundle, or its partial contents, but you cannot partially unload an asset bundle. No asset in `stuff` will unload until the bundle itself is completely unloaded. The exception to this rule is the engine interface [`Resources.UnloadUnusedAssets`](https://docs.unity3d.com/ScriptReference/Resources.UnloadUnusedAssets.html). Executing this method in the above scenario will cause `tree` to unload. Because the Addressables system cannot be aware of these events, the profiler graph only reflects the Addressables ref-counts (not exactly what memory holds). Note that if you choose to use `Resources.UnloadUnusedAssets`, it is a very slow operation, and should only be called on a screen that won't show any hitches (such as a loading screen).
