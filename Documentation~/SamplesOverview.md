---
uid: samples-overview
---

# Addressables samples

The Addressables package contains samples that you can import into your project. To access the samples go to **Window** > **Package Management** > **Package Manager** > **Addressables**, and then select the **Samples** tab.

These samples include examples on how to disable asset importing during a build, creating custom build and Play mode scripts, and providing an `AddressableUtility` class.

When you download and import a sample, it's placed inside the `Assets/Samples/Addressables/{AddressablesVersionNumber}` path of your project.

|**Sample**|**Description**|
|---|---|
|**Addressables Utility**|Contains a set of utility functions for Addressables. The script contains a static method `GetAddressFromAssetReference` which provides the Addressable address used to reference a given `AssetRefence` internally.|
|**ComponentReference**|Creates an AssetReference that's restricted to having a specific component. For more information refer to the ComponentReference sample project located in the [Addressables Samples](https://github.com/Unity-Technologies/Addressables-Sample) repository.|
|**Custom Analyze Rules**|Create custom AnalyzeRules to use in the [Analyze window](analyze-addressables-window.md). Both rules follow the recommended pattern for adding themselves to the UI.|
|**Custom Build and Playmode Scripts**|Includes two [custom scripts](xref:addressables-api-build-player-content):<ul><li>A custom Play mode script located in `Editor/CustomPlayModeScript.cs` of the sample. This script works similarly to the Use Existing Build (requires built groups) Play mode script included. The methods added to accomplish this are `CreateCurrentSceneOnlyBuildSetup` and `RevertCurrentSceneSetup` on the `CustomBuildScript`.</li><li>A custom build script located in `Editor/CustomBuildScript.cs` of the sample. This custom build script creates a build that only includes the currently open scene. An initialization scene is automatically created and a script is added that loads the built scene on startup.</li></ul>For these examples, the build and load paths used by default are `[UnityEngine.AddressableAssets.Addressables.BuildPath]/[BuildTarget]` and `{UnityEngine.AddressableAssets.Addressables.RuntimePath}/[BuildTarget]` respectively.<br/><br/>The `ScriptableObject` of the class has already been created, but you can use the Create menu to make another `ScriptableObject`. For this `CustomPlayModeScript` the create menu path is **Addressables > Content Builders > Use CustomPlayMode Script**. By default, this creates a CustomPlayMode.asset ScriptableObject. The same goes for the `CustomBuildScript`.|
|**Disable Asset Import on Build**|Provides a script that disables asset importing during a player build. This improves build performance because AssetBundles are copied into StreamingAssets at build time. This sample is only relevant for Editor versions before 2021.2. In 2021.2+, the Editor provides the ability to include folders outside of `Assets` into `StreamingAssets`.<br/><br/>When the sample is imported into the project, a player build without asset importing can be triggered by the new menu item **Build/Disabled Importer Build**. The build output is placed into `DisabledImporterBuildPath/{EditorUserBuildSettings.activeBuildTarget}/` by default. The sample class `DisableAssetImportOnBuild` can be edited to alter the build path.|
|**Import Existing Group**|Contains a tool that imports group assets, for example from a custom package, to the current project.<br/><br/>The tool is located under `Window/Asset Management/Addressables/Import Groups`. The window requires a path to the `AddressableAssetGroup.asset` scriptable object, a name for the group, and a folder for any schemas related to the imported `AddressableAssetGroup`.|
|**Prefab Spawner**|Provides a basic script that instantiates and destroys a prefab `AssetReference`. To use the sample, attach the provided script, `PrefabSpawnerSample`, to a GameObject in your scene. Assign an Adressable asset to the `AssetReference` field of that script. If you're using the `Use Existing Build` Play mode script, ensure that your Addressable content is built. Then, enter Play mode.|


## Additional resources

* [Build Addressable assets from scripts](build-scripting-builds.md)
* [Referencing Addressable assets in code](AssetReferences.md)
* [Analyze Addressable layouts](analyze-addressable-layouts.md)