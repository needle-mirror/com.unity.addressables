using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEditor.Build.Pipeline.Utilities;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement;

[assembly: System.Runtime.CompilerServices.InternalsVisibleToAttribute("Unity.Addressables.Editor.Tests")]

namespace UnityEditor.AddressableAssets
{
    /// <summary>
    /// Contains editor data for the addressables system.
    /// </summary>
    public partial class AddressableAssetSettings : ScriptableObject
    {
        [InitializeOnLoadMethod]
        static void RegisterWithAssetPostProcessor()
        {
            //if the Libray folder has been deleted, this will be null and it will have to be set on the first access of the settings object
            if(AddressableAssetSettingsDefaultObject.Settings != null)
                AddressablesAssetPostProcessor.OnPostProcess = AddressableAssetSettingsDefaultObject.Settings.OnPostprocessAllAssets;
        }
        /// <summary>
        /// Default name of a newly created group.
        /// </summary>
        public const string kNewGroupName = "New Group";

        public const string kLocalBuildPath = "LocalBuildPath";
        public const string kLocalLoadPath = "LocalLoadPath";
        public const string kRemoteBuildPath = "RemoteBuildPath";
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

        [SerializeField]
        private string m_defaultGroup;
        [SerializeField]
        Hash128 m_cachedHash;

        private bool m_isTemporary = false;
        /// <summary>
        /// Returns whether this settings object is persisted to an asset.
        /// </summary>
        public bool IsPersisted { get { return !m_isTemporary; } }

        /// <summary>
        /// Hash of the current settings.  This value is recomputed if anything changes.
        /// </summary>
        public Hash128 currentHash
        {
            get
            {
                if (m_cachedHash.isValid)
                    return m_cachedHash;
                var stream = new MemoryStream();
                var formatter = new BinaryFormatter();
                //                formatter.Serialize(stream, m_buildSettings);
                m_buildSettings.SerializeForHash(formatter, stream);
                formatter.Serialize(stream, activeProfileId);
                formatter.Serialize(stream, m_labelTable);
                formatter.Serialize(stream, m_profileSettings);
                formatter.Serialize(stream, m_groupAssets.Count);
                foreach (var g in m_groupAssets)
                    g.SerializeForHash(formatter, stream);
                return (m_cachedHash = HashingMethods.Calculate(stream).ToHash128());
            }
        }
        
        [SerializeField]
        bool m_assetsModified = true;
        internal bool AssetsModifiedSinceLastPackedBuild
        {
            get { return m_assetsModified; }
            set { m_assetsModified = value; }
        }
        

        internal void DataBuilderCompleted(IDataBuilder builder, IDataBuilderResult result)
        {
            if (OnDataBuilderComplete != null)
                OnDataBuilderComplete(this, builder, result);
        }
        
        internal class FileModificationWarning : AssetModificationProcessor
        {
            static string[] OnWillSaveAssets(string[] paths)
            {
                if (AddressableAssetSettingsDefaultObject.Settings != null)
                    AddressableAssetSettingsDefaultObject.Settings.AssetsModifiedSinceLastPackedBuild = true;
                return paths;
            }
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

        [SerializeField]
        List<AddressableAssetGroup> m_groupAssets = new List<AddressableAssetGroup>();
        /// <summary>
        /// List of asset groups.
        /// </summary>
        public List<AddressableAssetGroup> groups { get { return m_groupAssets; } }

        [SerializeField]
        AddressableAssetBuildSettings m_buildSettings = new AddressableAssetBuildSettings();
        /// <summary>
        /// Build settings object.
        /// </summary>
        public AddressableAssetBuildSettings buildSettings { get { return m_buildSettings; } }

        [SerializeField]
        AddressableAssetProfileSettings m_profileSettings = new AddressableAssetProfileSettings();
        /// <summary>
        /// Profile settings object.
        /// </summary>
        public AddressableAssetProfileSettings profileSettings { get { return m_profileSettings; } }

        [SerializeField]
        LabelTable m_labelTable = new LabelTable();
        /// <summary>
        /// LabelTable object.
        /// </summary>
        internal LabelTable labelTable { get { return m_labelTable; } }
        [SerializeField]
        List<AddressableAssetGroupSchemaTemplate> m_schemaTemplates = new List<AddressableAssetGroupSchemaTemplate>();
        /// <summary>
        /// Get defined schema templates.
        /// </summary>
        public List<AddressableAssetGroupSchemaTemplate> SchemaTemplates { get { return m_schemaTemplates; } }


        /// <summary>
        /// Remove  the schema at the specified index.
        /// </summary>
        /// <param name="index">The index to remove at.</param>
        /// <returns>True if the schema was removed.</returns>
        public bool RemoveSchemaTemplate(int index, bool postEvent = true)
        {
            if (index < 0 || index >= m_schemaTemplates.Count)
            {
                Debug.LogWarningFormat("Invalid index for schema template: {0}.", index);
                return false;
            }
            var s = m_schemaTemplates[index];
            m_schemaTemplates.RemoveAt(index);
            SetDirty(ModificationEvent.GroupSchemaRemoved, s, postEvent);
            return true;
        }


        [SerializeField]
        List<ScriptableObject> m_initializationObjects = new List<ScriptableObject>();
        /// <summary>
        /// List of ScriptableObjects that implement the IObjectInitializationDataProvider interface for providing runtime initialization.
        /// </summary>
        public List<ScriptableObject> InitializationObjects
        {
            get { return m_initializationObjects; }
        }

        /// <summary>
        /// Get the IObjectInitializationDataProvider at a specifc index.
        /// </summary>
        /// <param name="index">The index of the initialization object.</param>
        /// <returns>The initialization object at the specified index.</returns>
        public IObjectInitializationDataProvider GetInitializationObject(int index)
        {
            if (m_initializationObjects.Count == 0)
                return null;
            if (index < 0 || index >= m_initializationObjects.Count)
            {
                Debug.LogWarningFormat("Invalid index for data builder: {0}.", index);
                return null;
            }
            return m_initializationObjects[Mathf.Clamp(index, 0, m_initializationObjects.Count)] as IObjectInitializationDataProvider;
        }

        /// <summary>
        /// Adds an initialization object.
        /// </summary>
        /// <param name="initObject">The initialization object to add.</param>
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

            m_initializationObjects.Add(so);
            SetDirty(ModificationEvent.InitializationObjectAdded, so, postEvent);
            return true;
        }

        /// <summary>
        /// Remove the initialization object at the specified index.
        /// </summary>
        /// <param name="index">The index to remove.</param>
        /// <returns>True if the initialization object was removed.</returns>
        public bool RemoveInitializationObject(int index, bool postEvent = true)
        {
            if (m_initializationObjects.Count <= index)
                return false;
            var so = m_initializationObjects[index];
            m_initializationObjects.RemoveAt(index);
            SetDirty(ModificationEvent.InitializationObjectRemoved, so, postEvent);
            return true;
        }

        /// <summary>
        /// Sets the initialization object at the specified index.
        /// </summary>
        /// <param name="index">The index to set the initialization object.</param>
        /// <param name="initObject">The initialization object to set.  This must be a valid scriptable object that implements the IInitializationObject interface.</param>
        /// <returns>True if the initialization object was set, false otherwise.</returns>
        public bool SetInitializationObjectAtIndex(int index, IObjectInitializationDataProvider initObject, bool postEvent = true)
        {
            if (m_initializationObjects.Count <= index)
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

            m_initializationObjects[index] = so;
            SetDirty(ModificationEvent.InitializationObjectAdded, so, postEvent);
            return true;
        }


        [SerializeField]
        private int m_activePlayerDataBuilderIndex = 2;
        [SerializeField]
        private int m_activePlayModeDataBuilderIndex = 0;
        [SerializeField]
        private List<ScriptableObject> m_dataBuilders = new List<ScriptableObject>();
        /// <summary>
        /// List of ScriptableObjects that implement the IDataBuilder interface.  These are used to create data for editor play mode and for player builds.
        /// </summary>
        public List<ScriptableObject> DataBuilders { get { return m_dataBuilders; } }
        /// <summary>
        /// Get The data builder at a specifc index.
        /// </summary>
        /// <param name="index">The index of the builder.</param>
        /// <returns>The data builder at the specified index.</returns>
        public IDataBuilder GetDataBuilder(int index)
        {
            if (m_dataBuilders.Count == 0)
                return null;
            if (index < 0 || index >= m_dataBuilders.Count)
            {
                Debug.LogWarningFormat("Invalid index for data builder: {0}.", index);
                return null;
            }
            return m_dataBuilders[Mathf.Clamp(index, 0, m_dataBuilders.Count)] as IDataBuilder;
        }

        /// <summary>
        /// Adds a data builder.
        /// </summary>
        /// <param name="builder">The data builder to add.</param>
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

            m_dataBuilders.Add(so);
            SetDirty(ModificationEvent.DataBuilderAdded, so, postEvent);
            return true;
        }

        /// <summary>
        /// Remove the data builder at the sprcified index.
        /// </summary>
        /// <param name="index">The index to remove.</param>
        /// <returns>True if the builder was removed.</returns>
        public bool RemoveDataBuilder(int index, bool postEvent = true)
        {
            if (m_dataBuilders.Count <= index)
                return false;
            var so = m_dataBuilders[index];
            m_dataBuilders.RemoveAt(index);
            SetDirty(ModificationEvent.DataBuilderRemoved, so, postEvent);
            return true;
        }

        /// <summary>
        /// Sets the data builder at the specified index.
        /// </summary>
        /// <param name="index">The index to set the builder.</param>
        /// <param name="builder">The builder to set.  This must be a valid scriptable object that implements the IDataBuilder interface.</param>
        /// <returns>True if the builder was set, false otherwise.</returns>
        public bool SetDataBuilderAtIndex(int index, IDataBuilder builder, bool postEvent = true)
        {
            if (m_dataBuilders.Count <= index)
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

            m_dataBuilders[index] = so;
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
                return GetDataBuilder(m_activePlayerDataBuilderIndex);
            }
        }

        /// <summary>
        /// Get the active data builder for editor play mode data.
        /// </summary>
        public IDataBuilder ActivePlayModeDataBuilder
        {
            get
            {
                return GetDataBuilder(m_activePlayModeDataBuilderIndex);
            }
        }

        /// <summary>
        /// Get the index of the active player data builder.
        /// </summary>
        public int ActivePlayerDataBuilderIndex
        {
            get
            {
                return m_activePlayerDataBuilderIndex;
            }
            set
            {
                m_activePlayerDataBuilderIndex = value;
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
                return m_activePlayModeDataBuilderIndex;
            }
            set
            {
                m_activePlayModeDataBuilderIndex = value;
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
            m_labelTable.AddLabelName(label);
            SetDirty(ModificationEvent.LabelAdded, label, postEvent);
        }

        /// <summary>
        /// Remove a label by name.
        /// </summary>
        /// <param name="label">The label name.</param>
        /// <param name="postEvent">Send modification event.</param>
        public void RemoveLabel(string label, bool postEvent = true)
        {
            m_labelTable.RemoveLabelName(label);
            SetDirty(ModificationEvent.LabelRemoved, label, postEvent);
        }

        [SerializeField]
        string m_activeProfileId;
        /// <summary>
        /// The active profile id.
        /// </summary>
        public string activeProfileId
        {
            get
            {
                if (string.IsNullOrEmpty(m_activeProfileId))
                    m_activeProfileId = m_profileSettings.CreateDefaultProfile();
                return m_activeProfileId;
            }
            set
            {
                var oldVal = m_activeProfileId;
                m_activeProfileId = value;

                if (oldVal != value)
                {
                    SetDirty(ModificationEvent.ActiveProfileSet, value, true);
                }
            }
        }

        [SerializeField]
        private HostingServicesManager m_hostingServicesManager;
        /// <summary>
        /// Get the HostingServicesManager object.
        /// </summary>
        public HostingServicesManager HostingServicesManager
        {
            get
            {
                if (m_hostingServicesManager == null)
                    m_hostingServicesManager = new HostingServicesManager();

                if (!m_hostingServicesManager.IsInitialized)
                    m_hostingServicesManager.Initialize(this);

                return m_hostingServicesManager;
            }

            // For unit tests
            internal set { m_hostingServicesManager = value; }
        }

        /// <summary>
        /// Gets all asset entries from all groups.
        /// </summary>
        /// <param name="assets">The list of asset entries.</param>
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
            if (m_schemaTemplates == null)
                m_schemaTemplates = new List<AddressableAssetGroupSchemaTemplate>();
            if (m_schemaTemplates.Count == 0)
                AddSchemaTemplate("Packed Assets", "Pack asset sinto asset bundles.", typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            if (m_buildSettings == null)
                m_buildSettings = new AddressableAssetBuildSettings();
            if (m_profileSettings == null)
                m_profileSettings = new AddressableAssetProfileSettings();
            if (m_labelTable == null)
                m_labelTable = new LabelTable();
            if (string.IsNullOrEmpty(m_activeProfileId))
                m_activeProfileId = m_profileSettings.CreateDefaultProfile();
            if (m_dataBuilders == null || m_dataBuilders.Count == 0)
            {
                m_dataBuilders = new List<ScriptableObject>();
                m_dataBuilders.Add(CreateScriptAsset<BuildScriptFastMode>());
                m_dataBuilders.Add(CreateScriptAsset<BuildScriptVirtualMode>());
                m_dataBuilders.Add(CreateScriptAsset<BuildScriptPackedMode>());
            }
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
            AddressableAssetSettings aa = null;
            var path = configFolder + "/" + configName + ".asset";
            aa = isPersisted ? AssetDatabase.LoadAssetAtPath<AddressableAssetSettings>(path) : null;
            if (aa == null)
            {
                aa = CreateInstance<AddressableAssetSettings>();
                aa.m_isTemporary = !isPersisted;
                aa.activeProfileId = aa.profileSettings.Reset();
                aa.name = configName;

                if (isPersisted)
                {
                    Directory.CreateDirectory(configFolder);
                    AssetDatabase.CreateAsset(aa, path);
                }

                if (createDefaultGroups)
                {
                    var playerData = aa.CreateGroup(PlayerDataGroupName, false, false, false, typeof(PlayerDataGroupSchema));
                    var resourceEntry = aa.CreateOrMoveEntry(AddressableAssetEntry.ResourcesName, playerData);
                    resourceEntry.IsInResources = true;
                    aa.CreateOrMoveEntry(AddressableAssetEntry.EditorSceneListName, playerData);

                    var localGroup = aa.CreateGroup(DefaultLocalGroupName, true, false, false, typeof(ContentUpdateGroupSchema), typeof(BundledAssetGroupSchema));
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
        /// <param name="name">The display name of the template.</param>
        /// <param name="description">Tooltip text for the template.</param>
        /// <param name="types">The schema types for the template.</param>
        /// <returns>True if the template was added, false otherwise.</returns>
        public bool AddSchemaTemplate(string name, string description, params Type[] types)
        {
            if (string.IsNullOrEmpty(name))
            {
                Debug.LogWarningFormat("AddSchemaTemplate - Schema template must have a valid name.");
                return false;
            }
            if (types.Length == 0)
            {
                Debug.LogWarningFormat("AddSchemaTemplate - Schema template {0} must contain at least 1 schema type.", name);
                return false;
            }
            bool typesAreValid = true;
            for(int i = 0; i < types.Length; i++)
            {
                var t = types[i];
                if (t == null)
                {
                    Debug.LogWarningFormat("AddSchemaTemplate - Schema template {0} schema type at index {1} is null.", name, i);
                    typesAreValid = false;
                }
                if (!typeof(AddressableAssetGroupSchema).IsAssignableFrom(t))
                {
                    Debug.LogWarningFormat("AddSchemaTemplate - Schema template {0} schema type at index {1} must inherit from AddressableAssetGroupSchema.  Specified type was {2}.", name, i, t.FullName);
                    typesAreValid = false;
                }
            }
            if (!typesAreValid)
            {
                Debug.LogWarningFormat("AddSchemaTemplate - Schema template {0} must contains at least 1 invalid schema type.", name);
                return false;
            }
            m_schemaTemplates.Add(AddressableAssetGroupSchemaTemplate.Create(name, description, types));
            SetDirty(ModificationEvent.GroupSchemaTemplateAdded, m_schemaTemplates[m_schemaTemplates.Count - 1], true);
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
        /// <param name="name">The name of the group.</param>
        /// <returns>The group found or null.</returns>
        public AddressableAssetGroup FindGroup(string name)
        {
            return FindGroup(g => g.Name == name);
        }

        /// <summary>
        /// The default group.  This group is used when marking assets as addressable via the inspector.
        /// </summary>
        public AddressableAssetGroup DefaultGroup
        {
            get
            {
                if (string.IsNullOrEmpty(m_defaultGroup))
                {
                    //set to the first non readonly group if possible
                    foreach (var g in groups)
                    {
                        if (!g.ReadOnly)
                        {
                            m_defaultGroup = g.Guid;
                            break;
                        }
                    }
                    if (string.IsNullOrEmpty(m_defaultGroup))
                    {
                        Addressables.LogError("Attempting to access Default Addressables group but no valid group is available");
                        return null;
                    }
                }
                var group = groups.Find(s => s.Guid == m_defaultGroup);
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
                        group = CreateGroup("New Group", true, false, true);
                        group.AddSchema<BundledAssetGroupSchema>();
                    }
                }
                return group;
            }
            set
            {
                m_defaultGroup = value.Guid;
            }
        }

        private AddressableAssetEntry CreateEntry(string guid, string address, AddressableAssetGroup parent, bool readOnly, bool postEvent = true)
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
            var unityObj = eventData as UnityEngine.Object;
            if (unityObj != null && !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(unityObj)))
                EditorUtility.SetDirty(unityObj);

            if (IsPersisted)
                EditorUtility.SetDirty(this);

            m_cachedHash = default(Hash128);
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
                if (!dirInfo.Exists)
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
            SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entries, true);
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

            bool readOnly = true;
            var entry = FindAssetEntry(guid);
            if (entry == null)
            {
                entry = CreateEntry(guid, address, parentEntry.parentGroup, readOnly);
                entry.IsSubAsset = true;
                return entry;
            }
            else
            {
                //if the sub-entry already exists update it's info.  This mainly covers the case of dragging folders around.
                if (entry.IsSubAsset)
                {
                    entry.parentGroup = parentEntry.parentGroup;
                    entry.IsInResources = parentEntry.IsInResources;
                    entry.address = address;
                    entry.ReadOnly = readOnly;
                    return entry;
                }
            }
            return null;
        }

        /// <summary>
        /// Create a new asset group.
        /// </summary>
        /// <param name="groupName">The group name.</param>
        /// <param name="processorType">The processor type.</param>
        /// <param name="setAsDefaultGroup">Set the new group as the default group.</param>
        /// <param name="readOnly">Is the new group read only.</param>
        /// <param name="postEvent">Post modification event.</param>
        /// <returns>The newly created group.</returns>
        public AddressableAssetGroup CreateGroup(string groupName, bool setAsDefaultGroup, bool readOnly, bool postEvent, params Type[] types)
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
                    validName = cleanedName + index.ToString();
                    index++;
                }
            }

            return validName;
        }

        internal bool IsNotUniqueGroupName(string name)
        {
            bool foundExisting = false;
            foreach (var g in groups)
            {
                if (g.Name == name)
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
                        Debug.Log("You have moved addressable asset " + fileName + " into a Resources directory.  Thus we have un-marked it as Addressable. An asset cannot be both");
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
            aa.AssetsModifiedSinceLastPackedBuild = true;
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
    }
}
