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
    /// Restores temporary data created as part of a build.
    /// </summary>
    /// <param name="report">Stores temporary player build data.</param>
    public void OnPostprocessBuild(BuildReport report)
    {
        CleanTemporaryPlayerBuildData();
    }

    [InitializeOnLoadMethod]
    internal static void CleanTemporaryPlayerBuildData()
    {
        if (Directory.Exists(Addressables.PlayerBuildDataPath))
        {
            DirectoryUtility.DirectoryMove(Addressables.PlayerBuildDataPath, Addressables.BuildPath);
            DirectoryUtility.DeleteDirectory(Application.streamingAssetsPath, onlyIfEmpty: true);
        }
    }

    ///<summary>
    /// Initializes temporary build data.
    /// </summary>
    /// <param name="report">Contains build data information.</param>
    public void OnPreprocessBuild(BuildReport report)
    {
        CopyTemporaryPlayerBuildData();
    }

    internal static void CopyTemporaryPlayerBuildData()
    {
        if (Directory.Exists(Addressables.BuildPath))
        {
            if (Directory.Exists(Addressables.PlayerBuildDataPath))
            {
                Debug.LogWarning($"Found and deleting directory \"{Addressables.PlayerBuildDataPath}\", directory is managed through Addressables.");
                DirectoryUtility.DeleteDirectory(Addressables.PlayerBuildDataPath, false);
            }

            string parentDir = Path.GetDirectoryName(Addressables.PlayerBuildDataPath);
            if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
                Directory.CreateDirectory(parentDir);
            Directory.Move(Addressables.BuildPath, Addressables.PlayerBuildDataPath );
        }
    }
}
