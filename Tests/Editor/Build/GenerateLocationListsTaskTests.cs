using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets.Build.BuildPipelineTasks;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Build.Pipeline;
using UnityEngine;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.ResourceProviders;

public class GenerateLocationListsTaskTests
{
    AddressableAssetSettings m_Settings;

    const string TempPath = "Assets/TempGen";

    [SetUp]
    public void Setup()
    {
        if (Directory.Exists(TempPath))
            Directory.Delete(TempPath, true);
        Directory.CreateDirectory(TempPath);

        m_Settings = AddressableAssetSettings.Create(Path.Combine(TempPath, "Settings"), "AddressableAssetSettings.Tests", false, true);
    }

    [TearDown]
    public void Teardown()
    {
        // Many of the tests keep recreating assets in the same path, so we need to unload them completely so they don't get reused by the next test
        AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(m_Settings));
        Resources.UnloadAsset(m_Settings);
        if (Directory.Exists(TempPath))
            Directory.Delete(TempPath, true);
        AssetDatabase.Refresh();
    }

    static string CreateAsset(string assetPath, string objectName)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = objectName;
        //this is to ensure that bundles are different for every run.
        go.transform.localPosition = UnityEngine.Random.onUnitSphere;
        PrefabUtility.SaveAsPrefabAsset(go, assetPath);
        UnityEngine.Object.DestroyImmediate(go, false);
        return AssetDatabase.AssetPathToGUID(assetPath);
    }

    GenerateLocationListsTask.Input GenerateDefaultInput()
    {
        var input = new GenerateLocationListsTask.Input();
        input.AssetToFiles = new Dictionary<GUID, List<string>>();
        input.FileToBundle = new Dictionary<string, string>();
        input.Settings = m_Settings;
        input.BundleToAssetGroup = new Dictionary<string, string>();
        return input;
    }

    private bool FindLocationEntry(List<ContentCatalogDataEntry> locations, string key, out ContentCatalogDataEntry entry)
    {
        foreach (ContentCatalogDataEntry e in locations)
            if (e.Keys.Contains(key))
            {
                entry = e;
                return true;
            }
        entry = null;
        return false;
    }

    static K GetKeyForValue<K, V>(Dictionary<K, V> dict, V value) where V : class
    {
        foreach (var kvp in dict)
        {
            if (kvp.Value == value)
                return kvp.Key;
        }
        throw new System.Exception("Couldn't find value");
    }

    string CreateAddressablePrefab(GenerateLocationListsTask.Input input, string name, AddressableAssetGroup group, params string[] depFiles)
    {
        string guid = CreateAsset($"{TempPath}/{name}.prefab", name);
        var entry = m_Settings.CreateOrMoveEntry(guid, group, false, false);
        entry.address = Path.GetFileNameWithoutExtension(entry.AssetPath);
        input.AssetToFiles[new GUID(guid)] = new List<string>(depFiles);
        return guid;
    }

    AddressableAssetGroup CreateGroupMappedToBundle(GenerateLocationListsTask.Input input, string postfix)
    {
        AddressableAssetGroup group = m_Settings.CreateGroup($"testGroup{postfix}", false, false, false, null, typeof(BundledAssetGroupSchema));
        input.BundleToAssetGroup[$"bundle{postfix}"] = group.Guid;
        input.FileToBundle[$"file{postfix}"] = $"bundle{postfix}";
        return group;
    }

    void AssertLocationDependencies(GenerateLocationListsTask.Output output, string location, params string[] deps)
    {
        FindLocationEntry(output.Locations, location, out ContentCatalogDataEntry e1);
        CollectionAssert.AreEquivalent(e1.Dependencies, deps);
    }

    static List<AddressableAssetEntry> BuildAddressableAssetEntryList(AddressableAssetSettings settings)
    {
        List<AddressableAssetEntry> entries = new List<AddressableAssetEntry>();
        foreach (AddressableAssetGroup group in settings.groups)
        {
            group.GatherAllAssets(entries, true, true, false);
        }
        return entries;
    }

    [Test]
    public void WhenAssetLoadsFromBundle_ProviderTypesIncludesBundledAssetProvider()
    {
        GenerateLocationListsTask.Input input = GenerateDefaultInput();
        AddressableAssetGroup groupX = CreateGroupMappedToBundle(input, "X");
        CreateAddressablePrefab(input, "p1", groupX, "fileX");
        input.AddressableAssetEntries = BuildAddressableAssetEntryList(input.Settings);
        GenerateLocationListsTask.Output output = GenerateLocationListsTask.RunInternal(input);
        CollectionAssert.Contains(output.ProviderTypes, typeof(BundledAssetProvider));
    }

    [Test]
    public void WhenGroupCreatesMultipleBundles_AllBundlesInAssetGroupToBundlesMap()
    {
        GenerateLocationListsTask.Input input = GenerateDefaultInput();
        AddressableAssetGroup group = m_Settings.CreateGroup($"groupX", false, false, false, null, typeof(BundledAssetGroupSchema));
        input.BundleToAssetGroup["bundleX"] = group.Guid;
        input.BundleToAssetGroup["bundleY"] = group.Guid;
        input.FileToBundle["fileX"] = "bundle1";
        input.FileToBundle["fileY"] = "bundle2";
        CreateAddressablePrefab(input, "p1", group, "fileX");
        CreateAddressablePrefab(input, "p2", group, "fileY");
        input.AddressableAssetEntries = BuildAddressableAssetEntryList(input.Settings);
        GenerateLocationListsTask.Output output = GenerateLocationListsTask.RunInternal(input);
        CollectionAssert.AreEquivalent(new string[] { "bundle1", "bundle2" }, output.AssetGroupToBundles[group]);
    }

    [Test]
    public void WhenAssetHasDependencyOnBundle_AssetLocationIncludesRecursiveBundleDependencies()
    {
        GenerateLocationListsTask.Input input = GenerateDefaultInput();

        AddressableAssetGroup groupX = CreateGroupMappedToBundle(input, "X");
        AddressableAssetGroup groupY = CreateGroupMappedToBundle(input, "Y");
        AddressableAssetGroup groupZ = CreateGroupMappedToBundle(input, "Z");
        AddressableAssetGroup groupW = CreateGroupMappedToBundle(input, "W");

        CreateAddressablePrefab(input, "p1", groupX, "fileX", "fileY");
        CreateAddressablePrefab(input, "p2", groupY, "fileY");
        CreateAddressablePrefab(input, "p3", groupY, "fileY", "fileZ");
        CreateAddressablePrefab(input, "p4", groupZ, "fileZ");
        CreateAddressablePrefab(input, "p5", groupZ, "fileZ", "fileW");
        CreateAddressablePrefab(input, "p6", groupW, "fileW");

        input.AddressableAssetEntries = BuildAddressableAssetEntryList(input.Settings);
        GenerateLocationListsTask.Output output = GenerateLocationListsTask.RunInternal(input);

        AssertLocationDependencies(output, "p1", "bundleX", "bundleY", "bundleZ", "bundleW");
        AssertLocationDependencies(output, "p2", "bundleY", "bundleZ", "bundleW");
        AssertLocationDependencies(output, "p3", "bundleY", "bundleZ", "bundleW");
        AssertLocationDependencies(output, "p4", "bundleZ", "bundleW");
        AssertLocationDependencies(output, "p5", "bundleZ", "bundleW");
        AssertLocationDependencies(output, "p6", "bundleW");
    }

    //[Test]
    //public void WhenEntryAddressContainsBrackets_ExceptionIsThrown()
    //{
    // TODO:
    //}
    //}
}
