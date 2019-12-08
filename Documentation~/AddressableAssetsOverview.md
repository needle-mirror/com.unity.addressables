# Addressable Assets overview
The Addressable Assets System consists of two packages:

* Addressable Assets package (primary package)
* Scriptable Build Pipeline package (dependency)

When you install the Addressable Assets package, the Scriptable Build Pipeline package installs at the same time. See the latest version of the [Unity Scriptable Build Pipeline](https://docs.unity3d.com/Packages/com.unity.scriptablebuildpipeline@latest) documentation.

## Concepts
The following concepts are referenced throughout this documentation:

* **Address**: An asset's location identifier for easy runtime retrieval.
* **`AddressableAssetData` directory**: Stores your Addressable Asset metadata in your Project’s _Assets_ directory.
* **Asset group**: A set of Addressable Assets available for build-time processing.
* **Asset group schema**: Defines a set of data that you can assign to a group and use during the build.
* **`AssetReference`**: An object that operates like a direct reference, but with deferred initialization. The `AssetReference` object stores the GUID as an Addressable that you can load on demand.
* **Asynchronous loading**: Allows the location of the asset and its dependencies to change throughout the course of your development without changing the game code. Asynchronous loading is foundational to the Addressable Asset System.
* **Build script**: Runs asset group processors to package assets, and provides the mapping between addresses and _Resource_ locations for the Resource Manager.
* **Label**: Provides an additional Addressable Asset identifier for runtime loading of similar items (for example, `Addressables.DownloadDependenciesAsync("spaceHazards");`).
