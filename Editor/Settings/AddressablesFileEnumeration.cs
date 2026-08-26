using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.Build.Pipeline.Interfaces;

namespace UnityEditor.AddressableAssets.Settings
{
    internal class AddressableAssetTree
    {
        internal class TreeNode
        {
            internal string Path { get; set; }
            internal bool IsAddressable { get; set; }
            public bool IsFolder { get; set; }
            internal bool HasEnumerated { get; set; }

            internal Dictionary<string, TreeNode> Children { get; set; }

            internal TreeNode(string path)
            {
                Path = path;
            }

            internal bool GetChild(string p, out TreeNode node)
            {
                if (Children != null)
                    return Children.TryGetValue(p, out node);
                node = null;
                return false;
            }

            internal void AddChild(string p, TreeNode node)
            {
                if (Children == null)
                    Children = new Dictionary<string, TreeNode>();
                Children.Add(p, node);
            }
        }

        TreeNode m_Root;

        internal AddressableAssetTree()
        {
            m_Root = new TreeNode("");
            m_Root.IsFolder = true;
        }

        IEnumerable<string> EnumAddressables(TreeNode node, bool recursive, string relativePath)
        {
            if (node.Children != null)
            {
                List<TreeNode> curDirectory = new List<TreeNode>(node.Children.Values.Where(x => !x.IsAddressable));
                curDirectory.Sort((x, y) => { return string.CompareOrdinal(x.Path, y.Path); });
                string pathPrepend = string.IsNullOrEmpty(relativePath) ? "" : $"{relativePath}/";
                foreach (var v in curDirectory)
                {
                    if (!v.IsFolder)
                        yield return pathPrepend + v.Path;
                }

                if (recursive)
                {
                    foreach (var v in curDirectory)
                        if (v.Children != null)
                            foreach (string v2 in EnumAddressables(v, true, pathPrepend + v.Path))
                                yield return v2;
                }
            }
        }

        internal IEnumerable<string> Enumerate(string path, bool recursive)
        {
            TreeNode node = FindNode(path, false);
            if (node == null)
                throw new Exception($"Path {path} was not in the enumeration tree");
            if (!node.HasEnumerated)
                throw new Exception($"Path {path} cannot be enumerated because the file system has not enumerated them yet");

            return EnumAddressables(node, recursive, path);
        }

        internal TreeNode FindNode(string path, bool shouldAdd)
        {
            TreeNode it = m_Root;
            foreach (string p in path.Split('/'))
            {
                if (!it.GetChild(p, out TreeNode it2))
                {
                    if (!shouldAdd)
                        return null;
                    it2 = new TreeNode(p);
                    it.AddChild(p, it2);
                    it.IsFolder = true;
                }

                it = it2;
            }

            return it;
        }
    }

    /// <summary>
    /// Walks the Addressable folders of one settings asset and reuses that work across calls.
    /// </summary>
    internal sealed class AddressableFolderEnumerator : IDisposable
    {
        readonly AddressableAssetSettings m_Settings;
        readonly IBuildLogger m_Logger;

        // Whether this enumerator subscribed to change events, so Dispose only unsubscribes
        // what it actually took out.
        readonly bool m_WatchingForChanges;

        // The folders whose contents this enumerator has read off disk, so an import
        // somewhere else can't throw that work away.
        readonly HashSet<string> m_WalkedFolders = new HashSet<string>();

        AddressableAssetTree m_Tree;

        // Tracked separately from a null tree because BuildAddressableTree returns null when
        // the settings has no addressable folder at all. Without this we would rebuild on
        // every call just to learn that again.
        bool m_Built;

        /// <param name="watchForChanges">
        /// Follow entry and asset changes for this enumerator's lifetime. Only a build needs
        /// this - it spans group processing, where a ProcessGroup override can append a group
        /// and an import can add files. Watching subscribes to process-wide events, so an
        /// enumerator that opts in MUST be disposed or it lives as long as the settings asset.
        /// </param>
        internal AddressableFolderEnumerator(AddressableAssetSettings settings, bool prepopulateAssetsFolder,
            IBuildLogger logger, bool watchForChanges = false)
        {
            m_Settings = settings;
            m_Logger = logger;
            m_WatchingForChanges = watchForChanges;

            if (m_WatchingForChanges)
            {
                if (m_Settings != null)
                    m_Settings.OnModification += OnSettingsModification;
                AddressablesAssetPostProcessor.OnPostProcess.Register(OnAssetsChanged, 0);
            }

            if (prepopulateAssetsFolder)
                WalkFolder("Assets");
        }

        /// <summary>
        /// The tree as it currently stands, or null when nothing has been built yet.
        /// </summary>
        internal AddressableAssetTree CurrentTree => m_Tree;

        public void Dispose()
        {
            if (!m_WatchingForChanges)
                return;

            if (m_Settings != null)
                m_Settings.OnModification -= OnSettingsModification;
            AddressablesAssetPostProcessor.OnPostProcess.Unregister(OnAssetsChanged);
        }

        /// <summary>
        /// Collects the asset paths of an Addressable folder, reusing any earlier walk of it.
        /// </summary>
        internal List<string> Enumerate(string path, bool recurseAll)
        {
            if (!AssetDatabase.IsValidFolder(path))
                throw new Exception($"Path {path} cannot be enumerated because it does not exist");

            AddressableAssetTree tree = WalkFolder(path);
            if (tree == null)
                return new List<string>();

            List<string> files = new List<string>();
            using (m_Logger.ScopedStep(LogLevel.Info, $"Enumerating Addressables Tree {path}"))
            {
                foreach (string file in tree.Enumerate(path, recurseAll))
                {
                    if (BuiltinSceneCache.Contains(file))
                        continue;
                    files.Add(file);
                }
            }

            return files;
        }

        // Reads the folder off disk once and remembers that it was read, so a later import
        // under it can throw that walk away. Null when there is no tree to walk into.
        AddressableAssetTree WalkFolder(string path)
        {
            AddressableAssetTree tree = EnsureTree();
            if (tree == null)
                return null;

            m_WalkedFolders.Add(path);
            AddressablesFileEnumeration.AddLocalFilesToTreeIfNotEnumerated(tree, path, m_Logger);
            return tree;
        }

        // Built on first use, so a build with no addressable folders never pays for walking
        // every group and entry just to discover that.
        AddressableAssetTree EnsureTree()
        {
            if (!m_Built)
            {
                m_Built = true;
                m_Tree = m_Settings == null ? null : AddressablesFileEnumeration.BuildAddressableTree(m_Settings, m_Logger);
            }

            return m_Tree;
        }

        // Applies the smallest patch a modification event implies, in place. Rebuilding
        // instead resets every node's HasEnumerated, which re-walks the filesystem for every
        // folder already enumerated rather than just the one that changed.
        //
        // EntryMoved (a group reassignment) and EntryModified (address and label edits) never
        // change AssetPath, so neither can change what this tree tracks.
        void OnSettingsModification(AddressableAssetSettings settings, AddressableAssetSettings.ModificationEvent modificationEvent, object data)
        {
            switch (modificationEvent)
            {
                case AddressableAssetSettings.ModificationEvent.EntryCreated:
                case AddressableAssetSettings.ModificationEvent.EntryAdded:
                    MarkEntries(AsEntries(data), true);
                    break;

                case AddressableAssetSettings.ModificationEvent.EntryRemoved:
                    MarkEntries(AsEntries(data), false);
                    break;

                case AddressableAssetSettings.ModificationEvent.GroupAdded:
                    MarkGroups(AsGroups(data), true);
                    break;

                case AddressableAssetSettings.ModificationEvent.GroupRemoved:
                    MarkGroups(AsGroups(data), false);
                    break;

                // Bulk APIs such as CreateOrMoveEntries suppress the per-entry events and post
                // this one instead, and OnPostprocessAllAssets posts it with no data at all.
                // Either way, which paths changed is gone by the time this fires.
                case AddressableAssetSettings.ModificationEvent.BatchModification:
                    Invalidate();
                    break;
            }
        }

        void MarkEntries(IEnumerable<AddressableAssetEntry> entries, bool addressable)
        {
            if (!TryGetTreeToPatch(out AddressableAssetTree tree))
                return;

            foreach (AddressableAssetEntry entry in entries)
                SetAddressable(tree, entry.AssetPath, addressable);
        }

        void MarkGroups(IEnumerable<AddressableAssetGroup> groups, bool addressable)
        {
            if (!TryGetTreeToPatch(out AddressableAssetTree tree))
                return;

            foreach (AddressableAssetGroup group in groups)
            {
                // The group's asset file may already be deleted by the time this fires: the
                // Groups window's bulk remove deletes each asset, then posts one aggregate
                // event. A destroyed group's entries aren't safely readable, so rebuild rather
                // than leave its paths marked addressable.
                if (group == null)
                {
                    Invalidate();
                    return;
                }

                foreach (AddressableAssetEntry entry in group.entries)
                    SetAddressable(tree, entry.AssetPath, addressable);
            }
        }

        // A folder's contents are read off disk once and kept, so an asset appearing in one
        // mid-build would otherwise stay invisible. Only assets the AssetDatabase knows about
        // are ever enumerated, so importing, deleting or moving one is the whole exposure -
        // and each of those lands here.
        //
        // Settings modification events don't cover this: a new file that isn't yet an entry
        // and isn't in Resources raises nothing.
        void OnAssetsChanged(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (m_Tree == null)
                return;

            if (TouchesWalkedFolder(imported) || TouchesWalkedFolder(deleted)
                || TouchesWalkedFolder(moved) || TouchesWalkedFolder(movedFrom))
                Invalidate();
        }

        // Changes outside the folders this enumerator read must not cost it their walks.
        bool TouchesWalkedFolder(string[] paths)
        {
            foreach (string path in paths)
            {
                foreach (string folder in m_WalkedFolders)
                {
                    if (!path.StartsWith(folder, StringComparison.Ordinal))
                        continue;

                    // The folder itself, or something inside it - but not a sibling whose
                    // name merely starts the same way.
                    if (path.Length == folder.Length || path[folder.Length] == '/')
                        return true;
                }
            }

            return false;
        }

        // False means there is nothing to patch, and the next Enumerate builds current data.
        bool TryGetTreeToPatch(out AddressableAssetTree tree)
        {
            tree = m_Tree;

            if (!m_Built)
                return false;

            // Built, but the settings had no addressable folder then. It may have one now, so
            // drop that answer instead of patching nothing.
            if (tree == null)
            {
                Invalidate();
                return false;
            }

            return true;
        }

        // Lazy on purpose: a burst of events costs one rebuild, and none at all if nothing
        // enumerates again.
        void Invalidate()
        {
            m_Tree = null;
            m_Built = false;
            m_WalkedFolders.Clear();
        }

        static void SetAddressable(AddressableAssetTree tree, string path, bool addressable)
        {
            // An entry whose asset is gone reports an empty path and belongs in no tree.
            if (string.IsNullOrEmpty(path))
                return;

            if (addressable)
            {
                tree.FindNode(path, true).IsAddressable = true;
                return;
            }

            AddressableAssetTree.TreeNode node = tree.FindNode(path, false);
            if (node != null)
                node.IsAddressable = false;
        }

        static IEnumerable<AddressableAssetEntry> AsEntries(object data) => data switch
        {
            AddressableAssetEntry entry => new[] {entry},
            IEnumerable<AddressableAssetEntry> entries => entries,
            _ => Array.Empty<AddressableAssetEntry>()
        };

        static IEnumerable<AddressableAssetGroup> AsGroups(object data) => data switch
        {
            AddressableAssetGroup group => new[] {group},
            IEnumerable<AddressableAssetGroup> groups => groups,
            _ => Array.Empty<AddressableAssetGroup>()
        };
    }

    /// <summary>
    /// Methods for enumerating Addressable folders.
    /// </summary>
    public class AddressablesFileEnumeration
    {
        internal static AddressableAssetTree BuildAddressableTree(AddressableAssetSettings settings, IBuildLogger logger = null)
        {
            using (logger.ScopedStep(LogLevel.Verbose, "BuildAddressableTree"))
            {
                if (!ExtractAddressablePaths(settings, out HashSet<string> paths))
                    return null;

                AddressableAssetTree tree = new AddressableAssetTree();
                foreach (string path in paths)
                {
                    AddressableAssetTree.TreeNode node = tree.FindNode(path, true);
                    node.IsAddressable = true;
                }

                return tree;
            }
        }

        static bool ExtractAddressablePaths(AddressableAssetSettings settings, out HashSet<string> paths)
        {
            paths = new HashSet<string>();
            bool hasAddrFolder = false;
            foreach (AddressableAssetGroup group in settings.groups)
            {
                if (group == null)
                    continue;
                foreach (AddressableAssetEntry entry in group.entries)
                {
                    string convertedPath = entry.AssetPath;
                    if (!hasAddrFolder && AssetDatabase.IsValidFolder(convertedPath))
                        hasAddrFolder = true;
                    paths.Add(convertedPath);
                }
            }

            return hasAddrFolder;
        }

        internal static void AddLocalFilesToTreeIfNotEnumerated(AddressableAssetTree tree, string path, IBuildLogger logger)
        {
            AddressableAssetTree.TreeNode pathNode = tree.FindNode(path, true);

            if (pathNode == null || pathNode.HasEnumerated) // Already enumerated
                return;

            pathNode.HasEnumerated = true;
            using (logger.ScopedStep(LogLevel.Info, $"Enumerating {path}"))
            {
                foreach (string filename in Directory.EnumerateFileSystemEntries(path, "*.*", SearchOption.AllDirectories))
                {
                    if (!AddressableAssetUtility.IsPathValidForEntry(filename) || string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(filename)))
                        continue;
                    string convertedPath = filename.Replace('\\', '/');
                    var node = tree.FindNode(convertedPath, true);
                    node.IsFolder = AssetDatabase.IsValidFolder(filename);
                    node.HasEnumerated = true;
                }
            }
        }

        /// <summary>
        /// Collects and returns all the asset paths of a given Addressable folder entry
        /// </summary>
        /// <param name="path">The path of the folder</param>
        /// <param name="settings">The AddressableAssetSettings used to gather sub entries.</param>
        /// <param name="recurseAll">Flag indicating if the folder should be traversed recursively.</param>
        /// <param name="logger">Used to log messages during a build, if desired.</param>
        /// <returns>List of asset files in a given folder entry</returns>
        public static List<string> EnumerateAddressableFolder(string path, AddressableAssetSettings settings, bool recurseAll, IBuildLogger logger = null)
        {
            using (var enumerator = new AddressableFolderEnumerator(settings, false, logger))
                return enumerator.Enumerate(path, recurseAll);
        }
    }
}
