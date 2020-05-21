using NUnit.Framework;
#if UNITY_EDITOR
using UnityEditor.AddressableAssets.Settings;
#endif
using UnityEngine;
using UnityEngine.ResourceManagement.Diagnostics;
using UnityEngine.TestTools;

namespace DiagnosticEventCollectorIntegrationTests
{
    [UnityPlatform(exclude = new[] { RuntimePlatform.WindowsEditor, RuntimePlatform.OSXEditor, RuntimePlatform.LinuxEditor })]
    abstract class DiagnosticEventCollectorIntegrationTests : AddressablesTestFixture
    {
        protected abstract bool PostProfilerEvents();

#if UNITY_EDITOR
        protected override void RunBuilder(AddressableAssetSettings settings)
        {
            bool oldState = ProjectConfigData.postProfilerEvents;
            ProjectConfigData.postProfilerEvents = PostProfilerEvents();
            base.RunBuilder(settings);
            ProjectConfigData.postProfilerEvents = oldState;
        }
#endif

        protected override TestBuildScriptMode BuildScriptMode => TestBuildScriptMode.Packed;
    }

    class DiagnosticEventCollectorIntegrationTestsPostProfilerEventsIsTrue : DiagnosticEventCollectorIntegrationTests
    {
        protected override bool PostProfilerEvents() => true;

        [Test]
        public void WhenPostProfilerEventsIsTrue_DiagnosticEventsCollectorIsCreated()
        {
            Assert.AreEqual(1, Resources.FindObjectsOfTypeAll(typeof(DiagnosticEventCollector)).Length);
        }
    }

    class DiagnosticEventCollectorIntegrationTestsPostProfilerEventsIsFalse : DiagnosticEventCollectorIntegrationTests
    {
        protected override bool PostProfilerEvents() => false;

        [Test]
        public void WhenPostProfilerEventsIsFalse_DiagnosticEventsCollectorIsNotCreated()
        {
            Assert.AreEqual(0, Resources.FindObjectsOfTypeAll(typeof(DiagnosticEventCollector)).Length);
        }
    }
}