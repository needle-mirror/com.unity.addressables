using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets.Build.Layout;
using UnityEngine;
using UnityEngine.TestTools;

namespace UnityEditor.AddressableAssets.Tests.Diagnostics
{
    public class ProfilerDataTests
    {
        string m_Directory;

        [SetUp]
        public void SetUp()
        {
            m_Directory = Path.Combine("Library", "ProfilerDataTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(m_Directory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(m_Directory))
                Directory.Delete(m_Directory, true);
        }

        static BuildLayout.File FirstFile(BuildLayout layout, int bundleIndex = 0)
        {
            return layout.Groups[0].Bundles[bundleIndex].Files[0];
        }

        [Test]
        public void WriteThenAttach_RestoresObjectsOnEveryAsset()
        {
            BuildLayout source = BuildLayoutTestUtility.CreateLayout(Hash128.Compute("attach").ToString(), "a", "b");
            string layoutPath = BuildLayoutTestUtility.WriteSplit(source, m_Directory);

            BuildLayout read = BuildLayout.Open(layoutPath, readFullFile: true);
            Assert.AreEqual(0, FirstFile(read).Assets[0].Objects.Count);

            Assert.IsTrue(ProfilerData.TryAttachObjects(read));

            for (int i = 0; i < 2; i++)
            {
                BuildLayout.File file = FirstFile(read, i);
                Assert.AreEqual(2, file.Assets[0].Objects.Count);
                Assert.AreEqual(1, file.OtherAssets[0].Objects.Count);
            }

            BuildLayout.ObjectData root = FirstFile(read).Assets[0].Objects[0];
            Assert.AreEqual(1, root.LocalIdentifierInFile);
            Assert.AreEqual("Root", root.ObjectName);
            Assert.AreEqual(AssetType.GameObject, root.AssetType);
            Assert.AreEqual(100, root.SerializedSize);
            Assert.AreEqual(1, root.References.Count);
            Assert.AreEqual(1, root.References[0].AssetId);
            CollectionAssert.AreEqual(new[] { 0 }, root.References[0].ObjectIds);
            Assert.AreEqual("MeshRenderer", FirstFile(read).Assets[0].Objects[1].ComponentName);
            Assert.AreEqual(AssetType.Texture2D, FirstFile(read).OtherAssets[0].Objects[0].AssetType);
        }

        [Test]
        public void MoveToFile_WritesHeaderThenOneLinePerFileAndEmptiesLayout()
        {
            BuildLayout layout = BuildLayoutTestUtility.CreateLayout(Hash128.Compute("lines").ToString(), "a", "b", "c");
            string path = Path.Combine(m_Directory, "buildlayout_1.profiler");

            ProfilerData.MoveToFile(layout, path, Path.Combine(m_Directory, "buildlayout_1.json"));

            string[] lines = File.ReadAllLines(path);
            Assert.AreEqual(4, lines.Length);
            StringAssert.StartsWith("{\"ProfilerFileVersion\":", lines[0]);
            StringAssert.Contains(layout.AddressablesBuildSessionGUID.ToString(), lines[0]);
            StringAssert.Contains("\"LayoutFileName\":\"buildlayout_1.json\"", lines[0]);
            StringAssert.Contains("\"Name\":\"CAB-a\"", lines[1]);
            StringAssert.DoesNotContain("\"Name\":\"CAB-b\"", lines[1]);
            Assert.IsTrue(ProfilerData.IsProfilerFile(path));

            Assert.AreEqual(0, FirstFile(layout).Assets[0].Objects.Count);
            Assert.AreEqual(0, FirstFile(layout).OtherAssets[0].Objects.Count);
            Assert.AreEqual("buildlayout_1.profiler", layout.ProfilerFileName);
        }

        [Test]
        public void IsProfilerFile_RejectsLayoutsAndMissingFiles()
        {
            BuildLayout layout = BuildLayoutTestUtility.CreateLayout(Hash128.Compute("classify").ToString(), "a");
            string layoutPath = Path.Combine(m_Directory, "buildlayout_1.json");
            layout.WriteToFile(layoutPath, true);

            Assert.IsFalse(ProfilerData.IsProfilerFile(layoutPath));
            Assert.IsFalse(ProfilerData.IsProfilerFile(Path.Combine(m_Directory, "missing.json")));
        }

        [Test]
        public void TryAttachObjects_DifferentBuild_LeavesObjectsEmptyAndWarns()
        {
            string layoutPath = BuildLayoutTestUtility.WriteSplit(BuildLayoutTestUtility.CreateLayout(Hash128.Compute("first").ToString(), "a"), m_Directory);
            BuildLayout read = BuildLayout.Open(layoutPath, readFullFile: true);
            read.BuildSessionGuidString = GUID.Generate().ToString();

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("is from a different build"));
            Assert.IsFalse(ProfilerData.TryAttachObjects(read));
            Assert.AreEqual(0, FirstFile(read).Assets[0].Objects.Count);
        }

        [Test]
        public void TryAttachObjects_UnknownFile_IsSkippedWithOneWarning()
        {
            string layoutPath = BuildLayoutTestUtility.WriteSplit(BuildLayoutTestUtility.CreateLayout(Hash128.Compute("unknown").ToString(), "a", "b"), m_Directory);
            BuildLayout read = BuildLayout.Open(layoutPath, readFullFile: true);
            read.Groups[0].Bundles[0].InternalName = "renamed";
            FirstFile(read, 1).Name = "CAB-renamed";

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("1 entries .* were skipped"));
            Assert.IsTrue(ProfilerData.TryAttachObjects(read));
            Assert.AreEqual(2, FirstFile(read, 0).Assets[0].Objects.Count);
            Assert.AreEqual(0, FirstFile(read, 1).Assets[0].Objects.Count);
        }

        [Test]
        public void TryAttachObjects_ProfilerFileMissing_WarnsAndReturnsFalse()
        {
            string layoutPath = BuildLayoutTestUtility.WriteSplit(BuildLayoutTestUtility.CreateLayout(Hash128.Compute("missing").ToString(), "a"), m_Directory);
            File.Delete(Path.Combine(m_Directory, "buildlayout_1.profiler"));

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("was not found next to"));
            Assert.IsFalse(ProfilerData.TryAttachObjects(BuildLayout.Open(layoutPath, readFullFile: true)));
        }

        [Test]
        public void TryAttachObjects_RenamedLayout_StillFindsItsObjects()
        {
            string layoutPath = BuildLayoutTestUtility.WriteSplit(BuildLayoutTestUtility.CreateLayout(Hash128.Compute("renamed").ToString(), "a"), m_Directory);
            string renamedPath = Path.Combine(m_Directory, "renamed report.json");
            File.Move(layoutPath, renamedPath);

            BuildLayout read = BuildLayout.Open(renamedPath, readFullFile: true);
            Assert.IsTrue(ProfilerData.TryAttachObjects(read));
            Assert.AreEqual(2, FirstFile(read).Assets[0].Objects.Count);
        }

        [Test]
        public void Paths_ComeFromTheNamesEachFileStores()
        {
            string layoutPath = BuildLayoutTestUtility.WriteSplit(BuildLayoutTestUtility.CreateLayout(Hash128.Compute("paths").ToString(), "a"), m_Directory);
            BuildLayout read = BuildLayout.Open(layoutPath, readFullFile: true);

            string profilerPath = ProfilerData.GetProfilerPath(read);
            Assert.AreEqual(Path.Combine(m_Directory, "buildlayout_1.profiler"), profilerPath);
            Assert.AreEqual(Path.Combine(m_Directory, "buildlayout_1.json"), ProfilerData.GetLayoutPath(profilerPath));

            BuildLayout oldLayout = BuildLayoutTestUtility.CreateLayout(Hash128.Compute("old").ToString(), "a");
            oldLayout.m_FilePath = layoutPath;
            Assert.IsNull(ProfilerData.GetProfilerPath(oldLayout));
        }
    }
}
