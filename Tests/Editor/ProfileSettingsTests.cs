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
            foreach (var prof in settings.profileSettings.profiles)
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
            settings.profileSettings.CreateValue("SomePath", path);

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
            string originalPath = "/Assets/Important";
            settings.profileSettings.CreateValue("SomePath", originalPath);
            var secondId = settings.profileSettings.AddProfile("TestProfile", mainId);

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
            settings.profileSettings.CreateValue("SomePath", originalPath);

            //Act
            string varId = null;
            foreach (var variable in settings.profileSettings.profileEntryNames)
            {
                if (variable.Name == "SomePath")
                {
                    varId = variable.Id;
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
            Assert.AreEqual(badIdName, AddressableAssetProfileSettings.ProfileIDData.Evaluate(settings.profileSettings, settings.activeProfileId, badIdName));

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
            Assert.AreEqual("VariableNotThere", settings.profileSettings.GetValueById("invalid key", "VariableNotThere"));
        }
        [Test]
        public void CanRenameEntry()
        {
            //Arrange
            Assert.IsNotNull(settings.profileSettings);
            settings.activeProfileId = null;
            settings.profileSettings.Reset();
            string entryName = "SomeName";
            string newName = "NewerName";
            string originalPath = "/Assets/Important";
            settings.profileSettings.CreateValue(entryName, originalPath);

            AddressableAssetProfileSettings.ProfileIDData currEntry = null;
            foreach(var entry in settings.profileSettings.profileEntryNames)
            {
                if(entry.Name == entryName)
                {
                    currEntry = entry;
                    break;
                }
            }

            //Act
            currEntry.SetName(newName, settings.profileSettings);

            //Assert
            Assert.AreEqual(currEntry.Name, newName);
        }
        [Test]
        public void CannotRenameEntryToDuplicateName()
        {
            //Arrange
            Assert.IsNotNull(settings.profileSettings);
            settings.activeProfileId = null;
            settings.profileSettings.Reset();
            string entryName = "SomeName";
            string newName = "NewerName";
            string originalPath = "/Assets/Important";
            settings.profileSettings.CreateValue(entryName, originalPath);
            settings.profileSettings.CreateValue(newName, originalPath);

            AddressableAssetProfileSettings.ProfileIDData currEntry = null;
            foreach (var entry in settings.profileSettings.profileEntryNames)
            {
                if (entry.Name == entryName)
                {
                    currEntry = entry;
                    break;
                }
            }

            //Act
            currEntry.SetName(newName, settings.profileSettings);

            //Assert
            Assert.AreNotEqual(currEntry.Name, newName);
        }

    }
}