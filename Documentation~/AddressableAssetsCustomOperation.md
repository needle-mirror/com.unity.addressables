# Custom operations
The [`IResourceProvider`](../api/UnityEngine.ResourceManagement.ResourceProviders.IResourceProvider.html) API allows you to extend the loading process by defining locations and dependencies in a data-driven manner. 

In some cases, you might want to create a custom operation. The `IResourceProvider` API is internally built on top of these custom operations.

### Creating custom operations
Create custom operations by deriving from the [`AsyncOperationBase`](../api/UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationBase-1.html) class and overriding the desired virtual methods. You can pass the derived operation to the `ResourceManager.StartOperation` method to start the operation and receive an `AsyncOperationHandle` struct. Operations started this way are registered with the `ResourceManager` and appear in the [Addressables Profiler](MemoryManagement.md#the-addressable-profiler).

#### Executing the operation
The `ResourceManager` invokes the `AsyncOperationBase.Execute` method for your custom operation once the optional dependent operation completes.

#### Completion handling
When your custom operation completes, call `AsyncOperationBase.Complete` on your custom operation object. You can call this within the `Execute` method or defer it to outside the call. Calling `AsyncOperationBase.Complete` notifies the `ResourceManager` that the operation is complete and will invoke the associated `AsyncOperationHandle.Completed` events.

#### Terminating the operation
The `ResourceManager` invokes `AsyncOperationBase.Destroy` method for your custom operation when you release the `AsyncOperationHandle` that references it. This is where you should release any memory or resources associated with your custom operation.
