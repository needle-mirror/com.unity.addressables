using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor.Build.Utilities;
using Object = UnityEngine.Object;

namespace UnityEditor.AddressableAssets
{
    static class AddressableAssetUtility
    {
        internal static bool IsInResources(string path)
        {
            return path.Replace('\\', '/').ToLower().Contains("/resources/");
        }
        internal static bool GetPathAndGUIDFromTarget(Object t, out string path, ref string guid)
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
            var ext = Path.GetExtension(path);
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

            float fullCount = bundleList.Length;
            int currCount = 0;

            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            foreach (var bundle in bundleList)
            {
                if (EditorUtility.DisplayCancelableProgressBar("Converting Legacy Asset Bundles", bundle, currCount / fullCount))
                    break;

                currCount++;
                var group = settings.CreateGroup(bundle, false, false, false, null);
                var schema = group.AddSchema<BundledAssetGroupSchema>();
                schema.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
                schema.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
                schema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
                group.AddSchema<ContentUpdateGroupSchema>().StaticContent = true;

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
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true);
            EditorUtility.ClearProgressBar();
            AssetDatabase.RemoveUnusedAssetBundleNames();
        }

        /// <summary>
        /// Get all types that can be assigned to type T
        /// </summary>
        /// <typeparam name="T">The class type to use as the base class or interface for all found types.</typeparam>
        /// <returns>A list of types that are assignable to type T.  The results are cached.</returns>
        public static List<Type> GetTypes<T>()
        {
            return TypeManager<T>.Types;
        }

        /// <summary>
        /// Get all types that can be assigned to type rootType.
        /// </summary>
        /// <param name="rootType">The class type to use as the base class or interface for all found types.</param>
        /// <returns>A list of types that are assignable to type T.  The results are not cached.</returns>
        public static List<Type> GetTypes(Type rootType)
        {
            return TypeManager.GetManagerTypes(rootType);
        }

        class TypeManager
        {
            public static List<Type> GetManagerTypes(Type rootType)
            {
                var types = new List<Type>();
                try
                {
                    foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                    {
#if NET_4_6
                                if (a.IsDynamic)
                                    continue;
                                foreach (var t in a.ExportedTypes)
#else
                        foreach (var t in a.GetExportedTypes())
#endif
                        {
                            if (t != rootType && rootType.IsAssignableFrom(t) && !t.IsAbstract)
                                types.Add(t);
                        }
                    }
                }
                catch (Exception)
                {
                    // ignored
                }

                return types;
            }
        }

        class TypeManager<T> : TypeManager
        {
            // ReSharper disable once StaticMemberInGenericType
            static List<Type> s_Types;
            public static List<Type> Types
            {
                get
                {
                    if (s_Types == null)
                        s_Types = GetManagerTypes(typeof(T));

                    return s_Types;
                }
            }
        }
    }
}
