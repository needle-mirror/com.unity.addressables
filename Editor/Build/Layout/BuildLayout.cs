using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnityEditor.AddressableAssets.Build.Layout
{
    internal class BuildLayout
    {
        public class AssetBundleObjectInfo
        {
            public ulong Size;
        }
        public class Group
        {
            public string Name;
            public string Guid;
            public List<Bundle> Bundles = new List<Bundle>();
            public List<SchemaData> Schemas = new List<SchemaData>();
        }

        public class SchemaData
        {
            public string Guid;
            public string Type;
            public List<Tuple<string, string>> KvpDetails = new List<Tuple<string, string>>();
        }

        public class Bundle
        {
            public string Name;
            public ulong FileSize;
            public string Compression;
            public List<File> Files = new List<File>();
            public List<Bundle> Dependencies;
            public List<Bundle> ExpandedDependencies;
        }

        public class SubFile
        {
            public string Name;
            public bool IsSerializedFile;
            public ulong Size;
        }

        public class File
        {
            public string Name;
            public List<SubFile> SubFiles = new List<SubFile>();
            public List<ExplicitAsset> Assets = new List<ExplicitAsset>();
            public List<DataFromOtherAsset> OtherAssets = new List<DataFromOtherAsset>();
            public string WriteResultFilename;
            public AssetBundleObjectInfo BundleObjectInfo;
            public int PreloadInfoSize;
            public int MonoScriptCount;
            public ulong MonoScriptSize;
        }

        public class ExplicitAsset
        {
            public string Guid;
            public string AssetPath;
            public string AddressableName;
            public ulong SerializedSize;
            public ulong StreamedSize;
            public List<DataFromOtherAsset> InternalReferencedOtherAssets = new List<DataFromOtherAsset>();
            public List<ExplicitAsset> InternalReferencedExplicitAssets = new List<ExplicitAsset>();
            public List<ExplicitAsset> ExternallyReferencedAssets = new List<ExplicitAsset>();
        }

        public class DataFromOtherAsset
        {
            public string AssetGuid;
            public string AssetPath;
            public List<ExplicitAsset> ReferencingAssets = new List<ExplicitAsset>();
            public int ObjectCount;
            public ulong SerializedSize;
            public ulong StreamedSize;
        }

        public List<Group> Groups = new List<Group>();
        public List<Bundle> BuiltInBundles = new List<Bundle>();
    }

    internal class LayoutLookupTables
    {
        public Dictionary<string, BuildLayout.Bundle> Bundles = new Dictionary<string, BuildLayout.Bundle>();
        public Dictionary<string, BuildLayout.File> Files = new Dictionary<string, BuildLayout.File>();
        public Dictionary<string, BuildLayout.ExplicitAsset> GuidToExplicitAsset = new Dictionary<string, BuildLayout.ExplicitAsset>();
        public Dictionary<string, BuildLayout.Group> GroupLookup = new Dictionary<string, BuildLayout.Group>();
    }

    internal class BuildLayoutHelpers
    {
        public static IEnumerable<BuildLayout.ExplicitAsset> EnumerateAssets(BuildLayout layout)
        {
            return EnumerateBundles(layout).SelectMany(b => b.Files).SelectMany(f => f.Assets);
        }

        public static IEnumerable<BuildLayout.ExplicitAsset> EnumerateAssets(BuildLayout.Bundle bundle)
        {
            return bundle.Files.SelectMany(f => f.Assets);
        }

        public static IEnumerable<BuildLayout.Bundle> EnumerateBundles(BuildLayout layout)
        {
            foreach (BuildLayout.Bundle b in layout.BuiltInBundles)
                yield return b;

            foreach (BuildLayout.Bundle b in layout.Groups.SelectMany(g => g.Bundles))
                yield return b;
        }

        public static IEnumerable<BuildLayout.File> EnumerateFiles(BuildLayout layout)
        {
            return EnumerateBundles(layout).SelectMany(b => b.Files);
        }
    }
}
