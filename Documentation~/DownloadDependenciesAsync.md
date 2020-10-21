---
uid: addressables-api-download-dependencies-async
---
# Addressables.DownloadDependenciesAsync
#### API
- `static AsyncOperationHandle DownloadDependenciesAsync(object key, bool autoReleaseHandle = false)`
- `static AsyncOperationHandle DownloadDependenciesAsync(IList<IResourceLocation> locations, bool autoReleaseHandle = false)`
- `static AsyncOperationHandle DownloadDependenciesAsync(IEnumerable keys, MergeMode mode, bool autoReleaseHandle = false)`

#### Returns
`AsyncOperationHandle`: an async operation handle that encompasses all the operations used to download the requested dependencies.  Once complete, this handle can be safely released.

#### Description
`Addressables.DownloadDependenciesAsync` is primarily designed to be used to download and cache remote `AssetBundles` prior to their use at runtime.  Caching `AssetBundles` early leads to improved performance on any initial call, such as a `LoadAssetAsync`, that would have otherwise needed to download the bundles as part of their operation.  After reading the `Result` of this operation, it is best practice to release the handle manually; though the memory footprint is low, should you keep it in memory.

This operation can be safely released once it is complete or you can pass `true` in the `autoReleaseHandle` parameter to ensure it is released on completion.  Of note, if the handle is released you won't be able to check the success of the operation handle through the `Status` property since the release invalidates the operation handle.

Downloaded `AssetBundles` are stored in the engines `AssetBundle` cache.  `Addressables` provides a type of initialization object called a [`CacheInitializationSettings`](xref:UnityEditor.AddressableAssets.Settings.CacheInitializationSettings) which can be used to control the `AssetBundle` cache settings.

##### Related API
`GetDownloadSizeAsync` checks the total size of all `AssetBundles` that need to be downloaded.  Cached `AssetBundles` return a size of 0.  Unlike most operations that don't load anything, `GetDownloadSizeAsync` does not autorelease.  It does not autorelease because the `Result` is needed which could not be accessed from a released handle.  Once you have read the size out of the `Result`, you are responsible for releasing the operation handle; not doing so has little impact on the memory footprint.

`ClearDependencyCacheAsync` clears any cached `AssetBundles` for a given key or list of keys and its dependencies.  It is also possible to use the [`UnityEngine.Caching`](https://docs.unity3d.com/ScriptReference/Caching.html) APIs to manipulate the cache used by Addressables.  Of note, `ClearDependencyCacheAsync` works off the current content catalog.  This means if an `AssetBundle` is downloaded and cached, and the content catalog is updated to point to a new version of that `AssetBundle`, and it is possible for the previously cached bundle to remain in the cache until the cache says it's expired.

#### Code Sample
```
public IEnumerator Start()
{
    string key = "assetKey";
    //Clear all cached AssetBundles
    Addressables.ClearDependencyCacheAsync(key);

    //Check the download size
    AsyncOperationHandle<long> getDownloadSize = Addressables.GetDownloadSizeAsync(key);
    yield return getDownloadSize;

    //If the download size is greater than 0, download all the dependencies.
    if (getDownloadSize.Result > 0)
    {
        AsyncOperationHandle downloadDependencies = Addressables.DownloadDependenciesAsync(key);
        yield return downloadDependencies;
    }
    
    //...
}
```

#### Pitfalls
Some platforms, such as PlayStation 4 and Nintendo Switch, do not support local `AssetBundle` caching.