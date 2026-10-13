#if UNITY_6000_0_OR_NEWER
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace UnityEditor.AddressableAssets.Build
{
    /// <summary>
    /// Handles conversion of legacy BinaryFormatter files to the new DataContractSerializer format.
    /// This class provides utilities for detecting legacy formats and running the converter tool.
    /// </summary>
    internal static class LegacyFormatConverter
    {
        /// <summary>
        /// Version marker for content state files (.bin): "ACS" + version 1
        /// </summary>
        public static readonly byte[] ContentStateVersionMarker = { 0x41, 0x43, 0x53, 0x01 };

        /// <summary>
        /// Version marker for config data files (.dat): "ACD" + version 1
        /// </summary>
        public static readonly byte[] ConfigDataVersionMarker = { 0x41, 0x43, 0x44, 0x01 };

        /// <summary>
        /// Checks if a file is in the legacy BinaryFormatter format by comparing against the expected version marker.
        /// </summary>
        /// <param name="path">Path to the file to check.</param>
        /// <param name="versionMarker">The version marker bytes expected for the new format.</param>
        /// <returns>True if the file is in legacy format (doesn't have the version marker), false otherwise.</returns>
        public static bool IsLegacyFormat(string path, byte[] versionMarker)
        {
            if (!File.Exists(path))
                return false;

            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                return IsLegacyFormat(stream, versionMarker);
            }
        }

        /// <summary>
        /// Checks if a stream contains legacy BinaryFormatter format data.
        /// </summary>
        /// <param name="stream">The stream to check. Position will be reset after checking.</param>
        /// <param name="versionMarker">The version marker bytes expected for the new format.</param>
        /// <returns>True if the stream contains legacy format data.</returns>
        public static bool IsLegacyFormat(Stream stream, byte[] versionMarker)
        {
            if (stream.Length < versionMarker.Length)
                return true; // Too short, assume legacy

            var originalPosition = stream.Position;
            var header = new byte[versionMarker.Length];
            stream.Read(header, 0, versionMarker.Length);
            stream.Position = originalPosition;

            for (int i = 0; i < versionMarker.Length; i++)
            {
                if (header[i] != versionMarker[i])
                    return true; // Doesn't match new format marker, assume legacy
            }

            return false;
        }

        /// <summary>
        /// Gets the path to the converter tool for the current platform.
        /// The tool is located in the package's Editor/Tools~ directory.
        /// </summary>
        /// <returns>Path to the converter tool executable.</returns>
        public static string GetConverterToolPath()
        {
            var packagePath = Path.GetFullPath("Packages/com.unity.addressables");
            var toolsPath = Path.Combine(packagePath, "Editor", "Tools~");

#if UNITY_EDITOR_WIN
            return Path.Combine(toolsPath, "win/AddressablesFormatConverter.exe");
#elif UNITY_EDITOR_OSX
            // There is no scripting define for the Editor's CPU architecture, so check it at runtime.
            // An x64 Editor running under Rosetta reports X64 and gets the x64 tool, which Rosetta can also run.
            if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
                return Path.Combine(toolsPath, "osx-arm64/AddressablesFormatConverter");
            return Path.Combine(toolsPath, "osx-x64/AddressablesFormatConverter");
#elif UNITY_EDITOR_LINUX
            return Path.Combine(toolsPath, "linux/AddressablesFormatConverter");
#else
            return Path.Combine(toolsPath, "win/AddressablesFormatConverter.exe");
#endif
        }

        /// <summary>
        /// Makes sure the converter tool can be launched on macOS and Linux.
        /// The package can be extracted without the execute bit set on the tool, which makes starting it fail with "Access denied".
        /// </summary>
        /// <param name="toolPath">Path to the converter tool executable.</param>
        internal static void EnsureExecutable(string toolPath)
        {
#if !UNITY_EDITOR_WIN
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "/bin/chmod",
                    Arguments = $"+x \"{toolPath}\"",
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(startInfo))
                {
                    if (!process.WaitForExit(10000))
                    {
                        process.Kill();
                        process.WaitForExit();
                        Debug.LogWarning($"Timed out making the format converter tool executable at {toolPath}.");
                        return;
                    }

                    if (process.ExitCode != 0)
                        Debug.LogWarning($"Could not make the format converter tool executable at {toolPath}: {process.StandardError.ReadToEnd()}");
                }
            }
            catch (Exception e)
            {
                // Let the launch attempt that follows report the actual failure.
                Debug.LogWarning($"Could not make the format converter tool executable at {toolPath}: {e.Message}");
            }
#endif
        }

        /// <summary>
        /// Converts a legacy BinaryFormatter file to the new format using the converter tool.
        /// </summary>
        /// <param name="legacyPath">Path to the legacy file.</param>
        /// <param name="logAsWarning">If true, logs failures as warnings instead of errors.</param>
        /// <returns>Path to the converted file (same as input, replaced in-place), or null if conversion failed.</returns>
        public static string ConvertLegacyFile(string legacyPath, bool logAsWarning = false)
        {
            var converterPath = GetConverterToolPath();
            if (string.IsNullOrEmpty(converterPath) || !File.Exists(converterPath))
            {
                var message = $"Format converter tool not found at: {converterPath}";
                if (logAsWarning)
                    Debug.LogWarning(message);
                else
                    Debug.LogError(message);
                return null;
            }

            var convertedPath = legacyPath + ".converted";

            EnsureExecutable(converterPath);

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = converterPath,
                    Arguments = $"\"{legacyPath}\" \"{convertedPath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(startInfo))
                {
                    process.WaitForExit(60000); // 60 second timeout

                    if (process.ExitCode != 0)
                    {
                        var error = process.StandardError.ReadToEnd();
                        var message = $"Format converter failed with exit code {process.ExitCode}: {error}";
                        if (logAsWarning)
                            Debug.LogWarning(message);
                        else
                            Debug.LogError(message);
                        return null;
                    }
                }

                if (!File.Exists(convertedPath))
                {
                    var message = "Format converter did not produce output file.";
                    if (logAsWarning)
                        Debug.LogWarning(message);
                    else
                        Debug.LogError(message);
                    return null;
                }

                // Replace the original file with the converted one
                File.Delete(legacyPath);
                File.Move(convertedPath, legacyPath);

                Debug.Log($"Successfully converted legacy file: {legacyPath}");
                return legacyPath;
            }
            catch (Exception e)
            {
                if (logAsWarning)
                    Debug.LogWarning($"Failed to convert legacy file: {e.Message}");
                else
                    Debug.LogException(e);
                return null;
            }
        }
    }
}
#endif
