using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
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
    }


    /// <summary>
    /// Interface for objects that can create object initialization data.
    /// </summary>
    public interface IObjectInitializationDataProvider
    {
        string Name { get; }
        ObjectInitializationData CreateObjectInitializationData();
    }



    /// <summary>
    /// Attribute for restricting which types can be assigned to a SerializedType
    /// </summary>
    public class SerializedTypeRestrictionAttribute : Attribute
    {
        public Type type;
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
        public string AssemblyName { get { return m_AssemblyName; } }

        [FormerlySerializedAs("m_className")]
        [SerializeField]
        string m_ClassName;
        /// <summary>
        /// The name of the type.
        /// </summary>
        public string ClassName { get { return m_ClassName; } }

        Type m_CachedType;

        /// <inheritdoc/>
        public override string ToString()
        {
            return Value.Name;
        }

        /// <summary>
        /// Get and set the serialized type.
        /// </summary>
        public Type Value
        {
            get
            {
                if (string.IsNullOrEmpty(m_AssemblyName) || string.IsNullOrEmpty(m_ClassName))
                    return null;

                if (m_CachedType == null)
                {
                    var assembly = Assembly.Load(m_AssemblyName);
                    if(assembly != null)
                        m_CachedType = assembly.GetType(m_ClassName);
                }
                return m_CachedType;
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
        public string Id { get { return m_Id; } internal set { m_Id = value; } }

        [FormerlySerializedAs("m_objectType")]
        [SerializeField]
        SerializedType m_ObjectType;
        /// <summary>
        /// The object type that will be created.
        /// </summary>
        public SerializedType ObjectType { get { return m_ObjectType; } }

        [FormerlySerializedAs("m_data")]
        [SerializeField]
        string m_Data;
        /// <summary>
        /// String representation of the data that will be passed to the IInitializableObject.Initialize method of the created object.  This is usually a JSON string of the serialized data object.
        /// </summary>
        public string Data { get { return m_Data; } internal set { m_Data = value; } }
#pragma warning restore 0649 

        /// <inheritdoc/>
        public override string ToString()
        {
            return string.Format("ObjectInitializationData: id={0}, type={1}", m_Id, m_ObjectType);
        }

        /// <summary>
        /// Create an instance of the defined object.  Initialize will be called on it with the id and data if it implements the IInitializableObject interface.
        /// </summary>
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
                m_ObjectType = new SerializedType { Value = objectType },
                m_Id = string.IsNullOrEmpty(id) ? objectType.FullName : id,
                m_Data = dataObject == null ? null : JsonUtility.ToJson(dataObject),
                m_RuntimeTypes = dataObject == null ? null : new[] { dataObject.GetType() }
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

    static class ResourceManagerConfig
    {
        public static TObject CreateArrayResult<TObject>(Object[] allAssets) where TObject : class
        {
            var t = typeof(TObject);
            var elementType = t.GetElementType();
            if (elementType  == null)
                return null;
            int length = 0;
            foreach (var asset in allAssets)
            {
                if (asset.GetType() == elementType)
                    length++;
            }
            var array = Array.CreateInstance(elementType, length);
            int index = 0;
            
            foreach (var asset in allAssets)
            {
                if(asset.GetType() == elementType)
                    array.SetValue(asset, index++);
            }
            return array as TObject;
        }

        public static TObject CreateListResult<TObject>(Object[] allAssets) where TObject : class
        {
            var t = typeof(TObject);
            var listType = typeof(List<>).MakeGenericType(t.GetGenericArguments());
            var list = Activator.CreateInstance(listType) as IList;
            if (list == null)
                return null;
            foreach (var a in allAssets)
                list.Add(a);
            return list as TObject;
        }

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
}
