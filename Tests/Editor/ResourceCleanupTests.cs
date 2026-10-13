using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.ResourceManagement.Util;
using UnityEngine.TestTools;

namespace UnityEditor.AddressableAssets.Tests
{
    using Object = UnityEngine.Object;

    public class ResourceCleanupTests
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

        [UnityTest]
#if UNITY_7000_0_OR_NEWER
        [Ignore("Domain reload logs an assembly location error on trunk. Tracked in CBD-2441.")]
#endif
        public IEnumerator CleanupDelayedActionManager()
        {
            yield return new EnterPlayMode();
            Assert.AreEqual(0, CountResourcesByName("DelayedActionManager"));
            DelayedActionManager.AddAction(new Action(() => { }));
            Assert.True(DelayedActionManager.Exists);
            Assert.NotNull(DelayedActionManager.Instance);
            Assert.AreEqual(1, CountResourcesByName("DelayedActionManager"));
            yield return new ExitPlayMode();
            Assert.False(DelayedActionManager.Exists);
            Assert.AreEqual(0, CountResourcesByName("DelayedActionManager"));
        }
    }
}
