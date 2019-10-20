using System.Collections;
using System.IO;
using NUnit.Framework;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
#endif
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.TestTools;

namespace LegacyResourcesTests
{
    abstract class LegacyResourceTests : AddressablesTestFixture
    {
        protected const string spriteName = "testSprite";
#if UNITY_EDITOR
        internal override void Setup(AddressableAssetSettings settings, string tempAssetFolder)
        {
            var group = settings.CreateGroup("TestStuff", true, false, false, null, typeof(BundledAssetGroupSchema));
            string resourceDirectory = Path.Combine(tempAssetFolder, "Resources");
            Directory.CreateDirectory(resourceDirectory);
            var spritePath = Path.Combine(resourceDirectory, string.Concat(GetBuildScriptTypeFromMode(BuildScriptMode), spriteName, ".png"));
            CreateSpriteOnPath(spritePath);
            var spriteEntry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(spritePath), group, false, false);
            spriteEntry.address = spriteName;
        }

        void CreateSpriteOnPath(string spritePath)
        {
            CreateTextureOnPath(spritePath);
            var importer = AssetImporter.GetAtPath(spritePath) as TextureImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.SaveAndReimport();
        }

        void CreateTextureOnPath(string spritePath)
        {
            var data = ImageConversion.EncodeToPNG(new Texture2D(32, 32));
            File.WriteAllBytes(spritePath, data);
            AssetDatabase.ImportAsset(spritePath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        }
#endif

        [UnityTest]
        public IEnumerator WhenLoadingFromResources_AndResourceExists_ResourceIsLoaded()
        {
            var op = m_Addressables.LoadAssetAsync<Sprite>(spriteName);
            yield return op;
            Assert.AreEqual(AsyncOperationStatus.Succeeded, op.Status);
            Assert.IsNotNull(op.Result.GetType());
            m_Addressables.Release(op);
        }

        [UnityTest]
        public IEnumerator WhenLoadingSpecificTypes_ObjectOfSpecifiedTypeIsReturned()
        {
            AsyncOperationHandle spriteOp = m_Addressables.LoadAssetAsync<Sprite>(spriteName);
            AsyncOperationHandle texOp = m_Addressables.LoadAssetAsync<Texture>(spriteName);
            yield return spriteOp;
            yield return texOp;
            Assert.AreEqual(typeof(Sprite), spriteOp.Result.GetType());
            Assert.AreEqual(typeof(Texture2D), texOp.Result.GetType());
            m_Addressables.Release(spriteOp);
            m_Addressables.Release(texOp);
        }
    }

#if UNITY_EDITOR
    class LegacyResourceTests_FastMode : LegacyResourceTests { protected override TestBuildScriptMode BuildScriptMode { get { return TestBuildScriptMode.Fast; } } }

    class LegacyResourceTests_VirtualMode : LegacyResourceTests { protected override TestBuildScriptMode BuildScriptMode { get { return TestBuildScriptMode.Virtual; } } }

    class LegacyResourceTests_PackedPlaymodeMode : LegacyResourceTests { protected override TestBuildScriptMode BuildScriptMode { get { return TestBuildScriptMode.PackedPlaymode; } } }
#endif

    [UnityPlatform(exclude = new[] { RuntimePlatform.WindowsEditor, RuntimePlatform.OSXEditor, RuntimePlatform.LinuxEditor })]
    class LegacyResourceTests_PackedMode : LegacyResourceTests { protected override TestBuildScriptMode BuildScriptMode { get { return TestBuildScriptMode.Packed; } } }

}
