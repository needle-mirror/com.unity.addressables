using UnityEditor.AddressableAssets.Settings;

namespace UnityEditor.AddressableAssets.GUI
{
    internal class UpgradeNotifications
    {
        private readonly string[] oldEntryNames = new string[]
        {
            "LocalBuildPath", "LocalLoadPath", "RemoteBuildPath", "RemoteLoadPath"
        };

        private readonly string[] newEntryNames = new string[]
        {
            AddressableAssetSettings.kLocalBuildPath, AddressableAssetSettings.kLocalLoadPath, AddressableAssetSettings.kRemoteBuildPath, AddressableAssetSettings.kRemoteLoadPath
        };

        internal bool NeedsPathPairMigration(AddressableAssetSettings settings)
        {
            for (int i = 0; i < oldEntryNames.Length; i++)
            {
                // basically we verify that the old variable exists and that the new variable does not yet exist
                if (settings.profileSettings.GetVariableNames().Contains(oldEntryNames[i]) &&
                    settings.profileSettings.ValidateNewVariableName(newEntryNames[i]))
                {
                    return true;
                }
            }

            return false;
        }

        internal void DoPathPairMigration(AddressableAssetSettings settings)
        {
            for (int i = 0; i < oldEntryNames.Length; i++)
            {
                if (settings.profileSettings.GetVariableNames().Contains(oldEntryNames[i]) &&
                    settings.profileSettings.ValidateNewVariableName(newEntryNames[i]))
                {
                    var profileVariable = settings.profileSettings.GetProfileDataByName(oldEntryNames[i]);
                    Undo.RecordObject(settings, "Profile Variable Renamed");
                    profileVariable.SetName(newEntryNames[i], settings.profileSettings);
                    AddressableAssetUtility.OpenAssetIfUsingVCIntegration(settings, true);
                }
            }
        }

        internal void ShowUpgradeNotifications(AddressableAssetSettings settings)
        {
            bool pathPairMigrationCheck = ProjectConfigData.UserHasBeenInformedAboutPathPairMigration;
            if (!pathPairMigrationCheck && NeedsPathPairMigration(settings))
            {
                bool doPathPairMigration = EditorUtility.DisplayDialog("Migrate Path Pairs",
                    "Addressables now uses a new path pair format. Your path pairs, like 'RemoteLoadPath', can be migrated automatically to the new format, 'Remote.LoadPath'.",
                    "Migrate Path Pairs", "Skip");
                if (doPathPairMigration)
                    DoPathPairMigration(settings);
                ProjectConfigData.UserHasBeenInformedAboutPathPairMigration = true;
            }
        }

        internal void ShowAllowNestedFoldersNotifications(AddressableAssetSettings settings)
        {
            bool allowNestedFolderCheck = ProjectConfigData.UserHasBeenInformedAbouNestedFolderStructure;
            if (!allowNestedFolderCheck && settings.AllowNestedBundleFolders)
            {
                bool enableNestedFolders = EditorUtility.DisplayDialog(
                "Allow Nested Bundle Folders",
                "Folders can now be nested inside other folders. Not allowing this may change AssetBundle filenames for Pack Separately groups with slashes in addresses. Allow nested folders for this project? You can change this later in Addressable Asset Settings.",
                "Allow", "Don't Allow");

                settings.AllowNestedBundleFolders = enableNestedFolders;
                ProjectConfigData.UserHasBeenInformedAbouNestedFolderStructure = true;
            }
        }

        internal static void Show(AddressableAssetSettings settings)
        {
            var upgradeNotifications = new UpgradeNotifications();
            upgradeNotifications.ShowUpgradeNotifications(settings);
            upgradeNotifications.ShowAllowNestedFoldersNotifications(settings);
        }
    }
}
