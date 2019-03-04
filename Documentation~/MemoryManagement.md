# Memory Mangement

### Mirroring Load and Unload
The primary way to ensure your memory is cleaned up properly in Addressables is to mirror your load and unload calls correctly. How to do this depends on the type of asset and method of load.

**Asset Loading**
To load, use `Addressables.LoadAsset` or `Addressables.LoadAssets`.
This will load the asset into memory, but not instantiate it.  It will add one to the ref-count for each asset loaded, each time the Load call executes.  If you call `LoadAsset` three times with the same address, you will get the same `IAsyncOperation` back, but will have a ref-count of three for that asset.  The resulting `IAsyncOperation` will have the asset in the `.Result` field if the load was successful.  You can use the loaded asset to instantiate using Unity's built in instantiation methods, which will not increment the Addressables ref-count.

To unload, use `Addresslabes.ReleaseAsset`

**Scene Loading**
To load, use `Addressables.LoadScene`.
You can use this method to load a scene in `Single` mode, which will close all open scenes, or in `Additive` mode. See https://docs.unity3d.com/ScriptReference/SceneManagement.LoadSceneMode.html.  

To unload, use `Addressables.UnloadScene`, or open a new scene in `Single` mode.  When opening a new scene, you can do so either with the above Addressables interface or `SceneManager.LoadScene`.  Opening a new scene will close the current one, which will properly decrement the ref-count.

**Game Object Instantiation**
To load, use `Addressables.Instantiate`.
This will instantiate the object, and increment the ref-count on every instantiation.  

To unload, use `Addressables.ReleaseInstance` or close the scene that the instantiated object is in.  This scene could have been loaded (and thus closed) in `Additive` or `Single` mode.  This scene could also have been loaded via Addressables or SceneManagement.

A note on `Object.Destroy()` and `Addressables.ReleaseInstance`.  If you call `Addressables.ReleaseInstance` on an instance that was not created via Addressables, we detect that, and simply call Object.Destroy() on it for you.  So it is possible to replace all calls to Destroy in your code with ReleaseInstance.  

You are also welcome to load via Addressables, then instantiate outside Addressables.  In this instance, you would call `Addressables.LoadAsset`, then save off the result, and call `GameObject.Instantiate()` on that result.  This gives you the flexibility to call Instantiate in a synchronous way.  The downside is that we will have no idea how many instances you have created.  If you still have instances floating around when you call `Addressables.ReleaseAsset`, you will likely end up in a bad situation.  For example, a prefab referencing a texture, would no longer have a valid (loaded) texture to reference, causing rendering issues (or worse).  These sorts of problems can be hard to track down as we may not immediately trigger the memory unload (see "When memory is cleared" below).

### Addressable Profiler
To open the Addressable Profiler window, use *Window->Asset Management->Addressable Profiler*.  You also need to enable *Send Profiler Events* on your Addressable Asset Settings.  This can be done on the inspector for that asset, either by finding the asset in your project, or by selecting a group in the Addressable window, which then allows you to navigate (in the inspector) to the top level settings.

The purpose of this window is to show you the status of ref-counts on all assets and asset bundles within the Addressables System.  
* A white vertical line indicates the frame in which a load request occurs.
* The blue background indicates that the asset in question is currently loaded.  
* The green part of the graph indicates current ref-count.

##### When is memory cleared
The end of a blue section does not necessarily mean that the item has been unloaded.  The key scenario in question is multiple assets in an asset bundle.  Say you have three assets: "tree", "tank", and "cow" in an asset bundle called "stuff".  When "tree" is loaded, you'll see a single ref count on "tree" as well as one on "stuff".  Then later, if you load "tank", you'll see a single count on each "tree" and "tank", and a count of two on "stuff".  Now if you release "tree", it's count will go to zero, and the blue bar will go away.  The asset is not actually unloaded at this point.  With asset bundles, you can load a bundle, and part of the contents, but you cannot partially unload a bundle.  No asset in "stuff" will be unloaded until that bundle is done with and completely unloaded.  
The exception to this rule is the engine interface `Resources.UnloadUnusedAssets` (see https://docs.unity3d.com/ScriptReference/Resources.UnloadUnusedAssets.html).  If you execute this in the above scenario, "tree" will be unloaded.  Because we cannot be aware of these events, we only have the graph reflecting our ref-counts, not exactly what memory holds.  Note that if you choose to use `Resources.UnloadUnusedAssets`, it is a very slow operation, and should only be called during a screen that won't show any hitches (such as a loading screen).

##### Bundle Cache
A given bundle cannot be re-loaded while already in memory. To account for this, we automatically wrap bundle loads in a memory-caching system ("Bundle Cached Providor").  By default, this cache only keeps bundles that have a ref count greater than zero.  It can be configured however, to hold on to bundles after they are no longer referenced in a Least-Recently-Used cache (LRU).  To set that up, each group has settings called "Bundle Cached Provider Max LRU Count/Age".  The cache will never hold more bundles than the max count.  Thus if count is zero, age is ignored.  If count is some value N greater than zero, it will still never exceed the count of N bundles, but as it sits below that max, it will evict based on age in seconds.  An age value of zero is considered "infinity", so only the count will matter.
