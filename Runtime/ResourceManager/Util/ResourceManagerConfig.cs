using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.Serialization;

namespace UnityEngine.ResourceManagement.Util
{
    /// <summary>
    /// Interface for objects that support post construction initialization via an id and byte array.
    /// </summary>
    public interface IInitializableObject
    {
        /// <summary>
        /// Initialize a constructed object.
        /// </summary>
        /// <param name="id">The id of the object.</param>
        /// <param name="data">Serialized data for the object.</param>
        /// <returns>The result of the initialization.</returns>
        bool Initialize(string id, string data);

        /// <summary>
        /// Async operation for initializing a constructed object.
        /// </summary>
        /// <param name="rm">The current instance of Resource Manager.</param>
        /// <param name="id">The id of the object.</param>
        /// <param name="data">Serialized data for the object.</param>
        /// <returns>Async operation</returns>
        AsyncOperationHandle<bool> InitializeAsync(ResourceManager rm, string id, string data);
    }


    /// <summary>
    /// Interface for objects that can create object initialization data.
    /// </summary>
    public interface IObjectInitializationDataProvider
    {
        /// <summary>
        /// The name used in the GUI for this provider
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Construct initialization data for runtime.
        /// </summary>
        /// <returns>Init data that will be deserialized at runtime.</returns>
        ObjectInitializationData CreateObjectInitializationData();
    }

    /// <summary>
    /// Allocation strategy for managing heap allocations
    /// </summary>
    public interface IAllocationStrategy
    {
        /// <summary>
        /// Create a new object of type t.
        /// </summary>
        /// <param name="type">The type to return.</param>
        /// <param name="typeHash">The hash code of the type.</param>
        /// <returns>The new object.</returns>
        object New(Type type, int typeHash);

        /// <summary>
        /// Release an object.
        /// </summary>
        /// <param name="typeHash">The hash code of the type.</param>
        /// <param name="obj">The object to release.</param>
        void Release(int typeHash, object obj);
    }

    /// <summary>
    /// Default allocator that relies in garbace collection
    /// </summary>
    public class DefaultAllocationStrategy : IAllocationStrategy
    {
        /// <inheritdoc/>
        public object New(Type type, int typeHash)
        {
            return Activator.CreateInstance(type);
        }

        /// <inheritdoc/>
        public void Release(int typeHash, object obj)
        {
        }
    }

    /// <summary>
    /// Allocation strategy that uses internal pools of objects to avoid allocations that can trigger GC calls.
    /// </summary>
    public class LRUCacheAllocationStrategy : IAllocationStrategy
    {
        int m_poolMaxSize;
        int m_poolInitialCapacity;
        int m_poolCacheMaxSize;
        List<List<object>> m_poolCache = new List<List<object>>();
        Dictionary<int, List<object>> m_cache = new Dictionary<int, List<object>>();

        /// <summary>
        /// Create a new LRUAllocationStrategy objct.
        /// </summary>
        /// <param name="poolMaxSize">The max size of each pool.</param>
        /// <param name="poolCapacity">The initial capacity to create each pool list with.</param>
        /// <param name="poolCacheMaxSize">The max size of the internal pool cache.</param>
        /// <param name="initialPoolCacheCapacity">The initial number of pools to create.</param>
        public LRUCacheAllocationStrategy(int poolMaxSize, int poolCapacity, int poolCacheMaxSize, int initialPoolCacheCapacity)
        {
            m_poolMaxSize = poolMaxSize;
            m_poolInitialCapacity = poolCapacity;
            m_poolCacheMaxSize = poolCacheMaxSize;
            for (int i = 0; i < initialPoolCacheCapacity; i++)
                m_poolCache.Add(new List<object>(m_poolInitialCapacity));
        }

        List<object> GetPool()
        {
            int count = m_poolCache.Count;
            if (count == 0)
                return new List<object>(m_poolInitialCapacity);
            var pool = m_poolCache[count - 1];
            m_poolCache.RemoveAt(count - 1);
            return pool;
        }

        void ReleasePool(List<object> pool)
        {
            if (m_poolCache.Count < m_poolCacheMaxSize)
                m_poolCache.Add(pool);
        }

        /// <inheritdoc/>
        public object New(Type type, int typeHash)
        {
            List<object> pool;
            if (m_cache.TryGetValue(typeHash, out pool))
            {
                var count = pool.Count;
                var v = pool[count - 1];
                pool.RemoveAt(count - 1);
                if (count == 1)
                {
                    m_cache.Remove(typeHash);
                    ReleasePool(pool);
                }

                return v;
            }

            return Activator.CreateInstance(type);
        }

        /// <inheritdoc/>
        public void Release(int typeHash, object obj)
        {
            List<object> pool;
            if (!m_cache.TryGetValue(typeHash, out pool))
                m_cache.Add(typeHash, pool = GetPool());
            if (pool.Count < m_poolMaxSize)
                pool.Add(obj);
        }
    }

    /// <summary>
    /// Attribute for restricting which types can be assigned to a SerializedType
    /// </summary>
    public class SerializedTypeRestrictionAttribute : Attribute
    {
        /// <summary>
        /// The type to restrict a serialized type to.
        /// </summary>
        public Type type;
    }

    /// <summary>
    /// Cache for nodes of LinkedLists.  This can be used to eliminate GC allocations.
    /// </summary>
    /// <typeparam name="T">The type of node.</typeparam>
    public class LinkedListNodeCache<T>
    {
        int m_maxNodesAllowed = int.MaxValue;
        int m_NodesCreated = 0;
        Stack<LinkedListNode<T>> m_NodeCache;

        /// <summary>
        /// Create a LinkedListNode cache.  This is intended to be used to reduce GC allocations for LinkedLists.
        /// </summary>
        public LinkedListNodeCache()
        {
            InitCache();
        }

        /// <summary>
        /// Create a LinkedListNode cache.  This is intended to be used to reduce GC allocations for LinkedLists.
        /// </summary>
        /// <param name="maxNodesAllowed">Specify a number greater than zero to limit the number of nodes that can be in the pool.</param>
        /// <param name="initialCapacity">Specify a number greater than zero to preallocate a certain number of nodes.</param>
        /// <param name="initialPreallocateCount">The number of nodes to start in the linked list cache.</param>
        public LinkedListNodeCache(int maxNodesAllowed, int initialCapacity, int initialPreallocateCount)
        {
            InitCache(maxNodesAllowed, initialCapacity, initialPreallocateCount);
        }

        void InitCache(int maxNodesAllowed = int.MaxValue, int initialCapacity = 10, int initialPreallocateCount = 0)
        {
            m_maxNodesAllowed = maxNodesAllowed;
            m_NodeCache = new Stack<LinkedListNode<T>>(initialCapacity);
            for (int i = 0; i < initialPreallocateCount; i++)
            {
                m_NodeCache.Push(new LinkedListNode<T>(default));
                m_NodesCreated++;
            }
        }

        /// <summary>
        /// Creates or returns a LinkedListNode of the requested type and set the value.
        /// </summary>
        /// <param name="val">The value to set to returned node to.</param>
        /// <returns>A LinkedListNode with the value set to val.</returns>
        public LinkedListNode<T> Acquire(T val)
        {
            if (m_NodeCache.TryPop(out var node))
            {
                node.Value = val;
                return node;
            }

            m_NodesCreated++;
            return new LinkedListNode<T>(val);
        }

        /// <summary>
        /// Release the linked list node for later use.
        /// </summary>
        /// <param name="node">The node to release</param>
        public void Release(LinkedListNode<T> node)
        {
            if (m_NodeCache.Count < m_maxNodesAllowed)
            {
                node.Value = default(T);
                m_NodeCache.Push(node);
            }
        }

        internal int CreatedNodeCount
        {
            get { return m_NodesCreated; }
        }

        internal int CachedNodeCount
        {
            get { return m_NodeCache == null ? 0 : m_NodeCache.Count; }
            set
            {
                while (value < m_NodeCache.Count)
                    m_NodeCache.TryPop(out var _);
                while (value > m_NodeCache.Count)
                    m_NodeCache.Push(new LinkedListNode<T>(default));
            }
        }
    }

    internal static class GlobalLinkedListNodeCache<T>
    {
        static LinkedListNodeCache<T> m_globalCache;

        public static bool CacheExists => m_globalCache != null;

        public static void SetCacheSize(int length)
        {
            if (m_globalCache == null)
                m_globalCache = new LinkedListNodeCache<T>();
            m_globalCache.CachedNodeCount = length;
        }

        public static LinkedListNode<T> Acquire(T val)
        {
            if (m_globalCache == null)
                m_globalCache = new LinkedListNodeCache<T>();
            return m_globalCache.Acquire(val);
        }

        public static void Release(LinkedListNode<T> node)
        {
            if (m_globalCache == null)
                m_globalCache = new LinkedListNodeCache<T>();
            m_globalCache.Release(node);
        }
    }

    /// <summary>
    /// Wrapper for serializing types for runtime.
    /// </summary>
    [Serializable]
    public struct SerializedType
    {
        [FormerlySerializedAs("m_assemblyName")]
        [SerializeField]
        string m_AssemblyName;

        /// <summary>
        /// The assembly name of the type.
        /// </summary>
        public string AssemblyName
        {
            get { return m_AssemblyName; }
        }

        [FormerlySerializedAs("m_className")]
        [SerializeField]
        string m_ClassName;

        /// <summary>
        /// The name of the type.
        /// </summary>
        public string ClassName
        {
            get { return m_ClassName; }
        }

        Type m_CachedType;

        /// <summary>
        /// Converts information about the serialized type to a formatted string.
        /// </summary>
        /// <returns>Returns information about the serialized type.</returns>
        public override string ToString()
        {
            return Value == null ? "<none>" : Value.Name;
        }

        /// <summary>
        /// Get and set the serialized type.
        /// </summary>
        public Type Value
        {
            get
            {
                try
                {
                    if (string.IsNullOrEmpty(m_AssemblyName) || string.IsNullOrEmpty(m_ClassName))
                        return null;

                    if (m_CachedType == null)
                    {
                        var assembly = Assembly.Load(m_AssemblyName);
                        if (assembly != null)
                            m_CachedType = assembly.GetType(m_ClassName);
                    }

                    return m_CachedType;
                }
                catch (Exception ex)
                {
                    //file not found is most likely an editor only type, we can ignore error.
                    if (ex.GetType() != typeof(FileNotFoundException))
                        Debug.LogException(ex);
                    return null;
                }
            }
            set
            {
                if (value != null)
                {
                    m_AssemblyName = value.Assembly.FullName;
                    m_ClassName = value.FullName;
                }
                else
                {
                    m_AssemblyName = m_ClassName = null;
                }
            }
        }

        /// <summary>
        /// Used for multi-object editing. Indicates whether or not property value was changed.
        /// </summary>
        public bool ValueChanged { get; set; }
    }

    /// <summary>
    /// Contains data used to construct and initialize objects at runtime.
    /// </summary>
    [Serializable]
    public struct ObjectInitializationData
    {
#pragma warning disable 0649
        [FormerlySerializedAs("m_id")]
        [SerializeField]
        string m_Id;

        /// <summary>
        /// The object id.
        /// </summary>
        public string Id
        {
            get { return m_Id; }
        }

        [FormerlySerializedAs("m_objectType")]
        [SerializeField]
        SerializedType m_ObjectType;

        /// <summary>
        /// The object type that will be created.
        /// </summary>
        public SerializedType ObjectType
        {
            get { return m_ObjectType; }
        }

        [FormerlySerializedAs("m_data")]
        [SerializeField]
        string m_Data;

        /// <summary>
        /// String representation of the data that will be passed to the IInitializableObject.Initialize method of the created object.  This is usually a JSON string of the serialized data object.
        /// </summary>
        public string Data
        {
            get { return m_Data; }
        }
#pragma warning restore 0649

#if !ENABLE_JSON_CATALOG
        internal class Serializer : BinaryStorageBuffer.ISerializationAdapter<ObjectInitializationData>
        {
            struct Data
            {
                public uint id;
                public uint type;
                public uint data;
            }

            public IEnumerable<BinaryStorageBuffer.ISerializationAdapter> Dependencies => null;


            unsafe public object Deserialize(BinaryStorageBuffer.Reader reader, Type t, uint offset, out uint size)
            {
                var dataStruct = reader.ReadValue<Data>(offset, out var dataStructSize);
                var dataId = reader.ReadString(dataStruct.id, out var idSize);
                var dataType = new SerializedType { Value = reader.ReadObject<Type>(dataStruct.type, out var typeSize) };
                var res = new ObjectInitializationData { m_Id = dataId, m_ObjectType = dataType, m_Data = reader.ReadString(dataStruct.data, out var dataSize) };
                size = dataStructSize + idSize + typeSize + dataSize;
                return res;
            }

            public uint Serialize(BinaryStorageBuffer.Writer writer, object val)
            {
                var oid = (ObjectInitializationData)val;
                var d = new Data
                {
                    id = writer.WriteString(oid.m_Id),
                    type = writer.WriteObject(oid.ObjectType.Value, false),
                    data = writer.WriteString(oid.m_Data)
                };
                return writer.Write(d);
            }
        }
#endif
        /// <summary>
        /// Converts information about the initialization data to a formatted string.
        /// </summary>
        /// <returns>Returns information about the initialization data.</returns>
        public override string ToString()
        {
            return string.Format("ObjectInitializationData: id={0}, type={1}", m_Id, m_ObjectType);
        }

        /// <summary>
        /// Create an instance of the defined object.  Initialize will be called on it with the id and data if it implements the IInitializableObject interface.
        /// </summary>
        /// <typeparam name="TObject">The instance type.</typeparam>
        /// <param name="idOverride">Optional id to assign to the created object.  This only applies to objects that inherit from IInitializableObject.</param>
        /// <returns>Constructed object.  This object will already be initialized with its serialized data and id.</returns>
        public TObject CreateInstance<TObject>(string idOverride = null)
        {
            try
            {
                var objType = m_ObjectType.Value;
                if (objType == null)
                    return default(TObject);
                var obj = Activator.CreateInstance(objType, true);
                var serObj = obj as IInitializableObject;
                if (serObj != null)
                {
                    if (!serObj.Initialize(idOverride == null ? m_Id : idOverride, m_Data))
                        return default(TObject);
                }

                return (TObject)obj;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return default(TObject);
            }
        }

        /// <summary>
        /// Create an instance of the defined object.  This will get the AsyncOperationHandle for the async Initialization operation if the object implements the IInitializableObject interface.
        /// </summary>
        /// <param name="rm">The current instance of Resource Manager</param>
        /// <param name="idOverride">Optional id to assign to the created object.  This only applies to objects that inherit from IInitializableObject.</param>
        /// <returns>AsyncOperationHandle for the async initialization operation if the defined type implements IInitializableObject, otherwise returns a default AsyncOperationHandle.</returns>
        public AsyncOperationHandle GetAsyncInitHandle(ResourceManager rm, string idOverride = null)
        {
            try
            {
                var objType = m_ObjectType.Value;
                if (objType == null)
                    return default(AsyncOperationHandle);
                var obj = Activator.CreateInstance(objType, true);
                var serObj = obj as IInitializableObject;
                if (serObj != null)
                    return serObj.InitializeAsync(rm, idOverride == null ? m_Id : idOverride, m_Data);
                return default(AsyncOperationHandle);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return default(AsyncOperationHandle);
            }
        }

#if UNITY_EDITOR
        Type[] m_RuntimeTypes;

        /// <summary>
        /// Construct a serialized data for the object.
        /// </summary>
        /// <param name="objectType">The type of object to create.</param>
        /// <param name="id">The object id.</param>
        /// <param name="dataObject">The serializable object that will be saved into the Data string via the JSONUtility.ToJson method.</param>
        /// <returns>Contains data used to construct and initialize an object at runtime.</returns>
        public static ObjectInitializationData CreateSerializedInitializationData(Type objectType, string id = null, object dataObject = null)
        {
            return new ObjectInitializationData
            {
                m_ObjectType = new SerializedType {Value = objectType},
                m_Id = string.IsNullOrEmpty(id) ? objectType.FullName : id,
                m_Data = dataObject == null ? null : JsonUtility.ToJson(dataObject),
                m_RuntimeTypes = dataObject == null ? null : new[] {dataObject.GetType()}
            };
        }

        /// <summary>
        /// Construct a serialized data for the object.
        /// </summary>
        /// <typeparam name="T">The type of object to create.</typeparam>
        /// <param name="id">The ID for the object</param>
        /// <param name="dataObject">The serializable object that will be saved into the Data string via the JSONUtility.ToJson method.</param>
        /// <returns>Contains data used to construct and initialize an object at runtime.</returns>
        public static ObjectInitializationData CreateSerializedInitializationData<T>(string id = null, object dataObject = null)
        {
            return CreateSerializedInitializationData(typeof(T), id, dataObject);
        }

        /// <summary>
        /// Get the set of runtime types need to deserialized this object.  This is used to ensure that types are not stripped from player builds.
        /// </summary>
        /// <returns></returns>
        public Type[] GetRuntimeTypes()
        {
            return m_RuntimeTypes;
        }

#endif
    }

    /// <summary>
    /// Resource Manager Config utility class.
    /// </summary>
    public static class ResourceManagerConfig
    {
        /// <summary>
        /// Extracts main and subobject keys if properly formatted
        /// </summary>
        /// <param name="keyObj">The key as an object.</param>
        /// <param name="mainKey">The key of the main asset.  This will be set to null if a sub key is not found.</param>
        /// <param name="subKey">The key of the sub object.  This will be set to null if not found.</param>
        /// <returns>Returns true if properly formatted keys are extracted.</returns>
        public static bool ExtractKeyAndSubKey(object keyObj, out string mainKey, out string subKey)
        {
            var key = keyObj as string;
            if (key != null)
            {
                var i = key.IndexOf('[');
                if (i > 0)
                {
                    var j = key.LastIndexOf(']');
                    if (j > i)
                    {
                        mainKey = key.Substring(0, i);
                        subKey = key.Substring(i + 1, j - (i + 1));
                        return true;
                    }
                }
            }

            mainKey = null;
            subKey = null;
            return false;
        }

        /// <summary>
        /// Check to see if a path is remote or not.
        /// </summary>
        /// <param name="path">The path to check.</param>
        /// <returns>Returns true if path is remote.</returns>
        public static bool IsPathRemote(string path)
        {
            return path != null && path.StartsWith("http", StringComparison.Ordinal);
        }

        /// <summary>
        /// Strips the query parameters of an url.
        /// </summary>
        /// <param name="path">The path to check.</param>
        /// <returns>Returns the path without query parameters.</returns>
        public static string StripQueryParameters(string path)
        {
            if (path != null)
            {
                var idx = path.IndexOf('?');
                if (idx >= 0)
                    return path.Substring(0, idx);
            }

            return path;
        }

        /// <summary>
        /// Check if path should use WebRequest.  A path should use WebRequest for remote paths and platforms that require WebRequest to load locally.
        /// </summary>
        /// <param name="path">The path to check.</param>
        /// <returns>Returns true if path should use WebRequest.</returns>
        public static bool ShouldPathUseWebRequest(string path)
        {
            if (PlatformCanLoadLocallyFromUrlPath() && File.Exists(path))
                return false;
            return path != null && path.Contains("://");
        }

        /// <summary>
        /// Checks if the current platform can use urls for load loads.
        /// </summary>
        /// <returns>True if the current platform can use urls for local loads, false otherwise.</returns>
        private static bool PlatformCanLoadLocallyFromUrlPath()
        {
            //For something so simple, this is pretty over engineered.  But, if more platforms come up that do this, it'll be easy to account for them.
            //Just add runtime platforms to this list that do the same thing Android does.
            List<RuntimePlatform> platformsThatUseUrlForLocalLoads = new List<RuntimePlatform>()
            {
                RuntimePlatform.Android
            };

            return platformsThatUseUrlForLocalLoads.Contains((Application.platform));
        }

        /// <summary>
        /// Used to create an operation result that has multiple items.
        /// </summary>
        /// <param name="type">The type of the result.</param>
        /// <param name="allAssets">The result objects.</param>
        /// <returns>Returns Array object with result items.</returns>
        public static Array CreateArrayResult(Type type, Object[] allAssets)
        {
            var elementType = type.GetElementType();
            if (elementType == null)
                return null;
            int length = 0;
            foreach (var asset in allAssets)
            {
                if (elementType.IsAssignableFrom(asset.GetType()))
                    length++;
            }

            var array = Array.CreateInstance(elementType, length);
            int index = 0;

            foreach (var asset in allAssets)
            {
                if (elementType.IsAssignableFrom(asset.GetType()))
                    array.SetValue(asset, index++);
            }

            return array;
        }

        /// <summary>
        /// Used to create an operation result that has multiple items.
        /// </summary>
        /// <typeparam name="TObject">The type of the result.</typeparam>
        /// <param name="allAssets">The result objects.</param>
        /// <returns>Returns result Array as TObject.</returns>
        public static TObject CreateArrayResult<TObject>(Object[] allAssets) where TObject : class
        {
            return CreateArrayResult(typeof(TObject), allAssets) as TObject;
        }

        /// <summary>
        /// Used to create an operation result that has multiple items.
        /// </summary>
        /// <param name="type">The type of the result objects.</param>
        /// <param name="allAssets">The result objects.</param>
        /// <returns>An IList of the resulting objects.</returns>
        public static IList CreateListResult(Type type, Object[] allAssets)
        {
            var genArgs = type.GetGenericArguments();
            var listType = typeof(List<>).MakeGenericType(genArgs);
            var list = Activator.CreateInstance(listType) as IList;
            var elementType = genArgs[0];
            if (list == null)
                return null;
            foreach (var a in allAssets)
            {
                if (elementType.IsAssignableFrom(a.GetType()))
                    list.Add(a);
            }

            return list;
        }

        /// <summary>
        /// Used to create an operation result that has multiple items.
        /// </summary>
        /// <typeparam name="TObject">The type of the result.</typeparam>
        /// <param name="allAssets">The result objects.</param>
        /// <returns>An IList of the resulting objects converted to TObject.</returns>
        public static TObject CreateListResult<TObject>(Object[] allAssets)
        {
            return (TObject)CreateListResult(typeof(TObject), allAssets);
        }

        /// <summary>
        /// Check if one type is an instance of another type.
        /// </summary>
        /// <typeparam name="T1">Expected base type.</typeparam>
        /// <typeparam name="T2">Expected child type.</typeparam>
        /// <returns>Returns true if T2 is a base type of T1.</returns>
        public static bool IsInstance<T1, T2>()
        {
            var tA = typeof(T1);
            var tB = typeof(T2);
#if !UNITY_EDITOR && UNITY_WSA_10_0 && ENABLE_DOTNET
            return tB.GetTypeInfo().IsAssignableFrom(tA.GetTypeInfo());
#else
            return tB.IsAssignableFrom(tA);
#endif
        }
    }

    [System.Flags]
    internal enum BundleSource
    {
        None = 0,
        Local = 1,
        Cache = 2,
        Download = 4
    }
}
