---
uid: addressables-api-download-dependencies-async
---

# Pre-download remote content

When you distribute content remotely, you can improve performance by downloading dependencies in advance of when your application needs them. For example, you can download essential content on start up when your application is launched for the first time to make sure that users don't have to wait for content in the middle of gameplay.

## Create a pre-download script

Use the [`Addressables.DownloadDependenciesAsync`](xref:UnityEngine.AddressableAssets.Addressables.DownloadDependenciesAsync*)  method to pre-download content so that it's cached on disk and faster to access when the application needs it. This method downloads an Addressable entity and any dependencies as a background task.

[!code-cs[sample](../Tests/Editor/DocExampleCode/MiscellaneousTopics.cs#doc_Download)]

Calling the `Addressables.DownloadDependenciesAsync` method loads the dependencies for the address or label that you pass in. Typically, this is the AssetBundle.

> [!TIP]
> If you have a set of assets that you want to pre-download, you can assign the [same label](Labels.md), such as `preload`, to the assets and use that label as the key when calling [`Addressables.DownloadDependenciesAsync`](xref:UnityEngine.AddressableAssets.Addressables.DownloadDependenciesAsync*). Addressables downloads all the AssetBundles containing an asset with that label if not already available, along with any AssetBundles containing the assets' dependencies.


## Monitor download progress

An [`AsyncOperationHandle`](xref:UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle) instance provides the following ways to get progress updates:

* [`AsyncOperationHandle.PercentComplete`](xref:UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle.PercentComplete): Reports the percentage of sub operations that have finished. For example, if an operation uses six sub operations to perform its task, the `PercentComplete` indicates the entire operation is 50% complete when three of those operations have finished (it doesn't matter how much data each operation loads).
* [`AsyncOperationHandle.GetDownloadStatus`](xref:UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle.GetDownloadStatus): Returns a [`DownloadStatus`](xref:UnityEngine.ResourceManagement.AsyncOperations.DownloadStatus) struct that reports the percentage of total download size. For example, if an operation has six sub operations, but the first operation represented 50% of the total download size, then `GetDownloadStatus` indicates the operation is 50% complete when the first operation finishes.

The following example illustrates how you can use `GetDownloadStatus` to check the status and dispatch progress events during the download:

[!code-cs[sample](../Tests/Editor/DocExampleCode/PreloadWithProgress.cs#doc_Preload)]

To discover how much data you need to download to load one or more assets, you can call [`Addressables.GetDownloadSizeAsync`](xref:UnityEngine.AddressableAssets.Addressables.GetDownloadSizeAsync*):

[!code-cs[sample](../Tests/Editor/DocExampleCode/PreloadWithProgress.cs#doc_DownloadSize)]

The `Result` of the completed operation is the number of bytes that must be downloaded. If Addressables has already cached all the required AssetBundles, then `Result` is zero.

Always release the download operation handle after you have read the `Result` object. If you don't need to access the results of the download operation, you can automatically release the handle by setting the `autoReleaseHandle` parameter to true, as shown in the following example:

[!code-cs[sample](../Tests/Editor/DocExampleCode/Preload.cs#doc_Preload)]

>[!NOTE]
> On the WebGL platform, this API always returns the size of the AssetBundle, even if the AssetBundle has been cached. Cached AssetBundles aren't stored on the local file system, but persisted as part of the IndexedDB of the browser.  [WebGL Caching](https://docs.unity3d.com/Manual/webgl-caching.html)

## Clear the dependency cache

If you want to clear any AssetBundles cached by Addressables, call [`Addressables.ClearDependencyCacheAsync`](xref:UnityEngine.AddressableAssets.Addressables.ClearDependencyCacheAsync*). This method clears the cached AssetBundles containing the assets identified by a key along with any AssetBundles containing those assets' dependencies.

`ClearDependencyCacheAsync` only clears AssetBundles related to the specified key. If you updated the content catalog so the key no longer exists or it no longer depends on the same AssetBundles, then these AssetBundles remain in the cache until they expire based on [cache settings](xref:UnityEngine.Cache).

To clear all AssetBundles, you can use the methods in the [`UnityEngine.Caching`](xref:UnityEngine.Caching) class.

## Ask users for download consent

If you want to ask the user for consent before download, use [`Addressables.GetDownloadSizeAsync`](xref:UnityEngine.AddressableAssets.Addressables.GetDownloadSizeAsync*) to return how much space is needed to download the content from a given address or label. Note that this takes into account any previously downloaded AssetBundles that are still in Unity's AssetBundle cache.

## When to download on demand

While it can be advantageous to download assets for your application in advance, there are instances where you might choose not to do so. For example:

* If your application has a large amount of online content, and you expect users to only ever interact with some of it.
* Your application must be connected online to function. If all your application's content is in small AssetBundles, you might choose to download content as needed.

## Additional resources

* [`Addressables.DownloadDependenciesAsync` API reference](xref:UnityEngine.AddressableAssets.Addressables.DownloadDependenciesAsync*)
* [Enable remote content](remote-content-enable.md)