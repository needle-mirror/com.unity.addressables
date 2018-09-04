namespace UnityEngine.AddressableAssets
{
    /// <summary>
    /// Reference to an asset label.  This class can be used in scripts as a field and will use a CustomPropertyDrawer to provide a DropDown UI of available labels.
    /// </summary>
    [System.Serializable]
    public class AssetLabelReference
    {
        [SerializeField]
        private string m_labelString;
        /// <summary>
        /// The label string.
        /// </summary>
        public string labelString
        {
            get { return m_labelString; }
            set { m_labelString = value; }
        }
    }
}
