using NUnit.Framework;
using UnityEditor.AddressableAssets.GUI;


namespace UnityEditor.AddressableAssets.Tests
{
    public class AddressableAssetWindowTests : AddressableAssetTestBase
    {
        private AddressableAssetsWindow m_AddressableAssetsWindow;

        [Test]
        public void AddressableAssetWindow_OfferToConvert_CantConvertWithNoBundles()
        {
            m_AddressableAssetsWindow = new AddressableAssetsWindow();
            var prevGroupCount = Settings.groups.Count;
            m_AddressableAssetsWindow.OfferToConvert(Settings);
            Assert.AreEqual(prevGroupCount, Settings.groups.Count);
        }
    }
}
