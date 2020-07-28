using NUnit.Framework;

namespace UnityEngine.AddressableAssets
{
    public class AddressablesVersionTests
    {
        [Test]
        public void TestPackageVersion()
        {
            // Make sure that the version strings in the package and Addressables don't get out of sync.
            // Unfortunately, the PackageInfo methods don't exist in earlier versions of the editor.
#if UNITY_2019_3_OR_NEWER
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(AddressablesVersion).Assembly);
            Assert.AreEqual(AddressablesVersion.kPackageName, packageInfo.name);
            Assert.AreEqual(AddressablesVersion.kPackageVersion, packageInfo.version);
#endif
        }
    }
}