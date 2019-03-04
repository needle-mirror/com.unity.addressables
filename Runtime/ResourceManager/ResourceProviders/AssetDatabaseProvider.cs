#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.Util;

namespace UnityEngine.ResourceManagement.ResourceProviders
{
    /// <summary>
    /// Provides assets loaded via the AssetDatabase API.  This provider is only available in the editor and is used for fast iteration or to simulate asset bundles when in play mode.
    /// </summary>
    public class AssetDatabaseProvider : ResourceProviderBase
    {
        float m_LoadDelay = .1f;

        /// <summary>
        /// Default constructor.
        /// </summary>
        public AssetDatabaseProvider() { }

        /// <summary>
        /// Constructor that allows for a sepcified delay for all requests.
        /// </summary>
        /// <param name="delay">Time in seconds for each delay call.</param>
        public AssetDatabaseProvider(float delay = .25f)
        {
            m_LoadDelay = delay;
        }

        class InternalOp<TObject> : InternalProviderOperation<TObject>
            where TObject : class
        {
            public InternalProviderOperation<TObject> Start(IResourceLocation location, float loadDelay)
            {
                m_Result = null;
                Context = location;
                DelayedActionManager.AddAction((Action)CompleteLoad, loadDelay);
                return base.Start(location);
            }

            void CompleteLoad()
            { 
                var location = Context as IResourceLocation;
                var assetPath = location == null ? string.Empty : location.InternalId;
                var t = typeof(TObject);
                if (t.IsArray)
                    SetResult(ResourceManagerConfig.CreateArrayResult<TObject>(AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath)));
                else if (t.IsGenericType && typeof(IList<>) == t.GetGenericTypeDefinition())
                    SetResult(ResourceManagerConfig.CreateListResult<TObject>(AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath)));
                else
                {
                    var mainType = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
                    if (mainType == typeof(Texture2D) && typeof(TObject) == typeof(Sprite))
                    {
                        SetResult(AssetDatabase.LoadAssetAtPath(assetPath, typeof(TObject)) as TObject);
                    }
                    else
                        SetResult(AssetDatabase.LoadAssetAtPath(assetPath, mainType) as TObject);
                }
                OnComplete();
            }

            internal override TObject ConvertResult(AsyncOperation op) { return null; }
        }


        /// <inheritdoc/>
        public override bool CanProvide<TObject>(IResourceLocation location)
        {
            if (!base.CanProvide<TObject>(location))
                return false;
            var t = typeof(TObject);

            if (t.IsArray)
                t = t.GetElementType();
            else if (t.IsGenericType && typeof(IList<>) == t.GetGenericTypeDefinition())
                t = t.GetGenericArguments()[0];
            
            
            return t == typeof(object) || typeof(Object).IsAssignableFrom(t);
        }

        /// <inheritdoc/>
        public override IAsyncOperation<TObject> Provide<TObject>(IResourceLocation location, IList<object> deps)
        {
            if (location == null)
                throw new ArgumentNullException("location");
            return AsyncOperationCache.Instance.Acquire<InternalOp<TObject>>().Start(location, m_LoadDelay);
        }

        /// <inheritdoc/>
        public override bool Release(IResourceLocation location, object asset)
        {
            if (location == null)
                throw new ArgumentNullException("location");
            var obj = asset as Object;

            if (obj != null)
                return true;

            return false;
        }
    }
}
#endif
