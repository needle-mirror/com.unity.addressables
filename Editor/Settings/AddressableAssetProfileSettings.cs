using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.ResourceManagement;
using UnityEngine.AddressableAssets;

namespace UnityEditor.AddressableAssets
{
    /// <summary>
    /// TODO - doc
    /// </summary>
    [Serializable]
    public class AddressableAssetProfileSettings
    {
        [Serializable]
        internal class BuildProfile
        {
            [Serializable]
            public class ProfileEntry
            {

                public string m_id;
                public string m_value;
                public ProfileEntry() { }
                public ProfileEntry(string id, string v)
                {
                    m_id = id;
                    m_value = v;
                }
                public ProfileEntry(ProfileEntry copy)
                {
                    m_id = copy.m_id;
                    m_value = copy.m_value;
                }
            }
            [NonSerialized]
            AddressableAssetProfileSettings m_profileParent = null;

            [SerializeField]
            internal string m_inheritedParent;
            [SerializeField]
            string m_id;
            public string id
            {
                get { return m_id; }
                set { m_id = value; }
            }
            [SerializeField]
            string m_profileName;
            public string profileName
            {
                get { return m_profileName; }
                set { m_profileName = value; }
            }
            [SerializeField]
            private List<ProfileEntry> m_values = new List<ProfileEntry>();
            public List<ProfileEntry> values
            {
                get { return m_values; }
                set { m_values = value; }
            }

            public BuildProfile(string name, BuildProfile copyFrom, AddressableAssetProfileSettings ps)
            {
                m_inheritedParent = null;
                id = GUID.Generate().ToString();
                profileName = name;
                values.Clear();
                m_profileParent = ps;

                if (copyFrom != null)
                {
                    foreach (var v in copyFrom.values)
                        values.Add(new ProfileEntry(v));
                    m_inheritedParent = copyFrom.m_inheritedParent;
                }
            }
            internal void OnAfterDeserialize(AddressableAssetProfileSettings ps)
            {
                m_profileParent = ps;
            }

            private int IndexOfVarId(string variableId)
            {
                if (string.IsNullOrEmpty(variableId))
                    return -1;

                for (int i = 0; i < values.Count; i++)
                    if (values[i].m_id == variableId)
                        return i;
                return -1;
            }

            private int IndexOfVarName(string name)
            {
                if (m_profileParent == null)
                    return -1;

                var id = m_profileParent.GetVariableID(name);
                if (string.IsNullOrEmpty(id))
                    return -1;

                for (int i = 0; i < values.Count; i++)
                    if (values[i].m_id == id)
                        return i;
                return -1;
            }

            internal string GetValueById(string variableId)
            {
                var i = IndexOfVarId(variableId);
                if (i >= 0)
                    return values[i].m_value;


                if (m_profileParent == null)
                    return null;

                return m_profileParent.GetValueById(m_inheritedParent, variableId);
            }

            internal void SetValueById(string variableId, string val)
            {
                var i = IndexOfVarId(variableId);
                if (i >= 0)
                    values[i].m_value = val;
            }

            internal void ReplaceVariableValueSubString(string searchStr, string replacementStr)
            {
                foreach (var v in values)
                    v.m_value = v.m_value.Replace(searchStr, replacementStr);
            }


            internal bool IsValueInheritedByName(string variableName)
            {
                return IndexOfVarName(variableName) >= 0;
            }

            internal bool IsValueInheritedById(string variableId)
            {
                return IndexOfVarId(variableId) >= 0;
            }
        }

        internal void OnAfterDeserialize(AddressableAssetSettings settings)
        {
            m_Settings = settings;
            foreach (var prof in m_profiles)
            {
                prof.OnAfterDeserialize(this);
            }
        }

        [NonSerialized]
        AddressableAssetSettings m_Settings;
        [SerializeField]
        List<BuildProfile> m_profiles = new List<BuildProfile>();
        internal List<BuildProfile> profiles { get { return m_profiles; } }

        [Serializable]
        internal class ProfileIDData
        {
            [SerializeField]
            string m_id;
            public string Id { get { return m_id; } }

            [SerializeField]
            string m_name;
            public string Name { get { return m_name; } set { m_name = value; } }

            [SerializeField]
            bool m_inlineUsage;
            public bool InlineUsage { get { return m_inlineUsage; } }
            public ProfileIDData() { }
            public ProfileIDData(string entryId, string entryName, bool inline = false)
            {
                m_id = entryId;
                m_name = entryName;
                m_inlineUsage = inline;
            }
            public string Evaluate(AddressableAssetProfileSettings ps, string profileId)
            {
                if (InlineUsage)
                    return ps.EvaluateString(profileId, Id);

                return Evaluate(ps, profileId, Id);
            }
            public static string Evaluate(AddressableAssetProfileSettings ps, string profileId, string idString)
            {
                string baseValue = ps.GetValueById(profileId, idString);
                return ps.EvaluateString(profileId, baseValue);
            }
        }
        [SerializeField]
        List<ProfileIDData> m_profileEntryNames = new List<ProfileIDData>();
        internal List<ProfileIDData> profileEntryNames
        {
            get
            {
                if (m_profileEntryNames.Count == 0)
                    m_profileEntryNames.Add(new ProfileIDData(GUID.Generate().ToString(), k_customEntryString, true));
                return m_profileEntryNames;
            }
        }
        internal const string k_customEntryString = "<custom>";

        internal ProfileIDData GetProfileDataById(string id)
        {
            foreach (var data in profileEntryNames)
            {
                if (id == data.Id)
                    return data;
            }
            return null;
        }

        internal ProfileIDData GetProfileDataByName(string name)
        {
            foreach (var data in profileEntryNames)
            {
                if (name == data.Name)
                    return data;
            }
            return null;
        }

        public string Reset()
        {
            m_profiles = new List<BuildProfile>();
            return CreateDefaultProfile();
        }

        public string EvaluateString(string profileId, string varString)
        {
            Func<string, string> getVal = (s) =>
            {
                string v = GetValueByName(profileId, s);
                if (string.IsNullOrEmpty(v))
                    v = AAConfig.GetGlobalVar(s);
                return v;
            };
            return AAConfig.ExpandWithVariables(varString, '[', ']', getVal);
        }

        internal void Validate(AddressableAssetSettings addressableAssetSettings)
        {
            CreateDefaultProfile();
        }

        internal const string k_rootProfileName = "Default";
        internal string CreateDefaultProfile()
        {
            if (!ValidateProfiles())
            {
                m_profileEntryNames.Clear();
                m_profiles.Clear();

                AddProfile(k_rootProfileName, null);
                CreateValue("ContentVersion", "1");
                CreateValue("BuildTarget", "[UnityEditor.EditorUserBuildSettings.activeBuildTarget]");
                CreateValue("LocalBuildPath", Addressables.BuildPath + "/[BuildTarget]");
                CreateValue("LocalLoadPath", "{UnityEngine.AddressableAssets.Addressables.RuntimePath}/[BuildTarget]");
                CreateValue("RemoteBuildPath", "ServerData/[BuildTarget]");
                CreateValue("RemoteLoadPath", "http://localhost/[BuildTarget]");
            }
            return GetDefaultProfileID();
        }
        internal string GetDefaultProfileID()
        {
            var def = GetDefaultProfile();
            if (def != null)
                return def.id;
            return null;
        }
        private BuildProfile GetDefaultProfile()
        {
            var profile = GetProfileByName(k_rootProfileName);
            if (profile == null && m_profiles.Count > 0)
                profile = m_profiles[0];
            return profile;
        }

        internal bool ValidateProfiles()
        {
            if (m_profiles.Count == 0)
                return false;
            var root = m_profiles[0];
            if (root == null || root.profileName != k_rootProfileName)
                root = GetProfileByName(k_rootProfileName);

            if (root == null)
                return false;

            foreach (var i in profileEntryNames)
                if (string.IsNullOrEmpty(i.Id) || string.IsNullOrEmpty(i.Name))
                    return false;

            var rootValueCount = root.values.Count;
            foreach (var profile in m_profiles)
            {
                if (profile.profileName == k_rootProfileName)
                    continue;
                if (string.IsNullOrEmpty(profile.profileName))
                    return false;

                if (profile.values.Count != rootValueCount)
                    return false;

                for (int i = 0; i < rootValueCount; i++)
                {
                    if (root.values[i].m_id != profile.values[i].m_id)
                        return false;
                }
            }

            return true;
        }

        /// <summary>
        /// TODO - doc
        /// </summary>
        public HashSet<string> GetAllVariableNames()
        {
            HashSet<string> names = new HashSet<string>();
            foreach (var entry in profileEntryNames)
            {
                names.Add(entry.Name);
            }
            return names;
        }

        public string[] GetVariableNames()
        {
            HashSet<string> names = new HashSet<string>();
            foreach (var entry in profileEntryNames)
            {
                names.Add(entry.Name);
            }
            var list = names.ToList();
            list.Sort();
            return list.ToArray();
        }

        /// <summary>
        /// TODO - doc
        /// </summary>
        public List<string> GetAllProfileNames()
        {
            CreateDefaultProfile();
            List<string> result = new List<string>();
            foreach (var p in m_profiles)
            {
                result.Add(p.profileName);
            }
            return result;

        }
        public string GetProfileName(string profileID)
        {
            foreach (var p in m_profiles)
            {
                if (p.id == profileID)
                    return p.profileName;
            }
            return "";
        }
        public string GetProfileId(string profileName)
        {
            foreach (var p in m_profiles)
            {
                if (p.profileName == profileName)
                    return p.id;
            }
            return null;
        }
        public HashSet<string> GetAllVariableIds()
        {
            HashSet<string> ids = new HashSet<string>();
            foreach (var v in profileEntryNames)
                ids.Add(v.Id);
            return ids;
        }

        void PostModificationEvent(AddressableAssetSettings.ModificationEvent e)
        {
            if (m_Settings != null)
                m_Settings.PostModificationEvent(e, this);
        }

        internal bool ValidateNewVariableName(string name)
        {
            foreach (var idPair in profileEntryNames)
                if (idPair.Name == name)
                    return false;
            return !string.IsNullOrEmpty(name) && !name.Any(c => { return c == '[' || c == ']' || c == '{' || c == '}'; });
        }

        internal string RenameEntry(string currName, string newName)
        {
            if (!ValidateNewVariableName(newName))
                return currName;

            foreach (var idPair in profileEntryNames)
                if (idPair.Name == currName)
                    idPair.Name = newName;

            var currRefStr = "[" + currName + "]";
            var newRefStr = "[" + newName + "]";
            foreach (var p in m_profiles)
                p.ReplaceVariableValueSubString(currRefStr, newRefStr);
            return newName;
        }

        /// <summary>
        /// TODO - doc
        /// </summary>
        public string AddProfile(string name, string copyFromId)
        {
            var existingProfile = GetProfileByName(name);
            if (existingProfile != null)
                return existingProfile.id;
            var copyRoot = GetProfile(copyFromId);
            if (copyRoot == null && m_profiles.Count > 0)
                copyRoot = GetDefaultProfile();
            var prof = new BuildProfile(name, copyRoot, this);
            m_profiles.Add(prof);
            PostModificationEvent(AddressableAssetSettings.ModificationEvent.ProfileAdded);
            return prof.id;
        }

        /// <summary>
        /// TODO - doc
        /// </summary>
        public void RemoveProfile(string profileId)
        {
            m_profiles.RemoveAll(p => p.id == profileId);
            m_profiles.ForEach(p => { if (p.m_inheritedParent == profileId) p.m_inheritedParent = null; });
            PostModificationEvent(AddressableAssetSettings.ModificationEvent.ProfileRemoved);
        }

        private BuildProfile GetProfileByName(string profileName)
        {
            return m_profiles.Find(p => p.profileName == profileName);
        }


        private BuildProfile GetProfile(string profileId)
        {
            return m_profiles.Find(p => p.id == profileId);
        }

        private string GetVariableName(string variableId)
        {
            foreach (var idPair in profileEntryNames)
            {
                if (idPair.Id == variableId)
                    return idPair.Name;
            }
            return null;
        }
        //public List<ProfileIDData> GetVairablesFromUsage(ProfileEntryUsage usage)
        //{
        //    var result = new List<ProfileIDData>();
        //    foreach(var idPair in profileEntryNames)
        //    {
        //        if (idPair.usage == usage)
        //            result.Add(idPair);
        //    }
        //    return result;
        //}
        private string GetVariableID(string variableName)
        {
            foreach (var idPair in profileEntryNames)
            {
                if (idPair.Name == variableName)
                    return idPair.Id;
            }
            return null;
        }

        /// <summary>
        /// TODO - doc
        /// </summary>
        public void SetValue(string profileId, string variableName, string val)
        {
            var profile = GetProfile(profileId);
            if (profile == null)
            {
                Debug.LogError("setting variable " + variableName + " failed because profile " + profileId + " does not exist.");
                return;
            }

            var id = GetVariableID(variableName);
            if (string.IsNullOrEmpty(id))
            {
                Debug.LogError("setting variable " + variableName + " failed because variable does not yet exist. Call CreateValue() first.");
                return;
            }

            profile.SetValueById(id, val);
            PostModificationEvent(AddressableAssetSettings.ModificationEvent.ProfileModified);
        }
        public string CreateValue(string variableName, string defaultValue, bool inline = false)
        {
            if (m_profiles.Count == 0)
            {
                Debug.LogError("Attempting to add a profile variable in Addressables, but there are no profiles yet.");
            }

            var id = GetVariableID(variableName);
            if (string.IsNullOrEmpty(id))
            {
                id = GUID.Generate().ToString();
                profileEntryNames.Add(new ProfileIDData(id, variableName, inline));

                foreach (var pro in m_profiles)
                {
                    pro.values.Add(new BuildProfile.ProfileEntry(id, defaultValue));
                }
            }
            return id;
        }

        public void RemoveValue(string variableID)
        {
            foreach (var pro in m_profiles)
            {
                pro.values.RemoveAll(x => x.m_id == variableID);
            }
            m_profileEntryNames.RemoveAll(x => x.Id == variableID);
        }

        /// <summary>
        /// TODO - doc
        /// </summary>
        public string GetValueById(string profileId, string varId)
        {
            BuildProfile profile = GetProfile(profileId);
            return profile == null ? varId : profile.GetValueById(varId);
        }

        public string GetValueByName(string profileId, string varName)
        {
            return GetValueById(profileId, GetVariableID(varName));
        }


        internal bool IsValueInheritedById(string profileId, string variableId)
        {
            var p = GetProfile(profileId);
            if (p == null)
                return false;
            return p.IsValueInheritedById(variableId);
        }
    }
}
