using System.Collections.Generic;
using UnityEngine.ResourceManagement;

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
        public override bool ValidateType(System.Type type)
        {
            return typeof(TObject).IsAssignableFrom(type);
        }
    }

    /// <summary>
    /// GameObject only asset reference.
    /// </summary>
    [System.Serializable]
    public class AssetReferenceGameObject : AssetReferenceT<GameObject> { }
    /// <summary>
    /// Texture only asset reference.
    /// </summary>
    [System.Serializable]
    public class AssetReferenceTexture : AssetReferenceT<Texture> { }
    /// <summary>
    /// Texture2D only asset reference.
    /// </summary>
    [System.Serializable]
    public class AssetReferenceTexture2D : AssetReferenceT<Texture2D> { }
    /// <summary>
    /// Texture3D only asset reference
    /// </summary>
    [System.Serializable]
    public class AssetReferenceTexture3D : AssetReferenceT<Texture3D> { }
    /// <summary>
    /// Sprite only asset reference.
    /// </summary>
    [System.Serializable]
    public class AssetReferenceSprite : AssetReferenceT<Sprite> { }
    //TODO: implement more of these....

    /// <summary>
    /// Reference to an addressable asset.  This can be used in script to provide fields that can be easily set in the editor and loaded dynamically at runtime.
    /// </summary>
    [System.Serializable]
    public class AssetReference
    {
        [SerializeField]
        private string m_assetGUID;

        /// <summary>
        /// The actual key used to request the asset at runtime.
        /// </summary>
        public Hash128 RuntimeKey { get { return Hash128.Parse(m_assetGUID); } }

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
            m_assetGUID = guid;
        }

#if UNITY_EDITOR
        [SerializeField]
        private Object m_cachedAsset;
#endif
        /// <summary>
        /// String representation of asset reference.
        /// </summary>
        /// <returns>The asset guid as a string.</returns>
        public override string ToString()
        {
#if UNITY_EDITOR
            return "[" + m_assetGUID + "]" + m_cachedAsset;
#else
            return "[" + m_assetGUID + "]";
#endif
        }

        /// <summary>
        /// Load the referenced asset as type TObject.
        /// </summary>
        /// <typeparam name="TObject">The object type.</typeparam>
        /// <returns>The load operation.</returns>
        public IAsyncOperation<TObject> LoadAsset<TObject>() where TObject : Object
        {
            return Addressables.LoadAsset<TObject>(RuntimeKey);
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
        /// <param name="obj">The object to release.</param>
        public void ReleaseAsset<TObject>(TObject obj) where TObject : Object
        {
            Addressables.ReleaseAsset(obj);
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
        public virtual bool ValidateType(System.Type type)
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
                if (m_cachedAsset != null)
                    return m_cachedAsset;
                return (m_cachedAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<Object>(UnityEditor.AssetDatabase.GUIDToAssetPath(m_assetGUID.ToString())));
            }

            set
            {
                if(value == null)
                {
                    m_cachedAsset = null;
                    m_assetGUID = string.Empty;
                    return;
                }

                if (m_cachedAsset != value)
                {
                    var path = UnityEditor.AssetDatabase.GetAssetOrScenePath(value);
                    if (string.IsNullOrEmpty(path))
                    {
                        Addressables.LogWarningFormat("Invalid object for AssetReference {0}.", value);
                        return;
                    }
                    var type = UnityEditor.AssetDatabase.GetMainAssetTypeAtPath(path);
                    if (!ValidateType(type))
                    {
                        Addressables.LogWarningFormat("Invalid type for AssetReference {0}, path = '{1}'.", type.FullName, path);
                        return;
                    }
                    else
                    {
                        m_assetGUID = UnityEditor.AssetDatabase.AssetPathToGUID(path);
                        m_cachedAsset = value;
                    }
                }
            }
        }
#endif
    }

    internal class AssetReferenceLocator : IResourceLocator
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
    [System.AttributeUsage(System.AttributeTargets.Field | System.AttributeTargets.Property)]
    public sealed class AssetReferenceLabelRestriction : System.Attribute
    {
        private string[] m_allowedLabels;
        private string _cachedToString = null;
        /// <summary>
        /// The labels allowed for the attributed AssetReference.
        /// </summary>
        public ICollection<string> AllowedLabels { get { return m_allowedLabels; } }
        /// <summary>
        /// Construct a new AssetReferenceLabelAttribute.
        /// </summary>
        /// <param name="allowedLabels">The labels allowed for the attributed AssetReference.</param>
        public AssetReferenceLabelRestriction(params string[] allowedLabels)
        {
            m_allowedLabels = allowedLabels;
        }
        ///<inheritdoc/>
        public override string ToString()
        {
            if (_cachedToString == null)
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                bool first = true;
                foreach (var t in m_allowedLabels)
                {
                    if (!first)
                        sb.Append(',');
                    first = false;
                    sb.Append(t);
                }
                _cachedToString = sb.ToString();
            }
            return _cachedToString;
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
            foreach (var tt in m_allowedLabels)
                if (labels.Contains(tt))
                    return true;
            return false;
        }
    }

    /// <summary>
    /// Used to restrict an AssetReference field or property to only allow assignment of assets of specific types.  This is only enforced through the UI.
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.Field | System.AttributeTargets.Property)]
    public sealed class AssetReferenceTypeRestriction : System.Attribute
    {
        private System.Type[] m_allowedTypes;
        private string _cachedToString = null;
        /// <summary>
        /// The allowed types for the asset reference.
        /// </summary>
        public ICollection<System.Type> AllowedTypes { get { return m_allowedTypes; } }
        public AssetReferenceTypeRestriction(params System.Type[] allowedTypes)
        {
            m_allowedTypes = allowedTypes;
        }
        ///<inheritdoc/>
        public override string ToString()
        {
            if (_cachedToString == null)
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                bool first = true;
                foreach (var t in m_allowedTypes)
                {
                    if (!first)
                        sb.Append(',');
                    first = false;
                    sb.Append(t.Name);
                }
                _cachedToString = sb.ToString();
            }
            return _cachedToString;
        }

        /// <summary>
        /// Validates the type against the set of allowed types.
        /// </summary>
        /// <param name="type">The type to validate.</param>
        /// <returns>Whether the type is contained in the set of allowed types.</returns>
        public bool Validate(System.Type type)
        {
            if (type == null)
                return false;
            foreach (var tt in m_allowedTypes)
                if (tt.IsAssignableFrom(type))
                    return true;
            return false;
        }
    }
}
