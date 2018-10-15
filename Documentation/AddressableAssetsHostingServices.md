## Hosting Services
### Overview
Hosting Services provides an integrated facility for serving packed content to local or network connected Unity player builds, based on the Addressable Assets configuration data. Hosting Services are designed to improve iteration velocity when testing packed content, and can also be used to serve content to connected clients on local and remote networks.
#### Packed Mode Testing and Iteration
Moving from Editor playmode testing to platform Player build testing introduces new complexities and time costs to the development process. Hosting Services provides extensible Editor-embedded content delivery services that map directly to the Addressables group configuration. Using a custom Addressables Profile, your application can be quickly configured to load all content from the Unity Editor itself. This includes Unity Player builds deployed to mobile devices or any other platform with network access to your development system.
#### Turn-key Content Server
In addition to accelerating Packed Mode content iteration, you can deploy Hosting Services into a server environment by running in batch mode (headless) to host content for both intranet and internet facing Unity Player clients.
### Setup

This guide will walk you through the initial setup of Hosting Services for your project. Note that while this guide primarly focuses on editor workflows, Hosting Services can also be configured via API through the `HostingServicesManager` property of the `AddressableAssetSettings` class.

#### Create and Configure a new Hosting Service
Use the Hosting Services window to add, configure, and enable new Hosting Services, and can be accessed from the Window menu in the toolbar (`Window/Asset Management/Hosting Services`) or from the `Hosting` button in the main Addressables window.

![Hosting Services Window](images/HostingServicesWindow_1.png)

To add a new Hosting Service, click the `Add Service...` button.

![Add Service Window](images/HostingServicesAddService_1.png)

Select the Service Type you wish to add, provide a Descriptive Name and press the `Add` button. In this example, we'll add an HttpHostingService class. For more details on implementing custom hosting service types, see [Custom Services](#custom-services).

After adding the service, it will appear in the Hosting Services section, and is initially in the _disabled_ state. Press `Enable Service` to start the service.

![Hosting Services Window](images/HostingServicesWindow_2.png)

The HttpHostingService automatically assigns a free port number when it is started. This port number is saved and reused between Unity sessions. To choose a new port, either assign a specific port number in the `Port` field, or use the `Reset` button to randomly select a new free port. Note: if the port number is reset, a new full player build is required to generate and embed the correct URL.

The HttpHostingService is now enabled and ready to serve content from the directory specified in the `BuildPath` of each Asset Group.  The next section will discuss using Profiles to configure Asset Groups to load content from the newly configured hosting service.

#### Profile Setup

The recommended pattern for working with Hosting Services during development is to create a profile that configures all Asset Groups to load content from the hosting service from a directory or directories created specifically for that purpose.

From the Addressables window, select `Inspect Profile Settings` from the Profiles drop-down, or find and select the *AddressableAssetSettings* object for inspection.

Next, create a new Profile. In this example it is called "Editor Hosted".

![Create Profile](images/HostingServicesProfiles_1.png)

Now modify all variables used for loading paths (such as `LocalLoadPath` and `RemoteLoadPath` in this example) to instead load from the hosting service. With HttpHostingService, this is a URL that uses the local IP address and the port assigned to the service. From the Hosting Services window, you can use the Profile Variables named `PrivateIPAddress` and `HostingServicePort` to construct the url. e.g. `http://[PrivateIPAddress]:[HostingServicePort]`

Additionally, modify all variables used for build paths (such as `LocalBuildPath` and `RemoteBuildPath`) to point to a common directory outside of the project `Assets` folder.

![Edit Profile](images/HostingServicesProfiles_2.png)

Verify that each group is configured correctly. Ensure that the `BuildPath` and `LoadPath` are set to their respective profile keys modifed for use with Hosting Services. In this example you can see how the profile variables within the `LoadPath` are expanded to build a correct base URL for loading from Hosted Services.

![Inspect Group](images/HostingServicesGroups_1.png)

Finally, select the new profile from the Addressables window, create a build, and deploy to the target device. The Unity Editor now serves all load requests from the player through the HttpHostingService. You can now make additions and changes to content without redeployment - rebuild the Addressable content, and relaunch the already deployed player to refresh the content.

![Select Profile](images/HostingServicesProfiles_3.png)

### Batch Mode

You can also use Hosting Services to serve content from the Unity Editor running in batch mode.

Launch Unity from the command line with the following options:
`-batchMode -executeMethod UnityEditor.AddressableAssets.HostingServicesManager.BatchMode`

This loads the Hosting Services configuration from the default AddressableAssetSettings object, and start all configured services.

To use an alternative AddressableAssetSettings configuration, create your own static method entry point, and call through to `UnityEditor.AddressableAssets.HostingServicesManager.BatchMode(AddressableAssetSettings settings)` overload.

### Custom Services

Hosting Services is designed to be easily extensible, allowing you to implement your own custom logic for serving content loading requests from the Addressables system. Example use cases:
* Support a custom `IResourceProvider` that uses a non-HTTP protocol for downloading content.
* Manage an external process for serving content that matches your production CDN solution - e.g. Apache HTTP server.

#### Implement IHostingService

The HostingServicesManager can manage any class that implements `IHostingService`. See the [API documentation](../api/UnityEditor.AddressableAssets.IHostingService.html) for for details on method parameters and return values.

#### Add a Custom Service

From the `Add Service` dialog, select `Custom` and drag-and-drop the script into place, or select from the object picker. The dialog verifies that the selected script implements the `IHostingService` interface. To finish adding the service, click the `Add` button. In the future, your custom implementation will show up in the `Service Type` drop-down menu.

![Add Custom Service](images/HostingServicesAddService_2.png)
