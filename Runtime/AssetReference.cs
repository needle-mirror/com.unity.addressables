using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine.ResourceManagement;
using UnityEngine.Serialization;

namespace UnityEngine.AddressableAssets
{
    /// <summary>
    /// Generic version of AssetReference class.  This should not be used directly as CustomPropertyDrawers do not support generic types.  Instead use the concrete derived classes such as AssetReferenceGameObject.
    /// </summary>
    /// <typeparam name="TObject"></typeparam>
    public class AssetReferenceT<TObject> : AssetReference where TObject : Object
    {
        /// <summary>
        /// Load the referenced asset as type TObject.
        /// </summary>
        /// <returns>The load operation.</returns>
        public IAsyncOperation<TObject> LoadAsset()
        {
            return LoadAsset<TObject>();
        }
        /// <summary>
        /// Instantiate the referenced asset as type TObject.
        /// </summary>
        /// <param name="position">The position of the instantiated object.</param>
        /// <param name="rotation">The rotation of the instantiated object.</param>
        /// <param name="parent">The parent transformation of the instantiated object.</param>
        /// <returns></returns>
        public IAsyncOperation<TObject> Instantiate(Vector3 position, Quaternion rotation, Transform parent = null)
        {
            return Instantiate<TObject>(position, rotation, parent);
        }

        /// <summary>
        /// Instantiate the referenced asset as type TObject.
        /// </summary>
        /// <param name="parent">The parent transform of the instantiated object.</param>
        /// <param name="instantiateInWorldSpace">Option to retain world space when instantiated with a parent.</param>
        /// <returns></returns>
        public IAsyncOperation<TObject> Instantiate(Transform parent = null, bool instantiateInWorldSpace = false)
        {
            return Instantiate<TObject>(parent, instantiateInWorldSpace);
        }

        /// <summary>
        /// Ensure that the referenced asset is of the correct type.
        /// </summary>
        /// <param name="type">The type to validate.</param>
        /// <returns></returns>
        public override bool ValidateType(Type type)
        {
            return typeof(TObject).IsAssignableFrom(type);
        }
    }

    /// <summary>
    /// GameObject only asset reference.
    /// </summary>
    [Serializable]
    public class AssetReferenceGameObject : AssetReferenceT<GameObject> { }
    /// <summary>
    /// Texture only asset reference.
    /// </summary>
    [Serializable]
    public class AssetReferenceTexture : AssetReferenceT<Texture> { }
    /// <summary>
    /// Texture2D only asset reference.
    /// </summary>
    [Serializable]
    public class AssetReferenceTexture2D : AssetReferenceT<Texture2D> { }
    /// <summary>
    /// Texture3D only asset reference
    /// </summary>
    [Serializable]
    public class AssetReferenceTexture3D : AssetReferenceT<Texture3D> { }
    /// <summary>
    /// Sprite only asset reference.
    /// </summary>
    [Serializable]
    public class AssetReferenceSprite : AssetReferenceT<Sprite> { }
    //TODO: implement more of these....

    /// <summary>
    /// Reference to an addressable asset.  This can be used in script to provide fields that can be easily set in the editor and loaded dynamically at runtime.
    /// </summary>
    [Serializable]
    public class AssetReference
    {
        [FormerlySerializedAs("m_assetGUID")]
        [SerializeField]
        string m_AssetGUID;
        Object m_LoadedAsset;

        /// <summary>
        /// The actual key used to request the asset at runtime.
        /// </summary>
        public Hash128 RuntimeKey { get { return Hash128.Parse(m_AssetGUID); } }

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
        /// The loaded asset.  This value is only set after the IAsyncOperation returned from LoadAsset completes.  It will not be set if only Instantiate is called.  It will be set to null if release is called.
        /// </summary>
        public Object Asset
        {
            get
            {
                return m_LoadedAsset;
            }
        }

#if UNITY_EDITOR
        [FormerlySerializedAs("m_cachedAsset")]
        [SerializeField]
        Object m_CachedAsset;
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
        public IAsyncOperation<TObject> LoadAsset<TObject>() where TObject : Object
        {
            var loadOp = Addressables.LoadAsset<TObject>(RuntimeKey);
            loadOp.Completed += op => m_LoadedAsset = op.Result;
            return loadOp;
        }

        /// <summary>
        /// Instantiate the referenced asset as type TObject.
        /// </summary>
        /// <typeparam name="TObject">The object type.</typeparam>
        /// <param name="position">Position of the instantiated object.</param>
        /// <param name="rotation">Rotation of the instantiated object.</param>
        /// <param name="parent">The parent of the instantiated object.</param>
        /// <returns></returns>
        public IAsyncOperation<TObject> Instantiate<TObject>(Vector3 position, Quaternion rotation, Transform parent = null) where TObject : Object
        {
            return Addressables.Instantiate<TObject>(RuntimeKey, position, rotation, parent);
        }

        /// <summary>
        /// Instantiate the referenced asset as type TObject.
        /// </summary>
        /// <typeparam name="TObject">The object type.</typeparam>
        /// <param name="parent">The parent of the instantiated object.</param>
        /// <param name="instantiateInWorldSpace">Option to retain world space when instantiated with a parent.</param>
        /// <returns></returns>
        public IAsyncOperation<TObject> Instantiate<TObject>(Transform parent = null, bool instantiateInWorldSpace = false) where TObject : Object
        {
            return Addressables.Instantiate<TObject>(RuntimeKey, parent, instantiateInWorldSpace);
        }

        /// <summary>
        /// Release the refrerenced asset.
        /// </summary>
        /// <typeparam name="TObject">The object type.</typeparam>
        [Obsolete("Use ReleaseAsset without parameters.  It will release the asset loaded via LoadAsset.")]
        public void ReleaseAsset<TObject>(Object asset) where TObject : Object
        {
            if (asset != m_LoadedAsset)
            {
                Debug.LogWarning("Attempting to release the wrong asset to an AssetReference.");
                return;
            }
            if (m_LoadedAsset == null)
            {
                Debug.LogWarning("Cannot release null asset.");
                return;
            }
            Addressables.ReleaseAsset(m_LoadedAsset);
            m_LoadedAsset = null;
        }


        /// <summary>
        /// Release the refrerenced asset.
        /// </summary>
        /// <typeparam name="TObject">The object type.</typeparam>
        public void ReleaseAsset<TObject>() where TObject : Object
        {
            if (m_LoadedAsset == null)
            {
                Debug.LogWarning("Cannot release null asset.");
                return;
            }
            Addressables.ReleaseAsset(m_LoadedAsset);
            m_LoadedAsset = null;
        }

        /// <summary>
        /// Release an instantiated object.
        /// </summary>
        /// <typeparam name="TObject">The object type.</typeparam>
        /// <param name="obj">The object to release.</param>
        public void ReleaseInstance<TObject>(TObject obj) where TObject : Object
        {
            Addressables.ReleaseInstance(obj);
        }

        /// <summary>
        /// Validates that the referenced asset is the correct type.
        /// </summary>
        /// <param name="type">The type to validate.</param>
        /// <returns>Whether the referenced asset is the specified type.</returns>
        public virtual bool ValidateType(Type type)
        {
            return true;
        }
#if UNITY_EDITOR

        /// <summary>
        /// Used by the editor to represent the asset referenced.
        /// </summary>
        public Object editorAsset
        {
            get
            {
                if (m_CachedAsset != null)
                    return m_CachedAsset;
                return (m_CachedAsset = AssetDatabase.LoadAssetAtPath<Object>(AssetDatabase.GUIDToAssetPath(m_AssetGUID)));
            }

            set
            {
                if(value == null)
                {
                    m_CachedAsset = null;
                    m_AssetGUID = string.Empty;
                    return;
                }

                if (m_CachedAsset != value)
                {
                    var path = AssetDatabase.GetAssetOrScenePath(value);
                    if (string.IsNullOrEmpty(path))
                    {
                        Addressables.LogWarningFormat("Invalid object for AssetReference {0}.", value);
                        return;
                    }
                    var type = AssetDatabase.GetMainAssetTypeAtPath(path);
                    if (!ValidateType(type))
                    {
                        Addressables.LogWarningFormat("Invalid type for AssetReference {0}, path = '{1}'.", type.FullName, path);
                    }
                    else
                    {
                        m_AssetGUID = AssetDatabase.AssetPathToGUID(path);
                        m_CachedAsset = value;
                    }
                }
            }
        }
#endif
    }

    class AssetReferenceLocator : IResourceLocator
    {
        public bool Locate(object key, out IList<IResourceLocation> locations)
        {
            locations = null;
            var ar = key as AssetReference;
            if (ar == null)
                return false;
            return Addressables.GetResourceLocations(ar.RuntimeKey, out locations);
        }
    }

    /// <summary>
    /// Used to restrict an AssetReference field or property to only allow items wil specific labels.  This is only enforced through the UI.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class AssetReferenceLabelRestriction : Attribute
    {
        string[] m_AllowedLabels;
        string m_CachedToString;
        /// <summary>
        /// The labels allowed for the attributed AssetReference.
        /// </summary>
        public ICollection<string> AllowedLabels { get { return m_AllowedLabels; } }
        /// <summary>
        /// Construct a new AssetReferenceLabelAttribute.
        /// </summary>
        /// <param name="allowedLabels">The labels allowed for the attributed AssetReference.</param>
        public AssetReferenceLabelRestriction(params string[] allowedLabels)
        {
            m_AllowedLabels = allowedLabels;
        }
        ///<inheritdoc/>
        public override string ToString()
        {
            if (m_CachedToString == null)
            {
                StringBuilder sb = new StringBuilder();
                bool first = true;
                foreach (var t in m_AllowedLabels)
                {
                    if (!first)
                        sb.Append(',');
                    first = false;
                    sb.Append(t);
                }
                m_CachedToString = sb.ToString();
            }
            return m_CachedToString;
        }
        /// <summary>
        /// Validate the set of labels against the allowed labels.
        /// </summary>
        /// <param name="labels">The set of labels to validate.</param>
        /// <returns>Returns true if the labels set is null or it contains any of the allowed labels.</returns>
        public bool Validate(HashSet<string> labels)
        {
            if (labels == null)
                return true;
            foreach (var tt in m_AllowedLabels)
                if (labels.Contains(tt))
                    return true;
            return false;
        }
    }

    /// <summary>
    /// Used to restrict an AssetReference field or property to only allow assignment of assets of specific types.  This is only enforced through the UI.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class AssetReferenceTypeRestriction : Attribute
    {
        Type[] m_AllowedTypes;
        string m_CachedToString;
        /// <summary>
        /// The allowed types for the asset reference.
        /// </summary>
        public ICollection<Type> AllowedTypes { get { return m_AllowedTypes; } }
        public AssetReferenceTypeRestriction(params Type[] allowedTypes)
        {
            m_AllowedTypes = allowedTypes;
        }
        ///<inheritdoc/>
        public override string ToString()
        {
            if (m_CachedToString == null)
            {
                StringBuilder sb = new StringBuilder();
                bool first = true;
                foreach (var t in m_AllowedTypes)
                {
                    if (!first)
                        sb.Append(',');
                    first = false;
                    sb.Append(t.Name);
                }
                m_CachedToString = sb.ToString();
            }
            return m_CachedToString;
        }

        /// <summary>
        /// Validates the type against the set of allowed types.
        /// </summary>
        /// <param name="type">The type to validate.</param>
        /// <returns>Whether the type is contained in the set of allowed types.</returns>
        public bool Validate(Type type)
        {
            if (type == null)
                return false;
            foreach (var tt in m_AllowedTypes)
                if (tt.IsAssignableFrom(type))
                    return true;
            return false;
        }
    }
}
