using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor.AddressableAssets.Tests
{
    public class GroupSchemaTests : AddressableAssetTestBase
    {
        CustomTestSchema testSchemaObject;
        CustomTestSchemaSubClass testSchemaObjectSubClass;
        protected override bool PersistSettings { get { return true; } }
        protected override void OnInit()
        {
            testSchemaObject = ScriptableObject.CreateInstance<CustomTestSchema>();
            AssetDatabase.CreateAsset(testSchemaObject, TestConfigFolder + "/testSchemaObject.asset");
            testSchemaObjectSubClass = ScriptableObject.CreateInstance<CustomTestSchemaSubClass>();
            AssetDatabase.CreateAsset(testSchemaObjectSubClass, TestConfigFolder + "/testSchemaObjectSubClass.asset");
        }

        [Test]
        public void CanAddSchemaWithSavedAsset()
        {
            var group = m_settings.CreateGroup("TestGroup", false, false, false);
            var newSchema = group.AddSchema(testSchemaObject);
            Assert.IsNotNull(newSchema);
            Assert.AreNotEqual(newSchema, testSchemaObject);
            Assert.IsTrue(group.HasSchema(testSchemaObject.GetType()));
            Assert.IsTrue(group.RemoveSchema(testSchemaObject.GetType()));
        }

        [Test]
        public void CanAddSchemaWithSavedAssetGeneric()
        {
            var group = m_settings.CreateGroup("TestGroup", false, false, false);
            var newSchema = group.AddSchema(testSchemaObject);
            Assert.IsNotNull(newSchema);
            Assert.AreNotEqual(newSchema, testSchemaObject);
            Assert.IsTrue(group.HasSchema<CustomTestSchema>());
            Assert.IsTrue(group.RemoveSchema<CustomTestSchema>());
        }

        [Test]
        public void CanAddSchemaWithNonSavedAsset()
        {
            var group = m_settings.CreateGroup("TestGroup", false, false, false);
            var templateSchema = ScriptableObject.CreateInstance<CustomTestSchema>();
            var newSchema = group.AddSchema(templateSchema);
            Assert.IsNotNull(newSchema);
            Assert.AreNotEqual(newSchema, templateSchema);
            Assert.IsTrue(group.HasSchema(templateSchema.GetType()));
            Assert.IsTrue(group.RemoveSchema(templateSchema.GetType()));
        }

        [Test]
        public void CanAddAndRemoveSchemaObjectByType()
        {
            var group = m_settings.CreateGroup("TestGroup", false, false, false);
            var s = group.AddSchema(typeof(CustomTestSchema));
            Assert.IsNotNull(s);
            string guid;
            long lfid;
            Assert.IsTrue(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(s, out guid, out lfid));
            var path = AssetDatabase.GUIDToAssetPath(guid);
            FileAssert.Exists(path);
            Assert.IsTrue(group.RemoveSchema(typeof(CustomTestSchema)));
            FileAssert.DoesNotExist(path);
        }

        [Test]
        public void CanAddAndRemoveSchemaObjectByGenericType()
        {
            var group = m_settings.CreateGroup("TestGroup", false, false, false);
            var s = group.AddSchema<CustomTestSchema>();
            Assert.IsNotNull(s);
            string guid;
            long lfid;
            Assert.IsTrue(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(s, out guid, out lfid));
            var path = AssetDatabase.GUIDToAssetPath(guid);
            FileAssert.Exists(path);
            Assert.IsTrue(group.RemoveSchema<CustomTestSchema>());
            FileAssert.DoesNotExist(path);
        }

        [Test]
        public void CanCheckSchemaObjectByGenericType()
        {
            var group = m_settings.CreateGroup("TestGroup", false, false, false);
            Assert.IsNotNull(group.AddSchema<CustomTestSchema>());
            Assert.IsTrue(group.HasSchema<CustomTestSchema>());
            Assert.IsTrue(group.RemoveSchema<CustomTestSchema>());
        }

        [Test]
        public void CanCheckSchemaObjectAsSubclass()
        {
            var group = m_settings.CreateGroup("TestGroup", false, false, false);
            Assert.IsNotNull(group.AddSchema<CustomTestSchemaSubClass>());
            Assert.IsFalse(group.HasSchema<CustomTestSchema>());
            Assert.IsTrue(group.HasSchema<CustomTestSchemaSubClass>());
            Assert.IsFalse(group.RemoveSchema<CustomTestSchema>());
            Assert.IsTrue(group.RemoveSchema<CustomTestSchemaSubClass>());
        }

        [Test]
        public void CanCheckSchemaObjectAsBaseclass()
        {
            var group = m_settings.CreateGroup("TestGroup", false, false, false);
            Assert.IsNotNull(group.AddSchema<CustomTestSchema>());
            Assert.IsTrue(group.HasSchema<CustomTestSchema>());
            Assert.IsFalse(group.HasSchema<CustomTestSchemaSubClass>());
            Assert.IsTrue(group.RemoveSchema<CustomTestSchema>());
            Assert.IsFalse(group.RemoveSchema<CustomTestSchemaSubClass>());
        }

        [Test]
        public void CanNotAddDuplicateSchemaObjects()
        {
            var group = m_settings.CreateGroup("TestGroup", false, false, false);
            var added = group.AddSchema<CustomTestSchemaSubClass>();
            Assert.IsNotNull(added);
            Assert.AreEqual(added, group.AddSchema<CustomTestSchemaSubClass>());
            Assert.IsNotNull(group.AddSchema<CustomTestSchema>());
            Assert.IsTrue(group.RemoveSchema<CustomTestSchemaSubClass>());
            Assert.IsTrue(group.RemoveSchema<CustomTestSchema>());
        }
    }

}
