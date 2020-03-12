using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using NUnit.Framework;
using UnityEngine.AddressableAssets;
using UnityEditor.U2D;
using UnityEngine.U2D;

namespace UnityEditor.AddressableAssets.Tests
{
	public class AddressableAssetReferenceTests : AddressableAssetTestBase
    {
	    private string m_ScriptableObjectPath;
	    private string m_SpriteAtlasPath;
	    private string m_TexturePath;
	    
	    protected override void OnInit()
	    {
		    var mainSO = ScriptableObject.CreateInstance<TestObject>();
		    var subSO = ScriptableObject.CreateInstance<TestSubObject>();
		    subSO.name = "sub";

		    m_ScriptableObjectPath = Path.Combine(k_TestConfigFolder, "testScriptableObject.asset");
		    AssetDatabase.CreateAsset(mainSO, m_ScriptableObjectPath);
		    AssetDatabase.AddObjectToAsset(subSO, m_ScriptableObjectPath);
		    AssetDatabase.ImportAsset(m_ScriptableObjectPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
		    
		    // create a Sprite atlas, + sprite
		    m_SpriteAtlasPath = Path.Combine(k_TestConfigFolder, "testAtlas.spriteatlas");
		    SpriteAtlas spriteAtlas = new SpriteAtlas();
		    AssetDatabase.CreateAsset(spriteAtlas, m_SpriteAtlasPath);
		    
		    Texture2D texture = Texture2D.whiteTexture;
		    byte[] data = texture.EncodeToPNG();
		    m_TexturePath = Path.Combine( k_TestConfigFolder, "testTexture.png" );
		    File.WriteAllBytes(m_TexturePath, data);
		    AssetDatabase.ImportAsset(m_TexturePath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

		    TextureImporter importer = TextureImporter.GetAtPath( m_TexturePath ) as TextureImporter;
		    importer.textureType = TextureImporterType.Sprite;
		    importer.spriteImportMode = SpriteImportMode.Single;
		    importer.SaveAndReimport();
		    
		    SpriteAtlasExtensions.Add(spriteAtlas, new []{AssetDatabase.LoadAssetAtPath<Texture>(m_TexturePath)});
		    SpriteAtlasUtility.PackAtlases(new SpriteAtlas[]{spriteAtlas}, EditorUserBuildSettings.activeBuildTarget, false);
	    }

#if UNITY_2019_1_OR_NEWER
        [Test]
        public void VerifySetEditorAsset_DoesNotMakeRefAssetDirty()
        {
            //Setup
            AssetReference reference = new AssetReference();
            string path =AssetDatabase.GUIDToAssetPath (m_AssetGUID.ToString());
            GameObject o = AssetDatabase.LoadAssetAtPath(path, typeof(GameObject)) as GameObject;
            
            //Test
            Assert.IsFalse(EditorUtility.IsDirty(reference.editorAsset)); // IsDirty(Object o) only available in 2019.1 or newer
            reference.SetEditorAsset(o);
            Assert.IsFalse(EditorUtility.IsDirty(reference.editorAsset));
        }
#endif
        
	    [Test]
	    public void AssetReferenceEditorAssetForSubObject_DifferentType()
	    {
		    var guid = AssetDatabase.AssetPathToGUID(m_ScriptableObjectPath);
		    AssetReferenceT<TestSubObject> typeReference = new AssetReferenceT<TestSubObject>(guid);
		    typeReference.SubObjectName = "sub";

		    //Test
		    Assert.IsNull(typeReference.editorAsset);
		    AssetReference asBase = typeReference;
		    Assert.IsNotNull(asBase.editorAsset);
		    Assert.AreEqual(asBase.editorAsset, AssetDatabase.LoadAssetAtPath<TestObject>(m_ScriptableObjectPath));
	    }
	    
	    [Test]
	    public void AssetReferenceEditorAssetForSubObject_Sprite()
	    {
		    var guid = AssetDatabase.AssetPathToGUID(m_TexturePath);
		    AssetReferenceSprite atlasReference = new AssetReferenceSprite(guid);
		    atlasReference.SubObjectName = "testTexture";

		    //Test
		    Assert.IsNotNull(atlasReference.editorAsset);
	    }
	    
	    [Test]
	    public void AssetReferenceEditorAssetForSubObject_AtlasedSprite()
	    {
		    var guid = AssetDatabase.AssetPathToGUID(m_SpriteAtlasPath);
		    AssetReferenceAtlasedSprite atlasReference = new AssetReferenceAtlasedSprite(guid);
		    atlasReference.SubObjectName = "testTexture";

		    //Test
		    Assert.IsNotNull(atlasReference.editorAsset);
	    }
    }
}
 