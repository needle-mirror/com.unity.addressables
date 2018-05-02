using NUnit.Framework;

namespace UnityEditor.AddressableAssets.Tests
{
    public class ProfileSettingsTests : AddressableAssetTestBase
    {
        [Test]
        public void AddRemoveProfile()
        {
            //Arrange
            Assert.IsNotNull(settings.profileSettings);
            settings.activeProfileId = null;
            var mainId = settings.profileSettings.Reset();
            
            //Act 
            var secondId = settings.profileSettings.AddProfile("TestProfile", mainId);

            //Assert
            bool foundIt = false;
            foreach(var prof in settings.profileSettings.profiles)
            {
                if (prof.profileName == "TestProfile")
                    foundIt = true;
            }
            Assert.IsTrue(foundIt);
            Assert.IsNotEmpty(secondId);

            //Act again
            settings.profileSettings.RemoveProfile(secondId);

            //Assert again
            foundIt = false;
            foreach (var prof in settings.profileSettings.profiles)
            {
                if (prof.profileName == "TestProfile")
                    foundIt = true;
            }
            Assert.IsFalse(foundIt);
        }

        [Test]
        public void CreateValuePropogtesValue()
        {
            //Arrange
            Assert.IsNotNull(settings.profileSettings);
            settings.activeProfileId = null;
            var mainId = settings.profileSettings.Reset();
            var secondId = settings.profileSettings.AddProfile("TestProfile", mainId);

            //Act
            string path = "/Assets/Important";
            settings.profileSettings.CreateValue("SomePath", path, AddressableAssetSettings.ProfileSettings.ProfileEntryUsage.Other);

            //Assert
            Assert.AreEqual(path, settings.profileSettings.GetValueByName(mainId, "SomePath"));
            Assert.AreEqual(path, settings.profileSettings.GetValueByName(secondId, "SomePath"));
        }
        [Test]
        public void SetValueOnlySetsDesiredProfile()
        {
            //Arrange
            Assert.IsNotNull(settings.profileSettings);
            settings.activeProfileId = null;
            var mainId = settings.profileSettings.Reset();
            var secondId = settings.profileSettings.AddProfile("TestProfile", mainId);
            string originalPath = "/Assets/Important";
            settings.profileSettings.CreateValue("SomePath", originalPath, AddressableAssetSettings.ProfileSettings.ProfileEntryUsage.Other);

            //Act
            string newPath = "/Assets/LessImportant";
            settings.profileSettings.SetValue(secondId, "SomePath", newPath);

            //Assert
            Assert.AreEqual(originalPath, settings.profileSettings.GetValueByName(mainId, "SomePath"));
            Assert.AreEqual(newPath, settings.profileSettings.GetValueByName(secondId, "SomePath"));
        }
        [Test]
        public void CanGetValueById()
        {
            //Arrange
            Assert.IsNotNull(settings.profileSettings);
            settings.activeProfileId = null;
            var mainId = settings.profileSettings.Reset();
            string originalPath = "/Assets/Important";
            settings.profileSettings.CreateValue("SomePath", originalPath, AddressableAssetSettings.ProfileSettings.ProfileEntryUsage.Other);

            //Act
            string varId = null;
            foreach(var variable in settings.profileSettings.profileEntryNames)
            {
                if(variable.name == "SomePath")
                {
                    varId = variable.id;
                    break;
                }
            }

            //Assert
            Assert.AreEqual(originalPath, settings.profileSettings.GetValueById(mainId, varId));
        }
        [Test]
        public void EvaluatingUnknownIdReturnsIdAsResult()
        {
            //Arrange
            Assert.IsNotNull(settings.profileSettings);
            settings.activeProfileId = null;
            settings.profileSettings.Reset();

            //Act
            string badIdName = "BadIdName";


            //Assert
            Assert.AreEqual(badIdName, AddressableAssetSettings.ProfileSettings.ProfileIDData.Evaluate(settings.profileSettings, settings.activeProfileId, badIdName));

        }
        [Test]
        public void MissingVariablesArePassThrough()
        {
            //Arrange
            Assert.IsNotNull(settings.profileSettings);
            settings.activeProfileId = null;

            //Act
            settings.profileSettings.Reset();

            //Assert
            Assert.AreEqual("VariableNotThere", AddressableAssetSettings.ProfileSettings.TryGetProfileID("VariableNotThere"));
        }
        [Test]
        public void DefaultVariablesExist()
        {
            //Arrange
            Assert.IsNotNull(settings.profileSettings);
            settings.activeProfileId = null;

            //Act
            settings.profileSettings.Reset();

            //Assert
            Assert.AreNotEqual("LocalBuildPath", AddressableAssetSettings.ProfileSettings.TryGetProfileID("LocalBuildPath"));
            Assert.AreNotEqual("LocalLoadPrefix", AddressableAssetSettings.ProfileSettings.TryGetProfileID("LocalLoadPrefix"));
        }

    }
}