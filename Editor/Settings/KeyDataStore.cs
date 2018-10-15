using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.AddressableAssets;

namespace UnityEditor.AddressableAssets
{
    /// <summary>
    /// Contains serialized data in a generic serializable container.
    /// </summary>
    [Serializable]
    public class KeyDataStore : ISerializationCallbackReceiver
    {
        [Serializable]
        internal struct Entry
        {
            [SerializeField] string m_assemblyName;
            internal string AssemblyName { get { return m_assemblyName; } set { m_assemblyName = value; } }
            [SerializeField] string m_className;
            internal string ClassName { get { return m_className; } set { m_className = value; } }
            [SerializeField] string m_data;
            internal string Data { get { return m_data; } set { m_data = value; } }
            [SerializeField] string m_key;
            internal string Key { get { return m_key; } set { m_key = value; } }

            /// <inheritdoc/>
            public override string ToString()
            {
                return Data;
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
        /// <summary>
        /// Delegate that is invoked when data is modified.
        /// </summary>
        public Action<string, object, bool> OnSetData { get; set; }

        /// <summary>
        /// Implementation of ISerializationCallbackReceiver interface, used to convert data to a serializable form.
        /// </summary>
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
                Addressables.LogWarningFormat("KeyDataStore unable to serizalize entry {0} with value {1}, exception: {2}", key, value, ex);
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
                Addressables.LogWarningFormat("KeyDataStore unable to deserizalize entry {0} from assembly {1} of type {2}, exception: {3}", e.Key, e.AssemblyName, e.ClassName, ex);
                return null;
            }
        }

        /// <summary>
        /// Implementation of ISerializationCallbackReceiver interface, used to convert data from its serializable form.
        /// </summary>
        public void OnAfterDeserialize()
        {
            m_entryMap = new Dictionary<string, object>(m_serializedData.Count);
            foreach (var e in m_serializedData)
                m_entryMap.Add(e.Key, CreateObject(e));
            m_serializedData = null;
        }

        /// <summary>
        /// The collection of keys stored.
        /// </summary>
        public IEnumerable<string> Keys { get { return m_entryMap.Keys; } }

        /// <summary>
        /// Set the value of a specified key.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="data">The data to store.  Supported types are strings, POD types, objects that have a static method named 'Parse' that convert a string to an object, and object that are serializable via JSONUtilty.</param>
        public void SetData(string key, object data)
        {
            var isNew = m_entryMap.ContainsKey(key);
            m_entryMap[key] = data;
            if (OnSetData != null)
                OnSetData(key, data, isNew);
        }

        /// <summary>
        /// Set data for a specified key from a string.
        /// </summary>
        /// <param name="key">The data key.</param>
        /// <param name="data">The data string value.</param>
        public void SetDataFromString(string key, string data)
        {
            var existingType = GetDataType(key);
            if (existingType == null)
                SetData(key, data);
            SetData(key, CreateObject(new Entry() { AssemblyName = existingType.Assembly.FullName, ClassName = existingType.FullName, Data = data, Key = key }));
        }

        internal Type GetDataType(string key)
        {
            object val;
            if (m_entryMap.TryGetValue(key, out val))
                return val.GetType();
            return null;

        }

        internal string GetDataString(string key, string defaultValue)
        {
            object val;
            if (m_entryMap.TryGetValue(key, out val))
                return CreateEntry(key, val).ToString();
            return defaultValue;
        }

        /// <summary>
        /// Get data via a specified key.
        /// </summary>
        /// <typeparam name="T">The data type.</typeparam>
        /// <param name="key">The key.</param>
        /// <param name="defaultValue">The default value to return if the data is not found.</param>
        /// <param name="addDefault">Optional parameter to control whether to add the default value if the data is not found.</param>
        /// <returns></returns>
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

        internal void OnGUI(AddressableAssetGroup parent)
        {
            List<string> keys = new List<string>(Keys); //copy key list to avoid dictionary errors.
            foreach (var key in keys)
            {
                var itemType = m_entryMap[key].GetType();
                if (itemType == typeof(string))
                {
                    string currValue = m_entryMap[key].ToString();
                    var newValue = ProfilesEditor.ValueGUILayout(parent.Settings, key, currValue);
                    if (newValue != currValue)
                    {
                        SetDataFromString(key, newValue);
                    }
                }
                else
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PrefixLabel(key);
                    //do custom UI for type...
                    if (itemType.IsEnum)
                    {
                        var currValue = m_entryMap[key] as System.Enum;
                        var newValue = EditorGUILayout.EnumPopup(currValue);
                        if (currValue != newValue)
                        {
                            SetData(key, newValue);
                        }
                    }
                    else if (itemType.IsPrimitive)
                    {
                        var currValue = m_entryMap[key];
                        if (itemType == typeof(bool))
                        {
                            var newValue = EditorGUILayout.Toggle((bool)currValue);
                            if (newValue != (bool)currValue)
                                SetData(key, newValue);
                        }
                        else if (itemType == typeof(int))
                        {
                            var newValue = EditorGUILayout.IntField((int)currValue);
                            if (newValue != (int)currValue)
                                SetData(key, newValue);
                        }
                        else if (itemType == typeof(float))
                        {
                            var newValue = EditorGUILayout.FloatField((float)currValue);
                            if (newValue != (float)currValue)
                                SetData(key, newValue);
                        }
                        else
                        {
                            var newValue = EditorGUILayout.DelayedTextField(currValue.ToString());
                            if (newValue != currValue.ToString())
                                SetDataFromString(key, newValue);
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }
        }

    }
}