# Content update workflow
Addressables provides a content update workflow intended for games that will dynamically be downloading content from a CDN.  In this situation, a player (app, exe, apk, etc.) is built and deployed (such as through the Android app store).  While running, the app will contact a CDN to discover and download additional content.  

This is not the same as games that use platform provided patching systems (such as Switch or Steam).  For these games the every build of the game should be a complete fresh content build, completely bypassing the update flow.  In this instance, the _addressables_content_state.bin_ file that is generated after each build can be discarded or ignored. 

## Project structure
Unity recommends structuring your game content into two categories: 

* `Cannot Change Post Release`: Static content that you never expect to update. 
* `Can Change Post Release`: Dynamic content that you expect to update. 

In this structure, content marked as `Cannot Change Post Release` ships with the application (or downloads soon after install), and resides in very few large bundles. Content marked as `Can Change Post Release` resides online, ideally in smaller bundles to minimize the amount of data needed for each update. One of the goals of the Addressable Assets System is to make this structure easy to work with and modify without having to change your scripts. 

However, the Addressable Assets System can also accommodate situations that require changes to the content marked as `Cannot Change Post Release`, when you don't want to publish a whole new application build.  Modified assets and their dependencies (and dependents) will be duplicated in new bundles that will be used instead of the shipped content.  This can result in a much smaller update than replacing the entire bundle or rebuilding the game.  Once a build has been made, it is important to NOT change the state of a group from "Cannot Change Post Release" to "Can Change Post Release" or vice versa until an entirely new build is made.  If the groups change after a full content build but before a content update, Addressables will not be able to generate the correct changes needed for the update.

Note that in cases that do not allow remote updates (such as many of the current video-game consoles, or games without a server), you should make a complete and fresh build every time.

## How it works
Addressables uses a content catalog to map an address to each Asset, specifying where and how to load it. In order to provide your app with the ability to modify that mapping, your original app must be aware of an online copy of this catalog. To set that up, enable the **Build Remote Catalog** setting on the [`AddressableAssetSettings`](xref:UnityEditor.AddressableAssets.Settings.AddressableAssetSettings) Inspector. This ensures that a copy of the catalog gets built to and loaded from the specified paths. This load path cannot change once your app has shipped. The content update process creates a new version of the catalog (with the same file name) to overwrite the file at the previously specified load path.

Building an application generates a unique app content version string, which identifies what content catalog each app should load. A given server can contain catalogs of multiple versions of your app without conflict. We store the data we need in the `addressables_content_state.bin` file. This includes the version string, along with hash information for any Asset that is contained in a group marked as `Cannot Change Post Release`.  By default, this is located in the `Assets/AddressableAssetsData/\<platform\>` Project directory, where `\<platform\>` is your target platform.

The `addressables_content_state.bin` file contains hash and dependency information for every `Cannot Change Post Release` Asset group in the Addressables system. All groups building to the `StreamingAssets` folder should be marked as `Cannot Change Post Release`, though large remote groups may also benefit from this designation. During the next step (preparing for content update, described below), this hash information determines if any `Cannot Change Post Release` groups contain changed Assets, and thus need those Assets moved elsewhere.

### Update life cycle
The first step in the process of building content is always a fresh full build.  This can be triggered from within the **Addressables Groups** window in the Unity Editor (**Window** > **Asset Management** > **Addressables** > **Groups**).  Once there, selecting your build script from **Build** > **New Build**.  Unless you create a custom build, the only option will be **Default Build Script**.  

Every time a full player build is created (such as through **File** > **Build and Run**), this should be preceded by a full content build of Addressables.  After a player build is created, if you wish to update it's contents via a CDN, then it becomes time to do a content update build.

#### Life cycle example
This is a sample flow over the course of your game's existence.  More details of key steps are outlined later in this document.  
1. Create and refine content until ready for initial release.
2. Trigger initial addressables content build via the Groups window.
3. Build a player, such as via Build and Run.  
4. Continue to refine & iterate on content.

If you do not have a CDN, and are not dynamically downloading content, after step 4, return to step 2 to create a fresh content build and fresh player.

If you do distribute content via CDN, continued iteration involves more steps.  I will refer to the player built in step 3 above as "PlayerBuild1".  Steps continue as follows:

5. Optionally trigger **Check for Content Update Restrictions** (see Identifying changed assets below).
6. Trigger a content update build via **Update a Previous Build** (see Building for content updates below).

At this point you can repeat steps 4-6 until you are ready to create a new player to submit to your platform of choice.

7. Optionally save _addressables_content_state.bin_ file and branch content.
8. Optionally create a new build destination on your CDN. Especially if changing Unity version. 

When creating a new player, there are two scenarios to consider.  In the simplest case, you are never going to distribute new content for "PlayerBuild1".  The content you have released so far for "PlayerBuild1" is all users will get until they update to future player builds.  In this scenario, you do not do step 7, and simply return from step 6 back to step 2, this time creating "PlayerBuild2", and only making content updates for it.

In the more complex scenario, you wish to build a "PlayerBuild2", but make a new content available to both players.  Here, you must do step 7, saving your _addressables_content_state.bin_.  The simplest way to handle this is to completely branch your content.  This ensures properly named catalogs and content for each build.  There are ways around this, but they involve leaving the standard catalog creation and naming systems.  

The purpose of step 8 is to ensure each player build has a clean space to download content.  Often this isn't needed, but is safer. One key example where it is an absolute must is when you have updated Unity version, but not some content.  If an AssetBundle is built with identical content, but two different Unity versions, the hash will be the same, but the CRC will be different.  This means, with any of our bundle naming schemes, the new bundle will have the same name as the old one (thus overwritting it).  As it also has a new CRC, the old player will not be able to download it successfully (we keep up with CRC's in our catalog for download safety).  

## Planning for content updates
When planning for a content update, there are a few items to ensure are set up correctly during the initial build (step 2 above).  First is that the correct groups are tagged with "Can" or "Cannot" change as described above.  The next is that the _addressables_content_state.bin_ file that is generated off of this build is saved.  By default this is built to `Assets/AddressableAssetsData/\<platform\>` Project directory, where `\<platform\>` is your target platform.  We recommend using version control to save the file at this point.  

### When a full rebuild is required
Addressables can only distribute content, not code.  As such, a code change generally necessitates a fresh player build, and often a fresh build of content. In some instances a new player can download old content from a CDN, but this requires a careful analysis of what type tree was created during the initial build.  This is advanced territory to explored carefully. 

Note that Addressables itself is code, so updating Addressables or Unity version likely requires creating a new player build and fresh content builds. 

### Unique Bundle IDs
Unique Bundle IDs is an advanced option that should only be enabled if you require the ability to load new versions of content while old versions are still in memory.  There is an extra cost associated with build and content refreshes if this option is on.

When loading AssetBundles into memory, Unity enforces that two bundles cannot be loaded with the same internal names.  This can put some limitations on updating bundles at runtime.  As Addressables supports updating the catalog outside of initialization, it's possible to update content that you have already loaded.

To make this work, one of two things must happen.  One option is to unload all your Addressables content prior to updating the catalog.  This ensures new bundles with old names will not cause conflicts in memory.  The second option is to ensure that your updated AssetBundles have unique internal identifiers.  This would allow you to load new bundles, while the old are still in memory.  We have an option to enable this second option.  Turn on "Unique Bundle IDs" within the [`AddressableAssetSettings`](xref:UnityEditor.AddressableAssets.Settings.AddressableAssetSettings) Inspector.  The downside of this option is that it requires bundles to be rebuilt up the dependency chain.  Meaning if you changed a material in one group, by default only the material's bundle would be rebuilt.  With "Unique Bundle IDs" on, any Asset that references that material would also need rebuilding.

## Identifying changed assets
If you have modified Assets in any `Cannot Change Post Release` groups, you'll need to run the **Check for Content Update Restrictions** command (step 5 above). This will take any modified Asset out of the `Cannot Change Post Release` groups and move them to a new group. To generate the new Asset groups:

1. Open the **Addressables Groups** window in the Unity Editor (**Window** > **Asset Management** > **Addressables** > **Groups**).
2. In the **Addressables Groups** window, select **Tools** on the top menu bar, then **Check for Content Update Restrictions**.
3. In the **Build Data File** dialog that opens, select the _addressables_content_state.bin_ file (by default, this is located in the `Assets/AddressableAssetsData/\<platform\>` Project directory, where `\<platform\>` is your target platform).

This data is used to determine which Assets or dependencies have been modified since the application was last built. The system moves these Assets to a new group in preparation for the content update build. 

**Note**: This command will do nothing if all your changes are confined to `Can Change Post Release` groups.  

**Important**: Before running the prepare operation, Unity recommends branching your version control system. The prepare operation rearranges your Asset groups in a way suited for updating content. Branching ensures that next time you ship a new player, you can return to your preferred content arrangement.


## Building for content updates
To build for a content update:

1. Open the **Addressables Groups** window in the Unity Editor (**Window** > **Asset Management** > **Addressables** > **Groups**).
2. In the **Addressables Groups** window, select **Build** on the top menu, then **Update a Previous Build**.
3. In the **Build Data File** dialog that opens, select the build folder of an existing application build. The build folder must contain an `addressables_content_state.bin` file (by default, this is located in the `Assets/AddressableAssetsData/\<platform\>` Project directory, where `\<platform\>` is your target platform). 

The build generates a content catalog, a hash file, and the AssetBundles.

The generated content catalog has the same name as the catalog in the selected application build, overwriting the old catalog and hash file. The application loads the hash file to determine if a new catalog is available. The system loads unmodified Assets from existing bundles that were shipped with the application or already downloaded.

The system uses the content version string and location information from the `addressables_content_state.bin` file to create the AssetBundles. AssetBundles that do not contain updated content are written using the same file names as those in the build selected for the update. If an AssetBundle contains updated content, a new AssetBundle is generated that contains the updated content, with a new file name so that it can coexist with the original. Only AssetBundles with new file names must be copied to the location that hosts your content.  

The system also builds AssetBundles for content that cannot change, but you do not need to upload them to the content hosting location, as no Addressables Asset entries reference them.

Note that you should not change the build scripts between building a new player and making content updates (e.g., player code, addressables). This could cause unpredictable behavior in your application.

## Checking for content updates at runtime
You can add a custom script to periodically check whether there are new Addressables content updates. Use the following function call to start the update:

[`public static AsyncOperationHandle<List<string>> CheckForCatalogUpdates(bool autoReleaseHandle = true)`](xref:UnityEngine.AddressableAssets.Addressables.CheckForCatalogUpdates(System.Boolean))

where `List<string>` contains the list of modified locator IDs.  You can filter this list to only update specific IDs, or pass it entirely into the UpdateCatalogs API.

If there is new content, you can either present the user with a button to perform the update, or do it automatically. Note that it is up to the developer to make sure that stale Assets are released.

The list of catalogs can be null and if so, the following script updates all catalogs that need an update:

[`public static AsyncOperationHandle<List<IResourceLocator>> UpdateCatalogs(IEnumerable<string> catalogs = null, bool autoReleaseHandle = true)`](xref:UnityEngine.AddressableAssets.Addressables.UpdateCatalogs(System.Collections.Generic.IEnumerable{System.String},System.Boolean))

The return value is the list of updated locators.

## Content update examples
In this example, a shipped application is aware of the following groups:

| **`Local_Static`** | **`Remote_Static`** | **`Remote_NonStatic`** |
|:---------|:---------|:---------|
| `AssetA` | `AssetL` | `AssetX` |
| `AssetB` | `AssetM` | `AssetY` |
| `AssetC` | `AssetN` | `AssetZ` |

Note that `Local_Static` and `Remote_Static` are part of the `Cannot Change Post Release` groups.

As this version is live, there are players that have `Local_Static` on their devices, and potentially have either or both of the remote bundles cached locally. 

If you modify one Asset from each group (`AssetA`, `AssetL`, and `AssetX`), then run **Check for Content Update Restrictions**, the results in your local Addressable settings are now:

| **`Local_Static`** | **`Remote_Static`** | **`Remote_NonStatic`** | **`content_update_group (non-static)`** |
|:---------|:---------|:---------|:---------|
|  |  | `AssetX` | `AssetA` |
| `AssetB` | `AssetM` | `AssetY` | `AssetL` |
| `AssetC` | `AssetN` | `AssetZ` |  |

Note that the prepare operation actually edits the `Cannot Change Post Release` groups, which may seem counterintuitive. The key, however, is that the system builds the above layout, but discards the build results for any such groups. As such, you end up with the following from a player's perspective:

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

The `content_update_group` bundle consists of the modified Assets that will be referenced moving forward. 

Note that the example above has the following implications:

1. Any changed local Assets remain unused on the user's device forever.  
2. If the user already cached a non-static bundle, they will need to re-download the bundle, including the unchanged Assets (in this instance, for example, `AssetY` and `AssetZ`). Ideally, the user has not cached the bundle, in which case they simply need to download the new `Remote_NonStatic` bundle.
3. If the user has already cached the `Static_Remote` bundle, they only need to download the updated asset (in this instance, `AssetL` via `content_update_group`). This is ideal in this case. If the user has not cached the bundle, they must download both the new `AssetL` via `content_update_group` and the now-defunct `AssetL` via the untouched `Remote_Static` bundle. Regardless of the initial cache state, at some point the user will have the defunct `AssetL` on their device, cached indefinitely despite never being accessed. 

The best setup for your remote content will depend on your specific use case.
