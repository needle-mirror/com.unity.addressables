#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net.Mail;
using UnityEditor;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.Util;

namespace UnityEngine.ResourceManagement.ResourceProviders
{
    /// <summary>
    /// Provides assets loaded via the AssetDatabase API.  This provider is only available in the editor and is used for fast iteration or to simulate asset bundles when in play mode.
    /// </summary>
    [DisplayName("Assets from AssetDatabase Provider")]
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

        class InternalOp
        {
            ProvideHandle m_ProvideHandle;
            public void Start(ProvideHandle provideHandle, float loadDelay)
            {
                m_ProvideHandle = provideHandle;
                if (loadDelay < 0)
                    LoadImmediate();
                else
                    DelayedActionManager.AddAction((Action)LoadImmediate, loadDelay);
            }
            
            void LoadImmediate()
            { 
                string assetPath = m_ProvideHandle.ResourceManager.TransformInternalId(m_ProvideHandle.Location);
                object result = null;
                if (m_ProvideHandle.Type.IsArray)
                {
                    result = ResourceManagerConfig.CreateArrayResult(m_ProvideHandle.Type, AssetDatabase.LoadAllAssetsAtPath(assetPath));
                }
                else if (m_ProvideHandle.Type.IsGenericType && typeof(IList<>) == m_ProvideHandle.Type.GetGenericTypeDefinition())
                {
                    result = ResourceManagerConfig.CreateListResult(m_ProvideHandle.Type, AssetDatabase.LoadAllAssetsAtPath(assetPath));
                }
                else
                {
                    var i = assetPath.LastIndexOf('[');
                    if (i > 0)
                    {
                        var i2 = assetPath.LastIndexOf(']');
                        if (i2 < i)
                        {
                            m_ProvideHandle.Complete(result, false, new Exception(string.Format("Invalid index format in internal id {0}", assetPath)));
                        }
                        else
                        {
                            var subObjectName = assetPath.Substring(i + 1, i2 - (i + 1));
                            assetPath = assetPath.Substring(0, i);
                            var objs = AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath);
                            foreach (var o in objs)
                            {
                                if (o.name == subObjectName)
                                {
                                    if (m_ProvideHandle.Type.IsAssignableFrom(o.GetType()))
                                    {
                                        result = o;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        var obj = AssetDatabase.LoadAssetAtPath(assetPath, m_ProvideHandle.Location.ResourceType);
                        result = obj != null && m_ProvideHandle.Type.IsAssignableFrom(obj.GetType()) ? obj : null;
                    }
                }
                m_ProvideHandle.Complete(result, result != null, null);
            }
        }


        /// <inheritdoc/>
        public override bool CanProvide(Type t, IResourceLocation location)
        {
            if (t.IsArray)
                t = t.GetElementType();
            else if (t.IsGenericType && typeof(IList<>) == t.GetGenericTypeDefinition())
                t = t.GetGenericArguments()[0];

            return t == typeof(object) || typeof(Object).IsAssignableFrom(t);
        }

        public override void Provide(ProvideHandle provideHandle)
        {
            new InternalOp().Start(provideHandle, m_LoadDelay);
        }
    }
}
#endif
