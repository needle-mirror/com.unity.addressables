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
            DiagnosticEventCollector.FindOrCreateGlobalInstance();
            EditorApplication.isPlaying = false;

            Assert.AreEqual(0, CountResourcesByName("EventCollector"));
        }

        [Test]
        public void CleanupDelayedActionManager()
        {
            EditorApplication.isPlaying = true;
            DelayedActionManager.AddAction(new Action(() => {}));
            EditorApplication.isPlaying = false;
            
            Assert.AreEqual(0, CountResourcesByName("DelayedActionManager"));
            
        }
    }
}
