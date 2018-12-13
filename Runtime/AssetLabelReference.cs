using System;
using UnityEngine.Serialization;

namespace UnityEngine.AddressableAssets
{
    /// <summary>
    /// Reference to an asset label.  This class can be used in scripts as a field and will use a CustomPropertyDrawer to provide a DropDown UI of available labels.
    /// </summary>
    [Serializable]
    public class AssetLabelReference
    {
        [FormerlySerializedAs("m_labelString")]
        [SerializeField]
        string m_LabelString;
        /// <summary>
        /// The label string.
        /// </summary>
        public string labelString
        {
            get { return m_LabelString; }
            set { m_LabelString = value; }
        }
    }
}
