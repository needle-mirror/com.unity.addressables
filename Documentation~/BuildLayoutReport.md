---
uid: addressables-build-layout-report
---

# Create a build report

The build layout report provides detailed information and statistics about Addressables builds. The format of the report depends on whether you're using AssetBundles or content directories as the [content build system](content-build-systems.md):

* **Content directories**: Uses the [**Build Analysis** window](https://docs.unity3d.com/6000.6/Documentation/Manual/build-analysis-window-reference.html) to display the details of the content build.
* **AssetBundles**: Uses the [**Addressables Report** window](addressables-report-window.md) to display the details of the content build at  `Library/com.unity.addressables/buildlayout.json`.

When the **Debug Build Layout** setting is enabled in the [**Preferences** window](addressables-preferences.md), Unity creates the report whenever you build Addressables content.

## Create a build report

To create a build report, you must enable the **Debug Build Layout** setting, which creates a build report whenever you create a content build:

1. Open the [**Preferences** window](addressables-preferences.md) (menu: **Edit > Preferences**, macOS: **Unity > Settings**).
1. Select __Addressables__ from the list of preference types.
1. Enable the __Debug Build Layout__ option.
1. [Perform a build](builds-full-build.md) of Addressables content.
1. Open the [**Addressables Report** window](addressables-report-window.md) (**Window** > **Asset Management** > **Addressables** > **Addressables Report**) to view the report.

>[!TIP]
> To open a report as soon as a build completes, enable **Open Build Analysis after build** in the [**Preferences** window](addressables-preferences.md). In Unity 6.7 and later, this opens the [**Build Analysis** window](https://docs.unity3d.com/6000.6/Documentation/Manual/build-analysis-window-reference.html). In Unity 6.6 and earlier, this setting is labeled **Open Addressables Report after build** and opens the **Addressables Report** window. When the **Addressables Report** window displays a build that contains content directories, it also displays a button to open the **Build Analysis** window to inspect the build further.

## Additional resources

* [Addressables Report window reference](addressables-report-window.md)
* [Addressables Preferences reference](addressables-preferences.md)
* [Build Analysis window reference](https://docs.unity3d.com/6000.6/Documentation/Manual/build-analysis-window-reference.html)

