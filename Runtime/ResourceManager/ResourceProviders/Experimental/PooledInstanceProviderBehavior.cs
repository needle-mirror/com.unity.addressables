using System;

namespace UnityEngine.ResourceManagement.ResourceProviders.Experimental
{
    class PooledInstanceProviderBehavior : MonoBehaviour
    {
        PooledInstanceProvider m_Provider;
        public void Init(PooledInstanceProvider p)
        {
            m_Provider = p;
            DontDestroyOnLoad(gameObject);
        }

        void Update()
        {
            m_Provider.Update();
        }
    }
}
