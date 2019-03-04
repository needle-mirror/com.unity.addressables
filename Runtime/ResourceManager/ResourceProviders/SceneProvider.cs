using System;
using System.Collections.Generic;
using UnityEngine.Networking;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.Util;
using UnityEngine.SceneManagement;

namespace UnityEngine.ResourceManagement.ResourceProviders
{
    /// <summary>
    /// Provides Scene objects.
    /// </summary>
    public class SceneProvider : ISceneProvider
    {
        class InternalOp : AsyncOperationBase<Scene>
        {
            
#if UNITY_2018_3_OR_NEWER
            LoadSceneParameters m_LoadParameters;
#else
            LoadSceneMode m_LoadMode = LoadSceneMode.Single;
#endif
            Scene m_Scene;
            Action<IAsyncOperation<IList<object>>> m_Action;
            IAsyncOperation m_DependencyOperation;
            AsyncOperation m_RequestOperation;


            AsyncOperation InternalLoad(string path, bool loadingFromBundle)
            {
#if !UNITY_EDITOR
                return InternalPlayerLoad(path);
#else
                return loadingFromBundle ? InternalPlayerLoad(path) : InternalEditorLoad(path);
#endif
            }

            AsyncOperation InternalPlayerLoad(string path)
            {
#if UNITY_2018_3_OR_NEWER
                return SceneManager.LoadSceneAsync(path, m_LoadParameters);
#else
                return SceneManager.LoadSceneAsync(path, m_LoadMode);
#endif
            }

#if UNITY_EDITOR
            AsyncOperation InternalEditorLoad(string path)
            {
                AsyncOperation op = null;
#if UNITY_2018_3_OR_NEWER
                op = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(path, m_LoadParameters);
#else
                if (m_LoadMode == LoadSceneMode.Single)
                    op = UnityEditor.EditorApplication.LoadLevelAsyncInPlayMode((Context as IResourceLocation).InternalId);
                else
                    op = UnityEditor.EditorApplication.LoadLevelAdditiveAsyncInPlayMode((Context as IResourceLocation).InternalId);
#endif

#if !UNITY_2019_1_OR_NEWER
                DelayedActionManager.AddAction((Action<AsyncOperation>)CheckSceneLoaded, .1f, op);
#endif
                return op;
            }
#if !UNITY_2019_1_OR_NEWER
            void CheckSceneLoaded(AsyncOperation operation)
            {
                if (operation.isDone)
                    OnSceneLoaded(operation);
                else
                    DelayedActionManager.AddAction((Action<AsyncOperation>)CheckSceneLoaded, .1f, operation);
            }
#endif
#endif

            public InternalOp()
            {
                m_Action = op =>
                {
                    if ( (op == null || op.Status == AsyncOperationStatus.Succeeded) && Context is IResourceLocation)
                    {
                        bool loadingFromBundle = false;
                        if (op != null)
                        {
                            var bundle = AssetBundleProvider.LoadBundleFromDependecies(op.Result);
                            if (bundle != null)
                                loadingFromBundle = true;
                        }

                        m_RequestOperation = InternalLoad((Context as IResourceLocation).InternalId, loadingFromBundle);

                        if (m_RequestOperation != null)
                            m_Scene = SceneManager.GetSceneAt(SceneManager.sceneCount - 1);

                        if (m_RequestOperation == null || m_RequestOperation.isDone)
                            DelayedActionManager.AddAction((Action<AsyncOperation>)OnSceneLoaded, 0, m_RequestOperation);
                        else
                            m_RequestOperation.completed += OnSceneLoaded;
                    }
                    else
                    {
                        if(op != null)
                            m_Error = op.OperationException;
                        SetResult(default(Scene));
                        OnSceneLoaded(null);
                    }
                };
            }

            public override float PercentComplete
            {
                get
                {
                    if (IsDone)
                        return 1;

                    float reqPer = m_RequestOperation == null ? 0 : m_RequestOperation.progress;
                    if (m_DependencyOperation == null)
                        return reqPer;
                    return reqPer * .25f + m_DependencyOperation.PercentComplete * .75f;
                }
            }

            public IAsyncOperation<Scene> Start(IResourceLocation location, IAsyncOperation<IList<object>> loadDependencyOperation, LoadSceneMode loadMode)
            {
                Validate();
                m_RequestOperation = null;
                m_DependencyOperation = loadDependencyOperation;
                Context = location;
#if UNITY_2018_3_OR_NEWER
                m_LoadParameters = new LoadSceneParameters(loadMode);
#else
                m_LoadMode = loadMode;
#endif
                if (loadDependencyOperation == null)
                    m_Action(null);
                else
                    loadDependencyOperation.Completed += m_Action;
                return this;
            }

#if UNITY_2018_3_OR_NEWER
            public IAsyncOperation<Scene> Start(IResourceLocation location, IAsyncOperation<IList<object>> loadDependencyOperation, LoadSceneParameters loadParams)
            {
                Validate();
                m_RequestOperation = null;
                m_DependencyOperation = loadDependencyOperation;
                Context = location;
                m_LoadParameters = loadParams;
                if (loadDependencyOperation == null)
                    m_Action(null);
                else
                    loadDependencyOperation.Completed += m_Action;
                return this;
            }
#endif

            void OnSceneLoaded(AsyncOperation operation)
            {
                Validate();
                ResourceManagerEventCollector.PostEvent(ResourceManagerEventCollector.EventType.LoadSceneAsyncCompletion, Context, 1);
                ResourceManagerEventCollector.PostEvent(ResourceManagerEventCollector.EventType.CacheEntryLoadPercent, Context, 100);
                SetResult(m_Scene);
                InvokeCompletionEvent();
            }

            public override bool IsDone
            {
                get
                {
                    Validate();
                    return base.IsDone && Result.isLoaded;
                }
            }
        }
        /// <inheritdoc/>
        public IAsyncOperation<Scene> ProvideSceneAsync(IResourceLocation location, IAsyncOperation<IList<object>> loadDependencyOperation, LoadSceneMode loadMode)
        {
            if (location == null)
                throw new ArgumentNullException("location");
            return AsyncOperationCache.Instance.Acquire<InternalOp>().Start(location, loadDependencyOperation, loadMode);
        }

#if UNITY_2018_3_OR_NEWER
        /// <inheritdoc/>
        public IAsyncOperation<Scene> ProvideSceneAsync(IResourceLocation location, IAsyncOperation<IList<object>> loadDependencyOperation, LoadSceneParameters loadParams)
        {
            if (location == null)
                throw new ArgumentNullException("location");
            return AsyncOperationCache.Instance.Acquire<InternalOp>().Start(location, loadDependencyOperation, loadParams);
        }
#endif

        class InternalReleaseOp : AsyncOperationBase<Scene>
        {
            Scene m_Scene;
            public IAsyncOperation<Scene> Start(IResourceLocation location, Scene scene)
            {
                Validate();
                m_Scene = scene;
                Context = location;
                ResourceManagerEventCollector.PostEvent(ResourceManagerEventCollector.EventType.ReleaseSceneAsyncRequest, Context, 0);
                var unloadOp = SceneManager.UnloadSceneAsync(scene);
                if (unloadOp.isDone)
                    DelayedActionManager.AddAction((Action<AsyncOperation>)OnSceneUnloaded, 0, unloadOp);
                else
                    unloadOp.completed += OnSceneUnloaded;
                return this;
            }

            void OnSceneUnloaded(AsyncOperation operation)
            {
                Validate();
                ResourceManagerEventCollector.PostEvent(ResourceManagerEventCollector.EventType.ReleaseSceneAsyncCompletion, Context, 0);
                ResourceManagerEventCollector.PostEvent(ResourceManagerEventCollector.EventType.CacheEntryLoadPercent, Context, 0);
                SetResult(m_Scene);
                InvokeCompletionEvent();
            }

            public override bool IsDone
            {
                get
                {
                    Validate();
                    return base.IsDone && !Result.isLoaded;
                }
            }
        }

        /// <inheritdoc/>
        public IAsyncOperation<Scene> ReleaseSceneAsync(IResourceLocation location, Scene scene)
        {
            return AsyncOperationCache.Instance.Acquire<InternalReleaseOp>().Start(location, scene);
        }
    }
}
