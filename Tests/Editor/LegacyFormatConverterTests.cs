#if UNITY_6000_0_OR_NEWER
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using NUnit.Framework;
using UnityEditor.AddressableAssets.Build;
using UnityEngine;
using UnityEngine.TestTools;

namespace UnityEditor.AddressableAssets.Tests
{
    public class LegacyFormatConverterTests
    {
        private string m_TempDir;

        [SetUp]
        public void SetUp()
        {
            m_TempDir = Path.Combine(Path.GetTempPath(), "LegacyFormatConverterTests_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(m_TempDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(m_TempDir))
                Directory.Delete(m_TempDir, true);
        }

        [Test]
        public void GetConverterToolPath_ReturnsExistingTool()
        {
            var toolPath = LegacyFormatConverter.GetConverterToolPath();
            Assert.IsTrue(File.Exists(toolPath), $"Converter tool not found at {toolPath}");
        }

        [Test]
        [UnityPlatform(RuntimePlatform.OSXEditor)]
        public void GetConverterToolPath_OnMac_MatchesEditorArchitecture()
        {
            var expectedFolder = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "osx-arm64" : "osx-x64";
            var toolFolder = Path.GetFileName(Path.GetDirectoryName(LegacyFormatConverter.GetConverterToolPath()));
            Assert.AreEqual(expectedFolder, toolFolder);
        }

        [Test]
        [UnityPlatform(RuntimePlatform.OSXEditor, RuntimePlatform.LinuxEditor)]
        public void EnsureExecutable_WithNonExecutableFile_SetsExecuteBit()
        {
            var path = Path.Combine(m_TempDir, "tool");
            File.WriteAllText(path, "#!/bin/sh\nexit 0\n");
            Assert.AreEqual(0, RunAndGetExitCode("/bin/chmod", $"644 \"{path}\""));
            Assert.AreNotEqual(0, RunAndGetExitCode("/bin/test", $"-x \"{path}\""), "File should start out non-executable");

            LegacyFormatConverter.EnsureExecutable(path);

            Assert.AreEqual(0, RunAndGetExitCode("/bin/test", $"-x \"{path}\""), "File should be executable");
        }

        private static int RunAndGetExitCode(string fileName, string arguments)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = Process.Start(startInfo))
            {
                process.WaitForExit();
                return process.ExitCode;
            }
        }
    }
}
#endif
