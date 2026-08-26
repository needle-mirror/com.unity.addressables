using System;
using NUnit.Framework;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace UnityEditor.AddressableAssets.Tests
{
    /// <summary>
    /// Covers who owns the folder enumerator that <see cref="BuildScriptBase.BuildData{TResult}"/>
    /// puts on the builder input. A nested build reuses it; only the creator disposes it.
    /// </summary>
    public class BuildScriptEnumeratorOwnershipTests : AddressableAssetTestBase
    {
        string m_FolderPath;
        AddressableAssetGroup m_FolderGroup;

        protected override void OnInit()
        {
            m_FolderPath = TestFolder + "/EnumeratorOwner";
            CreateAsset(m_FolderPath + "/ownerObj.prefab");
            AssetDatabase.ImportAsset(m_FolderPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            m_FolderGroup = MakeFolderAddressable(m_FolderPath, "EnumeratorOwnerGroup");
        }

        protected override void OnCleanup()
        {
            if (m_FolderGroup != null)
            {
                Settings.RemoveGroup(m_FolderGroup);
                m_FolderGroup = null;
            }
        }

        // A build script that records the enumerator it was handed, then lets the test
        // poke at it while the build is still open.
        class SpyScript : BuildScriptBase
        {
            internal AddressableFolderEnumerator Seen;
            internal Action<AddressablesDataBuilderInput> During;

            public override bool CanBuildData<T>()
            {
                return true;
            }

            protected override TResult BuildDataImplementation<TResult>(AddressablesDataBuilderInput builderInput)
            {
                Seen = builderInput.FolderEnumerator;
                During?.Invoke(builderInput);
                return default;
            }
        }

        // Stands in for a custom script that hands the same input to another builder.
        class NestingScript : BuildScriptBase
        {
            internal SpyScript Inner;
            internal AddressableFolderEnumerator Seen;
            internal AddressableFolderEnumerator SeenAfterInner;
            internal Action<AddressablesDataBuilderInput> AfterInner;

            public override bool CanBuildData<T>()
            {
                return true;
            }

            protected override TResult BuildDataImplementation<TResult>(AddressablesDataBuilderInput builderInput)
            {
                Seen = builderInput.FolderEnumerator;
                Inner.BuildData<TResult>(builderInput);
                SeenAfterInner = builderInput.FolderEnumerator;
                AfterInner?.Invoke(builderInput);
                return default;
            }
        }

        AddressableAssetGroup MakeFolderAddressable(string folderPath, string groupName)
        {
            var group = Settings.CreateGroup(groupName, false, false, false, null, typeof(BundledAssetGroupSchema));
            Settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(folderPath), group);
            return group;
        }

        string CreateNonAddressableSubfolder(string folderName)
        {
            string folderPath = m_FolderPath + "/" + folderName;
            CreateAsset(folderPath + "/lateObj.prefab");
            AssetDatabase.ImportAsset(folderPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            return folderPath;
        }

        AddressableAssetTree WarmTree(AddressablesDataBuilderInput input)
        {
            input.FolderEnumerator.Enumerate(m_FolderPath, true);
            return input.FolderEnumerator.CurrentTree;
        }

        // Runs a nested build and hands the scripts and the input to the verify step.
        // Assertions belong in verify, not in the hooks - BuildData swallows exceptions
        // thrown out of BuildDataImplementation.
        void RunNestedBuild(Action<AddressablesDataBuilderInput> afterInner,
            Action<NestingScript, SpyScript, AddressablesDataBuilderInput> verify)
        {
            var inner = ScriptableObject.CreateInstance<SpyScript>();
            var outer = ScriptableObject.CreateInstance<NestingScript>();
            try
            {
                outer.Inner = inner;
                outer.AfterInner = afterInner;

                var input = new AddressablesDataBuilderInput(Settings);
                outer.BuildData<AddressableAssetBuildResult>(input);

                verify(outer, inner, input);
            }
            finally
            {
                ScriptableObject.DestroyImmediate(outer);
                ScriptableObject.DestroyImmediate(inner);
            }
        }

        [Test]
        public void NestedBuild_ReusesOuterEnumerator_AndClearsItOnce()
        {
            RunNestedBuild(null, (outer, inner, input) =>
            {
                Assert.IsNotNull(outer.Seen, "The outer build should have created an enumerator.");
                Assert.AreSame(outer.Seen, inner.Seen,
                    "A nested build must reuse the outer enumerator, or it re-walks every folder into a second tree.");
                Assert.AreSame(outer.Seen, outer.SeenAfterInner,
                    "The nested build does not own the enumerator, so it must leave it in place.");
                Assert.IsNull(input.FolderEnumerator,
                    "The input outlives the build, so it must never be left pointing at a disposed enumerator.");
            });
        }

        [Test]
        public void NestedBuild_LeavesOuterEnumeratorTracking_AfterInnerReturns()
        {
            string lateFolderPath = CreateNonAddressableSubfolder("LateNestedFolder");

            AddressableAssetGroup lateGroup = null;
            bool ranAfterInner = false;
            bool addressableBefore = true;
            bool addressableAfter = false;
            try
            {
                RunNestedBuild(
                    input =>
                    {
                        AddressableAssetTree tree = WarmTree(input);
                        AddressableAssetTree.TreeNode lateNode = tree.FindNode(lateFolderPath, false);
                        addressableBefore = lateNode.IsAddressable;

                        lateGroup = MakeFolderAddressable(lateFolderPath, "LateNestedGroup");
                        addressableAfter = lateNode.IsAddressable;
                        ranAfterInner = true;
                    },
                    (outer, inner, input) => { });

                Assert.IsTrue(ranAfterInner, "Expected the outer build to get past the nested call.");
                Assert.IsFalse(addressableBefore, "Expected the folder walk to add the subfolder as non-addressable.");
                Assert.IsTrue(addressableAfter,
                    "The nested build must not dispose the shared enumerator, or the outer build stops seeing group changes.");
            }
            finally
            {
                if (lateGroup != null)
                    Settings.RemoveGroup(lateGroup);
                AssetDatabase.DeleteAsset(lateFolderPath);
            }
        }

        [Test]
        public void SingleBuild_CreatesItsOwnEnumerator_AndDisposesIt()
        {
            string lateFolderPath = CreateNonAddressableSubfolder("LateSingleFolder");

            var script = ScriptableObject.CreateInstance<SpyScript>();
            AddressableAssetGroup lateGroup = null;
            AddressableAssetTree tree = null;
            try
            {
                script.During = buildInput => { tree = WarmTree(buildInput); };

                var input = new AddressablesDataBuilderInput(Settings);
                script.BuildData<AddressableAssetBuildResult>(input);

                Assert.IsNotNull(script.Seen, "A build with nothing above it has to create its own enumerator.");
                Assert.IsNull(input.FolderEnumerator, "The build owns the enumerator, so it must clear it on the way out.");

                AddressableAssetTree.TreeNode lateNode = tree.FindNode(lateFolderPath, false);
                Assert.IsNotNull(lateNode, "Expected the folder walk to have added the subfolder.");

                lateGroup = MakeFolderAddressable(lateFolderPath, "LateSingleGroup");
                Assert.IsFalse(lateNode.IsAddressable,
                    "The build owns the enumerator, so disposing it must have dropped the change subscription.");
            }
            finally
            {
                ScriptableObject.DestroyImmediate(script);
                if (lateGroup != null)
                    Settings.RemoveGroup(lateGroup);
                AssetDatabase.DeleteAsset(lateFolderPath);
            }
        }
    }
}
