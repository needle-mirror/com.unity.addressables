using UnityEditor;
using UnityEngine;

namespace UnityEditor.AddressableAssets.Tests.Editor.TestObjects
{
    public class TestEditorObject : ScriptableObject
    {
        public static TestEditorObject Create(string name, string assetPath = null)
        {
            var obj = CreateInstance<TestEditorObject>();
            obj.name = name;
            if (!string.IsNullOrEmpty(assetPath))
            {
                AssetDatabase.CreateAsset(obj, assetPath);
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            }

            return obj;
        }

        internal void AddTestSubObject()
        {
            TestEditorSubObject n = ScriptableObject.CreateInstance<TestEditorSubObject>();
            n.name = "testSubObject";
            AssetDatabase.AddObjectToAsset(n, this);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(this), ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        }
    }
}
