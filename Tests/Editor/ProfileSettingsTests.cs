using NUnit.Framework;

namespace UnityEditor.AddressableAssets.Tests
{
    public class ProfileSettingsTests : AddressableAssetTestBase
    {
        [Test]
        public void AddRemoveProfile()
        {
            //Arrange
            Assert.IsNotNull(m_settings.profileSettings);
            m_settings.activeProfileId = null;
            var mainId = m_settings.profileSettings.Reset();

            //Act 
            var secondId = m_settings.profileSettings.AddProfile("TestProfile", mainId);

            //Assert
            bool foundIt = false;
            foreach (var prof in m_settings.profileSettings.profiles)
            {
                if (prof.profileName == "TestProfile")
                    foundIt = true;
            }
            Assert.IsTrue(foundIt);
            Assert.IsNotEmpty(secondId);

            //Act again
            m_settings.profileSettings.RemoveProfile(secondId);

            //Assert again
            foundIt = false;
            foreach (var prof in m_settings.profileSettings.profiles)
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
            Assert.IsNotNull(m_settings.profileSettings);
            m_settings.activeProfileId = null;
            var mainId = m_settings.profileSettings.Reset();
            var secondId = m_settings.profileSettings.AddProfile("TestProfile", mainId);

            //Act
            string path = "/Assets/Important";
            m_settings.profileSettings.CreateValue("SomePath", path);

            //Assert
            Assert.AreEqual(path, m_settings.profileSettings.GetValueByName(mainId, "SomePath"));
            Assert.AreEqual(path, m_settings.profileSettings.GetValueByName(secondId, "SomePath"));
        }
        [Test]
        public void SetValueOnlySetsDesiredProfile()
        {
            //Arrange
            Assert.IsNotNull(m_settings.profileSettings);
            m_settings.activeProfileId = null;
            var mainId = m_settings.profileSettings.Reset();
            string originalPath = "/Assets/Important";
            m_settings.profileSettings.CreateValue("SomePath", originalPath);
            var secondId = m_settings.profileSettings.AddProfile("TestProfile", mainId);

            //Act
            string newPath = "/Assets/LessImportant";
            m_settings.profileSettings.SetValue(secondId, "SomePath", newPath);

            //Assert
            Assert.AreEqual(originalPath, m_settings.profileSettings.GetValueByName(mainId, "SomePath"));
            Assert.AreEqual(newPath, m_settings.profileSettings.GetValueByName(secondId, "SomePath"));
        }
        [Test]
        public void CanGetValueById()
        {
            //Arrange
            Assert.IsNotNull(m_settings.profileSettings);
            m_settings.activeProfileId = null;
            var mainId = m_settings.profileSettings.Reset();
            string originalPath = "/Assets/Important";
            m_settings.profileSettings.CreateValue("SomePath", originalPath);

            //Act
            string varId = null;
            foreach (var variable in m_settings.profileSettings.profileEntryNames)
            {
                if (variable.Name == "SomePath")
                {
                    varId = variable.Id;
                    break;
                }
            }

            //Assert
            Assert.AreEqual(originalPath, m_settings.profileSettings.GetValueById(mainId, varId));
        }
        [Test]
        public void EvaluatingUnknownIdReturnsIdAsResult()
        {
            //Arrange
            Assert.IsNotNull(m_settings.profileSettings);
            m_settings.activeProfileId = null;
            m_settings.profileSettings.Reset();

            //Act
            string badIdName = "BadIdName";


            //Assert
            Assert.AreEqual(badIdName, AddressableAssetProfileSettings.ProfileIDData.Evaluate(m_settings.profileSettings, m_settings.activeProfileId, badIdName));

        }
        [Test]
        public void MissingVariablesArePassThrough()
        {
            //Arrange
            Assert.IsNotNull(m_settings.profileSettings);
            m_settings.activeProfileId = null;

            //Act
            m_settings.profileSettings.Reset();

            //Assert
            Assert.AreEqual("VariableNotThere", m_settings.profileSettings.GetValueById("invalid key", "VariableNotThere"));
        }
        [Test]
        public void CanRenameEntry()
        {
            //Arrange
            Assert.IsNotNull(m_settings.profileSettings);
            m_settings.activeProfileId = null;
            m_settings.profileSettings.Reset();
            string entryName = "SomeName";
            string newName = "NewerName";
            string originalPath = "/Assets/Important";
            m_settings.profileSettings.CreateValue(entryName, originalPath);

            AddressableAssetProfileSettings.ProfileIDData currEntry = null;
            foreach(var entry in m_settings.profileSettings.profileEntryNames)
            {
                if(entry.Name == entryName)
                {
                    currEntry = entry;
                    break;
                }
            }

            //Act
            currEntry.SetName(newName, m_settings.profileSettings);

            //Assert
            Assert.AreEqual(currEntry.Name, newName);
        }
        [Test]
        public void CannotRenameEntryToDuplicateName()
        {
            //Arrange
            Assert.IsNotNull(m_settings.profileSettings);
            m_settings.activeProfileId = null;
            m_settings.profileSettings.Reset();
            string entryName = "SomeName";
            string newName = "NewerName";
            string originalPath = "/Assets/Important";
            m_settings.profileSettings.CreateValue(entryName, originalPath);
            m_settings.profileSettings.CreateValue(newName, originalPath);

            AddressableAssetProfileSettings.ProfileIDData currEntry = null;
            foreach (var entry in m_settings.profileSettings.profileEntryNames)
            {
                if (entry.Name == entryName)
                {
                    currEntry = entry;
                    break;
                }
            }

            //Act
            currEntry.SetName(newName, m_settings.profileSettings);

            //Assert
            Assert.AreNotEqual(currEntry.Name, newName);
        }

    }
}