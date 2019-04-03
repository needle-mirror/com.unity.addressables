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

        /// <summary>
        /// TODO - doc
        /// </summary>
        public void ReleaseAsset(TObject obj)
        {
            ReleaseAsset<TObject>(obj);
        }

        /// <summary>
        /// TODO - doc
        /// </summary>
        public void ReleaseInstance(TObject obj)
        {
            ReleaseInstance<TObject>(obj);
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
        public string assetGUID;

        public Hash128 RuntimeKey { get { return Hash128.Parse(assetGUID); } }

#if UNITY_EDITOR
        [SerializeField]
        private Object _cachedAsset;
#endif

        public override string ToString()
        {
#if UNITY_EDITOR
            return "[" + assetGUID + "]" + _cachedAsset;
#else
            return "[" + assetGUID + "]";
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
                if (_cachedAsset != null)
                    return _cachedAsset;
                return (_cachedAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<Object>(UnityEditor.AssetDatabase.GUIDToAssetPath(assetGUID.ToString())));
            }

            set
            {
                if (_cachedAsset != value)
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
                        assetGUID = UnityEditor.AssetDatabase.AssetPathToGUID(path);
                        _cachedAsset = value;
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
    public class AssetReferenceLabelRestriction : System.Attribute
    {
        private string[] allowedLabels;
        public AssetReferenceLabelRestriction(params string[] labels)
        {
            allowedLabels = labels;
        }
        string _cachedToString = null;
        public override string ToString()
        {
            if (_cachedToString == null)
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                bool first = true;
                foreach (var t in allowedLabels)
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
            foreach (var tt in allowedLabels)
                if (t.Contains(tt))
                    return true;
            return false;
        }
    }

    [System.AttributeUsage(System.AttributeTargets.Field | System.AttributeTargets.Property)]
    public class AssetReferenceTypeRestriction : System.Attribute
    {
        private System.Type[] allowedTypes;
        public AssetReferenceTypeRestriction(params System.Type[] types)
        {
            allowedTypes = types;
        }

        string _cachedToString = null;
        public override string ToString()
        {
            if (_cachedToString == null)
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                bool first = true;
                foreach (var t in allowedTypes)
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
            foreach (var tt in allowedTypes)
                if (tt.IsAssignableFrom(t))
                    return true;
            return false;
        }
    }
}
