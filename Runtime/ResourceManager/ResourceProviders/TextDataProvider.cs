using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using UnityEngine.Networking;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.Util;

namespace UnityEngine.ResourceManagement.ResourceProviders
{
    /// <summary>
    /// Provides raw text from a local or remote URL.
    /// </summary>
    [DisplayName("Text Data Provider")]
    public class TextDataProvider : ResourceProviderBase
    {
        /// <summary>
        /// Controls whether errors are logged - this is disabled when trying to load from the local cache since failures are expected
        /// </summary>
        public bool IgnoreFailures { get; set; }

        class InternalOp
        {
            TextDataProvider m_Provider;
            UnityWebRequestAsyncOperation m_RequestOperation;
            WebRequestQueueOperation m_RequestQueueOperation;
            bool m_IgnoreFailures;
            ProvideHandle m_PI;

            private float GetPercentComplete() { return m_RequestOperation != null ? m_RequestOperation.progress : 0.0f; }

            public void Start(ProvideHandle provideHandle, TextDataProvider rawProvider, bool ignoreFailures)
            {
                m_PI = provideHandle;
                provideHandle.SetProgressCallback(GetPercentComplete);
                m_Provider = rawProvider;
                m_IgnoreFailures = ignoreFailures;
                var path = m_PI.ResourceManager.TransformInternalId(m_PI.Location);
                if (File.Exists(path))
                {
#if NET_4_6
                    if(path.Length >= 260)
                        path =  @"\\?\" + path;
#endif
                    var text = File.ReadAllText(path);
                    object result = m_Provider.Convert(m_PI.Type, text);
                    m_PI.Complete(result, result != null, result == null ? new Exception($"Unable to load asset of type {m_PI.Type} from location {m_PI.Location}.") : null);
                }
                else if (ResourceManagerConfig.ShouldPathUseWebRequest(path))
                {
                    UnityWebRequest request = new UnityWebRequest(path, UnityWebRequest.kHttpVerbGET, new DownloadHandlerBuffer(), null);
                    m_RequestQueueOperation = WebRequestQueue.QueueRequest(request);
                    if (m_RequestQueueOperation.IsDone)
                    {
                        m_RequestOperation = m_RequestQueueOperation.Result;
                        if(m_RequestOperation.isDone)
                            RequestOperation_completed(m_RequestOperation);
                        else
                            m_RequestOperation.completed += RequestOperation_completed;
                    }
                    else
                    {
                        m_RequestQueueOperation.OnComplete += asyncOperation =>
                        {
                            m_RequestOperation = asyncOperation;
                            m_RequestOperation.completed += RequestOperation_completed;
                        };
                    }
                }
                else
                {
                    Exception exception = null;
                    //Don't log errors when loading from the persistentDataPath since these files are expected to not exist until created
                    if (!m_IgnoreFailures)
                    {
                        exception = new Exception(string.Format("Invalid path in " + nameof(TextDataProvider) + " : '{0}'.", path));
                    }
                    m_PI.Complete<object>(null, m_IgnoreFailures, exception);
                }
            }

            private void RequestOperation_completed(AsyncOperation op)
            {
                var webOp = op as UnityWebRequestAsyncOperation;
                object result = null;
                Exception exception = null;
                if (webOp != null)
                {
                    var webReq = webOp.webRequest;
                    if (string.IsNullOrEmpty(webReq.error))
                        result = m_Provider.Convert(m_PI.Type, webReq.downloadHandler.text);
                    else
                        exception = new Exception(string.Format(nameof(TextDataProvider) + " unable to load from url {0}, result='{1}'.", webReq.url, webReq.error));
                }
                else
                {
                    exception = new Exception(nameof(TextDataProvider) + " unable to load from unknown url.");
                }

                m_PI.Complete(result, result != null || m_IgnoreFailures, exception);
            }
        }

        /// <summary>
        /// Method to convert the text into the object type requested.  Usually the text contains a JSON formatted serialized object.
        /// </summary>
        /// <typeparam name="TObject">The object type to convert the text to.</typeparam>
        /// <param name="text">The text to be converted.</param>
        /// <returns>The converted object.</returns>
        public virtual object Convert(Type type, string text) { return text; }

        /// <summary>
        /// Provides raw text data from the location.
        /// </summary>
        /// <typeparam name="TObject">Object type.</typeparam>
        /// <param name="location">Location of the data to load.</param>
        /// <param name="loadDependencyOperation">Depency operation.</param>
        /// <returns>Operation to load the raw data.</returns>
        public override void Provide(ProvideHandle provideHandle)
        {
            new InternalOp().Start(provideHandle, this, IgnoreFailures);
        }
    }
}