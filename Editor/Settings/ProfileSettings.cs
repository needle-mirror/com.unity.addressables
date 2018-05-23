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
    public partial class AddressableAssetSettings
    {
        /// <summary>
        /// TODO - doc
        /// </summary>
        [Serializable]
        public class ProfileSettings
        {
            [Serializable]
            internal class BuildProfile
            {
                [Serializable]
                public class ProfileEntry
                {

                    public string m_id;
                    public string m_value;
                    public ProfileEntry(string id, string v)
                    {
                        m_id = id;
                        m_value = v;
                    }
                }
                [NonSerialized]
                ProfileSettings m_profileParent = null;

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
                public List<ProfileEntry> values {
                    get { return m_values; }
                    set { m_values = value; }
                }

                public BuildProfile(string name, BuildProfile copyFrom, ProfileSettings ps)
                {
                    m_inheritedParent = null;
                    id = GUID.Generate().ToString();
                    profileName = name;
                    values.Clear();
                    m_profileParent = ps;

                    if (copyFrom != null)
                    {
                        values.AddRange(copyFrom.values);
                        m_inheritedParent = copyFrom.m_inheritedParent;
                    }
                        
                }
                internal void OnAfterDeserialize(ProfileSettings ps)
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
                foreach(var prof in m_profiles)
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
            public class ProfileIDData
            {
                public string id;
                public string name;
                public bool inlineUsage;
                public ProfileIDData(string entryId, string entryName, bool inline = false)
                {
                    id = entryId;
                    name = entryName;
                    inlineUsage = inline;
                }
                public string Evaluate(ProfileSettings ps, string profileId)
                {
                    if (inlineUsage)
                        return ps.EvaluateString(profileId, id);

                    return Evaluate(ps, profileId, id);
                }
                public static string Evaluate(ProfileSettings ps, string profileId, string idString)
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

            public ProfileIDData GetProfileDataById(string id)
            {
                foreach(var data in profileEntryNames)
                {
                    if (id == data.id)
                        return data;
                }
                return null;
            }
            public static string TryGetProfileID(string dataName, AddressableAssetSettings settings = null)
            {
                var result = dataName;
                if(settings == null)
                    settings = AddressableAssetSettings.GetDefault(false, false);
                if(settings != null)
                {
                    var data = settings.profileSettings.GetProfileDataByName(dataName);
                    if (data != null)
                        result = data.id;
                }
                return result;
            }
            public ProfileIDData GetProfileDataByName(string name)
            {
                foreach (var data in profileEntryNames)
                {
                    if (name == data.name)
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

            internal const string k_rootProfileName = "Default";
            internal string CreateDefaultProfile()
            {   
                if (!ValidateProfiles())
                {
                    m_profileEntryNames.Clear();
                    m_profiles.Clear();
                    
                    AddProfile(k_rootProfileName, null);
                    CreateValue("LocalBuildPath", "Assets/StreamingAssets");
                    CreateValue("LocalLoadPrefix", "file://{UnityEngine.Application.streamingAssetsPath}");
                    CreateValue("RemoteBuildPath", "ServerData/[UnityEditor.EditorUserBuildSettings.activeBuildTarget]");
                    CreateValue("RemoteLoadPrefix", "http://localhost/[UnityEditor.EditorUserBuildSettings.activeBuildTarget]");

                    CreateValue("ContentVersion", "1");
                    CreateValue("BuildTarget", "[UnityEditor.EditorUserBuildSettings.activeBuildTarget]");

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
                if(root == null || root.profileName != k_rootProfileName)
                    root = GetProfileByName(k_rootProfileName);

                if (root == null)
                    return false;

                var rootValueCount = root.values.Count;
                foreach(var profile in m_profiles)
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
                    names.Add(entry.name);
                }
                return names;
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
                foreach(var p in m_profiles)
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
                    ids.Add(v.id);
                return ids;
            }

            void PostModificationEvent(ModificationEvent e)
            {
                if (m_Settings != null)
                    m_Settings.PostModificationEvent(e, this);
            }

            internal bool ValidateNewVariableName(string name)
            {
                foreach (var idPair in profileEntryNames)
                    if (idPair.name == name)
                        return false;
                return !string.IsNullOrEmpty(name) && !name.Any(c => { return c == '[' || c == ']' || c == '{' || c == '}'; });
            }

            internal string RenameEntry(string currName, string newName)
            {
                if (!ValidateNewVariableName(newName))
                    return currName;

                foreach (var idPair in profileEntryNames)
                    if (idPair.name == currName)
                        idPair.name = newName;

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
                PostModificationEvent(ModificationEvent.ProfileAdded);
                return prof.id;
            }

            /// <summary>
            /// TODO - doc
            /// </summary>
            public void RemoveProfile(string profileId)
            {
                m_profiles.RemoveAll(p => p.id == profileId);
                m_profiles.ForEach(p => { if (p.m_inheritedParent == profileId) p.m_inheritedParent = null; });
                PostModificationEvent(ModificationEvent.ProfileRemoved);
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
                foreach(var idPair in profileEntryNames)
                {
                    if (idPair.id == variableId)
                        return idPair.name;
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
                    if (idPair.name == variableName)
                        return idPair.id;
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
                if(string.IsNullOrEmpty(id))
                {
                    Debug.LogError("setting variable " + variableName + " failed because variable does not yet exist. Call CreateValue() first.");
                    return;
                }
                
                profile.SetValueById(id, val);
                PostModificationEvent(ModificationEvent.ProfileModified);
            }
            public void CreateValue(string variableName, string defaultValue, bool inline=false)
            {
                if(m_profiles.Count == 0)
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
            }
            public void RemoveValue(string variableID)
            {
                foreach(var pro in m_profiles)
                {
                    pro.values.RemoveAll(x => x.m_id == variableID );
                }
                m_profileEntryNames.RemoveAll(x => x.id == variableID);
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
}
