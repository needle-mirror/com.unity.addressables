using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace UnityEditor.AddressableAssets
{
    public class AddressableAssetSettingsDefaultObject : ScriptableObject
    {
        /// <summary>
        /// Default name for the addressable assets settings
        /// </summary>
        public const string kDefaultConfigAssetName = "AddressableAssetSettings";
        /// <summary>
        /// The default folder for the serialized version of this class.
        /// </summary>
        public const string kDefaultConfigFolder = "Assets/AddressableAssetsData";
        /// <summary>
        /// The name of the default config object
        /// </summary>
        public const string kDefaultConfigObjectName = "com.unity.addressableassets";

        /// <summary>
        /// Default path for addressable asset settings assets.
        /// </summary>
        public static string DefaultAssetPath
        {
            get
            {
                return kDefaultConfigFolder + "/" + kDefaultConfigAssetName + ".asset";
            }
        }

        [SerializeField]
        string m_addressableAssetSettingsGuid;

        AddressableAssetSettings LoadSettingsObject()
        {
            if (string.IsNullOrEmpty(m_addressableAssetSettingsGuid))
            {
                Debug.LogError("Invalid guid for default AddressableAssetSettings object.");
                return null;
            }
            var path = AssetDatabase.GUIDToAssetPath(m_addressableAssetSettingsGuid);
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogErrorFormat("Unable to determine path for default AddressableAssetSettings object with guid {0}.", m_addressableAssetSettingsGuid);
                return null;
            }
            var settings = AssetDatabase.LoadAssetAtPath<AddressableAssetSettings>(path);
            if (settings != null)
                AddressablesAssetPostProcessor.OnPostProcess = settings.OnPostprocessAllAssets;
            return settings;
        }

        void SetSettingsObject(AddressableAssetSettings settings)
        {
            if (settings == null)
            {
                m_addressableAssetSettingsGuid = null;
                return;
            }
            var path = AssetDatabase.GetAssetPath(settings);
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogErrorFormat("Unable to determine path for default AddressableAssetSettings object with guid {0}.", m_addressableAssetSettingsGuid);
                return;
            }
            AddressablesAssetPostProcessor.OnPostProcess = settings.OnPostprocessAllAssets;
            m_addressableAssetSettingsGuid = AssetDatabase.AssetPathToGUID(path);
        }

        static AddressableAssetSettings m_defaultSettingsObject;

        /// <summary>
        /// Used to determine if a default settings asset exists.
        /// </summary>
        public static bool SettingsExists
        {
            get
            {
                AddressableAssetSettingsDefaultObject so;
                if (EditorBuildSettings.TryGetConfigObject(kDefaultConfigObjectName, out so))
                    return !string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(so.m_addressableAssetSettingsGuid));
                return false;
            }
        }

        /// <summary>
        /// Gets the default addressable asset settings object.  This will return null during editor startup if EditorApplication.isUpdating or EditorApplication.isCompiling are true.
        /// </summary>
        public static AddressableAssetSettings Settings
        {
            get
            {
                if (m_defaultSettingsObject == null && !EditorApplication.isUpdating && !EditorApplication.isCompiling)
                {
                    AddressableAssetSettingsDefaultObject so;
                    if (EditorBuildSettings.TryGetConfigObject(kDefaultConfigObjectName, out so))
                    {
                        m_defaultSettingsObject = so.LoadSettingsObject();
                    }
                    else
                    {
                        //legacy support, try to get the old config object and then remove it
                        if (EditorBuildSettings.TryGetConfigObject(kDefaultConfigAssetName, out m_defaultSettingsObject))
                        {
                            EditorBuildSettings.RemoveConfigObject(kDefaultConfigAssetName);
                            so = CreateInstance<AddressableAssetSettingsDefaultObject>();
                            so.SetSettingsObject(m_defaultSettingsObject);
                            AssetDatabase.CreateAsset(so, kDefaultConfigFolder + "/DefaultObject.asset");
                            EditorUtility.SetDirty(so);
                            AssetDatabase.SaveAssets();
                            EditorBuildSettings.AddConfigObject(kDefaultConfigObjectName, so, true);
                        }
                    }
                }
                return m_defaultSettingsObject;
            }
            set
            {
                if (value != null)
                {
                    var path = AssetDatabase.GetAssetPath(value);
                    if (string.IsNullOrEmpty(path))
                    {
                        Debug.LogErrorFormat("AddressableAssetSettings object must be saved to an asset before it can be set as the default.");
                        return;
                    }
                }

                m_defaultSettingsObject = value;
                AddressableAssetSettingsDefaultObject so;
                if (!EditorBuildSettings.TryGetConfigObject(kDefaultConfigObjectName, out so))
                {
                    so = CreateInstance<AddressableAssetSettingsDefaultObject>();
                    AssetDatabase.CreateAsset(so, kDefaultConfigFolder + "/DefaultObject.asset");
                    AssetDatabase.SaveAssets();
                    EditorBuildSettings.AddConfigObject(kDefaultConfigObjectName, so, true);
                }
                so.SetSettingsObject(m_defaultSettingsObject);
                EditorUtility.SetDirty(so);
                AssetDatabase.SaveAssets();
            }
        }

        /// <summary>
        /// Gets the settings object with the option to create a new one if it does not exist.
        /// </summary>
        /// <param name="create">If true and no settings object exists, a new one will be created using the default config folder and asset name.</param>
        /// <returns>The default settings object.</returns>
        public static AddressableAssetSettings GetSettings(bool create)
        {
            if (Settings == null && create)
                Settings = AddressableAssetSettings.Create(kDefaultConfigFolder, kDefaultConfigAssetName, true, true);
            return Settings;
        }

    }
}