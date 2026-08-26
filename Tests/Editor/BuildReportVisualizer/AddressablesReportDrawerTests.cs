#if ENABLE_BUILD_HISTORY_EXTERNAL_BUILDS
using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Build.Layout;
using UnityEditor.AddressableAssets.BuildReportVisualizer;
using UnityEditor.Build;
using UnityEditor.Build.Analysis;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Tests.Editor.BuildReportVisualizer
{
    public class AddressablesReportDrawerTests
    {
        const string k_ProfileName = "TestProfile";

        GUID m_RegisteredBuild;
        string m_LayoutSourcePath;

        [SetUp]
        public void RegisterAddressablesBuild()
        {
            BuildLayout layout = new BuildLayout
            {
                AddressablesBuildSessionGUID = GUID.Generate(),
                BuildTarget = BuildTarget.Android,
                BuildStart = DateTime.Now,
                Duration = 12.5,
                LocalCatalogBuildPath = "Library/com.unity.addressables/aa/catalog.bin",
                AddressablesEditorSettings = new BuildLayout.AddressablesEditorData
                {
                    ActiveProfile = new BuildLayout.Profile { Name = k_ProfileName }
                },
                AddressablesRuntimeSettings = new BuildLayout.AddressablesRuntimeData()
            };

            m_RegisteredBuild = layout.AddressablesBuildSessionGUID;
            m_LayoutSourcePath = Path.Combine(Path.GetTempPath(), $"testLayout_{m_RegisteredBuild}.json");
            layout.WriteToFile(m_LayoutSourcePath, false);

            AddressablesBuildHistorySupport.RegisterBuild(layout, m_LayoutSourcePath);
        }

        [TearDown]
        public void DeleteAddressablesBuild()
        {
            BuildHistory.DeleteHistory(new[] { m_RegisteredBuild });
            if (File.Exists(m_LayoutSourcePath))
                File.Delete(m_LayoutSourcePath);
        }

        BuildReportSummary RegisteredSummary()
        {
            Assert.IsTrue(BuildHistory.TryGetFilePath(m_RegisteredBuild, "BuildReportSummary.json", out string summaryPath));
            return BuildReportSummary.Load(summaryPath);
        }

        static AddressablesReportDrawer CreateDrawer()
        {
            var drawer = new AddressablesReportDrawer();
            drawer.CreateContent();
            return drawer;
        }

        static BuildReportSummary CreateSummary(GUID buildSessionGuid)
        {
            return new BuildReportSummary
            {
                BuildSessionGUID = buildSessionGuid,
                BuildType = UnityEditor.Build.Reporting.BuildType.AssetBundle
            };
        }

        [Test]
        public void EmbeddedToolbarAssets_CanBeLoaded()
        {
            Assert.IsNotNull(
                AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(BuildReportUtility.EmbeddedReportUxmlPath),
                "Could not load " + BuildReportUtility.EmbeddedReportUxmlPath);
            Assert.IsNotNull(
                AssetDatabase.LoadAssetAtPath<StyleSheet>(BuildReportUtility.BuildReportContentViewEmbeddedUssPath),
                "Could not load " + BuildReportUtility.BuildReportContentViewEmbeddedUssPath);
        }

        [Test]
        public void Drawer_DeclaresThePackageTheRegisteredBuildRecords()
        {
            var attribute = (ExternalBuildDrawerAttribute)Attribute.GetCustomAttribute(
                typeof(AddressablesReportDrawer), typeof(ExternalBuildDrawerAttribute));

            Assert.IsNotNull(attribute, "Without the attribute the Build Analysis window cannot route builds to the drawer.");
            Assert.AreEqual(AddressablesBuildHistorySupport.kPackageName, attribute.ProducerPackage);
            Assert.AreEqual(attribute.ProducerPackage, RegisteredSummary().ProducerPackage,
                "Routing only works while the registered build records the same package the drawer declares.");
        }

        [Test]
        public void OnBuildSelected_ConsumesTheLayoutFromTheBuildHistoryEntry()
        {
            var drawer = CreateDrawer();

            drawer.OnBuildSelected(RegisteredSummary());

            Assert.IsNotNull(drawer.ShownReport);
            Assert.AreEqual(k_ProfileName, drawer.ShownReport.AddressablesEditorSettings.ActiveProfile.Name);
        }

        [Test]
        public void OnBuildSelected_SameBuildTwice_KeepsTheParsedLayout()
        {
            var drawer = CreateDrawer();

            drawer.OnBuildSelected(RegisteredSummary());
            BuildLayout first = drawer.ShownReport;
            drawer.OnBuildSelected(RegisteredSummary());

            Assert.AreSame(first, drawer.ShownReport, "Reselecting the same build should not parse the layout again.");
        }

        [Test]
        public void OnBuildSelected_AfterDeselect_ParsesTheLayoutAgain()
        {
            var drawer = CreateDrawer();

            drawer.OnBuildSelected(RegisteredSummary());
            BuildLayout first = drawer.ShownReport;
            drawer.OnBuildDeselected();
            drawer.OnBuildSelected(RegisteredSummary());

            Assert.AreNotSame(first, drawer.ShownReport);
        }

        [Test]
        public void OnBuildSelected_BuildWithoutAnAddressablesLayout_Warns()
        {
            var drawer = CreateDrawer();
            drawer.OnBuildSelected(RegisteredSummary());

            LogAssert.Expect(LogType.Warning, new Regex("No Addressables build report was found"));
            drawer.OnBuildSelected(CreateSummary(GUID.Generate()));
        }
    }
}
#endif
