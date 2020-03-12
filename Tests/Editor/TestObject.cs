using UnityEngine;

namespace UnityEditor.AddressableAssets.Tests
{
	public class TestObject : ScriptableObject
	{
        public static TestObject Create(string name)
        {
            var obj = CreateInstance<TestObject>();
            obj.name = name;
            return obj;
        }
	}
}
