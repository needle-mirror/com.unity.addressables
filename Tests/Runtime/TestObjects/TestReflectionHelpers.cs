using System.Reflection;

namespace UnityEditor.AddressableAssets.Tests.Runtime.TestObjects
{
    static class TestReflectionHelpers
    {
        public static void SetResritctMainThreadFileIO(bool errorOnMainThreadFileIO)
        {
            System.Type t = System.Type.GetType("UnityEngine.IO.File, UnityEngine.CoreModule");
            if (t != null)
            {
                PropertyInfo pInfo = t.GetProperty("MainThreadIORestrictionMode", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                if (pInfo != null)
                    pInfo.SetValue(null, errorOnMainThreadFileIO ? 1 : 0);
            }
        }
    }
}
