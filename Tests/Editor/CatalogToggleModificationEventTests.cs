using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;

namespace UnityEditor.AddressableAssets.Tests
{
    public class CatalogToggleModificationEventTests : AddressableAssetTestBase
    {
        AddressableAssetGroup m_Group;
        BundledAssetGroupSchema m_BundledSchema;
        ContentDirectoryGroupSchema m_ContentDirectorySchema;

        struct CatalogToggle
        {
            public string Name;
            public Func<bool> Read;
            public Action<bool> Write;
        }

        [SetUp]
        public void CreateSchemas()
        {
            m_Group = Settings.CreateGroup("CatalogToggleEventGroup", false, false, false, null);
            m_BundledSchema = m_Group.AddSchema<BundledAssetGroupSchema>(false);
            m_ContentDirectorySchema = m_Group.AddSchema<ContentDirectoryGroupSchema>(false);
        }

        [TearDown]
        public void RemoveGroup()
        {
            if (m_Group != null)
                Settings.RemoveGroup(m_Group);
            m_Group = null;
            m_BundledSchema = null;
            m_ContentDirectorySchema = null;
        }

        List<CatalogToggle> AllCatalogToggles()
        {
            return new List<CatalogToggle>
            {
                new CatalogToggle
                {
                    Name = "Bundled.IncludeAddressInCatalog",
                    Read = () => m_BundledSchema.IncludeAddressInCatalog,
                    Write = value => m_BundledSchema.IncludeAddressInCatalog = value
                },
                new CatalogToggle
                {
                    Name = "Bundled.IncludeGUIDInCatalog",
                    Read = () => m_BundledSchema.IncludeGUIDInCatalog,
                    Write = value => m_BundledSchema.IncludeGUIDInCatalog = value
                },
                new CatalogToggle
                {
                    Name = "Bundled.IncludeLabelsInCatalog",
                    Read = () => m_BundledSchema.IncludeLabelsInCatalog,
                    Write = value => m_BundledSchema.IncludeLabelsInCatalog = value
                },
                new CatalogToggle
                {
                    Name = "Bundled.IncludeFolderKeysInCatalog",
                    Read = () => m_BundledSchema.IncludeFolderKeysInCatalog,
                    Write = value => m_BundledSchema.IncludeFolderKeysInCatalog = value
                },
                new CatalogToggle
                {
                    Name = "Bundled.IncludeAddressesForFolderChildren",
                    Read = () => m_BundledSchema.IncludeAddressesForFolderChildren,
                    Write = value => m_BundledSchema.IncludeAddressesForFolderChildren = value
                },
                new CatalogToggle
                {
                    Name = "ContentDirectory.IncludeLabelsInCatalog",
                    Read = () => m_ContentDirectorySchema.IncludeLabelsInCatalog,
                    Write = value => m_ContentDirectorySchema.IncludeLabelsInCatalog = value
                },
                new CatalogToggle
                {
                    Name = "ContentDirectory.IncludeFolderKeysInCatalog",
                    Read = () => m_ContentDirectorySchema.IncludeFolderKeysInCatalog,
                    Write = value => m_ContentDirectorySchema.IncludeFolderKeysInCatalog = value
                },
                new CatalogToggle
                {
                    Name = "ContentDirectory.IncludeAddressesForFolderChildren",
                    Read = () => m_ContentDirectorySchema.IncludeAddressesForFolderChildren,
                    Write = value => m_ContentDirectorySchema.IncludeAddressesForFolderChildren = value
                }
            };
        }

        // Collects the schemas named by every GroupSchemaModified event that write raises.
        static List<object> CaptureSchemaModifications(Action write)
        {
            var modifiedSchemas = new List<object>();
            Action<AddressableAssetSettings, AddressableAssetSettings.ModificationEvent, object> callback =
                (settings, modificationEvent, eventData) =>
                {
                    if (modificationEvent == AddressableAssetSettings.ModificationEvent.GroupSchemaModified)
                        modifiedSchemas.Add(eventData);
                };

            AddressableAssetSettings.OnModificationGlobal += callback;
            try
            {
                write();
            }
            finally
            {
                AddressableAssetSettings.OnModificationGlobal -= callback;
            }

            return modifiedSchemas;
        }

        [Test]
        public void SettingCatalogToggle_PostsGroupSchemaModified_OnlyWhenTheValueChanges()
        {
            foreach (var toggle in AllCatalogToggles())
            {
                bool flipped = !toggle.Read();

                var onChange = CaptureSchemaModifications(() => toggle.Write(flipped));
                Assert.AreEqual(1, onChange.Count, $"{toggle.Name} posted {onChange.Count} events, expected 1");
                Assert.AreEqual(flipped, toggle.Read(), $"{toggle.Name} did not keep its new value");

                // Every setter guards on !=, so a repaint that rewrites the same value must stay quiet.
                var onRewrite = CaptureSchemaModifications(() => toggle.Write(flipped));
                Assert.IsEmpty(onRewrite, $"{toggle.Name} posted an event for an unchanged value");
            }
        }

        [Test]
        public void SettingBundleMode_PostsGroupSchemaModified_OnlyWhenTheValueChanges()
        {
            var flipped = m_BundledSchema.BundleMode == BundledAssetGroupSchema.BundlePackingMode.PackTogether
                ? BundledAssetGroupSchema.BundlePackingMode.PackSeparately
                : BundledAssetGroupSchema.BundlePackingMode.PackTogether;

            var onChange = CaptureSchemaModifications(() => m_BundledSchema.BundleMode = flipped);
            Assert.AreEqual(1, onChange.Count, "BundleMode posted the wrong number of events");
            Assert.AreEqual(m_BundledSchema, onChange[0], "BundleMode named the wrong schema");
            Assert.AreEqual(flipped, m_BundledSchema.BundleMode);

            var onRewrite = CaptureSchemaModifications(() => m_BundledSchema.BundleMode = flipped);
            Assert.IsEmpty(onRewrite, "BundleMode posted an event for an unchanged value");
        }

        [Test]
        public void SetCatalogToggleOptions_PostsOnlyWhenTheValueChanges()
        {
            var current = new ContentDirectoryGroupSchema.CatalogToggleValues
            {
                IncludeLabels = m_ContentDirectorySchema.IncludeLabelsInCatalog,
                IncludeFolderKeys = m_ContentDirectorySchema.IncludeFolderKeysInCatalog,
                IncludeAddressesForFolderChildren = m_ContentDirectorySchema.IncludeAddressesForFolderChildren
            };
            var everyToggle = ContentDirectoryGroupSchema.CatalogToggles.Labels |
                ContentDirectoryGroupSchema.CatalogToggles.FolderKeys |
                ContentDirectoryGroupSchema.CatalogToggles.AddressesForFolderChildren;

            // Multi-select applies to every selected schema, including ones already matching.
            var onMatch = CaptureSchemaModifications(() =>
                ContentDirectoryGroupSchema.SetCatalogToggleOptions(m_ContentDirectorySchema, everyToggle, current));
            Assert.IsEmpty(onMatch, "Applying values the schema already held posted an event");

            var flipped = current;
            flipped.IncludeLabels = !current.IncludeLabels;

            var onChange = CaptureSchemaModifications(() =>
                ContentDirectoryGroupSchema.SetCatalogToggleOptions(m_ContentDirectorySchema, everyToggle, flipped));
            Assert.AreEqual(1, onChange.Count, $"Expected one event, got {onChange.Count}");
            Assert.AreEqual(m_ContentDirectorySchema, onChange[0], "The event named the wrong schema");
        }
    }
}
