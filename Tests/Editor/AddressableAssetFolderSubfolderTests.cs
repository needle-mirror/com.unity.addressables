using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Build.Pipeline;
using UnityEngine;
using static UnityEditor.AddressableAssets.Settings.AddressablesFileEnumeration;

namespace UnityEditor.AddressableAssets.Tests
{
    public class AddressableAssetFolderSubfolderTests : AddressableAssetTestBase
    {
        string m_TestFolderPath;

        string m_AddrParentFolderPath;
        string m_AddrChildSubfolderPath;

        string m_ParentObjPath;
        string m_AddrParentObjPath;
        string m_ChildObjPath;

        AddressableAssetGroup m_ParentGroup;
        AddressableAssetGroup m_ChildGroup;

        /* Creates the following folder structure
        * /AddrParentFolder/
        *       parentObj.prefab
        *       addrParentObj.prefab
        *       /AddrChildSubfolder/
        *               childObj.prefab
        */
        protected override void OnInit()
        {
            // Create directories
            m_TestFolderPath = TestFolder;
            m_AddrParentFolderPath = m_TestFolderPath + "/AddrParentFolder";
            m_AddrChildSubfolderPath = m_AddrParentFolderPath + "/AddrChildSubfolder";

            string addrParentFolderGuid = AssetDatabase.CreateFolder(m_TestFolderPath, "AddrParentFolder");
            string addrChildFolderGuid = AssetDatabase.CreateFolder(m_AddrParentFolderPath, "AddrChildSubfolder");

            // Create prefabs
            GameObject parentObj = new GameObject("ParentObject");
            GameObject addrParentObj = new GameObject("AddrParentObject");
            GameObject childObj = new GameObject("ChildObject");

            m_ParentObjPath = m_AddrParentFolderPath + "/parentObj.prefab";
            m_AddrParentObjPath = m_AddrParentFolderPath + "/addrParentObj.prefab";
            m_ChildObjPath = m_AddrChildSubfolderPath + "/childObj.prefab";

            PrefabUtility.SaveAsPrefabAsset(parentObj, m_ParentObjPath);
            PrefabUtility.SaveAsPrefabAsset(addrParentObj, m_AddrParentObjPath);
            PrefabUtility.SaveAsPrefabAsset(childObj, m_ChildObjPath);

            // Create groups
            const string parentGroupName = "ParentGroup";
            const string childGroupName = "ChildGroup";

            m_ParentGroup = Settings.CreateGroup(parentGroupName, false, false, false, null, typeof(BundledAssetGroupSchema));
            m_ChildGroup = Settings.CreateGroup(childGroupName, false, false, false, null, typeof(BundledAssetGroupSchema));

            // Create entries
            Settings.CreateOrMoveEntry(addrParentFolderGuid, m_ParentGroup);

            Settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(m_AddrParentObjPath), m_ChildGroup);
            Settings.CreateOrMoveEntry(addrChildFolderGuid, m_ChildGroup);
        }

        protected override void OnCleanup()
        {
            Settings.RemoveGroup(m_ParentGroup);
            Settings.RemoveGroup(m_ChildGroup);

            AssetDatabase.DeleteAsset(m_TestFolderPath + "/");
            AssetDatabase.Refresh();
        }

        List<string> GetValidAssetPaths(string path, AddressableAssetSettings settings)
        {
            List<string> pathsWithEnumerator;
            using (var enumerator = new AddressableFolderEnumerator(settings, true, null))
            {
                pathsWithEnumerator = enumerator.Enumerate(path, true).ToList();
            }

            List<string> pathsWithoutEnumerator = EnumerateAddressableFolder(path, settings, true).ToList();

            // Compare the two code paths: a shared enumerator, and a throwaway tree.
            CollectionAssert.AreEqual(pathsWithEnumerator, pathsWithoutEnumerator);

            return pathsWithEnumerator;
        }

        [Test]
        public void Build_WithAddrParentFolderAndAddrSubfolders_InSeparateGroups_Succeeds()
        {
            var context = new AddressablesDataBuilderInput(Settings);
            foreach (IDataBuilder db in Settings.DataBuilders.Cast<IDataBuilder>())
            {
                if (db.CanBuildData<AddressablesPlayerBuildResult>())
                    db.BuildData<AddressablesPlayerBuildResult>(context);
            }
        }

        // Packed mode forwards the build to a nested schema-driven instance, so the enumerator
        // has to travel on something both instances share.
        class EnumeratorSpyPackedMode : BuildScriptPackedMode
        {
            internal bool Ran;
            internal AddressableFolderEnumerator Seen;

            protected override string ProcessAllGroups(AddressableAssetsBuildContext aaContext)
            {
                Ran = true;
                Seen = aaContext.FolderEnumerator;
                return base.ProcessAllGroups(aaContext);
            }
        }

        [Test]
        public void PackedBuild_ReachesGroupProcessing_WithTheBuildsEnumerator()
        {
            var spy = ScriptableObject.CreateInstance<EnumeratorSpyPackedMode>();
            try
            {
                spy.BuildData<AddressablesPlayerBuildResult>(new AddressablesDataBuilderInput(Settings));

                Assert.IsTrue(spy.Ran, "Expected the build to reach group processing.");
                Assert.IsNotNull(spy.Seen,
                    "The build's enumerator has to survive the hop into the nested schema-driven instance, "
                    + "or every folder gather falls back to a throwaway tree.");
            }
            finally
            {
                ScriptableObject.DestroyImmediate(spy);
            }
        }

        [Test]
        public void WhenSubfolderIsAddr_AddrParentFolder_DoesNotInclude_SubfolderContents()
        {
            List<string> assetPaths = GetValidAssetPaths(m_AddrParentFolderPath, Settings);
            Assert.IsFalse(assetPaths.Contains(m_ChildObjPath));
        }

        [Test]
        public void WhenAssetIsAddr_AssetIsNotIncludedInParentAddrFolder()
        {
            List<string> assetPaths = GetValidAssetPaths(m_AddrParentFolderPath, Settings);
            Assert.IsFalse(assetPaths.Contains(m_AddrParentObjPath));
        }

        [Test]
        public void EnumerateFiles_ReturnsFilesOnly()
        {
            List<string> assetPaths = EnumerateAddressableFolder(m_AddrParentFolderPath, Settings, true);
            foreach (string path in assetPaths)
            {
                Assert.IsFalse(Directory.Exists(path));
            }
        }

        [Test]
        public void WhenEmptyFolderIsAddr_EnumerateFiles_ReturnsNothing()
        {
            string path = m_TestFolderPath + "/AddrEmptyFolder";
            string guid = AssetDatabase.CreateFolder(m_TestFolderPath, "AddrEmptyFolder");
            Settings.CreateOrMoveEntry(guid, m_ParentGroup);

            List<string> assetPaths = EnumerateAddressableFolder(path, Settings, true);
            Assert.AreEqual(0, assetPaths.Count);

            Settings.RemoveAssetEntry(guid);
            AssetDatabase.DeleteAsset(path);
        }

        [Test]
        public void WhenEnumerateFilesIsNonRecursive_ReturnTopLevelAssetsOnly()
        {
            string path = m_AddrParentFolderPath + "/ChildFolder";
            string guid = AssetDatabase.CreateFolder(m_AddrParentFolderPath, "ChildFolder");

            GameObject obj = new GameObject("TestObject");
            string objPath = path + "/childObj.prefab";

            PrefabUtility.SaveAsPrefabAsset(obj, objPath);

            List<string> assetPaths = EnumerateAddressableFolder(m_AddrParentFolderPath, Settings, false);
            Assert.AreEqual(1, assetPaths.Count);
            Assert.AreEqual(m_ParentObjPath, assetPaths[0]);

            AssetDatabase.DeleteAsset(path);
        }

        [Test]
        public void WhenPathDoesNotExist_EnumerateFiles_ThrowsException()
        {
            string path = "PathDoesntExist";
            Exception ex = Assert.Throws<Exception>(() => { EnumerateAddressableFolder(path, Settings, false); });
            Assert.AreEqual($"Path {path} cannot be enumerated because it does not exist", ex.Message);
        }

        [Test]
        public void WhenPathIsNotInTree_EnumerateFiles_ReturnsFiles()
        {
            string path = m_AddrParentFolderPath + "/ChildFolder";
            string guid = AssetDatabase.CreateFolder(m_AddrParentFolderPath, "ChildFolder");

            GameObject obj = new GameObject("TestObject");
            string objPath = path + "/childObj.prefab";
            PrefabUtility.SaveAsPrefabAsset(obj, objPath);
            List<string> assetPaths = EnumerateAddressableFolder(path, Settings, true);
            Assert.AreEqual(1, assetPaths.Count);
            Assert.AreEqual(objPath, assetPaths[0]);

            AssetDatabase.DeleteAsset(path);
        }

        [Test]
        public void WhenFileIsNotInAssetDatabase_EnumerateFiles_DoesNotReturnPath()
        {
            string folderPath = m_TestFolderPath + "/TestFolder";
            AssetDatabase.CreateFolder(m_TestFolderPath, "TestFolder");
            string filePath = Path.Combine(folderPath, ".hiddenfile");
            File.Create(filePath).Close();

            List<string> assetPaths = EnumerateAddressableFolder(folderPath, Settings, true);
            Assert.AreEqual(0, assetPaths.Count);

            File.Delete(filePath);
            AssetDatabase.DeleteAsset(folderPath);
        }

        [Test]
        public void WhenNoFolderIsAddressable_EnumerateFiles_ReturnsNothing()
        {
            string parentFolderGuid = AssetDatabase.AssetPathToGUID(m_AddrParentFolderPath);
            string childFolderGuid = AssetDatabase.AssetPathToGUID(m_AddrChildSubfolderPath);
            Settings.RemoveAssetEntry(parentFolderGuid);
            Settings.RemoveAssetEntry(childFolderGuid);

            using (var enumerator = new AddressableFolderEnumerator(Settings, true, null))
            {
                List<string> assetPaths = enumerator.Enumerate(m_TestFolderPath, false);
                Assert.AreEqual(0, assetPaths.Count);
            }

            Settings.CreateOrMoveEntry(parentFolderGuid, m_ParentGroup);
            Settings.CreateOrMoveEntry(childFolderGuid, m_ChildGroup);
        }

        [Test]
        public void WhenAFolderIsAddressable_Enumerator_BuildsTreeOnFirstUse()
        {
            using (var enumerator = new AddressableFolderEnumerator(Settings, false, null))
            {
                Assert.IsNull(enumerator.CurrentTree, "The tree should not be built until a folder is actually enumerated.");

                enumerator.Enumerate(m_AddrParentFolderPath, true);

                Assert.IsNotNull(enumerator.CurrentTree);
            }
        }

        [Test]
        public void WhenNoFolderIsAddressable_Enumerator_BuildsNoTree()
        {
            string parentFolderGuid = AssetDatabase.AssetPathToGUID(m_AddrParentFolderPath);
            string childFolderGuid = AssetDatabase.AssetPathToGUID(m_AddrChildSubfolderPath);
            Settings.RemoveAssetEntry(parentFolderGuid);
            Settings.RemoveAssetEntry(childFolderGuid);

            using (var enumerator = new AddressableFolderEnumerator(Settings, true, null))
            {
                Assert.IsNull(enumerator.CurrentTree);
            }

            Settings.CreateOrMoveEntry(parentFolderGuid, m_ParentGroup);
            Settings.CreateOrMoveEntry(childFolderGuid, m_ChildGroup);
        }

        [Test]
        public void WhenPrepopulateAssetsIsTrue_Enumerator_AddsAllAssetsToTree()
        {
            using (var enumerator = new AddressableFolderEnumerator(Settings, true, null))
            {
                AddressableAssetTree.TreeNode node = enumerator.CurrentTree.FindNode(m_ChildObjPath, false);
                Assert.IsNotNull(node);
            }
        }

        [Test]
        public void BuildAddressableTree_OnlyAddsAddressablesToTree()
        {
            AddressableAssetTree tree = BuildAddressableTree(Settings);
            AddressableAssetTree.TreeNode node = tree.FindNode(m_ChildObjPath, false);
            Assert.IsNull(node);
        }

        [Test]
        public void WhenFolderEnumeratedTwice_Enumerator_KeepsTheFirstWalk()
        {
            using (var enumerator = new AddressableFolderEnumerator(Settings, false, null))
            {
                List<string> first = enumerator.Enumerate(m_AddrParentFolderPath, true);
                AddressableAssetTree tree = enumerator.CurrentTree;
                Assert.IsTrue(tree.FindNode(m_AddrParentFolderPath, false).HasEnumerated);

                List<string> second = enumerator.Enumerate(m_AddrParentFolderPath, true);

                Assert.AreSame(tree, enumerator.CurrentTree, "The second call should reuse the tree, not build another.");
                CollectionAssert.AreEqual(first, second);
            }
        }

        [Test]
        public void GatherAllAssets_WithAndWithoutEnumerator_ProducesSameEntries()
        {
            var folderEntry = m_ParentGroup.GetAssetEntry(AssetDatabase.AssetPathToGUID(m_AddrParentFolderPath));
            Assert.NotNull(folderEntry);

            var withoutEnumerator = new List<AddressableAssetEntry>();
            folderEntry.GatherAllAssets(withoutEnumerator, false, true, false);

            // BuildScriptBase.BuildData holds one of these for the whole build, so gathering
            // with one must not change what a folder yields.
            var withEnumerator = new List<AddressableAssetEntry>();
            using (var enumerator = new AddressableFolderEnumerator(Settings, false, null))
            {
                folderEntry.GatherAllAssets(withEnumerator, false, true, false, null, enumerator);
            }

            CollectionAssert.AreEqual(
                withoutEnumerator.Select(e => e.address).ToList(),
                withEnumerator.Select(e => e.address).ToList());
        }

        [Test]
        public void WhenGroupIsNotSubclassed_GatherUsesTheEnumerator()
        {
            using (var enumerator = new AddressableFolderEnumerator(Settings, false, null))
            {
                m_ParentGroup.GatherAllAssets(new List<AddressableAssetEntry>(), true, true, false, null, enumerator);

                Assert.IsNotNull(enumerator.CurrentTree,
                    "A group of the base type should pass the enumerator down, not route through the public form.");
            }
        }

        [Test]
        public void GetAllAssets_WithAndWithoutEnumerator_ProducesSameEntries()
        {
            var withoutEnumerator = new List<AddressableAssetEntry>();
            Settings.GetAllAssets(withoutEnumerator, false);

            var withEnumerator = new List<AddressableAssetEntry>();
            using (var enumerator = new AddressableFolderEnumerator(Settings, false, null))
            {
                Settings.GetAllAssets(withEnumerator, false, null, null, enumerator);
            }

            CollectionAssert.AreEqual(
                withoutEnumerator.Select(e => e.address).ToList(),
                withEnumerator.Select(e => e.address).ToList());
        }

        [Test]
        public void WithNullSettings_Enumerator_BuildsNoTree()
        {
            // BuildScriptBase.BuildData creates one before the build script can reject a null
            // settings object, so it has to tolerate one.
            Assert.DoesNotThrow(() =>
            {
                using (var enumerator = new AddressableFolderEnumerator(null, false, null))
                {
                    Assert.IsNull(enumerator.CurrentTree);
                }
            });
        }

        // Saves a prefab and imports it synchronously, so the asset postprocessor has run by
        // the time this returns.
        void CreateAndImportPrefab(string prefabPath)
        {
            GameObject prefabObject = new GameObject(Path.GetFileNameWithoutExtension(prefabPath));
            try
            {
                PrefabUtility.SaveAsPrefabAsset(prefabObject, prefabPath);
            }
            finally
            {
                GameObject.DestroyImmediate(prefabObject);
            }

            AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        }

        // A subfolder that starts non-addressable, so the parent folder owns its file.
        string CreateNonAddressableSubfolder(string folderName, out string prefabPath)
        {
            string folderPath = m_AddrParentFolderPath + "/" + folderName;
            AssetDatabase.CreateFolder(m_AddrParentFolderPath, folderName);
            prefabPath = folderPath + "/lateObj.prefab";
            CreateAndImportPrefab(prefabPath);

            AssetDatabase.ImportAsset(folderPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            return folderPath;
        }

        // ProcessAllGroups deliberately allows a ProcessGroup override to append a group part
        // way through a build.
        AddressableAssetGroup MakeFolderAddressable(string folderPath, string groupName)
        {
            var group = Settings.CreateGroup(groupName, false, false, false, null, typeof(BundledAssetGroupSchema));
            Settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(folderPath), group);
            return group;
        }

        [Test]
        public void WhenEntryAddedDuringUse_Enumerator_PatchesTree()
        {
            string lateFolderPath = CreateNonAddressableSubfolder("LateFolder", out string lateObjPath);

            AddressableAssetGroup lateGroup = null;
            try
            {
                using (var enumerator = new AddressableFolderEnumerator(Settings, false, null, watchForChanges: true))
                {
                    List<string> before = enumerator.Enumerate(m_AddrParentFolderPath, true);
                    Assert.Contains(lateObjPath, before, "Expected the parent folder to own the file while the subfolder is not addressable.");

                    AddressableAssetTree tree = enumerator.CurrentTree;
                    AddressableAssetTree.TreeNode siblingNode = tree.FindNode(m_AddrChildSubfolderPath, false);
                    Assert.IsTrue(siblingNode.HasEnumerated, "Expected the sibling subfolder to already be walked.");

                    lateGroup = MakeFolderAddressable(lateFolderPath, "LateGroup");

                    Assert.AreSame(tree, enumerator.CurrentTree, "Adding an entry should patch the tree in place, not rebuild it.");
                    Assert.IsTrue(siblingNode.HasEnumerated,
                        "A patch in place must not lose the filesystem walk an unrelated sibling folder already paid for.");

                    List<string> after = enumerator.Enumerate(m_AddrParentFolderPath, true);
                    Assert.IsFalse(after.Contains(lateObjPath),
                        "An unpatched tree lets the parent folder keep a file that now belongs to another entry, so both would pack it.");
                }
            }
            finally
            {
                if (lateGroup != null)
                    Settings.RemoveGroup(lateGroup);
                AssetDatabase.DeleteAsset(lateFolderPath);
            }
        }

        [Test]
        public void WhenEntryRemovedDuringUse_Enumerator_PatchesTree()
        {
            string lateFolderPath = CreateNonAddressableSubfolder("EntryRemovedLateFolder", out string lateObjPath);
            AddressableAssetGroup lateGroup = MakeFolderAddressable(lateFolderPath, "EntryRemovedLateGroup");

            try
            {
                using (var enumerator = new AddressableFolderEnumerator(Settings, false, null, watchForChanges: true))
                {
                    List<string> before = enumerator.Enumerate(m_AddrParentFolderPath, true);
                    Assert.IsFalse(before.Contains(lateObjPath), "Expected the folder's own entry to own the file.");

                    AddressableAssetTree tree = enumerator.CurrentTree;
                    AddressableAssetEntry entry = lateGroup.GetAssetEntry(AssetDatabase.AssetPathToGUID(lateFolderPath));
                    lateGroup.RemoveAssetEntry(entry);

                    Assert.AreSame(tree, enumerator.CurrentTree, "Removing an entry should patch the tree in place, not rebuild it.");

                    List<string> after = enumerator.Enumerate(m_AddrParentFolderPath, true);
                    Assert.Contains(lateObjPath, after,
                        "Removing the entry should unmark its path, so the parent folder owns the file again.");
                }
            }
            finally
            {
                Settings.RemoveGroup(lateGroup);
                AssetDatabase.DeleteAsset(lateFolderPath);
            }
        }

        [Test]
        public void WhenGroupRemovedDuringUse_Enumerator_PatchesTree()
        {
            string lateFolderPath = CreateNonAddressableSubfolder("GroupRemovedLateFolder", out string lateObjPath);
            AddressableAssetGroup lateGroup = MakeFolderAddressable(lateFolderPath, "GroupRemovedLateGroup");

            try
            {
                using (var enumerator = new AddressableFolderEnumerator(Settings, false, null, watchForChanges: true))
                {
                    List<string> before = enumerator.Enumerate(m_AddrParentFolderPath, true);
                    Assert.IsFalse(before.Contains(lateObjPath), "Expected the folder's own group entry to own the file.");

                    AddressableAssetTree tree = enumerator.CurrentTree;
                    Settings.RemoveGroup(lateGroup);
                    lateGroup = null;

                    Assert.AreSame(tree, enumerator.CurrentTree, "Removing a group should patch the tree in place, not rebuild it.");

                    List<string> after = enumerator.Enumerate(m_AddrParentFolderPath, true);
                    Assert.Contains(lateObjPath, after,
                        "Removing the group should unmark its entry, so the parent folder owns the file again.");
                }
            }
            finally
            {
                if (lateGroup != null)
                    Settings.RemoveGroup(lateGroup);
                AssetDatabase.DeleteAsset(lateFolderPath);
            }
        }

        [Test]
        public void WhenGroupAddedWithExistingEntries_Enumerator_PatchesTree()
        {
            string lateFolderPath = CreateNonAddressableSubfolder("GroupAddedLateFolder", out string lateObjPath);

            AddressableAssetGroup lateGroup = null;
            try
            {
                using (var enumerator = new AddressableFolderEnumerator(Settings, false, null, watchForChanges: true))
                {
                    List<string> before = enumerator.Enumerate(m_AddrParentFolderPath, true);
                    Assert.Contains(lateObjPath, before, "Expected the parent folder to own the file while the subfolder is not addressable.");

                    // Created with events suppressed, so no per-entry patch reaches the
                    // enumerator. That isolates what GroupAdded's own patch does: CreateGroup
                    // posts it with an empty group in practice, but the event data can carry
                    // one that already has entries.
                    lateGroup = Settings.CreateGroup("GroupAddedLateGroup", false, false, false, null, typeof(BundledAssetGroupSchema));
                    Settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(lateFolderPath), lateGroup, false, false);

                    Assert.Contains(lateObjPath, enumerator.Enumerate(m_AddrParentFolderPath, true),
                        "The suppressed creation must not have patched the tree, or this no longer tests GroupAdded.");

                    Settings.SetDirty(AddressableAssetSettings.ModificationEvent.GroupAdded, lateGroup, true, false);

                    List<string> after = enumerator.Enumerate(m_AddrParentFolderPath, true);
                    Assert.IsFalse(after.Contains(lateObjPath),
                        "GroupAdded should mark every one of the group's entries addressable, not just the group itself.");
                }
            }
            finally
            {
                if (lateGroup != null)
                    Settings.RemoveGroup(lateGroup);
                AssetDatabase.DeleteAsset(lateFolderPath);
            }
        }

        [Test]
        public void WhenEntriesAddedInBatchDuringUse_Enumerator_RebuildsTree()
        {
            string lateFolderPath = CreateNonAddressableSubfolder("BatchLateFolder", out string lateObjPath);

            // Created before the enumerator, because GroupAdded would patch the tree on its
            // own. An empty group contributes no paths to it.
            AddressableAssetGroup lateGroup = Settings.CreateGroup("BatchLateGroup", false, false, false, null, typeof(BundledAssetGroupSchema));

            var observed = new List<AddressableAssetSettings.ModificationEvent>();

            void Probe(AddressableAssetSettings s, AddressableAssetSettings.ModificationEvent e, object data) => observed.Add(e);

            AddressableAssetSettings.OnModificationGlobal += Probe;
            try
            {
                using (var enumerator = new AddressableFolderEnumerator(Settings, false, null, watchForChanges: true))
                {
                    List<string> before = enumerator.Enumerate(m_AddrParentFolderPath, true);
                    Assert.Contains(lateObjPath, before, "Expected the parent folder to own the file while the subfolder is not addressable.");

                    AddressableAssetTree treeBeforeBatch = enumerator.CurrentTree;

                    Settings.CreateOrMoveEntries(new[] {AssetDatabase.AssetPathToGUID(lateFolderPath)}, lateGroup);

                    CollectionAssert.Contains(observed, AddressableAssetSettings.ModificationEvent.BatchModification);
                    CollectionAssert.IsEmpty(observed.Intersect(new[]
                        {
                            AddressableAssetSettings.ModificationEvent.EntryCreated,
                            AddressableAssetSettings.ModificationEvent.EntryAdded,
                            AddressableAssetSettings.ModificationEvent.EntryMoved,
                            AddressableAssetSettings.ModificationEvent.EntryModified
                        }),
                        "A per-entry event would patch the tree on its own, so this no longer covers the batch path.");

                    List<string> after = enumerator.Enumerate(m_AddrParentFolderPath, true);

                    Assert.AreNotSame(treeBeforeBatch, enumerator.CurrentTree,
                        "BatchModification carries no information about what changed, so it can only be handled by rebuilding.");
                    Assert.IsFalse(after.Contains(lateObjPath),
                        "The rebuild should reflect current membership, so the parent folder no longer owns the file.");
                }
            }
            finally
            {
                AddressableAssetSettings.OnModificationGlobal -= Probe;
                Settings.RemoveGroup(lateGroup);
                AssetDatabase.DeleteAsset(lateFolderPath);
            }
        }

        [Test]
        public void WhenEntryMovedOrModifiedDuringUse_Enumerator_DoesNotTouchTree()
        {
            string lateFolderPath = CreateNonAddressableSubfolder("MoveModifyLateFolder", out string lateObjPath);
            AddressableAssetGroup lateGroup = MakeFolderAddressable(lateFolderPath, "MoveModifyLateGroup");
            AddressableAssetGroup targetGroup = Settings.CreateGroup("MoveModifyTargetGroup", false, false, false, null, typeof(BundledAssetGroupSchema));

            try
            {
                using (var enumerator = new AddressableFolderEnumerator(Settings, false, null, watchForChanges: true))
                {
                    List<string> before = enumerator.Enumerate(m_AddrParentFolderPath, true);
                    Assert.IsFalse(before.Contains(lateObjPath), "Expected the folder's own entry to own the file.");

                    AddressableAssetTree tree = enumerator.CurrentTree;
                    AddressableAssetEntry entry = lateGroup.GetAssetEntry(AssetDatabase.AssetPathToGUID(lateFolderPath));
                    entry.SetLabel("SomeLabel", true, true); // EntryModified - address and label edits never touch AssetPath.
                    Settings.MoveEntries(new List<AddressableAssetEntry> {entry}, targetGroup); // EntryMoved - a group reassignment, not a path change.

                    Assert.AreSame(tree, enumerator.CurrentTree, "Neither event should rebuild the tree.");

                    List<string> after = enumerator.Enumerate(m_AddrParentFolderPath, true);
                    CollectionAssert.AreEqual(before, after,
                        "Neither a label change nor a group move can change which paths are addressable, so the folder's listing should be unaffected.");
                }
            }
            finally
            {
                Settings.RemoveGroup(targetGroup);
                Settings.RemoveGroup(lateGroup);
                AssetDatabase.DeleteAsset(lateFolderPath);
            }
        }

        [Test]
        public void WhenAssetImportedIntoWalkedFolder_Enumerator_PicksItUp()
        {
            string extraPath = m_AddrParentFolderPath + "/extraObj.prefab";

            try
            {
                using (var enumerator = new AddressableFolderEnumerator(Settings, false, null, watchForChanges: true))
                {
                    List<string> before = enumerator.Enumerate(m_AddrParentFolderPath, true);
                    Assert.IsFalse(before.Contains(extraPath));

                    CreateAndImportPrefab(extraPath);

                    // A new file that is not an entry and not in Resources raises no settings
                    // event, so only the asset postprocessor can catch this.
                    List<string> after = enumerator.Enumerate(m_AddrParentFolderPath, true);
                    Assert.Contains(extraPath, after,
                        "A file imported into a walked folder should appear in the next enumeration.");
                }
            }
            finally
            {
                AssetDatabase.DeleteAsset(extraPath);
            }
        }

        [Test]
        public void WhenAssetDeletedFromWalkedFolder_Enumerator_DropsIt()
        {
            string extraPath = m_AddrParentFolderPath + "/deletedObj.prefab";
            CreateAndImportPrefab(extraPath);

            try
            {
                using (var enumerator = new AddressableFolderEnumerator(Settings, false, null, watchForChanges: true))
                {
                    List<string> before = enumerator.Enumerate(m_AddrParentFolderPath, true);
                    Assert.Contains(extraPath, before);

                    AssetDatabase.DeleteAsset(extraPath);

                    List<string> after = enumerator.Enumerate(m_AddrParentFolderPath, true);
                    Assert.IsFalse(after.Contains(extraPath),
                        "A file deleted from a walked folder should leave the next enumeration.");
                }
            }
            finally
            {
                AssetDatabase.DeleteAsset(extraPath);
            }
        }

        [Test]
        public void WhenAssetImportedOutsideWalkedFolders_Enumerator_KeepsItsTree()
        {
            // A sibling of the walked folder, so nothing this enumerator read has changed.
            string outsidePath = m_TestFolderPath + "/outsideObj.prefab";

            try
            {
                using (var enumerator = new AddressableFolderEnumerator(Settings, false, null, watchForChanges: true))
                {
                    enumerator.Enumerate(m_AddrParentFolderPath, true);
                    AddressableAssetTree tree = enumerator.CurrentTree;
                    AddressableAssetTree.TreeNode parentNode = tree.FindNode(m_AddrParentFolderPath, false);
                    Assert.IsTrue(parentNode.HasEnumerated, "Expected the folder to already be walked.");

                    CreateAndImportPrefab(outsidePath);

                    Assert.AreSame(tree, enumerator.CurrentTree,
                        "An import outside every walked folder should not cost the enumerator its walks.");
                    Assert.IsTrue(parentNode.HasEnumerated,
                        "The folder walk should have survived an unrelated import.");
                }
            }
            finally
            {
                AssetDatabase.DeleteAsset(outsidePath);
            }
        }

        [Test]
        public void WhenUnrelatedSettingsIsModified_Enumerator_KeepsItsTree()
        {
            var otherSettings = new AddressableAssetSettings();
            try
            {
                using (var enumerator = new AddressableFolderEnumerator(Settings, false, null, watchForChanges: true))
                {
                    enumerator.Enumerate(m_AddrParentFolderPath, true);
                    AddressableAssetTree tree = enumerator.CurrentTree;
                    AddressableAssetTree.TreeNode parentNode = tree.FindNode(m_AddrParentFolderPath, false);
                    Assert.IsTrue(parentNode.HasEnumerated, "Expected the folder to already be walked.");

                    // A membership change on an unrelated settings asset must not evict this
                    // tree, or a project with a second settings asset would re-walk every
                    // folder on the next enumeration.
                    otherSettings.SetDirty(AddressableAssetSettings.ModificationEvent.GroupAdded, null, true, false);

                    Assert.AreSame(tree, enumerator.CurrentTree,
                        "A modification to an unrelated settings asset should not invalidate this tree.");
                    Assert.IsTrue(parentNode.HasEnumerated,
                        "The tree should not have been rebuilt for an unrelated settings change.");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(otherSettings);
            }
        }

        [Test]
        public void WhenNotWatching_Enumerator_IgnoresModifications()
        {
            string lateFolderPath = CreateNonAddressableSubfolder("UnwatchedLateFolder", out string lateObjPath);

            AddressableAssetGroup lateGroup = null;
            try
            {
                // Not watching is the default, and what the locator and the Groups window use:
                // nothing is subscribed, so there is no lifetime to manage and nothing to leak.
                using (var enumerator = new AddressableFolderEnumerator(Settings, false, null))
                {
                    List<string> before = enumerator.Enumerate(m_AddrParentFolderPath, true);
                    Assert.Contains(lateObjPath, before, "Expected the parent folder to own the file while the subfolder is not addressable.");

                    AddressableAssetTree tree = enumerator.CurrentTree;

                    // Raises EntryCreated, which a watching enumerator would patch on.
                    lateGroup = MakeFolderAddressable(lateFolderPath, "UnwatchedLateGroup");

                    Assert.AreSame(tree, enumerator.CurrentTree,
                        "An unwatched enumerator should not react to a membership change.");
                    CollectionAssert.AreEqual(before, enumerator.Enumerate(m_AddrParentFolderPath, true),
                        "Its listing should be unchanged - the locator's own dirty flag is what handles this.");
                }
            }
            finally
            {
                if (lateGroup != null)
                    Settings.RemoveGroup(lateGroup);
                AssetDatabase.DeleteAsset(lateFolderPath);
            }
        }

        [Test]
        public void WhenTwoEnumeratorsAreOpen_TheyDoNotShareATree()
        {
            using (var first = new AddressableFolderEnumerator(Settings, false, null))
            using (var second = new AddressableFolderEnumerator(Settings, false, null))
            {
                first.Enumerate(m_AddrParentFolderPath, true);
                Assert.IsNull(second.CurrentTree, "Creating one enumerator must not build a tree in another.");

                second.Enumerate(m_AddrParentFolderPath, true);

                Assert.AreNotSame(first.CurrentTree, second.CurrentTree,
                    "Each enumerator owns its own tree, so neither can be torn down by the other.");
            }
        }

        [Test]
        public void WhenDisposed_Enumerator_StopsTrackingModifications()
        {
            string lateFolderPath = CreateNonAddressableSubfolder("DisposedLateFolder", out string lateObjPath);

            AddressableAssetGroup lateGroup = null;
            AddressableAssetTree tree;
            var enumerator = new AddressableFolderEnumerator(Settings, false, null, watchForChanges: true);
            try
            {
                enumerator.Enumerate(m_AddrParentFolderPath, true);
                tree = enumerator.CurrentTree;
                enumerator.Dispose();

                lateGroup = MakeFolderAddressable(lateFolderPath, "DisposedLateGroup");

                AddressableAssetTree.TreeNode lateNode = tree.FindNode(lateFolderPath, false);
                Assert.IsNotNull(lateNode, "Expected the folder walk to have added the subfolder.");
                Assert.IsFalse(lateNode.IsAddressable, "A disposed enumerator should no longer be patched by modification events.");
            }
            finally
            {
                enumerator.Dispose();
                if (lateGroup != null)
                    Settings.RemoveGroup(lateGroup);
                AssetDatabase.DeleteAsset(lateFolderPath);
            }
        }
    }
}
