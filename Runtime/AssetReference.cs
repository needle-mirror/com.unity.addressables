using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.U2D;

namespace UnityEngine.AddressableAssets
{
    /// <summary>
    /// Generic version of AssetReference class.  This should not be used directly as CustomPropertyDrawers do not support generic types.  Instead use the concrete derived classes such as AssetReferenceGameObject.
    /// </summary>
    /// <typeparam name="TObject"></typeparam>
    public class AssetReferenceT<TObject> : AssetReference where TObject : Object
    {

        /// <summary>
        /// Construct a new AssetReference object.
        /// </summary>
        /// <param name="guid">The guid of the asset.</param>
        public AssetReferenceT(string guid) : base(guid)
        {
        }

        /// <summary>
        /// Load the referenced asset as type TObject.
        /// </summary>
        /// <returns>The load operation.</returns>
        //[Obsolete("We have added Async to the name of all asycn methods (UnityUpgradable) -> LoadAssetAsync(*)", true)]
        [Obsolete]
        public AsyncOperationHandle<TObject> LoadAsset()
        {
            return LoadAssetAsync();
        }

        /// <summary>
        /// Load the referenced asset as type TObject.
        /// </summary>
        /// <returns>The load operation.</returns>
        public virtual AsyncOperationHandle<TObject> LoadAssetAsync()
        {
            return LoadAssetAsync<TObject>();
        }
        
        /// <inheritdoc/>
        public override bool ValidateAsset(Object obj)
        {
            var type = obj.GetType();
            return typeof(TObject).IsAssignableFrom(type);
        }
        
        /// <inheritdoc/>
        public override bool ValidateAsset(string path)
        {
#if UNITY_EDITOR
            var type = AssetDatabase.GetMainAssetTypeAtPath(path);
            return typeof(TObject).IsAssignableFrom(type);
#else
            return false;
#endif
        }
                
#if UNITY_EDITOR
        /// <summary>
        /// Type-specific override of parent editorAsset.  Used by the editor to represent the asset referenced.
        /// </summary>
        /// <returns>Editor Asset as type TObject, else null</returns>
        public new TObject editorAsset
        {
            get
            {
                Object baseAsset = base.editorAsset;
                TObject asset = baseAsset as TObject;
                if( asset == null && baseAsset != null )
                    Debug.Log( "editorAsset cannot cast to " + typeof(TObject) );
                return asset;
            }
        }
#endif

        
    }

    /// <summary>
    /// GameObject only asset reference.
    /// </summary>
    [Serializable]
    public class AssetReferenceGameObject : AssetReferenceT<GameObject>
    {
        public AssetReferenceGameObject(string guid) : base(guid) { }
    }
    /// <summary>
    /// Texture only asset reference.
    /// </summary>
    [Serializable]
    public class AssetReferenceTexture : AssetReferenceT<Texture>
    {
        public AssetReferenceTexture(string guid) : base(guid) { }
    }
    /// <summary>
    /// Texture2D only asset reference.
    /// </summary>
    [Serializable]
    public class AssetReferenceTexture2D : AssetReferenceT<Texture2D>
    {
        public AssetReferenceTexture2D(string guid) : base(guid) { }
    }
    /// <summary>
    /// Texture3D only asset reference
    /// </summary>
    [Serializable]
    public class AssetReferenceTexture3D : AssetReferenceT<Texture3D>
    {
        public AssetReferenceTexture3D(string guid) : base(guid) { }
    }

    /// <summary>
    /// Sprite only asset reference.
    /// </summary>
    [Serializable]
    public class AssetReferenceSprite : AssetReferenceT<Sprite>
    {
        public AssetReferenceSprite(string guid) : base(guid) { }

        /// <inheritdoc/>
        public override bool ValidateAsset(string path)
        {
#if UNITY_EDITOR
            if (AssetDatabase.GetMainAssetTypeAtPath(path) == typeof(SpriteAtlas))
                return true;

            var type = AssetDatabase.GetMainAssetTypeAtPath(path);
            bool isTexture = typeof(Texture2D).IsAssignableFrom(type);
            if (isTexture)
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                return (importer != null) && (importer.spriteImportMode != SpriteImportMode.None);
            }
#endif
            return false;
        }
        
#if UNITY_EDITOR
        /// <summary>
        /// Typeless override of parent editorAsset. Used by the editor to represent the asset referenced.
        /// </summary>
        public new Object editorAsset
        {
            get
            {
                if (CachedAsset != null || string.IsNullOrEmpty(AssetGUID))
                    return CachedAsset;
                
                var prop = typeof(AssetReference).GetProperty("editorAsset");
                return prop.GetValue(this, null) as Object;
            }
        }
#endif
    }
    
    /// <summary>
    /// Assetreference that only allows atlassed sprites.
    /// </summary>
    [Serializable]
    public class AssetReferenceAtlasedSprite : AssetReferenceT<Sprite>
    {
        public AssetReferenceAtlasedSprite(string guid) : base(guid) { }

        /// <inheritdoc/>
        public override bool ValidateAsset(string path)
        {
#if UNITY_EDITOR
            return AssetDatabase.GetMainAssetTypeAtPath(path) == typeof(SpriteAtlas);
#else
            return false;
#endif
        }
        
#if UNITY_EDITOR
        /// <summary>
        /// SpriteAtlas Type-specific override of parent editorAsset. Used by the editor to represent the asset referenced.
        /// </summary>
        public new SpriteAtlas editorAsset
        {
            get
            {
                if (CachedAsset != null || string.IsNullOrEmpty(AssetGUID))
                    return CachedAsset as SpriteAtlas;
                
                var assetPath = AssetDatabase.GUIDToAssetPath(AssetGUID);
                var main = AssetDatabase.LoadMainAssetAtPath(assetPath) as SpriteAtlas;
                if (main != null)
                    CachedAsset = main;
                return main;
            }
        }
#endif
    }

    /// <summary>
    /// Reference to an addressable asset.  This can be used in script to provide fields that can be easily set in the editor and loaded dynamically at runtime.
    /// To determine if the reference is set, use RuntimeKeyIsValid().  
    /// </summary>
    [Serializable]
    public class AssetReference : IKeyEvaluator
    {
        [FormerlySerializedAs("m_assetGUID")]
        [SerializeField]
        string m_AssetGUID = "";
        [SerializeField]
        string m_SubObjectName;

        AsyncOperationHandle m_Operation;
        /// <summary>
        /// The actual key used to request the asset at runtime. RuntimeKeyIsValid() can be used to determine if this reference was set.
        /// </summary>
        public virtual object RuntimeKey
        {
            get
            {
                if (m_AssetGUID == null)
                    m_AssetGUID = string.Empty;
                if (!string.IsNullOrEmpty(m_SubObjectName))
                    return string.Format("{0}[{1}]", m_AssetGUID, m_SubObjectName);
                return m_AssetGUID;
            }
        }

        public virtual string AssetGUID { get { return m_AssetGUID; } }
        public virtual string SubObjectName { get { return m_SubObjectName; } set { m_SubObjectName = value; } }


        /// <summary>
        /// Returns the state of the internal operation.
        /// </summary>
        /// <returns>True if the operation is valid.</returns>
        public bool IsValid()
        {
            return m_Operation.IsValid();
        }

        /// <summary>
        /// Get the loading status of the internal operation.
        /// </summary>
        public bool IsDone
        {
            get
            {
                return m_Operation.IsDone;
            }
        }

        /// <summary>
        /// Construct a new AssetReference object.
        /// </summary>
        public AssetReference()
        {
        }

        /// <summary>
        /// Construct a new AssetReference object.
        /// </summary>
        /// <param name="guid">The guid of the asset.</param>
        public AssetReference(string guid)
        {
            m_AssetGUID = guid;
        }

        /// <summary>
        /// The loaded asset.  This value is only set after the AsyncOperationHandle returned from LoadAssetAsync completes.  It will not be set if only InstantiateAsync is called.  It will be set to null if release is called.
        /// </summary>
        public virtual Object Asset
        {
            get
            {
                if (!m_Operation.IsValid())
                    return null;

                return m_Operation.Result as Object;
            }
        }

#if UNITY_EDITOR
        Object m_CachedAsset;
        
        /// <summary>
        /// Cached Editor Asset.
        /// </summary>
        protected Object CachedAsset
        {
            get { return m_CachedAsset; }
            set { m_CachedAsset = value; }
        }
#endif
        /// <summary>
        /// String representation of asset reference.
        /// </summary>
        /// <returns>The asset guid as a string.</returns>
        public override string ToString()
        {
#if UNITY_EDITOR
            return "[" + m_AssetGUID + "]" + m_CachedAsset;
#else
            return "[" + m_AssetGUID + "]";
#endif
        }

        /// <summary>
        /// Load the referenced asset as type TObject.
        /// </summary>
        /// <typeparam name="TObject">The object type.</typeparam>
        /// <returns>The load operation.</returns>
        //[Obsolete("We have added Async to the name of all asycn methods (UnityUpgradable) -> LoadAssetAsync(*)", true)]
        [Obsolete]
        public AsyncOperationHandle<TObject> LoadAsset<TObject>()
        {
            return LoadAssetAsync<TObject>();
        }

        /// <summary>
        /// Loads the reference as a scene.
        /// </summary>
        /// <returns>The operation handle for the scene load.</returns>
        //[Obsolete("We have added Async to the name of all asycn methods (UnityUpgradable) -> LoadSceneAsync(*)", true)]
        [Obsolete]
        public AsyncOperationHandle<SceneInstance> LoadScene()
        {
            return LoadSceneAsync();
        }
        /// <summary>
        /// InstantiateAsync the referenced asset as type TObject.
        /// </summary>
        /// <param name="position">Position of the instantiated object.</param>
        /// <param name="rotation">Rotation of the instantiated object.</param>
        /// <param name="parent">The parent of the instantiated object.</param>
        /// <returns></returns>
        //[Obsolete("We have added Async to the name of all asycn methods (UnityUpgradable) -> InstantiateAsync(*)", true)]
        [Obsolete]
        public AsyncOperationHandle<GameObject> Instantiate(Vector3 position, Quaternion rotation, Transform parent = null)
        {
            return InstantiateAsync(position, rotation, parent);
        }

        /// <summary>
        /// InstantiateAsync the referenced asset as type TObject.
        /// </summary>
        /// <typeparam name="TObject">The object type.</typeparam>
        /// <param name="parent">The parent of the instantiated object.</param>
        /// <param name="instantiateInWorldSpace">Option to retain world space when instantiated with a parent.</param>
        /// <returns></returns>
        //[Obsolete("We have added Async to the name of all asycn methods (UnityUpgradable) -> InstantiateAsync(*)", true)]
        [Obsolete]
        public AsyncOperationHandle<GameObject> Instantiate(Transform parent = null, bool instantiateInWorldSpace = false)
        {
            return InstantiateAsync(parent, instantiateInWorldSpace);
        }

        /// <summary>
        /// Load the referenced asset as type TObject.
        /// </summary>
        /// <typeparam name="TObject">The object type.</typeparam>
        /// <returns>The load operation.</returns>
        public virtual AsyncOperationHandle<TObject> LoadAssetAsync<TObject>()
        {
            AsyncOperationHandle<TObject> result = Addressables.LoadAssetAsync<TObject>(RuntimeKey);
            m_Operation = result;
            return result;
        }

        /// <summary>
        /// Loads the reference as a scene.
        /// </summary>
        /// <param name="loadMode">Scene load mode.</param>
        /// <param name="activateOnLoad">If false, the scene will load but not activate (for background loading).  The SceneInstance returned has an Activate() method that can be called to do this at a later point.</param>
        /// <param name="priority">Async operation priority for scene loading.</param>
        /// <returns>The operation handle for the request.</returns>
        public virtual AsyncOperationHandle<SceneInstance> LoadSceneAsync(LoadSceneMode loadMode = LoadSceneMode.Single, bool activateOnLoad = true, int priority = 100)
        {

            var result = Addressables.LoadSceneAsync(RuntimeKey, loadMode, activateOnLoad, priority);
            m_Operation = result;
            return result;
        }
        /// <summary>
        /// Unloads the reference as a scene.
        /// </summary>
        /// <returns>The operation handle for the scene load.</returns>
        public virtual AsyncOperationHandle<SceneInstance> UnLoadScene()
        {
            return Addressables.UnloadSceneAsync(m_Operation, true);
        }
        /// <summary>
        /// InstantiateAsync the referenced asset as type TObject.
        /// </summary>
        /// <param name="position">Position of the instantiated object.</param>
        /// <param name="rotation">Rotation of the instantiated object.</param>
        /// <param name="parent">The parent of the instantiated object.</param>
        /// <returns></returns>
        public virtual AsyncOperationHandle<GameObject> InstantiateAsync(Vector3 position, Quaternion rotation, Transform parent = null)
        {
            return Addressables.InstantiateAsync(RuntimeKey, position, rotation, parent, true);
        }

        /// <summary>
        /// InstantiateAsync the referenced asset as type TObject.
        /// </summary>
        /// <typeparam name="TObject">The object type.</typeparam>
        /// <param name="parent">The parent of the instantiated object.</param>
        /// <param name="instantiateInWorldSpace">Option to retain world space when instantiated with a parent.</param>
        /// <returns></returns>
        public virtual AsyncOperationHandle<GameObject> InstantiateAsync(Transform parent = null, bool instantiateInWorldSpace = false)
        {
            return Addressables.InstantiateAsync(RuntimeKey, parent, instantiateInWorldSpace, true);
        }

        /// <inheritdoc/>
        public virtual bool RuntimeKeyIsValid()
        {
            Guid result;
            string guid = RuntimeKey.ToString();
            int subObjectIndex = guid.IndexOf("[");
            if (subObjectIndex != -1) //This means we're dealing with a sub-object and need to convert the runtime key.
                guid = guid.Substring(0, subObjectIndex);
            return Guid.TryParse(guid, out result);
        }

        /// <summary>
        /// Release the internal operation handle.
        /// </summary>
        public virtual void ReleaseAsset()
        {
            if (!m_Operation.IsValid())
            {
                Debug.LogWarning("Cannot release a null or unloaded asset.");
                return;
            }
            Addressables.Release(m_Operation);
            m_Operation = default(AsyncOperationHandle);
        }


        /// <summary>
        /// Release an instantiated object.
        /// </summary>
        /// <param name="obj">The object to release.</param>
        public virtual void ReleaseInstance(GameObject obj)
        {
            Addressables.ReleaseInstance(obj);
        }

        /// <summary>
        /// Validates that the referenced asset allowable for this asset reference.
        /// </summary>
        /// <param name="obj">The Object to validate.</param>
        /// <returns>Whether the referenced asset is valid.</returns>
        public virtual bool ValidateAsset(Object obj)
        {
            return true;
        }
        
        /// <summary>
        /// Validates that the referenced asset allowable for this asset reference.
        /// </summary>
        /// <param name="path">The path to the asset in question.</param>
        /// <returns>Whether the referenced asset is valid.</returns>
        public virtual bool ValidateAsset(string path)
        {
            return true;
        }

#if UNITY_EDITOR

        /// <summary>
        /// Used by the editor to represent the asset referenced.
        /// </summary>
        public virtual Object editorAsset
        {
            get
            {
                if (m_CachedAsset != null || string.IsNullOrEmpty(m_AssetGUID))
                    return m_CachedAsset;
                var assetPath = AssetDatabase.GUIDToAssetPath(m_AssetGUID);
                var mainType = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
                return (m_CachedAsset = AssetDatabase.LoadAssetAtPath(assetPath, mainType));
            }
        }
        /// <summary>
        /// Sets the asset on the AssetReference.  Only valid in the editor, this sets both the editorAsset attribute,
        ///   and the internal asset GUID, which drives the RuntimeKey attribute.
        /// <param name="value">Object to reference</param>
        /// </summary>
        public virtual bool SetEditorAsset(Object value)
        {
            if(value == null)
            {
                m_CachedAsset = null;
                m_AssetGUID = string.Empty;
                m_SubObjectName = null;
                return true;
            }

            if (m_CachedAsset != value)
            {
                var path = AssetDatabase.GetAssetOrScenePath(value);
                if (string.IsNullOrEmpty(path))
                {
                    Addressables.LogWarningFormat("Invalid object for AssetReference {0}.", value);
                    return false;
                }
                if (!ValidateAsset(path))
                {
                    Addressables.LogWarningFormat("Invalid asset for AssetReference path = '{0}'.", path);
                    return false;
                }
                else
                {
                    m_AssetGUID = AssetDatabase.AssetPathToGUID(path);
                    var mainAsset = AssetDatabase.LoadMainAssetAtPath(path);
                    m_CachedAsset = mainAsset;
                    if (value != mainAsset)
                        SetEditorSubObject(value);
                }
            }
            
            return true;
        }

        /// <summary>
        /// Sets the sub object for this asset reference.
        /// </summary>
        /// <param name="value">The sub object.</param>
        /// <returns>True if set correctly.</returns>
        public virtual bool SetEditorSubObject(Object value)
        {
            if (value == null)
            {
                m_SubObjectName = null;
                return true;
            }

            if (editorAsset == null)
                return false;
            if (editorAsset.GetType() == typeof(SpriteAtlas))
            {
                var spriteName = value.name;
                if (spriteName.EndsWith("(Clone)"))
                    spriteName = spriteName.Replace("(Clone)", "");
                if ((editorAsset as SpriteAtlas).GetSprite(spriteName) == null)
                {
                    Debug.LogWarningFormat("Unable to find sprite {0} in atlas {1}.", spriteName, editorAsset.name);
                    return false;
                }
                m_SubObjectName = spriteName;
                return true;
            }

            var subAssets = AssetDatabase.LoadAllAssetRepresentationsAtPath(AssetDatabase.GUIDToAssetPath(m_AssetGUID));
            foreach (var s in subAssets)
            {
                if (s.name == value.name)
                {
                    m_SubObjectName = value.name;
                    return true;
                }
            }
            return false;
        }
#endif
    }
}
