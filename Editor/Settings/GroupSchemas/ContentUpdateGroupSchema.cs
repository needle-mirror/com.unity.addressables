using System;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;
using UnityEngine.Serialization;

namespace UnityEditor.AddressableAssets.Settings.GroupSchemas
{
    /// <summary>
    /// Schema for content updates.
    /// </summary>
    //  [CreateAssetMenu(fileName = "ContentUpdateGroupSchema.asset", menuName = "Addressables/Group Schemas/Content Update")]
    [DisplayName("Content Update Restriction")]
    public class ContentUpdateGroupSchema : AddressableAssetGroupSchema
    {
        enum ContentType
        {
            CanChangePostRelease,
            CannotChangePostRelease
        }

        [FormerlySerializedAs("m_staticContent")]
        [SerializeField]
        bool m_StaticContent;
        /// <summary>
        /// Is the group static.  This property is used in determining which assets need to be moved to a new remote group during the content update process.
        /// </summary>
        public bool StaticContent
        {
            get { return m_StaticContent; }
            set
            {
                m_StaticContent = value;
                SetDirty(true);
            }
        }

        private GUIContent m_UpdateRestrictionGUIContent = new GUIContent("Prevent Updates", "Assets in Prevent Update groups will be moved to a new remote group during the content update process");

        /// <inheritdoc/>
        public override void OnGUI()
        {
            var staticContent = EditorGUILayout.Toggle(m_UpdateRestrictionGUIContent, m_StaticContent);
            if (staticContent != m_StaticContent)
                StaticContent = staticContent;
        }

        /// <inheritdoc/>
        public override void OnGUIMultiple(List<AddressableAssetGroupSchema> otherSchemas)
        {
            var so = new SerializedObject(this);
            var prop = so.FindProperty("m_StaticContent");

            // Type/Static Content
            ShowMixedValue(prop, otherSchemas, typeof(bool), "m_StaticContent");
            EditorGUI.BeginChangeCheck();
            
            var staticContent = EditorGUILayout.Toggle(m_UpdateRestrictionGUIContent, m_StaticContent);

            if (EditorGUI.EndChangeCheck())
            {
                StaticContent = staticContent;
                foreach (var s in otherSchemas)
                    (s as ContentUpdateGroupSchema).StaticContent = StaticContent;
            }
            EditorGUI.showMixedValue = false;

            so.ApplyModifiedProperties();
        }
    }
}
