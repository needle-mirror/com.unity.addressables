using System.Collections.Generic;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace AddressableAssets.DocExampleCode
{
    #region doc_Preload

    using System.Collections;
    using UnityEngine;
    using UnityEngine.AddressableAssets;
    using UnityEngine.Events;
    using UnityEngine.ResourceManagement.AsyncOperations;
    using UnityEngine.ResourceManagement.ResourceProviders;

    internal class PreloadWithProgress : MonoBehaviour
    {
        public string preloadLabel = "preload";
        public UnityEvent<float> ProgressEvent;
        public UnityEvent<bool> CompletionEvent;
        private AsyncOperationHandle downloadHandle;

        IEnumerator Start()
        {
            downloadHandle = Addressables.DownloadDependenciesAsync(preloadLabel, false);
            float progress = 0;

            while (downloadHandle.Status == AsyncOperationStatus.None)
            {
                float percentageComplete = downloadHandle.GetDownloadStatus().Percent;
                if (percentageComplete > progress * 1.1) // Report at most every 10% or so
                {
                    progress = percentageComplete; // More accurate %
                    ProgressEvent.Invoke(progress);
                }

                yield return null;
            }

            CompletionEvent.Invoke(downloadHandle.Status == AsyncOperationStatus.Succeeded);
            downloadHandle.Release(); //Release the operation handle
        }
    }

    #endregion

    internal class PreloadExamples
    {
        string key;

        void example()
        {
            #region doc_DownloadSize

            AsyncOperationHandle<long> getDownloadSize =
                Addressables.GetDownloadSizeAsync(key);

            #endregion
        }

        IEnumerator TotalExample()
        {
            #region doc_DownloadSizeTotal

            // Everything in every loaded catalog that reports a download size, without
            // resolving a single key.
            var allDownloads = new List<IResourceLocation>();
            foreach (var locator in Addressables.ResourceLocators)
            {
                foreach (var location in locator.AllLocations)
                {
                    if (location.Data is ILocationSizeData)
                        allDownloads.Add(location);
                }
            }

            AsyncOperationHandle<long> getTotalDownloadSize =
                Addressables.GetDownloadSizeAsync(allDownloads);
            yield return getTotalDownloadSize;

            if (getTotalDownloadSize.Status == AsyncOperationStatus.Succeeded)
                Debug.Log($"Total download size: {getTotalDownloadSize.Result}");

            getTotalDownloadSize.Release(); //Release the operation handle

            #endregion
        }
    }
}
