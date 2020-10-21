---
uid: addressables-ccd
---
# Addressable Asset system with Cloud Content Delivery

In addition to the existing [Asset Hosting Services](./AddressableAssetsHostingServices.md), the [Addressable Asset system](./index.md) can be used in conjunction with [Cloud Content Delivery](https://docs.unity3d.com/Manual/UnityCCD.html) (CCD) to have the CCD service serve assets you configure using the Addressable Asset system.

**Note**: The purpose of this page is to describe how to link the concepts of Addressable Assets to CCD, and is not meant to be an in-depth discussion of these ideas. Before reading this page, make sure you are familiar with both the Addressable Asset system and Cloud Content Delivery.

In order to set up Addressable Assets to work with CCD:
1. [Configure your assets as addressable.](#configure-your-assets-as-addressable)
1. [Configure your groups.](#configure-your-groups)
1. [Configure your profile to include your CCD URL.](#configure-profile-with-ccd-url)
1. [Build your AssetBundles, then upload them to CCD.](#build-and-upload-addressable-content-to-ccd)

## Configure your assets as addressable
The first step to managing Addressable Assets with CCD is to mark the assets you require as Addressable. See [Marking assets as Addressable](./AddressableAssetsGettingStarted.md#marking-assets-as-addressable).

## Configure your groups
Next, it's important to place your assets into groups. This is important because you need to generate [AssetBundles](https://docs.unity3d.com/Manual/AssetBundlesIntro.html "AssetBundles") later by building these groups. See [Using the Addressables window](./AddressableAssetsGettingStarted.md#using-the-addressables-window).

## Configure profile with CCD URL
Next, you need to link your project to your CCD project. You do this by inserting a custom URL as the `RemoteLoadPath` of your desired Addressables profile. At this stage, there are two workflow paths you can choose, each with its own custom URL.
* The basic workflow just points to the latest content in the specified bucket, given by: `https://(ProjectID).client-api.unity3dusercontent.com/client_api/v1/buckets/(BucketID)/entry_by_path/content/?path=`
* The badge workflow links your project to the contents pointed at by a specific badge: `https://(ProjectID).client-api.unity3dusercontent.com/client_api/v1/buckets/(BucketID)/release_by_badge/(BadgeName)/entry_by_path/content/?path=`

where:
* `(ProjectID)` is your CCD project's ID string.
* `(BucketID)` is the Bucket ID string for the CCD bucket within your project with which you want to interact.
* `(BadgeName)` is the name of the specific CCD badge with which you want to interact.

Once you have your custom URL:
1. In the Editor, select **Window** > **Asset Management** > **Addressables** > **Profiles**.
1. In the `RemoteLoadPath` field for the desired profile row, enter your URL.

For that profile, your project will now know where to fetch its Addressable Assets.

## Build and upload Addressable content to CCD
Next, for the profile you want to use with CCD, you must generate your Addressable content that you will later place in your CCD project.
* To build content, see [Building your Addressable content](./AddressableAssetsGettingStarted.md#building-your-addressable-content).
* If you are changing the contents of a group, see [Building for content updates](./ContentUpdateWorkflow.md#building-for-content-updates).

Building your Addressable content can generate a content catalog  (.json), a hash file (.hash), and an AssetBundle (.bundle) file. At this point, you need to upload these files to the bucket you specified above. This upload is done via the CCD command-line interface (CLI). See [Unity Cloud Content Delivery](https://docs.unity3d.com/Manual/UnityCCD.html).