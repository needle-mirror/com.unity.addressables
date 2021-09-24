---
uid: addressables-loading-bundles
---

# AssetBundle Loading

The Addressables system packs your assets in AssetBundles and loads these bundles "behind the scenes" as you load individual assets. You can control how AssetBundles load which are exposed on the `BundledAssetGroupSchema` class. You can set these options through the scripting API or under the Advanced options in the inspector of the `AddressablesAssetGroup` inspector.

## UnityWebRequestForLocalBundles

Addressables can load AssetBundles via two engine APIs: `UnityWebRequest.GetAssetBundle`, and `AssetBundle.LoadFromFileAsync`. The default behavior is to use `AssetBundle.LoadFromFileAsync` when the AssetBundle is in local storage and use `UnityWebRequest` when the AssetBundle path is a URL.

You can override this behavior to use `UnityWebRequest` for local Asset Bundles by setting `BundledAssetGroupSchema.UseUnityWebRequestForLocalBundles` to true. It can also be set through the BundledAssetGroupSchema GUI. 

A few of these situations would include:

1. You are shipping local AssetBundles that use LZMA compression because you want your shipped game package to be as small as possible. In this case, you would want to use UnityWebRequest to recompress those AssetBundles LZ4 into the local disk cache.
2. You are shipping an Android game and your APK contains AssetBundles that are compressed with the default APK compression.
3. You want the entire local AssetBundle to be loaded into memory to avoid disk seeks. If you use `UnityWebRequest` and have caching disabled, the entire AssetBundle file will be loaded into the memory cache. This increases your runtime memory usage, but may improve loading performance as it eliminates disk seeking after the initial AssetBundle load.
Both situations 1 and 2 above result in the AssetBundle existing on the player device twice (original and cached representations). This means the initial loads (decompressing and copying to cache) are slower than subsequent loads (loading from cache)