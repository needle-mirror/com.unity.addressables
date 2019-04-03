using System.ComponentModel;
using UnityEngine;
using UnityEngine.ResourceManagement;
using UnityEngine.AddressableAssets;
using System.Collections.Generic;
using System.IO;

namespace UnityEditor.AddressableAssets
{
    /// <summary>
    /// TODO - doc
    /// </summary>
    [Description("Local Packed Content")]
    public class LocalAssetBundleAssetGroupProcessor : AssetBundleAssetGroupProcessor
    {
        [SerializeField]
        Vector2 position = new Vector2();
        internal override void OnDrawGUI(AddressableAssetSettings settings, Rect rect)
        {
            GUILayout.BeginArea(rect);
            position = EditorGUILayout.BeginScrollView(position, false, false, GUILayout.MaxWidth(rect.width));
            bool oldWrap = EditorStyles.label.wordWrap;
            EditorStyles.label.wordWrap = true;
            EditorGUILayout.LabelField("Assets in this group will be packed together in the StreamingAssets folder and will be delivered with the game");
            EditorStyles.label.wordWrap = oldWrap;
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        internal override string displayName { get { return "Local Packed Content"; } }


        protected override string GetBuildPath(AddressableAssetSettings settings)
        {
            return "Assets/StreamingAssets";
        }

        protected override string GetBundleLoadPath(AddressableAssetSettings settings, string bundleName)
        {
            return "{UnityEngine.Application.streamingAssetsPath}/" + bundleName;
        }

        protected override BundleMode GetBundleMode(AddressableAssetSettings settings)
        {
            return BundleMode.PackTogether;
        }

        internal override int GetPriority(AddressableAssetSettings aaSettings, AddressableAssetGroup group)
        {
            return 100;
        }

    }
}
