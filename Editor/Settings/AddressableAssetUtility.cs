using UnityEditor.Build.Utilities;
using Object = UnityEngine.Object;

namespace UnityEditor.AddressableAssets
{
    internal static class AddressablesUtility
    {
        internal static bool GetPathAndGUIDFromTarget(Object t, ref string path, ref string guid)
        {
            path = AssetDatabase.GetAssetOrScenePath(t);
            if (!IsPathValidForEntry(path))
                return false;
            guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid))
                return false;
            var mat = AssetDatabase.GetMainAssetTypeAtPath(path);
            if (mat != t.GetType() && !typeof(AssetImporter).IsAssignableFrom(t.GetType()))
                return false;
            return true;
        }

        internal static bool IsPathValidForEntry(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;
            path = path.ToLower();
            if (path == CommonStrings.UnityEditorResourcePath ||
                path == CommonStrings.UnityDefaultResourcePath ||
                path == CommonStrings.UnityBuiltInExtraPath)
                return false;
            var ext = System.IO.Path.GetExtension(path);
            if (ext == ".cs" || ext == ".js" || ext == ".boo" || ext == ".exe" || ext == ".dll")
                return false;
            var t = AssetDatabase.GetMainAssetTypeAtPath(path);
            if (t == typeof(AddressableAssetSettings))
                return false;
            return true;
        }

        internal static void ConvertAssetBundlesToAddressables()
        {
            AssetDatabase.RemoveUnusedAssetBundleNames();
            var bundleList = AssetDatabase.GetAllAssetBundleNames();

            var message = "You are about to convert " + bundleList.Length + " asset bundles into " + bundleList.Length + " Addressable groups. This action cannot be undone.";
            if (!EditorUtility.DisplayDialog("Convert Legacy Asset Bundles", message, "Continue", "Abort"))
                return;

            float fullCount = bundleList.Length;
            int currCount = 0;

            var settings = AddressableAssetSettings.GetDefault(true, true);
            foreach (var bundle in bundleList)
            {
                if (EditorUtility.DisplayCancelableProgressBar("Converting Legacy Asset Bundles", bundle, currCount / fullCount))
                    break;

                currCount++;
                var group = settings.CreateGroup(bundle, typeof(BundledAssetGroupProcessor), false, false);
                var assetList = AssetDatabase.GetAssetPathsFromAssetBundle(bundle);
                foreach (var asset in assetList)
                {
                    var guid = AssetDatabase.AssetPathToGUID(asset);
                    settings.CreateOrMoveEntry(guid, group, false, false);
                    var imp = AssetImporter.GetAtPath(asset);
                    if (imp != null)
                        imp.SetAssetBundleNameAndVariant(string.Empty, string.Empty);
                }
            }
            settings.PostModificationEvent(AddressableAssetSettings.ModificationEvent.BatchModification, null);
            EditorUtility.ClearProgressBar();
            AssetDatabase.RemoveUnusedAssetBundleNames();
        }
    }
}
