# Addressable Assets development cycle
One of the key benefits of Addressable Assets is decoupling how you arrange, build, and load your content. Traditionally, these facets of development are heavily tied together. 

## Traditional asset management
If you arrange content in _Resources_ directories, it gets built into the base application and you must load the content using the [`Resources.Load`](https://docs.unity3d.com/ScriptReference/Resources.Load.html) method, supplying the path to the resource. To access content stored elsewhere, you would use direct references or asset bundles. If you use asset bundles, you would again load by path, tying your load and organization strategies together. If your asset bundles are remote, or have dependencies on other bundles, you have to write code to manage downloading, loading, and unloading all of your bundles.

## Addressable Asset management
Giving an asset an address allows you to load it using that address, no matter where it is in your Project or how you built the asset.  You can change an Addressable Asset’s path or filename without issue. You can also move the Addressable Asset from the _Resources_ folder, or from a local build destination, to some other build location (including remote ones), without ever changing your loading code.

### Asset group schemas
Schemas define a set of data. You can attach schemas to asset groups in the Inspector. The set of schemas attached to a group defines how the build processes its contents. For example, when building in packed mode, groups with the [`BundledAssetGroupSchema`](../api/UnityEditor.AddressableAssets.Settings.GroupSchemas.BundledAssetGroupSchema.html) schema attached to them act as sources for asset bundles. You can combine sets of schemas into templates that you use to define new groups. You can add schema templates via the [`AddressableAssetSettings`](../api/UnityEditor.AddressableAssets.Settings.AddressableAssetSettings.html) Inspector.

## Build scripts
Build scripts are represented as [`ScriptableObject`](https://docs.unity3d.com/Manual/class-ScriptableObject.html) assets in the Project that implement the [`IDataBuilder`](../api/UnityEditor.AddressableAssets.Build.IDataBuilder.html) interface. Users can create their own build scripts and add them to the [`AddressableAssetSettings`](../api/UnityEditor.AddressableAssets.Settings.AddressableAssetSettings.html) object through its Inspector. To apply a build script in the **Addressables Groups** window (**Window** > **Asset Management** > **Addressables** > **Groups**), select **Play Mode Script**, and choose a dropdown option. Currently, there are three scripts implemented to support the full application build, and three Play mode scripts for iterating in the Editor.

### Play mode scripts
The Addressable Assets package has three build scripts that create Play mode data to help you accelerate app development.

#### Use Asset Database (faster)
Use Asset Database mode ([`BuildScriptFastMode`](../api/UnityEditor.AddressableAssets.Build.DataBuilders.BuildScriptFastMode.html)) allows you to run the game quickly as you work through the flow of your game. It loads assets directly through the asset database for quick iteration with no analysis or asset bundle creation.

#### Simulate Groups (advanced)
Simulate Groups mode ([`BuildScriptVirtualMode`](../api/UnityEditor.AddressableAssets.Build.DataBuilders.BuildScriptVirtualMode.html)) analyzes content for layout and dependencies without creating asset bundles. Assets load from the asset database though the [`ResourceManager`](../api/UnityEngine.ResourceManagement.ResourceManager.html), as if they were loaded through bundles. To see when bundles load or unload during game play, view the asset usage in the [**Addressables Event Viewer** window](MemoryManagement.md#the-addressables-event-viewer) (**Window** > **Asset Management** > **Addressables** > **Event Viewer**).

Simulate Groups mode helps you simulate load strategies and tweak your content groups to find the right balance for a production release.

#### Use Existing Build (requires built groups)
Use Existing Build mode most closely matches a deployed application build, but it requires you to build the data as a separate step. If you aren't modifying assets, this mode is the fastest since it does not process any data when entering Play mode. You must either build the content for this mode in the **Addressables Groups** window (**Window** > **Asset Management** > **Addressables** > **Groups**) by selecting **Build** > **New Build** > **Default Build Script**, or using the [`AddressableAssetSettings.BuildPlayerContent()`](../api/UnityEditor.AddressableAssets.Settings.AddressableAssetSettings.html?q=addressableassetsettings#UnityEditor_AddressableAssets_Settings_AddressableAssetSettings_BuildPlayerContent) method in your game script.

## Analysis and debugging
By default, Addressable Assets only logs warnings and errors. You can enable detailed logging by opening the **Player** settings window (**Edit** > **Project Settings...** > **Player**), navigating to the **Other Settings** > **Configuration** section, and adding "`ADDRESSABLES_LOG_ALL`" to the **Scripting Define Symbols** field. 

You can also disable exceptions by unchecking the **Log Runtime Exceptions** option in the [`AddressableAssetSettings`](../api/UnityEditor.AddressableAssets.Settings.AddressableAssetSettings.html) object Inspector. You can implement the [`ResourceManager.ExceptionHandler`](../api/UnityEngine.ResourceManagement.ResourceManager.html#UnityEngine_ResourceManagement_ResourceManager_ExceptionHandler) property with your own exception handler if desired, but this should be done after Addressables finishes runtime initialization (see below).

## Initialization objects
You can attach objects to the Addressable Assets settings and pass them to the initialization process at runtime. The [`CacheInitializationSettings`](../api/UnityEditor.AddressableAssets.Settings.CacheInitializationSettings.html) object controls Unity's caching API at runtime. To create your own initialization object, create a ScriptableObject that implements the [`IObjectInitializationDataProvider`](../api/UnityEngine.ResourceManagement.Util.IObjectInitializationDataProvider.html) interface. This is the Editor component of the system responsible for creating the [`ObjectInitializationData`](../api/UnityEngine.ResourceManagement.Util.ObjectInitializationData.html) that is serialized with the runtime data.

## Customizing URL Evaluation
There are several scenarios where you will need to customize the path or URL of an asset (an AssetBundle generally) at runtime.  The most common example is creating signed URLs.  Another might be dynamic host determination.  

The code below is an example of appending a query string to all URLs:

```
//Implement a method to transform the internal ids of locations
string MyCustomTransform(IResourceLocation location)
{
	if (location.ResourceType == typeof(IAssetBundleResource) && location.InternalId.StartsWith("http"))
		return location.InternalId + "?customQueryTag=customQueryValue";
	return location.InternalId;
}

//Override the Addressables transform method with your custom method.  This can be set to null to revert to default behavior.
[RuntimeInitializeOnLoadMethod]
static void SetInternalIdTransform()
{
	Addressables.InternalIdTransformFunc = MyCustomTransform;
}
```

****Please Note****: When bundling video files into Addressables with the intent of loading them on the Android platform, you must create a [`CacheInitializationSettings`](../api/UnityEditor.AddressableAssets.Settings.CacheInitializationSettings.html) object, disable `Compress Bundles` on that object, then add it to the list of Initialization Objects on the [`AddressableAssetSettings`](../api/UnityEditor.AddressableAssets.Settings.AddressableAssetSettings.html) object if it has not been already.

## Content update workflow
Unity recommends structuring your game content into two categories: 

* **Static** content that you never expect to update. 
* **Dynamic** content that you expect to update. 

In this structure, static content ships with the application (or downloads soon after install), and resides in very few large bundles. Dynamic content resides online, ideally in smaller bundles to minimize the amount of data needed for each update. One of the goals of the Addressable Assets System is to make this structure easy to work with and modify without having to change your scripts. 

However, the Addressable Assets System can also accommodate situations that require changes to the "static" content, when you don't want to publish a whole new application build.  

Note that in cases that do not allow remote updates (such as many of the current video-game consoles, or games without a server), you should make a complete and fresh build every time.

### How it works
Addressables uses a content catalog to map an address to each asset, specifying where and how to load it. In order to provide your app with the ability to modify that mapping, your original app must be aware of an online copy of this catalog. To set that up, enable the **Build Remote Catalog** setting on the [`AddressableAssetSettings`](../api/UnityEditor.AddressableAssets.Settings.AddressableAssetSettings.html) Inspector. This ensures that a copy of the catalog gets built to and loaded from the specified paths. This load path cannot change once your app has shipped. The content update process creates a new version of the catalog (with the same file name) to overwrite the file at the previously specified load path.

Building an application generates a unique app content version string, which identifies what content catalog each app should load. A given server can contain catalogs of multiple versions of your app without conflict. We store the data we need in the _addressables_content_state.bin_ file. This includes the version string, along with hash information for any asset that is contained in a group marked as `StaticContent`.  By default, this is located in the _Assets/AddressableAssetsData/\<platform\>_ Project directory, where _\<platform\>_ is your target platform.

The _addressables_content_state.bin_ file contains hash and dependency information for every `StaticContent` asset group in the Addressables system. All groups building to the _StreamingAssets_ folder should be marked as `StaticContent`, though large remote groups may also benefit from this designation. During the next step (preparing for content update, described below), this hash information determines if any `StaticContent` groups contain changed assets, and thus need those assets moved elsewhere.

### Unique Bundle IDs
When loading AssetBundles into memory, Unity enforces that two bundles cannot be loaded with the same internal names.  This can put some limitations on updating bundles at runtime.  Now that Addressables supports updating the catalog outside of initialization, it's possible to update content that you have alreaedy loaded.

To make this work, one of two things must happen.  One option is to unload all your addressables content prior to updating the catalog.  The second option is to ensure that your updated AssetBundles have unique internal identifiers.  This would allow you to load new bundles, while the old are still in memory.  We have an option to enable this second option.  Turn on "Unique Bundle IDs" within the [`AddressableAssetSettings`](../api/UnityEditor.AddressableAssets.Settings.AddressableAssetSettings.html) Inspector.  The downside of this option is that it requires bundles to be rebuilt up the dependency chain.  Meaning if you changed a material in one group, by default only the material's bundle would be rebuilt.  With "Unique Bundle IDs" on, any asset that references that material would also need rebuilding.

### Preparing for content updates
If you have modified assets in any `StaticContent` groups, you'll need to run the **Check for Content Update Restrictions** command. This will take any modified asset out of the static groups and move them to a new group. To generate the new asset groups:

1. Open the **Addressables Groups** window in the Unity Editor (**Window** > **Asset Management** > **Addressables** > **Groups**).
2. In the **Addressables Groups** window, select **Tools** on the top menu bar, then **Check for Content Update Restrictions**.
3. In the **Build Data File** dialog that opens, select the _addressables_content_state.bin_ file (by default, this is located in the _Assets/AddressableAssetsData/\<platform\>_ Project directory, where _\<platform\>_ is your target platform).

This data is used to determine which assets or dependencies have been modified since the application was last built. The system moves these assets to a new group in preparation for the content update build. 

**Note**: This command will do nothing if all your changes are confined to non-static groups.  

**Important**: Before running the prepare operation, Unity recommends branching your version control system. The prepare operation rearranges your asset groups in a way suited for updating content. Branching ensures that next time you ship a new player, you can return to your preferred content arrangement.

### Building for content updates
To build for a content update:

1. Open the **Addressables Groups** window in the Unity Editor (**Window** > **Asset Management** > **Addressables** > **Groups**).
2. In the **Addressables Groups** window, select **Build** on the top menu, then **Update a Previous Build**.
3. In the **Build Data File** dialog that opens, select the build folder of an existing application build. The build folder must contain an _addressables_content_state.bin_ file (by default, this is located in the _Assets/AddressableAssetsData/\<platform\>_ Project directory, where _\<platform\>_ is your target platform). 

The build generates a content catalog, a hash file, and the asset bundles.

The generated content catalog has the same name as the catalog in the selected application build, overwriting the old catalog and hash file. The application loads the hash file to determine if a new catalog is available. The system loads unmodified assets from existing bundles that were shipped with the application or already downloaded.

The system uses the content version string and location information from the _addressables_content_state.bin_ file to create the asset bundles. Asset bundles that do not contain updated content are written using the same file names as those in the build selected for the update. If an asset bundle contains updated content, a new asset bundle is generated that contains the updated content, with a new file name so that it can coexist with the original. Only asset bundles with new file names must be copied to the location that hosts your content.  

The system also builds asset bundles for static content, but you do not need to upload them to the content hosting location, as no Addressables asset entries reference them.

Note that you should not change the build scripts between building a new player and making content updates (e.g., player code, addressables). This could cause unpredictable behavior in your application.

### Checking for content updates at runtime
You can add a custom script to periodically check whether there are new Addressables content updates. Use the following function call to start the update:

[`public static AsyncOperationHandle<List<string>> CheckForCatalogUpdates(bool autoReleaseHandle = true)`](../api/UnityEngine.AddressableAssets.Addressables.html?q=checkforcatalogupdates#UnityEngine_AddressableAssets_Addressables_CheckForCatalogUpdates_System_Boolean_)

where `List<string>` contains the list of modified locator IDs.  You can filter this list to only update specific IDs, or pass it entirely into the UpdateCatalogs API.

If there is new content, you can either present the user with a button to perform the update, or do it automatically. Note that it is up to the developer to make sure that stale assets are released.

The list of catalogs can be null and if so, the following script updates all catalogs that need an update:

[`public static AsyncOperationHandle<List<IResourceLocator>> UpdateCatalogs(IEnumerable<string> catalogs = null, bool autoReleaseHandle = true)`](../api/UnityEngine.AddressableAssets.Addressables.html?q=UpdateCatalogs#UnityEngine_AddressableAssets_Addressables_UpdateCatalogs_System_Collections_Generic_IEnumerable_System_String__System_Boolean_)

The return value is the list of updated locators.

### Content update examples
In this example, a shipped application is aware of the following groups:

| **`Local_Static`** | **`Remote_Static`** | **`Remote_NonStatic`** |
|:---------|:---------|:---------|
| `AssetA` | `AssetL` | `AssetX` |
| `AssetB` | `AssetM` | `AssetY` |
| `AssetC` | `AssetN` | `AssetZ` |

As this version is live, there are players that have `Local_Static` on their devices, and potentially have either or both of the remote bundles cached locally. 

If you modify one asset from each group (`AssetA`, `AssetL`, and `AssetX`), then run **Check for Content Update Restrictions**, the results in your local Addressable settings are now:

| **`Local_Static`** | **`Remote_Static`** | **`Remote_NonStatic`** | **`content_update_group (non-static)`** |
|:---------|:---------|:---------|:---------|
|  |  | `AssetX` | `AssetA` |
| `AssetB` | `AssetM` | `AssetY` | `AssetL` |
| `AssetC` | `AssetN` | `AssetZ` |  |

Note that the prepare operation actually edits the static groups, which may seem counter intuitive. The key, however, is that the system builds the above layout, but discards the build results for any static groups. As such, you end up with the following from a player's perspective:

| **`Local_Static`** |
|:---------|
| `AssetA` |
| `AssetB` |
| `AssetC` |

The `Local_Static` bundle is already on player devices, which you can't change. This old version of `AssetA` is no longer referenced. Instead, it is stuck on player devices as dead data.

| **`Remote_Static`** |
|:---------|
| `AssetL` |
| `AssetM` |
| `AssetN` |

The `Remote_Static` bundle is unchanged. If it is not already cached on a player's device, it will download when `AssetM` or `AssetN` is requested. Like `AssetA`, this old version of `AssetL` is no longer referenced. 

| **`Remote_NonStatic`** (old) |
|:---------|
| `AssetX` |
| `AssetY` |
| `AssetZ` |

The `Remote_NonStatic` bundle is now old. You could delete it from the server, but either way it will not be downloaded from this point forward. If cached, it will eventually leave the cache. Like `AssetA` and `AssetL`, this old version of `AssetX` is no longer referenced.

| **`Remote_NonStatic`** (new) |
|:---------|
| `AssetX` |
| `AssetY` |
| `AssetZ` |

The old `Remote_NonStatic` bundle is replaced with a new version, distinguished by its hash file. The modified version of `AssetX` is updated with this new bundle.

| **`content_update_group`**  |
|:---------|
| `AssetA` |
| `AssetL` |

The `content_update_group` bundle consists of the modified assets that will be referenced moving forward. 

Note that the example above has the following implications:

1. Any changed local assets remain unused on the user's device forever.  
2. If the user already cached a non-static bundle, they will need to re-download the bundle, including the unchanged assets (in this instance, for example, `AssetY` and `AssetZ`). Ideally, the user has not cached the bundle, in which case they simply need to download the new `Remote_NonStatic` bundle.
3. If the user has already cached the `Static_Remote` bundle, they only need to download the updated asset (in this instance, `AssetL` via `content_update_group`). This is ideal in this case. If the user has not cached the bundle, they must download both the new `AssetL` via `content_update_group` and the now-defunct `AssetL` via the untouched `Remote_Static` bundle. Regardless of the initial cache state, at some point the user will have the defunct `AssetL` on their device, cached indefinitely despite never being accessed. 

The best setup for your remote content will depend on your specific use case.
