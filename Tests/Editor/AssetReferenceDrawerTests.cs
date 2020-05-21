using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEditor.AddressableAssets.GUI;
using UnityEngine.AddressableAssets;

namespace UnityEditor.AddressableAssets.Tests
{
    public class AssetReferenceDrawerTests : AddressableAssetTestBase
    {
        private AssetReferenceDrawer m_AssetReferenceDrawer;
        
        private class TestARDObject : TestObject
        {
            [SerializeField]
            [AssetReferenceUILabelRestriction(new[] {"HD"})]
            private AssetReference Reference = new AssetReference();
        }
        
        private class TestARDObjectMultipleLabels : TestObject
        {
            [SerializeField]
            [AssetReferenceUILabelRestriction(new[] {"HDR","test","default"})]
            private AssetReference ReferenceMultiple = new AssetReference();
        }

        [Test]
        public void CanRestrictLabel()
        {
            m_AssetReferenceDrawer = new AssetReferenceDrawer();
            TestARDObject obj = ScriptableObject.CreateInstance<TestARDObject>();
            var so = new SerializedObject(obj);
            var property = so.FindProperty("Reference");
            m_AssetReferenceDrawer.GatherFilters(property);
            Assert.AreEqual(m_AssetReferenceDrawer.Restrictions.Count,1);
            List<AssetReferenceUIRestrictionSurrogate> restrictions = m_AssetReferenceDrawer.Restrictions;
            Assert.True(restrictions.First().ToString().Contains("HD"));
        }
        
        [Test]
        public void CanRestrictMultipleLabels()
        {
            m_AssetReferenceDrawer = new AssetReferenceDrawer();
            TestARDObjectMultipleLabels obj = ScriptableObject.CreateInstance<TestARDObjectMultipleLabels>();
            var so = new SerializedObject(obj);
            var property = so.FindProperty("ReferenceMultiple");
            m_AssetReferenceDrawer.GatherFilters(property);
            List<AssetReferenceUIRestrictionSurrogate> restrictions = m_AssetReferenceDrawer.Restrictions;
            string restriction = restrictions.First().ToString();
            Assert.True(restriction.Contains("HDR"));
            Assert.True(restriction.Contains("test"));
            Assert.True(restriction.Contains("default"));
        }

    }
}
