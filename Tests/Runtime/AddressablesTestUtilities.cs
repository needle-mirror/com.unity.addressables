using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.ResourceManagement;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.Util;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.ResourceManagement.AsyncOperations;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
#endif

public static class AddressablesTestUtility
{

    static public void Reset()
    {
        DelayedActionManager.Wait(0, .1f);
        Addressables.ResourceLocators.Clear();
        Addressables.ResourceManager.ResourceProviders.Clear();
        Addressables.ResourceManager.InstanceProvider = null;
        Addressables.ResourceManager.SceneProvider = new SceneProvider();
        AsyncOperationCache.Instance.Clear();
        DelayedActionManager.Clear();
    }

    static public void TearDown(string testType, string pathFormat)
    {
#if UNITY_EDITOR
        Reset();
        var RootFolder = string.Format(pathFormat, testType);
        AssetDatabase.DeleteAsset(RootFolder);
#endif 
    }

    static public void Setup(string testType, string pathFormat, string suffix = "")
    {
#if UNITY_EDITOR
        var RootFolder = string.Format(pathFormat, testType);
        if (!Directory.Exists(RootFolder))
        {
            Directory.CreateDirectory(RootFolder);
            AssetDatabase.Refresh();
        }

        var settings = AddressableAssetSettings.Create(RootFolder + "/Settings", "AddressableAssetSettings.Tests", true, true);
        var playerData = settings.FindGroup(g => g.HasSchema<PlayerDataGroupSchema>());
        if (playerData != null)
        {
            var s = playerData.GetSchema<PlayerDataGroupSchema>();
            s.IncludeBuildSettingsScenes = false;
            s.IncludeResourcesFolders = false;
        }
        settings.DefaultGroup.GetSchema<BundledAssetGroupSchema>().IncludeInBuild = false;
        var group = settings.CreateGroup("TestStuff" + suffix, true, false, false, null, typeof(BundledAssetGroupSchema));
        settings.DefaultGroup = group;
        var schema = group.GetSchema<BundledAssetGroupSchema>();
        schema.AssetCachedProviderMaxLRUAge = 0;
        schema.AssetCachedProviderMaxLRUCount = 0;
        schema.BundleCachedProviderMaxLRUAge = 0;
        schema.BundleCachedProviderMaxLRUCount = 0;
        AssetDatabase.StartAssetEditing();
        for (int i = 0; i < 10; i++)
        {
            var guid = CreateAsset(RootFolder + "/test" + i + suffix + ".prefab", "testPrefab" + i);
            var entry = settings.CreateOrMoveEntry(guid, group, false, false);
            entry.address = Path.GetFileNameWithoutExtension(entry.AssetPath);
            entry.SetLabel("prefabs" + suffix, true, false);
        }
        AssetDatabase.StopAssetEditing();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        RunBuilder(settings, testType, suffix);
#endif
    }

#if UNITY_EDITOR
    static string CreateAsset(string assetPath, string objectName)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
#if UNITY_2018_3_OR_NEWER
        PrefabUtility.SaveAsPrefabAsset(go, assetPath);
#else
        PrefabUtility.CreatePrefab(assetPath, go);
#endif
        go.name = objectName;
        UnityEngine.Object.DestroyImmediate(go, false);
        return AssetDatabase.AssetPathToGUID(assetPath);
    }


    static void RunBuilder(AddressableAssetSettings settings, string testType, string suffix)
    {
        var buildContext = new AddressablesBuildDataBuilderContext(settings,
            BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget),
            EditorUserBuildSettings.activeBuildTarget, EditorUserBuildSettings.development,
            false, settings.PlayerBuildVersion);
        buildContext.SetValue(AddressablesBuildDataBuilderContext.BuildScriptContextConstants.kRuntimeSettingsFilename, "settings" + suffix + ".json");
        buildContext.SetValue(AddressablesBuildDataBuilderContext.BuildScriptContextConstants.kRuntimeCatalogFilename, "catalog" + suffix + ".json");

        foreach (var db in settings.DataBuilders)
        {
            var b = db as IDataBuilder;
            if (b.GetType().Name != testType)
                continue;
            buildContext.SetValue("PathFormat", "{0}Library/com.unity.addressables/{1}_" + testType + "_TEST_" + suffix + ".json");
            b.BuildData<AddressableAssetBuildResult>(buildContext);
        }
    }
#endif
    }
