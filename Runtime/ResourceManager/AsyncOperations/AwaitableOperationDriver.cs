using System;
using System.Threading;

namespace UnityEngine.ResourceManagement.AsyncOperations
{
    /// <summary>
    /// Shared driver for both <c>ToAwaitable(CancellationToken)</c> overloads on <see cref="AsyncOperationHandle"/>,
    /// since they wrap the same underlying operation and would otherwise duplicate this logic.
    /// </summary>
    internal static class AwaitableOperationDriver
    {
        /// <summary>
        /// Drives an awaitable-completion source from the handle's completion and the token's cancellation.
        /// </summary>
        /// <remarks>
        /// Releases the caller's reference to <paramref name="handle"/> exactly once, on whichever of
        /// {failure, cancellation} fires first, unless a release-on-completion listener already owns that job.
        /// Cancellation registrations are never disposed early, so a long-lived token can still release the
        /// handle after a successful completion.
        /// </remarks>
        /// <param name="handle">The operation handle to await and (conditionally) release.</param>
        /// <param name="cancellationToken">Token that cancels the awaitable.</param>
        /// <param name="completeSource">Sets the caller's completion source's result or exception from a handle.</param>
        /// <param name="setCanceled">Cancels the caller's completion source.</param>
        public static void Drive(
            AsyncOperationHandle handle,
            CancellationToken cancellationToken,
            Action<AsyncOperationHandle> completeSource,
            Action setCanceled)
        {
            // Already canceled: skip the fast path/Completed machinery entirely.
            if (cancellationToken.IsCancellationRequested)
            {
                ReleaseCallerRef(handle);
                setCanceled();
                return;
            }

            // Guards so the caller's reference is released at most once across {failure, cancellation}.
            // Without it, cancelling after a failure would release the reference the failure exception holds.
            int callerReleased = 0;

            void ReleaseCallerRefOnce()
            {
                if (Interlocked.Exchange(ref callerReleased, 1) == 0)
                    ReleaseCallerRef(handle);
            }

            // Fast path: already done and nothing else is listening. Still register for a later cancellation.
            if (handle.IsDone && (!handle.IsValid() || !handle.CompletedEventHasListeners))
            {
                completeSource(handle);
                if (handle.Status == AsyncOperationStatus.Failed)
                    ReleaseCallerRefOnce();
                if (cancellationToken.CanBeCanceled)
                    cancellationToken.Register(ReleaseCallerRefOnce);
                return;
            }

            // Pending path: acquire our own reference in case an autoReleaseHandle listener runs first
            // and invalidates the handle before we read it.
            var acquired = handle.Acquire();

            // Guards so exactly one of {completion, cancellation} resolves the source and releases `acquired`.
            int resolved = 0;

            handle.Completed += _ =>
            {
                if (Interlocked.Exchange(ref resolved, 1) == 0)
                {
                    // completeSource acquires its own reference for a failure exception, so `acquired`
                    // is always ours alone to release.
                    try
                    {
                        completeSource(acquired);
                    }
                    finally
                    {
                        acquired.Release();
                    }

                    if (handle.Status == AsyncOperationStatus.Failed)
                        ReleaseCallerRefOnce();
                }
            };

            if (cancellationToken.CanBeCanceled)
            {
                cancellationToken.Register(() =>
                {
                    if (Interlocked.Exchange(ref resolved, 1) == 0)
                    {
                        setCanceled();
                        acquired.Release();
                    }

                    ReleaseCallerRefOnce();
                });
            }
        }

        /// <summary>
        /// Releases the caller's reference to <paramref name="handle"/>, unless something else already owns that release.
        /// </summary>
        static void ReleaseCallerRef(AsyncOperationHandle handle)
        {
            if (handle.IsValid() && !handle.HasReleaseOnCompletionRegistered)
                handle.Release();
        }
    }
}
