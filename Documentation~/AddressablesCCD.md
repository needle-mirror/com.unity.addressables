---
uid: addressables-ccd
---

# Addressable Asset system with Cloud Content Delivery

You can use the [Addressable asset system] in conjunction with [Unity Cloud Content Delivery] \(CCD) to distribute your remote Addressables content.

**Note**: The purpose of this page is to describe how to link the concepts of Addressable Assets to CCD, and isn't meant to be an in-depth discussion of these ideas. Before you read this page, make sure you are familiar with both the Addressable system and Cloud Content Delivery.

To set up Addressable assets to work with CCD:
1. [Configure a profile to include your CCD URL]
1. [Build your AssetBundles, then upload them to CCD]

See [Getting Started] for information about installing and implementing the Addressables package.

See [Upgrading to the Addressables System] for information about integrating Addressables in an existing Unity Project.

See [Remote content distribution] for information on how to set up your Project so that you can host your Addressables content on a remote server.

See [Unity Cloud Content Delivery] for more information about CCD.

## Configure profile with CCD URL

> [!TIP]
> The `BuildPath` and `LoadPath` variables stored in [Profiles] specify where the Addressables system creates your build artifacts and where it looks for your assets at runtime. Configure the remote paths to work with CCD. (Leave the local paths with their standard, default values, unless you have a specifc reason to change them.)

If necessary, create a new profile for publishing builds to CCD on the [Profiles] window. Configure the remote path variables in this profile to access your content at the correct URL.

You can set the remote `BuildPath` to a convenient value. If you have multiple profiles, consider using a unique build path for each of them so that the build artifacts do not get mixed together, especially if you are hosting them from a different remote URL.

Set the remote `LoadPath` to one of the following two paths:

* If you publish content using a badge: 

```c#
  https://(ProjectID).client-api.unity3dusercontent.com/client_api/v1/buckets/(BucketID)/release_by_badge/(BadgeName)/entry_by_path/content/?path=

```

* If you publish using a release: 

```c#
  https://(ProjectID).client-api.unity3dusercontent.com/client_api/v1/buckets/(BucketID)/releases/(ReleaseID)/entry_by_path/content/?path=

```

where:
* `(ProjectID)` is your CCD project's ID string
* `(BucketID)` is the Bucket ID string for a CCD bucket within your project
* `(ReleaseID)` is the ID of a specific release within a bucket
* `(BadgeName)` is the name of the specific CCD badge

See [Profiles] for information about how to create and edit profiles.

> [!IMPORTANT]
> You must perform a full rebuild your Addressables content when you change the remote load path. 

## Build and upload Addressable content to CCD

To generate the Addressable content and upload it to your CCD project:

1. Set the profile you have set up for CCD as the active profile.
2. Build your Addressables content. 
   * If you are making a full content build, see [Building your Addressable content].
   * If your are updating an existing build with modified remote content, see [Building for content updates].
3. Upload the files created at the remote build path using the CCD dashboard or command-line interface. See [Unity Cloud Content Delivery] for more information.

Building your Addressable content generates a content catalog  (.json), a hash file (.hash), and one or more AssetBundle (.bundle) files. Upload these files to the bucket you corresponding to the URL used in your profile load path.

If you have made changes to local content, you must create a new Player build.

If you are using the Unity Cloud Build service, you can configure your cloud builds to send content to CCD. See [Using Addressables in Unity Cloud Build] for information.


[Getting Started]: xref:addressables-getting-started
[Upgrading to the Addressables System]: xref:addressables-migration
[Remote content distribution]: xref:addressables-remote-content-distribution
[Profiles]: xref:addressables-profiles
[default values]: xref:addressables-profiles#default-path-values
[Addressable Asset system]: xref:addressables-home
[Asset Hosting Services]: ./AddressableAssetsHostingServices.md
[AssetBundles]: xref:AssetBundlesIntro
[Build your AssetBundles, then upload them to CCD]: #build-and-upload-addressable-content-to-ccd
[Building for content updates]: ./ContentUpdateWorkflow.md#building-content-updates
[Building your Addressable content]: xref:addressables-building-content
[Configure a profile to include your CCD URL]: #configure-profile-with-ccd-url
[Marking assets as Addressable]: xref:addressables-getting-started#making-an-asset-addressable
[Unity Cloud Content Delivery]: https://docs.unity3d.com/Manual/UnityCCD.html
[Using Addressables in Unity Cloud Build]: xref:UnityCloudBuildAddressables
[Groups]: xref:addressables-groups
