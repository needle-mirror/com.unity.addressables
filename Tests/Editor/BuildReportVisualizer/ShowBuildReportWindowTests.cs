using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets.BuildReportVisualizer;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.Editor.BuildReportVisualizer
{
    // The preference must open whichever window draws the Addressables report on the running editor.
    public class ShowBuildReportWindowTests
    {
        const string k_BuildAnalysisWindowType = "UnityEditor.Build.Analysis.BuildAnalysisWindow";

        static EditorWindow[] OpenBuildAnalysisWindows()
        {
            return Resources.FindObjectsOfTypeAll<EditorWindow>()
                .Where(window => window.GetType().FullName == k_BuildAnalysisWindowType)
                .ToArray();
        }

        [SetUp]
        [TearDown]
        public void CloseReportWindows()
        {
            foreach (EditorWindow window in OpenBuildAnalysisWindows())
                window.Close();
            foreach (BuildReportWindow window in Resources.FindObjectsOfTypeAll<BuildReportWindow>())
                window.Close();
        }

#if ENABLE_BUILD_HISTORY_EXTERNAL_BUILDS
        [Test]
        public void ShowBuildReportWindow_OpensTheBuildAnalysisWindow()
        {
            BuildReportUtility.ShowBuildReportWindow();

            // A menu item renamed on the editor side leaves ExecuteMenuItem logging an error and opening nothing.
            LogAssert.NoUnexpectedReceived();
            Assert.IsNotEmpty(OpenBuildAnalysisWindows(),
                $"'{BuildReportUtility.BuildAnalysisWindowMenuItem}' did not open the Build Analysis window.");
            Assert.IsEmpty(Resources.FindObjectsOfTypeAll<BuildReportWindow>(),
                "The standalone Addressables Report window is superseded by Build Analysis here and must stay closed.");
        }
#else
        [Test]
        public void ShowBuildReportWindow_OpensTheAddressablesReportWindow()
        {
            BuildReportUtility.ShowBuildReportWindow();

            LogAssert.NoUnexpectedReceived();
            Assert.IsNotEmpty(Resources.FindObjectsOfTypeAll<BuildReportWindow>(),
                "Editors without the Build Analysis window keep the standalone Addressables Report window.");
        }
#endif
    }
}
