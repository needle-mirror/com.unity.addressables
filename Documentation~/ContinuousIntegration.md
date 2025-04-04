---
uid: addressables-ci
---

# Use continuous integration to build Addressables

You can use a continuous integration (CI) system to perform Addressables content builds and your application player builds. This page provides general guidelines for building Addressables with CI systems.

## Select a content builder

One of the main choices when building Addressables content is selecting a content builder. By default, if you call [`AddressableAssetSettings.BuildPlayerContent`](xref:UnityEditor.AddressableAssets.Settings.AddressableAssetSettings.BuildPlayerContent) it uses the `BuildScriptPackedMode` script as the [`IDataBuilder`](xref:UnityEditor.AddressableAssets.Build.IDataBuilder) instance. The `BuildPlayerContent` method checks the [`ActivePlayerDataBuilder`](xref:UnityEditor.AddressableAssets.Settings.AddressableAssetSettings.ActivePlayerDataBuilder) setting and calls into that script's `BuildDataImplementation`

If you've implemented your own custom `IDataBuilder` and want to use it for your CI builds, set the [`ActivePlayerDataBuilderIndex`](xref:UnityEditor.AddressableAssets.Settings.AddressableAssetSettings.ActivePlayerDataBuilderIndex) property of [`AddressableAssetSettings`](xref:UnityEditor.AddressableAssets.Settings.AddressableAssetSettings). By default, you can access the correct settings instance through [`AddressableAssetSettingsDefaultObject.Settings`](xref:UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings). This index refers to the position of the `IDataBuilder` in the list of [`AddressableAssetSettings.DataBuilders`](xref:UnityEditor.AddressableAssets.Settings.AddressableAssetSettings.DataBuilders). The following code sample demonstrates how to set a custom `IDataBuilder`:

[!code-cs[sample](../Tests/Editor/DocExampleCode/CustomDataBuilder.cs#doc_SetCustomBuilder)]

## Clean the Addressables content builder cache

`IDataBuilder` implementations define a [`ClearCachedData`](xref:UnityEditor.AddressableAssets.Build.IDataBuilder.ClearCachedData) method, which cleans up any files created by that data builder. For example, the default `BuildScriptPackedMode` script deletes the following:

- The content catalog
- The serialized settings file
- The built AssetBundles
- Any link.xml files created

You can call `IDataBuilder.ClearCachedData` as part of your CI process to make sure the build doesn't use files generated by earlier builds.

## Clean the scriptable build pipeline cache

Cleaning the Scriptable Build Pipeline (SBP) cache cleans the `BuildCache` folder from the `Library` directory along with all the hash maps generated by the build and the Type Database. The `Library/BuildCache` folder contains `.info` files created by SBP during the build which speeds up subsequent builds by reading data from these `.info` files instead of re-generating data that hasn't changed.

To clear the SBP cache in a script without opening a confirmation dialog, call [`BuildCache.PurgeCache(false)`](xref:UnityEditor.Build.Pipeline.Utilities.BuildCache.PurgeCache*).

When building Addressables content or player builds with command line arguments or through continuous integration, you should restart the Unity Editor for each target platform. This ensures that Unity can't invoke `-executeMethod` until after script compilation completes for a platform. For more information about using command line arguments, refer to the Unity User Manual documentation [Command line arguments](https://docs.unity3d.com/Manual/CommandLineArguments.html).
