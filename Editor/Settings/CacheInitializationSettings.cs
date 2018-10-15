using UnityEngine.ResourceManagement;

namespace UnityEngine.AddressableAssets
{
    /// <summary>
    /// Asset container for CacheInitializationData.
    /// </summary>
    [CreateAssetMenu(fileName = "CacheInitializationSettings.asset", menuName = "Addressable Assets/Initialization/Cache Initialization Settings")]
    public class CacheInitializationSettings : ScriptableObject, IObjectInitializationDataProvider
    {
        [SerializeField]
        CacheInitializationData m_data = new CacheInitializationData();
        public string Name { get { return "Asset Bundle Cache Settings"; } }
        /// <summary>
        /// The cache initialization data that will be serialized and applied during Addressables initialization.
        /// </summary>
        public CacheInitializationData Data
        {
            get
            {
                return m_data;
            }
            set
            {
                m_data = value;
            }
        }

        /// <summary>
        /// Create initialization data to be serialized into the Addressables runtime data.
        /// </summary>
        /// <returns>The serialized data for the initialization class and the data.</returns>
        public ObjectInitializationData CreateObjectInitializationData()
        {
            return ObjectInitializationData.CreateSerializedInitializationData<CacheInitialization>(typeof(CacheInitialization).Name, m_data);
        }
    }
}