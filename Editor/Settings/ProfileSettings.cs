using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine.ResourceManagement;

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
            /// <summary>
            /// TODO - doc
            /// </summary>
            [Serializable]
            public class ProfileValue
            {
                /// <summary>
                /// TODO - doc
                /// </summary>
                public bool custom
                {
                    get { return m_custom; }
                }
                [SerializeField]
                private bool m_custom = false;
                /// <summary>
                /// TODO - doc
                /// </summary>
                public string value
                {
                    get { return m_value; }
                }
                [SerializeField]
                private string m_value = "";

                /// <summary>
                /// TODO - doc
                /// </summary>
                /// 
                public string GetDisplayName(ProfileSettings ps, string profileId)
                {
                    return m_custom ? "<custom>" : ps.GetVariableName(profileId, m_value);
                }

                public ProfileValue() { }
                /// <summary>
                /// TODO - doc
                /// </summary>
                public ProfileValue(string val, bool custom) { m_value = val; m_custom = custom; }
                /// <summary>
                /// TODO - doc
                /// </summary>
                public string Evaluate(ProfileSettings ps, string profileId)
                {
                    return ps.Evaluate(profileId, m_custom ? m_value : ps.GetValueById(profileId, m_value));
                }
                /// <summary>
                /// TODO - doc
                /// </summary>
                public bool SetValue(ProfileSettings ps, string val, bool cust = false)
                {
                    if (cust == m_custom && val == m_value)
                        return false;
                    m_custom = cust;
                    m_value = val;
                    ps.PostModificationEvent(ModificationEvent.ProfileModified);
                    return true;
                }
            }

            [Serializable]
            class BuildProfile
            {
                [Serializable]
                public class Variable
                {
                    public string m_id;
                    public string m_name;
                    public string m_value;
                    public Variable(string id, string n, string v)
                    {
                        m_id = id;
                        m_name = n;
                        m_value = v;
                    }
                }

                [SerializeField]
                internal string m_parent;
                [SerializeField]
                public string m_id;
                [SerializeField]
                public string m_name;
                [SerializeField]
                public List<Variable> m_values = new List<Variable>();

                public BuildProfile(string profileId, string n, string p)
                {
                    m_id = profileId;
                    m_name = n;
                    m_parent = p;
                }

                private int IndexOfVarId(string variableId)
                {
                    for (int i = 0; i < m_values.Count; i++)
                        if (m_values[i].m_id == variableId)
                            return i;
                    return -1;
                }

                private int IndexOfVarName(string name)
                {
                    for (int i = 0; i < m_values.Count; i++)
                        if (m_values[i].m_name == name)
                            return i;
                    return -1;
                }

                internal string GetValueById(ProfileSettings ps, string variableId)
                {
                    var i = IndexOfVarId(variableId);
                    if (i >= 0)
                        return m_values[i].m_value;

                    return ps.GetValueById(m_parent, variableId);
                }

                internal string GetValueByName(ProfileSettings ps, string variableName)
                {
                    var i = IndexOfVarName(variableName);
                    if (i >= 0)
                        return m_values[i].m_value;

                    return ps.GetValueByName(m_parent, variableName);
                }

                internal void SetValueById(string variableId, string val)
                {
                    var i = IndexOfVarId(variableId);
                    if (i >= 0)
                        m_values[i].m_value = val;
                }

                internal void SetValueByName(ProfileSettings ps, string variableName, string val, bool create)
                {
                    var i = IndexOfVarName(variableName);
                    if (i < 0)
                    {
                        if (create)
                        {
                            m_values.Add(new Variable(ps.GetVariableIdForName(m_id, variableName), variableName, val));
                        }
                    }
                    else
                    {
                        m_values[i].m_value = val;
                    }
                }

                internal void ChangeVariableName(string currName, string newName)
                {
                    foreach (var v in m_values)
                        if (v.m_name == currName)
                            v.m_name = newName;
                }
                internal void ReplaceVariableValueSubString(string searchStr, string replacementStr)
                {
                    foreach (var v in m_values)
                        v.m_value = v.m_value.Replace(searchStr, replacementStr);
                }

                internal string GetVariableName(ProfileSettings ps, string variableId)
                {
                    var index = IndexOfVarId(variableId);
                    if (index >= 0)
                        return m_values[index].m_name;
                    var p = ps.GetProfile(m_parent);
                    if (p == null)
                        return "<undefined>";
                    return p.GetVariableName(ps, variableId);
                }

                internal bool IsValueInheritedByName(string variableName)
                {
                    return IndexOfVarName(variableName) >= 0;
                }

                internal bool IsValueInheritedById(string variableId)
                {
                    return IndexOfVarId(variableId) >= 0;
                }

                internal string GetVariableId(string variableName)
                {
                    var index = IndexOfVarName(variableName);
                    if (index < 0)
                        return null;
                    return m_values[index].m_id;
                }
            }

            private string GetVariableIdForName(string profileId, string variableName)
            {
                var p = GetProfile(profileId);
                while (p != null)
                {
                    string varId = p.GetVariableId(variableName);
                    if (!string.IsNullOrEmpty(varId))
                        return varId;
                    p = GetProfile(p.m_parent);
                }
                return GUID.Generate().ToString();
            }

            internal string GetVariableIdFromName(string variableName)
            {
                foreach (var p in m_profiles)
                    foreach (var v in p.m_values)
                        if (v.m_name == variableName)
                            return v.m_id;
                return string.Empty;
            }

            internal string GetVariableNameFromId(string variableId)
            {
                foreach (var p in m_profiles)
                    foreach (var v in p.m_values)
                        if (v.m_id == variableId)
                            return v.m_name;
                return string.Empty;
            }

            internal void OnAfterDeserialize(AddressableAssetSettings settings)
            {
                m_Settings = settings;
            }

            internal int GetIndexOfProfile(string profileId)
            {
                return m_profiles.FindIndex(p => p.m_id == profileId);
            }

            [NonSerialized]
            AddressableAssetSettings m_Settings;
            [SerializeField]
            List<BuildProfile> m_profiles = new List<BuildProfile>();

            /// <summary>
            /// TODO - doc
            /// </summary>
            public List<string> profileNames
            {
                get
                {
                    CreateDefaultProfile();
                    return m_profiles.Select(p => p.m_name).ToList();
                }
            }

            public string Reset()
            {
                m_profiles = new List<BuildProfile>();
                return CreateDefaultProfile();
            }

            public ProfileValue CreateProfileValue(string initialValue, bool custom = false)
            {
                return new ProfileValue(initialValue, custom);
            }

            internal string DefaultProfileId { get { return m_profiles[0].m_id; } }

            public string Evaluate(string profileId, string varString)
            {
                Func<string, string> getVal = (s) =>
                {
                    string v = GetValueByName(profileId, s);
                    if (string.IsNullOrEmpty(v))
                        v = ResourceManagerConfig.GetGlobalVar(s);
                    return v;
                };
                return ResourceManagerConfig.ExpandWithVariables(varString, '[', ']', getVal);
            }

            internal string CreateDefaultProfile()
            {
                if (m_profiles.Count == 0)
                {
                    var defaultId = AddProfile("Default", null);
                    SetValueByName(defaultId, "BuildTarget", "[UnityEditor.EditorUserBuildSettings.activeBuildTarget]");
                    SetValueByName(defaultId, "LocalBuildPath", "Assets/StreamingAssets");
                    SetValueByName(defaultId, "LocalLoadPrefix", "file://{UnityEngine.Application.streamingAssetsPath}");
                    SetValueByName(defaultId, "RemoteBuildPath", "ServerData/[BuildTarget]");
                    SetValueByName(defaultId, "RemoteLoadPrefix", "http://localhost/[BuildTarget]");
                    SetValueByName(defaultId, "version", "1");

                    var devId = AddProfile("Dev", defaultId);
                    SetValueByName(devId, "RemoteBuildPath", "DevServerData/[BuildTarget]");
                    SetValueByName(devId, "RemoteLoadPrefix", "http://devserver/[BuildTarget]");

                    var prodId = AddProfile("Production", defaultId);
                    SetValueByName(prodId, "RemoteBuildPath", "ProductionServerData/[BuildTarget]");
                    SetValueByName(prodId, "RemoteLoadPrefix", "http://productionserver/[BuildTarget]");
                }
                return m_profiles[0].m_id;
            }

            /// <summary>
            /// TODO - doc
            /// </summary>
            public HashSet<string> GetAllVariableNames(string profileId)
            {
                HashSet<string> names = new HashSet<string>();
                var p = GetProfile(profileId);
                while (p != null)
                {
                    foreach (var v in p.m_values)
                        names.Add(v.m_name);
                    p = GetProfile(p.m_parent);
                }
                return names;
            }

            /// <summary>
            /// TODO - doc
            /// </summary>
            public HashSet<string> GetAllVariableNames()
            {
                HashSet<string> names = new HashSet<string>();
                foreach (var p in m_profiles)
                    foreach (var v in p.m_values)
                        names.Add(v.m_name);
                return names;
            }

            public HashSet<string> GetAllVariableIds()
            {
                HashSet<string> ids = new HashSet<string>();
                foreach (var p in m_profiles)
                    foreach (var v in p.m_values)
                        ids.Add(v.m_id);
                return ids;
            }

            internal string GetProfileAtIndex(int profileIndex)
            {
                return m_profiles[profileIndex].m_id;
            }

            void PostModificationEvent(ModificationEvent e)
            {
                if (m_Settings != null)
                    m_Settings.PostModificationEvent(e, this);
            }

            internal bool ValidateNewVariableName(string name)
            {
                foreach (var p in m_profiles)
                    foreach (var v in p.m_values)
                        if (name == v.m_name)
                            return false;
                return !string.IsNullOrEmpty(name) && !name.Any(c => { return c == '[' || c == ']' || c == '{' || c == '}'; });
            }

            internal string RenameEntry(string currName, string newName)
            {
                if (!ValidateNewVariableName(newName))
                    return currName;
                foreach (var p in m_profiles)
                    p.ChangeVariableName(currName, newName);
                var currRefStr = "[" + currName + "]";
                var newRefStr = "[" + newName + "]";
                foreach (var p in m_profiles)
                    p.ReplaceVariableValueSubString(currRefStr, newRefStr);
                return newName;
            }

            /// <summary>
            /// TODO - doc
            /// </summary>
            public string AddProfile(string name, string parent)
            {
                if (GetProfileByName(name) != null)
                    return string.Empty;
                var id = GUID.Generate().ToString();
                m_profiles.Add(new BuildProfile(id, name, parent));
                PostModificationEvent(ModificationEvent.ProfileAdded);
                return id;
            }

            /// <summary>
            /// TODO - doc
            /// </summary>
            public void RemoveProfile(string profileId)
            {
                m_profiles.RemoveAll(p => p.m_id == profileId);
                m_profiles.ForEach(p => { if (p.m_parent == profileId) p.m_parent = null; });
                PostModificationEvent(ModificationEvent.ProfileRemoved);
            }

            private BuildProfile GetProfileByName(string profileName)
            {
                return m_profiles.Find(p => p.m_name == profileName);
            }


            private BuildProfile GetProfile(string profileId)
            {
                return m_profiles.Find(p => p.m_id == profileId);
            }

            private string GetVariableName(string profileId, string variableId)
            {
                var p = GetProfile(profileId);
                if (p == null)
                    return "<undefined>";
                return p.GetVariableName(this, variableId);
            }


            /// <summary>
            /// TODO - doc
            /// </summary>
            public void SetValueById(string profileId, string variableId, string val)
            {
                var p = GetProfile(profileId);
                if (p == null)
                    return;
                p.SetValueById(variableId, val);
                PostModificationEvent(ModificationEvent.ProfileModified);
            }
            /// <summary>
            /// TODO - doc
            /// </summary>
            public void SetValueByName(string profileId, string variableName, string val, bool create = true)
            {
                var p = GetProfile(profileId);
                if (p == null)
                    return;
                p.SetValueByName(this, variableName, val, create);
                PostModificationEvent(ModificationEvent.ProfileModified);
            }

            /// <summary>
            /// TODO - doc
            /// </summary>
            public string GetValueById(string profileId, string varId)
            {
                BuildProfile profile = GetProfile(profileId);
                return profile == null ? string.Empty : profile.GetValueById(this, varId);
            }

            public string GetValueByName(string profileId, string varName)
            {
                return GetValueById(profileId, GetVariableIdForName(profileId, varName));
            }

            internal bool IsValueInheritedById(string profileId, string variableId)
            {
                var p = GetProfile(profileId);
                if (p == null)
                    return false;
                return p.IsValueInheritedById(variableId);
            }

            internal string GetProfileName(string profileId)
            {
                var p = GetProfile(profileId);
                if (p == null)
                    return string.Empty;
                return p.m_name;
            }
        }
    }
}
