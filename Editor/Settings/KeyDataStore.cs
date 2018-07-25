using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
namespace UnityEditor.AddressableAssets
{
    [Serializable]
    public class KeyDataStore : ISerializationCallbackReceiver
    {
        [Serializable]
        internal struct Entry
        {
            [SerializeField] string m_assemblyName;
            public string AssemblyName { get { return m_assemblyName; } set { m_assemblyName = value; } }
            [SerializeField] string m_className;
            public string ClassName { get { return m_className; } set { m_className = value; } }
            [SerializeField] string m_data;
            public string Data { get { return m_data; } set { m_data = value; } }
            [SerializeField] string m_key;
            public string Key { get { return m_key; } set { m_key = value; } }

            public override string ToString()
            {
                return string.Format("{0} ({1})", Data, ClassName);
            }
        }

        internal void Reset()
        {
            m_serializedData = null;
            m_entryMap = new Dictionary<string, object>();
        }

        [SerializeField]
        List<Entry> m_serializedData;
        Dictionary<string, object> m_entryMap = new Dictionary<string, object>();
        public Action<string, object, bool> OnSetData { get; set; }

        public void OnBeforeSerialize()
        {
            m_serializedData = new List<Entry>(m_entryMap.Count);
            foreach (var k in m_entryMap)
                m_serializedData.Add(CreateEntry(k.Key, k.Value));
        }

        private Entry CreateEntry(string key, object value)
        {
            var entry = new Entry();
            entry.Key = key;
            var objType = value.GetType();
            entry.AssemblyName = objType.Assembly.FullName;
            entry.ClassName = objType.FullName;
            try
            {
                if (objType == typeof(string))
                {
                    entry.Data = value as string;
                }
                else if (objType.IsEnum)
                {
                    entry.Data = value.ToString();
                }
                else
                {
                    var parseMethod = objType.GetMethod("Parse", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public, null, System.Reflection.CallingConventions.Any, new Type[] { typeof(string) }, null);
                    if (parseMethod == null || parseMethod.ReturnType != objType)
                        entry.Data = JsonUtility.ToJson(value);
                    else
                        entry.Data = value.ToString();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarningFormat("KeyDataStore unable to serizalize entry {0} with value {1}, exception: {2}", key, value, ex);
            }
            return entry;
        }

        private object CreateObject(Entry e)
        {
            try
            {
                var assembly = System.Reflection.Assembly.Load(e.AssemblyName);
                var objType = assembly.GetType(e.ClassName);
                if (objType == typeof(string))
                    return e.Data;
                if (objType.IsEnum)
                    return Enum.Parse(objType, e.Data);
                var parseMethod = objType.GetMethod("Parse", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public, null, System.Reflection.CallingConventions.Any, new Type[] { typeof(string) }, null);
                if (parseMethod == null || parseMethod.ReturnType != objType)
                    return JsonUtility.FromJson(e.Data, objType);
                return parseMethod.Invoke(null, new object[] { e.Data });
            }
            catch (Exception ex)
            {
                Debug.LogWarningFormat("KeyDataStore unable to deserizalize entry {0} from assembly {1} of type {2}, exception: {3}", e.Key, e.AssemblyName, e.ClassName, ex);
                return null;
            }
        }

        public void OnAfterDeserialize()
        {
            m_entryMap = new Dictionary<string, object>(m_serializedData.Count);
            foreach (var e in m_serializedData)
                m_entryMap.Add(e.Key, CreateObject(e));
            m_serializedData = null;
        }

        public IEnumerable<string> Keys { get { return m_entryMap.Keys; } }

        public void SetData(string key, object data)
        {
            var isNew = m_entryMap.ContainsKey(key);
            m_entryMap[key] = data;
            if (OnSetData != null)
                OnSetData(key, data, isNew);
        }

        public void SetDataFromString(string key, string data)
        {
            var existingType = GetDataType(key);
            if(existingType == null)
                SetData(key, data);
            SetData(key, CreateObject(new Entry() { AssemblyName = existingType.Assembly.FullName, ClassName = existingType.FullName, Data = data, Key = key }));
        }

        public Type GetDataType(string key)
        {
            object val;
            if (m_entryMap.TryGetValue(key, out val))
                return val.GetType();
            return null;

        }

        public string GetDataString(string key, string defaultValue)
        {
            object val;
            if (m_entryMap.TryGetValue(key, out val))
                return CreateEntry(key, val).ToString();
            return defaultValue;
        }

        public T GetData<T>(string key, T defaultValue, bool addDefault = false)
        {
            try
            {
                object val;
                if (m_entryMap.TryGetValue(key, out val))
                    return (T)val;
            }
            catch (Exception) { }

            if (addDefault)
                SetData(key, defaultValue);
            return defaultValue;
        }

    }
}