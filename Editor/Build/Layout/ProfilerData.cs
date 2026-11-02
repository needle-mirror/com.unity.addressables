using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace UnityEditor.AddressableAssets.Build.Layout
{
    /// <summary>
    /// The per-object data of a build, split out of the build layout; only the Addressables Profiler reads it.
    /// Written next to the layout, which records its name in ProfilerFileName: a header line, then one json record per
    /// file, so no single string holds every object. Objects attach to the layout by file name and asset id.
    /// </summary>
    internal static class ProfilerData
    {
        internal const int k_Version = 1;

        const string k_HeaderPrefix = "{\"ProfilerFileVersion\":";

        [Serializable]
        internal class Header
        {
            public int ProfilerFileVersion;
            public string BuildSessionGuidString;
            public string LayoutFileName;
        }

        [Serializable]
        internal class FileObjects
        {
            public string Name;
            public List<AssetObjects> Assets = new List<AssetObjects>();
            public List<AssetObjects> OtherAssets = new List<AssetObjects>();
        }

        [Serializable]
        internal class AssetObjects
        {
            public string Guid;
            public List<BuildLayout.ObjectData> Objects = new List<BuildLayout.ObjectData>();
        }

        // Writes the layout's objects to their own file and leaves each asset with an empty list, so the layout can then
        // be written without them. Also records the file's name on the layout.
        internal static void MoveToFile(BuildLayout layout, string destinationPath, string layoutPath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));

            using (StreamWriter writer = new StreamWriter(File.Open(destinationPath, FileMode.Create)))
            {
                Header header = new Header
                {
                    ProfilerFileVersion = k_Version,
                    BuildSessionGuidString = layout.AddressablesBuildSessionGUID.ToString(),
                    LayoutFileName = Path.GetFileName(layoutPath)
                };
                writer.WriteLine(JsonUtility.ToJson(header));

                HashSet<BuildLayout.Bundle> written = new HashSet<BuildLayout.Bundle>();
                foreach (BuildLayout.Bundle bundle in BuildLayoutHelpers.EnumerateBundles(layout))
                {
                    if (bundle == null || !written.Add(bundle))
                        continue;
                    foreach (BuildLayout.File file in bundle.Files)
                        writer.WriteLine(JsonUtility.ToJson(MoveToRecord(file)));
                }
            }
            layout.ProfilerFileName = Path.GetFileName(destinationPath);
        }

        static FileObjects MoveToRecord(BuildLayout.File file)
        {
            FileObjects record = new FileObjects { Name = file.Name };
            foreach (BuildLayout.ExplicitAsset asset in file.Assets)
            {
                if (asset?.Objects == null || asset.Objects.Count == 0)
                    continue;
                record.Assets.Add(new AssetObjects { Guid = asset.Guid, Objects = asset.Objects });
                asset.Objects = new List<BuildLayout.ObjectData>();
            }
            foreach (BuildLayout.DataFromOtherAsset otherAsset in file.OtherAssets)
            {
                if (otherAsset?.Objects == null || otherAsset.Objects.Count == 0)
                    continue;
                record.OtherAssets.Add(new AssetObjects { Guid = otherAsset.AssetGuid, Objects = otherAsset.Objects });
                otherAsset.Objects = new List<BuildLayout.ObjectData>();
            }
            return record;
        }

        // both files sit in the same folder and name each other
        internal static string GetProfilerPath(BuildLayout layout)
        {
            if (string.IsNullOrEmpty(layout.ProfilerFileName) || string.IsNullOrEmpty(layout.m_FilePath))
                return null;
            return Path.Combine(Path.GetDirectoryName(layout.m_FilePath), layout.ProfilerFileName);
        }

        internal static string GetLayoutPath(string profilerPath)
        {
            try
            {
                string headerLine;
                using (StreamReader reader = new StreamReader(profilerPath))
                    headerLine = reader.ReadLine();
                Header header = JsonUtility.FromJson<Header>(headerLine);
                if (string.IsNullOrEmpty(header?.LayoutFileName))
                    return null;
                return Path.Combine(Path.GetDirectoryName(profilerPath), header.LayoutFileName);
            }
            catch (Exception)
            {
                return null;
            }
        }

        internal static bool IsProfilerFile(string path)
        {
            try
            {
                using (StreamReader reader = new StreamReader(path))
                {
                    char[] buffer = new char[k_HeaderPrefix.Length];
                    int total = 0;
                    while (total < buffer.Length)
                    {
                        int read = reader.Read(buffer, total, buffer.Length - total);
                        if (read == 0)
                            return false;
                        total += read;
                    }
                    return new string(buffer) == k_HeaderPrefix;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        // Fills in objects for a layout written without them. Layouts from before the split have no ProfilerFileName.
        internal static bool TryAttachObjects(BuildLayout layout)
        {
            if (layout.m_ObjectsAttached)
                return true;
            string path = GetProfilerPath(layout);
            if (path == null)
                return false;
            if (!File.Exists(path))
            {
                Debug.LogWarning($"Build layout profiler file {layout.ProfilerFileName} was not found next to {layout.m_FilePath}, object details are unavailable.");
                return false;
            }

            try
            {
                using (StreamReader reader = new StreamReader(path))
                {
                    Header header = JsonUtility.FromJson<Header>(reader.ReadLine());
                    if (!MatchesLayout(header, layout))
                    {
                        Debug.LogWarning($"Build layout profiler file at {path} is from a different build than {layout.m_FilePath}, object details are unavailable.");
                        return false;
                    }

                    // file names are unique across a build
                    Dictionary<string, BuildLayout.File> files = new Dictionary<string, BuildLayout.File>();
                    foreach (BuildLayout.Bundle bundle in BuildLayoutHelpers.EnumerateBundles(layout))
                    {
                        if (bundle == null)
                            continue;
                        foreach (BuildLayout.File file in bundle.Files)
                        {
                            if (file != null && !string.IsNullOrEmpty(file.Name))
                                files[file.Name] = file;
                        }
                    }

                    int unmatched = 0;
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (line.Length == 0)
                            continue;
                        FileObjects record = JsonUtility.FromJson<FileObjects>(line);
                        if (record?.Name == null || !files.TryGetValue(record.Name, out BuildLayout.File file))
                        {
                            unmatched++;
                            continue;
                        }
                        unmatched += Attach(record, file);
                    }

                    if (unmatched > 0)
                        Debug.LogWarning($"{unmatched} entries in the build layout profiler file at {path} don't match {layout.m_FilePath} and were skipped.");
                    layout.m_ObjectsAttached = true;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to read the build layout profiler file at {path}: {e.Message}");
                return false;
            }

            return true;
        }

        static bool MatchesLayout(Header header, BuildLayout layout)
        {
            if (header == null || header.ProfilerFileVersion < 1 || header.ProfilerFileVersion > k_Version)
                return false;
            // unique per build, unlike BuildResultHash, which repeats when content is unchanged
            return !string.IsNullOrEmpty(header.BuildSessionGuidString) &&
                string.Equals(header.BuildSessionGuidString, layout.BuildSessionGuidString, StringComparison.OrdinalIgnoreCase);
        }

        static int Attach(FileObjects record, BuildLayout.File file)
        {
            int unmatched = 0;
            Dictionary<string, BuildLayout.ExplicitAsset> assets = new Dictionary<string, BuildLayout.ExplicitAsset>();
            foreach (BuildLayout.ExplicitAsset asset in file.Assets)
            {
                if (asset != null && !string.IsNullOrEmpty(asset.Guid))
                    assets[asset.Guid] = asset;
            }
            foreach (AssetObjects assetRecord in record.Assets)
            {
                if (assetRecord.Guid != null && assets.TryGetValue(assetRecord.Guid, out BuildLayout.ExplicitAsset asset))
                    asset.Objects = assetRecord.Objects;
                else
                    unmatched++;
            }

            Dictionary<string, BuildLayout.DataFromOtherAsset> otherAssets = new Dictionary<string, BuildLayout.DataFromOtherAsset>();
            foreach (BuildLayout.DataFromOtherAsset otherAsset in file.OtherAssets)
            {
                if (otherAsset != null && !string.IsNullOrEmpty(otherAsset.AssetGuid))
                    otherAssets[otherAsset.AssetGuid] = otherAsset;
            }
            foreach (AssetObjects assetRecord in record.OtherAssets)
            {
                if (assetRecord.Guid != null && otherAssets.TryGetValue(assetRecord.Guid, out BuildLayout.DataFromOtherAsset otherAsset))
                    otherAsset.Objects = assetRecord.Objects;
                else
                    unmatched++;
            }
            return unmatched;
        }
    }
}
