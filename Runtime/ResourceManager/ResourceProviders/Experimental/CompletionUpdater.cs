using System;

namespace UnityEngine.ResourceManagement.ResourceProviders.Experimental
{
    class CompletionUpdater : MonoBehaviour
    {
        public Func<bool> operation;
        void Update()
        {
            if (operation())
                Destroy(gameObject);
        }

        public static void UpdateUntilComplete(string name, Func<bool> func)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullException("name");
            if (func == null)
                throw new ArgumentNullException("func");

            new GameObject(name).AddComponent<CompletionUpdater>().operation = func;
        }
    }
}
