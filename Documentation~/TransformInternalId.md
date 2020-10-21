---
uid: addressables-api-transform-internal-id
---
# TransformInternalId
#### Returns
`string` A transformation of a given location to a potentially different location used at runtime.

#### Description
`string Addressables.ResourceManager.TransformInternalId(IResourceLocation)` is a function that Addressables uses when evaluating internal IDs.  You can also manually pass in an IResourceLocation and return an ID based on a transformation function you specify.  You can specify the transformation used by the `ResourceManager` by assigning a `Func<IResourceLocation, string>` to `Addressables.ResourceManager.InternalIdTransformFunc`.  If no `InternalIdTransformFunc` is available, then the `IResourceLocation.InternalId` is returned.  `IResourceLocation.InternalId` is the default location assigned to an `IResourceLocation` at build time to locate an asset.

Using `TransformInternalId` grants a fair amount of flexability, especially in regards to remote hosting.  Given a single IResourceLocation, you can transform the ID to point towards a server specified at runtime.  This is particularly useful if your server IP address changes or to point at different servers for variant purposes.

If `Addressables.ResourceManager.InternalIdTransformFunc` is not assigned to or is assigned `null`, then the `IResourceLocation.InternalId` is used by `ResourceManager` without passing any transformation.

You do not need to call `TransformInternalId` manually in order for `ResourceManager` to use it.

See [`ResourceManager`](xref:UnityEngine.ResourceManagement.ResourceManager)) for its full API documentation.

#### Code Sample
```
void Start()
{
    Addressables.ResourceManager.InternalIdTransformFunc = TransformFunc;
}

string TransformFunc(IResourceLocation location)
{
    //Implement a method that gets the base url for a given location
    string baseUrl = GetBaseURL(location);
    
    //Get the url you want to use to point to your current server
    string currentUrlToUse = GetCurrentURL();
    
    return location.InternalId.Replace(baseUrl, currentUrlToUse);
}
```
In the above code sample, `GetBaseURL` and `GetCurrentURL` can be any solution you find appropriate for your project; these are not methods implemented in Addressables.  The former can be your solution for acquiring the IP/url that was used at build time in the project and will be part of the `IResourceLocation.InternalId`.  The latter can be your solution to get a server that points to your new server IP/url or specifc variant/themed/scaled content.

```
void Start()
{
    Addressables.ResourceManager.InternalIdTransformFunc = TransformFunc;
}

string TransformFunc(IResourceLocation location)
{
    if(location.PrimaryKey.Contains("background") && MyQualityCheck.Resolution == Res.Low)
        return location.InternalId.Replace("background", "bkg_low");

    return irl.InternalId;
}
```
This example shows how you could use information provided from the `IResourceLocation` among other settings to transform the `IResourceLocation.InternalId`.

These are, of course, only a basic examples of the possibilities provided by assigning a custom implementation to `Addressables.ResourceManager.InternalIdTransformFunc`.

#### Pitfalls
If your first loads point to the wrong server, ensure that you assign `Addressables.ResourceManager.InternalIdTransformFunc` to your desired transform function prior to initiating your operation.