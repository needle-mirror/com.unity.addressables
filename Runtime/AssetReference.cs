using System.Collections.Generic;
using UnityEngine.ResourceManagement;

namespace UnityEngine.AddressableAssets
{
    public class AssetReferenceT<TObject> : AssetReference where TObject : Object
    {
        public IAsyncOperation<TObject> LoadAsset()
        {
            return LoadAsset<TObject>();
        }
        public IAsyncOperation<TObject> Instantiate(Vector3 position, Quaternion rotation, Transform parent = null)
        {
            return Instantiate<TObject>(position, rotation, parent);
        }

        public IAsyncOperation<TObject> Instantiate(Transform parent = null, bool instantiateInWorldSpace = false)
        {
            return Instantiate<TObject>(parent, instantiateInWorldSpace);
        }

        public override bool ValidateType(System.Type type)
        {
            return typeof(TObject).IsAssignableFrom(type);
        }
    }

    [System.Serializable]
    public class AssetReferenceGameObject : AssetReferenceT<GameObject> { }
    [System.Serializable]
    public class AssetReferenceTexture : AssetReferenceT<Texture> { }
    [System.Serializable]
    public class AssetReferenceTexture2D : AssetReferenceT<Texture2D> { }
    [System.Serializable]
    public class AssetReferenceTexture3D : AssetReferenceT<Texture3D> { }
    [System.Serializable]
    public class AssetReferenceSprite : AssetReferenceT<Sprite> { }
    //TODO: implement more of these....

    /// <summary>
    /// TODO - doc
    /// </summary>
    [System.Serializable]
    public class AssetReference
    {
        [SerializeField]
        private string m_assetGUID;

        public Hash128 RuntimeKey { get { return Hash128.Parse(m_assetGUID); } }

#if UNITY_EDITOR
        [SerializeField]
        private Object m_cachedAsset;
#endif

        public override string ToString()
        {
#if UNITY_EDITOR
            return "[" + m_assetGUID + "]" + m_cachedAsset;
#else
            return "[" + m_assetGUID + "]";
#endif
        } 

        /// <summary>
        /// TODO - doc
        /// </summary>
        public IAsyncOperation<TObject> LoadAsset<TObject>() where TObject : Object
        {
            return Addressables.LoadAsset<TObject>(RuntimeKey);
        }

        /// <summary>
        /// TODO - doc
        /// </summary>
        public IAsyncOperation<TObject> Instantiate<TObject>(Vector3 position, Quaternion rotation, Transform parent = null) where TObject : Object
        {
            return Addressables.Instantiate<TObject>(RuntimeKey, position, rotation, parent);
        }

        public IAsyncOperation<TObject> Instantiate<TObject>(Transform parent = null, bool instantiateInWorldSpace = false) where TObject : Object
        {
            return Addressables.Instantiate<TObject>(RuntimeKey, parent, instantiateInWorldSpace);
        }

        /// <summary>
        /// TODO - doc
        /// </summary>
        public void ReleaseAsset<TObject>(TObject obj) where TObject : Object
        {
            Addressables.ReleaseAsset(obj);
        }

        /// <summary>
        /// TODO - doc
        /// </summary>
        public void ReleaseInstance<TObject>(TObject obj) where TObject : Object
        {
            Addressables.ReleaseInstance(obj);
        }


        public virtual bool ValidateType(System.Type type)
        {
            return true;
        }
#if UNITY_EDITOR


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
                if (m_cachedAsset != value)
                {
                    var path = UnityEditor.AssetDatabase.GetAssetOrScenePath(value);
                    if (string.IsNullOrEmpty(path))
                    {
                        Debug.LogWarningFormat("Invalid object for AssetReference {0}.", value);
                        return;
                    }
                    var type = UnityEditor.AssetDatabase.GetMainAssetTypeAtPath(path);
                    if (!ValidateType(type))
                    {
                        Debug.LogWarningFormat("Invalid type for AssetReference {0}, path = '{1}'.", type.FullName, path);
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
    /// TODO - doc
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.Field | System.AttributeTargets.Property)]
    public sealed class AssetReferenceLabelRestriction : System.Attribute
    {
        private string[] m_allowedLabels;
        private string _cachedToString = null;
        public ICollection<string> AllowedLabels { get { return m_allowedLabels; } }
        public AssetReferenceLabelRestriction(params string[] allowedLabels)
        {
            m_allowedLabels = allowedLabels;
        }

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

        public bool Validate(HashSet<string> t)
        {
            if (t == null)
                return true;
            foreach (var tt in m_allowedLabels)
                if (t.Contains(tt))
                    return true;
            return false;
        }
    }

    [System.AttributeUsage(System.AttributeTargets.Field | System.AttributeTargets.Property)]
    public sealed class AssetReferenceTypeRestriction : System.Attribute
    {
        private System.Type[] m_allowedTypes;
        private string _cachedToString = null;
        public ICollection<System.Type> AllowedTypes { get { return m_allowedTypes; } }
        public AssetReferenceTypeRestriction(params System.Type[] allowedTypes)
        {
            m_allowedTypes = allowedTypes;
        }

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

        public bool Validate(System.Type t)
        {
            if (t == null)
                return false;
            foreach (var tt in m_allowedTypes)
                if (tt.IsAssignableFrom(t))
                    return true;
            return false;
        }
    }
}
