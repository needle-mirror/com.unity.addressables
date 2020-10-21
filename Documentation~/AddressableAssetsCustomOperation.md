---
uid: addressables-custom-operations
---
# Custom operations
The [`IResourceProvider`](xref:UnityEngine.ResourceManagement.ResourceProviders.IResourceProvider) API allows you to extend the loading process by defining locations and dependencies in a data-driven manner. 

In some cases, you might want to create a custom operation. The [`IResourceProvider`](xref:UnityEngine.ResourceManagement.ResourceProviders.IResourceProvider) API is internally built on top of these custom operations.

### Creating custom operations
Create custom operations by deriving from the [`AsyncOperationBase`](xref:UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationBase`1) class and overriding the desired virtual methods. You can pass the derived operation to the [`ResourceManager.StartOperation`](xref:UnityEngine.ResourceManagement.ResourceManager.StartOperation``1(UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationBase{``0},UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle)) method to start the operation and receive an [`AsyncOperationHandle`](xref:UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle) struct. Operations started this way are registered with the [`ResourceManager`](xref:UnityEngine.ResourceManagement.ResourceManager)
 and appear in the [Addressables Event Viewer](MemoryManagement.md#the-addressables-event-viewer).

#### Executing the operation
The [`ResourceManager`](xref:UnityEngine.ResourceManagement.ResourceManager) invokes the [`AsyncOperationBase.Execute`](xref:UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationBase`1.Execute) method for your custom operation once the optional dependent operation completes.

#### Completion handling
When your custom operation completes, call [`AsyncOperationBase.Complete`](xref:UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationBase`1.Complete(`0,System.Boolean,System.String)) on your custom operation object. You can call this within the [`Execute`](xref:UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationBase`1.Execute) method or defer it to outside the call. Calling `AsyncOperationBase.Complete` notifies the [`ResourceManager`](xref:UnityEngine.ResourceManagement.ResourceManager) that the operation is complete and will invoke the associated [`AsyncOperationHandle.Completed`](xref:UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle.Completed) events.

#### Terminating the operation
The [`ResourceManager`](xref:UnityEngine.ResourceManagement.ResourceManager) invokes [`AsyncOperationBase.Destroy`](xref:UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationBase`1.Destroy) method for your custom operation when you release the [`AsyncOperationHandle`](xref:UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle) that references it. This is where you should release any memory or resources associated with your custom operation.
