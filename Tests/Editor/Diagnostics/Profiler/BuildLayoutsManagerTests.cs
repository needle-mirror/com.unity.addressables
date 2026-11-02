using System;
using System.IO;
using NUnit.Framework;
using UnityEditor.AddressableAssets.Build.Layout;
using UnityEditor.AddressableAssets.Diagnostics;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.Profiling;

namespace UnityEditor.AddressableAssets.Tests.Diagnostics.Profiler
{
    public class BuildLayoutsManagerTests
    {
        string m_PreviousBuildReportPath;
        BuildLayoutsManager m_Manager;

        [SetUp]
        public void SetUp()
        {
            m_PreviousBuildReportPath = Addressables.BuildReportPath;
            Addressables.BuildReportPath = Path.Combine("Library", "BuildLayoutsManagerTests_" + Guid.NewGuid().ToString("N")) + "/";
            Directory.CreateDirectory(Addressables.BuildReportPath);
            m_Manager = new BuildLayoutsManager();
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(Addressables.BuildReportPath))
                Directory.Delete(Addressables.BuildReportPath, true);
            Addressables.BuildReportPath = m_PreviousBuildReportPath;
        }

        static int ObjectCount(BuildLayout.ExplicitAsset asset) => asset.Objects.Count;

        [Test]
        public void ActivatingNewLayout_AttachesObjectsFromItsProfilerFile()
        {
            string hash = Hash128.Compute("attach").ToString();
            BuildLayoutTestUtility.WriteSplit(BuildLayoutTestUtility.CreateLayout(hash, "a"), Addressables.BuildReportPath);

            m_Manager.LoadReports();
            m_Manager.AddActiveLayout(hash);

            Assert.AreEqual(2, ObjectCount(m_Manager.GetAsset("a".GetHashCode(), "asset_a".GetHashCode())));
        }

        [Test]
        public void ActivatingOldLayout_KeepsInlineObjects()
        {
            string hash = Hash128.Compute("old").ToString();
            BuildLayoutTestUtility.CreateLayout(hash, "a").WriteToFile(Path.Combine(Addressables.BuildReportPath, "buildlayout_1.json"), true);

            m_Manager.LoadReports();
            m_Manager.AddActiveLayout(hash);

            Assert.AreEqual(2, ObjectCount(m_Manager.GetAsset("a".GetHashCode(), "asset_a".GetHashCode())));
        }

        [Test]
        public void LoadReports_OnlyOpensLayoutFiles()
        {
            string layoutHash = Hash128.Compute("layout").ToString();
            string otherHash = Hash128.Compute("othernames").ToString();
            BuildLayoutTestUtility.CreateLayout(layoutHash, "a").WriteToFile(Path.Combine(Addressables.BuildReportPath, "buildlayout_1.json"), true);
            // a valid layout under another name, like a TEP or steps file, is never opened
            BuildLayoutTestUtility.CreateLayout(otherHash, "b").WriteToFile(Path.Combine(Addressables.BuildReportPath, "buildlayoutTEP_1.json"), true);
            BuildLayoutTestUtility.CreateLayout(otherHash, "b").WriteToFile(Path.Combine(Addressables.BuildReportPath, "buildlayoutSteps_1.json"), true);

            m_Manager.LoadReports();

            m_Manager.AddActiveLayout(layoutHash);
            Assert.Throws<Exception>(() => m_Manager.AddActiveLayout(otherHash));
        }

        [Test]
        public void LoadManualReport_ProfilerFile_LoadsItsLayout()
        {
            string hash = Hash128.Compute("manual").ToString();
            BuildLayoutTestUtility.WriteSplit(BuildLayoutTestUtility.CreateLayout(hash, "a"), Addressables.BuildReportPath);

            Assert.IsTrue(m_Manager.LoadManualReport(Path.Combine(Addressables.BuildReportPath, "buildlayout_1.profiler")));
            m_Manager.AddActiveLayout(hash);

            Assert.AreEqual(2, ObjectCount(m_Manager.GetAsset("a".GetHashCode(), "asset_a".GetHashCode())));
        }

        [Test]
        public void SetActiveReports_SameHashTwice_ReusesActiveLayout()
        {
            string hash = Hash128.Compute("reuse").ToString();
            BuildLayoutTestUtility.WriteSplit(BuildLayoutTestUtility.CreateLayout(hash, "a"), Addressables.BuildReportPath);
            m_Manager.LoadReports();

            CatalogFrameData[] catalogs = { new CatalogFrameData { BuildResultHash = Hash128.Parse(hash) } };

            Assert.IsNull(m_Manager.SetActiveReportsAndGetMissingBuildHashes(catalogs));
            Assert.AreEqual(1, m_Manager.ActiveLayouts.Count);
            BuildLayoutsManager.ActiveLayout first = m_Manager.ActiveLayouts[0];

            Assert.IsNull(m_Manager.SetActiveReportsAndGetMissingBuildHashes(catalogs));
            Assert.AreEqual(1, m_Manager.ActiveLayouts.Count);
            Assert.AreSame(first, m_Manager.ActiveLayouts[0]);
        }
    }
}
