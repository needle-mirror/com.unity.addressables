using NUnit.Framework;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace UnityEditor.AddressableAssets.Tests
{
    public class CatalogOptionsCopyTests
    {
        BundledAssetGroupSchema m_BundledSchema;

        [SetUp]
        public void CreateSchema()
        {
            m_BundledSchema = ScriptableObject.CreateInstance<BundledAssetGroupSchema>();
        }

        [TearDown]
        public void DestroySchema()
        {
            if (m_BundledSchema != null)
                Object.DestroyImmediate(m_BundledSchema);
            m_BundledSchema = null;
        }

        static void AssertLabel(GUIContent content, string expectedLabel)
        {
            Assert.AreEqual(expectedLabel, content.text, "label text drifted from the approved copy");
            Assert.IsNotEmpty(content.tooltip, $"'{expectedLabel}' has no tooltip");
            Assert.That(content.text, Does.Not.Contain("in Catalog"),
                $"'{content.text}' still repeats what the Included in Catalog header already says");
        }

        [Test]
        public void BundledSchema_CatalogOptions_UseApprovedCopy()
        {
            AssertLabel(m_BundledSchema.m_IncludeAddressInCatalogContent, "Addresses");
            AssertLabel(m_BundledSchema.m_IncludeGUIDInCatalogContent, "GUIDs");
            AssertLabel(m_BundledSchema.m_IncludeLabelsInCatalogContent, "Labels");
            AssertLabel(m_BundledSchema.m_IncludeFolderKeysInCatalogContent, "Folder Key");
            AssertLabel(m_BundledSchema.m_IncludeAddressesForFolderChildrenContent, "Individual Asset Addresses");
        }

        [Test]
        public void ContentDirectorySchema_CatalogOptions_UseApprovedCopy()
        {
            AssertLabel(ContentDirectoryGroupSchema.k_IncludeLabelsInCatalogContent, "Labels");
            AssertLabel(ContentDirectoryGroupSchema.k_IncludeFolderKeysInCatalogContent, "Folder Key");
            AssertLabel(ContentDirectoryGroupSchema.k_IncludeAddressesForFolderChildrenContent,
                "Individual Asset Addresses");
        }

        [Test]
        public void SharedCatalogOptions_ReadTheSameInBothSchemas()
        {
            AssertSameContent(m_BundledSchema.m_IncludeLabelsInCatalogContent,
                ContentDirectoryGroupSchema.k_IncludeLabelsInCatalogContent);
            AssertSameContent(m_BundledSchema.m_IncludeFolderKeysInCatalogContent,
                ContentDirectoryGroupSchema.k_IncludeFolderKeysInCatalogContent);
            AssertSameContent(m_BundledSchema.m_IncludeAddressesForFolderChildrenContent,
                ContentDirectoryGroupSchema.k_IncludeAddressesForFolderChildrenContent);
        }

        static void AssertSameContent(GUIContent bundled, GUIContent contentDirectory)
        {
            Assert.AreEqual(bundled.text, contentDirectory.text, "the two schemas label this control differently");
            Assert.AreEqual(bundled.tooltip, contentDirectory.tooltip,
                $"the '{bundled.text}' tooltip drifted between the two schemas");
        }
    }
}
