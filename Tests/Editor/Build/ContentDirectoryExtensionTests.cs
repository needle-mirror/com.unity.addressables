#if ENABLE_CONTENT_DIRECTORIES
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Unity.Content;
using Unity.IO.Archive;
using UnityEditor;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Build.DataBuilders.SchemaBuilders;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Build.Pipeline.Utilities;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.TestTools;

namespace UnityEditor.AddressableAssets.Tests
{
    /// <summary>
    /// Validates the category-to-extension mapping used when composing content directory file
    /// names, and that an Addressables content directory build produces the same artifact
    /// files as an equivalent native content directory build -- both as loose files and as
    /// entries inside content archives.
    /// </summary>
    public class ContentDirectoryExtensionTests : AddressableAssetTestBase
    {
        string m_OutputDir;

        [SetUp]
        public void Setup()
        {
            m_OutputDir = Path.Combine("Temp", "ContentDirectoryExtensionTests");
            if (Directory.Exists(m_OutputDir))
                Directory.Delete(m_OutputDir, true);
            Directory.CreateDirectory(m_OutputDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(m_OutputDir))
                Directory.Delete(m_OutputDir, true);
        }

        (ContentDirectorySchemaBuilder schemaBuilder, ContentDirectorySchemaBuilder.ContentLayout layout) BuildContentDirectoryGroup(ContentDirectoryGroupSchema schema)
        {
            var schemaBuilder = new ContentDirectorySchemaBuilder();
            var aaContext = new AddressableAssetsBuildContext { Settings = Settings };
            var input = new AddressablesDataBuilderInput(Settings);
            input.Logger = new BuildLog();
            var buildResult = new AddressablesPlayerBuildResult();

            schemaBuilder.Init(aaContext, input, null, null);
            string error = schemaBuilder.ProcessGroupSchema(aaContext, schema);
            Assert.IsEmpty(error, $"ProcessGroupSchema failed: {error}");
            schemaBuilder.Build(aaContext, buildResult);

            Assert.AreEqual(1, buildResult.ContentDirectoryBuildResults.Count);
            var layout = ContentDirectorySchemaBuilder.ContentLayout.Load(buildResult.ContentDirectoryBuildResults[0].BuildReportDirectory);
            Assert.IsNotEmpty(layout.BinaryArtifacts);
            return (schemaBuilder, layout);
        }

        [TestCase("ContentFile", ".cf")]
        [TestCase("TEXTURE", ".resS")]
        [TestCase("Manifest", ".json")]
        public void GetExtensionForCategory_IsCaseInsensitive(string category, string expectedExtension)
        {
            Assert.AreEqual(expectedExtension, ContentDirectorySchemaBuilder.ContentDirectoryFileCategory.GetExtensionForCategory(category));
        }

        [TestCase("unknowncategory", ".unknowncategory")]
        public void GetExtensionForCategory_UnknownCategory_FallsBackToCategoryName(string category, string expectedExtension)
        {
            Assert.AreEqual(expectedExtension, ContentDirectorySchemaBuilder.ContentDirectoryFileCategory.GetExtensionForCategory(category));
        }

        [TestCase(null)]
        [TestCase("")]
        public void GetExtensionForCategory_NullOrEmptyCategory_ReturnsEmpty(string category)
        {
            Assert.AreEqual(string.Empty, ContentDirectorySchemaBuilder.ContentDirectoryFileCategory.GetExtensionForCategory(category));
        }

        // Builds the same content through both pipelines -- an Addressables content directory
        // group build, then a direct BuildPipeline.BuildContentDirectory using the root asset
        // the Addressables build produced -- and verifies that every artifact file the
        // Addressables build created has an identically-named equivalent in the native
        // content directory build output.
        [Test]
        public void AddressablesBuild_ArtifactFiles_MatchEquivalentContentDirectoryBuild()
        {
            // The build name is serialized into the BuildManifest, which is itself a hashed
            // artifact, so the native build below is given the same name as the Addressables one.
            string addressablesOutputDir = Path.Combine(m_OutputDir, "addressables", "output");
            string nativeOutputDir = Path.Combine(m_OutputDir, "native", "output");

            (string wavPath, string texturePath) = CreateTestAssets();

            var group = Settings.CreateGroup("ContentDirectoryExtensionTestGroup", false, false, false, null, typeof(ContentDirectoryGroupSchema));
            var originalArchiveMode = Settings.ContentDirectoryArchiveMode;
            try
            {
                var schema = group.GetSchema<ContentDirectoryGroupSchema>();
                Settings.profileSettings.SetValue(Settings.activeProfileId, AddressableAssetSettings.kLocalBuildPath, addressablesOutputDir);
                Settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(wavPath), group, false, false);
                Settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(texturePath), group, false, false);
                Settings.ContentDirectoryArchiveMode = ContentDirectoryArchiveMode.None;

                var (schemaBuilder, layout) = BuildContentDirectoryGroup(schema);

                // The equivalent native build: same root asset the Addressables build just
                // produced, same options as ContentDirectorySchemaBuilder.Build computes.
                var buildParams = new BuildContentDirectoryParameters
                {
                    outputPath = nativeOutputDir,
                    rootAssetPaths = new[] { $"{schemaBuilder.RootAssetBuildPath}/AddressableRootAsset.asset" },
                    name = ContentDirectorySchemaBuilder.kContentDirectoryBuildName,
                };
                if (Settings.DisableWriteTypeTree)
                    buildParams.options |= BuildContentOptions.DisableWriteTypeTree | BuildContentOptions.SerializeUnityVersion;
                else if (!Settings.StripUnityVersion)
                    buildParams.options |= BuildContentOptions.SerializeUnityVersion;
                BuildReport report = BuildPipeline.BuildContentDirectory(buildParams);
                Assert.AreEqual(BuildResult.Succeeded, report.summary.result, "Native content directory build failed");

                var categories = new HashSet<string>();
                // construct the expected filenames using the layout created by the content directory build
                // then confirm that each expected file is present in both builds
                foreach (var artifact in layout.BinaryArtifacts)
                {
                    categories.Add(artifact.Category);
                    string fileName = artifact.ContentHash + ContentDirectorySchemaBuilder.ContentDirectoryFileCategory.GetExtensionForCategory(artifact.Category);
                    Assert.IsTrue(File.Exists(Path.Combine(addressablesOutputDir, fileName)),
                        $"Artifact {artifact.ContentHash} (category '{artifact.Category}') should exist in the Addressables build output as '{fileName}'.");
                    Assert.IsTrue(File.Exists(Path.Combine(nativeOutputDir, fileName)),
                        $"Addressables artifact file '{fileName}' (category '{artifact.Category}') has no equivalent in the content directory build output.");
                }

                CollectionAssert.Contains(categories, ContentDirectorySchemaBuilder.ContentDirectoryFileCategory.ContentFile);
                CollectionAssert.Contains(categories, ContentDirectorySchemaBuilder.ContentDirectoryFileCategory.Manifest);
                CollectionAssert.Contains(categories, ContentDirectorySchemaBuilder.ContentDirectoryFileCategory.Audio);
                CollectionAssert.Contains(categories, ContentDirectorySchemaBuilder.ContentDirectoryFileCategory.Texture);
            }
            finally
            {
                Settings.ContentDirectoryArchiveMode = originalArchiveMode;
                Settings.RemoveGroup(group);
            }
        }

        [TestCase(ContentDirectoryArchiveMode.Uncompressed)]
        [TestCase(ContentDirectoryArchiveMode.Lz4)]
        public void ArchivedAddressablesBuild_FilesInsideArchives_HaveNativeExtensions(ContentDirectoryArchiveMode archiveMode)
        {
            string outputDir = Path.Combine(m_OutputDir, "archived", archiveMode.ToString(), "output");
            (string wavPath, string texturePath) = CreateTestAssets();

            var group = Settings.CreateGroup($"ContentDirectoryArchiveExtensionTestGroup{archiveMode}", false, false, false, null, typeof(ContentDirectoryGroupSchema));
            var originalArchiveMode = Settings.ContentDirectoryArchiveMode;
            try
            {
                var schema = group.GetSchema<ContentDirectoryGroupSchema>();
                Settings.profileSettings.SetValue(Settings.activeProfileId, AddressableAssetSettings.kLocalBuildPath, outputDir);
                Settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(wavPath), group, false, false);
                Settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(texturePath), group, false, false);
                Settings.ContentDirectoryArchiveMode = archiveMode;

                var (_, layout) = BuildContentDirectoryGroup(schema);

                var expectedNames = new HashSet<string>(layout.BinaryArtifacts.Select(a =>
                    a.ContentHash + ContentDirectorySchemaBuilder.ContentDirectoryFileCategory.GetExtensionForCategory(a.Category)));

                var archivedFileNames = new HashSet<string>(ReadArchivedFileNames(outputDir));

                CollectionAssert.AreEquivalent(expectedNames, archivedFileNames,
                    "Every file inside the content archives should be a ContentLayout artifact named as its content hash plus extension.");

                var archivedExtensions = new HashSet<string>(archivedFileNames.Select(Path.GetExtension));
                CollectionAssert.IsSubsetOf(new[] { ".cf", ".resS", ".resource", ".json" }, archivedExtensions);
            }
            finally
            {
                Settings.ContentDirectoryArchiveMode = originalArchiveMode;
                Settings.RemoveGroup(group);
            }
        }

        [InitializeOnLoadMethod]
        static void ConditionalIgnoreFlag()
        {
#if UNITY_6000_7_OR_NEWER
            ConditionalIgnoreAttribute.AddConditionalIgnoreMapping("IgnoreUnity6.6OrOlder", false);
#else
            ConditionalIgnoreAttribute.AddConditionalIgnoreMapping("IgnoreUnity6.6OrOlder", true);
#endif
        }

        [Test, ConditionalIgnore("IgnoreUnity6.6OrOlder", "Content Directory builds only output duplicate BinaryArtifacts in Unity 6.7 or newer")]
        public void ArchiveFromUDS_AssetsSharingAnArtifactHash_ArchivesThatArtifactOnce()
        {
            string outputDir = Path.Combine(m_OutputDir, "duplicateArtifacts", "output");
            (string firstFontPath, string secondFontPath) = CreateIdenticalFonts();

            var group = Settings.CreateGroup("ContentDirectoryDuplicateArtifactTestGroup", false, false, false, null, typeof(ContentDirectoryGroupSchema));
            var originalArchiveMode = Settings.ContentDirectoryArchiveMode;
            try
            {
                var schema = group.GetSchema<ContentDirectoryGroupSchema>();
                Settings.profileSettings.SetValue(Settings.activeProfileId, AddressableAssetSettings.kLocalBuildPath, outputDir);
                Settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(firstFontPath), group, false, false);
                Settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(secondFontPath), group, false, false);
                Settings.ContentDirectoryArchiveMode = ContentDirectoryArchiveMode.Lz4;

                var (_, layout) = BuildContentDirectoryGroup(schema);

                var repeatedArtifacts = layout.BinaryArtifacts
                    .GroupBy(a => a.ContentHash)
                    .Where(g => g.Count() > 1)
                    .ToList();
                Assert.IsNotEmpty(repeatedArtifacts,
                    "Two assets with an identical binary result should make the content directory build list a shared artifact more than once. Layout: " +
                    string.Join(", ", layout.BinaryArtifacts.Select(a => $"{a.ContentHash}/{a.Category}/{a.Size}")));

                var archivedFileNames = ReadArchivedFileNames(outputDir);

                CollectionAssert.AllItemsAreUnique(archivedFileNames,
                    "An artifact the layout repeats must not be written into the content archives more than once.");
                CollectionAssert.AreEquivalent(
                    layout.BinaryArtifacts
                        .Select(a => a.ContentHash + ContentDirectorySchemaBuilder.ContentDirectoryFileCategory.GetExtensionForCategory(a.Category))
                        .Distinct()
                        .ToList(),
                    archivedFileNames,
                    "The content archives should hold exactly one entry per distinct artifact file name in the layout.");
            }
            finally
            {
                Settings.ContentDirectoryArchiveMode = originalArchiveMode;
                Settings.RemoveGroup(group);
            }
        }

        [Test]
        public void ArchiveFromUDS_OneContentHashUnderTwoCategories_ArchivesBothArtifacts()
        {
            string buildDir = Path.Combine(m_OutputDir, "sameHashTwoCategories", "output");
            string metadataDir = Path.Combine(m_OutputDir, "sameHashTwoCategories", "metadata");
            string reArchivedDir = Path.Combine(m_OutputDir, "sameHashTwoCategories", "rearchived");
            string wavPath = ImportAudioClip("SameHashTwoCategoriesClip.wav");

            var group = Settings.CreateGroup("ContentDirectorySameHashTwoCategoriesTestGroup", false, false, false, null, typeof(ContentDirectoryGroupSchema));
            var originalArchiveMode = Settings.ContentDirectoryArchiveMode;
            try
            {
                var schema = group.GetSchema<ContentDirectoryGroupSchema>();
                Settings.profileSettings.SetValue(Settings.activeProfileId, AddressableAssetSettings.kLocalBuildPath, buildDir);
                Settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(wavPath), group, false, false);
                Settings.ContentDirectoryArchiveMode = ContentDirectoryArchiveMode.Lz4;

                var (_, layout) = BuildContentDirectoryGroup(schema);

                var artifact = layout.BinaryArtifacts.First(a =>
                    a.Category == ContentDirectorySchemaBuilder.ContentDirectoryFileCategory.ContentFile);
                const string otherCategory = ContentDirectorySchemaBuilder.ContentDirectoryFileCategory.Audio;

                Directory.CreateDirectory(metadataDir);
                File.WriteAllText(Path.Combine(metadataDir, "ContentLayout.json"),
                    $@"{{""BuildManifestHash"":""{layout.BuildManifestHash}"",""BinaryArtifacts"":[" +
                    $@"{{""ContentHash"":""{artifact.ContentHash}"",""Category"":""{artifact.Category}"",""Size"":{artifact.Size}}}," +
                    $@"{{""ContentHash"":""{artifact.ContentHash}"",""Category"":""{otherCategory}"",""Size"":{artifact.Size}}}]}}");

                Directory.CreateDirectory(reArchivedDir);
                ContentDirectorySchemaBuilder.ContentDirectoryArchiver.ArchiveFromUDS(
                    ContentDirectorySchemaBuilder.ContentLayout.Load(metadataDir), reArchivedDir,
                    4096L * 1024 * 1024, ContentDirectoryArchiveMode.Lz4, new BuildLog());

                var expectedNames = new[]
                {
                    artifact.ContentHash + ContentDirectorySchemaBuilder.ContentDirectoryFileCategory.GetExtensionForCategory(artifact.Category),
                    artifact.ContentHash + ContentDirectorySchemaBuilder.ContentDirectoryFileCategory.GetExtensionForCategory(otherCategory),
                };
                CollectionAssert.AreEquivalent(expectedNames, ReadArchivedFileNames(reArchivedDir),
                    "Both categories of a shared content hash name a distinct archive entry, so neither may be stripped.");
            }
            finally
            {
                Settings.ContentDirectoryArchiveMode = originalArchiveMode;
                Settings.RemoveGroup(group);
            }
        }

        // Repeats included, so a caller can tell "archived once" from "archived twice".
        static List<string> ReadArchivedFileNames(string outputDir)
        {
            string[] archivePaths = Directory.GetFiles(outputDir, "*.archive");
            Assert.IsNotEmpty(archivePaths, $"An archived build should produce at least one content archive in {outputDir}.");

            var archivedFileNames = new List<string>();
            foreach (var archivePath in archivePaths)
            {
                var archiveHandle = ArchiveFileInterface.MountAsync(ContentNamespace.Default, archivePath, "test:");
                archiveHandle.JobHandle.Complete();
                try
                {
                    Assert.AreEqual(ArchiveStatus.Complete, archiveHandle.Status, $"Failed to mount {archivePath}");
                    foreach (var fileInArchive in archiveHandle.GetFileInfo())
                        archivedFileNames.Add(Path.GetFileName(fileInArchive.Filename));
                }
                finally
                {
                    archiveHandle.Unmount().Complete();
                }
            }

            return archivedFileNames;
        }

        // A font generates sub-assets that do not carry the asset name, so two copies under
        // different names build to the same artifact. The fixture is the Editor's System Normal.ttf.
        (string firstPath, string secondPath) CreateIdenticalFonts()
        {
            string sourcePath = $"{AddressablesTestUtility.GetPackagePath()}/Tests/Editor/Build/fonts~/SystemNormal.ttf";
            return (ImportFontCopy(sourcePath, "IdenticalFontA.ttf"), ImportFontCopy(sourcePath, "IdenticalFontB.ttf"));
        }

        string ImportFontCopy(string sourcePath, string fileName)
        {
            string fontPath = $"{TestFolder}/{fileName}";
            File.Copy(sourcePath, fontPath, true);
            AssetDatabase.ImportAsset(fontPath, ImportAssetOptions.ForceSynchronousImport);
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Font>(fontPath), $"Failed to import {fontPath}");
            return fontPath;
        }

        string ImportStreamedTexture(string fileName, byte[] pngBytes)
        {
            string texturePath = $"{TestFolder}/{fileName}";
            File.WriteAllBytes(texturePath, pngBytes);
            AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceSynchronousImport);
            var textureImporter = (TextureImporter)AssetImporter.GetAtPath(texturePath);
            textureImporter.mipmapEnabled = true;
            textureImporter.streamingMipmaps = true;
            textureImporter.SaveAndReimport();
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            return texturePath;
        }

        // Creates an AudioClip and a mip-streamed Texture2D under the test asset folder so the
        // builds produce streamed resource artifacts in addition to content files.
        (string wavPath, string texturePath) CreateTestAssets()
        {
            return (ImportAudioClip("TestClip.wav"), ImportStreamedTexture("TestTexture.png", CreateTexturePngBytes()));
        }

        string ImportAudioClip(string fileName)
        {
            string wavPath = $"{TestFolder}/{fileName}";
            File.WriteAllBytes(wavPath, CreateWavBytes());
            AssetDatabase.ImportAsset(wavPath, ImportAssetOptions.ForceSynchronousImport);
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<AudioClip>(wavPath));
            return wavPath;
        }

        static byte[] CreateTexturePngBytes()
        {
            var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            var pixels = new Color32[64 * 64];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32((byte)(i % 255), (byte)(i / 64), 128, 255);
            texture.SetPixels32(pixels);
            texture.Apply();
            byte[] bytes = texture.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(texture);
            return bytes;
        }

        // Minimal 16-bit mono PCM WAV with a short sine wave.
        static byte[] CreateWavBytes(int sampleCount = 4410, int sampleRate = 44100)
        {
            int dataSize = sampleCount * 2;
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(new[] { 'R', 'I', 'F', 'F' });
                writer.Write(36 + dataSize);
                writer.Write(new[] { 'W', 'A', 'V', 'E' });
                writer.Write(new[] { 'f', 'm', 't', ' ' });
                writer.Write(16);
                writer.Write((short)1); // PCM
                writer.Write((short)1); // mono
                writer.Write(sampleRate);
                writer.Write(sampleRate * 2); // byte rate
                writer.Write((short)2); // block align
                writer.Write((short)16); // bits per sample
                writer.Write(new[] { 'd', 'a', 't', 'a' });
                writer.Write(dataSize);
                for (int i = 0; i < sampleCount; i++)
                    writer.Write((short)(Math.Sin(2 * Math.PI * 440 * i / sampleRate) * short.MaxValue * 0.5));
                return stream.ToArray();
            }
        }
    }
}
#endif
