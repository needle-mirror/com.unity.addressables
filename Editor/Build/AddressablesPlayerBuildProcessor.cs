using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.AddressableAssets;

/// <summary>
/// Maintains Addresssables build data when processing a player build.
/// </summary>
public class AddressablesPlayerBuildProcessor : IPreprocessBuildWithReport, IPostprocessBuildWithReport
{
    /// <summary>
    /// Returns the player build processor callback order.
    /// </summary>
    public int callbackOrder
    {
        get { return 1; }
    }

    /// <summary>
    /// Removes temporary data created as part of a build.
    /// </summary>
    public void OnPostprocessBuild(BuildReport report)
    {
        CleanTemporaryPlayerBuildData();
    }

    [InitializeOnLoadMethod]
    internal static void CleanTemporaryPlayerBuildData()
    {
        if (Directory.Exists(Addressables.PlayerBuildDataPath))
        {
            Debug.Log(string.Format("Deleting Addressables data from {0}.", Addressables.PlayerBuildDataPath));
            DirectoryUtility.DeleteDirectory(Addressables.PlayerBuildDataPath, false);
            //Will delete the directory only if it's empty
            DirectoryUtility.DeleteDirectory(Application.streamingAssetsPath);
        }
    }

    ///<summary>
    /// Initializes temporary build data.
    /// </summary>
    public void OnPreprocessBuild(BuildReport report)
    {
        CopyTemporaryPlayerBuildData();
    }

    internal static void CopyTemporaryPlayerBuildData()
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
