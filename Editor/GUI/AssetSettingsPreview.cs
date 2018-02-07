using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEditor.IMGUI.Controls;
using System;
using UnityEditor.Build;
using UnityEditor.Experimental.Build.AssetBundle;

namespace UnityEditor.AddressableAssets
{
    [Serializable]
    internal class AssetSettingsPreview
    {
        [SerializeField]
        TreeViewState treeState;
        AssetSettingsPreviewTreeView tree;
        
        [SerializeField]
        internal BundleWriteData m_bundleWriteData;
        internal BuildDependencyData m_buildDependencyData;

        [SerializeField]
        internal HashSet<GUID> explicitAssets;

        internal AssetSettingsPreview()
        {
        }

        SearchField m_searchField = null;


        private Texture2D m_RefreshTexture;
        internal Texture2D bundleIcon;
        internal Texture2D sceneIcon;

        private void FindBundleIcons()
        {
            string[] icons = AssetDatabase.FindAssets("AddressableAssetsIconY1756");
            foreach (string i in icons)
            {
                string name = AssetDatabase.GUIDToAssetPath(i);
                if (name.Contains("AddressableAssetsIconY1756Basic.png"))
                    bundleIcon = (Texture2D)AssetDatabase.LoadAssetAtPath(name, typeof(Texture2D));
                else if (name.Contains("AddressableAssetsIconY1756Scene.png"))
                    sceneIcon = (Texture2D)AssetDatabase.LoadAssetAtPath(name, typeof(Texture2D));
            }
        }

        internal void OnGUI(Rect pos)
        {
            if(m_searchField == null)
                m_searchField = new SearchField();

            if (tree == null)
            {
                if (treeState == null)
                    treeState = new TreeViewState();

                tree = new AssetSettingsPreviewTreeView(treeState, this);
                tree.Reload();


                m_RefreshTexture = EditorGUIUtility.FindTexture("Refresh");
                FindBundleIcons();
            }

            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(m_RefreshTexture, GUILayout.ExpandWidth(false)))
            {
                ReloadPreview();
            }
            GUILayout.Space(12);
            tree.searchString = m_searchField.OnGUI(tree.searchString);
            GUILayout.Space(12);
            GUILayout.EndHorizontal();

            tree.OnGUI(new Rect(pos.x, pos.y + 32, pos.width, pos.height - 28));
        }

        private void ReloadPreview()
        {
            explicitAssets = new HashSet<GUID>();
            if (BuildScript.PreviewDependencyInfo(out m_buildDependencyData, out m_bundleWriteData))
            {
                foreach (var a in m_bundleWriteData.AssetToFiles)
                    explicitAssets.Add(a.Key);
            }
            else
                Debug.LogError("Build preview failed.");

            tree.Reload();
        }
    }

    internal class AssetSettingsPreviewTreeView : TreeView
    {
        AssetSettingsPreview preview;
        internal AssetSettingsPreviewTreeView(TreeViewState state, AssetSettingsPreview prev) : base(state)
        {
            showBorder = true;
            preview = prev;
        }

        protected override TreeViewItem BuildRoot()
        {
            return new TreeViewItem(-1, -1);
        }

        protected override IList<TreeViewItem> BuildRows(TreeViewItem root)
        {
            List<TreeViewItem> tempRows = new List<TreeViewItem>(10);
            if (preview.m_bundleWriteData != null)
            {
                var bundleToAssets = new Dictionary<string, List<GUID>>();
                foreach (var k in preview.m_bundleWriteData.AssetToFiles)
                {
                    List<string> bundleList = new List<string>();
                    List<GUID> assetList = null;
                    foreach (var f in k.Value)
                    {
                        var bundle = preview.m_bundleWriteData.FileToBundle[f];
                        if (!bundleToAssets.TryGetValue(bundle, out assetList))
                            bundleToAssets.Add(bundle, assetList = new List<GUID>());
                        if (!bundleList.Contains(bundle))
                            bundleList.Add(bundle);
                    }
                    assetList.Add(k.Key);
                }

                foreach (var bundleAssets in bundleToAssets)
                {
                    var bundleItem = new TreeViewItem(bundleAssets.Key.GetHashCode(), 0, bundleAssets.Key);
                    bundleItem.icon = preview.bundleIcon;
                    tempRows.Add(bundleItem);
                    if (bundleAssets.Value.Count > 0)
                    {
                        if (IsExpanded(bundleItem.id))
                        {
                            foreach (var g in bundleAssets.Value)
                            {
                                var path = AssetDatabase.GUIDToAssetPath(g.ToString());
                                var assetItem = new TreeViewItem(path.GetHashCode(), 1, path);
                                assetItem.icon = AssetDatabase.GetCachedIcon(path) as Texture2D;
                                tempRows.Add(assetItem);
                                bundleItem.AddChild(assetItem);

                                AssetLoadInfo loadInfo;
                                if(preview.m_buildDependencyData.AssetInfo.TryGetValue(g, out loadInfo))
                                {
                                    if (loadInfo.referencedObjects.Count > 0)
                                    {
                                        if (IsExpanded(assetItem.id))
                                        {
                                            HashSet<string> assetRefs = new HashSet<string>();
                                            foreach (var r in loadInfo.referencedObjects)
                                            {
                                                if ((!preview.explicitAssets.Contains(r.guid)) &&
                                                    (r.filePath != "library/unity default resources") &&
                                                    (r.filePath != "resources/unity_builtin_extra"))
                                                {
                                                    var filePath = AssetDatabase.GUIDToAssetPath(r.guid.ToString());
                                                    if (!string.IsNullOrEmpty(filePath) && !assetRefs.Contains(filePath))
                                                        assetRefs.Add(filePath);
                                                }
                                            }
                                            foreach (var r in assetRefs)
                                            {
                                                var subAssetItem = new TreeViewItem(r.GetHashCode(), 2, r);
                                                subAssetItem.icon = AssetDatabase.GetCachedIcon(r) as Texture2D;
                                                tempRows.Add(subAssetItem);
                                                assetItem.AddChild(subAssetItem);
                                            }
                                        }
                                        else
                                            assetItem.children = CreateChildListForCollapsedParent();
                                    }
                                }
                            }
                        }
                        else
                            bundleItem.children = CreateChildListForCollapsedParent();
                    }
                }
            }


            return tempRows;
        }

        protected override void RowGUI(RowGUIArgs args)
        {
            if (hasSearch)
            {
                GUI.Label(args.rowRect, "Search not yet implemented");
                return;
            }

            if ((args.selected == false) &&
                (Event.current.type == EventType.Repaint))
            {
                if (args.item.depth % 2 == 0)
                    DefaultStyles.backgroundOdd.Draw(args.rowRect, false, false, false, false);
                else
                    DefaultStyles.backgroundEven.Draw(args.rowRect, false, false, false, false);
            }
            using (new EditorGUI.DisabledScope(args.item.depth >= 2))
                base.RowGUI(args);
        }

        public override void OnGUI(Rect rect)
        {
            base.OnGUI(rect);

            //temporarily removing due to "hot control" issue.
            if (Event.current.type == EventType.MouseDown &&
                Event.current.button == 0 &&
                rect.Contains(Event.current.mousePosition))
            {
                SetSelection(new int[0], TreeViewSelectionOptions.FireSelectionChanged);
            }
        }
    }
}
