using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.ResourceManagement.Diagnostics;
using UnityEngine.ResourceManagement.Util;

namespace UnityEditor.AddressableAssets.Tests
{
    using Object = UnityEngine.Object;

    public class ResourceCleanupTests : AddressableAssetTestBase
    {
        int CountResourcesByName(string name)
        {
            int count = 0;
            Object[] objects = Resources.FindObjectsOfTypeAll(typeof(GameObject));
            foreach (Object o in objects)
            {
                if (o.name.Equals(name)) ++count;
            }
            return count;
        }

        [Test]
        public void CleanupEventCollector()
        {
            EditorApplication.isPlaying = true;
            Assert.NotNull(DiagnosticEventCollectorSingleton.Instance);
            Assert.True(DiagnosticEventCollectorSingleton.Exists);
            EditorApplication.isPlaying = false;

            Assert.False(DiagnosticEventCollectorSingleton.Exists);
            Assert.AreEqual(0, CountResourcesByName("EventCollector"));
        }

        [Test]
        public void CleanupDelayedActionManager()
        {
            EditorApplication.isPlaying = true;
            DelayedActionManager.AddAction(new Action(() => {}));
            Assert.True(DelayedActionManager.Exists);
            Assert.NotNull(DelayedActionManager.Instance);
            EditorApplication.isPlaying = false;
            Assert.False(DelayedActionManager.Exists);

            Assert.AreEqual(0, CountResourcesByName("DelayedActionManager"));
        }
    }
}
