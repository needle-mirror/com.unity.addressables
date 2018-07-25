using System.Collections.Generic;
using UnityEngine.ResourceManagement;

namespace UnityEngine.AddressableAssets
{
   
    /// <summary>
    /// TODO - doc
    /// </summary>
    [System.Serializable]
    public class AssetLabelReference
    {
        [SerializeField]
        private string m_labelString;
        public string labelString
        {
            get { return m_labelString; }
            set { m_labelString = value; }
        }
    }
}
