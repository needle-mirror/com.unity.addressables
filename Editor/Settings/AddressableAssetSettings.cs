using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEditor.Build.Pipeline.Utilities;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement;
using UnityEngine.Serialization;
using Object = UnityEngine.Object;

[assembly: InternalsVisibleTo("Unity.Addressables.Editor.Tests")]

namespace UnityEditor.AddressableAssets
{
    /// <summary>
    /// Contains editor data for the addressables system.
    /// </summary>
    public class AddressableAssetSettings : ScriptableObject
    {
        [InitializeOnLoadMethod]
        static void RegisterWithAssetPostProcessor()
        {
            //if the Library folder has been deleted, this will be null and it will have to be set on the first access of the settings object
            if(AddressableAssetSettingsDefaultObject.Settings != null)
                AddressablesAssetPostProcessor.OnPostProcess = AddressableAssetSettingsDefaultObject.Settings.OnPostprocessAllAssets;
        }
        /// <summary>
        /// Default name of a newly created group.
        /// </summary>
        public const string kNewGroupName = "New Group";
        /// <summary>
        /// Default name of local build path.
        /// </summary>
        public const string kLocalBuildPath = "LocalBuildPath";
        /// <summary>
        /// Default name of local load path.
        /// </summary>
        public const string kLocalLoadPath = "LocalLoadPath";
        /// <summary>
        /// Default name of remote build path.
        /// </summary>
        public const string kRemoteBuildPath = "RemoteBuildPath";
        /// <summary>
        /// Default name of remote load path.
        /// </summary>
        public const string kRemoteLoadPath = "RemoteLoadPath";

        /// <summary>
        /// Enumeration of different event types that are generated.
        /// </summary>
        public enum ModificationEvent
        {
            GroupAdded,
            GroupRemoved,
            GroupRenamed,
            GroupSchemaAdded,
            GroupSchemaRemoved,
            GroupSchemaModified,
            GroupSchemaTemplateAdded,
            GroupSchemaTemplateRemoved,
            EntryCreated,
            EntryAdded,
            EntryMoved,
            EntryRemoved,
            LabelAdded,
            LabelRemoved,
            ProfileAdded,
            ProfileRemoved,
            ProfileModified,
            ActiveProfileSet,
            EntryModified,
            BuildSettingsChanged,
            ActiveBuildScriptChanged,
            DataBuilderAdded,
            DataBuilderRemoved,
            InitializationObjectAdded,
            InitializationObjectRemoved,
            ActivePlayModeScriptChanged,
            BatchModification, // <-- posted object will be null.
            HostingServicesManagerModified
        }

        /// <summary>
        /// The path of the settings asset.
        /// </summary>
        public string AssetPath
        {
            get
            {
                string guid;
                long localId;
                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(this, out guid, out localId))
                    return AddressableAssetSettingsDefaultObject.DefaultAssetPath;
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(assetPath))
                    return AddressableAssetSettingsDefaultObject.DefaultAssetPath;
                return assetPath;
            }
        }

        /// <summary>
        /// The folder of the settings asset.
        /// </summary>
        public string ConfigFolder
        {
            get
            {
                return Path.GetDirectoryName(AssetPath);
            }
        }

        /// <summary>
        /// The folder for the group assets.
        /// </summary>
        public string GroupFolder
        {
            get
            {
                return ConfigFolder + "/AssetGroups";
            }
        }
        /// <summary>
        /// The folder for the script assets.
        /// </summary>
        public string DataBuilderFolder
        {
            get
            {
                return ConfigFolder + "/DataBuilders";
            }
        }
        /// <summary>
        /// The folder for the asset group schema assets.
        /// </summary>
        public string GroupSchemaFolder
        {
            get
            {
                return GroupFolder + "/Schemas";
            }
        }
        /// <summary>
        /// Event for handling settings changes.  The object passed depends on the event type.
        /// </summary>
        public Action<AddressableAssetSettings, ModificationEvent, object> OnModification { get; set; }

        /// <summary>
        /// Event for handling the result of a DataBuilder.Build call.
        /// </summary>
        public Action<AddressableAssetSettings, IDataBuilder, IDataBuilderResult> OnDataBuilderComplete { get; set; }

        [FormerlySerializedAs("m_defaultGroup")]
        [SerializeField]
        string m_DefaultGroup;
        [FormerlySerializedAs("m_cachedHash")]
        [SerializeField]
        Hash128 m_CachedHash;

        bool m_IsTemporary;
        /// <summary>
        /// Returns whether this settings object is persisted to an asset.
        /// </summary>
        public bool IsPersisted { get { return !m_IsTemporary; } }

        /// <summary>
        /// Hash of the current settings.  This value is recomputed if anything changes.
        /// </summary>
        public Hash128 currentHash
        {
            get
            {
                if (m_CachedHash.isValid)
                    return m_CachedHash;
                var stream = new MemoryStream();
                var formatter = new BinaryFormatter();
                //                formatter.Serialize(stream, m_buildSettings);
                m_BuildSettings.SerializeForHash(formatter, stream);
                formatter.Serialize(stream, activeProfileId);
                formatter.Serialize(stream, m_LabelTable);
                formatter.Serialize(stream, m_ProfileSettings);
                formatter.Serialize(stream, m_GroupAssets.Count);
                foreach (var g in m_GroupAssets)
                    g.SerializeForHash(formatter, stream);
                return (m_CachedHash = HashingMethods.Calculate(stream).ToHash128());
            }
        }
        
        internal void DataBuilderCompleted(IDataBuilder builder, IDataBuilderResult result)
        {
            if (OnDataBuilderComplete != null)
                OnDataBuilderComplete(this, builder, result);
        }
                
        /// <summary>
        /// Create an AssetReference object.  If the asset is not already addressable, it will be added.  
        /// </summary>
        /// <param name="guid">The guid of the asset reference.</param>
        /// <returns>Returns the newly created AssetReference.</returns>
        public AssetReference CreateAssetReference(string guid)
        {
            CreateOrMoveEntry(guid, DefaultGroup);
            return new AssetReference(guid);
        }

        /// <summary>
        /// The version of the player build.  This is implemented as a timestamp int UTC of the form  string.Format("{0:D4}.{1:D2}.{2:D2}.{3:D2}.{4:D2}.{5:D2}", now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second).
        /// </summary>
        public string PlayerBuildVersion
        {
            get
            {
                var now = DateTime.UtcNow;
                return string.Format("{0:D4}.{1:D2}.{2:D2}.{3:D2}.{4:D2}.{5:D2}", now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second);
            }
        }

        //TODO: deprecate and remove once most users have transitioned to newer external data files
        [SerializeField]
        List<AddressableAssetGroupDeprecated> m_groups = new List<AddressableAssetGroupDeprecated>();

        [FormerlySerializedAs("m_groupAssets")]
        [SerializeField]
        List<AddressableAssetGroup> m_GroupAssets = new List<AddressableAssetGroup>();
        /// <summary>
        /// List of asset groups.
        /// </summary>
        public List<AddressableAssetGroup> groups { get { return m_GroupAssets; } }

        [FormerlySerializedAs("m_buildSettings")]
        [SerializeField]
        AddressableAssetBuildSettings m_BuildSettings = new AddressableAssetBuildSettings();
        /// <summary>
        /// Build settings object.
        /// </summary>
        public AddressableAssetBuildSettings buildSettings { get { return m_BuildSettings; } }

        [FormerlySerializedAs("m_profileSettings")]
        [SerializeField]
        AddressableAssetProfileSettings m_ProfileSettings = new AddressableAssetProfileSettings();
        /// <summary>
        /// Profile settings object.
        /// </summary>
        public AddressableAssetProfileSettings profileSettings { get { return m_ProfileSettings; } }

        [FormerlySerializedAs("m_labelTable")]
        [SerializeField]
        LabelTable m_LabelTable = new LabelTable();
        /// <summary>
        /// LabelTable object.
        /// </summary>
        internal LabelTable labelTable { get { return m_LabelTable; } }
        [FormerlySerializedAs("m_schemaTemplates")]
        [SerializeField]
        List<AddressableAssetGroupSchemaTemplate> m_SchemaTemplates = new List<AddressableAssetGroupSchemaTemplate>();
        /// <summary>
        /// Get defined schema templates.
        /// </summary>
        public List<AddressableAssetGroupSchemaTemplate> SchemaTemplates { get { return m_SchemaTemplates; } }

        /// <summary>
        /// Remove  the schema at the specified index.
        /// </summary>
        /// <param name="index">The index to remove at.</param>
        /// <param name="postEvent">Indicates if an even should be posted to the Addressables event system for this change.</param>
        /// <returns>True if the schema was removed.</returns>
        public bool RemoveSchemaTemplate(int index, bool postEvent = true)
        {
            if (index < 0 || index >= m_SchemaTemplates.Count)
            {
                Debug.LogWarningFormat("Invalid index for schema template: {0}.", index);
                return false;
            }
            var s = m_SchemaTemplates[index];
            m_SchemaTemplates.RemoveAt(index);
            SetDirty(ModificationEvent.GroupSchemaRemoved, s, postEvent);
            return true;
        }


        [FormerlySerializedAs("m_initializationObjects")]
        [SerializeField]
        List<ScriptableObject> m_InitializationObjects = new List<ScriptableObject>();
        /// <summary>
        /// List of ScriptableObjects that implement the IObjectInitializationDataProvider interface for providing runtime initialization.
        /// </summary>
        public List<ScriptableObject> InitializationObjects
        {
            get { return m_InitializationObjects; }
        }

        /// <summary>
        /// Get the IObjectInitializationDataProvider at a specifc index.
        /// </summary>
        /// <param name="index">The index of the initialization object.</param>
        /// <returns>The initialization object at the specified index.</returns>
        public IObjectInitializationDataProvider GetInitializationObject(int index)
        {
            if (m_InitializationObjects.Count == 0)
                return null;
            if (index < 0 || index >= m_InitializationObjects.Count)
            {
                Debug.LogWarningFormat("Invalid index for data builder: {0}.", index);
                return null;
            }
            return m_InitializationObjects[Mathf.Clamp(index, 0, m_InitializationObjects.Count)] as IObjectInitializationDataProvider;
        }

        /// <summary>
        /// Adds an initialization object.
        /// </summary>
        /// <param name="initObject">The initialization object to add.</param>
        /// <param name="postEvent">Indicates if an even should be posted to the Addressables event system for this change.</param>
        /// <returns>True if the initialization object was added.</returns>
        public bool AddInitializationObject(IObjectInitializationDataProvider initObject, bool postEvent = true)
        {
            if (initObject == null)
            {
                Debug.LogWarning("Cannot add null IObjectInitializationDataProvider");
                return false;
            }
            var so = initObject as ScriptableObject;
            if (so == null)
            {
                Debug.LogWarning("Initialization objects must inherit from ScriptableObject.");
                return false;
            }

            m_InitializationObjects.Add(so);
            SetDirty(ModificationEvent.InitializationObjectAdded, so, postEvent);
            return true;
        }

        /// <summary>
        /// Remove the initialization object at the specified index.
        /// </summary>
        /// <param name="index">The index to remove.</param>
        /// <param name="postEvent">Indicates if an even should be posted to the Addressables event system for this change.</param>
        /// <returns>True if the initialization object was removed.</returns>
        public bool RemoveInitializationObject(int index, bool postEvent = true)
        {
            if (m_InitializationObjects.Count <= index)
                return false;
            var so = m_InitializationObjects[index];
            m_InitializationObjects.RemoveAt(index);
            SetDirty(ModificationEvent.InitializationObjectRemoved, so, postEvent);
            return true;
        }

        /// <summary>
        /// Sets the initialization object at the specified index.
        /// </summary>
        /// <param name="index">The index to set the initialization object.</param>
        /// <param name="initObject">The initialization object to set.  This must be a valid scriptable object that implements the IInitializationObject interface.</param>
        /// <param name="postEvent">Indicates if an even should be posted to the Addressables event system for this change.</param>
        /// <returns>True if the initialization object was set, false otherwise.</returns>
        public bool SetInitializationObjectAtIndex(int index, IObjectInitializationDataProvider initObject, bool postEvent = true)
        {
            if (m_InitializationObjects.Count <= index)
                return false;
            if (initObject == null)
            {
                Debug.LogWarning("Cannot add null IObjectInitializationDataProvider");
                return false;
            }
            var so = initObject as ScriptableObject;
            if (so == null)
            {
                Debug.LogWarning("Initialization objects must inherit from ScriptableObject.");
                return false;
            }

            m_InitializationObjects[index] = so;
            SetDirty(ModificationEvent.InitializationObjectAdded, so, postEvent);
            return true;
        }


        [FormerlySerializedAs("m_activePlayerDataBuilderIndex")]
        [SerializeField]
        int m_ActivePlayerDataBuilderIndex = 3;
        [FormerlySerializedAs("m_activePlayModeDataBuilderIndex")]
        [SerializeField]
        int m_ActivePlayModeDataBuilderIndex;
        [FormerlySerializedAs("m_dataBuilders")]
        [SerializeField]
        List<ScriptableObject> m_DataBuilders = new List<ScriptableObject>();
        /// <summary>
        /// List of ScriptableObjects that implement the IDataBuilder interface.  These are used to create data for editor play mode and for player builds.
        /// </summary>
        public List<ScriptableObject> DataBuilders { get { return m_DataBuilders; } }
        /// <summary>
        /// Get The data builder at a specifc index.
        /// </summary>
        /// <param name="index">The index of the builder.</param>
        /// <returns>The data builder at the specified index.</returns>
        public IDataBuilder GetDataBuilder(int index)
        {
            if (m_DataBuilders.Count == 0)
                return null;
            if (index < 0 || index >= m_DataBuilders.Count)
            {
                Debug.LogWarningFormat("Invalid index for data builder: {0}.", index);
                return null;
            }
            return m_DataBuilders[Mathf.Clamp(index, 0, m_DataBuilders.Count)] as IDataBuilder;
        }

        /// <summary>
        /// Adds a data builder.
        /// </summary>
        /// <param name="builder">The data builder to add.</param>
        /// <param name="postEvent">Indicates if an even should be posted to the Addressables event system for this change.</param>
        /// <returns>True if the data builder was added.</returns>
        public bool AddDataBuilder(IDataBuilder builder, bool postEvent = true)
        {
            if (builder == null)
            {
                Debug.LogWarning("Cannot add null IDataBuilder");
                return false;
            }
            var so = builder as ScriptableObject;
            if (so == null)
            {
                Debug.LogWarning("Data builders must inherit from ScriptableObject.");
                return false;
            }

            m_DataBuilders.Add(so);
            SetDirty(ModificationEvent.DataBuilderAdded, so, postEvent);
            return true;
        }

        /// <summary>
        /// Remove the data builder at the sprcified index.
        /// </summary>
        /// <param name="index">The index to remove.</param>
        /// <param name="postEvent">Indicates if an even should be posted to the Addressables event system for this change.</param>
        /// <returns>True if the builder was removed.</returns>
        public bool RemoveDataBuilder(int index, bool postEvent = true)
        {
            if (m_DataBuilders.Count <= index)
                return false;
            var so = m_DataBuilders[index];
            m_DataBuilders.RemoveAt(index);
            SetDirty(ModificationEvent.DataBuilderRemoved, so, postEvent);
            return true;
        }

        /// <summary>
        /// Sets the data builder at the specified index.
        /// </summary>
        /// <param name="index">The index to set the builder.</param>
        /// <param name="builder">The builder to set.  This must be a valid scriptable object that implements the IDataBuilder interface.</param>
        /// <param name="postEvent">Indicates if an even should be posted to the Addressables event system for this change.</param>
        /// <returns>True if the builder was set, false otherwise.</returns>
        public bool SetDataBuilderAtIndex(int index, IDataBuilder builder, bool postEvent = true)
        {
            if (m_DataBuilders.Count <= index)
                return false;
            if (builder == null)
            {
                Debug.LogWarning("Cannot add null IDataBuilder");
                return false;
            }
            var so = builder as ScriptableObject;
            if (so == null)
            {
                Debug.LogWarning("Data builders must inherit from ScriptableObject.");
                return false;
            }

            m_DataBuilders[index] = so;
            SetDirty(ModificationEvent.DataBuilderAdded, so, postEvent);
            return true;
        }

        /// <summary>
        /// Get the active data builder for player data.
        /// </summary>
        public IDataBuilder ActivePlayerDataBuilder
        {
            get
            {
                return GetDataBuilder(m_ActivePlayerDataBuilderIndex);
            }
        }

        /// <summary>
        /// Get the active data builder for editor play mode data.
        /// </summary>
        public IDataBuilder ActivePlayModeDataBuilder
        {
            get
            {
                return GetDataBuilder(m_ActivePlayModeDataBuilderIndex);
            }
        }

        /// <summary>
        /// Get the index of the active player data builder.
        /// </summary>
        public int ActivePlayerDataBuilderIndex
        {
            get
            {
                return m_ActivePlayerDataBuilderIndex;
            }
            set
            {
                m_ActivePlayerDataBuilderIndex = value;
                SetDirty(ModificationEvent.ActiveBuildScriptChanged, ActivePlayerDataBuilder, true);
            }
        }

        /// <summary>
        /// Get the index of the active play mode data builder.
        /// </summary>
        public int ActivePlayModeDataBuilderIndex
        {
            get
            {
                return m_ActivePlayModeDataBuilderIndex;
            }
            set
            {
                m_ActivePlayModeDataBuilderIndex = value;
                SetDirty(ModificationEvent.ActivePlayModeScriptChanged, ActivePlayModeDataBuilder, true);
            }
        }


        /// <summary>
        /// Add a new label.
        /// </summary>
        /// <param name="label">The label name.</param>
        /// <param name="postEvent">Send modification event.</param>
        public void AddLabel(string label, bool postEvent = true)
        {
            m_LabelTable.AddLabelName(label);
            SetDirty(ModificationEvent.LabelAdded, label, postEvent);
        }

        /// <summary>
        /// Remove a label by name.
        /// </summary>
        /// <param name="label">The label name.</param>
        /// <param name="postEvent">Send modification event.</param>
        public void RemoveLabel(string label, bool postEvent = true)
        {
            m_LabelTable.RemoveLabelName(label);
            SetDirty(ModificationEvent.LabelRemoved, label, postEvent);
        }

        [FormerlySerializedAs("m_activeProfileId")]
        [SerializeField]
        string m_ActiveProfileId;
        /// <summary>
        /// The active profile id.
        /// </summary>
        public string activeProfileId
        {
            get
            {
                if (string.IsNullOrEmpty(m_ActiveProfileId))
                    m_ActiveProfileId = m_ProfileSettings.CreateDefaultProfile();
                return m_ActiveProfileId;
            }
            set
            {
                var oldVal = m_ActiveProfileId;
                m_ActiveProfileId = value;

                if (oldVal != value)
                {
                    SetDirty(ModificationEvent.ActiveProfileSet, value, true);
                }
            }
        }

        [FormerlySerializedAs("m_hostingServicesManager")]
        [SerializeField]
        HostingServicesManager m_HostingServicesManager;
        /// <summary>
        /// Get the HostingServicesManager object.
        /// </summary>
        public HostingServicesManager HostingServicesManager
        {
            get
            {
                if (m_HostingServicesManager == null)
                    m_HostingServicesManager = new HostingServicesManager();

                if (!m_HostingServicesManager.IsInitialized)
                    m_HostingServicesManager.Initialize(this);

                return m_HostingServicesManager;
            }

            // For unit tests
            internal set { m_HostingServicesManager = value; }
        }

        /// <summary>
        /// Gets all asset entries from all groups.
        /// </summary>
        /// <param name="assets">The list of asset entries.</param>
        /// <param name="filter">A method to filter groups.  Groups will be processed if filter is null, or it returns TRUE</param>
        public void GetAllAssets(List<AddressableAssetEntry> assets, Func<AddressableAssetGroup, bool> filter = null)
        {
            foreach (var g in groups)
                if (filter == null || filter(g))
                    g.GatherAllAssets(assets, true, true);
        }

        /// <summary>
        /// Remove an asset entry.
        /// </summary>
        /// <param name="guid">The  guid of the asset.</param>
        /// <param name="postEvent">Send modifcation event.</param>
        /// <returns>True if the entry was found and removed.</returns>
        public bool RemoveAssetEntry(string guid, bool postEvent = true)
        {
            var entry = FindAssetEntry(guid);
            if (entry != null)
            {
                if (entry.parentGroup != null)
                    entry.parentGroup.RemoveAssetEntry(entry, postEvent);
                SetDirty(ModificationEvent.EntryRemoved, entry, postEvent);
                return true;
            }
            return false;
        }
        
        void OnEnable()
        {
            //TODO: deprecate and remove once most users have transitioned to newer external data files
          if (m_groups != null)
            {
                for (int i = 0; i < m_groups.Count; i++)
                    if (m_groups[i] != null)
                        this.ConvertDeprecatedGroupData(m_groups[i], i < 2);
                m_groups = null;
            }
            
            profileSettings.OnAfterDeserialize(this);
            buildSettings.OnAfterDeserialize(this);
            Validate();
            HostingServicesManager.OnEnable();
        }

        void OnDisable()
        {
            HostingServicesManager.OnDisable();
        }

        void Validate()
        {
            if (m_SchemaTemplates == null)
                m_SchemaTemplates = new List<AddressableAssetGroupSchemaTemplate>();
            if (m_SchemaTemplates.Count == 0)
                AddSchemaTemplate("Packed Assets", "Pack assets into asset bundles.", typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            if (m_BuildSettings == null)
                m_BuildSettings = new AddressableAssetBuildSettings();
            if (m_ProfileSettings == null)
                m_ProfileSettings = new AddressableAssetProfileSettings();
            if (m_LabelTable == null)
                m_LabelTable = new LabelTable();
            if (string.IsNullOrEmpty(m_ActiveProfileId))
                m_ActiveProfileId = m_ProfileSettings.CreateDefaultProfile();
            if (m_DataBuilders == null || m_DataBuilders.Count == 0)
            {
                m_DataBuilders = new List<ScriptableObject>();
                m_DataBuilders.Add(CreateScriptAsset<BuildScriptFastMode>());
                m_DataBuilders.Add(CreateScriptAsset<BuildScriptVirtualMode>());
                m_DataBuilders.Add(CreateScriptAsset<BuildScriptPackedPlayMode>());
                m_DataBuilders.Add(CreateScriptAsset<BuildScriptPackedMode>());
            }
            if(m_DataBuilders.Find(s=>s.GetType() == typeof(BuildScriptPackedPlayMode)) == null)
                m_DataBuilders.Add(CreateScriptAsset<BuildScriptPackedPlayMode>());

            if (!ActivePlayerDataBuilder.CanBuildData<AddressablesPlayerBuildResult>())
                ActivePlayerDataBuilderIndex = m_DataBuilders.IndexOf(m_DataBuilders.Find(s => s.GetType() == typeof(BuildScriptPackedMode)));
            if (!ActivePlayModeDataBuilder.CanBuildData<AddressablesPlayModeBuildResult>())
                ActivePlayerDataBuilderIndex = m_DataBuilders.IndexOf(m_DataBuilders.Find(s => s.GetType() == typeof(BuildScriptFastMode)));

            profileSettings.Validate(this);
            buildSettings.Validate(this);
        }
        
        T CreateScriptAsset<T>() where T : ScriptableObject
        {
            var script = CreateInstance<T>();
            if (!Directory.Exists(DataBuilderFolder))
                Directory.CreateDirectory(DataBuilderFolder);
            var path = DataBuilderFolder + "/" + typeof(T).Name + ".asset";
            if (!File.Exists(path))
                AssetDatabase.CreateAsset(script, path);
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }

        internal const string PlayerDataGroupName = "Built In Data";
        internal const string DefaultLocalGroupName = "Default Local Group";

        /// <summary>
        /// Create a new AddressableAssetSettings object.
        /// </summary>
        /// <param name="configFolder">The folder to store the settings object.</param>
        /// <param name="configName">The name of the settings object.</param>
        /// <param name="createDefaultGroups">If true, create groups for player data and local packed content.</param>
        /// <param name="isPersisted">If true, assets are created.</param>
        /// <returns></returns>
        public static AddressableAssetSettings Create(string configFolder, string configName, bool createDefaultGroups, bool isPersisted)
        {
            AddressableAssetSettings aa;
            var path = configFolder + "/" + configName + ".asset";
            aa = isPersisted ? AssetDatabase.LoadAssetAtPath<AddressableAssetSettings>(path) : null;
            if (aa == null)
            {
                aa = CreateInstance<AddressableAssetSettings>();
                aa.m_IsTemporary = !isPersisted;
                aa.activeProfileId = aa.profileSettings.Reset();
                aa.name = configName;

                if (isPersisted)
                {
                    Directory.CreateDirectory(configFolder);
                    AssetDatabase.CreateAsset(aa, path);
                }

                if (createDefaultGroups)
                {
                    var playerData = aa.CreateGroup(PlayerDataGroupName, false, false, false, null, typeof(PlayerDataGroupSchema));
                    var resourceEntry = aa.CreateOrMoveEntry(AddressableAssetEntry.ResourcesName, playerData);
                    resourceEntry.IsInResources = true;
                    aa.CreateOrMoveEntry(AddressableAssetEntry.EditorSceneListName, playerData);

                    var localGroup = aa.CreateGroup(DefaultLocalGroupName, true, false, false, null, typeof(ContentUpdateGroupSchema), typeof(BundledAssetGroupSchema));
                    var schema = localGroup.GetSchema<BundledAssetGroupSchema>();
                    schema.BuildPath.SetVariableByName(aa, kLocalBuildPath);
                    schema.LoadPath.SetVariableByName(aa, kLocalLoadPath);
                    schema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
                }

                if (isPersisted)
                    AssetDatabase.SaveAssets();
            }
            AddressableScenesManager.RegisterForSettingsCallback(aa);
            return aa;
        }

        /// <summary>
        /// Adds a named set of schema types for use in the editor GUI.
        /// </summary>
        /// <param name="displayName">The display name of the template.</param>
        /// <param name="description">Tooltip text for the template.</param>
        /// <param name="types">The schema types for the template.</param>
        /// <returns>True if the template was added, false otherwise.</returns>
        public bool AddSchemaTemplate(string displayName, string description, params Type[] types)
        {
            if (string.IsNullOrEmpty(displayName))
            {
                Debug.LogWarningFormat("AddSchemaTemplate - Schema template must have a valid name.");
                return false;
            }
            if (types.Length == 0)
            {
                Debug.LogWarningFormat("AddSchemaTemplate - Schema template {0} must contain at least 1 schema type.", displayName);
                return false;
            }
            bool typesAreValid = true;
            for(int i = 0; i < types.Length; i++)
            {
                var t = types[i];
                if (t == null)
                {
                    Debug.LogWarningFormat("AddSchemaTemplate - Schema template {0} schema type at index {1} is null.", displayName, i);
                    typesAreValid = false;
                }
                else if (!typeof(AddressableAssetGroupSchema).IsAssignableFrom(t))
                {
                    Debug.LogWarningFormat("AddSchemaTemplate - Schema template {0} schema type at index {1} must inherit from AddressableAssetGroupSchema.  Specified type was {2}.", displayName, i, t.FullName);
                    typesAreValid = false;
                }
            }
            if (!typesAreValid)
            {
                Debug.LogWarningFormat("AddSchemaTemplate - Schema template {0} must contains at least 1 invalid schema type.", displayName);
                return false;
            }
            m_SchemaTemplates.Add(AddressableAssetGroupSchemaTemplate.Create(displayName, description, types));
            SetDirty(ModificationEvent.GroupSchemaTemplateAdded, m_SchemaTemplates[m_SchemaTemplates.Count - 1], true);
            return true;
        }


        /// <summary>
        /// Find asset group by functor.
        /// </summary>
        /// <param name="func">The functor to call on each group.  The first group that evaluates to true is returned.</param>
        /// <returns>The group found or null.</returns>
        public AddressableAssetGroup FindGroup(Func<AddressableAssetGroup, bool> func)
        {
            return groups.Find(g => func(g));
        }


        /// <summary>
        /// Find asset group by name.
        /// </summary>
        /// <param name="groupName">The name of the group.</param>
        /// <returns>The group found or null.</returns>
        public AddressableAssetGroup FindGroup(string groupName)
        {
            return FindGroup(g => g.Name == groupName);
        }

        /// <summary>
        /// The default group.  This group is used when marking assets as addressable via the inspector.
        /// </summary>
        public AddressableAssetGroup DefaultGroup
        {
            get
            {
                if (string.IsNullOrEmpty(m_DefaultGroup))
                {
                    //set to the first non readonly group if possible
                    foreach (var g in groups)
                    {
                        if (!g.ReadOnly)
                        {
                            m_DefaultGroup = g.Guid;
                            break;
                        }
                    }
                    if (string.IsNullOrEmpty(m_DefaultGroup))
                    {
                        Addressables.LogError("Attempting to access Default Addressables group but no valid group is available");
                        return null;
                    }
                }
                var group = groups.Find(s => s.Guid == m_DefaultGroup);
                if(group == null)
                {
                    foreach(var g in groups)
                    {
                        if (!g.ReadOnly)
                        {
                            group = g;
                            break;
                        }
                    }
                    if(group == null)
                    {
                        Debug.LogWarning("Addressable assets must have at least one group that is not read-only to be default, creating new group");
                        group = CreateGroup("New Group", true, false, true, null, typeof(BundledAssetGroupSchema));
                    }
                }
                return group;
            }
            set
            {
                m_DefaultGroup = value.Guid;
            }
        }

        AddressableAssetEntry CreateEntry(string guid, string address, AddressableAssetGroup parent, bool readOnly, bool postEvent = true)
        {
            var entry = new AddressableAssetEntry(guid, address, parent, readOnly);
            if (!readOnly)
                SetDirty(ModificationEvent.EntryCreated, entry, postEvent);
            return entry;
        }

        /// <summary>
        /// Marks the object as modified.
        /// </summary>
        /// <param name="modificationEvent">The event type that is changed.</param>
        /// <param name="eventData">The object data that corresponds to the event.</param>
        /// <param name="postEvent">If true, the event is propagated to callbacks.</param>
        public void SetDirty(ModificationEvent modificationEvent, object eventData, bool postEvent)
        {
            if (modificationEvent == ModificationEvent.ProfileRemoved && eventData as string == activeProfileId)
                activeProfileId = null;

            if (postEvent && OnModification != null)
                OnModification(this, modificationEvent, eventData);
            var unityObj = eventData as Object;
            if (unityObj != null && !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(unityObj)))
                EditorUtility.SetDirty(unityObj);

            if (IsPersisted)
                EditorUtility.SetDirty(this);

            m_CachedHash = default(Hash128);
        }

        /// <summary>
        /// Find and asset entry by guid.
        /// </summary>
        /// <param name="guid">The asset guid.</param>
        /// <returns>The found entry or null.</returns>
        public AddressableAssetEntry FindAssetEntry(string guid)
        {
            foreach (var g in groups)
            {
                var e = g.GetAssetEntry(guid);
                if (e != null)
                    return e;
            }
            return null;
        }

        internal void MoveAssetsFromResources(Dictionary<string, string> guidToNewPath, AddressableAssetGroup targetParent)
        {
            if (guidToNewPath == null)
                return;
            var entries = new List<AddressableAssetEntry>();
            AssetDatabase.StartAssetEditing();
            foreach (var item in guidToNewPath)
            {

                var dirInfo = new FileInfo(item.Value).Directory;
                if (dirInfo != null && !dirInfo.Exists)
                {
                    dirInfo.Create();
                    AssetDatabase.StopAssetEditing();
                    AssetDatabase.Refresh();
                    AssetDatabase.StartAssetEditing();
                }

                var oldPath = AssetDatabase.GUIDToAssetPath(item.Key);
                var errorStr = AssetDatabase.MoveAsset(oldPath, item.Value);
                if (!string.IsNullOrEmpty(errorStr))
                {
                    Addressables.LogError("Error moving asset: " + errorStr);
                }
                else
                {
                    AddressableAssetEntry e = FindAssetEntry(item.Key);
                    if (e != null)
                        e.IsInResources = false;

                    var newEntry = CreateOrMoveEntry(item.Key, targetParent, false, false);
                    var index = oldPath.ToLower().LastIndexOf("resources/");
                    if (index >= 0)
                    {
                        var newAddress = Path.GetFileNameWithoutExtension(oldPath.Substring(index + 10));
                        if (!string.IsNullOrEmpty(newAddress))
                        {
                            newEntry.address = newAddress;
                        }
                    }
                    entries.Add(newEntry);
                }

            }
            AssetDatabase.StopAssetEditing();
            AssetDatabase.Refresh();
            SetDirty(ModificationEvent.EntryMoved, entries, true);
        }


        /// <summary>
        /// Create a new entry, or if one exists in a different group, move it into the new group.
        /// </summary>
        /// <param name="guid">The asset guid.</param>
        /// <param name="targetParent">The group to add the entry to.</param>
        /// <param name="readOnly">Is the new entry read only.</param>
        /// <param name="postEvent">Send modification event.</param>
        /// <returns></returns>
        public AddressableAssetEntry CreateOrMoveEntry(string guid, AddressableAssetGroup targetParent, bool readOnly = false, bool postEvent = true)
        {
            if (targetParent == null)
                return null;

            AddressableAssetEntry entry = FindAssetEntry(guid);
            if (entry != null) //move entry to where it should go...
            {
                entry.IsSubAsset = false;
                entry.ReadOnly = readOnly;
                if (entry.parentGroup == targetParent)
                {
                    targetParent.AddAssetEntry(entry, postEvent); //in case this is a sub-asset, make sure parent knows about it now.
                    return entry;
                }

                if (entry.IsInSceneList)
                {
                    var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
                    foreach (var scene in scenes)
                    {
                        if (scene.guid == new GUID(entry.guid))
                            scene.enabled = false;
                    }
                    EditorBuildSettings.scenes = scenes.ToArray();
                    entry.IsInSceneList = false;
                }
                if (entry.parentGroup != null)
                    entry.parentGroup.RemoveAssetEntry(entry, postEvent);
                entry.parentGroup = targetParent;
            }
            else //create entry
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (AddressableAssetUtility.IsPathValidForEntry(path))
                {
                    entry = CreateEntry(guid, path, targetParent, readOnly, postEvent);
                }
                else
                {
                    entry = CreateEntry(guid, guid, targetParent, true, postEvent);
                }
            }

            targetParent.AddAssetEntry(entry, postEvent);
            return entry;
        }

        internal AddressableAssetEntry CreateSubEntryIfUnique(string guid, string address, AddressableAssetEntry parentEntry)
        {
            if (string.IsNullOrEmpty(guid))
                return null;

            var entry = FindAssetEntry(guid);
            if (entry == null)
            {
                entry = CreateEntry(guid, address, parentEntry.parentGroup, true);
                entry.IsSubAsset = true;
                return entry;
            }

            //if the sub-entry already exists update it's info.  This mainly covers the case of dragging folders around.
            if (entry.IsSubAsset)
            {
                entry.parentGroup = parentEntry.parentGroup;
                entry.IsInResources = parentEntry.IsInResources;
                entry.address = address;
                entry.ReadOnly = true;
                return entry;
            }
            return null;
        }

        /// <summary>
        /// Create a new asset group.
        /// </summary>
        /// <param name="groupName">The group name.</param>
        /// <param name="setAsDefaultGroup">Set the new group as the default group.</param>
        /// <param name="readOnly">Is the new group read only.</param>
        /// <param name="postEvent">Post modification event.</param>
        /// <param name="schemasToCopy">Schema set to copy from.</param>
        /// <param name="types">Types of schemas to add.</param>
        /// <returns>The newly created group.</returns>
        public AddressableAssetGroup CreateGroup(string groupName, bool setAsDefaultGroup, bool readOnly, bool postEvent, List<AddressableAssetGroupSchema> schemasToCopy, params Type[] types)
        {
            if (string.IsNullOrEmpty(groupName))
                groupName = kNewGroupName;
            string validName = FindUniqueGroupName(groupName);
            var group = CreateInstance<AddressableAssetGroup>();
            group.Initialize(this, validName, GUID.Generate().ToString(), readOnly);

            groups.Add(group);
            if (IsPersisted)
            {
                if (!Directory.Exists(GroupFolder))
                    Directory.CreateDirectory(GroupFolder);
                AssetDatabase.CreateAsset(group, GroupFolder + "/" + group.Name + ".asset");
            }
            if (setAsDefaultGroup)
                DefaultGroup = group;
            if (schemasToCopy != null)
            {
                foreach (var s in schemasToCopy)
                    group.AddSchema(s, false);
            }
            foreach (var t in types)
                group.AddSchema(t);

            SetDirty(ModificationEvent.GroupAdded, group, postEvent);
            return group;
        }

        internal string FindUniqueGroupName(string potentialName)
        {
            var cleanedName = potentialName.Replace('/', '-');
            cleanedName = cleanedName.Replace('\\', '-');
            if(cleanedName != potentialName)
                Debug.Log("Group names cannot include '\\' or '/'.  Replacing with '-'. " + cleanedName);
            var validName = cleanedName;
            int index = 1;
            bool foundExisting = true;
            while (foundExisting)
            {
                if (index > 1000)
                {
                    Addressables.LogError("Unable to create valid name for new Addressable Assets group.");
                    return cleanedName;
                }
                foundExisting = IsNotUniqueGroupName(validName);
                if (foundExisting)
                {
                    validName = cleanedName + index;
                    index++;
                }
            }

            return validName;
        }

        internal bool IsNotUniqueGroupName(string groupName)
        {
            bool foundExisting = false;
            foreach (var g in groups)
            {
                if (g.Name == groupName)
                {
                    foundExisting = true;
                    break;
                }
            }
            return foundExisting;
        }

        /// <summary>
        /// Remove an asset group.
        /// </summary>
        /// <param name="g"></param>
        public void RemoveGroup(AddressableAssetGroup g)
        {
            RemoveGroupInternal(g, true, true);
        }

        internal void RemoveGroupInternal(AddressableAssetGroup g, bool deleteAsset, bool postEvent)
        {
            g.ClearSchemas(true);
            groups.Remove(g);
            SetDirty(ModificationEvent.GroupRemoved, g, postEvent);
            if (deleteAsset)
            {
                string guidOfGroup;
                long localId;
                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(g, out guidOfGroup, out localId))
                {
                    var groupPath = AssetDatabase.GUIDToAssetPath(guidOfGroup);
                    if (!string.IsNullOrEmpty(groupPath))
                        AssetDatabase.DeleteAsset(groupPath);
                }
            }
        }


        internal void SetLabelValueForEntries(List<AddressableAssetEntry> entries, string label, bool value, bool postEvent = true)
        {
            if (value)
                AddLabel(label);

            foreach (var e in entries)
                e.SetLabel(label, value, false);

            SetDirty(ModificationEvent.EntryModified, entries, postEvent);
        }

        internal void MoveEntriesToGroup(List<AddressableAssetEntry> entries, AddressableAssetGroup targetGroup)
        {
            foreach (var e in entries)
            {
                if (e.parentGroup != null)
                    e.parentGroup.RemoveAssetEntry(e, false);
                targetGroup.AddAssetEntry(e, false);
            }
            SetDirty(ModificationEvent.EntryMoved, entries, true);
        }

        internal void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            var aa = this;
            bool modified = false;
            foreach (string str in importedAssets)
            {
                var assetType = AssetDatabase.GetMainAssetTypeAtPath(str);

                if (typeof(AddressableAssetEntryCollection).IsAssignableFrom(assetType))
                {
                    aa.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(str), aa.DefaultGroup);
                    modified = true;
                }
                var guid = AssetDatabase.AssetPathToGUID(str);
                if (aa.FindAssetEntry(guid) != null)
                    modified = true;

                if (AddressableAssetUtility.IsInResources(str))
                    modified = true;
            }
            foreach (string str in deletedAssets)
            {
                var guidOfDeletedAsset = AssetDatabase.AssetPathToGUID(str);
                var oldGroupName = Path.GetFileNameWithoutExtension(str);
                var group = aa.FindGroup(oldGroupName);
                var groupObj = group as object;
                bool assetIsGroup = false;
                if (groupObj != null)
                {
                    string guidOfGroup;
                    long localId;
                    if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(group, out guidOfGroup, out localId))
                    {
                        if (guidOfGroup == guidOfDeletedAsset)
                        {
                            assetIsGroup = true;
                            aa.RemoveGroupInternal(group, false, true);
                        }
                    }
                }

                if (!assetIsGroup)
                {
                    var guid = AssetDatabase.AssetPathToGUID(str);
                    if (aa.RemoveAssetEntry(guid))
                        modified = true;
                }

                if (AddressableAssetUtility.IsInResources(str))
                    modified = true;
            }
            for (int i = 0; i < movedAssets.Length; i++)
            {
                var str = movedAssets[i];
                var assetType = AssetDatabase.GetMainAssetTypeAtPath(str);
                if (typeof(AddressableAssetGroup).IsAssignableFrom(assetType))
                {
                    var oldGroupName = Path.GetFileNameWithoutExtension(movedFromAssetPaths[i]);
                    var group = aa.FindGroup(oldGroupName);
                    if (group != null)
                    {
                        var newGroupName = Path.GetFileNameWithoutExtension(str);
                        group.Name = newGroupName;
                    }
                }
                else
                {
                    var guid = AssetDatabase.AssetPathToGUID(str);
                    if (aa.FindAssetEntry(guid) != null)
                        modified = true;

                    //move to Resources
                    if (AddressableAssetUtility.IsInResources(str))
                    {
                        modified = true;
                        var fileName = Path.GetFileNameWithoutExtension(str);
                        Debug.Log("You have moved addressable asset " + fileName + " into a Resources directory.  It has been unmarked as addressable, but can still be loaded via the Addressables API via its Resources path.");
                        aa.RemoveAssetEntry(guid, false);
                    }
                    //move from Resources
                    if (AddressableAssetUtility.IsInResources(movedFromAssetPaths[i]))
                    {
                        modified = true;
                    }
                }
            }

            if (modified)
                aa.SetDirty(ModificationEvent.BatchModification, null, true);
        }

        /// <summary>
        /// [Obsolete] Get the default addressables settings object
        /// </summary>
        /// <param name="create">Create settings asset if it does not exist.</param>
        /// <param name="browse">Unused.</param>
        /// <returns>The default settings object or null if it has not been set.</returns>
        [Obsolete("Use AddressableAssetSettingsDefaultObject.Settings instead. (UnityUpgradable) -> AddressableAssetSettingsDefaultObject.Settings")]
        public static AddressableAssetSettings GetDefault(bool create, bool browse)
        {
            return AddressableAssetSettingsDefaultObject.GetSettings(create);
        }

        /// <summary>
        /// Runs the active player data build script to create runtime data.
        /// </summary>
        public static void BuildPlayerContent()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogError("Addressable Asset Settings does not exist.");
                return;
            }
            if (Directory.Exists(Addressables.BuildPath))
            {
                try
                {
                    Directory.Delete(Addressables.BuildPath, true);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
            
            var buildContext = new AddressablesBuildDataBuilderContext(settings,
                BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget),
                EditorUserBuildSettings.activeBuildTarget, EditorUserBuildSettings.development,
                ProjectConfigData.postProfilerEvents, settings.PlayerBuildVersion);
            settings.ActivePlayerDataBuilder.BuildData<AddressablesPlayerBuildResult>(buildContext);
        }

        /// <summary>
        /// Deletes all created runtime data for the active player data builder.
        /// </summary>
        public static void CleanPlayerContent()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogError("Addressable Asset Settings does not exist.");
                return;
            }
            settings.ActivePlayerDataBuilder.ClearCachedData();
        }
    }
}
