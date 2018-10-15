using UnityEngine;

namespace UnityEditor.AddressableAssets
{
    [CustomPropertyDrawer(typeof(ProfileValueReference), true)]
    internal class ProfileValueReferenceDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
                return;
            EditorGUI.BeginProperty(position, label, property);
            var idProp = property.FindPropertyRelative("m_id");
           
            var newId = ProfilesEditor.ValueGUI(position, settings, label.text, idProp.stringValue);
           
            if (newId != idProp.stringValue)
                idProp.stringValue = newId;
            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
                return 0;
            var idProp = property.FindPropertyRelative("m_id");
            return ProfilesEditor.CalcGUIHeight(settings, label.text, idProp.stringValue);
        }
    }
}
