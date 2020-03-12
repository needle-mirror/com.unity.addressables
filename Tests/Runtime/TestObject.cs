using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestObject : ScriptableObject
{
    static public TestObject Create(string name)
    {
        var so = CreateInstance<TestObject>();
        so.name = name;
        return so;
    }
}
