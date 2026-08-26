using UnityEngine;

namespace UnityEditor.AddressableAssets.Tests.Runtime.TestObjects
{
    public class TestObject2 : ScriptableObject
    {
        static public TestObject2 Create(string name)
        {
            var so = CreateInstance<TestObject2>();
            so.name = name;
            return so;
        }
    }
}
