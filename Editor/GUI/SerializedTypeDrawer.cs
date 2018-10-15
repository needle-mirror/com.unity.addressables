using UnityEngine;
using System;
using UnityEditor.IMGUI.Controls;
using System.Collections.Generic;
using UnityEngine.AddressableAssets;
using System.Linq;
using UnityEngine.ResourceManagement;
using System.Reflection;

namespace UnityEditor.AddressableAssets
{
    [CustomPropertyDrawer(typeof(SerializedType), true)]
    public class SerializedTypeDrawer : PropertyDrawer
    {
        List<Type> m_types;
        FieldInfo m_fieldInfo;
        SerializedProperty m_property;
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            label.text = ObjectNames.NicifyVariableName(property.propertyPath);
            m_property = property;
            if (m_fieldInfo == null)
                m_fieldInfo = GetFieldInfo(property);
            if (m_types == null)
                m_types = GetTypes(m_fieldInfo);

            EditorGUI.BeginProperty(position, label, property);
            var smallPos = EditorGUI.PrefixLabel(position, label);
            var st = (SerializedType)m_fieldInfo.GetValue(property.serializedObject.targetObject);
            if (EditorGUI.DropdownButton(smallPos, new GUIContent(st.ToString()), FocusType.Keyboard))
            {
                var menu = new GenericMenu();
                for (int i = 0; i < m_types.Count; i++)
                {
                    var type = m_types[i];
                    menu.AddItem(new GUIContent(type.Name, ""), false, OnSetType, type);
                }
                menu.ShowAsContext();
            }

            EditorGUI.EndProperty();
        }

        void OnSetType(object context)
        {
            Undo.RecordObject(m_property.serializedObject.targetObject, "Set Serialized Type");
            var type = context as Type;
            m_fieldInfo.SetValue(m_property.serializedObject.targetObject, new SerializedType() { Value = type });
            EditorUtility.SetDirty(m_property.serializedObject.targetObject);
        }

        static FieldInfo GetFieldInfo(SerializedProperty property)
        {
            var o = property.serializedObject.targetObject;
            var t = o.GetType();
            string propertyName = property.name;
            int i = property.propertyPath.IndexOf('.');
            if (i > 0)
                propertyName = property.propertyPath.Substring(0, i);
            return t.GetField(propertyName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        }

        static List<Type> GetTypes(FieldInfo fieldInfo)
        {
            var attrs = fieldInfo.GetCustomAttributes(typeof(SerializedTypeRestrictionAttribute), false);
            if (attrs.Length == 0)
                return null;
            return AddressableAssetUtility.GetTypes((attrs[0] as SerializedTypeRestrictionAttribute).type);
        }
    }
}