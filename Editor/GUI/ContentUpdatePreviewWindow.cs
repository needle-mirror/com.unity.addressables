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
    internal class ContentUpdatePreviewWindow : EditorWindow
    {
        internal static bool PrepareForContentUpdate(AddressableAssetSettings settings, string buildPath)
        {
            var modifiedEntries = ContentUpdateScript.GatherModifiedEntries(settings, buildPath);
            if (modifiedEntries == null)
                return false;
            var previewWindow = EditorWindow.GetWindow<ContentUpdatePreviewWindow>();
            previewWindow.Show(settings, modifiedEntries);
            return true;
        }

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
            using (new EditorGUI.DisabledScope(tree.GetEnabledEntries().Count == 0))
            {
                if (GUILayout.Button("Apply Changes"))
                {
                    ContentUpdateScript.CreateContentUpdateGroup(m_settings, tree.GetEnabledEntries(), "Content Update");
                    Close();
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }
    }
}