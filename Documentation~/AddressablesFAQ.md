---
uid:  addressables-faq
---

# Addressables FAQ

### Is it better to have many small bundles or a few bigger ones?
There are a few key factors that go into deciding how many bundles to generate.
First, it's important to note that you control how many bundles you have both by how large your groups are, and by the groups' build settings.  "Pack Together" for example, creates one bundle per group, while "Pack Separately" creates many.  See [schema build settings for more information](xref:UnityEditor.AddressableAssets.Settings.GroupSchemas.BundledAssetGroupSchema.BundleMode).

Once you know how to control bundle layout, the decision of how to set these up will be game specific.  Here are key pieces of data to help make that decision:

Dangers of too many bundles:
* Each bundle has memory overhead.  Details are [on the memory management page](MemoryManagement.md#assetbundle-memory-overhead). This is tied to a number of factors, outlined on that page, but the short version is that this overhead can be significant.  If you anticipate 100's or even 1000's of bundles loaded in memory at once, this could mean a noticeable amount of memory eaten up.
* There are concurrency limits for downloading bundles.  If you have 1000's of bundles you need all at once, they cannot not all be downloaded at the same time.  Some number will be downloaded, and as they finish, more will trigger. In practice this is a fairly minor concern, so minor that you'll often be gated by the total size of your download, rather than how many bundles it's broken into.
* Bundle information can bloat the catalog.  To be able to download or load catalogs, we store string-based information about your bundles.  1000's of bundles worth of data can greatly increase the size of the catalog.

Dangers of too few bundles:
* The UnityWebRequest (which we use to download) does not resume failed downloads.  So if a large bundle downloading and your user loses connection, the download is started over once they regain connection. 
* Items can be loaded individually from bundles, but cannot be unloaded individually.  For example, if you have 10 materials in a bundle, load all 10, then tell Addressables to release 9 of them, all 10 will likely be in memory.  This is also covered [on the memory management page](MemoryManagement.md#when-is-memory-cleared).

### What compression settings are best?
Addressables provides three different options for bundle compression: Uncompressed, LZ4, and LZMA.  Generally speaking, LZ4 should be used for local content, and LZMA for remote, but more details are outlined below as there can be exceptions to this.  
You can set the compression option using the Advanced settings on each group. Compression does not affect in-memory size of your loaded content. 
* Uncompressed - This option is largest on disk, and generally fasted to load.  If your game happens to have space to spare, this option should at least be considered for local content.  A key advantage of uncompressed bundles is how they handle being patched.  If you are developing for a platform where the platform itself provides patching (such as Steam or Switch), uncompressed bundles provide the most accurate (smallest) patching.  Either of the other compression options will cause at least some bloat of patches.
* LZ4 - If Uncompressed is not a viable option, then LZ4 should be used for all other local content.  This is a chunk-based compression which provides the ability to load parts of the file without needing to load it in its entirety. 
* LZMA - LZMA should be used for all remote content, but not for any local content.  It provides the smallest bundle size, but is slow to load. If you were to store local bundles in LZMA you could create a smaller player, but load times would be significantly worse than uncompressed or LZ4. For downloaded bundles, we avoid the slow load time by recompressing the downloaded bundle when storing it in the asset bundle cache.  By default, bundles will be stored in the cache Uncompressed.  If you wish to compress the cache with LZ4, you can do so by creating a [`CacheInitializationSettings`](xref:UnityEditor.AddressableAssets.Settings.CacheInitializationSettings).  See [Initialization Objects](AddressableAssetsDevelopmentCycle.md#initialization-objects) for more information about setting this up. 

Note that the hardware characteristics of a platform can mean that uncompressed bundles are not always the fastest to load.  The maximum speed of loading uncompressed bundles is gated by IO speed, while the speed of loading LZ4-compressed bundles can be gated by either IO speed or CPU, depending on hardware.  On most platforms, loading LZ4-compressed bundles is CPU bound, and loading uncompressed bundles will be faster. On platforms that have low IO speeds and high CPU speeds, LZ4 loading can be faster. It is always a good practice to run performance analysis to validate whether your game fits the common patterns, or needs some unique tweaking.

More information on Unity's compression selection is available in the [Asset Bundle documentation](https://docs.unity3d.com/Manual/AssetBundles-Cache.html).  

### Are there ways to miminize the catalog size?
Currently there are two optimizations available.
1. Compress the local catalog.  If your primary concern is how big the catalog is in your build, there is an option in the inspector for the top level settings of **Compress Local Catalog**. This option builds catalog that ships with your game into an asset bundle. Compressing the catalog makes the file itself smaller, but note that this does increase catalog load time.  
2. Disable built-in scenes and Resources.  Addressables provides the ability to load content from Resources and from the built-in scenes list. By default this feature is on, which can bloat the catalog if you do not need this feature.  To disable it, select the "Built In Data" group within the Groups window (**Window** > **Asset Management** > **Addressables** > **Groups**). From the settings for that group, you can uncheck "Include Resources Folders" and "Include Build Settings Scenes". Unchecking these option only removes the references to those asset types from the Addressables catalog.  The content itself is still built into the player you create, and you can still load them via legacy API. 

### What is addressables_content_state?
After every content build of addressables, we produce an addressables_content_state.bin file, which is saved to the `Assets/AddressableAssetsData/<Platform>/` folder of your Unity project.
This file is critical to our [content update workflow](ContentUpdateWorkflow.md). If you are not doing any content updates, you can completely ignore this file.
If you are planning to do content updates, you will need the version of this file produced for the previous release. We recommend checking it into version control and creating a branch each time you release a player build.  More information is available on our [content update workflow page](ContentUpdateWorkflow.md).

### What are possible scale implications?
As your project grows larger, keep an eye on the following aspects of your assets and bundles:
* Total bundle size - Historically Unity has not supported files larger than 4GB.  This has been fixed in some recent editor versions, but there can still be issues. It is recommended to keep the content of a given bundle under this limit for best compatibility across all platforms.  
* Sub assets affecting UI performance. There is no hard limit here, but if you have many assets, and those assets have many sub-assets, it may be best to turn off sub-asset display. This option only affects how the data is displayed in the Groups window, and does not affect what you can and cannot load at runtime.  The option is available in the groups window under **Tools** > **Show Sprite and Subobject Addresses**.  Disabling this will make the UI more responsive.
* Group hierarchy display.  Another UI-only option to help with scale is **Group Hierarchy with Dashes**.  This is available within the inspector of the top level settings. With this enabled, groups that contain dashes '-' in their names will display as if the dashes represented folder hierarchy. This does not affect the actual group name, or the way things are built.  For example, two groups called "x-y-z" and "x-y-w" would display as if inside a folder called "x", there was a folder called "y".  Inside that folder were two groups, called "x-y-z" and "x-y-w". This will not really affect UI responsiveness, but simply makes it easier to browse a large collection of groups. 
* Bundle layout at scale.  For more information about how best to set up your layout, see the earlier question: [_Is it better to have many small bundles or a few bigger ones_](AddressablesFAQ.md#Is-it-better-t-have-many-small-bundles-or-a-few-bigger-ones)


