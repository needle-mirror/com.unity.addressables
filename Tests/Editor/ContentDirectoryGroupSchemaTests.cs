using NUnit.Framework;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using CatalogToggles = UnityEditor.AddressableAssets.Settings.GroupSchemas.ContentDirectoryGroupSchema.CatalogToggles;
using CatalogToggleValues = UnityEditor.AddressableAssets.Settings.GroupSchemas.ContentDirectoryGroupSchema.CatalogToggleValues;

namespace UnityEditor.AddressableAssets.Tests
{
    public class ContentDirectoryGroupSchemaTests : AddressableAssetTestBase
    {
        AddressableAssetGroup m_Group;
        ContentDirectoryGroupSchema m_Schema;

        bool m_OriginalAdvancedOptionsFoldout;
        bool m_OriginalIncludedInCatalogFoldout;

        [SetUp]
        public void CreateSchema()
        {
            m_OriginalAdvancedOptionsFoldout = ContentDirectoryGroupSchema.AdvancedOptionsFoldoutActive;
            m_OriginalIncludedInCatalogFoldout = ContentDirectoryGroupSchema.IncludedInCatalogFoldoutActive;

            m_Group = Settings.CreateGroup("ContentDirectorySchemaGroup", false, false, false, null);
            m_Schema = m_Group.AddSchema<ContentDirectoryGroupSchema>(false);
        }

        [TearDown]
        public void RestoreState()
        {
            // Restore through the schema so the cached foldout values are reset too.
            ContentDirectoryGroupSchema.AdvancedOptionsFoldoutActive = m_OriginalAdvancedOptionsFoldout;
            ContentDirectoryGroupSchema.IncludedInCatalogFoldoutActive = m_OriginalIncludedInCatalogFoldout;

            if (m_Group != null)
                Settings.RemoveGroup(m_Group);
            m_Group = null;
            m_Schema = null;
        }

        [Test]
        public void ShowAllProperties_OpensBothFoldouts()
        {
            ContentDirectoryGroupSchema.AdvancedOptionsFoldoutActive = false;
            ContentDirectoryGroupSchema.IncludedInCatalogFoldoutActive = false;

            m_Schema.ShowAllProperties();

            // Read back through SessionState, which is what carries the state over a domain reload.
            Assert.IsTrue(SessionState.GetBool(ContentDirectoryGroupSchema.k_AdvancedOptionsFoldoutKey, false),
                "Expand Schema Content left the Advanced Options foldout closed");
            Assert.IsTrue(SessionState.GetBool(ContentDirectoryGroupSchema.k_IncludedInCatalogFoldoutKey, false),
                "Expand Schema Content left the Included in Catalog foldout closed");
        }

        [Test]
        public void SetCatalogToggleOptions_AppliesOnlyTheNamedToggles()
        {
            // Every toggle defaults to true, so asking for all three off exposes a stray copy.
            var allOff = new CatalogToggleValues();

            ContentDirectoryGroupSchema.SetCatalogToggleOptions(m_Schema, CatalogToggles.Labels, allOff);

            Assert.IsFalse(m_Schema.IncludeLabelsInCatalog, "Labels was named but did not apply");
            Assert.IsTrue(m_Schema.IncludeFolderKeysInCatalog, "Folder Key was not named but changed anyway");
            Assert.IsTrue(m_Schema.IncludeAddressesForFolderChildren,
                "Individual Asset Addresses was not named but changed anyway");

            ContentDirectoryGroupSchema.SetCatalogToggleOptions(m_Schema,
                CatalogToggles.FolderKeys | CatalogToggles.AddressesForFolderChildren, allOff);

            Assert.IsFalse(m_Schema.IncludeFolderKeysInCatalog, "Folder Key did not apply once named");
            Assert.IsFalse(m_Schema.IncludeAddressesForFolderChildren,
                "Individual Asset Addresses did not apply once named");
        }
    }
}
