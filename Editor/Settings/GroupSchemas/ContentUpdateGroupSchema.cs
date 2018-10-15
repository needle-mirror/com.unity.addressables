using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor.AddressableAssets
{
    /// <summary>
    /// Schema for content updates.
    /// </summary>
    [CreateAssetMenu(fileName = "ContentUpdateGroupSchema.asset", menuName = "Addressable Assets/Group Schemas/Content Update")]
    public class ContentUpdateGroupSchema : AddressableAssetGroupSchema
    {
        [SerializeField]
        private bool m_staticContent = false;
        /// <summary>
        /// Is the group static.  This property is used in determining which assets need to be moved to a new remote group during the content update process.
        /// </summary>
        public bool StaticContent
        {
            get { return m_staticContent; }
            set
            {
                m_staticContent = value;
                SetDirty(true);
            }
        }
    }
}