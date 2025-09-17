---
uid: addressables-api-transform-internal-id
---

# Change Addressable load URLs

You can modify the URLs that Addressables uses to load assets at runtime in the following ways:

* [Use static properties in a profile variable](#static-profile-variables)
* [Implement an ID transform method](#id-transform-method)
* [Implement a WebRequestOverride method](#webrequest-override)

## Static profile variables

When you define the [RemoteLoadPath Profile variable](addressables-profiles-window.md#default-variables) you can use a static property to specify all or part of the URL that your application loads remote content from, including catalogs, catalog hash files, and AssetBundles. For information about specifying a property name in a profile variable refer to [Profile variable syntax](ProfileVariables.md#profile-variable-syntax).

The value of the static property must be set before Addressables initializes. Changing the value after initialization has no effect.

## ID transform method

You can assign a method to the [`Addressables.ResourceManager`](xref:UnityEngine.AddressableAssets.Addressables.ResourceManager) object's [`InternalIdTransformFunc`](xref:UnityEngine.ResourceManagement.ResourceLocations.IResourceLocation.InternalId) property to individually change the URLs that Addressables loads assets from. You must assign the method before the relevant operation starts, otherwise Unity uses the default URL.

Using `TransformInternalId` is useful for remote hosting. Given a single `IResourceLocation`, you can transform the ID to point towards a server specified at runtime. This is useful if the server IP address changes or if you use different URLs to give different variants of application assets.

`ResourceManager` calls the `TransformInternalId` method when it looks up an asset, passing the [`IResourceLocation`](xref:UnityEngine.ResourceManagement.ResourceLocations.IResourceLocation) instance for the asset to your method. You can change the [`InternalId`](xref:UnityEngine.ResourceManagement.ResourceLocations.IResourceLocation.InternalId) property of the `IResourceLocation` and return the modified object to the `ResourceManager`.

The following example illustrates how you can append a query string to all URLs for AssetBundles:

[!code-cs[sample](../Tests/Editor/DocExampleCode/IDTransformer.cs#doc_Transformer)]

## WebRequest override

You can assign a method to the `Addressable` object's [`WebRequestOverride`](xref:UnityEngine.AddressableAssets.Addressables.WebRequestOverride) property to individually change the [`UnityWebRequest`](xref:UnityEngine.Networking.UnityWebRequest) used to download files, such as an AssetBundle or catalog `.json` file. You must assign the method before the relevant operation starts, otherwise Unity uses the default `UnityWebRequest`.

The `ResourceManager` calls `WebRequestOverride` before [`UnityWebRequest.SendWebRequest`](xref:UnityEngine.Networking.UnityWebRequest.SendWebRequest) is called and passes the `UnityWebRequest` for the download to your method.

The following example shows how you can append a query string to all URLs for AssetBundles and catalogs:

[!code-cs[sample](../Tests/Editor/DocExampleCode/WebRequestOverride.cs#doc_TransformerWebRequest)]

## Additional resources

* [Enable remote content](remote-content-enable.md)
* [Define remote content profiles](remote-content-profiles.md)