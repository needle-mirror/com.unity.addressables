using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

internal static class DirectoryUtility
{
    internal static void DeleteDirectory(string directoryPath, bool onlyIfEmpty = true, bool recursiveDelete = true)
    {
        if (!Directory.Exists(directoryPath))
            return;

        bool isEmpty = !Directory.EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories).Any()
            && !Directory.EnumerateDirectories(directoryPath, "*", SearchOption.AllDirectories).Any();
        if (!onlyIfEmpty || isEmpty)
        {
            // check if the folder is valid in the AssetDatabase before deleting through standard file system
            string relativePath = directoryPath.Replace("\\", "/").Replace(Application.dataPath, "Assets");
            if (AssetDatabase.IsValidFolder(relativePath))
                AssetDatabase.DeleteAsset(relativePath);
            else
                Directory.Delete(directoryPath, recursiveDelete);
        }
    }

    internal static void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs)
    {
        // Get the subdirectories for the specified directory.
        DirectoryInfo dir = new DirectoryInfo(sourceDirName);

        if (!dir.Exists)
        {
            throw new DirectoryNotFoundException(
                "Source directory does not exist or could not be found: "
                + sourceDirName);
        }

        DirectoryInfo[] dirs = dir.GetDirectories();
        // If the destination directory doesn't exist, create it.
        if (!Directory.Exists(destDirName))
            Directory.CreateDirectory(destDirName);

        // Get the files in the directory and copy them to the new location.
        FileInfo[] files = dir.GetFiles();
        foreach (FileInfo file in files)
        {
            string temppath = Path.Combine(destDirName, file.Name);
            file.CopyTo(temppath, true);
        }

        // If copying subdirectories, copy them and their contents to new location.
        if (copySubDirs)
        {
            foreach (DirectoryInfo subdir in dirs)
            {
                string temppath = Path.Combine(destDirName, subdir.Name);
                DirectoryCopy(subdir.FullName, temppath, true);
            }
        }
    }
}
