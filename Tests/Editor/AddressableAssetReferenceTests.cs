using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NUnit.Framework;
using UnityEngine.AddressableAssets;

namespace UnityEditor.AddressableAssets.Tests
{
    public class AddressableAssetReferenceTests : AddressableAssetTestBase
    {
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
    }
}
 