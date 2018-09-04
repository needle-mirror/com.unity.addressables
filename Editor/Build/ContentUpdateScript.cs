using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System.Linq;
using UnityEditor.Build.Utilities;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEditor.Build.Pipeline.Tasks;
using UnityEditor.Build.Pipeline.Utilities;
using UnityEngine.ResourceManagement;
using UnityEngine.AddressableAssets;
using UnityEditor.SceneManagement;
using UnityEditor.IMGUI.Controls;
using System.Runtime.Serialization.Formatters.Binary;
using System;

namespace UnityEditor.AddressableAssets
{
    [System.Serializable]
    internal class CachedData
    {
        [SerializeField]
        public string m_playerVersion;
        [SerializeField]
        public CachedInfo[] m_cachedInfos;
    }
    internal class ContentUpdateScript
    {
        internal static string SaveCacheData(List<AddressableAssetEntry> entries, IBuildCache buildCache, string playerVersion)
        {
            try
            {
                var cacheEntries = new List<CacheEntry>();
                foreach (var entry in entries)
                {
                    if (entry.parentGroup.StaticContent)
                    {
                        GUID guid;
                        if (GUID.TryParse(entry.guid, out guid))
                        {
                            var cacheEntry = buildCache.GetCacheEntry(guid);
                            if (cacheEntry.IsValid())
                                cacheEntries.Add(cacheEntry);
                        }
                    }
                }
                IList<CachedInfo> cachedInfos;
                buildCache.LoadCachedData(cacheEntries, out cachedInfos);
                var cacheData = new CachedData() { m_cachedInfos = cachedInfos.ToArray(), m_playerVersion = playerVersion };
                var formatter = new BinaryFormatter();
                var tempPath = Path.GetDirectoryName(Application.dataPath) + "/Temp/com.unity.addressables/cachedata.bin";
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
                var dir = Path.GetDirectoryName(tempPath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write);
                formatter.Serialize(stream, cacheData);
                stream.Flush();
                stream.Close();
                stream.Dispose();
                return tempPath;
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                return null;
            }
        }

        internal static string GetCacheDataPath(bool browse)
        {
            var buildPath = EditorUserBuildSettings.GetBuildLocation(EditorUserBuildSettings.activeBuildTarget);
            if (File.Exists(buildPath))
                buildPath = Path.GetDirectoryName(buildPath);
            if (browse)
            {
                if (string.IsNullOrEmpty(buildPath))
                    buildPath = Application.dataPath;

                buildPath = EditorUtility.OpenFilePanel("Build Data File", Path.GetDirectoryName(buildPath), "bin");

                if (string.IsNullOrEmpty(buildPath))
                    return null;

                return buildPath;
            }
            else
            {
                if (string.IsNullOrEmpty(buildPath))
                    buildPath = Application.streamingAssetsPath;
            }
            var path = Path.Combine(buildPath, "cachedata.bin");
            return path;
        }

        internal static CachedData LoadCacheData(string cacheDataPath)
        {
            if (string.IsNullOrEmpty(cacheDataPath))
                return null;
            var stream = new FileStream(cacheDataPath, FileMode.Open, FileAccess.Read);
            var formatter = new BinaryFormatter();
            var cacheData = formatter.Deserialize(stream) as CachedData;
            if (cacheData == null)
            {
                Addressables.LogError("Invalid hash data file.  This file is usually named cachedata.bin and is built into the streaming assets path of a player build.");
                return null;
            }
            return cacheData;
        }

        static bool streamingAssetsExists = false;
        public static void BuildContentUpdate(string buildPath)
        {
            BuildContentUpdate(AddressableAssetSettings.GetDefault(false, false), buildPath);
        }

        internal static bool BuildContentUpdate(AddressableAssetSettings settings, string buildPath)
        {
            var cacheDataPath = string.IsNullOrEmpty(buildPath) ? GetCacheDataPath(true) : Path.Combine(buildPath, "cachedata.bin");
            var cacheData = LoadCacheData(cacheDataPath);
            if (cacheData == null)
                return false;

            streamingAssetsExists = Directory.Exists("Assets/StreamingAssets");
            SceneManagerState.Record();
            string unusedCacheDataPath;
            BuildScript.PrepareRuntimeData(false, false, false, true, false, BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget), EditorUserBuildSettings.activeBuildTarget, cacheData.m_playerVersion, ResourceManagerRuntimeData.EditorPlayMode.PackedMode, out unusedCacheDataPath);
            SceneManagerState.Restore();
            BuildScript.Cleanup(!streamingAssetsExists);

            return BuildScript.BuildDataForPlayMode(cacheData.m_playerVersion, ResourceManagerRuntimeData.EditorPlayMode.PackedMode);
        }

        public static void PrepareForContentUpdate(string buildPath, bool showConfirmationWindow)
        {
            PrepareForContentUpdate(AddressableAssetSettings.GetDefault(false, false), buildPath, showConfirmationWindow);

        }

        internal static bool PrepareForContentUpdate(AddressableAssetSettings settings, string buildPath, bool showConfirmationWindow)
        {
            var cacheDataPath = string.IsNullOrEmpty(buildPath) ? GetCacheDataPath(true) : Path.Combine(buildPath, "cachedata.bin");
            var cacheData = LoadCacheData(cacheDataPath);
            if (cacheData == null)
                return false;

            var allEntries = new List<AddressableAssetEntry>();
            foreach (var group in settings.groups)
                if (group.StaticContent)
                    foreach (var entry in group.entries)
                        entry.GatherAllAssets(allEntries, true, true);

            var entryToCacheInfo = new Dictionary<string, CachedInfo>();
            foreach (var cacheInfo in cacheData.m_cachedInfos)
                if (cacheInfo != null)
                    entryToCacheInfo[cacheInfo.Asset.Guid.ToString()] = cacheInfo;
            var modifiedEntries = new List<AddressableAssetEntry>();
            var buildCache = new BuildCache();
            foreach (var entry in allEntries)
            {
                CachedInfo info;
                if (!entryToCacheInfo.TryGetValue(entry.guid, out info) || buildCache.NeedsRebuild(info))
                    modifiedEntries.Add(entry);
            }
            if (showConfirmationWindow)
            {
                var previewWindow = EditorWindow.GetWindow<ContentUpdatePreviewWindow>();
                previewWindow.Show(settings, modifiedEntries);
            }
            else
            {
                CreateContentUpdateGroup(settings, modifiedEntries);
            }
            return true;
        }

        internal static void CreateContentUpdateGroup(AddressableAssetSettings settings, List<AddressableAssetEntry> items)
        {
            var contentGroup = settings.CreateGroup(settings.FindUniqueGroupName("Content Update"), typeof(BundledAssetGroupProcessor), false, false, true);
            contentGroup.Data.SetData("BuildPath", settings.profileSettings.CreateValue("RemoteBuildPath", Addressables.BuildPath));
            contentGroup.Data.SetData("LoadPath", settings.profileSettings.CreateValue("RemoteLoadPath", "http://localhost/[BuildTarget]"));
            settings.MoveEntriesToGroup(items, contentGroup);
        }

    }

    class ContentUpdatePreviewWindow : EditorWindow
    {
        class ContentUpdateTreeView : TreeView
        {
            class Item : TreeViewItem
            {
                internal AddressableAssetEntry m_entry;
                internal bool m_enabled;
                public Item(AddressableAssetEntry entry) : base(entry.guid.GetHashCode())
                {
                    m_entry = entry;
                    m_enabled = true;
                }
            }

            ContentUpdatePreviewWindow m_preview;
            public ContentUpdateTreeView(ContentUpdatePreviewWindow preview, TreeViewState state, MultiColumnHeaderState mchs) : base(state, new MultiColumnHeader(mchs))
            {
                m_preview = preview;
            }

            internal List<AddressableAssetEntry> GetEnabledEntries()
            {
                var result = new List<AddressableAssetEntry>();
                foreach (var i in GetRows())
                {
                    var item = i as Item;
                    if (item != null)
                    {
                        if (item.m_enabled)
                            result.Add(item.m_entry);
                    }
                }
                return result;
            }

            protected override TreeViewItem BuildRoot()
            {
                var root = new TreeViewItem(-1, -1);
                root.children = new List<TreeViewItem>();
                foreach (var k in m_preview.m_entries)
                    root.AddChild(new Item(k));

                return root;
            }

            protected override void RowGUI(RowGUIArgs args)
            {
                var item = args.item as Item;
                if (item == null)
                {
                    base.RowGUI(args);
                    return;
                }
                for (int i = 0; i < args.GetNumVisibleColumns(); ++i)
                {
                    CellGUI(args.GetCellRect(i), item, args.GetColumn(i), ref args);
                }
            }
            private void CellGUI(Rect cellRect, Item item, int column, ref RowGUIArgs args)
            {
                if (column == 0)
                {
                    item.m_enabled = EditorGUI.Toggle(cellRect, item.m_enabled);
                }
                else if (column == 1)
                {
                    EditorGUI.LabelField(cellRect, item.m_entry.address);
                }
                else if (column == 2)
                {
                    EditorGUI.LabelField(cellRect, item.m_entry.AssetPath);
                }
            }

            internal static MultiColumnHeaderState CreateDefaultMultiColumnHeaderState()
            {
                var retVal = new MultiColumnHeaderState.Column[3];
                retVal[0] = new MultiColumnHeaderState.Column();
                retVal[0].headerContent = new GUIContent("Include", "Include change in Update");
                retVal[0].minWidth = 50;
                retVal[0].width = 50;
                retVal[0].maxWidth = 50;
                retVal[0].headerTextAlignment = TextAlignment.Left;
                retVal[0].canSort = true;
                retVal[0].autoResize = true;

                retVal[1] = new MultiColumnHeaderState.Column();
                retVal[1].headerContent = new GUIContent("Address", "Data Value");
                retVal[1].minWidth = 300;
                retVal[1].width = 500;
                retVal[1].maxWidth = 1000;
                retVal[1].headerTextAlignment = TextAlignment.Left;
                retVal[1].canSort = true;
                retVal[1].autoResize = true;

                retVal[2] = new MultiColumnHeaderState.Column();
                retVal[2].headerContent = new GUIContent("Path", "Asset Path");
                retVal[2].minWidth = 300;
                retVal[2].width = 800;
                retVal[2].maxWidth = 1000;
                retVal[2].headerTextAlignment = TextAlignment.Left;
                retVal[2].canSort = true;
                retVal[2].autoResize = true;

                return new MultiColumnHeaderState(retVal);
            }
        }

        AddressableAssetSettings m_settings;
        List<AddressableAssetEntry> m_entries;
        Vector2 m_scrollPosition;
        ContentUpdateTreeView tree = null;
        [SerializeField]
        TreeViewState treeState;
        [SerializeField]
        MultiColumnHeaderState mchs;

        public void Show(AddressableAssetSettings settings, List<AddressableAssetEntry> entries)
        {
            m_settings = settings;
            m_entries = entries;
            Show();
        }

        public void OnGUI()
        {
            if (m_entries == null)
                return;
            Rect contentRect = new Rect(0, 0, position.width, position.height - 50);
            if (tree == null)
            {
                if (treeState == null)
                    treeState = new TreeViewState();

                var headerState = ContentUpdateTreeView.CreateDefaultMultiColumnHeaderState();
                if (MultiColumnHeaderState.CanOverwriteSerializedFields(mchs, headerState))
                    MultiColumnHeaderState.OverwriteSerializedFields(mchs, headerState);
                mchs = headerState;

                tree = new ContentUpdateTreeView(this, treeState, mchs);
                tree.Reload();
            }

            tree.OnGUI(contentRect);
            GUILayout.BeginArea(new Rect(0, position.height - 50, position.width, 50));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Cancel"))
                Close();
            if (GUILayout.Button("Apply Changes"))
            {
                ContentUpdateScript.CreateContentUpdateGroup(m_settings, tree.GetEnabledEntries());
                Close();
            }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }
    }
}