# Addressable Assets development

One of the key benefits of Addressables Assets is the decoupling of how you arrange your content, how you build your content, and how you load your content. Traditionally these facets of development are heavily tied together. If you arrange your content into Resources directories, the content is built into the base player, and you must load the content by calling the [Resources.Load](https://docs.unity3d.com/ScriptReference/Resources.Load.html) method and supplying the path to the resource.

To access content stored elsewhere, you used direct references or Asset bundles. If you used Asset bundles, you again loaded by path, tying your loading and your arranging together. If your Asset bundles were remote, or have dependencies in other bundles, you had to write a code to manage downloading, loading, and unloading of all of your bundles.

Giving an Asset an address, allows you to load it by that address, no matter where it is in your Project or how it was built.  You can change an Asset’s path or filename without problem.  You can also move the Asset from Resources, or from a local build destination, to some other build location, including remote ones, without your loading code ever changing.

## Asset Group Schemas
Schemas define a set of data and can be attached to asset groups vie their inspector.  The set of schemas attached to a group define how its contents are processed during a build.  For example, when building in packed mode, groups that have the BundledAssetGroupSchema attached to them are used as the sources for the bundles.  Sets of schemas can be combined into schema templates and these will be used to define new groups.  The schema templates can be added to the main Addressable Assets settings asset through the inspector.

## Build Scripts
Build scripts are represented as ScriptableObject assets in the project that implement the IDataBuilder interface.  Users can create their own build scripts and add them to the Addressable Asset settings object through its inspector.  Currently 3 scripts have been implemented to support a the full player build and 3 play modes for iterating in the editor.

## Play Mode Iteration

Addressable Assets has three build scripts that create play mode data to help you accelerate your app development.

Fast mode allows you to run the game quickly as you work through the flow of your game. Fast mode loads Assets directly through the Asset Database for quick iteration with no analysis or Asset bundle creation.

Virtual mode analyzes content for layout and dependencies without creating Asset bundles. Assets load from the Asset Database though the ResourceManager as if they were loaded through bundles. To see when bundles load/unload during game play, view the Asset usage in the Addressable Profiler window. To open the Addressable Profiler window, navigate to **Window** > **Asset Management** > **Addressable Profiler**.

Virtual mode helps you simulate load strategies and tweak your content groups to find that right balance for a production release.

Packed mode fully packs content and creates Asset bundles on disk. This mode takes the most time to prepare and provides the most accurate behavior for resource loading.

Each mode has its own time and place during development and deployment.

The following table shows segment of the development cycle in which a particular mode is useful.

| | Design | Develop | Build | Test / Play | Publish |
|:---|:---|:---|:---|:---|:---|
| Fast| x | x |   | x In Editor only |   |
|
Virtual| x | x | x Asset Bundle Layout | x In Editor only |  |
| Packed|   |   | x Asset Bundles  | x | x |

## Initialization objects

You can attach objects to the Addressable Assets settings and pass them to the initialization process at run time. The `CacheInitializationSettings` object is used to control the Unity's Caching API at runtime. To create your own initialization object, you can create a `ScriptableObject` that implements the `IObjectInitializationDataProvider` interface. It is the editor component of the system and is responsible for creating the `ObjectInitializationData` that is serialized with the run time data.

## Content update workflow

The recommended approach to content updates is to structure your game data into two groups: static content that you expect never to update and dynamic content that you expect to update. In this content structure, static content ships with the Player (or download soon after install) and resides in a single or a few large bundles. Dynamic content resides online and should be in smaller bundles to minimize the amount of data needed for each update. One of the goals of the Addressables system is to make this structure easy to work with and modify without having to change scripts. Sometimes you find yourself in a situation that requires changes to the "never update" content but you do not want to publish a full player build. 

### How it works

When you build a Player, you generate a unique Player content version string. The version string, along with hash information for each asset that is in a group marked as `StaticContent`, is stored in the *addressables_content_state.bin* file. The *addressables_content_state.bin* file contains hash and dependency information for every `StaticContent` asset group in the Addressables system. You should store this file where you can easily retrieve it.

Typically, `StaticContent` groups are built in the streaming assets folder, but can include remote groups that are large. The Player uses the unique content version string to identify the correct remote content catalog to load at startup. Each Player build looks for a different remote catalog. If a content only update is desired, the Addressables system can use the generated hash data of any previous Player build to determine which addressable assets need to move to a new group to support the update.

### Prepare for Content Update
When you build the Player, you can generate the new asset groups you need to properly update published content. To generate the the asset groups:

1. In the Editor, on the menu bar, click **Window**.
1. Click **Asset Management**, then select **Addressable Assets**.
1. In the Addressable Assets window, on the menu bar, click **Build**, then select **Prepare for Content Update**.
1. In the **Build Data File** dialog, select the build folder of a Player build.

This data is used to determine which assets or dependencies have been modified since the player was built. These assets are moved to a new group in preparation of the content update build.

### Build for Content Update

To build for a content update:

1. In the Editor, on the menu bar, click Window.
2. Click Asset Management, then select Addressable Assets.
3. In the **Addressable Assets** window, on the menu bar, click **Build**, then select **Build for Content Update**.
4. In the **Build Data File** dialog, select the build folder of an existing Player build. The build folder must contain an *addressables_content_state.bin* file. 

The build generates a content catalog, a hash file, and the asset bundles.

The generated content catalog has the same name as the catalog in the selected Player build and is overwritten as is the hash file. The hash file is loaded by the Player to determine if a new catalog is available. Assets that have not been modified are loaded from existing bundles that were shipped with the Player or already downloaded.

The Addressable Assets build system uses the content version string and location information from *addressables_content_state.bin* file to create the asset bundles. 
Asset bundles that do not contain updated content are written using the same file names as those in the build selected for the update. If an asset bundle contains updated content, a new asset bundle is generated that contains the updated content and is given a new file name so that it can coexist with the original. Only asset bundles with new file names must be copied to the location that hosts your content.  

Asset bundles for `StaticContent` groups are also built, but they do not need to be uploaded to the content hosting location as they are not referenced by any Addressable asset entries.

## Analyzing your data
To analyze your data configuration for potential problems, open the Addressables Window, and click the **Analyze** button on the top bar of that window.  This will open a sub-pane within the Addressables window.  From there you can click "Run Tests" to execute the analyze rules in the project.  After runing a test, if there are any potential problems, you can manually alter your groups and rerun, or click "Fix All" to have the system automatically do it. 

### Check Duplicate Bundle Dependencies
The only rule currently present checks for potentially duplicated assets.  It does so by scanning all groups with BundledAssetGroupSchemas, and spies on the planned asset bundle layout.  This requires essencially triggering a full build, so this check is time consuming and performance intensive.  

Duplicated assets are caused by assets in different bundles sharing dependencies.  An example would be marking two prefabs that share a material as addressable in different groups.  That material (and any of its dependencies) will be pulled into the bundles with each prefab.  To prevent this, the material has to be marked as addressable, either with one of the prefabs, or in its own space.  Doing so will put the material and its dependencies in a separate bundle.  

If this check finds any issues, and the "Fix All" button is pressed, a new group will be created, and all dependent assets will be moved into that group.

There is one scenario in which this removal of duplicates will be incorrect.  If you have an asset containing multiple objects, it is possible for different bundles to only be pulling in portions of the asset (some objects), and not actually duplicate.  An example would be an FBX with many meshes.  If one mesh is in BundleA and another is in BundleB, this check will think that the FBX is shared, and will pull it out into its own bundle.  In this rare case, that was actually harmful as neither bundle had the full FBX asset.

### Future rule structure
Right now, Analyze only provides one rule.  In the future, the system will come with additional rules, and there will be the ability to write custom rules and integrate them into this analyze workflow. 

