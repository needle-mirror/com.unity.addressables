using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement;

namespace UnityEditor.AddressableAssets
{
    [Serializable]
    public class AddressableAssetGroupSchemaTemplate
    {
        [SerializeField]
        string m_displayName;
        [SerializeField]
        string m_description;
        [SerializeField]
        List<SerializedType> m_schemaTypes;

        public string DisplayName
        {
            get { return m_displayName; }
        }
         
        public string Description
        {
            get { return m_description; }
        }

        public Type[] GetTypes()
        {
            var types = new Type[m_schemaTypes.Count];
            for (int i = 0; i < types.Length; i++)
                types[i] = m_schemaTypes[i].Value;
            return types;
        }

        public static AddressableAssetGroupSchemaTemplate Create(string name, string descr, params Type[] types)
        {
            var st = new AddressableAssetGroupSchemaTemplate() { m_displayName = name, m_description = descr };
            st.m_schemaTypes = new List<SerializedType>(types.Length);
            for (int i = 0; i < types.Length; i++)
                st.m_schemaTypes.Add(new SerializedType() { Value = types[i] });
            return st;
        }
    }
}