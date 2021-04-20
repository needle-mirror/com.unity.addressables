# AssetBundle Loading

Assets are stored in AssetBundles which will be loaded for you as you use the Addressables loading APIs. There are various ways you can control how AssetBundles load which are exposed on the `BundledAssetGroupSchema` class. You can set these options through the scripting API or under the Advanced options in the inspector of the `AddressablesAssetGroup` inspector.

## UnityWebRequestForLocalBundles

Addressables can load AssetBundles via two engine APIs: `UnityWebRequest.GetAssetBundle`, and `AssetBundle.LoadFromFileAsync`. The default behavior is to use `AssetBundle.LoadFromFileAsync` when the AssetBundle is in local storage and use `UnityWebRequest` when the AssetBundle path is a URL.

You can override this behavior to use `UnityWebRequest` for local Asset Bundles by setting `BundledAssetGroupSchema.UseUnityWebRequestForLocalBundles` to true. It can also be set through the BundledAssetGroupSchema GUI. 

A few of these situations would include:

1. You are shipping local AssetBundles using LZMA compression because you want your shipped game package to be as small as possible. In this case, you would want to use UnityWebRequest to recompress those AssetBundles LZ4 into the local disk cache.
2. You are shipping an Android game and your APK contains AssetBundles that are compressed with the default APK compression.
3. You want the entire local AssetBundle to be loaded into memory to avoid disk seeks. If you use `UnityWebRequest` and have caching disabled, the entire AssetBundle file will be loaded into the memory cache. This increases your runtime memory usage, but may improve loading performance as it eliminates disk seeking after the initial AssetBundle load.
Both situations 1 and 2 above, will result in the AssetBundle existing on the player device twice (original and cached representations). This will mean initial loads (decompressing and copying to cache) will be slower than subsequent loads (loading from cache)