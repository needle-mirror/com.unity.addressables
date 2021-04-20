#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.Util;
using UnityEngine.SceneManagement;

namespace UnityEngine.ResourceManagement.ResourceProviders
{
    /// <summary>
    /// Provides assets loaded via the AssetDatabase API.  This provider is only available in the editor and is used for fast iteration or to simulate asset bundles when in play mode.
    /// </summary>
    [DisplayName("Assets from AssetDatabase Provider")]
    public class AssetDatabaseProvider : ResourceProviderBase
    {
        private class AssetInstance
        {
            public Object Instance;
            public int LoadCount;
        }
        
        float m_LoadDelay = .1f;

        private static Dictionary<Object, AssetInstance> InstanceCache = new Dictionary<Object, AssetInstance>();
        private static Dictionary<int, Object> InstanceIDToAsset = new Dictionary<int, Object>();
        private static Type GameObjectType = typeof(GameObject);
        private static Type ScriptableObjectType = typeof(ScriptableObject);
        private static Scene _PreviewScene;
        private static Scene PreviewScene
        {
            get
            {
                if (!_PreviewScene.IsValid())
                {
                    _PreviewScene = EditorSceneManager.NewPreviewScene();
                    _PreviewScene.name = "InstancePreviewScene";
                    
                    EditorApplication.playModeStateChanged += PlayModeState;
                    if (EditorPrefs.GetInt("ScriptCompilationDuringPlay") == 0) // Recompile and continue playing
                        EditorApplication.update += EditorUpdate;
                }

                return _PreviewScene;
            }
        }
        
        private static void PlayModeState(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
                ClosePreviewScene();
        }

        private static void EditorUpdate()
        {
            if (EditorApplication.isPlaying && EditorApplication.isCompiling && _PreviewScene.IsValid())
                ClosePreviewScene();
        }

        private static void ClosePreviewScene()
        {
            if (_PreviewScene.IsValid())
            {
                EditorSceneManager.ClosePreviewScene(_PreviewScene);
                InstanceCache.Clear();
                InstanceIDToAsset.Clear();
            }
            else
            {
                Debug.LogError("Unable to close AssetDatabaseMode instance preview scene");
            }
            
            EditorApplication.update -= EditorUpdate;
            EditorApplication.playModeStateChanged -= PlayModeState;
        }

        private static Object[] LoadAllAssetRepresentationsAtPath(string assetPath)
        {
            var allObjects = AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath);
            for (int i = 0; i < allObjects.Length; ++i)
                allObjects[i] = GetInstanceObject(allObjects[i]);
            return allObjects;
        }

        internal static Object LoadAssetSubObject(string assetPath, string subObjectName, Type type)
        {
            var objs = AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath);
            foreach (var o in objs)
            {
                if (o.name == subObjectName)
                {
                    if (type.IsAssignableFrom(o.GetType()))
                    {
                        return GetInstanceObject(o);
                    }
                }
            }
            return null;
        }

        private static Object LoadMainAssetAtPath(string assetPath)
        {
            return GetInstanceObject(AssetDatabase.LoadMainAssetAtPath(assetPath));
        }

        internal static object LoadAssetAtPath(string assetPath, ProvideHandle provideHandle)
        {
            Object obj = AssetDatabase.LoadAssetAtPath(assetPath, provideHandle.Location.ResourceType);
            Type objType = obj.GetType();
            obj = obj != null && provideHandle.Type.IsAssignableFrom(objType) ? obj : null;
            return GetInstanceObject(obj, objType);
        }

        private static Object GetInstanceObject(Object original, Type resultType = null)
        {
            if (original != null)
            {
                if (resultType == null)
                    resultType = original.GetType();
                if (GameObjectType.IsAssignableFrom(resultType))
                {
                    Object o = AddOrGetInstanceFromCache(original);
                    SceneManager.MoveGameObjectToScene((GameObject)o, PreviewScene);
                    return o;
                }
                if (ScriptableObjectType.IsAssignableFrom(resultType))
                {
                    return AddOrGetInstanceFromCache(original);
                }
            }

            return original;
        }

        private static Object AddOrGetInstanceFromCache(Object original)
        {
            if (InstanceCache.TryGetValue(original, out AssetInstance inst))
            {
                inst.LoadCount++;
                return inst.Instance;
            }
            Object o = Object.Instantiate(original);
            o.name = o.name.Substring(0, o.name.Length - 7);
            InstanceCache.Add(original, new AssetInstance() {Instance = o, LoadCount = 1});
            InstanceIDToAsset.Add(o.GetInstanceID(), original);
            return o;
        }

        internal static void ReleaseAssetDatabaseLoadedObject(object obj)
        {
            switch (obj)
            {
                case Array objArray:
                {
                    foreach (object o in objArray)
                        Release(o as Object);
                    break;
                }
                case IList objList:
                {
                    foreach (object o in objList)
                        Release(o as Object);
                    break;
                }
                default:
                {
                    Release(obj as Object);
                    break;
                }
            }
        }

        private static void Release(Object o)
        {
            if (o == null)
                return;
            
            int instanceID = o.GetInstanceID();
            if (InstanceIDToAsset.TryGetValue(instanceID, out Object original))
            {
                AssetInstance usage = InstanceCache[original];
                usage.LoadCount--;
                if (usage.LoadCount == 0)
                {
                    InstanceIDToAsset.Remove(instanceID);
                    Object.Destroy(usage.Instance);
                    InstanceCache.Remove(original);
                }
            }
        }

        /// <summary>
        /// Default constructor.
        /// </summary>
        public AssetDatabaseProvider() {}

        /// <summary>
        /// Constructor that allows for a sepcified delay for all requests.
        /// </summary>
        /// <param name="delay">Time in seconds for each delay call.</param>
        public AssetDatabaseProvider(float delay = .25f)
        {
            m_LoadDelay = delay;
        }

        internal static Object[] LoadAssetsWithSubAssets(string assetPath)
        {
            var subObjects = LoadAllAssetRepresentationsAtPath(assetPath);
            var allObjects = new Object[subObjects.Length + 1];
            allObjects[0] = LoadMainAssetAtPath(assetPath);
            for (int i = 0; i < subObjects.Length; i++)
                allObjects[i + 1] = subObjects[i];
            return allObjects;
        }

        class InternalOp
        {
            ProvideHandle m_ProvideHandle;
            bool m_Loaded;
            public void Start(ProvideHandle provideHandle, float loadDelay)
            {
                m_Loaded = false;
                m_ProvideHandle = provideHandle;
                m_ProvideHandle.SetWaitForCompletionCallback(WaitForCompletionHandler);
                if (loadDelay < 0)
                    LoadImmediate();
                else
                    DelayedActionManager.AddAction((Action)LoadImmediate, loadDelay);
            }

            private bool WaitForCompletionHandler()
            {
                LoadImmediate();
                return true;
            }

            void LoadImmediate()
            {
                if (m_Loaded)
                    return;
                m_Loaded = true;
                string assetPath = m_ProvideHandle.ResourceManager.TransformInternalId(m_ProvideHandle.Location);
                object result = null;
                if (m_ProvideHandle.Type.IsArray)
                    result = ResourceManagerConfig.CreateArrayResult(m_ProvideHandle.Type, LoadAssetsWithSubAssets(assetPath));
                else if (m_ProvideHandle.Type.IsGenericType && typeof(IList<>) == m_ProvideHandle.Type.GetGenericTypeDefinition())
                    result = ResourceManagerConfig.CreateListResult(m_ProvideHandle.Type, LoadAssetsWithSubAssets(assetPath));
                else
                {
                    if (ResourceManagerConfig.ExtractKeyAndSubKey(assetPath, out string mainPath, out string subKey))
                        result = LoadAssetSubObject(mainPath, subKey, m_ProvideHandle.Type);
                    else
                        result = LoadAssetAtPath(assetPath, m_ProvideHandle);
                }
                m_ProvideHandle.Complete(result, result != null, result == null ? new Exception($"Unable to load asset of type {m_ProvideHandle.Type} from location {m_ProvideHandle.Location}.") : null);
            }
        }

        /// <inheritdoc/>
        public override bool CanProvide(Type t, IResourceLocation location)
        {
            return base.CanProvide(t, location);
        }

        public override void Provide(ProvideHandle provideHandle)
        {
            new InternalOp().Start(provideHandle, m_LoadDelay);
        }

        public override void Release(IResourceLocation location, object obj)
        {
            ReleaseAssetDatabaseLoadedObject(obj);
        }
    }
}
#endif
