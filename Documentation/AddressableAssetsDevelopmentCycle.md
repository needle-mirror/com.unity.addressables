# Addressable Assets development

One of the key benefits of Addressables Assets is the decoupling of how you arrange your content, how you build your content, and how you load your content. Traditionally these facets of development are heavily tied together. If you arrange your content into Resources directories, the content is built into the base player, and you must load the content by calling the [Resources.Load](https://docs.unity3d.com/ScriptReference/Resources.Load.html) method and supplying the path to the resource.

To access content stored elsewhere, you used direct references or Asset bundles. If you used Asset bundles, you again loaded by path, tying your loading and your arranging together. If your Asset bundles were remote, or have dependencies in other bundles, you had to write a code to manage downloading, loading, and unloading of all of your bundles.

Giving an Asset an address, allows you to load it by that address, no matter where it is in your Project or how it was built.  You can change an Asset’s path or filename without problem.  You can also move the Asset from Resources, or from a local build destination, to some other build location, including remote ones, without your loading code ever changing.

## Play Mode Iteration

Addressable Assets has three Play Modes to help you accelerate your app development.

Fast mode allows you to run the game quickly as you work through the flow of your game. Fast mode loads Assets directly through the Asset Database for quick iteration with no analysis or Asset bundle creation.

Virtual mode analyzes content for layout and dependencies without creating Asset bundles. Assets load from the Asset Database though the ResourceManager as if they were loaded through bundles. You can view Asset usage in the ResourceManager (RM) Profiler Window to see when bundles load/unload during game play.

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

## Content Update Workflow
The best approach to content updates is to structure your game data into two groups: things that you want to update and things that will never update.  The static content will ship with the player (or download soon after install) and reside in a single or few large bundles.  The dynamic content will reside online and should be in smaller bundles to minimize the amount of data needed for each update.  One of the primary goals of the Addressables system is to make this structure easy to work with and modify without having to change scripts.  Sometimes you find yourself in a situation that requires changes to the “never update” content but you do not want to publish a full player build.  The Addressables system has tools that can assist in this process.

### How it works
Whenever a player build is made, a unique player content version string is generated.  This version string is stored in a file along with hash information for each asset that is in a group marked as StaticContent.  These are usually groups that build to the streaming assets folder  but can include remote groups that are large.  This information should be saved for any published build, preferably in source control in a named branch of the release.  The player that has been built uses the unique content version string to identify the correct remote content catalog to load at startup.  Each player build will look for a different remote catalog.  If a content only update is desired, the addressables system can use the generated hash data of any previous player build to determine which addressable assets will need to move in order to support the update.  
### Prepare for Content Update
Once you have built the player, you can run the Prepare for Content Update command to generate the new asset groups needed to properly update published content.  You will need to select the build folder of a player build.  There is a file named cachedata.bin that contains hash and dependency information for every StaticContent asset group in the addressables system.  This data will be used to determine which assets or dependencies have been modified since the player was built.  These assets will be moved to a new group in preparation of the content update build.
### Build for Content Update
This command will also need to select the player build folder in order to load the cachedata.bin data.  A build is made using the existing player’s content version string and location information.  New asset bundles are generated that contain the updated content and can be copied into the server location for the hosted content.  Any bundles that have not changed will have the same file name as existing bundles and the copy can be skipped.  Any newly generated bundles will have a new file name and can coexist with previous versions.  The newly generated content catalog will have the same name as the targeted player build so that file will be overwritten as well as the hash file.  The hash file is loaded by the player to determine if a new catalog is available.  Asset bundles for and StaticContent groups will also be built but they do not need to be uploaded to the content hosting location as they are not referenced by any Addressable asset entries.  Entries to assets that have not been modified will be directed to the existing bundles that were shipped with the player or already downloaded.
