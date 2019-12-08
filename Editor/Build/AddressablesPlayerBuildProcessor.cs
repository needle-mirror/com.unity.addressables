using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.AddressableAssets;

public class AddressablesPlayerBuildProcessor : IPreprocessBuildWithReport, IPostprocessBuildWithReport
{
    public int callbackOrder
    {
        get { return 1; }
    }

    public void OnPostprocessBuild(BuildReport report)
    {
        CleanTemporaryPlayerBuildData();
    }
    
    [InitializeOnLoadMethod]
    static void CleanTemporaryPlayerBuildData()
    {
        string addressablesStreamingAssets = Path.Combine(Application.streamingAssetsPath, Addressables.StreamingAssetsSubFolder);
        if (Directory.Exists(addressablesStreamingAssets))
        {
            Debug.Log(string.Format("Deleting Addressables data from {0}.", addressablesStreamingAssets));
            Directory.Delete(addressablesStreamingAssets, true);
            //Will delete the directory only if it's empty
            DirectoryUtility.DeleteDirectory(Application.streamingAssetsPath);
        }
    }

    public void OnPreprocessBuild(BuildReport report)
    {
        if (Directory.Exists(Addressables.BuildPath))
        {
            Debug.Log(string.Format(
                "Copying Addressables data from {0} to {1}.  These copies will be deleted at the end of the build.",
                Addressables.BuildPath, Addressables.PlayerBuildDataPath));

            DirectoryUtility.DirectoryCopy(Addressables.BuildPath, Addressables.PlayerBuildDataPath, true);
        }
    }
}
