using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor.Compilation;
using UnityEngine.TestTools;
using File = System.IO.File;
using Path = System.IO.Path;

/// <summary>
/// This test exists because we frequently suggest that our users extend or copy and paste the builder script
/// files to customize or make their own. We didn't check this on every release and so internal API usage
/// had crept in and made it impossible to do this without copying the entire package. This test verifies
/// that you can copy the script into your own namespace and it will compile.
/// </summary>
/// <remarks>
/// The scripts are compiled out of the project with AssemblyBuilder rather than copied into Assets.
/// That avoids an asset refresh, a full script compile and a domain reload for every test case.
/// </remarks>
public class VerifyPublicBuildScripts
{
    // Temp is not imported by the AssetDatabase, so nothing written here triggers a refresh.
    private const string k_TempRoot = "Temp/VerifyPublicBuildScripts";
    private const string k_SourceFolder = k_TempRoot + "/Source";

    // This name must not appear in any InternalsVisibleTo attribute in the package,
    // otherwise the compiled scripts would get access to the internal APIs this test guards against.
    private const string k_AssemblyPath = k_TempRoot + "/Output/VerifyPublicBuildScripts.Compiled.dll";

    private static readonly TimeSpan k_CompileTimeout = TimeSpan.FromMinutes(2);

    private string m_PackagePath;
    private string m_SamplePath;
    private string m_LoadScenePath;

    /// <summary>
    /// Test setup for validating build scripts
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        DeleteTempRoot();
        Directory.CreateDirectory(k_SourceFolder);

        m_PackagePath = "Packages/com.unity.addressables";
        m_SamplePath = "Samples";
        if (Directory.Exists(Path.Combine(m_PackagePath, "Samples~")))
        {
            // when packaging the samples are moved into a hidden directory
            m_SamplePath = "Samples~";
        }

        // this is a dependant class and copying is easier than an asmdef
        var loadScenePath = "Samples/CustomBuildAndPlaymodeScripts/LoadSceneForCustomBuild.cs";
        m_LoadScenePath = $"{k_SourceFolder}/{Path.GetFileName(loadScenePath)}";
        File.Copy(GetPackageFilePath(loadScenePath), m_LoadScenePath);
    }

    /// <summary>
    /// Test tear down
    /// </summary>
    [TearDown]
    public void TearDown()
    {
        DeleteTempRoot();
    }

    private static string[] BuildScripts =
    {
        "Editor/Build/DataBuilders/BuildScriptFastMode.cs",
        "Editor/Build/DataBuilders/BuildScriptPackedMode.cs",
        "Editor/Build/DataBuilders/BuildScriptPackedPlayMode.cs",
#if ENABLE_CONTENT_DIRECTORIES
        "Editor/Build/DataBuilders/BuildScriptSchemaDriven.cs",
#endif
        "Editor/Build/CatalogBuilders/JsonCatalogBuilder.cs",
        "Editor/Build/CatalogBuilders/BinaryCatalogBuilder.cs",
        "Runtime/ResourceProviders/JsonCatalogProvider.cs",
        "Runtime/ResourceProviders/BinaryCatalogProvider.cs",
        "Runtime/ResourceManager/ResourceProviders/InstanceProvider.cs",
        "Editor/Build/DataBuilders/SchemaBuilders/BundledAssetSchemaBuilder.cs",
        "Samples/CustomBuildAndPlaymodeScripts/Editor/CustomBuildScript.cs",
        "Samples/CustomBuildAndPlaymodeScripts/Editor/CustomPlayModeScript.cs",
    };

    /// <summary>
    /// A named group of build script files that must compile together.
    /// </summary>
    public class BuildScriptGroupData
    {
        /// <summary>
        /// The name of the build script group, used as the test case name.
        /// </summary>
        public readonly string Name;

        /// <summary>
        /// The package-relative file paths of the scripts in the group.
        /// </summary>
        public readonly string[] Files;

        /// <summary>
        /// Creates a new group of build script files.
        /// </summary>
        /// <param name="name">The name of the group.</param>
        /// <param name="files">The package-relative file paths of the scripts in the group.</param>
        public BuildScriptGroupData(string name, string[] files)
        {
            Name = name;
            Files = files;
        }

        /// <summary>
        /// Returns the group name.
        /// </summary>
        /// <returns>The name of the group.</returns>
        public override string ToString() => Name;
    }


#if ENABLE_CONTENT_DIRECTORIES
    private static BuildScriptGroupData[] BuildScriptGroups =
    {
        new BuildScriptGroupData("ContentDirectorySchemaBuilder", new[]
        {
            "Editor/Build/DataBuilders/SchemaBuilders/ContentDirectorySchemaBuilder.cs",
            "Editor/Build/DataBuilders/SchemaBuilders/ContentDirectoryArchiver.cs",
            "Editor/Build/DataBuilders/SchemaBuilders/WebGLContentDirectoryManifest.cs",
        })
    };

#endif

    /// <summary>
    /// Verify that the public build scripts aren't using internal APIs directly
    /// </summary>
    /// <param name="buildScriptPath">The filepath of the build script</param>
    /// <returns>IEnumerator for async test</returns>
    [UnityTest]
    public IEnumerator Verify_BuildScript_HasNoInternalApis([ValueSource(nameof(BuildScripts))] string buildScriptPath)
    {
        var content = File.ReadAllText(GetPackageFilePath(buildScriptPath));
        content = StripContentDirectoriesGuard(content);
        content = content.Replace("namespace UnityEditor.AddressableAssets.Build.DataBuilders", "namespace TestBuildScriptNamespace");
        // this is the using statement for the package the scripts are being copied from
        content = "using UnityEditor; // added by unit test\n" + content;
        content = "using UnityEditor.AddressableAssets.Build; // added by unit test\n" + content;
        content = "using UnityEditor.AddressableAssets.Build.DataBuilders; // added by unit test\n" + content;
        content = "using UnityEditor.AddressableAssets; // added by unit test\n" + content;

        var testFilePath = WriteSourceFile(buildScriptPath, content);

        yield return CompileAndAssertNoErrors(m_LoadScenePath, testFilePath);
    }

#if ENABLE_CONTENT_DIRECTORIES
    /// <summary>
    /// Verify that a group of related build scripts compile together without using internal APIs
    /// </summary>
    /// <param name="group">The group of build scripts to compile together</param>
    /// <returns>IEnumerator for async test</returns>
    [UnityTest]
    public IEnumerator Verify_BuildScriptGroup_HasNoInternalApis([ValueSource(nameof(BuildScriptGroups))] BuildScriptGroupData group)
    {
        var scriptPaths = new string[group.Files.Length];
        for (int i = 0; i < group.Files.Length; i++)
        {
            var buildScriptPath = group.Files[i];
            var content = File.ReadAllText(GetPackageFilePath(buildScriptPath));
            content = StripContentDirectoriesGuard(content);
            content = content.Replace("namespace UnityEditor.AddressableAssets.Build.DataBuilders.SchemaBuilders", "namespace TestBuildScriptNamespace");
            content = content.Replace("namespace UnityEditor.AddressableAssets.Build.DataBuilders", "namespace TestBuildScriptNamespace");
            content = "using UnityEditor; // added by unit test\n" + content;
            content = "using UnityEditor.AddressableAssets.Build.DataBuilders.SchemaBuilders; // added by unit test\n" + content;
            content = "using UnityEditor.AddressableAssets.Build.DataBuilders; // added by unit test\n" + content;
            content = "using UnityEditor.AddressableAssets.Build; // added by unit test\n" + content;
            content = "using UnityEditor.AddressableAssets; // added by unit test\n" + content;

            scriptPaths[i] = WriteSourceFile(buildScriptPath, content);
        }

        yield return CompileAndAssertNoErrors(scriptPaths);
    }
#endif

    private string GetPackageFilePath(string packageRelativePath)
    {
        return Path.Combine(m_PackagePath, packageRelativePath).Replace("Samples", m_SamplePath);
    }

    private static string WriteSourceFile(string packageRelativePath, string content)
    {
        var path = $"{k_SourceFolder}/{Path.GetFileName(packageRelativePath)}";
        File.WriteAllText(path, content);
        return path;
    }

    private static void DeleteTempRoot()
    {
        if (Directory.Exists(k_TempRoot))
            Directory.Delete(k_TempRoot, true);
    }

    // Compiles the scripts into a standalone editor assembly. Like the predefined Assembly-CSharp-Editor
    // assembly a user's copy would land in, it references the compiled package assemblies but has no
    // access to their internals. The output is written outside Assets, so there is no domain reload.
    private static IEnumerator CompileAndAssertNoErrors(params string[] scriptPaths)
    {
        CompilerMessage[] messages = null;

        // AssemblyBuilder is obsolete, but it is the only public API that compiles against the
        // project's assemblies without importing the scripts into the project.
#pragma warning disable CS0618
        var builder = new AssemblyBuilder(k_AssemblyPath, scriptPaths)
        {
            flags = AssemblyBuilderFlags.EditorAssembly,
            referencesOptions = ReferencesOptions.UseEngineModules,
        };
        builder.buildFinished += (_, compilerMessages) => messages = compilerMessages;

        var deadline = DateTime.UtcNow + k_CompileTimeout;

        // Build returns false while the editor or another AssemblyBuilder is compiling.
        while (!builder.Build())
        {
            if (DateTime.UtcNow > deadline)
                Assert.Fail($"AssemblyBuilder could not start within {k_CompileTimeout.TotalSeconds}s because another compilation was in progress.");
            yield return null;
        }

        // Reading status is what completes the build and raises buildFinished.
        while (builder.status != AssemblyBuilderStatus.Finished)
        {
            if (DateTime.UtcNow > deadline)
                Assert.Fail($"AssemblyBuilder did not finish within {k_CompileTimeout.TotalSeconds}s.");
            yield return null;
        }
#pragma warning restore CS0618

        Assert.IsNotNull(messages, "AssemblyBuilder finished without reporting compiler messages.");

        var errors = messages
            .Where(m => m.type == CompilerMessageType.Error)
            .Select(m => m.message)
            .ToArray();
        Assert.IsEmpty(errors, "Copied build scripts failed to compile:\n" + string.Join("\n", errors));
    }

    // Content Directory source files are wrapped in #if ENABLE_CONTENT_DIRECTORIES.
    // That symbol is not defined for the predefined Assembly-CSharp-Editor assembly
    // a user's copy would land in, so without stripping the guard the body would
    // be preprocessed out and the compile check would pass vacuously.
    private static string StripContentDirectoriesGuard(string content)
    {
        string trimmed = content.TrimStart();
        if (!trimmed.StartsWith("#if ENABLE_CONTENT_DIRECTORIES"))
            return content;

        int ifIndex = content.IndexOf("#if ENABLE_CONTENT_DIRECTORIES", StringComparison.Ordinal);
        int lineEnd = content.IndexOf('\n', ifIndex);
        content = content.Substring(lineEnd + 1);

        int endifIndex = content.LastIndexOf("#endif", StringComparison.Ordinal);
        if (endifIndex >= 0)
            content = content.Substring(0, endifIndex);

        return content;
    }
}
