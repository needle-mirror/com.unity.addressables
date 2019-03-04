using System;
using System.Collections.Generic;
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
    public abstract class RawDataProvider : ResourceProviderBase
    {
        /// <summary>
        /// Controls whether errors are logged - this is disabled when trying to load from the local cache since failures are expected
        /// </summary>
        public bool IgnoreFailures { get; set; }

        class InternalOp<TObject> : InternalProviderOperation<TObject> where TObject : class
        {
            Func<string, TObject> m_ConvertFunc;
            IAsyncOperation m_DependencyOperation;
            UnityWebRequestAsyncOperation m_RequestOperation;
            bool m_IgnoreFailures;

            public override float PercentComplete
            {
                get
                {
                    if (IsDone)
                        return 1;
                    return m_RequestOperation != null ? m_RequestOperation.progress : 0.0f;
                }
            }
            public InternalProviderOperation<TObject> Start(IResourceLocation location, IList<object> deps, Func<string, TObject> convertFunc, bool ignoreFailures)
            {
                m_Result = null;
                m_IgnoreFailures = ignoreFailures;
                m_ConvertFunc = convertFunc;
                Context = location;
                base.Start(location);
                var path = location.InternalId;
                if (File.Exists(path))
                {
#if NET_4_6
                    if(path.Length >= 260)
                        path =  @"\\?\" + path;
#endif
                    var text = File.ReadAllText(path);
                    SetResult(m_ConvertFunc(text));
                    DelayedActionManager.AddAction((Action)OnComplete);
                }
                else if (path.Contains("://"))
                {
                    m_RequestOperation = new UnityWebRequest(path, UnityWebRequest.kHttpVerbGET, new DownloadHandlerBuffer(), null).SendWebRequest();
                    if (m_RequestOperation.isDone)
                        DelayedActionManager.AddAction((Action<AsyncOperation>)OnComplete, 0, m_RequestOperation);
                    else
                        m_RequestOperation.completed += OnComplete;
                }
                else
                {
                    //Don't log errors when loading from the persistentDataPath since these files are expected to not exist until created
                    if (!m_IgnoreFailures)
                        OperationException = new Exception(string.Format("Invalid path in RawDataProvider: '{0}'.", path));
                    SetResult(default(TObject));
                    OnComplete();
                }

                return this;
            }

            internal override TObject ConvertResult(AsyncOperation op)
            {
                var webOp = op as UnityWebRequestAsyncOperation;
                if (webOp != null)
                {
                    var webReq = webOp.webRequest;
                    if (string.IsNullOrEmpty(webReq.error))
                        return m_ConvertFunc(webReq.downloadHandler.text);
                    
                    if (!m_IgnoreFailures)
                        OperationException = new Exception(string.Format("RawDataProvider unable to load from url {0}, result='{1}'.", webReq.url, webReq.error));
                }
                else
                {
                    if (!m_IgnoreFailures)
                        OperationException = new Exception("RawDataProvider unable to load from unknown url.");
                }
                return default(TObject);

            }
        }

        /// <summary>
        /// Method to convert the text into the object type requested.  Usually the text contains a JSON formatted serialized object.
        /// </summary>
        /// <typeparam name="TObject">The object type to convert the text to.</typeparam>
        /// <param name="text">The text to be converted.</param>
        /// <returns>The converted object.</returns>
        public abstract TObject Convert<TObject>(string text) where TObject : class;

        /// <summary>
        /// If true, the data is loaded as text for the handler
        /// </summary>
        public virtual bool LoadAsText { get { return true; } }

        /// <summary>
        /// Provides raw text data from the location.
        /// </summary>
        /// <typeparam name="TObject">Object type.</typeparam>
        /// <param name="location">Location of the data to load.</param>
        /// <param name="loadDependencyOperation">Depency operation.</param>
        /// <returns>Operation to load the raw data.</returns>
        public override IAsyncOperation<TObject> Provide<TObject>(IResourceLocation location, IList<object> deps)
        {
            if (location == null)
                throw new ArgumentNullException("location");
            var operation = AsyncOperationCache.Instance.Acquire<InternalOp<TObject>>();
            return operation.Start(location, deps, Convert<TObject>, IgnoreFailures);
        }
    }
}