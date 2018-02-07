using UnityEngine;
using UnityEngine.ResourceManagement;

namespace UnityEngine.AddressableAssets
{
    /// <summary>
    /// TODO - doc
    /// </summary>
    [System.Serializable]
    public class AssetReference
#if UNITY_EDITOR
        : ISerializationCallbackReceiver
#endif
    {
        [SerializeField]
        public string assetType;
        /// <summary>
        /// TODO - doc
        /// </summary>
        [SerializeField]
        public string assetGUID;
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
        public IAsyncOperation<TObject> LoadAsync<TObject>() where TObject : Object
        {
            return ResourceManager.LoadAsync<TObject, AssetReference>(this);
        }

        /// <summary>
        /// TODO - doc
        /// </summary>
        public IAsyncOperation<TObject> InstantiateAsync<TObject>(Vector3 position, Quaternion rotation, Transform parent = null) where TObject : Object
        {
            return ResourceManager.InstantiateAsync<TObject, AssetReference>(this, position, rotation, parent);
        }

        public IAsyncOperation<TObject> InstantiateAsync<TObject>(Transform parent = null, bool instantiateInWorldSpace = false) where TObject : Object
        {
            return ResourceManager.InstantiateAsync<TObject, AssetReference>(this, parent, instantiateInWorldSpace);
        }

        /// <summary>
        /// TODO - doc
        /// </summary>
        public void Release<TObject>(TObject obj) where TObject : Object
        {
            ResourceManager.Release(obj);
        }

        /// <summary>
        /// TODO - doc
        /// </summary>
        public void ReleaseInstance<TObject>(TObject obj) where TObject : Object
        {
            ResourceManager.ReleaseInstance(obj);
        }


#if UNITY_EDITOR
        public void OnBeforeSerialize()
        {
            try
            {
                assetType = UnityEditor.AssetDatabase.GetMainAssetTypeAtPath(UnityEditor.AssetDatabase.GUIDToAssetPath(assetGUID)).FullName;
            }
            catch (System.Exception)
            {
             //   assetType = string.Empty;
            }
        }

        public void OnAfterDeserialize()
        {

        }

        public Object editorAsset
        {
            get
            {
                if (_cachedAsset != null)
                    return _cachedAsset;
                return (_cachedAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<Object>(UnityEditor.AssetDatabase.GUIDToAssetPath(assetGUID)));
            }

            set
            {
                if (_cachedAsset != value)
                {
                    var path = UnityEditor.AssetDatabase.GetAssetOrScenePath(_cachedAsset = value);
                    assetGUID = UnityEditor.AssetDatabase.AssetPathToGUID(path);
                    assetType = UnityEditor.AssetDatabase.GetMainAssetTypeAtPath(path).FullName;
                }
            }
        }
#endif
    }

    internal class AssetReferenceLocator : IResourceLocator<AssetReference>
    {
        System.Func<AssetReference, IResourceLocation> m_converter;
        public AssetReferenceLocator(System.Func<AssetReference, IResourceLocation> conv)
        {
            m_converter = conv;
        }
        public IResourceLocation Locate(AssetReference address)
        {
            return m_converter(address);
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

        public bool Validate(System.Collections.Generic.HashSet<string> t)
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
