# Custom operations
The [`IResourceProvider`](../api/UnityEngine.ResourceManagement.ResourceProviders.IResourceProvider.html) API allows you to extend the loading process by defining locations and dependencies in a data-driven manner. 

In some cases, you might want to create a custom operation. The [`IResourceProvider`](../api/UnityEngine.ResourceManagement.ResourceProviders.IResourceProvider.html) API is internally built on top of these custom operations.

### Creating custom operations
Create custom operations by deriving from the [`AsyncOperationBase`](../api/UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationBase-1.html) class and overriding the desired virtual methods. You can pass the derived operation to the [`ResourceManager.StartOperation`](../api/UnityEngine.ResourceManagement.ResourceManager.html#UnityEngine_ResourceManagement_ResourceManager_StartOperation__1_UnityEngine_ResourceManagement_AsyncOperations_AsyncOperationBase___0__UnityEngine_ResourceManagement_AsyncOperations_AsyncOperationHandle_) method to start the operation and receive an [`AsyncOperationHandle`](../api/UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle.html) struct. Operations started this way are registered with the [`ResourceManager`](../api/UnityEngine.ResourceManagement.ResourceManager.html)
 and appear in the [Addressables Event Viewer](MemoryManagement.md#the-addressables-event-viewer).

#### Executing the operation
The [`ResourceManager`](../api/UnityEngine.ResourceManagement.ResourceManager.html) invokes the [`AsyncOperationBase.Execute`](../api/UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationBase-1.html#UnityEngine_ResourceManagement_AsyncOperations_AsyncOperationBase_1_Execute) method for your custom operation once the optional dependent operation completes.

#### Completion handling
When your custom operation completes, call [`AsyncOperationBase.Complete`](../api/UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationBase-1.html#UnityEngine_ResourceManagement_AsyncOperations_AsyncOperationBase_1_Complete__0_System_Boolean_System_String_) on your custom operation object. You can call this within the [`Execute`](../api/UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationBase-1.html#UnityEngine_ResourceManagement_AsyncOperations_AsyncOperationBase_1_Execute) method or defer it to outside the call. Calling `AsyncOperationBase.Complete` notifies the [`ResourceManager`](../api/UnityEngine.ResourceManagement.ResourceManager.html) that the operation is complete and will invoke the associated [`AsyncOperationHandle.Completed`](../api/UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle.html?q=AsyncOperationHandle#UnityEngine_ResourceManagement_AsyncOperations_AsyncOperationHandle_Completed) events.

#### Terminating the operation
The [`ResourceManager`](../api/UnityEngine.ResourceManagement.ResourceManager.html) invokes [`AsyncOperationBase.Destroy`](../api/UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationBase-1.html#UnityEngine_ResourceManagement_AsyncOperations_AsyncOperationBase_1_Destroy) method for your custom operation when you release the [`AsyncOperationHandle`](../api/UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle.html) that references it. This is where you should release any memory or resources associated with your custom operation.
