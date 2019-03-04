#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.Util;
using UnityEngine.Serialization;

namespace UnityEngine.ResourceManagement.ResourceProviders.Simulation
{
    /// <summary>
    /// Contains data needed to simulate a bundled asset
    /// </summary>
    [Serializable]
    public class VirtualAssetBundleEntry
    {
        [FormerlySerializedAs("m_name")]
        [SerializeField]
        string m_Name;
        /// <summary>
        /// The name of the asset.
        /// </summary>
        public string Name { get { return m_Name; } }
        [FormerlySerializedAs("m_size")]
        [SerializeField]
        long m_Size;
        /// <summary>
        /// The file size of the asset, in bytes.
        /// </summary>
        public long Size { get { return m_Size; } }

        /// <summary>
        /// Construct a new VirtualAssetBundleEntry
        /// </summary>
        public VirtualAssetBundleEntry() { }
        /// <summary>
        /// Construct a new VirtualAssetBundleEntry
        /// </summary>
        /// <param name="name">The name of the asset.</param>
        /// <param name="size">The size of the asset, in bytes.</param>
        public VirtualAssetBundleEntry(string name, long size)
        {
            m_Name = name;
            m_Size = size;
        }
    }

    /// <summary>
    /// Contains data need to simulate an asset bundle.
    /// </summary>
    [Serializable]
    public class VirtualAssetBundle : ISerializationCallbackReceiver
    {
        [FormerlySerializedAs("m_name")]
        [SerializeField]
        string m_Name;
        [FormerlySerializedAs("m_isLocal")]
        [SerializeField]
        bool m_IsLocal;
        [FormerlySerializedAs("m_dataSize")]
        [SerializeField]
        long m_DataSize;
        [FormerlySerializedAs("m_headerSize")]
        [SerializeField]
        long m_HeaderSize;
        [FormerlySerializedAs("m_latency")]
        [SerializeField]
        float m_Latency;
        [SerializeField]
        uint m_Crc;
        [SerializeField]
        string m_Hash;

        [FormerlySerializedAs("m_serializedAssets")]
        [SerializeField]
        List<VirtualAssetBundleEntry> m_SerializedAssets = new List<VirtualAssetBundleEntry>();

        long m_HeaderBytesLoaded;
        long m_DataBytesLoaded;

        LoadAssetBundleOp m_BundleLoadOperation;
        List<IVirtualLoadable> m_AssetLoadOperations = new List<IVirtualLoadable>();
        Dictionary<string, VirtualAssetBundleEntry> m_AssetMap;
        /// <summary>
        /// The name of the bundle.
        /// </summary>
        public string Name { get { return m_Name; } }
        /// <summary>
        /// The assets contained in the bundle.
        /// </summary>
        public List<VirtualAssetBundleEntry> Assets { get { return m_SerializedAssets; } }

        /// <summary>
        /// Construct a new VirtualAssetBundle object.
        /// </summary>
        public VirtualAssetBundle()
        {
        }

        /// <summary>
        /// The percent of data that has been loaded.
        /// </summary>
        public float PercentComplete
        {
            get
            {
                if (m_HeaderSize + m_DataSize <= 0)
                    return 1;

                return (float)(m_HeaderBytesLoaded + m_DataBytesLoaded) / (m_HeaderSize + m_DataSize);
            }
        }

        /// <summary>
        /// Construct a new VirtualAssetBundle
        /// </summary>
        /// <param name="name">The name of the bundle.</param>
        /// <param name="local">Is the bundle local or remote.  This is used to determine which bandwidth value to use when simulating loading.</param>
        public VirtualAssetBundle(string name, bool local, uint crc, string hash)
        {
            m_Latency = .1f;
            m_Name = name;
            m_IsLocal = local;
            m_HeaderBytesLoaded = 0;
            m_DataBytesLoaded = 0;
            m_Crc = crc;
            m_Hash = hash;
        }

        /// <summary>
        /// Set the size of the bundle.
        /// </summary>
        /// <param name="dataSize">The size of the data.</param>
        /// <param name="headerSize">The size of the header.</param>
        public void SetSize(long dataSize, long headerSize)
        {
            m_HeaderSize = headerSize;
            m_DataSize = dataSize;
        }

        /// <summary>
        /// Not used
        /// </summary>
        public void OnBeforeSerialize()
        {
        }

        /// <summary>
        /// Load serialized data into runtime structures.
        /// </summary>
        public void OnAfterDeserialize()
        {
            m_AssetMap = new Dictionary<string, VirtualAssetBundleEntry>();
            foreach (var a in m_SerializedAssets)
                m_AssetMap.Add(a.Name, a);
        }

        class LoadAssetBundleOp : AsyncOperationBase<VirtualAssetBundle>
        {
            VirtualAssetBundle m_Bundle;
            float m_TimeInLoadingState;
            bool m_crcHashValidated;
            public LoadAssetBundleOp(IResourceLocation location, VirtualAssetBundle bundle)
            {
                Context = location;
                Retain();
                m_Bundle = bundle;
                m_TimeInLoadingState = 0.0f;
            }

            public override float PercentComplete
            {
                get
                {
                    if (IsDone)
                        return 1f;
                    return m_Bundle.PercentComplete;
                }
            }

            public void Update(long localBandwidth, long remoteBandwidth, float unscaledDeltaTime)
            {
                Validate();
                if (!m_crcHashValidated)
                {
                    var location = Context as IResourceLocation;
                    var reqOptions = location.Data as AssetBundleRequestOptions;
                    if (reqOptions != null)
                    {
                        if (reqOptions.Crc != 0 && m_Bundle.m_Crc != reqOptions.Crc)
                        {
                            var err = string.Format("Error while downloading Asset Bundle: CRC Mismatch. Provided {0}, calculated {1} from data. Will not load Asset Bundle.", reqOptions.Crc, m_Bundle.m_Crc);
                            SetResult(null);
                            OperationException = new Exception(err);
                            InvokeCompletionEvent();
                        }
                        if (!m_Bundle.m_IsLocal)
                        {
                            if (!string.IsNullOrEmpty(reqOptions.Hash))
                            {
                                if (string.IsNullOrEmpty(m_Bundle.m_Hash) || m_Bundle.m_Hash != reqOptions.Hash)
                                {
                                    Debug.LogWarningFormat("Mismatched hash in bundle {0}.", m_Bundle.Name);
                                }
                                //TODO: implement virtual cache that would persist between runs.
                                //if(vCache.hashBundle(m_Bundle.Name, reqOptions.Hash))
                                //      m_m_Bundle.IsLocal = true;
                            }
                        }
                    }
                    m_crcHashValidated = true;
                }

                m_TimeInLoadingState += unscaledDeltaTime;
                if (m_TimeInLoadingState > m_Bundle.m_Latency)
                {
                    long localBytes = (long)Math.Ceiling(unscaledDeltaTime * localBandwidth);
                    long remoteBytes = (long)Math.Ceiling(unscaledDeltaTime * remoteBandwidth);

                    if (m_Bundle.LoadData(localBytes, remoteBytes))
                    {
                        SetResult(m_Bundle);
                        InvokeCompletionEvent();
                    }
                }
            }
        }

        bool LoadData(long localBytes, long remoteBytes)
        {
            if (m_IsLocal)
            {
                m_HeaderBytesLoaded += localBytes;
                return m_HeaderBytesLoaded >= m_HeaderSize;
            }

            m_DataBytesLoaded += remoteBytes;
            if (m_DataBytesLoaded >= m_DataSize)
            {
                m_IsLocal = true;
                m_HeaderBytesLoaded = 0;
            }
            return false;
        }

        internal bool Unload()
        {
            if (m_BundleLoadOperation == null)
                Debug.LogWarningFormat("Simulated assetbundle {0} is already unloaded.", m_Name);
            m_HeaderBytesLoaded = 0;
            m_BundleLoadOperation = null;
            return true;
        }

        internal IAsyncOperation<VirtualAssetBundle> StartLoad(IResourceLocation location)
        {
            if (m_BundleLoadOperation != null)
            {
                if (m_BundleLoadOperation.IsDone)
                    Debug.LogWarningFormat("Simulated assetbundle {0} is already loaded.", m_Name);
                else
                    Debug.LogWarningFormat("Simulated assetbundle {0} is already loading.", m_Name);
                return m_BundleLoadOperation;
            }
            m_HeaderBytesLoaded = 0;
            return (m_BundleLoadOperation = new LoadAssetBundleOp(location, this));
        }

        /// <summary>
        /// Load an asset via its location.  The asset will actually be loaded via the AssetDatabase API.
        /// </summary>
        /// <typeparam name="TObject"></typeparam>
        /// <param name="location"></param>
        /// <returns></returns>
        public IAsyncOperation<TObject> LoadAssetAsync<TObject>(IResourceLocation location) where TObject : class
        {
            if (location == null)
                throw new ArgumentException("IResourceLocation location cannot be null.");
            if (m_BundleLoadOperation == null)
                return new CompletedOperation<TObject>().Start(location, location, default(TObject), new ResourceManagerException("LoadAssetAsync called on unloaded bundle " + m_Name));

            if (!m_BundleLoadOperation.IsDone)
                return new CompletedOperation<TObject>().Start(location, location, default(TObject), new ResourceManagerException("LoadAssetAsync called on loading bundle " + m_Name));

            VirtualAssetBundleEntry assetInfo;
            if (!m_AssetMap.TryGetValue(location.InternalId, out assetInfo))
                return new CompletedOperation<TObject>().Start(location, location, default(TObject), new ResourceManagerException(string.Format("Unable to load asset {0} from simulated bundle {1}.", location.InternalId, Name)));

            LoadAssetOp<TObject> op = new LoadAssetOp<TObject>(location, assetInfo);
            m_AssetLoadOperations.Add(op);
            return op;
        }

        internal void CountBandwidthUsage(ref long localCount, ref long remoteCount)
        {
            if (m_BundleLoadOperation != null && m_BundleLoadOperation.IsDone)
            {
                localCount += m_AssetLoadOperations.Count;
                return;
            }

            if (m_IsLocal)
                localCount++;
            else
                remoteCount++;
        }

        interface IVirtualLoadable
        {
            bool Load(long localBandwidth, long remoteBandwidth);
        }

        class LoadAssetOp<TObject> : AsyncOperationBase<TObject>, IVirtualLoadable where TObject : class
        {
            long m_BytesLoaded;
            float m_LastUpdateTime;
            VirtualAssetBundleEntry m_AssetInfo;
            public LoadAssetOp(IResourceLocation location, VirtualAssetBundleEntry assetInfo)
            {
                Context = location;
                m_AssetInfo = assetInfo;
                m_LastUpdateTime = Time.realtimeSinceStartup;
            }

            public override float PercentComplete { get { return Mathf.Clamp01(m_BytesLoaded / (float)m_AssetInfo.Size); } }
            public bool Load(long localBandwidth, long remoteBandwidth)
            {
                if (Time.unscaledTime > m_LastUpdateTime)
                {
                    m_BytesLoaded += (long)Math.Ceiling((Time.unscaledTime - m_LastUpdateTime) * localBandwidth);
                    m_LastUpdateTime = Time.unscaledDeltaTime;
                }
                if (m_BytesLoaded < m_AssetInfo.Size)
                    return true;
                if (!(Context is IResourceLocation))
                    return false;
                var assetPath = (Context as IResourceLocation).InternalId;
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
                InvokeCompletionEvent();
                return false;
            }
        }

        //return true until complete
        internal bool UpdateAsyncOperations(long localBandwidth, long remoteBandwidth, float unscaledDeltaTime)
        {
            if (m_BundleLoadOperation == null)
                return false;

            if (!m_BundleLoadOperation.IsDone)
            {
                m_BundleLoadOperation.Update(localBandwidth, remoteBandwidth, unscaledDeltaTime);
                return true;
            }

            foreach (var o in m_AssetLoadOperations)
            {
                if (!o.Load(localBandwidth, remoteBandwidth))
                {
                    m_AssetLoadOperations.Remove(o);
                    break;
                }
            }
            return m_AssetLoadOperations.Count > 0;
        }

    }
}
#endif
