namespace AddressableAssets.DocSampleCode
{
    #region doc_Preload
    using System.Collections;
    using UnityEngine;
    using UnityEngine.AddressableAssets;

    public class Preload : MonoBehaviour
    {
        public IEnumerator Start() {
            yield return Addressables.DownloadDependenciesAsync("preload", true);
        }
    }
    #endregion
}