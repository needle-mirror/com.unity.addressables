using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.Serialization;

namespace UnityEditor.AddressableAssets
{
	/// <summary>
	/// Class used to get and set the default Addressable Asset settings object.
	/// </summary>
	public class AddressableAssetSettingsDefaultObject : ScriptableObject
	{
		/// <summary>
		/// Default name for the addressable assets settings
		/// </summary>
		public string kDefaultConfigAssetName = "AddressableAssetSettings";
		/// <summary>
		/// The default folder for the serialized version of this class.
		/// </summary>
		public const string kDefaultConfigFolder = "Assets/AddressableAssetsData";
		/// <summary>
		/// The name of the default config object
		/// </summary>
		public const string kDefaultConfigObjectName = "com.unity.addressableassets";

		public static AddressableAssetSettingsDefaultObject Instance { get; set; }

		public List<AddressableAssetSettingsDefaultPair> settingsCollection;

		/// <summary>
		/// Default path for addressable asset settings assets.
		/// </summary>
		public string DefaultAssetPath
		{
			get
			{
				return kDefaultConfigFolder + "/" + kDefaultConfigAssetName + ".asset";
			}
		}
		bool m_LoadingSettingsObject = false;

		internal AddressableAssetSettings LoadSettingsObject()
		{
			//prevent re-entrant stack overflow
			if (m_LoadingSettingsObject)
			{
				Debug.LogWarning("Detected stack overflow when accessing AddressableAssetSettingsDefaultObject.Settings object.");
				return null;
			}
			if (settingsCollection == null || settingsCollection.Count == 0)
			{
				Debug.LogError("No settings objects created, please assign settings to default object");
				return null;
			}

			m_LoadingSettingsObject = true;
			var settingsPair = settingsCollection.Where(s => s.isDefault).FirstOrDefault(); 
			if (settingsPair == null)
			{
				settingsPair = settingsCollection[0];
			}

			if (settingsPair.addressableAssetSettings != null)
				AddressablesAssetPostProcessor.OnPostProcess = settingsPair.addressableAssetSettings.OnPostprocessAllAssets;

			m_LoadingSettingsObject = false;
			return settingsPair.addressableAssetSettings;
		}

		void SetSettingsObject(AddressableAssetSettings settings)
		{
			if (settings == null)
			{
				return;
			}
			var path = AssetDatabase.GetAssetPath(settings);
			AddressablesAssetPostProcessor.OnPostProcess = settings.OnPostprocessAllAssets;
		}

		static AddressableAssetSettings s_DefaultSettingsObject;

		/// <summary>
		/// Used to determine if a default settings asset exists.
		/// </summary>
		public static bool SettingsExists
		{
			get
			{
				AddressableAssetSettingsDefaultObject so;
				if (EditorBuildSettings.TryGetConfigObject(kDefaultConfigObjectName, out so))
					return so.settingsCollection.Count > 0;
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
				//Support for multiple settings setup, we always load current settings object
				AddressableAssetSettingsDefaultObject so;
				if (EditorBuildSettings.TryGetConfigObject(kDefaultConfigObjectName, out so))
				{
					Instance = so;
					s_DefaultSettingsObject = so.LoadSettingsObject();
				}
				return s_DefaultSettingsObject;

				//Unity's default implementation
				/*				if (s_DefaultSettingsObject == null)
								{
									AddressableAssetSettingsDefaultObject so;
									if (EditorBuildSettings.TryGetConfigObject(kDefaultConfigObjectName, out so))
									{
										AddressableAssetSettingsDefaultObject.Instance = so;
										s_DefaultSettingsObject = so.LoadSettingsObject();
									}
									else
									{
										//legacy support, try to get the old config object and then remove it
										 if (EditorBuildSettings.TryGetConfigObject(kDefaultConfigAssetName, out s_DefaultSettingsObject))
										 {
											 EditorBuildSettings.RemoveConfigObject(kDefaultConfigAssetName);
											 so = CreateInstance<AddressableAssetSettingsDefaultObject>();
											 so.SetSettingsObject(s_DefaultSettingsObject);
											 AssetDatabase.CreateAsset(so, kDefaultConfigFolder + "/DefaultObject.asset");
											 EditorUtility.SetDirty(so);
											 AddressableAssetUtility.OpenAssetIfUsingVCIntegration(kDefaultConfigFolder + "/DefaultObject.asset");
											 AssetDatabase.SaveAssets();
											 EditorBuildSettings.AddConfigObject(kDefaultConfigObjectName, so, true);
										 } 
									}
								}
								return s_DefaultSettingsObject; 
				*/
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

				s_DefaultSettingsObject = value;
				AddressableAssetSettingsDefaultObject so;
				if (!EditorBuildSettings.TryGetConfigObject(kDefaultConfigObjectName, out so))
				{
					so = CreateInstance<AddressableAssetSettingsDefaultObject>();
					AssetDatabase.CreateAsset(so, kDefaultConfigFolder + "/DefaultObject.asset");
					AssetDatabase.SaveAssets();
					EditorBuildSettings.AddConfigObject(kDefaultConfigObjectName, so, true);
					AddressableAssetSettingsDefaultObject.Instance = so;
				}
				so.SetSettingsObject(s_DefaultSettingsObject);
				EditorUtility.SetDirty(so);
				AddressableAssetUtility.OpenAssetIfUsingVCIntegration(kDefaultConfigFolder + "/DefaultObject.asset");
				AssetDatabase.SaveAssets();
				AddressableAssetSettingsDefaultObject.Instance = so;
			}
		}

		/// <summary>
		/// Gets the settings object with the option to create a new one if it does not exist.
		/// </summary>
		/// <param name="create">If true and no settings object exists, a new one will be created using the default config folder and asset name.</param>
		/// <returns>The default settings object.</returns>
		public static AddressableAssetSettings GetSettings(bool create)
		{
			/*		if (Settings == null && create)
					{
						Settings = AddressableAssetSettings.Create(kDefaultConfigFolder, kDefaultConfigAssetName, true, true);
					} */
			return Settings;
		}

		public AddressableAssetSettings CreateSettings()
		{
			Settings = AddressableAssetSettings.Create(kDefaultConfigFolder, kDefaultConfigAssetName, true, true);
			return Settings;
		}
	}

	[Serializable]
	public class AddressableAssetSettingsDefaultPair
	{
		public AddressableAssetSettings addressableAssetSettings;
		public bool isDefault;
	}
}