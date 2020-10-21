---
uid: addressables-api-exception-handler
---
# ResourceManager.ExceptionHandler
#### API
- `static Action<AsyncOperationHandle, Exception> ExceptionHandler`

#### Description
The `ResourceManager.ExceptionHandler` allows you to create and set custom exception handlers for the `Addressables` runtime.  If no `ExceptionHandler` is provided, a default implementation is used.

Exceptions created during runtime error scenarios do not get automatically thrown.  They are sent to the `ExceptionHandler` and reported in the `AsyncOperationHandle.OperationException` of any given operation.

#### Code Sample
```
void Start()
{
    ResourceManager.ExceptionHandler = CustomExceptionHandler;
    
    //...
}

//Gets called for every error scenario encountered during an operation.
//A common use case for this is having InvalidKeyExceptions fail silently when a location is missing for a given key.
void CustomExceptionHandler(AsyncOperationHandle handle, Exception exception)
{
    if (exception.GetType() != typeof(InvalidKeyException))
            Addressables.LogException(handle, exception);
}
```