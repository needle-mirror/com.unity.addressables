#if ENABLE_CONTENT_DIRECTORIES
using System.Collections;
using System.Linq;
using NUnit.Framework;
using Unity.Loading;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace UnityEditor.AddressableAssets.Tests
{
    public class ContentDirectoryPlaymodeExitTests
    {
        const string k_TempPath = "ContentDirectoryPlaymodeExitTests";
        const string k_SceneKey = "cd_playmode_exit_scene";
        const string k_MaterialKey = "cd_playmode_exit_material";

        // SessionState default when Addressables.kAddressablesRuntimeDataPath was never set.
        const string k_NoSavedRuntimeDataPath = "__ContentDirectoryPlaymodeExitTests_NoSavedRuntimeDataPath__";

        // Serialized so the values survive the domain reload when entering play mode with domain reload enabled.
        [SerializeField]
        bool m_SavedEnterPlayModeOptionsEnabled;
        [SerializeField]
        EnterPlayModeOptions m_SavedEnterPlayModeOptions;
        [SerializeField]
        string m_SavedRuntimeDataPath;

        static string GetPath(string a) => $"Assets/{k_TempPath}/{a}";

        // The material is its own addressable entry, so it is built into a content file separate from
        // the scene and the loaded scene holds a reverse-dependency count on it.
        static void CreateSceneWithDependency(AddressableAssetSettings settings, AddressableAssetGroup group)
        {
            var materialPath = GetPath("cd_playmode_exit_material.mat");
            AssetDatabase.CreateAsset(new Material(Shader.Find("Unlit/Color")), materialPath);
            AssetDatabase.SaveAssets();
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(materialPath), group, false, false).address = k_MaterialKey;

            var scenePath = GetPath($"{k_SceneKey}.unity");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            EditorSceneManager.SaveScene(scene, scenePath);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(scenePath), group, false, false).address = k_SceneKey;
        }

        static string BuildContentDirectory()
        {
            if (AssetDatabase.IsValidFolder($"Assets/{k_TempPath}"))
            {
                AssetDatabase.DeleteAsset($"Assets/{k_TempPath}");
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }

            AssetDatabase.CreateFolder("Assets", k_TempPath);
            bool defaultConfigFolderExisted = AssetDatabase.IsValidFolder(AddressableAssetSettingsDefaultObject.kDefaultConfigFolder);
            var settings = AddressableAssetSettings.Create(GetPath("Settings"), k_TempPath, true, true);

            var group = settings.CreateGroup("ContentDirectoryScenes", false, false, false, null, typeof(ContentDirectoryGroupSchema));
            var schema = group.GetSchema<ContentDirectoryGroupSchema>();
            schema.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
            schema.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
            schema.CatalogId = $"catalog_{k_TempPath}";

            CreateSceneWithDependency(settings, group);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            var inputs = new AddressablesDataBuilderInput(settings);
            // Unique filename prevents output collision with other fixtures using the default "settings.json".
            inputs.RuntimeSettingsFilename = $"settings{k_TempPath}.json";
            AddressableAssetSettings.BuildPlayerContent(out var buildResult, inputs);
            Assert.IsTrue(string.IsNullOrEmpty(buildResult.Error), $"Addressables build failed: {buildResult.Error}");

            AssetDatabase.DeleteAsset($"Assets/{k_TempPath}");
            if (!defaultConfigFolderExisted)
                AssetDatabase.DeleteAsset(AddressableAssetSettingsDefaultObject.kDefaultConfigFolder);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            return buildResult.OutputPath;
        }

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            // The test runner calls this again after the domain reload on entering play mode. The content is already
            // built and the serialized fields already hold the saved settings.
            if (EditorApplication.isPlaying)
                return;

            m_SavedEnterPlayModeOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            m_SavedEnterPlayModeOptions = EditorSettings.enterPlayModeOptions;

            var runtimeDataPath = BuildContentDirectory();
            m_SavedRuntimeDataPath = SessionState.GetString(Addressables.kAddressablesRuntimeDataPath, k_NoSavedRuntimeDataPath);
            SessionState.SetString(Addressables.kAddressablesRuntimeDataPath, runtimeDataPath);

            EditorSettings.enterPlayModeOptionsEnabled = true;
        }

        static void SetDomainReload(bool reloadDomain)
        {
            EditorSettings.enterPlayModeOptions = reloadDomain
                ? EnterPlayModeOptions.None
                : EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload;
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            EditorSettings.enterPlayModeOptionsEnabled = m_SavedEnterPlayModeOptionsEnabled;
            EditorSettings.enterPlayModeOptions = m_SavedEnterPlayModeOptions;
            if (m_SavedRuntimeDataPath == k_NoSavedRuntimeDataPath)
                SessionState.EraseString(Addressables.kAddressablesRuntimeDataPath);
            else
                SessionState.SetString(Addressables.kAddressablesRuntimeDataPath, m_SavedRuntimeDataPath);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (EditorApplication.isPlaying)
                yield return new ExitPlayMode();
        }

        // The handler below is a method rather than a closure over the test's locals: after a domain reload the test
        // runner resumes the test on a new enumerator past the EnterPlayMode yield, so the storage for captured locals,
        // created at the start of the method, would be missing.
        MeshRenderer m_Renderer;
        bool? m_MaterialLoadedAfterCleanup;

        void RecordMaterialStateOnExitingPlayMode(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingPlayMode)
                m_MaterialLoadedAfterCleanup = m_Renderer != null && m_Renderer.sharedMaterial != null;
        }

        // When play mode exits, the editor unloads the scene only after Addressables' ExitingPlayMode cleanup has run,
        // and the native content system then touches the content files the scene depends on. If the cleanup unmounts
        // the Content Directory, those files are freed first and the editor crashes (UUM-154740). That crash depends on
        // the freed memory being reused, so instead this checks the scene's material right after the cleanup: unloading
        // its content file destroys the material, so a null material means the dependency was freed too early.
        [UnityTest]
        public IEnumerator ExitingPlayMode_WithContentDirectorySceneLoaded_KeepsSceneDependenciesLoadedUntilSceneUnloads([Values] bool reloadDomain)
        {
            SetDomainReload(reloadDomain);
            yield return new EnterPlayMode();

            var mountedBeforeLoad = ContentLoadManager.GetContentDirectories();
            var handle = Addressables.LoadSceneAsync(k_SceneKey, LoadSceneMode.Additive);
            yield return handle;
            Assert.AreEqual(AsyncOperationStatus.Succeeded, handle.Status, "Expected the content directory scene to load.");
            var mountedByTest = ContentLoadManager.GetContentDirectories().Except(mountedBeforeLoad).ToArray();
            Assert.IsNotEmpty(mountedByTest, "Expected loading the scene to mount its Content Directory.");

            m_Renderer = Object.FindAnyObjectByType<MeshRenderer>();
            Assert.IsNotNull(m_Renderer.sharedMaterial, "Expected the scene's material dependency to be loaded.");

            // Subscribed after entering play mode so it runs after Addressables' own ExitingPlayMode cleanup,
            // while the editor still has the play mode scene loaded.
            m_MaterialLoadedAfterCleanup = null;
            EditorApplication.playModeStateChanged += RecordMaterialStateOnExitingPlayMode;
            try
            {
                yield return new ExitPlayMode();
            }
            finally
            {
                EditorApplication.playModeStateChanged -= RecordMaterialStateOnExitingPlayMode;
            }

            Assert.IsTrue(m_MaterialLoadedAfterCleanup.HasValue, "ExitingPlayMode was not observed.");
            Assert.IsTrue(m_MaterialLoadedAfterCleanup.Value,
                "The scene's material was unloaded while the scene was still loaded. When the editor later unloads the scene, " +
                "the native content system dereferences that freed dependency (UUM-154740).");
            Assert.IsEmpty(ContentLoadManager.GetContentDirectories().Intersect(mountedByTest),
                "Expected the Content Directories mounted in play mode to be unmounted once edit mode was entered.");
        }

        // A listener that runs on EnteredEditMode before Addressables' deferred unmount must not be handed a mount that
        // is about to be unmounted. Registering a Content Directory is not allowed in edit mode, so EnsureMounted
        // should throw instead of returning the directory left mounted by the disposed play mode instance.
        [UnityTest]
        public IEnumerator EnsureMounted_DuringEnteredEditMode_DoesNotReturnMountPendingUnmount()
        {
            SetDomainReload(false);
            yield return new EnterPlayMode();

            var locations = Addressables.LoadResourceLocationsAsync(k_MaterialKey);
            yield return locations;
            var loadPath = ((ContentDirectoryAssetData)locations.Result[0].Data).LoadPath;
            locations.Release();

            var material = Addressables.LoadAssetAsync<Material>(k_MaterialKey);
            yield return material;
            Assert.AreEqual(AsyncOperationStatus.Succeeded, material.Status, "Expected the content directory material to load.");

            // Subscribed before exiting so it runs ahead of the deferred unmount Addressables subscribes during ExitingPlayMode.
            ContentDirectoryHandle? handleFromEditMode = null;
            void OnPlayModeStateChanged(PlayModeStateChange change)
            {
                if (change != PlayModeStateChange.EnteredEditMode)
                    return;
                try
                {
                    handleFromEditMode = ContentDirectoryMountManager.EnsureMounted(loadPath);
                }
                catch (System.InvalidOperationException)
                {
                }
            }

            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            try
            {
                yield return new ExitPlayMode();
            }
            finally
            {
                EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            }

            bool stillMounted = handleFromEditMode.HasValue && ContentLoadManager.GetContentDirectories().Contains(handleFromEditMode.Value);
            ContentDirectoryMountManager.UnmountAll();
            Assert.IsTrue(!handleFromEditMode.HasValue || stillMounted,
                "EnsureMounted returned a Content Directory during EnteredEditMode that was unmounted right afterwards.");
        }
    }
}
#endif
