# Changelog
All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

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

