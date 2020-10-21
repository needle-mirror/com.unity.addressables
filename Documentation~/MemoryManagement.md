---
uid: addressables-memory-management
---
# Memory management
## Mirroring load and unload
When working with Addressable Assets, the primary way to ensure proper memory management is to mirror your load and unload calls correctly. How you do so depends on your Asset types and load methods. In all cases, however, the release method can either take the loaded Asset, or an operation handle returned by the load. For example, during Scene creation (described below) the load returns a [`AsyncOperationHandle<SceneInstance>`](xref:UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle), which you can release via this returned handle, or by keeping up with the `handle.Result` (in this case, a [`SceneInstance`](xref:UnityEngine.ResourceManagement.ResourceProviders.SceneInstance)).

### Asset loading
To load an Asset, use [`Addressables.LoadAssetAsync` (single asset)](xref:UnityEngine.AddressableAssets.Addressables.LoadAssetAsync``1(System.Object)) or [`Addressables.LoadAssetsAsync` (multiple assets)](xref:UnityEngine.AddressableAssets.Addressables.LoadAssetsAsync``1(System.Collections.Generic.IList{System.Object},System.Action{``0},UnityEngine.AddressableAssets.Addressables.MergeMode)).

**Note**: `LoadAssetAsync` is intended for use with keys that map to single entries. If you provide a key that matches multiple entries (such as a widely used label) the method will load the first match it finds to the given key. This is not deterministic as it can be affected by build order.

This loads the Asset into memory without instantiating it. Every time the load call executes, it adds one to the ref-count for each Asset loaded. If you call [`LoadAssetAsync`](xref:UnityEngine.AddressableAssets.Addressables.LoadAssetAsync``1(System.Object)) three times with the same address, you will get three different instances of an [`AsyncOperationHandle`](xref:UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle) struct back, all referencing the same underlying operation. That operation has a ref-count of three for the corresponding Asset. If the load succeeds, the resulting [`AsyncOperationHandle`](xref:UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle) struct contains the Asset in the `.Result` property. You can use the loaded Asset to instantiate using Unity's built-in instantiation methods, which does not increment the Addressables ref-count.

To unload the Asset, use the [`Addressables.Release`](xref:UnityEngine.AddressableAssets.Addressables.Release``1(``0)) method, which decrements the ref-count. When a given Asset's ref-count is zero, that Asset is ready to be unloaded, and decrements the ref-count of any dependencies. 

**Note**: The Asset may or may not be unloaded immediately, contingent on existing dependencies. For more information, read the section on [when memory is cleared](#when-is-memory-cleared).

### Scene loading
To load a Scene, use [`Addressables.LoadSceneAsync`](xref:UnityEngine.AddressableAssets.AssetReference.LoadSceneAsync(UnityEngine.SceneManagement.LoadSceneMode,System.Boolean,System.Int32)). You can use this method to load a Scene in `Single` mode, which closes all open Scenes, or in `Additive` mode (for more information, see documentation on [Scene mode loading](https://docs.unity3d.com/ScriptReference/SceneManagement.LoadSceneMode.html).  

To unload a Scene, use [`Addressables.UnloadSceneAsync`](xref:UnityEngine.AddressableAssets.Addressables.UnloadSceneAsync(UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle,System.Boolean)), or open a new Scene in `Single` mode. You can open a new Scene by either using the Addressables interface, or using the [`SceneManager.LoadScene`](https://docs.unity3d.com/ScriptReference/SceneManagement.SceneManager.LoadScene.html) or [`SceneManager.LoadSceneAsync`](https://docs.unity3d.com/ScriptReference/SceneManagement.SceneManager.LoadSceneAsync.html) methods. Opening a new Scene closes the current one, properly decrementing the ref-count.

### GameObject instantiation
To load and instantiate a GameObject Asset, use [`Addressables.InstantiateAsync`](xref:UnityEngine.AddressableAssets.Addressables.InstantiateAsync(System.Object,UnityEngine.Transform,System.Boolean,System.Boolean)). This instantiates the Prefab located by the specified `location` parameter. The Addressables system will load the Prefab and its dependencies, incrementing the ref-count of all associated Assets. 

Calling [`InstantiateAsync`](xref:UnityEngine.AddressableAssets.Addressables.InstantiateAsync(System.Object,UnityEngine.Transform,System.Boolean,System.Boolean)) three times on the same address results in all dependent assets having a ref-count of three. Unlike calling [`LoadAssetAsync`](xref:UnityEngine.AddressableAssets.Addressables.LoadAssetAsync``1(System.Object)) three times, however, each `InstantiateAsync` call returns an [`AsyncOperationHandle`](xref:UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle`1) pointing to a unique operation.  This is because the result of each `InstantiateAsync` is a unique instance. You would need to individually release each returned [`AsyncOperationHandle`](xref:UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle) or GameObject instance. Another distinction between `InstantiateAsync` and other load calls is the optional `trackHandle` parameter. When set to `false`, you must keep the [`AsyncOperationHandle`](xref:UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle) to use while releasing your instance. This is more efficient, but requires more development effort.

To destroy an instantiated GameObject, use [`Addressables.ReleaseInstance`](xref:UnityEngine.AddressableAssets.Addressables.ReleaseInstance(UnityEngine.GameObject)), or close the Scene that contains the instantiated object. This Scene can have been loaded (and thus closed) in `Additive` or `Single` mode. This Scene can also have been loaded using either the [`Addressables`](xref:UnityEngine.AddressableAssets.Addressables) or `SceneManagement` API. As noted above, if you set `trackHandle` to `false`, you can only call `Addressables.ReleaseInstance` with the handle, not with the actual GameObject.

**Note**: If you call [`Addressables.ReleaseInstance`](xref:UnityEngine.AddressableAssets.Addressables.ReleaseInstance(UnityEngine.GameObject)) on an instance that was not created using the [`Addressables`](xref:UnityEngine.AddressableAssets.Addressables) API, or was created with `trackHandle==false`, the system detects that and returns `false` to indicate that the method was unable to release the specified instance. The instance will not be destroyed in this case.

[`Addressables.InstantiateAsync`](xref:UnityEngine.AddressableAssets.Addressables.InstantiateAsync(System.Object,UnityEngine.Transform,System.Boolean,System.Boolean)) has some associated overhead, so if you need to instantiate the same objects hundreds of times per frame, consider loading via the [`Addressables`](xref:UnityEngine.AddressableAssets.Addressables) API, then instantiating through other methods. In this case, you would call [`Addressables.LoadAssetAsync`](xref:UnityEngine.AddressableAssets.Addressables.LoadAssetAsync*), then save the result and call [`GameObject.Instantiate()`](https://docs.unity3d.com/ScriptReference/Object.Instantiate.html) for that result. This allows flexibility to call `Instantiate` in a synchronous way. The downside is that the Addressables system has no knowledge of how many instances you created, which can lead to memory issues if not properly managed. For example, a Prefab referencing a texture would no longer have a valid loaded texture to reference, causing rendering issues (or worse). These sorts of problems can be hard to track down as you may not immediately trigger the memory unload (see section on [clearing memory](#when-is-memory-cleared), below).

### Data loading
Interfaces that do not need their [`AsyncOperationHandle.Result`](xref:UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle.Result) released, will still need the operation itself to be released. Examples of these would be `Addressables.LoadResourceLocationsAsync` and `Addressables.GetDownloadSizeAsync`. They load data that you can access until the operation is released. This release should be done via `Addressables.Release`.

### Background interactions
Operations that do not return anything in the [`AsyncOperationHandle.Result`](xref:UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle.Result) field have have an optional parameter to automatically release the operation handle on completion. If you have no further need for one of these operation handles after it has completed, set the `autoReleaseHandle` parameter to true to make sure the operation handle is cleaned up. The scenario where you would want `autoReleaseHandle` to be false would be if you needed to check the `Status` of the operation handle after it has completed.  Examples of these interfaces are [`Addressables.DownloadDependenciesAsync`](xref:UnityEngine.AddressableAssets.Addressables.DownloadDependenciesAsync(System.Collections.Generic.IList{System.Object},UnityEngine.AddressableAssets.Addressables.MergeMode,System.Boolean)) and [`Addressables.UnloadScene`](xref:UnityEngine.AddressableAssets.Addressables.UnloadScene(UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle,System.Boolean)).

## The Addressables Event Viewer
Use the **Addressables Event Viewer** window to monitor the ref-counts of all Addressables system operations. To access the window in the Editor, select **Window** > **Asset Management** > **Addressables** > **Event Viewer**.

**Important**: In order to view data in the Event Viewer, you must enable the **Send Profiler Events** setting in your [`AddressableAssetSettings`](xref:UnityEditor.AddressableAssets.Settings.AddressableAssetSettings) object's Inspector. Changes to **Send Profiler Events** will be reflected in the following build. This means that entering play mode when using the **Use Existing Build** play mode script will use the value set during the most recent build. Alternatively, entering play mode when using the **Use Asset Database** or **Simulate Groups** play mode scripts will pick up the current state, as those play mode scripts rebuild the settings data upon entering play mode.

The following information is available in the Event Viewer:

* A white vertical line indicates the frame in which a load request occurred.
* A blue background indicates that an Asset is currently loaded.
* The green part of the graph indicates an Asset's current ref-count.

Note that the Event Viewer is only concerned with ref-counts, not memory consumption (see section on [clearing memory](#when-is-memory-cleared), below, for more information).

Listed under the Assets column, you will see a row for each of the following, per frame:

* FPS: The frames per second count.
* MonoHeap: The amount of RAM in use.
* Event Counts: The total number of events in a frame.
* Instantiation Counts: The total number of calls to Addressables.InstantiateAsync on a frame.
* Asset requests: Displays the reference count on an operation over time. If the Asset request has any dependencies, a triangle appears that you can click on to view the children's request operations.

You can click the left and right arrows in order to step through the frames one by one, or click **Current** to jump to the latest frame. Press the **+** button to expand a row for more details.

The information displayed in the Event Viewer is related to the [build script](AddressableAssetsDevelopmentCycle.md#build-scripts) you use to create Play mode data.

When using the Event Viewer, avoid the **Use Asset Database** built script because it does not account for any shared dependencies among the Assets. Use the **Simulate Groups** script or the **Use Existing Build** script instead, but the latter is better suited for the Event Viewer because it gives a more accurate monitoring of the ref-counts.

### Connecting the Event Viewer to a standalone player	
To connect the Event Viewer to a standalone player, go into the build menu, select the platform you wish to use, and ensure that **Development Build** and **Autoconnect Profiler** are both enabled. Next, open the Unity Profiler by selecting **Window** > **Analysis** > **Profiler** and select the platform you wish to build for on the top toolbar. Finally, select **Build and Run** in the Build Settings window and the Event Viewer will automatically connect to and display events for the standalone player selected.

## When is memory cleared?
An Asset no longer being referenced (indicated by the end of a blue section in the profiler) does not necessarily mean that Asset was unloaded. A common applicable scenario involves multiple Assets in an [AssetBundle](https://docs.unity3d.com/Manual/AssetBundlesIntro.html "AssetBundles"). For example:
* You have three Assets (`tree`, `tank`, and `cow`) in an AssetBundle (`stuff`).  
* When `tree` loads, the profiler displays a single ref-count for `tree`, and one for `stuff`.  
* Later, when `tank` loads, the profiler displays a single ref-count for both `tree` and `tank`, and two ref-counts for the `stuff` AssetBundle.  
* If you release `tree`, it's ref-count becomes zero, and the blue bar goes away. 

In this example, the `tree` asset is not actually unloaded at this point. You can load an AssetBundle, or its partial contents, but you cannot partially unload an AssetBundle. No asset in `stuff` unloads until the AssetBundle itself is completely unloaded. The exception to this rule is the engine interface [`Resources.UnloadUnusedAssets`](https://docs.unity3d.com/ScriptReference/Resources.UnloadUnusedAssets.html). Executing this method in the above scenario causes `tree` to unload. Because the Addressables system cannot be aware of these events, the profiler graph only reflects the Addressables ref-counts (not exactly what memory holds). Note that if you choose to use `Resources.UnloadUnusedAssets`, it is a very slow operation, and should only be called on a screen that won't show any hitches (such as a loading screen).

## AssetBundle Memory Overhead
When deciding how to organize your Addressable groups and AssetBundles, you may want to consider the runtime memory usage of each AssetBundle. Many small AssetBundles can give greater granularity for unloading, but can come at the cost of some runtime memory overhead. This section describes the various types of AssetBundle memory overhead.

### Serialized File Overhead

When Unity loads an AssetBundle, it allocates an internal buffer for each serialized file in the AssetBundle. This buffer persists for the lifetime of the AssetBundle. A non-scene AssetBundle contains one serialized file, but a scene AssetBundle can contain up to two serialized files for each scene in the bundle. The buffer sizes are optimized per platform. Switch, Playstation, and Windows RT use 128k buffers. All other platforms use 14k buffers. You can use the [Build Layout Report](DiagnosticTools.md#build-layout-report) to determine how many serialized files are in an AssetBundle.

Each serialized file also contains a TypeTree for each object type within the file. The TypeTree describes the data layout of each object type and allows you to load objects that are deserialized slightly differently from how they were serialized. All the TypeTrees are loaded when the AssetBundle is loaded and held in memory for the lifetime of the AssetBundle. The memory overhead associated with TypeTrees scales with the number of unique types in the serialized file and the complexity of those types. Although you can choose to ship AssetBundles without TypeTrees, be aware that even engine version patches can slightly change the serialization format and could result in undefined behavior when you use a newer runtime to load assets serialized with an older format; Unity recommends always shipping AssetBundles with TypeTree information, which is the default behavior.

When you put objects of the same type in more than one AssetBundle, the type information for those objects is duplicated in the TypeTree of each AssetBundle. This duplication of type information is more noticeable when you use many small AssetBundles. You can test the impact that TypeTrees have on the size of your AssetBundles by building them with and without TypeTrees disabled and comparing the sizes. If after measuring, you find the duplicate TypeTree memory overhead to be unacceptable, you can avoid it by packing your objects of the same types in the same AssetBundles.

### AssetBundle Object

The AssetBundle object itself has two main sources of runtime memory overhead: the table of contents, and the preload table. While the size of an AssetBundle on disk is not the same as its size at runtime, you can use the disk size to approximate the memory overhead. This information is located in the [Build Layout Report](DiagnosticTools.md#build-layout-report).

The table of contents is a map within the bundle that allows you to look up each explicitly included asset by name. It scales linearly with the number of assets and the length of the string names by which they are mapped.

The preload table is a list of all the objects that a loadable asset references. This data is needed so Unity can load all those referenced objects when you load an asset from the AssetBundle. For example, a prefab would have a preload entry for each component as well as any other assets it may reference (materials, textures, etc). Each preload entry is 64 bits and can reference objects in other AssetBundles.

As an example, consider a situation in which you are adding two Assets to an AssetBundle  (`PrefabA` and `PrefabB`) and both of these prefabs reference a third prefab  (`PrefabC`), which is large and contains several components and references to other assets. This AssetBundle has two preload tables, one for `PrefabA` and one for `PrefabB`. Those tables contain entries for all the objects of their respective prefab, but also entries for all the objects in `PrefabC` and any objects referenced by `PrefabC`. Thus the information required to load `PrefabC` ends up duplicated in both `PrefabA` and `PrefabB`. This will happen whether or not `PrefabC` is explicitly added to an AssetBundle.

Depending on how you organize your assets, the preload tables in AssetBundles could become quite large and contain many duplicate entries. This is especially true if you had several loadable assets that all reference a complex asset, such as `PrefabC` in the situation above. If you determine that the memory overhead from the preload table is a problem, you can structure your loadable assets so that they have fewer complex loading dependencies.

## AssetBundle dependencies	
Loading an Addressable Asset loads all the AssetBundle dependencies and keeps them loaded until you call [`Addressables.Release`](xref:UnityEngine.AddressableAssets.Addressables.Release``1(``0)) on the handle returned from the loading method.	

AssetBundle dependencies are created when an asset in one AssetBundle references an asset in another AssetBundle. An example of this is a material referencing a texture.  The dependencies of all these AssetBundles can be thought of as a dependency graph. During the catalog generation stage of the build process, Addressables walks this graph to calculate all the AssetBundles that must be loaded for each Addressable Asset. Because dependencies are calculated at the AssetBundle level, all Addressable Assets within a single AssetBundle have the same dependencies. Adding an Addressable Asset that has an external reference (references an object in another AssetBundle) to an AssetBundle adds that AssetBundle as a dependency for all the other Addressable Assets in the AssetBundle.

For Example:	

`BundleA` contains Addressable Assets `RootAsset1` and `RootAsset2`. `RootAsset2` references `DependencyAsset3`, which is contained in `BundleB`. Even though `RootAsset1` has no reference to `BundleB`, `BundleB` is still a dependency of `RootAsset1` because `RootAsset1` is in `BundleA`, which has a reference on `BundleB`.

Prior to 1.13.0, the dependency graph was not as thorough as it is now.  In the example above, `RootAsset1` would not have had a dependency on `BundleB`. This previous behavior resulted in references breaking when an AssetBundle being referenced by another AssetBundle was unloaded and reloaded.  This fix may result in additional data remaining in memory if the dependency graph is complex enough.

### Duplicate dependencies
When exploring memory management and dependency graphs, it's important to discuss duplicated content.  There are two mechanisms by which an asset can be built into an AssetBundle: explicit and implicit. If you mark an asset as Addressable, it is explicitly put into an AssetBundle.  That is the only AssetBundle it is in.  

Example:
A material has a direct dependency on a texture, and both assets are marked as Addressable in separate AssetBundles `BundleM` and `BundleT` respectively.  `BundleT` contains the texture, `BundleM` contains the material, and lists `BundleT` as a dependency.

If any dependencies are not explicitly included, then they are implicitly pulled in.
Example:
A material has a direct dependency on a texture, and only the material is marked as Addressable in `BundleM`.  During build, the texture, because it is not explicitly included elsewhere, is pulled into `BundleM` when the material is.  

This implicit dependency inclusion can lead to duplication.
Example
Two materials, matA and matB, are Addressable and both have direct dependencies on the same texture.  If matA and matB are built into the same AssetBundle, then the texture is pulled implicitly in once.  If matA and matB are built into separate AssetBundles, then the texture is pulled implicitly into each of those AssetBundles.  At runtime, the engine has no record that these textures came from the same source asset, and are each loaded as they are needed by their respective materials. 

### SpriteAtlas dependencies
SpriteAtlases complicate the dependency calculation a bit, and merit a more thorough set of examples.

Addressable Sprite Example 1:
Three textures exist and are marked as Addressable in three separate groups.  Each texture builds to about 500KB.  During the build, they are built into three spearate AssetBundles, each AssetBundle only containing the given sprite meta data and texture.  Each AssetBundle is be about 500KB and none of these AssetBundles have dependencies.  

Addressable Sprite Example 2:
The three textures in Example 1 are put into a SpriteAtlas.  That atlas is not Addressable.  One of the AssetBundles generated contains that atlas texture and is about 1500KB.  The other two AssetBundles only contain Sprite metadata (a few KB), and list the atlas AssetBundle as a dependency.  Which AssetBundle contains the texture is deterministic in that it is the same through rebuilds, but is not something that can be set by the user.  This is the key portion that goes against the standard duplication of dependencies.  The sprites are dependent on the SpriteAtlas texture to load, and yet that texture is not built into all three AssetBundles, but is instead built only into one.

Addressable Sprite Example 3:
The SpriteAtlas from Example 2 is marked as Addressable in its own AssetBundle.  At this point there are four AssetBundles created.  If you are using a 2020.x or newer version of Unity, this builds as you would expect.  The three AssetBundles with the sprites are each be only a few KB and have a dependency on this fourth SpriteAtlas AssetBundle, which is be about 1500KB.  If you are using 2019.x or older, the texture itself may end up elsewhere.  The three sprite AssetBundles still depend on the SpriteAtlas AssetBundle. However, the SpriteAtlas AssetBundle may only contain meta data, and the texture may be with one of the other sprites.

Addressable Prefab With Sprite Dependency Example 1:
Instead of three Addressable textures, there are three Addressable sprite prefabs. Each prefab depends on its own sprite (about 500KB). Building the three prefabs seperately results, as expected, in three AssetBundles of about 500KB each.

Addressable Prefab With Sprite Dependency Example 2
Taking the prefabs and textures from the previous example, all three textures are added to a SpriteAtlas, and that atlas is not marked as Addressable.  In this scenario, the SpriteAtlas texture is duplicated.  All three AssetBundles are approximately 1500KB. This is expected based on the general rules about duplication of dependencies, but goes against the behavior seen in "Addressable Sprite Example 2".

Addressable Prefab With Sprite Dependency Example 2
Taking the prefabs, textures, and SpriteAtlas form the above example, the SpriteAtlas is also marked as Addressable.  Conforming to the rules of explicit inclusion, the SpriteAtlas texture is included only in the AssetBundle containing the SpriteAtlas.  The AssetBundles with prefabs reference this fourth AssetBundle as a dependency.






