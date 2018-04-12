# Changelog
All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

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

