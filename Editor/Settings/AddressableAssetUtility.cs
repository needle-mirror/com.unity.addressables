using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Build.Utilities;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEditor.VersionControl;
using UnityEngine;

namespace UnityEditor.AddressableAssets.Settings
{
    using Object = UnityEngine.Object;

    static class AddressableAssetUtility
    {
        internal static bool IsInResources(string path)
        {
            return path.Replace('\\', '/').ToLower().Contains("/resources/");
        }

        internal static bool GetPathAndGUIDFromTarget(Object target, out string path, out string guid, out Type mainAssetType)
        {
            mainAssetType = null;
            guid = string.Empty;
            path = string.Empty;
            if (target == null)
                return false;
            path = AssetDatabase.GetAssetOrScenePath(target);
            if (!IsPathValidForEntry(path))
                return false;
            guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid))
                return false;
            mainAssetType = AssetDatabase.GetMainAssetTypeAtPath(path);
            if (mainAssetType == null)
                return false;
            if (mainAssetType != target.GetType() && !typeof(AssetImporter).IsAssignableFrom(target.GetType()))
                return false;
            return true;
        }
        static HashSet<string> excludedExtensions = new HashSet<string>(new string[] { ".cs", ".js", ".boo", ".exe", ".dll", ".meta" });
        internal static bool IsPathValidForEntry(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;
            if (!path.StartsWith("assets", StringComparison.OrdinalIgnoreCase) && !IsPathValidPackageAsset(path))
                return false;
            if (path == CommonStrings.UnityEditorResourcePath ||
                path == CommonStrings.UnityDefaultResourcePath ||
                path == CommonStrings.UnityBuiltInExtraPath)
                return false;
            return !excludedExtensions.Contains(Path.GetExtension(path));
        }

        internal static bool IsPathValidPackageAsset(string path)
        {
            string convertPath = path.ToLower().Replace("\\", "/");
            string[] splitPath = convertPath.Split('/');

            if (splitPath.Length < 3)
                return false;
            if (splitPath[0] != "packages")
                return false;
            if (splitPath.Length == 3)
            {
                string ext = Path.GetExtension(splitPath[2]);
                if (ext == ".json" || ext == ".asmdef")
                    return false;
            }
            return true;
        }

        static HashSet<Type> validTypes = new HashSet<Type>();
        internal static Type MapEditorTypeToRuntimeType(Type t, bool allowFolders)
        {
            //type is valid and already seen (most common)
            if (validTypes.Contains(t))
                return t;

            //removes the need to check this outside of this call
            if (t == null)
                return t;

            //check for editor type, this will get hit once for each new type encountered
            if (!t.Assembly.IsDefined(typeof(AssemblyIsEditorAssembly), true) && !Build.BuildUtility.IsEditorAssembly(t.Assembly))
            {
                validTypes.Add(t);
                return t;
            }

            if (t == typeof(DefaultAsset) && allowFolders)
                return typeof(DefaultAsset);

            //try to remap the editor type to a runtime type
            return MapEditorTypeToRuntimeTypeInternal(t);
        }

        static Type MapEditorTypeToRuntimeTypeInternal(Type t)
        {
            if (t == typeof(UnityEditor.Animations.AnimatorController))
                return typeof(RuntimeAnimatorController);
            if (t == typeof(UnityEditor.SceneAsset))
                return typeof(UnityEngine.ResourceManagement.ResourceProviders.SceneInstance);
            if (t.FullName == "UnityEditor.Audio.AudioMixerController")
                return typeof(UnityEngine.Audio.AudioMixer);
            if (t.FullName == "UnityEditor.Audio.AudioMixerGroupController")
                return typeof(UnityEngine.Audio.AudioMixerGroup);
            return null;
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

            if (fullCount > 0)
                settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
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
                        if (a.IsDynamic)
                            continue;
                        foreach (var t in a.ExportedTypes)
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

        internal static bool SafeMoveResourcesToGroup(AddressableAssetSettings settings, AddressableAssetGroup targetGroup, List<string> paths, List<string> guids, bool showDialog = true)
        {
            if (targetGroup == null)
            {
                Debug.LogWarning("No valid group to move Resources to");
                return false;
            }

            if (paths == null || paths.Count == 0)
            {
                Debug.LogWarning("No valid Resources found to move");
                return false;
            }

            if (guids == null)
            {
                guids = new List<string>();
                foreach (var p in paths)
                    guids.Add(AssetDatabase.AssetPathToGUID(p));
            }

            Dictionary<string, string> guidToNewPath = new Dictionary<string, string>();

            var message = "Any assets in Resources that you wish to mark as Addressable must be moved within the project. We will move the files to:\n\n";
            for (int i = 0; i < guids.Count; i++)
            {
                var newName = paths[i].Replace("\\", "/");
                newName = newName.Replace("Resources", "Resources_moved");
                newName = newName.Replace("resources", "resources_moved");
                if (newName == paths[i])
                    continue;

                guidToNewPath.Add(guids[i], newName);
                message += newName + "\n";
            }
            message += "\nAre you sure you want to proceed?";
            if (!showDialog || EditorUtility.DisplayDialog("Move From Resources", message, "Yes", "No"))
            {
                settings.MoveAssetsFromResources(guidToNewPath, targetGroup);
                return true;
            }
            return false;
        }

        static Dictionary<Type, string> s_CachedDisplayNames = new Dictionary<Type, string>();
        internal static string GetCachedTypeDisplayName(Type type)
        {
            string result = "<none>";
            if (type != null)
            {
                if (!s_CachedDisplayNames.TryGetValue(type, out result))
                {
                    var displayNameAtr = type.GetCustomAttribute<DisplayNameAttribute>();
                    if (displayNameAtr != null)
                    {
                        result = (string)displayNameAtr.DisplayName;
                    }
                    else
                        result = type.Name;

                    s_CachedDisplayNames.Add(type, result);
                }
            }

            return result;
        }
        
        internal static bool IsUsingVCIntegration()
        {
            return Provider.isActive && Provider.enabled;
        }

        private static bool DisplayDialogueForEditingLockedFile(string path)
        {
            return EditorUtility.DisplayDialog("Attemping to edit locked file",
                "File " + path + " is locked. Check out?", "Yes", "No");
        }

        private static bool MakeAssetEditable(Object target, string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;
#if UNITY_2019_4_OR_NEWER
            if(!AssetDatabase.IsOpenForEdit(target))
                return AssetDatabase.MakeEditable(path);
#else
            Asset asset = Provider.GetAssetByPath(path);
            if (asset != null && !Provider.IsOpenForEdit(asset))
            {
                Task task = Provider.Checkout(asset, CheckoutMode.Asset);
                task.Wait();
                return task.success;
            }
#endif
            return false;
        }

         internal static bool OpenAssetIfUsingVCIntegration(Object target, bool exitGUI = false)
         {
            if (target == null)
                return false;
            return OpenAssetIfUsingVCIntegration(target, AssetDatabase.GetAssetOrScenePath(target), exitGUI);
         }

        internal static bool OpenAssetIfUsingVCIntegration(Object target, string path, bool exitGUI = false)
        {
            bool openedAsset = false;
            if (string.IsNullOrEmpty(path) || !IsUsingVCIntegration())
                return openedAsset;
            if (DisplayDialogueForEditingLockedFile(path))
                openedAsset = MakeAssetEditable(target, path);
            if (exitGUI)
                GUIUtility.ExitGUI();
            return openedAsset;
        }

        internal static List<PackageManager.PackageInfo> GetPackages()
        {
            ListRequest req = Client.List();
            while (!req.IsCompleted) {}

            var packages = new List<PackageManager.PackageInfo>();
            if (req.Status == StatusCode.Success)
            {
                PackageCollection collection = req.Result;
                foreach (PackageManager.PackageInfo package in collection)
                    packages.Add(package);
            }
            return packages;
        }
    }
}
