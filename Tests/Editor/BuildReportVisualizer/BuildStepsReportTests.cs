using System;
using System.IO;
using NUnit.Framework;
using UnityEditor.AddressableAssets.Build.Layout;
using UnityEditor.AddressableAssets.BuildReportVisualizer;
using UnityEditor.Build.Pipeline.Interfaces;

namespace Tests.Editor.BuildReportVisualizer
{
    public class BuildStepsReportTests
    {
        string m_TempDirectory;

        [SetUp]
        public void CreateTempDirectory()
        {
            m_TempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(m_TempDirectory);
        }

        [TearDown]
        public void DeleteTempDirectory()
        {
            if (Directory.Exists(m_TempDirectory))
                Directory.Delete(m_TempDirectory, true);
        }

        static BuildStepData Step(string name, int depth, double durationMs)
        {
            return new BuildStepData { Name = name, Depth = depth, DurationMs = durationMs };
        }

        class FakeLog : ILogBuildSteps
        {
            internal BuildStepData[] Steps = Array.Empty<BuildStepData>();
            public BuildStepData[] GetBuildSteps() => Steps;
        }

        [Test]
        public void TryRead_ReadsBackWhatWriteWrote()
        {
            var log = new FakeLog
            {
                Steps = new[] { Step("Build", 0, 1234.5d), Step("Inner", 1, 6d) }
            };
            string path = Path.Combine(m_TempDirectory, BuildReportUtility.StepsFileName);
            BuildStepsReport.Write(log, path);

            Assert.IsTrue(BuildStepsReport.TryRead(path, out BuildStepData[] steps));
            Assert.AreEqual(2, steps.Length);
            Assert.AreEqual("Build", steps[0].Name);
            Assert.AreEqual(1234.5d, steps[0].DurationMs, 1e-9);
            Assert.AreEqual(1, steps[1].Depth);
        }

        [Test]
        public void TryRead_WithAFileFromAnotherVersion_ReturnsFalse()
        {
            string path = Path.Combine(m_TempDirectory, BuildReportUtility.StepsFileName);
            BuildStepsReport.Write(new FakeLog { Steps = new[] { Step("Build", 0, 1d) } }, path);
            File.WriteAllText(path, File.ReadAllText(path)
                .Replace($"\"Version\":{BuildStepsFile.CurrentVersion}", "\"Version\":-1"));

            Assert.IsFalse(BuildStepsReport.TryRead(path, out BuildStepData[] steps));
            Assert.IsEmpty(steps);
        }

        [Test]
        public void TryRead_WithNothingUsable_ReturnsFalse()
        {
            string path = Path.Combine(m_TempDirectory, BuildReportUtility.StepsFileName);

            Assert.IsFalse(BuildStepsReport.TryRead(path, out _), "There is no file.");

            File.WriteAllText(path, "not json at all");
            Assert.IsFalse(BuildStepsReport.TryRead(path, out _), "The file is not readable.");

            BuildStepsReport.Write(new FakeLog(), path);
            Assert.IsFalse(BuildStepsReport.TryRead(path, out _), "The build recorded no steps.");
        }

        BuildLayout LayoutAt(string fileName)
        {
            var layout = new BuildLayout();
            layout.m_FilePath = Path.Combine(m_TempDirectory, fileName);
            return layout;
        }

        [Test]
        public void TryGetFilePath_FindsTheStepsBesideABuildHistoryLayout()
        {
            BuildLayout layout = LayoutAt(BuildReportUtility.EmbeddedLayoutFileName);
            string expected = Path.Combine(m_TempDirectory, BuildReportUtility.StepsFileName);
            File.WriteAllText(expected, "{}");

            Assert.IsTrue(BuildStepsReport.TryGetFilePath(layout, out string path));
            Assert.AreEqual(expected, path);
        }

        [Test]
        public void TryGetFilePath_WithoutAStepsFile_ReturnsFalse()
        {
            Assert.IsFalse(BuildStepsReport.TryGetFilePath(LayoutAt(BuildReportUtility.EmbeddedLayoutFileName), out _),
                "Nothing was written beside the layout.");
            Assert.IsFalse(BuildStepsReport.TryGetFilePath(LayoutAt("something else.json"), out _),
                "That is not a layout this knows how to pair with.");
            Assert.IsFalse(BuildStepsReport.TryGetFilePath(new BuildLayout(), out _),
                "The report was never read from a file.");
            Assert.IsFalse(BuildStepsReport.TryGetFilePath(null, out _));
        }
    }
}
