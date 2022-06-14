---
uid:  addressables-faq
---

# Addressables FAQ

<a name="faq-best-compression"></a>
### What compression settings are best?
See [AssetBundle Compression].

<a name="faq-bundle-size"></a>
### Is it better to have many small bundles or a few bigger ones?
See [Packing groups into AssetBundles].

<a name="faq-minimize-catalog-size"></a>
### Are there ways to minimize the catalog size?
See [Catalog settings]. 

<a name="faq-content-state-file"></a>
### What is addressables_content_state?
See [Content State File].

<a name="faq-scale-implications"></a>
### What are possible scale implications?
See [Scale implications as your project grows larger].

<a name="faq-load-modes"></a>
### What Asset Load Mode to use?
See [Asset Load Mode].

<a name="faq-internal-naming"></a>
### What are the Internal naming mode implications?
See [Advanced Group Settings].

<a name="faq-edit-loaded-assets"></a>
### Is it safe to edit loaded Assets?
When editing Assets loaded from Bundles, in a Player or when using "Use Existing Build (requires built groups)" playmode setting. The Assets are loaded from the Bundle and only exist in memory. Changes cannot be written back to the Bundle on disk, and any modifications to the Object in memory do not persist between sessions.

This is different when using "Use Asset Database (fastest)" or "Simulate Groups (advanced)" playmode settings, in these modes the Assets are loaded from the Project files. Any modifications that are made to loaded Asset modifies the Project Asset, and are saved to file.

In order to prevent this, when making runtime changes, create a new instance of the Object to modify and use as the Object to create an instance of with the Instantiate method. As shown in the example code below. 

```
var op = Addressables.LoadAssetAsync<GameObject>("myKey");
yield return op;
if (op.Result != null)
{
    GameObject inst = UnityEngine.Object.Instantiate(op.Result);
    // can now use and safely make edits to inst, without the source Project Asset being changed.
}
```

**Please Note**, When instancing an Object:
* The AsyncOperationHandle or original Asset must be used when releasing the Asset, not the instance.
* Instantiating an Asset that has references to other Assets does not create new instances other those referenced Assets. The references remain targeting the Project Asset.
* Unity Methods are invoked on the new instance, such as Start, OnEnable, and OnDisable.

<a name="faq-get-address"></a>
### Is it possible to retrieve the address of an asset or reference at runtime?
In the most general case, loaded assets no longer have a tie to their address or `IResourceLocation`. There are ways, however, to get the properly associated `IResourceLocation` and use that to read the field PrimaryKey. The PrimaryKey field will be set to the assets' address unless "Include Address In Catalog" is disabled for the group this object came from. In that case, the PrimaryKey will be the next item in the list of keys (probably a GUID, but possibly a Label or empty string). 

#### Examples

Retrieving an address of an AssetReference. This can be done by looking up the Location associated with that reference, and getting the PrimaryKey:

```
var op = Addressables.LoadResourceLocationsAsync(MyRef1);
yield return op;
if (op.Status == AsyncOperationStatus.Succeeded &&
	op.Result != null &&
	op.Result.Count > 0)
{
	Debug.Log("address is: " + op.Result[0].PrimaryKey);
}
```

Loading multiple assets by label, but associating each with their address. Here, again LoadResourceLocationsAsync is needed:

```
Dictionary<string, GameObject> _preloadedObjects = new Dictionary<string, GameObject>();
private IEnumerator PreloadHazards()
{
	//find all the locations with label "SpaceHazards"
	var loadResourceLocationsHandle = Addressables.LoadResourceLocationsAsync("SpaceHazards", typeof(GameObject));
	if( !loadResourceLocationsHandle.IsDone )
		yield return loadResourceLocationsHandle;
	
	//start each location loading
	List<AsyncOperationHandle> opList = new List<AsyncOperationHandle>();
	foreach (IResourceLocation location in loadResourceLocationsHandle.Result)
	{
		AsyncOperationHandle<GameObject> loadAssetHandle = Addressables.LoadAssetAsync<GameObject>(location);
		loadAssetHandle.Completed += obj => { _preloadedObjects.Add(location.PrimaryKey, obj.Result); };
		opList.Add(loadAssetHandle);
	}
	
	//create a GroupOperation to wait on all the above loads at once. 
	var groupOp = Addressables.ResourceManager.CreateGenericGroupOperation(opList);
	if( !groupOp.IsDone )
		yield return groupOp;
	
	Addressables.Release(loadResourceLocationsHandle);

	//take a gander at our results.
	foreach (var item in _preloadedObjects)
	{
		Debug.Log(item.Key + " - " + item.Value.name);
	}
}
```

<a name="faq-build-while-compiling"></a>
### Can I build Addressables when recompiling scripts?
If you have a pre-build step that triggers a domain reload, then you must take special care that the Addressables build itself does not start until after the domain reload is finished.

Using methods such as setting scripting define symbols ([PlayerSettings.SetScriptingDefineSymbolsForGroup](https://docs.unity3d.com/ScriptReference/PlayerSettings.SetScriptingDefineSymbolsForGroup.html)) or switching active build target ([EditorUserBuildSettings.SwitchActiveBuildTarget](https://docs.unity3d.com/ScriptReference/EditorUserBuildSettings.SwitchActiveBuildTarget.html)), triggers scripts to recompile and reload. The execution of the Editor code will continue with the currently loaded domain until the domain reloads and execution stops. Any [platform dependent compilation](https://docs.unity3d.com/Manual/PlatformDependentCompilation.html) or custom defines will not be set until after the domain reloads. This can lead to unexpected issues where code relies on these defines to build correctly, and can be easily missed.

#### Best Practice
When building via commandline arguments or CI, Unity recommends restarting the Editor for each desired platform using [command line arguments](https://docs.unity3d.com/Manual/CommandLineArguments.html). This ensures that scripts are compiled for a platform before -executeMethod is invoked.

#### Is there a safe way to change scripts before building?
To switch Platform, or modify Editor scripts in code and then continue with the defines set, a domain reload must be performed. Note in this case, -quit argument should not be used or the Editor will exit immediately after execution of the invoked method.

When the domain reloads, InitialiseOnLoad is invoked. The code below demonstrates how to set scripting define symbols and react to those in the Editor code, building Addressables after the domain reload completes. The same process can be done for switching platforms and [platform dependent compilation](https://docs.unity3d.com/Manual/PlatformDependentCompilation.html).

```
[InitializeOnLoad]
public class BuildWithScriptingDefinesExample
{
    static BuildWithScriptingDefinesExample()
    {
        bool toBuild = SessionState.GetBool("BuildAddressables", false);
        SessionState.EraseBool("BuildAddressables");
        if (toBuild)
        {
            Debug.Log("Domain reload complete, building Addressables as requested");
            BuildAddressablesAndRevertDefines();
        }
    }

    [MenuItem("Build/Addressables with script define")]
    public static void BuildTest()
    {
#if !MYDEFINEHERE
        Debug.Log("Setting up SessionState to inform an Addressables build is requested on next Domain Reload");
        SessionState.SetBool("BuildAddressables", true);
        string originalDefines = PlayerSettings.GetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
        string newDefines = string.IsNullOrEmpty(originalDefines) ? "MYDEFINEHERE" : originalDefines + ";MYDEFINEHERE";
        Debug.Log("Setting Scripting Defines, this will then start compiling and begin a domain reload of the Editor Scripts.");
        PlayerSettings.SetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup, newDefines);
#endif
    }

    static void BuildAddressablesAndRevertDefines()
    {
#if MYDEFINEHERE
        Debug.Log("Correct scripting defines set for desired build");
        AddressableAssetSettings.BuildPlayerContent();
        string originalDefines = PlayerSettings.GetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
        if (originalDefines.Contains(";MYDEFINEHERE"))
            originalDefines = originalDefines.Replace(";MYDEFINEHERE", "");
        else
            originalDefines = originalDefines.Replace("MYDEFINEHERE", "");
        PlayerSettings.SetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup, originalDefines);
        AssetDatabase.SaveAssets();
#endif
        EditorApplication.Exit(0);
    }
}
``` 

<a name="faq-monoscript-changes"></a>
### What changes to scripts require rebuilding Addressables?
Classes in Addressables content are referenced using a MonoScript object. Which defines a class using the Assembly name, [Namespace], and class name or the referenced class.

When loading content at runtime the MonoScript is used to load and create and instance of the runtime class from the player assemblies.
Changes to MonoScripts need to be consistent between the Player and the built Addressables content. Both the player and Addressables content must be rebuilt in order to load the classes correctly.

The following can result in changes to the MonoScript data:
- Moving the script file to a location that comes under another [Assembly Definition File]
- Changing the name of the [Assembly Definition File] containing the class
- Adding or Changing the class [Namespace]
- Changing the class name

#### How to minimize changes to bundles
Content bundles can be large, and having to update the whole bundle for small changes can result in a large amount of data being updated for a small change to the MonoScript.
Enabling the "MonoScript Bundle Naming Prefix" option in the [Addressables settings] will build an asset bundle that contains the MonoScript objects, separate to your serialized data.
If there are no changes to the serialized class data then only the MonoScript bundle will have changed and other bundles will not need to be updated.

#### Referencing Subobjects
What gets included in a content build relies heavily on how your assets, and scripts, reference each other.  This can be tricky when subobjects get involved.  

If an `AssetReference` points to a subobject of an Asset that is Addressable, the entire object is built into the `AssetBundle` at build time.  If, instead, the `AssetReference` points to an Addressable object, such as a `GameObject`, `ScriptableObject`, or `Scene`, that in turn directly refrences a subobject, only the subobject is pulled into the `AssetBundle` as an implicit dependency.

[Addressables settings]: xref:addressables-asset-settings#build
[Advanced Group Settings]:  xref:addressables-group-settings#advanced-options
[Asset Load Mode]: xref:addressables-group-settings#asset-load-mode
[Assembly Definition File]: https://docs.unity3d.com/Manual/ScriptCompilationAssemblyDefinitionFiles.html
[AssetBundle Compression]: xref:addressables-group-settings#assetbundle-compression
[Catalog settings]: xref:addressables-build-artifacts#catalog-settings
[Content State File]: xref:addressables-build-artifacts#content-state-file
[Namespace]: https://docs.unity3d.com/Manual/Namespaces.html
[Packing groups into AssetBundles]: xref:addressables-packing-groups
[Scale implications as your project grows larger]: xref:addressables-packing-groups#scale-implications-as-your-project-grows-larger
