---
uid: addressables-api-build-player-content
---
# AddressableAssetSettings.BuildPlayerContent
#### API
- `static void BuildPlayerContent()`

#### Description
`AddressableAssetSettings.BuildPlayerContent()` is used to build relevant `Addressables` data into `AssetBundles` and the corresponding `ContentCatalog`.  It can be used in custom scripts used to assist in continuous integration/deployment.

There are a few pieces of information that `BuildPlayerContent` takes into consideration when performing the build: the `AddressablesDefaultSettingsObject`, `ActivePlayerDataBuilder`, and the `content_state.bin`.

The `AddressablesDefaultSettingsObject` drives which groups get processed and which `Addressables` profile is used.  The profile dictates variables such as local and remote build paths as well as load paths.  A default `AddressablesDefaultSettingsObject` is provided but can be overwritten by setting `AddressablesDefaultSettingsObject.Settings` to your custom `AddressableAssetSettings`.

Ensure the active profile is set to the desired profile ID before performing the build.  If, for example, you have a profile you want to use for a continuous integration pipeline called "Custom CI Profile", you can set the active profile using this code snippet,
```
AddressableAssetProfileSettings profileSettings = AddressableAssetSettingsDefaultObject.Settings.profileSettings;
string profileId = profileSettings.GetProfileId("Custom CI Profile");
AddressableAssetSettingsDefaultObject.Settings.activeProfileId = profileId;
```

The build is performed using the `ActivePlayerDataBuilder`, which is determined by a combination of the `ActivePlayerDataBuilderIndex` and the list of `DataBuilders`.  The list of `DataBuilders` is comprised of `ScriptableObjects`, which implement the `IDataBuilder` interface.  A basic example of adding and setting the `ActivePlayerDataBuilder` would look like this,
```
public void AddAndSetActiveDataBuilder(IDataBuilder dataBuilder)
{
    if (AddressableAssetSettingsDefaultObject.Settings.AddDataBuilder(dataBuilder))
    {
        AddressableAssetSettingsDefaultObject.Settings.ActivePlayerDataBuilderIndex =
            AddressableAssetSettingsDefaultObject.Settings.DataBuilders.Count - 1;
    }
}
```
This sample adds the `IDataBuilder` to the list of `DataBuilders` and then sets the `ActivePlayerDataBuilderIndex` to the last index of that list.  Other useful methods for manipulating the `DataBuilders` are `RemoveDataBuilder` and `SetDataBuilderAtIndex`.

It may also be desirable to save content state between builds, typically for use with content build updates.  `Addressables` uses a `content_state.bin` to save the `AssetState` into a structure called a `CachedAssetState`.  Retrieval of the `.bin` file can be done as follows,
```
string contentStatePath = ContentUpdateScript.GetContentStateDataPath(false);
AddressablesContentState contentState = ContentUpdateScript.LoadContentState(contentStatePath);
//...
```
and then saving the new content state can be done using `ContentUpdateScript.SaveContentState(...)`.