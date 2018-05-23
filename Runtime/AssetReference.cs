using System.Collections.Generic;
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
        public IAsyncOperation<TObject> LoadAsync<TObject>() where TObject : Object
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

        public IAsyncOperation<TObject> InstantiateAsync<TObject>(Transform parent = null, bool instantiateInWorldSpace = false) where TObject : Object
        {
            return Addressables.Instantiate<TObject>(RuntimeKey, parent, instantiateInWorldSpace);
        }

        /// <summary>
        /// TODO - doc
        /// </summary>
        public void Release<TObject>(TObject obj) where TObject : Object
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


#if UNITY_EDITOR
        public void OnBeforeSerialize()
        {
            try
            {
                assetType = UnityEditor.AssetDatabase.GetMainAssetTypeAtPath(UnityEditor.AssetDatabase.GUIDToAssetPath(assetGUID.ToString())).FullName;
          //      serializedGUID = Hash128.Parse(assetGUID);
            }
            catch (System.Exception)
            {
             //   assetType = string.Empty;
            }
        }

        public void OnAfterDeserialize()
        {
//            assetGUID = serializedGUID.ToString();
        }

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
                    var path = UnityEditor.AssetDatabase.GetAssetOrScenePath(_cachedAsset = value);
                    assetGUID = UnityEditor.AssetDatabase.AssetPathToGUID(path);
                    assetType = UnityEditor.AssetDatabase.GetMainAssetTypeAtPath(path).FullName;
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
