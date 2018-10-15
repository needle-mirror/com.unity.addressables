using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace UnityEditor.AddressableAssets
{
    /// <summary>
    /// Contains data for AddressableAssetGroups. 
    /// </summary>
    public class AddressableAssetGroupSchema : ScriptableObject
    {
        [HideInInspector]
        [SerializeField]
        private AddressableAssetGroup m_group;

        /// <summary>
        /// Get the group that the schema belongs to.
        /// </summary>
        public AddressableAssetGroup Group
        {
            get
            {
                return m_group;
            }
            internal set
            {
                m_group = value;
                if(m_group != null)
                    OnSetGroup(m_group);
            }
        }

        /// <summary>
        /// Override this method to perform post creation initialization.
        /// </summary>
        /// <param name="group">The group that the schema is added to.</param>
        protected virtual void OnSetGroup(AddressableAssetGroup group)
        {
        }

        /// <summary>
        /// Used to notify the addressables settings that data has been modified.  This must be called by subclasses to ensure proper cache invalidation.
        /// </summary>
        protected void SetDirty(bool postEvent)
        {
            if (m_group != null)
            {
                if (m_group.Settings != null && m_group.Settings.IsPersisted)
                    EditorUtility.SetDirty(this);
                if (m_group != null)
                    m_group.SetDirty(AddressableAssetSettings.ModificationEvent.GroupSchemaModified, this, postEvent);
            }
        }
    }
}