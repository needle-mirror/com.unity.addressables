# Changelog
All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).
## [2.7.2] - 2025-08-11
- Fixed issue where builds were failing in Unity Cloud Build due to hash collisions in Addressables during dependency resolution.
- Fixed an issue where remote binary catalogs would mistakenly be listed as having the .json file extension in the Build Report summary tab.

## [2.7.0] - 2025-07-30
- Fixed issue where strikethrough appears on labels when reordering addressables labels in the labels window.
- Changed GetDownloadSizeAsync to actually use an AsyncOperation and run the calculation as part of a coroutine.
- Fix for CCD Upload Issue with Packed Separately Bundles.
- Fixed an issue where you weren't able to assign Sprites in a AssetReferenceAtlasedSprite if the Atlas was a variant.
- Fixed an issue when "Use Defaults" was selected for a Group. When building for Android the local settings were used for remote groups. This effectively disabled caching.
- Fixed an issue where Profiler runtime data was not stripped for non-development builds.
- Improved memory usage when using binary catalogs.
- Improved performance of the Addressable Groups editor window for large projects - this should be noticable when first launching the window, and when searching for assets.
- Fixed an issue where the Addressables Profiler Module would not work properly when using a standalone Profiler process.
- Fix an issue where Custom group settings were not correctly applied to built-in bundles.
- Revert name change to prevent gameobject warning on group Rename
- Added Preserve attribute to prevent stripping of RuntimePath property

## [2.6.0] - 2025-06-03
- Fixed issue where call to LoadAssetsAsync was treating single element keys into an array of characters.
- Fixed binary catalog extraction & optimization tools.  InternalIds of locations were getting resolved with Editor data incorrectly.  They are now copied directly to the output files.

## [2.5.0] - 2025-05-12
- Added IP ping timeout to the Hosting Services window.
- Fixed memory leak when loading Sprite objects from a SpriteAtlas asset.
- Fixed sub-object loading from AssetReferences for types that are not Sprites in a SpriteAtlas.
- Fixed Exception when using third build and load path variables option.
- Fixed bug where identical locations were calculated multiple times in the download size
- Fixed bug where we'd fetch the remote hash at start-up even though catalog updates were disabled

## [2.5.0] - 2025-05-12
- Added IP ping timeout to the Hosting Services window.
- Fixed memory leak when loading Sprite objects from a SpriteAtlas asset.
- Fixed sub-object loading from AssetReferences for types that are not Sprites in a SpriteAtlas.
- Fixed Exception when using third build and load path variables option.
- Fixed bug where identical locations were calculated multiple times in the download size
- Fixed bug where we'd fetch the remote hash at start-up even though catalog updates were disabled

## [2.4.6] - 2025-03-19
- Fixed issue where custom asset providers set in Addressable Asset Settings UI aren't being used.
- Fixed issue where duplicated assets savings in summary tab of the build report is incorrect.
- Moved AddressableAssetEntryTreeViewState into the UnityEditor.AddressableAssets namespace to fix compile errors in user code.
- Fixed Add Report... and Add Report From Folder... in the Addressables Report window so they now add reports properly.
- Fixed an issue where when an Addressable scene was unloaded because a new scene was loaded using LoadSceneMode.Single it was not decremented in the number of scenes loaded in the profiler.

## [2.3.16] - 2025-01-08
- Fixed compile error due to using restricted namespaces
- Moved AddressableAssetEntryTreeViewState into the UnityEditor.AddressableAssets namespace to fix compile errors in user code.

## [2.3.7] - 2024-12-04
- The group edit window saves sort order and enabled columns. Groups are not sorted but can be reordered by dragging and dropping and that state is saved. This ordering has been moved to the Addressables data folder into the file AddressableAssetGroupSortSettings. This file can be added to .gitignore for per user sorting, or can be checked into version control for team sorting.
- Fix "IndexOutOfRangeException" console message when assigning an addressable SpriteAtlas to an AssetReferenceSprite.
- Make SubObjectGUID an Editor only field. It would not work properly at Runtime due to the lack of AssetDatabase, but this makes it explicit at compile time.
- Switch the Runtime key to never use the SubObjectGUID and always use SubObjectName for subobjects.
- CheckCatalogUpdate now returns 1 when there is an update available.
- CanBuildPlayer has been switched to using the Bee postprocessor and should return better error messages on failure.

## [2.3.1] - 2024-09-24
- Added back Analyze Rule toolset
- Fixed issue where the content of the list with Addressable Assets Groups is improperly indented when displayed in Group Hierarchy with Dashes Group View.
- Fixed issue where “Profile rename failed because default profile cannot be renamed“ error is thrown when renaming a new profile in Addressables Profiles.
- Fixed a memory leak where asset bundles would not get unloaded when a scene was unloaded before completing load.
- Fixed loading error when the loading from an assetbundle that is currently being preloaded.

## [2.2.2] - 2024-06-26
- Fix KeyNotFoundException when clicking on local bundles in the profiler.
- Fix bundles incorrectly marked as released in the Profiler when they are still active
- Improved error message when trying to load a catalog in an unexpected file format.
- Fixed issue where operation that uses WaitForCompletion can timeout much earlier than it should.
- The build scripts were reworked so that you can extend them or copy them outside the package without having to fork the entire package.
- A Version field was added to the Addressables object for getting the package version in the Editor.
- Fixed an issue where tearing down the Addressables instance could happen before user tear down code was getting called.
- Sort collections to make serialized editor files deterministic
- Fixed issue where labels on an addressable sub-entry are incorrectly added to the former parent entry.
- Added support for calling Release() on AsyncOperationHandles directly that couldn't before.
- Fixed sub-object loading from AssetReferences for types that are not Sprites in a SpriteAtlas.


## [2.1.0] - 2024-03-19
- Fix "Unable to verify target bucket for Remote Catalog: Not Found. Object could not be found" error
- Fixed caching to prevent unnecessary refreshing of bucket data.
- Sort collections on serialization to prevent unnecessary merge conflicts
- Add warnings and documentation to make it clear you need to release the handle from LoadDependenciesAsync

## [2.0.8] - 2024-01-19
- Documents the behavior of using WaitForCompletion while a bundle is being unloaded.
- Prevent a KeyNotFoundException from being logged to the console.
- Fixed issue where a NullReferenceException occurs when using WaitForCompletion and the max number of concurrent requests is set to 1.
- Fix error message to report not found when loading non-Addressable asset by guid
- Fixed issue where there is missing asset data in the Addressables Profiler for binary catalogs.
- Fixed an issue the error "Cannot read BuildLayout header, BuildLayout has not open for a file" would occur after a build
- Added note about the limitations of the Check for Content Update Restrictions tool.
- Fixed issue where an ArgumentException can occur when viewing multiple assets in the Addressables Profiler.

Fixed an issue where a broken script on any Addressable Asset would make it impossible to select Addressable Assets in the AssetReference inspector
Add migration upgrade prompt for legacy path pairs (ex. RemoteLoadPath)
Add logging of catalog and asset bundle http operations.
Add UI to trigger CCD management API http call logging (requires newer CCD package)
CCD Automatic Profiles can now be one per-profile, rather than one per AddressableSettings instance
CCD Manager is built when using the Build to CCD and the standard Build content menu

## [2.0.7] - 2023-12-12
- Updating ScriptableBuildPipeline reference.

## [2.0.6] - 2023-10-30
- Updating ScriptableBuildPipeline reference.

## [2.0.3] - 2023-08-27
- ProfileValueReference.GetValue is now the preferred method of getting bulid and load paths. A new method has been added that allows token evaluation to be toggled on and off. Using this method ensures that custom build and load paths are handled properly.
- In the Editor when a sprite is no longer available it will be cleared when the property drawer is viewed.
- Add a SubObjectGUID to sprite atlases and use GUIDs for the runtime loading key if is exists.
- Catalog hashes get build with the local catalog now.  This prevents remote catalogs from getting downloaded if they match what was shipped with a player
- Added overloads to LoadAssetsAsync to accept a single string as a key
- Added bundle request retry override.
- Added shared bundle settings to AddressableAssetSettings. Monoscript bundle is always enabled.
- Added more BuildLayout public API
- Fixed issue with misleading error message for loading through asset where the asset is not Addressable in editor.
- Added in safeguard to prevent Compress Local Catalog option from being on alongside Binary Catalogs
- Fixed an issue where ResourceLocation would return a useless value when using Binary Catalogs
- Fixed an issue where there would sometimes be a minor memory leak when using Binary Catalogs
- Fixed an issue where Binary Catalogs would not be cached properly
- Fixed a bug where a GUI style was misspelled in the Editor and was recently fixed
- Fixed an issue where FindAssetEntry returns incorrect values after Undo/Redo
- Fixed a regression where remote provider failures were not retried.
- Fixed an issue where a NullReferenceException occurs when inspecting Addressable Settings
- Fixed an issue where the content update groups list being cutoff by buttons
- Fixed an issue where an ArgumentException occurs when sorting labels and creating a new group.
- Fixed an issue where using binary catalogs causes a crash on Android with ARM7.
- Fixed an issue where a NullReferenceException occurs in GenerateBuildLayout when a group is null.
- Fixed a bug where Timeout and Redirect Limit were getting serialized to different values from what was allowed in the Group schema
- DownloadDepedenciesAsync no longer loads asset bundles into memory
- Fixed issue where ResourceLocations weren't getting compared correctly for properties like AllLocations
- Fixed an issue where scene InternalId collisions were very likely when using dynamic internal asset naming
- Fixed an issue where sprites with normal maps would not be built properly
- Fixed an issue where "Failed to remove scene from Addressables profiler" warning occurs when a scene is unloaded.
- Fixed an exception getting thrown in the Addressables Report when drilling into a bundle chain
- Fixed string deduplication in binary catalogs.  Certain data sets were causing data to expand.
- Removed the Analyze Rule API and corresponding tool
- Removed the Event Viewer API and corresponding tool

## [1.21.18] - 2023-09-23
- Fixed an issue where scene InternalId collisions were very likely when using dynamic internal asset naming
- Fixed an issue where Android paths were being delimited with '\' rather than '/'

## [1.21.17] - 2023-08-16
- Fixed issue where sprite is missing normal texture when using "Use Existing Build" build mode
- Fixed a bug where a GUI style was misspelled in the Editor and was recently fixed
- Fixed an issue where asset loading would occasionally stop working if domain reload was disabled

## [1.21.15] - 2023-08-03
- Fixed an issue where using binary catalogs causes a crash on Android with ARM7.
- DownloadDepedenciesAsync no longer loads asset bundles into memory
- Fixed an exception getting thrown in the Addressables Report when drilling into a bundle chain

## [1.21.14] - 2023-06-14
- Fixed an issue where CleanBundleCache was giving an invalid handle error when Send Profiler Events was turned on
- Fixed issue where provider data wasn't set for catalog downloads, so web request timeouts would default to 0 instead of the given value
- Added the ability to manage Addressables labels from the asset inspector
- Fixed an issue where the Addressables build cache would get cleared even if you pressed no in the popup window.
- Optimised gc allocations with Addressables profiling module data transfer.
- Fixed issue with binary catalog and asset bundle download redirect limit causing an error when set to -1 (default)
- Fixed issue where player preferences for logging runtime exceptions weren't being compiled out using the Editor scripting define
- Fixed an issue where in some cases sprites would not load properly when using a binary catalog

## [1.21.12] - 2023-05-04
- Fixed issue with a potential race conditon when calling GetAssetBundle to load local files using WaitForCompletion
- Fixed compiler error when using the profiler module on a noncaching platform.
- Fixed issue where stack overflow occurs for http requests on Unity 2022.1+ when insecure requests are disallowed.
- Added groups field to Inspector and move assets popup.
- Fixed issue where sprite atlas cannot be assigned to an AssetReferenceAtlasedSprite.

## [1.21.10] - 2023-04-10
- Fixed Addressables profiler not correctly displaying loaded Asset subObjects where a Asset was loaded due to a direct reference to one of its subObjects.
- Fixed an issue where bundles built with the Append Hash to FIlename bundle naming option had strange interactions with the Prevent Updates option
- Improved performance of BuildLayoutGenerationTask

## [1.21.9] - 2023-03-07
- Removed code within WriteObjectToByteList that would never be reached
- Fixed an issue where SceneOps would have update called on them twice per frame
- Fixed an issue where in some circumstances an Operation would continue to have update called on it even after it finished.
- Fixed an issue with Labels window after a domain reload causing a null reference exception.
- Added Remove all unused labels option to labels popup window.
- Fixed issue when not including the address and guid in catalog using content update workflow, would always indicate the asset as changed
- Fixed Addressables profiler module help links to documentation.
- Fixed issue where scenes unloaded due to another scene loading in single mode, would result in the profiler module still reporting the scene as loaded.
- Groups window will now show labels of entries that are not a part of the settings.
- Fixed 404s when uploading files with incorrect environment ID
- Fixed issue where BuildLayoutGenerationTask would sometimes fail in large projects

## [1.21.8] - 2023-02-09
- Optimised PostProcessBundles
- Fixed issue where having a runtime profile variable or property to evaluate web urls on Windows platforms can result in malformed urls.
- Fixed possible `NullRefernceException` when importing new assets and `AddressableAssetUtility+SortedDelegate,Register` was called.
- Fixed an issue where EditorGUI.changed would not be properly set in certain circumstances when updating AssetReferences using the inspector
- Fixed CCD build and release to properly upload static groups
- Fixed CCD build and release to allow both static and managed groups in the same profile
- Include additional object dependency information in build layout report
- Addressables Profiler module compatible with Unity Editor 2022.2+
- Addressables Build Report window added. Compatible with Unity Editor 2022.2+

## [1.21.2] - 2022-12-09
- Fixed issue where folders in Groups window would display the subObjects of assets without expanding the assets.
- Added the ability to copy a subAsset address to clipboard from right click.
- Fixed bundle naming mode option names being unclear when selecting multiple groups.
- Optimised string usage across package
- Updated the way that the Build Layout calculates efficiency to base it off of the dependent file sizes instead of the dependent file count
- Fixed issue where Addressables inspector would not show until Addressables settings have been initialised
- Fixed issue where editor only assets would be marked as changed in the Check for Content Update restrictions window

## [1.21.1] - 2022-10-05
- Fixed issue where CcdManagedData State was not being set correctly
- Added IP ping timeout to the Hosting Services window.
- Fixed issue where loading cached bundles using WaitForCompletion in 2021.2+ results in an error.
- Fixed issue when loading urls with unconverted special url characters such as a space.
- Fixed issue where folders in Groups window would display the subObjects of assets without expanding the assets.
- Added public API to support more detailed Build Layout

## [1.20.5] - 2022-08-03
- Fixed issue where object picker for the AssetReferenceDrawer would cut off longer asset names due to only being as wide as the property drawer.
- Improved performance of gathering assets for an AssetReference.
- Fixed issue where settings hash was not included in settings.json for runtime settings.
- Fixed issue where CheckBundleDupeDependencies() would cause a null reference when no addressable assets are in a project.
- Fixed issue where WebRequestQueue would throw a NullReferenceException when given an aborted web request as a parameter.
- Fixed issue where renaming a label does not dirty and save settings and serialise to file.
- Fixed an issue where an updating catalogs operation would return a 'Succeeded' status even if its load operation failed.
- Fixed issue where hosting service roots would not change to correct platform when switching platforms in editor.
- Fixed issue where ProfileDataSourceSettings was generating wrong LoadPath url for CCD integration
- Fixed issue where ClearDependencyCacheAsync could try and clear a bundle that is actively loading.
- Fixed issue where web request is not disposed when downloading a file using TextDataProvider
- Fixed issue where DisableAssetImportOnBuild sample does not disable asset imports
- Fixed issue where retrying a download after a CRC mismatch would not occur.

## [1.20.3] - 2022-06-14
- Added documentation to several areas (Build, Settings, Profiles, Catalogs, Runtime loading).
- Fixed issue where GatherAllAssets filter would still return subObjects of filtered Assets.
- Fixed issue where content update entries dependent on modified entries and not found as modified by check for content update restrictions
- Fixed issue where the notification for changed static content wasn't getting cleared for assets inside of folders.
- Fixed issue where Sprites belonging to a Sprite Atlas aren't assignable to an AssetReferenceSprite field.
- Fixed issue where RefreshGlobalProfileVariables is called during script compilation.
- Fixed issue where UnauthorizedAccessException occurs when lacking permissions to cache a remote catalog.
- Fixed stack overflow with SortedDelegate and Addressables OnPostProcessAllAssets occurred during invoke a queued invoke and registering a new delegate.
- Fixed issue where SceneLoadParameters were not used when using LoadSceneAsync using SceneLoadParameters.
- Fixed issue where Schema gui with List members would not save when editing in the Group inspector.
- Fixed issue where Serializable types of structs and class members of MonoBehaviour or ScriptableObjects would be returned as a location with GetResourceLocations but would not be loadable.
- Fixed issue where setting default group does not dirty settings. Causing a reload to reset to previous default group.
- Added updated documentation for the 1.20 Content Update workflows
- Fixed issue where AssetReference subasset popup text is always white regardless of Editor skin.
- Fixed issue where newly created assets would not show the Addressables inspector until after a domain reload.
- Optimised Build pass Post Process Bundles when running on a large number of asset dependency trees.
- A warning now gets printed if caching data fails due to Application.persistentDataPath being an empty string
- Fixed issue where pressing the Reset button on the Hosting window would not assign a new random available port number.
- Fixed bug where WaitForCompletion could hang indefinitely under certain race conditions (primarily on Android)
- Fixed a bug where calling WaitForCompletion on a LoadAssetAsync call that was loading from Resources would freeze the editor.

## [1.20.0] - 2022-04-20
- Added ability to get the download size of a given Content Catalog by its resource location
- Add option to save the bundle build layout report as a json or txt file in the Preferences window.
- Added sample for resolving duplicate dependencies to multiple groups.
- AddressableAssetProfileSettings.GetProfileDataById and AddressableAssetProfileSettings.GetProfileDataByName public
- Added ability to load scenes using SceneLoadParameters through Addressables API.
- Made the following API public:
  - AsyncOperationHandle.IsWaitingForCompletion
  - AssetBundleResource.LoadType
  - AssetBundleResource.GetLoadInfo()
  - AssetBundleResource.GetAssetPreloadRequest()
  - AssetBundleResource.Start()
  - AssetBundleResource.Unload()
  - WebRequestQueueOperation.Result
  - WebRequestQueueOperation.OnComplete
  - WebRequestQueueOperation.IsDone
  - WebRequestQueueOperation.WebRequest
  - WebRequestQueueOperation.ctor()
  - WebRequestQueue.SetMaxConcurrentWebRequests()
  - WebRequestQueue.QueueRequest()
  - WebRequestQueue.WaitForRequestToBeActive()
  - ResourceManagerConfig.ExtractKeyAndSubKey()
  - UnityWebRequestUtilities.RequestHasErrors()
  - UnityWebRequestUtilities.IsAssetBundleDownloaded()
  - UnityWebRequestResult.Error.set
  - UnityWebRequestResult.ShouldRetryDownloadError
- Made the following API protected:
  - AssetBundleProvider.UnloadingBundles.get
  - AsyncOperationBase.ReferenceCount
  - AsyncOperationBase.IncrementReferenceCount
  - AsyncOperationBase.DecrementReferenceCount
- Added functionality to extend groups window build menu with pre and post build methods
- Fixed issue where custom analyze rules that are subclasses cannot be registered.
- Added more tooltips to UI
- Fixed issue where loading assets using a Location without initialising first would not initialise Addressables.
- Content Update workflow has been changed to be more streamlined.  New settings have been added to handle Content Update and the previous state .bin file can now be automatically loaded instead of requiring manual selection.
- Set default max concurrent Web requests value to 3.
- Pre-cache delegate list for Completed and CompletedTypeless to reduce GC allocation.
- Fixed issue where Scenes can be incorrectly reported as being in multiple bundles with Bundle Layout Preview analyze rule
- Fixed bug where requests for a ResourceLocation that pointed to a scene in an Addressable folder wasn't returning the location
- Fixed issue where content update could fail to update built-in shaders and monoscript bundles to load from the correct location.
- Cache results of FindEntry to improve performance when no changes are made.
- Fixed an issue where the AddressableAssetEntry returned by GetFolderSubEntry would not include the labels of the entry
- Fixed issue where inherited fast mode scripts would fail to use instance and scene providers set for that build script object.
- Fixed issue where multiple AssetReferences could not be dragged and dropped to a list or array.
- Improved performance when deleting Addressable Asset Groups.

## [1.19.19] - 2022-03-01
- Improved message of InvalidKeyException errors.
- Improved exception message of RemoteProviderExceptions
- Fixed issue where Clear Cache Behavior setting was getting reverted when changed while multi-selecting schemas
- Fixed an issue where when building with Missing References in the groups window a NullRefException would occur
- Added documentation explaining why the user might hit a deadlock when calling WaitForCompletion when loading multiple scenes in succession
- Fixed issue where DownloadDependenciesAsync with merge mode did not unload AssetBundles correctly
- Added ComponentReference and Custom Analyze Rule to Samples folder.
- Fixed issue where BundledAssetGroupSchema custom paths reset to default local paths after a domain reload.
- Added assemblyInfo to manage visible internals
- Fixed issue causing InvalidOperationException when loading Addressable Settings from OnPostProcessAllAsset during a project load without a cached AssetDatabase
- Fixed an issue where calling LoadSceneAsync.WaitForCompletion immediately after loading a scene singly would cause a freeze

## [1.19.18] - 2022-01-31
- Fixed issue where WaitForCompletion would take too long when used in virtual mode.
- Updated the documentation to include information on methods of waiting on asynchronous operations to complete.
- Fixed issue where IOException occurs when autoCleanBundleCache has value of 'true' in Addressables.UpdateCatalogs.
- Improved Addressables class Inspector headers with documentation links.
- Fixed issue in Editor where a large number of AssetReferences causes performance spikes
- Documentation has been added for shared bundles, HTTP restrictions in 2022 editor versions, Samples, and various cleanup
- Fixed issue with missing trace events in build profile trace log
- Fixed issue where adding a AddressablesGroupTemplate could not be added, throwing an Exception
- Added better logging when built in Editor Hosting Service fails to acquire a port
- Improved ordering of when OnPostprocessAllAssets occurs for Addressables settings and windows to ensure Addressable settings are processed first.
- Fixed issue where "Unable to load assets of type" error occurs when loading asset with classes referenced by value in "Use Asset Database" mode.
- Add more documentation about the "Non-Recursive Dependency Calculation" and "MonoScript Bundle Naming Prefix" options in AddressableAssetSettings.

## [1.19.17] - 2022-01-06
- New Projects use hierarchical search mode as the default option for Group search.
- Group referenced by Addressable Schema can now be viewed, but not edited, in the inspector
- Fixed issue where calling Addressables.CleanBundleCache freezes the WebGL player.
- Fixed API inconsistency in CheckForCatalogUpdates.  The API wasn't initializing Addressables if it was the first point of contact in the system.
- Fix issue where opening the Analyze window logs null exceptions after running the "Check Duplicate Bundle Dependencies" rule.
- Add platform type to ""Use Existing Build"" display name in the Addressables Groups > Play Mode Script menu."
- Fixed issue where Scene loading after a content update could result in "RemoteProviderException : Invalid path in AssetBundleProvider: ''.". Fix require a new addressables_content_state.bin to be created."
- Tests for the addressables package are no longer included. These can still be accessed upon request.
- Fixed an issue where calling WaitForCompletion on LoadSceneAsync would sometimes cause a freeze
- Mentioned the AssetBundle loading cache in the docs
- Fixed issue where using WaitForCompletion and loading an AssetBundle through a UnityWebRequest freezes the editor when using 2021.2+.
- Fixed issue where using WaitForCompletion and exceeding the max number of concurrent web requests freezes the editor."
- Updated the docs to use the correct name for the Analyze rule "Bundle Layout Preview"
- Fixed issue where Addressable Asset Entry cached data is not cleared during external changes in some editor versions.

## [1.19.15] - 2021-12-02
- Fix issue where opening the Analyze window logs null exceptions after running the "Check Duplicate Bundle Dependencies" rule.

## [1.19.13] - 2021-11-29
- Removed AddressableAssetEntryCollection upgrade check on opening project. Improving startup performance.
- Fixed issue where GetAllAsset with includeSubObjects did not get subObjects within Assets in an Addressable folder.
- Improved Groups window label dropdown. Adding the ability to search labels and add new labels from the dropdown.
- Added ability Assets from Analyze window, using double click and right click options on the results.
- Improved performance when displaying Addressables header for selected Assets.
- Fixed issue where Groups not marked as "Include in build" was being including in analyse rules.
- Fixed issue where WaitForCompletion will complete Scene operations, where Scenes require further asynchronous loading to complete correctly.
- Fixed issue where AssetDatabaseProvider.LoadAssetAtPath causes a null exception if the asset is not in the AssetDatabase.
- Fixed issue where "Reentering the Update method is not allowed" exception occurs when calling WaitForCompletion during an async method.
- Added FAQ documentation for internal Bundle Id and Asset naming modes.
- Added documentation describing behaviour of WaitForCompletion of Scenes.
- Added documentation for how script changes affect builds.
- Added documentation about Windows file path limit affecting content builds
- Added note about Sprite Atlas options in the documentation.
- Added Sample for custom build and play mode scripts
- Fixed issue where Editor assets were getting included in a build for certain platforms due to path separator character mis-match
- Fix issue with using custom AssetBundleProvider with custom AssetBundleResource.
- Fixed issue where editor hosting profile variables were serialized to AddressableAssetSettings.

## [1.19.11] - 2021-10-23
- Fixed issue with missing reference exception when using addressables where content has not been built.
- Added warning that LZMA compression is not available for WebGL AssetBundles.
- Fixed issue were getting a Group Template fails where the project name or parent directory ended in "Assets".
- Fixed issue where option to build Addressables when building a Player where displayed for unsupported editor versions.
- Fixed issue where hosting services filters ip addresses when entering playmode and no services are in use
- Fixed "Editor Hosted" LoadPath, to work with active local Editor hosting service
- Fixed error where creating new groups would lead to errors if the default build and load path variables were not present in one's profile settings.
- Modified the behavior of AssetReference.editorAsset and AssetReference.SetEditorAsset to be more consistent and intuitive
- Fixed issue where upgrading from versions that didn't have ProfileGroupTypes was causing issues during builds.

## [1.19.9] - 2021-09-30
- Fixing a compile error on platforms where the Caching API is stripped.
- Updating ScriptableBuildPipeline dependency

## [1.19.6] - 2021-09-24
- Fixed issue where built-in shaders and MonoScript Bundles prefix option was not prefixed to Bundle filename.
- Restructured and updated documentation.
- Fixed an issue where graphs in the event viewer would sometimes scroll off the window
- Fixed issue where an AssetReference field cannot be interacted with the tab and enter keys.
- Fixed issue where an AssetReference label is displayed wrong where the AssetReferece is a child of the property being displayed.
- Added documentation for Addressables.CleanBundleCache
- Fixed issue where editing an open Prefab and saving the Prefab will deselect selected Objects.
- Improved performance of displaying Addressables Inspector in a very large Project.
- Fixed issue where buildlayout.txt would contain incorrect bundle names if a group's bundle naming scheme was set to filename
- Fixed an issue where some platforms were caching catalogs that don't support caching
- Fixed an issue where the popup windows for creating new profiles path variables would appear in seemingly random places.
- Fixed an issue where the popup window for creating a Build and Load path variable pair would not properly display its save button
- Added note in Hosting Services docs about modifying firewall settings when testing on other devices.
- Added handling of possible exceptions when caching catalog files.

## [1.19.4] - 2021-08-24
- Removing support for 2018.4
- Added options for building Addressables content as a prebuild step when building Player.
- Optimised StreamingAssets usage to no longer need to be copied into the project (2021.2+).
- Fixed issue where OnDestroy use of Addressables API results in errors when Enter Play Mode Settings are enabled.
- Set AssetEntryCollection is Obsolete, includes auto update process to create Group entries from EntryCollections.
- Updated CheckForCatalogUpdates to properly report any failures that occur while its running.
- Combined BundledAssetGrupSchema CRC settings to a single value.
- BundledAssetGroupSchema Request Timeout will now use time in seconds since last time data wasa downloaded.
- Fixed issue where Exceptions in UnityWebRequest.Send were not caught.
- Updated the way that CompletedOperation events are handled in the Event Viewer to make it easier to associate a given CompletedOperation with its corresponding ChainOperation
- References to Time.deltaTime throughout Addressables are now replaced with Time.unscaledDeltaTime to better match whats described in the API
- Improved the performance of the ProcessAllGroups build step.
- Fixed a bug where having unmatched brackets in a profile's value could lead to a freeze.
- Fixed a bug where certain patterns of values in a profile variable would occasionally lead to an InvalidOperationException while building
- Added check to prevent infinite loop during WaitForCompletion during Asset Database Mode and Simulate Groups Mode
- Users can now supply a callback to receive the UnityWebRequest before being sent by web-based providers
- Added new API to clear the bundle cache for nonreferenced remote asset bundles. UpdateCatalogs has a new optional parameter called autoCleanBundleCache that when enabled will clear the bundle cache for nonreferenced remote asset bundles.
- New public APIs
    - BundledAssetGroupSchema.AssetLoadMode
    - AssetBundleProvider.AssetBundleRequestOptions.AssetLoadMode
    - Addressables.WebRequestOverride
    - ResourceManager.WebRequestOverride
    - AddressableAssetSettings.DisableVisibleSubAssetRepresentations
    - Exposed Auto release parameter added to InitializeAsync
    - BundleRuleBase
    - GenerateLocationListsTask.ProcessInput (formally RunInteral)
    - BuildScriptPackedMode.PrepGroupBundlePacking
    - UnloadSceneAsync APIs with exposed UnloadSceneOptions parameter
    - Addressables.CleanBundleCache
    - New parameter for Addressables.UpdateCatalogs to auto clean the bundle cache
       - ProfileGroupType introduces a new workflow of grouping profile variables in the Addressables Profiles window, otherwise known as path pairs.

## [1.18.15] - 2021-07-26
- Improved Addressables inspector for Assets.
- Fixed issue where the hosting window would use an exceptionally high (8-20%) amount of CPU while open with a hosting service created
- Added update on profile change, changed to remove preceding slashes and change all to forward slash for hosting service
- Added documentation explaining why we are unable to support WaitForCompletion (sync Addressables) on WebGL

## [1.18.13] - 2021-07-13
- Fixed issue where Addressables would not use a custom Asset Bundle Provider if the default group was empty
- InvalidKeyExceptions are now correctly thrown as InvalidKeyExceptions, as opposed to before, where they were thrown as System.Exceptions. Please note that this may break any checks that rely on InvalidKeyExceptions being thrown as System.Exception
- Fixed issue where UnauthorizedAccessException is logged during a build if content_state.bin is locked by version control integration.
- Fixed issue where user defined callbacks can cause unexpected behavior for async operations that are automatically released.
- Fixed issue where Content Update would not include folder entry sub entries.
- Fixed issue where NullReferenceException was logged when multi-selecting with Resource in Groups TreeView.
- Fixed issue where Check for Content Update Restrictions excludes dependencies for folder entries.
- Fixed issue where AddPostCatalogUpdatesInternal would attempt to remove the hash from strings that did not include a hash, occassionally leading to incorrect bundle names in catalog.json
- Load AssetBundles Asynchronously from UnityWebRequest for supported Editor versions
- Fixed issue where hidden files were being flagged in GetDownloadSizeAsync when "Use Asset Database (fastest)" is enabled.
- Added logic for auto releasing completion handle in InitializeAsync
- Fixed issue where AssetBundleProvider would fail to retry on download dailed
- Fixed bug where Fast Mode wasn't returning the correct resource locations or their types, especially for sub-objects.
- Fixed bug where Hosting Service was not saving if enabled between domain reloads
- Fixed bug where Scenes with Group setting Asset Internal Naming Mode of Filename failed to load
- Fixed bug where Hosting window would occassionally be empty on startup.

## [1.18.11] - 2021-06-15
- Improved performance of Labels popup in Groups Window.
- Added "Copy Address to Clipboard" Context menu option in Groups Window.
- Added AssetLoadMode option to AddressableAssetsGroup, adds "Requested Asset And Dependencies" and "All Packed - Assets And Dependencies" load methods.
- (2021.2+) Improved performance of copying local buld path Groups built content when building a Player.
- Removed "Export Addressables" button from groups window because it was no longer in use.
- Fixed issue where loading remote catalog from .json fails when Compress Local Catalog is enabled.
- Fixed issue where loading remote catalog from bundle on WebGL fails when Compress Local Catalog is enabled.
- Added multi-project workflow documentation
- Made CacheInitializationData.ExpirationDelay obsolete
- Improve Hierarchical Search performance in Groups Window.
- Build now fails earlier if invalid or unsupported files are included.
- Fixed issue where renaming Group and Profiles would not cancel using Escape key.
- Fixed issue where StripUnityVersionFromBundleBuild and DisableVisibleSubAssetRepresentations were not being serialised to file.
- Updated content update docs to be a little more clear
- Made ExpirationDelay on the CacheInitializationObjects obsolete
- Reduced amount of main thread file I/O performed during AssetBundle loading

## [1.18.9] - 2021-06-04
- Added "Select" button for Addressable Asset in Inspector to select the Asset in the Addressables Groups Window.
- Reduced the number of file copies required during building Addressables and moving Addressables content during Player build.
- Fixed issue with AssetReferenceUIRestriction not working with Lists and Arrays.
- Optimised loading AssetBundles to avoid redundent existing file checks.
- Fixed issue with folder asset entries throwing null ref exceptions when doing a Check for Content Update Restriction
- Added documentation about how to implement custom operations with synchronous behavior
- Added option on AddressableAssetSettings to strip the Unity version from the AssetBundle hash during build.
- Added documentation about useful tools you can use when building Addressables content with a CI pipeline
- Added Import Groups tool to Samples folder.
- Updated documentation for setting up and importing addressable assets in packages."
- Fixed issue where multi-group drag and drop places groups in reverse order.
- Fixed issue where an asset entry is no longer selected in the Project window after it is modified on disk.
- Fixed simulated play mode when "Internal Asset Naming Mode" was set to something other than "Full Path"
- Fixed issues with WaitForCompletion getting stuck in infinite loop during failed operations
- Organised AddressableAssetSettings GUI into more distint setting types.
- Fixed issue where the wrong operation would sometimes be returned by the cache when a project contains over 10K addressable assets
- Added path pairs feature
- Fixed issue where AsyncOperationBase.HasExecuted isn't being reset when the operation is reused.
- Added check to ensure that ResourceManager.Update() is never called from within its own callstack.
- Added ability to rename labels from the label window.
- Added the DisableVisibleSubAssetRepresentations option in Settings.

## [1.18.4] - 2021-05-06
- EditorOnly tagged GameObjects in Scenes are no longer detected as duplicates for Scene Analyze results.
- Fixed issue when dragging multiple groups around within the groups window to set their display order.
- Reimplemented AsyncOperationBase.Task API to use TaskComppletionSource instead of creating a background thread.
- Fixed issue where remote .hash file was still being requested when Disable Content Catalog Update on Startup was enabled
- Fixed issue where AssetReference variable names weren't consistently formatted in the inspector
- Fixed bug where Completed callback was not called the same frame for some async operations when WaitForCompletion is used.
- Added Samples to the package.  These can be added to the project through the Addressables page in Package Manager

## [1.18.2] - 2021-04-20
- Where available use synchronous load api's when AsyncOperationHandle.WaitForCompletion is called.
- Fixed issue where loading of Prefabs and ScriptableObjects in "Use Asset Database" and "Simulate Groups" play mode could cause changes to source Assets. Now those play modes will return instanced copies of the Assets.
- Added "Catalog Download Timeout" to AddressableAssetSettings, used for setting a timeout for .hash and .json catalog file downloads.
- Fixed issue where order of data in catalog.json can change. Order is now sorted to be deterministic.
- Added best practice documentation for define dependant compilation during build time.
- CompletedOperation are now returned to the op pool so they can be reused
- Made AddressableAssetSettings.ContentStateBuildPath public api access.
- Add option for building MonoScript bundle. This approach improves multi bundle dependencies to the same MonoScript.
- Added documentation for AddressableAssetSettings options.
- Improved error handling of failed unity web requests and some other operations.
- Users can now look into the InnerException property of an operation's exception for additional details"
- Fixed issue where .json and .asmdef files in the root of a package folder cannot be marked as Addressable.
- Fixed issue where unmodifiable assets cannot be marked as Addressable.
- Exposed more tools for making custom build scripts
- Exposed InvokeWaitForCompletion to be inheritable by custom operations
- Fixed issue where an url was improperly parsed by LoadContentCatalogAsync() if it contained query parameters
- Fixed issue where the post assigned to a hosting service was changing on domain reloads
- Add option for building asset bundles using "Non-Recursive Dependency calculation" methods. This approach helps reduce asset bundle rebuilds and runtime memory consumption.
- Add upload speed option to the http service settings. Downloads will be provided by the rate set in Kbp/s
- Add an option to force using UnityWebRequest even when AssetBundles are local
- Fixed issue with WebRequestQueue where web requests weren't getting queued correctly
- Fixed issue where looking for default group would spam null reference to GUI if Built In data group was deleted/null

## [1.17.17] - 2021-04-06
- Add AssetPostprocessor for AddressableSettings after AssetDatabase is Initialised, if not yet initialised on initial project launch.
- Removed serialisation of m_MainAsset and m_TargetAsset from Group entries.
- Fixed a warning "CacheInitialization.CacheInitOp.m_UpdateRequired'' is assigned but its value is never used" when building for platforms that don't have caching enabled
- A message is printed on successful Addressable build
- Properly save profile variables when switching profiles
- Fixed bug where multi-selected Addressable Groups weren't all getting set dirty on an edit.
- Fixed bug where Fast Mode wasn't respecting Log Runtime Exceptions setting
- Implicit assets are now taken into account when using applying a label restriction on an asset reference

## [1.17.15] - 2021-03-23
- Fixed FileNotFoundException when using bundle naming mode "Filename" with Unity Cloud Build.
- Fixed a bug where LoadAssetsAsync handle Completed callback is invoked before all individual Asset callbacks.
- Added in Asset validator on Editor startup.  This ensures that assets deleted when the editor was closed are removed from Addressables.
- Fixed bug where the current amount of downloaded bytes was not properly updated

## [1.17.13] - 2021-03-10
- Fixed issue when loading a Sprite from a SpriteAtlas from an Addressable folder in AssetDatabase mode.
- Fixed bug in AssetReference "Make Addressable" functionality (when referencing an asset no longer addressable)
- Fixed bug with cyclic references in profile variable causing an infinite loop.
- Fixed bug where cached asset type could get stuck with DefaultType, an invalid Editor type
- Fixed issue where AsyncOperationHandle.Completed is called after AsyncOperationHandle.Task returns when the handle is already done.
- Fixed some faulty logic in GetDownloadStatus() when errors occur
- Removed extra dependencies that were being flagged as modified when running Check For Content Update Restrictions.
- Fixed a bug where the result of a Task could be inconsistent and return null given certain race conditions
- Fixed bug where UnloadSceneAsync decreased ref count more than once, and added unload scene to Release if ref count goes to zero
- Fixed issue where a popup appears when an AddressableAsset file is being modified even if the file is checked out locally.
- Fixed bug where fast mode wasn't showing events in the profiler
- Remove check for isUpdating and isCompiling so GetSettings(true) still tries to load the settings when compiling or updating
- Fixed issue where modified local static bundle dependencies fail to load after updating a previous build. Fix is compatible with older shipped content.

## [1.17.6-preview] - 2021-02-23
- Fixed issue where OnGlobalModification events would be EntryMoved when adding new Entries instead of EntryAdded.
- Fixed issue where a previously built player fails to load content after running Content Update with missing local bundles
- Fixed bug where ClearDependencyCacheAsync was throwing invalid handle exceptions if auto releasing the handle
- Fixed a bug when SerializeReference entries in link.xml for addressable was causing Unity linker to fail.
- Added results out parameter to AddressableAssetSettings.BuildPlayerContent.

## [1.17.5-preview] - 2021-02-08
- Fixed performance issue when disabling "Addressable" for multiple Assets in the Inspector.
- Added option to set the build path of addressables_content_state.bin file.
- The buildlogtep.json file is not generated when building the catalog bundle.
- Fixed invalid handle exception getting thrown when static AssetReferences were used with domain reload turned off
- Fixed catalog using invalid load path for Groups built with "bundle naming mode" "Filename".
- Added option to set custom prefix on the unitybuiltinshader AssetBundle
- Added documentation explaining how dependencies affect Content Update
- Sub-assets with arbitrary main type can now be assigned to an asset reference if types match

## [1.17.4-preview] - 2021-01-27
- Removed unnecessary logging when deleting temporary Addressables build data.
- Added WaitForCompletion() on AsyncOperationHandles.  This allows async operation to be executed synchronously
- Alphanumeric sorting in the group window can be enabled through a setting in the editor preferences
- Change to set IgnoreFailures with LoadOptions.IgnoreFailures stored in the IResourceLocation.Data if not null
- Fixed issue when loading legacy Resources from Addressables using the guid when playmode is set to AssetDatabase.
- Fixed some compile warnings on 2020.2
- Change to use full path for name of cached catalog.

## [1.17.2-preview] - 2021-01-14
- Add silent fail option to providers to get rid of error when cache not found as expected
- Hierarchy now fully displayed in search results when 'show groups as hierarchy' and 'hierarchical search' options are enabled
- OnValidate is now called when an AssetReference changes
- Fixed bugs in Use Asset Database play mode related to multiple folders with matching addresses
- Made the following APIs public:
  - ResourceManager.CreateChainOperation
  - AddressablesAnalyzeResultData
  - AddressableAssetSettings.OptimizeCatalogSize
  - BundledAssetGroupSchema.AssetNamingMode
  - BundledAssetGroupSchema.IncludeAddressInCatalog
  - BundledAssetGroupSchema.IncludeGUIDInCatalog
  - BundledAssetGroupSchema.IncludeLabelsInCatalog
  - BundledAssetGroupSchema.InternalIdNamingMode
  - BuildScriptBase.Log
  - ResourceManagerRuntimeData.AddressablesVersion
  - ProjectConfigData
    - ProjectConfigData.ShowSubObjectsInGroupView
    - ProjectConfigData.GenerateBuildLayout
    - ProjectConfigData.ActivePlayModeIndex
    - ProjectConfigData.PostProfilerEvents
    - ProjectConfigData.LocalLoadSpeed
    - ProjectConfigData.RemoteLoadSpeed
    - ProjectConfigData.HierarchicalSearch
    - ProjectConfigData.ShowGroupsAsHierarchy
  - BuildLayoutGenerationTask
  - BuildLayoutGenerationTask.BundleNameRemap
  - ExtractDataTask.BuildContext
  - ContentCatalogData.SetData(IList<ContentCatalogDataEntry> data, bool optimizeSize)
  - ContentCatalogData(string id) constructor
  - ContentUpdateContext
    - ContentUpdateContext.GuidToPreviousAssetStateMap
    - ContentUpdateContext.IdToCatalogDataEntryMap
    - ContentUpdateContext.BundleToInternalBundleIdMap
    - ContentUpdateContext.WriteData
    - ContentUpdateContext.ContentState
    - ContentUpdateContext.Registry
    - ContentUpdateContext.PreviousAssetStateCarryOver
  - RevertUnchangedAssetsToPreviousAssetState
    - RevertUnchangedAssetsToPreviousAssetState.Run
  - AddressableAssetEntry.GetAssetLoadPath(bool isBundled, HashSet<string> otherLoadPaths)
  - AddressableAssetSettings.IgnoreUnsupportedFilesInBuild

## [1.17.0-preview] - 2020-12-13
- Added option to clear other cahced versions of asset bundles when a new version has been loaded.
- Added options for internal naming of asset bundles.  This will allow for deterministic naming to avoid unintended diffs for content updates.
- The "Ignore Invalid/Unsupported Files" option is now saved in the settings
- Fixed issue where Filename only bundle naming schemas were overwriting old bundles prematurely in content update.

## [1.16.19] - 2021-04-08
- Fixed an issue where the group property of the AddressableAssetGroupSchema was not persisted, and could get lost when objects were reloaded

## [1.16.18] - 2021-03-23
- Fixed compile warning in Unity 2020.2+

## [1.16.17] - 2021-02-25
- Updated group rename logic to support engine AssetDatabase fix. Change should be transparent to users.

## [1.16.16] - 2021-01-20
- Updated dependency versions for testcase fix

## [1.16.15] - 2020-12-09
- Addressables link.xml should now have it's own folder.
- Fixed an issue where InvalidKeyException was getting thrown when calling GetDownloadSizeAsync on scenes
- Resources folders inside Unity packages now get added to the Built In Data group
- Fixed issue where getting selected subasset would cause an error if any subassets' type was null

## [1.16.13] - 2020-11-18
- Added option to invert the display of CheckBundleDupeDependencies Analyze rule
- Fix GatherEntryLocations for scenes when parameter type is null
- Added some API docs for RuntimeBuildLog and AnalyzeResultData that were missing.
- Updated docs to explain the use of profile variables a little better.
- Added ability to toggle Check Duplicate Bundle Dependencies analyze rule results to be arranged by Group or Asset
- Allow assets that are inside a com.unity* package to be marked as addressable

## [1.16.10] - 2020-11-04
- Added internal naming option for the Bundled Asset Group Schema.  Instead of using the full path, there are options to use the asset guid or the hashcode of the guid.  These values are stable and wont change if the asset path changes, reducing the need to rebuild a bundle if paths change but contents do not.  The internal ids stored in the content catalog will generally be shorter than asset paths - 32 bytes for the full guid, 8 bytes for the guid hash.
- Added option to exclude sub catalog entries by file extension
- Added options to exclude catalog entries for address, labels, and guids
- Added option to optimize catalog size by extracting duplicated string in urls and file paths
- Fixed issue where ResourceLocations were returning null for the ResourceType.
- Added warning to build when an Addressable Group doesn't have any AddressableAssetGroupSchemas
- Fixed issue where resource folder search was case sensitive for Mac and Linux
- Fixed issue where warnings were getting logged incorrectly when marking an asset as Addressable using the checkbox in the inspector.
- Fixed issue where an AssetReference's cached asset is not reset when the underlying asset re-imports.
- Fixed issue where we were still checking for CRC when a bundle was cached.
- Fixed bug when using Play Mode Script "Use AssetDatabase (fastest)", and calling Addressables.LoadContentCatalogAsync would fail when it had not been cached.

## [1.16.7] - 2020-10-21
- Fixed issue where InvalidHandle errors were getting thrown if an operation failed with releaseDependenciesOnFailure turned on.
- Fixed group build and load paths not being saved when editing multiple groups at once
- Changed Analyze Result data to be cached in the Library.  Result data was previously stored in Assets/AddressableAssetsData/AnalyzeData/AnalyzeRuleData.asset.  It is now stored in Library/com.unity/addressables/AnalyzeData/AnalyzeRuleData.json.  If detected, the Assets - version of the Analyze data will be automatically cleaned up.
- Fixed line in AsyncOperationHandle documentation that told the wrong API for acquiring a handle
- Moved the content update documents to their own page.  Expanded and clarified information on the update process

## [1.16.6] - 2020-09-30
- Group hierarchy support in groups window by detecting '-' in group name
  - This can be turned on & off in the addressable asset settings inspector: Group Hierarchy with Dashes
  - This only affects the visual display, groups are still stored in the main settings object in a flat list
  - The group name is unaffected.  If you name a group "x-y-z" that will be it's name, but, with the option on, it will display as if it was in a folder called "y" that was inside a folder called "x"
- Fixed fast mode resource locator Keys property to expand all possible keys when accessed.  For large projects with many addressable entries and folders, this property may be slow when called for the first time.
- Added detailed build layout feature. See documentation for details.
- Fixed issue where assets in Resources weren't show full key in Groups window
- Fixed issue where loading Addressables from a different project was throwing errors.
- Fixed WriteSerializedFiles profile event timings when using the detailed build log
- Selecting multiple Resources and checking "addressable" now display a single popup
- Fixed CreateArrayResult wouldn't work with classes derived from Object, only the base class, so not for ScriptableObject. Also added test
- Fixed exceptions not handled while loading ContentCatalog
- Fixed issue where passing false into releaseDependenciesOnFailure was still releasing dependencies on failure
- Fixed issue where failed operations could over release their dependencies.
- Changes to an AssetReference rendered by AssetReferenceDrawer now register as a GUI change
- Added a checkbox in settings to ignore invalid/unsupported files during build
- empty folders are cleaned-up when moving multiple resources fails
- fixed bug where an error would occur when moving resources for paths without extensions
- Fixed issue where AddressableAsset files locked by version control couldn't be modified.

## [1.16.1] - 2020-09-15
- Fixed bug where some files would not be created in the right folder if the user moved its addressables config folder elsewhere
- Fixed determanism issue where bundles could have different names after Editor restart
- Added a blurb to the documentation explaining you have to pack an atlas before the sub objects will show up in the groups window
- Added "addressable" checkbox when viewing package assets in the Inspector.
- Fixed issue where GatherAllAssets would not retrieve assets located in package resources folders.
- Fixed issue where temporary StreamingAssets folder are recreated due to un-deleted meta files during a player build
- added Equals implementation for typeless AsyncOperationHandle
- When AssetReference MainAsset changed, reset SubObject
- resource manager callback leak fixes
- Packed Playmode build logs regarding BuildTargets now show up in PlayMode
- Additional Fast Mode optimizations
- Fixed issue where a released operation was not properly cleaned-up
- Fixed issue where renaming an AssetGroup with a name that contained a period led to unusual renaming behavior.
- Removed Analyze Rule "Check Sprite Atlas To...".  This check was not actually valid.  See "SpriteAtlas dependencies" section of "Memory Management" page in Addressables documentation for more information.
- UnloadSceneAsync calls that attempt to unload a scene that is still currently loading are now chained with the load operation and will complete after the load is finished.
- The MaxConcurrentWebRequests exposed on the AddressableAssetSettings object now gets set during runtime initialization
- Fix performance issues drawing AssetReferences with subassets caused by earlier changes to AssetReferenceDrawer
- Fixed bug where Addressables.ClearDepenendcyCache wasn't clearing the cache.
- AssetReferenceUILabelRestriction attribute now works properly on references in nested classes

## [1.15.1] - 2020-08-25
- Change to not allow the same AssetReference to LoadAssetAsync or LoadSceneAsync twice if current handle is valid, instead log an error with note about how to get the valid handle
- Fixed issue where disabled asset groups data would be included in the addressables_content_state.bin file for the build.
- Add ability to use custom ResourceManager exception handlers by not overwriting it in InitializeAsync if it is not null
- Fixed bug where Content Update would not use asset bundle load options for unchanged static remote bundles.
- Fixed LoadAssetAsync<IList<T>> to return the same set of objects in all play modes.  The main asset is always first and hidden objects are ignored.
- Changed keys parameter for many Addressables APIs to be IEnumerable instead of IList<object>.  This allows for passing collections of AssetReferences or AssetLabelReferences directly instead of requiring them to be copied into a new List<object>.
- Fix bug where lists of AssetReferenceSprites were not displayed or set right by AssetReferenceDrawer. Also fixed where multiple selected objects in project hierarchy couldn't set all lists of AssetReferences elements.
- Added better error logging when unrecognized file in build.
- Added error log when building asset bundles during player build.
- Added "Hide Events" context menu option in Event Viewer
- Fixed a bug where running the "Check Scene to Addressable Duplicate Dependencies" analyze rule multiple times would cause a duplicate key exception
- The "Check Scene to Addressable Duplicate Dependencies" analyze rule now only considers scenes that are enabled in the build settings.
- Fixed a bug where an error would be thrown when Unity 2019 opens and if the hosting window was previously left open
- Fixed a bug where changes to a service where not applied in the hosting window
- Fixed a bug where profile selection in the inspector was incorrectly reverted to the default one on domain reload
- Added documentation for LoadResourceLocationsAsync
- Added documentation for ResourceManager.ExceptionHandler
- Added documentation for AddressableAssetSettings.BuildPlayerContent
- Added documentation for LoadSceneAsync
- Added ScriptableBuildPipeline Build Callbacks to Addressables Build Scripts
- Temporary files made during bundled catalog creation are now properly cleaned up
- Inspector window now opens if it was closed when inspecting addressable settings
- Fixed bug where AsyncOperation.PercentComplete was returning 100% when IsDone was false before the operation had started.
- Progress bar is no longer updated for every entry while running Analyze rules for performance purposes.
- Fixed loading of scenes from scenes list through Addressables. Clears out an InvalidCastException that occured on init.
- Fixed issue where AssetReference wasn't able to load Addressable assets in folders during AssetDatabase Mode.

## [1.14.2] - 2020-08-11
- Addressables now logs the package version on initialization.
- Renamed Build Bundle Layout analyze rule to Bundle Layout Preview
- Marked RawWriteOperation obsolete.
- Marked SceneRawWriteOperation obsolete.
- AsyncOperationHandle<bool> ClearDependencyCacheAsync has been added.  The new API takes an autoReleaseHandle parameter and returns the AsyncOperationHandle.
- Made the AsyncOperationHandle in AssetReference public.
- Fixed loading of items from Resources and the built in ScenesList.
- Improved the performance of loading local content on Android by using LoadFromFileAsync instead of UnityWebRequest. Please note that the bundle compression mode for all local content (on any platform) should be LZ4 or uncompressed to have optimal load performance.
- Fixed issue where some Addressables settings were not being saved if they were only serialized properties or textfields like 'Build Remote Catalog'
- Fixed a bug where DiagnosticEvents would be created even if 'Send Profiler Events' was set to false.
- Refactored the DebugNames of many of the most common implementations of AsyncOperationHandle to improve readability in the event viewer.
- Events in the Event viewer should now display more accurately in the case of Repeated loads and unloads of the same object.
- AddressableAssetEntry now overrides ToString() to return the address of the entry
- Added support for setting multiple assets and subasset references at a time for field in GameObject script in the AssetReference Inspector
- Improved performance of the GenerateLocationLists task
- Refactored DiagnosticEventCollector.RegisterEventHandler so that events are always handled in frame order.
- Fixed bug where the Event Viewer would not work when connected to a standalone player.
- Added docs describing the process of connecting the Event Viewer to a standalone player.
- Fixed exception that was getting thrown on Editor restart regarding accessing EditorSettings.enterPlayModeOptionsEnabled during serialization.
- Added MaxConcurrentWebRequests option to the AddressableAssetSettings.
- Added GetDownloadStatus method to AsyncOperationHandle.  The DownloadStatus struct returned will contain the total number of bytes needed to be downloaded and the current number of bytes already downloaded.  Cached AssetBundles will not be included in the count and if everything is cached, the values in the struct will be zero.
- Added Documentation for the following:
  - InstantiateAsync
  - DownloadDependenciesAsync
  - LoadContentCatalogAsync
  - UpdateCatalogs

## [1.13.1] - 2020-07-28
- Made `AssetReferenceT<TObject>` be Serializable.  This will only matter if using Unity 2020.1 or later.
- Added AddressableAssetSettings.ContiguousBundles option, which when enabled will improve asset loading times.
  - In testing, performance improvements varied from 10% improvement over all, with improvements up to 50% for large complex assets such as extensive UI prefabs.
- Add New Build unclickable No Build Script Available option when no valid builder is found and added line in docs to explain what is needed
- Fixed bug where dragging a non addressable asset from an addressable folder in project viewer to AssetReference field would mark the asset as addressable and put it in the default group
- Fixed bug where enumerate exception is being thrown when expanding a group folder containing subfolders in the Addressable Groups window.
- Changed to only ask to convert legacy bundles when AddressableAssetSettings is first created or when selected from the Tools menu
- Fixed bug where clicking on an AssetReference property won't ping the referenced asset in the Project window.
- Fixed bug where GetDownloadSizeAsync was returning non-zero values for cached AssetBundles.
- Removed Event Viewer Record button because it didn't do anything.
- Fixed bug where changes made through the AddressableAssetProfileSettings API would not be immediately represented in the Profiles Window.
- Fixed bug where Instantiation and EventCount events in the Event Viewer would not update as expected.
- Fixed bug where events that occurred immediately after entering play mode would not be properly represented in the Event Viewer.
- Fixed bug where Instantiation and EventCount events would not display their most recent value when inspected in the Event Viewer.
- Added Documentation for the following:
  - LoadAssetAsync
  - LoadAssetsAsync
  - InitializeAsync
  - TransformInternalId
- Fixed bug where changing the value of "Optimize Mesh Data" in PlayerSettings doesn't affect bundle building until the old build cache is deleted.
- Expanded bundle dependencies so that loaded bundles maintain a reference to all bundle they references. This fixes various bugs when unloading and reloading a bundle that is being referenced by another bundle.

## [1.12.0] - 2020-07-14
- Implemented Undo/Redo capability for the Profiles Window.
- Fixed bug where the Profiles Window would occasionally throw a NullReferenceException when making a new profile.
- Added RenameProfile to the AddressableAssetsProfileSettings API
- Added error messages for failed attempts at renaming a Profile
- Fixed bug where when there are AssetReferences with the same name but in different Addressable groups only one could be selected in field dropdown
- Fixed bug surrounding addressable sprites that were also in a SpriteAtlas
- Fixed bug where loading a scene in a package would only load an empty scene with no contents.
- Fixed bug where Event Viewer window would always be empty.
- LinkXmlGenerator moved to the Scriptable Build Pipeline package in the UnityEditor.Build.Pipeline.Utilities namespace.
- Added documentation to explain how to make packages addressable.
- Fixed bug where ArgumentException errors are thrown when selecting a prefab from a read-only package.
- Fixed bug where setting AssetReference property to null wasn't dirtying the asset
- Fixed a bug where IResourceLocations were returning a false positive on comparison.
- Added error checking to make sure that an address doesn't have '[]'.

## [1.11.2] - 2020-06-15
- Refactored Play Mode Script for "Use Asset Database" to pull data directly from the settings.  This reduces the time needed to enter play mode.
- Added scrollbar to the Label dropdown
- Fixed misleading dialog box shown to the user when there are unsaved scenes.
- Fixed bug where DownloadDependenciesAsync always returns an AsyncOperationHandle with a null task.
- Fixed bug where AddressableAssetSettings.asset is always being written to disk whenever something is changed in OnPostProcessAllAssets, including asset modified, moved, group created or deleted
- Revamped Profiles window to a two panel layout.
- Fixed issue with Profiles window where changes would occasionally not be serialized to the settings asset.
- Fixed bug where an op with some failed dependencies would never release the ones that had succeeded.
- Added optional parameter "releaseDependenciesOnFailure" to LoadAssetsAsync to handle the scenario of partial success.  This is when there are multiple locations being loaded, and not all succeed.  In the partial success scenario:
  - By default, the new parameter is true, and all successful parts will be released.  The .Result on the returned handle will be null and Status will be Failed
  - When false, the returned .Result will be a List of size matching the number of matching locations.  Any failed location will correlate to null in the List, while successful locations will correlate to valid objects in the List.  Status will still be Failed in this scenario.
- Bundles that fail to load from the cache are now removed from the Cache and will be redownloaded.
- Added option to disable CRC checks for cached AssetBundles on BundledAssetGroupSchema under Advanced Options.
- If null is passed into Addressables.UpdateCatalogs(...) for the list of catalogIds, CheckForCatalogUpdates will be called automatically.
- Added null reference check when running InitializationObjectsOperation to take failed RuntimeData operations into account.
- Disabled hitting ENTER on an AssetReference inspector to open popup.  The drawer does not know which AssetReference to associate the popup should that MonoBehaviour have more than one. So disabling is unfortunately the only safe option.
- Fixed issue where assets located in subfolders marked as addressable would be added to build content multiple times.
- Fixed bug where Groups window hierarchical search was not filtering the group contents.
- Fixed bug with Groups window flat search not sorting.

## [1.10.0] - 2020-05-28
- Fixed hosting service not working for special characters in addressable asset address
- Fixed bug where tracked scene instance operation handles weren't matching the handles returned to the user.
- Fixed bug where Sprite Atlas ResourceProvider wasn't getting added to list of ResourceProviders.
- Fixed bug where pack separately groups were rebuilding all bundles when an asset was added or removed from the group.

## [1.9.2] - 2020-05-21
- Improved the performance of GenerateLocationLists.
- Fixed AssetReferenceLabelUIRestriction not working for private fields
- Fixed AssetReferenceDrawer OnGui changing text of static variable GUIContent.none
- Updated documentation to explain what's happening when DontDestroyOnLoad GameObjects are having their dependencies removed when the scene they originate in is unloaded.
- Using a more efficient method of gathering the Addressable entries for the AssetReferenceDropdown UI.
- Fixed bug surrounding how "Use AssetDatabase" build script handles deleted assets.
- Fixed issue where ContentUpdate was throwing an exception if a dependency wasn't in the previous build.
- PercentComplete calcluation updates to correctly take progress callbacks on ProviderOperations into account.
- Added support for Enable Play Mode Options in 2019.3+
- Fixed issue where diagnostic events are still being sent to the player regardless of the value of "Send Profiler Events".
- Added error checking to make sure that a group doesn't have both a PlayerDataGroupSchema and a BundledAssetGroupSchema.
- Fixed issue where InitializationObjects were causing the InitializationOperation to hang.

## [1.8.4] - 2020-05-20
- Taking an updated scriptable build pipeline that reverts a recent hashing change.

## [1.8.3] - 2020-04-07
- Option to disable sprites and subobjects has been added to the Groups window Tools menu. This option is persisted per user, not with the project.
- Catalog entries for subobjects and sprites are no longer serialized into the catalog.  These are generated at runtime with a custom resource locator.
- Added missing error logs to various failure cases.
- Fixed subobject parsing to treat anything between the first '[' character and the last ']' as the subobject name instead of the last '[' and the last ']'.
- Changed the display of AssetReference types in the inspector from a dropdown to look like an object reference.
- Added the option to compress the local content catalog by packing it in an asset bundle.
- Added method in settings to retrieve all defined labels.
- Fixed PercentComplete in ChainOperation
- Fixed main settings asset getting marked dirty when making builds.
- Fixed issues with Content Update when entry with dependant entries was modified.
- Fixed "Unknown Exception" issue caused by releasing certain operation handles too many times.
- Added link to online documentation in the addressable windows menu.
- Fixed bug where two assets with the same address packed separately was causing an error.
- Fixed issue where loading a content catalog multiple times was throwing exceptions.
- Made it so using the LoadContentCatalogAsync API creates a ResourceLocation that allows those catalogs to be updated properly.
- Fixed bug where the scene in a recycled InstanceOperation wasn't being cleaned.
- Fixed bug where an invalid location would be created for assets that weren't in a Resources folder, but were part of a group with the PlayerDataGroupShema.
- Schema asset file name uses group name instead of GUID. For example: GroupName_SchemaName.asset
- Fixed text that was being cutoff in the CacheIntializationSettings inspector view.
- During init, if a remote catalog is expected but not present, this will fail silently. Fixed a bug where that silent failure showed up later as an "unknown error in async operation".
  - if you wish to see a log for the failed catalog retrieval, enable ADDRESSABLES_LOG_ALL as a scripting define symbol.
- Fixed bug where renaming a class referenced by an AddressableAssetEntry causes an InvalidKeyException.
- Fixed performance regression in ContentUpdateScript.SaveContentState
- Fixed performance regression in BuildScriptPackedMode.PostProcessCatalogEntries
- Updated to scriptable build pipeline 1.7.2 which includes many build optimizations - see SBP changelog for details

## [1.7.5] - 2020-03-23
- Fixed null key exception when building that happened when an invalid asset type is in a resources folder.

## [1.7.4] - 2020-03-13
- Improved building catalog data speed.
- Various minor optimizations related to handling sub objects.
- Added progress bar to the catalog generation part of the build process.
- Gave initialization objects an asynchronous initialization API.
- Made it so a CacheInitializationObject waits for engine side Caching.ready to be true before completing.
- Fixed a bug when accessing AssetReferenceT.editorAsset where the Type does not match the Editor Asset type, Such as a subAsset type.
- Fixed bug where Use Asset Database and Use Existing Build could return a different number of results in LoadAssetAsync<IList<>>
- Fixed bug where SceneUnload Operations weren't getting properly released in certain circumstances.
- Fixed UI performance regression when opening the Addressables Group Editor window.
- Fixed issue where RuntimeKeyIsValid was give a false negative when checking sub-objects.
- Updating scripting defines to check if caching is enabled.
- Changed the display of AssetReference types in the inspector from a dropdown to look like an object reference.
- Prevent assets from being added to read only Addressable groups through the group editor window.
- Group names can now be searched through the group editor window.
- Added ability to set variables in AddressablesRuntimeProperties more than once.
- Fixed missed null check on drag and drop in Group Editor window.
- Updated Scriptable Build Pipeline dependency to bring in these changes:
  - Updated CompatibilityAssetBundleManifest so hash version is properly serializable.
  - Renamed "Build Cache" options in the Preferences menu to "Scriptable Build Pipeline"
  - Improved performance of the Scriptable Build Pipeline's archiving task.

## [1.6.2] - 2020-02-08
- Checking if Profile Events is enabled on the engine side before running the DiagnosticEventCollector Update.
- Fixed issue where RuntimeKeyIsValid was give a false negative when checking sub-objects.
- Fixed Update Previous Build workflow that wasn't re-using previously built Asset Bundle paths when necessary.
- Updated Scriptable Build Pipeline dependency to bring in these changes:
  - Fixed an issue where texture sources for sprites were not being stripped from the build.
  - Fixed an issue where scene changes weren't getting picked up in a content re-build.
  - Fixed an issue where texture sources for non-packed sprites were being stripped incorrectly.
- Fixed issue where hosting service ports were changing on assets re-import.
- Fixed issues with Content Update, including groups that are Packed Separately not updating properly.

## [1.6.0] - 2020-01-11
- Fixed bug where unsubscribing to AsyncOperations events could throw if no subscription happened beforehand.
- Fixed NullReferenceException when displaying Groups window displaying entries with Missing Script References on SubAssets.
- Moved AnalyzeWindow.RegisterNewRule to AnalyzeSystem.RegisterNewRule so that the API/logic wouldn't live in GUI code.
- Fixed bug where scenes in a folder containing "Assets" in the folder name not loadable in "Use Asset Database" mode.
- InvalidKeyException's message now include the type of the key that caused it, if applicable.
- Added the ability to select and edit multiple Addressable Groups at the same time.
- Assigning LocationCount during AddressableAssetBuildResult.CreateResult<T>
- Fixed issue where groups and schemas were getting deleted on import.
- Adding dependencies to built in modules to prevent them from being disabled if Addressables is active.
- Adding scripting define to remove Caching API calls when ENABLE_CACHING is false
- Added API to get the scene AsyncOperation from ActivateAsync().  Made the previous API, Activate(), obsolete.
- Fixed bug where the group window wasn't properly refreshed on Analyse fix

## [1.5.1] - 2020-01-13
- Fixed issue where groups and schemas were getting deleted on import.
- Adding scripting define to remove Caching API calls when ENABLE_CACHING is false

## [1.5.0] - 2019-12-09
- Fixed temporary StreamingAssets files not being removed on a failed player build.
- Added Bundle Naming option for naming as a hash of the full filename string.
- Added a delay before unloaded things are removed from Event Viewer graph.  Ideally this would track with dependencies, but for now it's simply time based.
- Fixed ProfileValueReferences not getting set dirty when changed.
- Added ability for Addressables to handle null references in the Addressables groups list.
  - Null groups should not affect or influence content builds, updates, or Analyze rules.
  - Right clicking on a [Missing Reference] will give you the option to remove all missing references.
- Fixed issue with Analyze reporting multiple duplicate data for one group.
- Fixed issue where unloading a scene was throwing an invalid handle error.
- Added Addressables.ClearDependencyCacheAsync API to clear cached dependent AssetBundles for a given key or list of keys.
- Added type conversion from AnimatorController to RuntimeAnimatorController.

## [1.4.0] - 2019-11-13
- Added the ability to disable checking for content catalog updates during initialization.
- Fixed issue where turning off Include in Build in the right circumstances would throw an exception.
- Made internal classes and members public to support custom build scripts.
- Exposed Addressables.InstanceProvider to allow for setting up runtime specific data on custom instance providers.
- Fixed issue with filenames being too long to write to our Temp cache of AssetBundles.
- Changed ProcessGroup in BuildScriptFastMode to directly create catalog entries from Addressable entries.
- Added progress bar to Fast Mode when creating content catalog.

## [1.3.8] - 2019-11-04
 - Properly suppressing a harmless "Unknown error in AsyncOperation" that has been popping up during init. It had to do with not finding a cached catalog before a catalog had been cached (so error shouldn't happen).
 - Fixed issue with asset hash calcluation for internal asset bundle name when building bundles.
 - Adding option "Unique Bundle IDs" to the General section of the AddressableAssetSettings Inspector.
   - If set, every content build (original or update) will result in asset bundles with more complex internal names.  This may result in more bundles being rebuilt, but safer mid-run updates.  See docs for more info.
   - This complex internal naming was added to 1.3.3 to support safter Content Catalog updating, but was forced on.  It is now optional as there are some drawbacks.

## [1.3.5] - 2019-11-01
 - Added documentation about updating Content Catalog at runtime (outside Init).  Uses CheckForCatalogUpdates() and UpdateCatalogs().

## [1.3.3] - 2019-10-21
 - UI and naming changes
   - "Static true or false" content is now content with an "Update Restriction" of "Cannot Change Post Release" or "Can Change Post Release"
   - "Fast Mode" (play mode) has been renamed "Use Asset Database (faster)"
   - "Virtual Mode" (play mode) has been renamed "Simulate Groups (advanced)"
   - "Packed Mode" (play mode) has been renamed "Use Existing Build (requires built groups)"
   - There is no longer a current "Build Script" (Build Script menu in Addressables window).  Instead the script is selected when triggering the build.
   - Schemas have been given display names to be more clear of their intent BundledAssetGroupSchema.
     - BundledAssetGroupSchema displays as "Content Packing & Loading"
       - ContentUpdateGroupSchema displays as "Content Update Restriction"
     - Bundle and Asset providers within schema settings are named more descriptively
   - Profile management is in its own window ("Profiles")
   - Label management is in its own window
   - "Prepare for Content Update" is now under the "Tools" menu (in Addressables window), and is called "Check for Content Update Restriction"
   - "Build for Content Update" is "Update a Previous Build" (still in "Build" menu of Addressables window).
   - "Profiler" window has been renamed "Event Viewer".  It's more accurate, and avoids confusion with "Profilers" window.
 - Added additional parameter to AssetReference.LoadSceneAsync method to match Addressables.LoadSceneAsync API
 - Added AssetReference.UnloadScene API
 - Fixed issue with WebGL builds where re-loading the page was causing an exception to get thrown.
 - Fixed Analyze bug where bundle referenced multiple times was flagged as duplicate.
 - Fixed issue with hashing dependencies that led to frequent "INCORRECT HASH: the same hash (hashCode) for different dependency lists:" errors.
 - Update AddressableAssetEntry cached path to new modified asset entry paths.
 - Storing the KeyData string from ContentCatalogData on disk instead of keeping it in memory as it can get quite large.
 - Fixed Custom Hosting Service window so it won't close when focus is lost.
 - Fixed issue with AudioMixerGroups not getting the proper runtime type conversion for the build.
 - Fixed invalid location load path when using "only hash" bundle naming option in 'content packing and loading' schema.
 - Removed content update hash from final AssetBundle filename.
 - Removed exception in Analyze that was triggering when "Fix Selected Rules" was bundling in Un-fixable rules.
 - (SBP) Fixed an edge case where Optimize Mesh would not apply to all meshes in the build.
 - (SBP) Fixed an edge case where Global Usage was not being updated with latest values from Graphics Settings.
 - (SBP) Fixed Scene Bundles not rebuilding when included prefab changes.
 - Added APIs to update content catalog at runtime: CheckForCatalogUpdates() and UpdateCatalogs().

## [1.2.4] - 2019-09-13
 - Further improvement to the % complete calculations.
   - Note that this is an average of dependency operations. Meaning a LoadAssetsAsync call will average the download, and the loading progress.  DownloadDependenciesAsync currently has one extra op, so the download will crawl to 50%, then jump to done (we will look into removing that). Similarly any op that is called before Addressables init's will jump to 50% once init is done.

## [1.2.3] - 2019-09-10
 - Actually "Made ContentUpdateScript.GatherModifiedEntries public."

## [1.2.2] - 2019-09-09
 - Made ContentUpdateScript.GatherModifiedEntries public.
 - Added sub-object support to AssetReference.  For example, you can now have an AssetReference to a specific sprite within a sprite atlas.
 - Added sub-object support to addresses via [] notation.  For example, sprite atlas "myAtlas", would support loading that atlas via that address, or a sprite via "myAtlas[mySprite]"
 - Fixed issue with Content Update workflow.  Assets that don't change groups during Content Update now remain in the same bundle.
 - Added funtionality to allow multiple diagnostic callbacks to the ResourceManager.
   - Added error and IResourceLocation to the callback.
 - Added default parameter to DownloadDependenciesAsync to allow auto releasing of the the operation handle on completion.
 - Added memory management documentation.
 - Changed OnlyHash naming option to remove folder structure.  This is a workaround to Windows long-file-path issues.
 - Made AssetReference interfaces virtual
 - Fixed hash calculations to avoid collisions
 - Added overload for GetDownloadSizeAsync.  The overload accepts a list of keys and calculates their total download size.
 - Improved percent complete calculations for async opertions.

## [1.1.10] - 2019-08-28
 - Fix for all files showing "Missing File" in the addressables window.
 - Fix for waiting on a successfully done Task

## [1.1.9] - 2019-08-22
 - Fixed drag and drop NullRef in main addressables window.
 - Fixed AudioMixer type assets getting stripped from bundles.
 - Fixed issue where failed async operations weren't getting released from the async operation cache.
 - Fix unloading of scenes so that the dependencies will wait for the unload operation to complete before unloading.  This was causing an occasional 1-frame visual glitch during unload.
 - Fixed scenario where AsyncOperation Task fails to complete when AsyncOperation has already completed.
 - Fixed a missed init-log that was stuck always-logging.
 - Fixed issue around assets losing dependencies when unloaded then reloaded.  This would manifest most often as sprites losing their texture or prefabs losing their shader/material/texture.
 - Changed checks for determining if a path is remote or not from looking for "://" to looking for starting with "http".  "://" is still used to determine if the asset should be loaded via UnityWebRequest or not.
 - Added Analyze Rule to show entire Asset Bundle layout
 - Added progress bars and some optimizations for calculating analyze rules

## [1.1.7] - 2019-07-30
 - Fixed chain operation percent complete calculation.
 - Fixed scenario where deleting assets would also delete groups that have similar names.
 - Fix in bundle caching bug surrounding bundles with '.' in their name
 - Significant improvements to the manual pages
 - Made the many init-logs not log unless ADDRESSABLES_LOG_ALL is defined in player settings (other logs always worked this way, init didn't).
 - Prevented NullReferenceException when attempting to delete entries in the Addressables window.
 - Fix for building by label (Bundle Mode = Pack Together By Label)
 - Removed ability to mark editor-only assets as addressable in GUI
 - Better fix to Editor types being added to the build
 - Made BuiltIn Data group read-only by default.
 - Fixed NullRef caused by an invalid (BuildIn Data) group being default during a build.
 - Fixed path where LoadResourceLocationsAsync could still throw an exception with unknown key.  Now it should not, and is a safe way to check for valid keys.
   - If Key does not exist but nothing else goes wrong, it will return an empty list and Success state.
 - Fixed NullRef caused when there was a deleted scene in the scenes list.
 - BuildCompression for Addressables can now be driven from the default group.  If necessary WebGL builds will fallback to LZ4Runtime and all other build targets will fallback to LZMA.
 - Added options for bundle naming: AppendHash, NoHash, OnlyHash.
   - As a temporary workaround for updating issues, we recommend setting all groups with StaticContent=true to be NoHash.  This will make sure the updated catalog still refers to the correct unchanged bundle.  An actual fix will be out in a few releases.

## [1.1.5] - 2019-07-15
 - Fixed scenario where scene unload simultaneously destroys objects being instantiated in different scenes.
 - Cleaned up SetDirty logic to remove excessive dirtying of assets.

## [1.1.4-preview] - 2019-06-19
 - Fixed an issue where Editor only types were being added to the build.

## [1.1.3-preview] - 2019-06-17
 - *BREAKING CODE CHANGES*
   - ReleaseInstance will now return a bool saying if it successfully destroyed the instance.  If an instance is passed in that Addressables is unaware of, this will return false (as of 0.8 and earlier, it would print a log, and still destroy the instance).  It will no longer destroy unknown instances.
 - Added PrimaryKey to the IResourceLocation.  By default, the PrimaryKey will be the address.  This allows you to use LoadResourceLocationsAsync and then map the results back to an address.
 - Added ResourceType to IResourceLocation.
   - This allows you to know the type of a location before loading it.
   - Fixes a problem where calling Load*<Type>(key) would load all items that matched the key, then filter based on type.  Now it will do the filter before loading (after looking up location matches)
   - This also adds a Type input to LoadResourceLocationsAsync.  null input will match all types.
 - Safety check AssetReference.Asset to return null if nothing loaded.
 - New rule added to Analyze window - CheckResourcesDupeDependencies - to check for dependencies between addressables and items in Resources
 - Added group rearranging support to the Addressables window.
 - Improved logging when doing a Prepare for Content Update.
 - Added versions of DownloadDependencies to take a list of IResourceLocations or a list of keys with a MergeMode.
 - Fixed scenario where Task completion wouldn't happen if operation was already in a certain state
 - Made LoadResourceLocations no longer throw an exception if given unknown keys.  This method is the best way to check if an address exists.
 - Exposed AnalyzeRule class to support creating custom rules for Addressables analysis.
 - Fixed some issues surrounding loading scenes in build scenes list via Addressables
 - Removed using alias directives defined in global space.
 - Proper disposal of DiagnosticEventCollector and DelayedActionManager when application closes.
 - Added support for loading named sub-objects via an "address.name" pattern.  So a sprite named "trees" with sub-sprites, could be loaded via LoadAssetAsync<Sprite>("trees.trees_0").
 - Known issue: loading IList<Sprite> from a Texture2D or IList<AnimationClip> from an fbx will crash the player.  The workaround for now is to load items by name as mentioned just above.  Engine fix for this is on its way in.

## [0.8.6-preview] - 2019-05-14
 - Fix to make UnloadSceneAsync(SceneInstance) actually unload the scene.

## [0.8.3-preview] - 2019-05-08
 - *BREAKING CODE CHANGES*
   - Chagned all asynchronous methods to include the word Async in method name.  This fits better with Unity's history and convention.  They should auto upgrade without actually breaking your game.
   - Moved AsyncOperationHandle inside namespace UnityEngine.ResourceManagement
 - Addressable Analyze changes:
   - Analyze has been moved into it's own window.
   - CheckSceneDupeDependencies Analyze rule has been added.
   - CheckDupeDependencies has been renamed into CheckBundleDupeDependencies.
   - Analyze Rule operations for individuals or specific sets of Analyze Rules has been added via AnalyzeRule selections.

## [0.7.4-preview] - 2019-04-19
 - Removed support for .NET 3.x as it is deprecated for Unity in general.
 - Replaced IAsyncOperation with AsyncOperationHandle.
   - Once the asset is no longer needed, the user can call Addressables.Release, passing in either the handle, or the result the handle provided.
 - Exposed AsyncOperationBase for creating custom operations
   - These operations must be started by ResourceManager.StartOperation
 - Replaced IDataBuilderContext and it's inherited classes with simpler AddressablesDataBuilderInput.  This class is fed into all IDataBuilder.BuildData calls.
 - Fixed Nintendo Switch and PlayStation4 support.
 - Simplified the IResourceProvider interface.
 - Refactored build script interface.  Made BuildScriptBase and the provided concrete versions easier to inherit from.
 - Removed DelayedActionManager.
 - Removed ISceneProvider. Users can implement custom scene loading using a custom AsyncOperationBase.
 - Removed optional LRU caching of Assets and Bundles.
 - Addressables Profiler now tracks all active async operations
 - AssetBundles targetting StreamingAssets (by using the profile variable [UnityEngine.AddressableAssets.Addressables.BuildPath] now build to the Library instead of StreamingAssets.  During the player build, these files are copied into StreamingAssets, then after the build, the copies are deleted. They are also built into platform specific folders (so building for a second platform will not overwrite data from a first build).  We recommend deleting anything in Assets/StreamingAssets/aa.
 - The addressables_content_state.bin is built into a platform specific folder within Assets/AddressableAssetsData/.  We recommend deleting the addressables_content_state.bin in Assets/AddressableAssetsData to avoid future confusion.
 - ScriptableBuildPipeline now purges stale data from its cache in the background after each build.
 - Disabled Addressables automatic initialization.  It will now initialize itself upon the first call into it (such as Load or Instantiate).  To Initialize on startup instead of first use, call Addressables.Initialize().
 - Optimized performance around instantiation and general garbage generation.
 - Added per-group bundle compression settings.
 - Fixes to AssetReference drawers.
 - Improved the group template system for creating better defined asset groups.
 - Fixed bug in bundle caching that caused GetDownloadSize to report incorrectly
 - Cleaned up Load/Release calls to make sure all releases could take either the handle returned by Load, or the handle.Result.
 - Added editor-only analytics (nothing added in runtime).  If you have Analytics disabled in your project nothing will be reported. Currently only run when you build addressables, it includes data such as Addressables version and Build Script name.
 - Fixed null ref issue when cleaning all the data builders
 - KNOWN ISSUE: there is still an occasional issue with code stripping on iOS.  If you run into iOS issues, try turning stripping off for now.

## [0.6.8-preview] - 2019-03-25
- fixed Build For Content Update to no longer delete everything it built.

## [0.6.7-preview] - 2019-03-07
 - Fix for iOS and Android. Symptom was NullReferenceException dring startup resulting in nothing working.  Fix requires re-running Build Player Content

## [0.6.6-preview] - 2019-03-05
 - *BREAKING CODE CHANGES*
   - to ease code navigation, we have added several layers of namespace to the code.
   - All Instantiate API calls (Addressables and AssetReference) have been changed to only work with GameObjects.
   - any hardcoded profile path to com.unity.addressables (specifically LocalLoadPath, RemoteLoadPath, etc) should use UnityEngine.AddressableAssets.Addressables.RuntimePath instead.
       For build paths, replace Assets/StreamingAssets/com.unity.addressables/[BuildTarget] with [UnityEngine.AddressableAssets.Addressables.BuildPath]/[BuildTarget]
       For load paths,  replace Assets/StreamingAssets/com.unity.addressables/[BuildTarget] with {UnityEngine.AddressableAssets.Addressables.RuntimePath}/[BuildTarget]
   - We have removed attribute AssetReferenceTypeRestriction as it is cleaner to enforce type via generics
   - Attribute AssetReferenceLabelRestriction is renamed to AssetReferenceUILabelRestriction and must be surrounded by #if UNITY_EDITOR in your game code, to enforce it's editor-only capability
   - Modifications to IResourceProvider API.
   - Removed PreloadDependencies API.  Instead use DownloadDependencies
 - Content Update calculation has changed, this will invalide previously generated addressables_content_state.bin files.
   - Some types for content update were made private as a result of the above change.
 - Minimum Unity version is now 2018.3 to address a build-time bug with progressive lightmapper.
 - Moved all of the Resource Manager package to be contained within Addressables (no longer a stand alone package).  No code change implications.
 - Change to content catalog building:
   - Previous model built one catalog per group, wherever that group built it's data.
   - New model builds one catalog locally, and optionally one "remote".  Remote location is set on the top level AddressableAssetSettings object.
   - Loading will now always check if remote has changes (if remote exists), and use local otherwise (or cached version of remote).
 - LoadScene API now takes the LoadSceneParameters that were added to the engine in 2018.2
 - Exposed AddressablesBuildDataBuilderContext.BuildScriptContextConstants for use in build scripts.
 - Refactored AddressablesBuildDataBuilderContext.GetValue to take default parameter.
 - Fixed Scene asset path to be consistent between different play modes in the catalog data.
 - Exposed the various IDataBuilder implementations as public classes.
 - Exposed asset and bundle provider types for BundledAssetGroupSchema.
 - Fixed several bugs when loading catalogs from other projects.
 - Added provider suffix to Initialization operation and Addressables.LoadCatalogsFromRuntimeData API to better support overriding providers.
 - Exposed CachedProvider options in BundledAssetGroupSchema.  Each unique set of parameters will generate a separate provider.  There is also an option to force a group to have its own providers.
 - Added IEnumerable<object> Keys property to IResourceLocator interface.
 - Exposed InitializationOperation as public API.
 - Added BuildTarget to ResourceManagerRuntimeData.  This is used to check if the generated player content was built with the same build target as the player or the editor when entering play mode.
 - Removed warnings generated from not finding the cached catalog hash files, which is not an error.
 - Fixed bug where scenes were not unloading.
 - Fixed GUI exception thrown in group inspector.
 - Fixed error case where an asset (usually a bundle) was loaded multiple times as different types (object and AssetBundle).
 - Fixed divide by zero bug when computing load percent of simulated asset bundles.
 - AddressableAssetBuildResult.CreateResult now takes the settingsPath as a parameter to pass this to the result.
 - Fix AssetReference GUI when the AssetReference is inside an array of classes, part of a SerializedObject, or private.
 - Fix AssetReferenceSprite to properly support sprites (as opposed to Texture2D's).
 - Fixed bug involving scenes being repeatedly added to the build scenes list.
 - Removed deprecated and obsolete code.  If you are upgrading from a very old version of Addressables, please update to 0.5.3-preview first.
 - Removed the default MergeMode on LoadAssets calls to enforce explicit behavior.
 - Added IAsyncOperation<long> GetDownloadSize(object key) API to compute remaining data needed to load an asset
 - Fixed assets being stuck in a read-only state in UI
 - Unified asset moving API to clean up public interface
 - Added PlayerVersion override to AddressableAssetSettings
 - Ensure UI cannot show invalide assets (such as .cs files)
 - Renamed Addressables.LoadAddtionalCatalogs to Addressables.LoadContentCatalog and now it takes the path of the catalog instead of the settings file
 - Moved provider information from ResourceManagerRuntimeDate into ContentCatalogData
 - Updating ResourceManager to be a non-static class
 - Fixed bugs surrounding assets moving in or out of Resources (outside Addressables UI)
 - Fixed the AssetReference dropdown to properly filter valid assets (no Resources and honoring type or label limitations).
 - Fixed AssetReferences to handle assets inside folders marked as Addressable.
 - Added attribute AssetReferenceUIRestriction to support user-created AssetReference restrictions (they are only enforced in UI, for dropdown and drag&drop)
 - Changed addressables_content_state.bin to only build to the folder containing the AddressableAssetSettings object (Assets/AddressableAssetsData/ in most cases)
 - Fixed issue where the wrong scene would sometimes be open post-build.

## [0.5.3-preview] - 2018-12-19
 - fixed upgrade bug from 0.4.x or lower to 0.5.x or higher. During upgrade, the "Packed Mode" option was removed from play mode.  Now it's back and upgrades are safe from 0.4.x or from 0.5.x to 0.5.3

## [0.5.2-preview] - 2018-12-14
 - *IMPORTANT CHANGE TO BUILDING*
   - We have disabled automatic asset bundle building.  That used to happen when you built the player, or entered play mode in "packed mode".  This is no longer the case.  You must now select "Build->Build Player Content" from the Addressables window, or call AddressableAssetSettings.BuildPlayerContent().  We did this because we determined that automatic building did not scale well at all for large projects.
 - fixed regression loading local bundles
 - Added Addressables.DownloadDependencies() interface
 - fixes for Nintendo Switch support
 - Fixed issues around referencing Addressables during an Awake() call
 - Code refactor and naming convention fixes
 - Cleaned up missing docs
 - Content update now handles not having and groups marked as Static Content
 - Fixed errors when browing for the addressables_content_state.bin and cancelling
 - Moved addressables_content_state.bin to be generated into the addressables settings folder
 - Changed some exceptions when releasing null bundles to warnings to handle the case of releasing a failed download operation
 - Separated hash and crc options to allow them to be used independently in asset bundle loads.
 - Use CRC in AssetBundle.LoadFromFileAsync calls if specified
 - Always include AssetBundleRequestOptions for asset bundle locations

## [0.4.8-preview] - 2018-10-22
 - Added all referenced types in asset bundles to link.xml to prevent them from being stripped in IL2CPP builds

## [0.4.7-preview] - 2018-10-20
 - updated Scriptable Build Pipeline version in dependencies

## [0.4.6-preview] - 2018-10-16
 - MINIMUM RECOMMENDED VERSION - 2018.2.11+
   - We have re-enabled the addressables checkbox. Versions of 2018.2 older than the .11 release will work unless you attempt to view the Animation Import Settings inspector.  If you do have animations you need to inspect, use .11+. If you do not, use any official release version of 2018.2.
 - refactored the way IResourceProviders are initialized in the player - serialized data is constructed at runtime to control how the providers are configured
 - added readonly custom inspector for AddressableAssetEntryCollection
 - AssetReference now stores the loaded asset which can be accessed via the Asset property after LoadAsset completes.  ReleaseAsset has been modified to not need the asset passed in (the old version is marked obsolete]
 - fixed profiler details view not updating when a mouse drag is completed
 - fixed null-ref when moving Resources to Addressables when there are no Resources
 - blocked moving EditorSceneList within GUI
 - fixed cap on address name length
 - fixed workflows of marking Resources as addressable and moving an addressable into Resources.
 - fixed issue where AssetReferenceDrawer did not mark scene as dirty when changed.
 - added Hosting Services feature; provides extensible framework and implementation for serving packed content to player builds from the Editor
 - replaced addressables buildscript with an interface based system.  IDataBuilder class is now used to define builders of specific types of data.  The Addressables settings object
   contains a collection of data builders and uses these to create player and play mode data.  Users can implemented custom data builders to control the build process.
 - replaced AssetGroupProcessors with a collection of AssetGroupSchema objects.  The difference is that the schema objects only contain data and groups can have multiple schemas.  The
   logic for processing groups now resides in the build script and uses the schemas as data sources and filters for how to build.
 - Added Initialization objects that can be created during the build to run during addressables initialization
 - Implemented Caching API initialization with initialization objects
 - Changed some API and tests to work with 2019.x
 - fixed how AssetReference's draw when within lists, arrays, or contained classes
 - Fixed the workflow of scenes moving in and out of the Editor Build Settings Scene list.
 - Removed "Preview" and added "Analyze".
   - The new system runs any rules it knows about.
   - Currently this is one rule that is manually set up in code. Future work will have additional rules, and expose the ability to create/add user- or project-specific rules
   - This process can be slow, as it runs most of a build to get accurate data.
   - Within the Analyze window there is a "fix" button that allows each rule to fix any issues if the rule knows how.
   - The current rule is a "check duplicate asset" rule. This looks for assets that are pulled into multiple asset bundles due to dependency calculations. The way it fixes things is to move all of those into a newly created group.
 - Added option to toggle logging of all exceptions within the Resource Manager
 - Refactored initialization of the addressable asset settings to prevent it getting into a bad state.

## [0.3.5-preview] - 2018-09-05
 - implemented content update workflow.  Added a dropdown to the "Build" button on main window's toolbar.
    - "Build/Prepare for Content Update" will detect assets in locked bundles (bundles flagged as static, by default all local bundles).
    - "Build/Build for Content Update" will build assets with a catalog that is compatible with a previously released player.
    - "Build/Build Packed Data" will build in the same way entering play mode in PackedMode would.
    - implemented Clean Build. "Build/Clean/*" will clear out build caches.
 - cleaned up streaming assets folder better after build
 - moved asset group data into separate assets in order to better support version control
 - fixed bug when canceling export of entries to an AssetEntryCollection
 - fixed several bugs related to caching packed bundles in play mode
 - added option to build settings to control whether streaming assets is cleared after each build
 - enabled CreateBuiltInShadersBundle task in build and preview
 - fixed bug in AA initialization that was cuasing tests to fail when AA is not being used.
 - fixed bug where toggling "send profiler events" would have no effect in some situations
 - default the first 2 converted groups to have StaticContent set to true
 - UI Redesign
  - Moved most data settings onto actual assets.  AddressableAssetSettings and AddressableAssetGroup assets.
    - AddressableAssetSettings asset has "Send Profile Events", list of groups, labels, and profiles
    - AddressableAssetGroup assets have all data associated with that group (such as BuildPath)
  - Made "preview" be a sub-section within the Addressables window.
  - The "Default" group can now be set with a right-click in the Addressables window.
  - Set play mode from "Mode" dropdown on main window's toolbar.
  - Moved "Hierarchical Search" option onto magnifying glass of search bar.  Removed now empty settings cog button.
 - fixed issue when packing groups into seperate bundles generated duplicate asset bundle names, leading to an error being thrown during build
 - added support for disabling the automatic initialization of the addressables system at runtime via a script define:  ADDRESSABLES_DISABLE_AUTO_INITIALIZATION
 - added API to create AssetReference from AddressableAssetSettings object in order to create an entry if it does not exist.
 - moving resource profiler from the ResourceManager package to the Addressables package
 - fixed bug where UnloadScene operation never entered Done state or called callback.
 - fixed loading of additonal catalogs. The API has changed to Addressables.LoadCatalogsFromRuntimeData.
 - fixed bug in InitializationOperation where content catalogs were not found.
 - changed content update workflow to browse for cachedata.bin file instead of folder
 - fixed exception thrown when creating a group and using .NET 4.x
 - fixed bugs surrounding a project without addressables data.
  - AssetLabelReference inspector rendering
  - AssetReference drag and drop
 - fixed profiler details view not updating when a mouse drag is completed
 - fixes surrounding the stability of interacting with the "default" group.
 - Added docs for the Content Update flow.
 - Adjusted UI slightly so single-clicking groups shows their inspector.
 - removed not-helpful "Build/Build Packed Data" item from menu.
 - fixed bug where you could no longer create groups, and group assets were not named correctly

## [0.2.2-preview] - 2018-08-08
 - disabled asset inspector gui for addressables checkbox due to editor bug

## [0.2.1-preview] - 2018-07-26
 - smoothed transition from 0.1.x data to 0.2.x data
 - added checks for adding duplicate scenes into the EditorBuildSettings.scenes list
 - fixed exception when deleting group via delete key, added confirmation to all deletions

## [0.2.0-preview] - 2018-07-23
 - Fixed bundles being built with default compression instead of compression from settings
 - Fixed bug in tracking loaded assets resulting in not being able to release them properly
 - Added Key property to IAsyncOperation to allow for retrieval of key that requested the operation
 - Added AssetLabelReference to provide inspector UI for selecting the string name of a label
 - Fixed dragging from Resources to a group.
 - Added ability to re-initialize Addressables with multiple runtime data paths.  This is to support split projects.
 - Clean up StreamingAssets folder after build/play mode

## [0.1.2-preview] - 2018-06-11
 - fixed Application.streamingAssetsPath being stripped in IL2CPP platforms

## [0.1.1-preview] - 2018-06-07
 - MIN VERSION NOW 2018.2.0b6
 - updated dependency

## [0.1.0-preview] - 2018-06-05
 - MIN VERSION NOW 2018.2.0b6
 - added better checks for detecting modified assets in order to invalidate cache
 - fixed preview window showing scenes in wrong bundle
 - exclude current processor type from conversion context menu
 - fixed exception when right clicking asset groups
 - added support for adding extra data to resource locations
 - made Addressables.ReleaseInstance destroy even non-addressable assets.
 - append hash to all bundle names
 - pass crc & hash to bundle provider
 - clear catalog cache whenever packed mode content is rebuilt

## [0.0.27-preview] - 2018-05-31
 - fixed ResourceManager initialization to work as the stand-alone player

## [0.0.26-preview] - 2018-05-24
 - re-added Instantiate(AssetReference) for the sake of backwards compatability.

## [0.0.25-preview] - 2018-05-23
 - workaround for engine bug surrounding shader build.  Fix to engine is on it's way in.

## [0.0.24-preview] - 2018-05-21
 - minor bug fix

## [0.0.23-preview] - 2018-05-21
 - new format for content catalogs
 - detects changes in project and invalidates cached runtime data and catalogs
 - data is not copied into StreamingAssets folder when running fast or virtual mode
 - added external AssetEntry collections for use by packages
 - modifying large number of asset entries on the UI is no longer unresponsive
 - added an option to search the asset list in a hierarchical fashion. Helps track down which group an asset is in.
 - many small bug fixes.

## [0.0.22-preview] - 2018-05-03
 - dependency update.

## [0.0.21-preview] - 2018-05-03
 - fixed build-time object deletion bug.

## [0.0.20-preview] - 2018-05-02
 - Added support for extracting Built-In Shaders to a common bundle
 - Added build task for generating extra data for sprite loading edge case
 - fix build related bugs introduced in 0.0.19.

## [0.0.19-preview] - 2018-05-01
 - Complete UI rework.
    - Moved all functionality to one tab
    - Preview is a toggle to view in-line.
    - Profiles are edied from second window (this part is somewhat placeholder pending a better setup)
 - Dependency updates
 - Minor cleanup to build scripts

## [0.0.18-preview] - 2018-04-13
 - minor bug fixes
 - exposed memory cache parameters to build settings, changed defaults to use LRU and timed releases to make preloading dependencies more effective

## [0.0.17-preview] - 2018-04-13
 - added tests
 - fixed bugs
 - major API rewrite
    - all API that deals with addresses or keys have been moved to Addressables
    - LoadDependencies APIs moved to Addressables
    - Async suffix removed from all Load APIs

## [0.0.16-preview] - 2018-04-04
- added BuildResult and callback for BuildScript
- added validation of instance to scene and scene to instance maps to help debug instances that change scenes and have not been updated
- added ResourceManager.RecordInstanceSceneChange() method to allow RM to track when an instance is moved to another scene
- moved variable expansion of location data to startup

## [0.0.15-preview] - 2018-03-28
- fixed scene unloading
- release all instances when a scene unloads that contains unreleased instances
- fixed overflow error in virtual mode load speeds

## [0.0.14-preview] - 2018-03-20
- Updated dependencies


## [0.0.12-preview] - 2018-03-20
- Minor UI updates
- doc updates
- fixed bug involving caching of "all assets"
- improved error checking & logging
- minor bug fixes.

## [0.0.8-preview] - 2018-02-08
- Initial submission for package distribution


