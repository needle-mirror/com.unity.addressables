## Synchronous Workflow
Synchronous Addressables APIs help to more closely mirror Unity asset loading workflows.  `AsyncOperationHandles` now have a method called `WaitForCompletion()` that force the async operation to complete and return the `Result` of the operation.

## API
`TObject WaitForCompletion()`

## Result
The result of `WaitForCompletion` is the `Result` of the async operation it is called on.  If the operation fails, this returns `default(TObject)`.

It is possible to get a `default(TObject)` for a result when the operation doesn't fail.  Async operations that auto release their `AsyncOperationHandles` on completion are such cases.  `Addressables.InitializeAsync()` and any API with a `autoReleaseHandle` parameter set to true will return `default(TObject)` even though the operations themselves succeeded.

## Performance
It is worth noting that calling `WaitForCompletion` may have performance implications on your runtime when compared to `Resources.Load` or `Instantiate` calls directly.  If your `AssetBundle` is local or has been previously downloaded and cached, these performance hits are likely to be negligible.  However, this may not be the case for your individual project setup.

All currently active Asset Load operations are completed when `WaitForCompletion` is called on any Asset Load operation, due to how Async operations are handled in the Engine. To avoid unexpected stalls, use `WaitForCompletion` when the current operation count is known, and the intention is for all active operations to complete synchronously.

When using `WaitForCompletion`, there are performance implications. When using 2021.2.0 or newer, these are minimal. Using an older version can result in delays that scale with the number of Engine Asset load calls that are loading when `WaitForCompletion` is called.

It is not recommended that you call `WaitForCompletion` on an operation that is going to fetch and download a remote `AssetBundle`.  Though, it is possible if that fits your specific situation. 

## Code Sample
```
void Start()
{
    //Basic use case of forcing a synchronous load of a GameObject
    var op = Addressables.LoadAssetAsync<GameObject>("myGameObjectKey");
    GameObject go = op.WaitForCompletion();
    
    //Do work...
    
    Addressables.Release(op);
}
```
### Synchronous Addressables with Custom Operations
Addressables supports custom `AsyncOperations` which support unique implementations of `InvokeWaitForCompletion`.  This overridable method is what you'll use to implement custom synchronous operations.

Custom operations work with `ChainOperations` and `GroupsOperations`.  If you require chained operations to be completed synchronously, ensure that your custom operations implement `InvokeWaitForCompletion` and create a `ChainOperation` using your custom operations.  Similarly, `GroupOperations` are well suited to ensure a collection of `AsyncOperations`, including custom operations, complete together.  Both `ChainOperation` and `GroupOperation` have their own implementations of `InvokeWaitForCompletion` that relies on the `InvokeWaitForCompletion` implementations of the operations they depend on.