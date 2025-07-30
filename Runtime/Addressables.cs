using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Serialization;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.AddressableAssets.ResourceProviders;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.ResourceManagement.Util;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Text;
using UnityEngine.Networking;
using UnityEngine.Scripting;


#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
#endif

namespace UnityEngine.AddressableAssets
{
    /// <summary>
    /// A container for data pertaining to a specific Resource Locator.  Used mainly to determine if a content catalog
    /// needs to be updated.
    /// </summary>
    public class ResourceLocatorInfo
    {
        /// <summary>
        /// The Resource Locator that has been loaded into the Addressables system.
        /// </summary>
        public IResourceLocator Locator { get; private set; }

        /// <summary>
        /// The local hash for this Resource Locator.  If a remote content catalog is updated and the remote hash changes,
        /// this locator info is used to determine if a new content catalog needs to be updated.
        /// </summary>
        public string LocalHash { get; private set; }

        /// <summary>
        /// The Content Catalog location this Resource Locator was loaded from.  Catalog locations typically contain
        /// exactly two dependencies.  The first dependency is the remote location of the content catalog hash file, the
        /// second is the local path of the hash file.
        /// </summary>
        public IResourceLocation CatalogLocation { get; private set; }

        internal bool ContentUpdateAvailable { get; set; }

        /// <summary>
        /// Contstruct a ResourceLocatorInfo for a given Resource Locator.
        /// </summary>
        /// <param name="loc">The IResourceLocator to track.</param>
        /// <param name="localHash">The local hash of the content catalog.</param>
        /// <param name="remoteCatalogLocation">The location for the remote catalog.  Typically this location contains exactly two dependeices,
        /// the first one pointing to the remote hash file.  The second dependency pointing to the local hash file.</param>
        public ResourceLocatorInfo(IResourceLocator loc, string localHash, IResourceLocation remoteCatalogLocation)
        {
            Locator = loc;
            LocalHash = localHash;
            CatalogLocation = remoteCatalogLocation;
        }

        /// <summary>
        /// The remote hash location of the content catalog used by the resource locator
        /// </summary>
        public IResourceLocation HashLocation
        {
            get { return CatalogLocation.Dependencies[0]; }
        }

        /// <summary>
        /// Checks to see if the provided CatalogLocation contains the expected amount of dependencies to check for catalog updates
        /// </summary>
        public bool CanUpdateContent
        {
            get { return !string.IsNullOrEmpty(LocalHash) && CatalogLocation != null && CatalogLocation.HasDependencies && CatalogLocation.Dependencies.Count == (int) ContentCatalogProvider.DependencyHashIndex.Count;
            }
        }

        internal void UpdateContent(IResourceLocator locator, string hash, IResourceLocation loc)
        {
            LocalHash = hash;
            CatalogLocation = loc;
            Locator = locator;
        }
    }

    /// <summary>
    /// Exception to encapsulate invalid key errors.
    /// </summary>
    public class InvalidKeyException : Exception
    {
        /// <summary>
        /// The key used to generate the exception.
        /// </summary>
        public object Key { get; private set; }

        /// <summary>
        /// The type of the key used to generate the exception.
        /// </summary>
        public Type Type { get; private set; }

        /// <summary>
        /// MergeMode if used, else null.
        /// </summary>
        public Addressables.MergeMode? MergeMode { get; }

        /// <summary>
        /// Construct a new InvalidKeyException.
        /// </summary>
        /// <param name="key">The key that caused the exception.</param>
        public InvalidKeyException(object key) : this(key, typeof(object))
        {
        }

        private AddressablesImpl m_Addressables;

        /// <summary>
        /// Construct a new InvalidKeyException.
        /// </summary>
        /// <param name="key">The key that caused the exception.</param>
        /// <param name="type">The type of the key that caused the exception.</param>
        public InvalidKeyException(object key, Type type)
        {
            Key = key;
            Type = type;
        }

        internal InvalidKeyException(object key, Type type, AddressablesImpl addr)
        {
            Key = key;
            Type = type;
            m_Addressables = addr;
        }

        /// <summary>
        /// Construct a new InvalidKeyException.
        /// </summary>
        /// <param name="key">The key that caused the exception.</param>
        /// <param name="type">The type of the key that caused the exception.</param>
        /// <param name="mergeMode">The mergeMode of the input that caused the exception.</param>
        public InvalidKeyException(object key, Type type, Addressables.MergeMode mergeMode)
        {
            Key = key;
            Type = type;
            MergeMode = mergeMode;
        }

        internal InvalidKeyException(object key, Type type, Addressables.MergeMode mergeMode, AddressablesImpl addr)
        {
            Key = key;
            Type = type;
            MergeMode = mergeMode;
            m_Addressables = addr;
        }

        ///<inheritdoc cref="InvalidKeyException"/>
        public InvalidKeyException()
        {
        }

        /// <summary>
        /// Construct a new InvalidKeyException.
        /// </summary>
        /// <param name="message">The message for the exception.</param>
        public InvalidKeyException(string message) : base(message)
        {
        }

        /// <summary>
        /// Construct a new InvalidKeyException.
        /// </summary>
        /// <param name="message">The message for the exception.</param>
        /// <param name="innerException">A message that describes what caused the exception.</param>
        public InvalidKeyException(string message, Exception innerException) : base(message, innerException)
        {
        }

        /// <summary>
        /// Construct a new InvalidKeyException.
        /// </summary>
        /// <param name="message">The message for the exception.</param>
        /// <param name="context">Describes the source and destination of a given serialized stream.</param>
        protected InvalidKeyException(SerializationInfo message, StreamingContext context) : base(message, context)
        {
        }


        internal const string BaseInvalidKeyMessageFormat = "{0}, Key={1}, Type={2}";
#if UNITY_EDITOR
        internal const string EditorNoLocationMessageFormat = "{0} No Location found for Key={1}. Asset exists in project at Path={2}, verify the asset is marked as Addressable.";
#endif
        internal const string NoLocationMessageFormat = "{0} No Location found for Key={1}";

        internal const string MultipleTypeMismatchMessageFormat = "{0} No Asset found for Key={1} with Type={2}. Key exists as multiple Types={3}, which is not assignable from the requested Type={2}";
        internal const string TypeMismatchMessageFormat = "{0} No Asset found for Key={1} with Type={2}. Key exists as Type={3}, which is not assignable from the requested Type={2}";
        internal const string MultipleTypesMessageFormat = "{0} Enumerable key contains multiple Types. {1}, all Keys are expected to be strings";

        internal const string MergeModeNoLocationMessageFormat = "\nNo Location found for Key={0}";
        internal const string NoMergeModeMessageFormat = "{0} No MergeMode is set to merge the multiple keys requested. {1}, Type={2}";
        internal const string MergeModeBaseMessageFormat = "{0} No {1} of Assets between {2} with Type={3}";
        internal const string UnionAvailableForKeysMessageFormat = "\nUnion of Type={0} found with {1}";
        internal const string UnionAvailableForKeysWithoutOtherMessageFormat = "\nUnion of Type={0} found with {1}. Without {2}";
        internal const string IntersectionAvailableMessageFormat = "\nAn Intersection exists for Type={0}";
        internal const string KeyAvailableAsTypeMessageFormat = "\nType={0} exists for {1}";

#if UNITY_EDITOR
        internal const string EditorGUIDKeyMessageFormat = "{0} No Location found for Key={1}. Asset exists in project at Path={2}, with a main Asset Type={3}";
#endif

        internal enum Format
        {
            StandardMessage = 0,

            NoMergeMode,
            MultipleTypesRequested,

            NoLocation,
            //return $"{base.Message} No Location found for Key={keyString}";

            // Single
            TypeMismatch,
            //return $"{base.Message} No Asset found for Key={keyString} with Type={Type}. Key exists as Type={availableType}, which is not assignable from the requested Type={Type}";
            MultipleTypeMismatch,
            //return $"{base.Message} No Asset found for Key={keyString} with Type={Type}. Key exists as multiple Types={csv}, which is not assignable from the requested Type={Type}";

            // merge
            MergeModeBase,
            //messageBuilder = new StringBuilder($"{base.Message} No {MergeMode.Value} of Assets between {keysCSV} with Type={Type}");

            // union
            UnionAvailableForKeys,
            //messageBuilder.Append($"\nUnion of Type={pair.Key} found with {availableKeysString}");
            UnionAvailableForKeysWithoutOther,
            //messageBuilder.Append($"\nUnion of Type={pair.Key} found with {availableKeysString}. Without {unavailableKeysString}");
            // intersection
            IntersectionAvailable,
            //messageBuilder.Append($"\nAn Intersection exists for Type={pair.Key}");
            // use first
            KeyAvailableAsType,
            // messageBuilder.Append($"\nType={pair.Key} exists for {keyCSV}");
        }

        internal string FormatMessage(Format format, string foundWithTypeString = null)
        {
            switch (format)
            {
                case Format.StandardMessage:
                    return string.Format(BaseInvalidKeyMessageFormat, base.Message, Key.ToString(), Type.FullName);

                case Format.MultipleTypesRequested:
                    var e = Key as IEnumerable;
                    string types = null;
                    foreach (var o in e)
                    {
                        if (types == null)
                            types = o.ToString();
                        else
                            types += ", " + o.ToString();
                    }
                    return string.Format(MultipleTypesMessageFormat, base.Message, types);
                case Format.NoLocation:
#if UNITY_EDITOR
                    string assetPath = AssetDatabase.GUIDToAssetPath(Key.ToString());
                    if (!string.IsNullOrEmpty(assetPath))
                    {
                        return string.Format(EditorNoLocationMessageFormat, base.Message, Key.ToString(), assetPath);
                    }
#endif
                    return string.Format(NoLocationMessageFormat, base.Message, Key.ToString());// $"{base.Message} No Location found for Key={keyString}";
                case Format.TypeMismatch:
                    return string.Format(TypeMismatchMessageFormat, base.Message, Key.ToString(), Type.FullName, foundWithTypeString);
                case Format.MultipleTypeMismatch:
                    return string.Format(MultipleTypeMismatchMessageFormat, base.Message, Key.ToString(), Type.FullName, foundWithTypeString);
                default:
                    throw new ArgumentOutOfRangeException(nameof(format), format, null);
            }
        }

        internal string FormatMergeModeMessage(Format format, string keysAvailable = null, string keysUnavailable = null, string typeString = null)
        {
            switch (format)
            {
                case Format.NoLocation:
                    return string.Format(MergeModeNoLocationMessageFormat, keysUnavailable == null ? GetKeyString() : keysUnavailable);

                // merge
                case Format.NoMergeMode:
                    return string.Format(NoMergeModeMessageFormat, base.Message, GetKeyString(), Type.FullName);
                case Format.MergeModeBase:
                    return string.Format(MergeModeBaseMessageFormat, base.Message, MergeMode.HasValue ? MergeMode.Value : Addressables.MergeMode.None, GetKeyString(), Type.FullName);

                    // union
                case Format.UnionAvailableForKeys:
                    return string.Format(UnionAvailableForKeysMessageFormat, typeString, keysAvailable);

                case Format.UnionAvailableForKeysWithoutOther:
                    return string.Format(UnionAvailableForKeysWithoutOtherMessageFormat, typeString, keysAvailable, keysUnavailable);
                    // intersection
                case Format.IntersectionAvailable:
                    return string.Format(IntersectionAvailableMessageFormat, typeString);
                    // use first
                case Format.KeyAvailableAsType:
                return string.Format(KeyAvailableAsTypeMessageFormat, typeString, keysAvailable);
                default:
                    throw new ArgumentOutOfRangeException(nameof(format), format, null);
            }
        }

        /// <summary>
        /// Stores information about the exception.
        /// </summary>
        public override string Message
        {
            get
            {
                string stringKey = Key as string;
                if (!string.IsNullOrEmpty(stringKey))
                {
                    if (m_Addressables == null)
                        return FormatMessage(Format.StandardMessage);
                    return GetMessageForSingleKey(stringKey);
                }

                IEnumerable enumerableKey = Key as IEnumerable;
                if (enumerableKey != null)
                {
                    int keyCount = 0;
                    List<string> stringKeys = new List<string>();
                    HashSet<string> keyTypeNames = new HashSet<string>();
                    foreach (object keyObj in enumerableKey)
                    {
                        keyCount++;
                        keyTypeNames.Add(keyObj.GetType().ToString());
                        if (keyObj is string)
                            stringKeys.Add(keyObj as string);
                    }

                    if (!MergeMode.HasValue)
                    {
                        string keysCSV = GetCSVString(stringKeys, "Key=", "Keys=");
                        FormatMergeModeMessage(Format.NoMergeMode);
                        return $"{base.Message} No MergeMode is set to merge the multiple keys requested. {keysCSV}, Type={Type}";
                    }
                    if (keyCount != stringKeys.Count)
                    {
                        string types = GetCSVString(keyTypeNames, "Type=", "Types=");
                        return FormatMessage(Format.MultipleTypesRequested, types);
                    }

                    if (keyCount == 1)
                        return GetMessageForSingleKey(stringKeys[0]);
                    return GetMessageforMergeKeys(stringKeys);
                }

                return FormatMessage(Format.StandardMessage);
            }
        }


        string GetMessageForSingleKey(string keyString)
        {
            HashSet<Type> typesAvailableForKey = GetTypesForKey(keyString);
            if (typesAvailableForKey.Count == 0)
            {
                return FormatNotFoundMessage(keyString);
            }

            if (typesAvailableForKey.Count == 1)
            {
                return FormatTypeNotAssignableMessage(keyString, typesAvailableForKey);
            }

            return FormatMultipleAssignableTypesMessage(keyString, typesAvailableForKey);
        }


            private string FormatNotFoundMessage(string keyString)
            {
                return FormatMessage(Format.NoLocation);
            }

#if UNITY_EDITOR
        private string FormatEditorTypeNotAssignableMessage(string keyString, string projectPath)
        {
            Type projectAssetType = string.IsNullOrEmpty(projectPath) ? null : AssetDatabase.GetMainAssetTypeAtPath(projectPath);
            if (projectAssetType != null)
                return string.Format(EditorGUIDKeyMessageFormat, base.Message, Key.ToString(), projectPath, projectAssetType.ToString());
            return string.Format(BaseInvalidKeyMessageFormat, base.Message, keyString, Type);
        }
#endif

        private string FormatTypeNotAssignableMessage(string keyString, HashSet<Type> typesAvailableForKey)
        {
            Type availableType = null;
            foreach (Type type in typesAvailableForKey)
                availableType = type;
            if (availableType == null)
                return FormatMessage(Format.StandardMessage);
            return FormatMessage(Format.TypeMismatch, availableType.ToString());
        }

        private string FormatMultipleAssignableTypesMessage(string keyString, HashSet<Type> typesAvailableForKey)
        {
            StringBuilder csv = new StringBuilder(512);
            int count = 0;
            foreach (Type type in typesAvailableForKey)
            {
                count++;
                csv.Append(count > 1 ? $", {type}" : type.ToString());
            }

            return FormatMessage(Format.MultipleTypeMismatch, csv.ToString());
        }

        string GetMessageforMergeKeys(List<string> keys)
        {
            StringBuilder messageBuilder = new StringBuilder(FormatMergeModeMessage(Format.MergeModeBase));
            switch (MergeMode)
            {
                case Addressables.MergeMode.Union:
                {
                    Dictionary<Type, List<string>> typeToKeys = new Dictionary<Type, List<string>>();
                    foreach (string key in keys)
                    {
                        if (!GetTypeToKeys(key, typeToKeys))
                            messageBuilder.Append(FormatMergeModeMessage(Format.NoLocation, keysUnavailable:key));
                    }

                    foreach (KeyValuePair<Type, List<string>> pair in typeToKeys)
                    {
                        string availableKeysString = GetCSVString(pair.Value, "Key=", "Keys=");
                        List<string> unavailableKeys = new List<string>();
                        foreach (string key in keys)
                        {
                            if (!pair.Value.Contains(key))
                                unavailableKeys.Add(key);
                        }

                        if (unavailableKeys.Count == 0)
                            messageBuilder.Append(FormatMergeModeMessage(Format.UnionAvailableForKeys, availableKeysString, null, pair.Key.ToString()));
                        else
                        {
                            string unavailableKeysString = GetCSVString(unavailableKeys, "Key=", "Keys=");
                            messageBuilder.Append(FormatMergeModeMessage(Format.UnionAvailableForKeysWithoutOther, availableKeysString, unavailableKeysString, pair.Key.ToString()));
                        }
                    }
                }
                    break;

                case Addressables.MergeMode.Intersection:
                {
                    bool hasInvalidKeys = false;
                    Dictionary<Type, List<string>> typeToKeys = new Dictionary<Type, List<string>>();
                    foreach (string key in keys)
                    {
                        if (!GetTypeToKeys(key, typeToKeys))
                        {
                            hasInvalidKeys = true;
                            messageBuilder.Append(FormatMergeModeMessage(Format.NoLocation, keysUnavailable:key));
                        }
                    }

                    if (hasInvalidKeys)
                        break;

                    foreach (KeyValuePair<Type, List<string>> pair in typeToKeys)
                    {
                        if (pair.Value.Count == keys.Count)
                            messageBuilder.Append(FormatMergeModeMessage(Format.IntersectionAvailable, null, null, pair.Key.ToString()));
                    }
                }
                    break;

                case Addressables.MergeMode.UseFirst:
                {
                    Dictionary<Type, List<string>> typeToKeys = new Dictionary<Type, List<string>>();
                    foreach (string key in keys)
                    {
                        if (!GetTypeToKeys(key, typeToKeys))
                            messageBuilder.Append(FormatMergeModeMessage(Format.NoLocation, keysUnavailable: key));
                    }

                    foreach (KeyValuePair<Type, List<string>> pair in typeToKeys)
                    {
                        foreach (string key in pair.Value)
                            messageBuilder.Append(FormatMergeModeMessage(Format.KeyAvailableAsType, key, null, pair.Key.ToString()));
                    }
                }
                    break;
            }

            return messageBuilder.ToString();
        }

        HashSet<Type> GetTypesForKey(string keyString)
        {
            HashSet<Type> typesAvailableForKey = new HashSet<Type>();
            foreach (var locator in m_Addressables.ResourceLocators)
            {
                if (!locator.Locate(keyString, null, out var locations))
                    continue;

                foreach (IResourceLocation location in locations)
                    typesAvailableForKey.Add(location.ResourceType);
            }

            return typesAvailableForKey;
        }

        bool GetTypeToKeys(string key, Dictionary<Type, List<string>> typeToKeys)
        {
            HashSet<Type> types = GetTypesForKey(key);
            if (types.Count == 0)
                return false;

            foreach (Type type in types)
            {
                if (!typeToKeys.TryGetValue(type, out List<string> keysForType))
                    typeToKeys.Add(type, new List<string>() {key});
                else
                    keysForType.Add(key);
            }

            return true;
        }

        internal string GetKeyString()
        {
            if (Key is string)
                return "Key=" + Key as string;
            IEnumerable enumerableKey = Key as IEnumerable;
            if (enumerableKey != null)
                return GetCSVString(enumerableKey, "Key=", "Keys=");

            return Key.ToString();
        }

        internal static string GetCSVString(IEnumerable enumerator, string prefixSingle, string prefixPlural)
        {
            StringBuilder keysCSVBuilder = new StringBuilder(prefixPlural);
            int count = 0;
            foreach (var key in enumerator)
            {
                count++;
                keysCSVBuilder.Append(count > 1 ? $", {key}" : key);
            }

            if (count == 1 && !string.IsNullOrEmpty(prefixPlural) && !string.IsNullOrEmpty(prefixSingle))
                keysCSVBuilder.Replace(prefixPlural, prefixSingle);
            return keysCSVBuilder.ToString();
        }
    }

    /// <summary>
    /// Entry point for Addressable API, this provides a simpler interface than using ResourceManager directly as it assumes string address type.
    /// </summary>
    public static class Addressables
    {
        internal static bool reinitializeAddressables = true;
        internal static AddressablesImpl m_AddressablesInstance = new AddressablesImpl(new DefaultAllocationStrategy());

        static AddressablesImpl m_Addressables
        {
            get
            {
#if UNITY_EDITOR
                // Addressables in the Editor can be reinitialized when entering or exiting playmode.
                // This waits until we are on the main thread so that calls like Addressables.Log don't
                // end up calling the reinitialization code and blowing up if they're being called on
                // a background thread.
                if (InternalEditorUtility.CurrentThreadIsMainThread() && reinitializeAddressables && EditorSettings.enterPlayModeOptionsEnabled)
                {
                    reinitializeAddressables = false;
                    m_AddressablesInstance.ReleaseSceneManagerOperation();
                    m_AddressablesInstance = new AddressablesImpl(new DefaultAllocationStrategy());
                }
#endif
                return m_AddressablesInstance;
            }
        }

        /// <summary>
        /// Returns the Addressables package version in Unity 2019.3 or newer
        /// </summary>
        public static string Version
        {
#if (UNITY_EDITOR)
            get => UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(Addressables).Assembly).version;
#else
            get => "";
#endif
        }

        /// <summary>
        /// Stores the ResourceManager associated with this Addressables instance.
        /// </summary>
        public static ResourceManager ResourceManager
        {
            get { return m_Addressables.ResourceManager; }
        }

        internal static AddressablesImpl Instance
        {
            get { return m_Addressables; }
        }

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        static void RegisterPlayModeStateChange()
        {
            EditorApplication.playModeStateChanged += SetAddressablesReInitFlagOnExitPlayMode;
        }

        static void SetAddressablesReInitFlagOnExitPlayMode(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredEditMode || change == PlayModeStateChange.ExitingPlayMode)
            {
                EditorApplication.delayCall -= EnableReinitializeAddressablesFlag;
                EditorApplication.delayCall += EnableReinitializeAddressablesFlag;
            }
        }

        static void EnableReinitializeAddressablesFlag()
        {
            reinitializeAddressables = true;
            EditorApplication.delayCall -= EnableReinitializeAddressablesFlag;
        }

#endif

        /// <summary>
        /// The Instance Provider used by the Addressables System.
        /// </summary>
        /// <remarks>
        /// Retrieves the interface used by the Addressables Asset System to create instances of Addressable GameObjects.
        /// </remarks>
        /// <value>The [IInstanceProvider](xref:UnityEngine.ResourceManagement.ResourceProviders.IInstanceProvider) object used to create instances of GameObjects.</value>
        /// <example>
        /// <para>
        /// The example below instantiates a GameObject using ProvideInstance.</para>
        /// <code source="../Tests/Editor/DocExampleCode/ScriptReference/UsingInstanceProvider.cs" region="SAMPLE"/>
        /// </example>
        public static IInstanceProvider InstanceProvider
        {
            get { return m_Addressables.InstanceProvider; }
        }

        /// <summary>
        /// Used to resolve a string using addressables config values
        /// </summary>
        /// <param name="id">The internal id to resolve.</param>
        /// <returns>Returns the string that the internal id represents.</returns>
        public static string ResolveInternalId(string id)
        {
            return m_Addressables.ResolveInternalId(id);
        }

        /// <inheritdoc cref="ResourceManager.InternalIdTransformFunc"/>
        static public Func<IResourceLocation, string> InternalIdTransformFunc
        {
            get { return m_Addressables.InternalIdTransformFunc; }
            set { m_Addressables.InternalIdTransformFunc = value; }
        }


        /// <inheritdoc cref="ResourceManager.WebRequestOverride"/>
        public static Action<UnityWebRequest> WebRequestOverride
        {
            get { return m_Addressables.WebRequestOverride; }
            set { m_Addressables.WebRequestOverride = value; }
        }

        /// <summary>
        /// Options for merging the results of requests.
        /// If keys (A, B) mapped to results ([1,2,4],[3,4,5])...
        ///  - UseFirst (or None) takes the results from the first key
        ///  -- [1,2,4]
        ///  - Union takes results of each key and collects items that matched any key.
        ///  -- [1,2,3,4,5]
        ///  - Intersection takes results of each key, and collects items that matched every key.
        ///  -- [4]
        /// </summary>
        public enum MergeMode
        {
            /// <summary>
            /// Use to indicate that no merge should occur. The first set of results will be used.
            /// </summary>
            None = 0,

            /// <summary>
            /// Use to indicate that the merge should take the first set of results.
            /// </summary>
            UseFirst = 0,

            /// <summary>
            /// Use to indicate that the merge should take the union of the results.
            /// </summary>
            Union,

            /// <summary>
            /// Use to indicate that the merge should take the intersection of the results.
            /// </summary>
            Intersection
        }

        /// <summary>
        /// The name of the PlayerPrefs value used to set the path to load the addressables runtime data file.
        /// </summary>
        public const string kAddressablesRuntimeDataPath = "AddressablesRuntimeDataPath";

        const string k_AddressablesLogConditional = "ADDRESSABLES_LOG_ALL";

        /// <summary>
        /// The name of the PlayerPrefs value used to set the path to check for build logs that need to be shown in the runtime.
        /// </summary>
        public const string kAddressablesRuntimeBuildLogPath = "AddressablesRuntimeBuildLog";

        /// <summary>
        /// The subfolder used by the Addressables system for its built data.
        /// </summary>
        /// <remarks>
        /// The Addressables system currently builds inside the library in a folder named 'aa'. Each platform that is built
        /// will be built inside a subfolder matching the platform name. This subfolder is also the base for loading
        /// streaming data at runtime. The Editor build location is <see cref="BuildPath"/> and player is <see cref="PlayerBuildDataPath"/>.
        /// </remarks>
        /// <example>
        /// <para>
        /// The example demonstrates a utility function that looks inside the Streaming Assets subfolder and gets a list of
        /// all the subfolders. This is returned as a list of all platforms that have been built on the current machine.</para>
        /// <code source="../Tests/Editor/DocExampleCode/ScriptReference/UsingStreamingAssetsSubFolder.cs" region="SAMPLE"/>
        /// </example>
        public static string StreamingAssetsSubFolder
        {
            get { return m_Addressables.StreamingAssetsSubFolder; }
        }

        /// <summary>
        /// The path to the Addressables Library subfolder
        /// </summary>
        public static string LibraryPath = "Library/com.unity.addressables/";

        /// <summary>
        /// The path to the Addressables Build Reports subfolder
        /// </summary>
        public static string BuildReportPath = "Library/com.unity.addressables/BuildReports/";

        /// <summary>
        /// The build path used by the Addressables system for its initialization data.
        /// </summary>
        /// <remarks>
        /// Addressables.BuildPath returns the path used by the Addressables system for its built initialization data.
        /// This path is to the Addressables folder in the Project Library folder for the active platform.This folder
        /// contains the settings, local catalog and addressables managed local asset bundles.
        /// </remarks>
        /// <example>
        /// <para>
        /// Gets the runtime settings from the buildPath and returns the Addressables package version that the content was built using.</para>
        /// <code source="../Tests/Editor/DocExampleCode/ScriptReference/UsingBuildPath.cs" region="GET_SETTINGS_FROM_BUILDPATH"/>
        ///<para>
        /// In this example we add a build pre-process hook. When building a player this throws an exception if the content has not been built.
        /// This could be useful when 'Build Addressables on Player Build' is not set in Addressables Settings.</para>
        /// <code source="../Tests/Editor/DocExampleCode/ScriptReference/ContentBuiltcheck.cs" region="CONTENT_BUILT_CHECK"/>
        /// </example>
        public static string BuildPath
        {
            get { return m_Addressables.BuildPath; }
        }

        /// <summary>
        /// The path that addressables player data gets copied to during a player build.
        /// </summary>
        /// <remarks>
        /// This resolves to Assets/StreamingAssets/aa by default. Files in this folder are included
        /// in the player build process. Remote bundles should be built outside this folder to prevent
        /// them from being included in the default player build.
        /// </remarks>
        /// <example>
        /// <para>This example is a custom build script that extends the default packed build script. Files are built
        /// in a custom build folder, and then copied into PlayerBuildDataPath so they are included with
        /// the player build.</para>
        /// <code source="../Tests/Editor/DocExampleCode/ScriptReference/UsingPlayerBuildDataPath.cs" region="SAMPLE"/>
        /// </example>
        public static string PlayerBuildDataPath
        {
            get { return m_Addressables.PlayerBuildDataPath; }
        }

        /// <summary>
        /// The path used by the Addressables system to load initialization data.
        /// </summary>
        /// <remarks>
        /// When running in the Editor Addressables.RuntimePath returns the path to the locally built data in the <see cref="Addressables.LibraryPath"/>.
        /// When running in a player this returns the path to the same content found in <see cref="Application.streamingAssetsPath"/>.
        /// This folder contains the settings, local catalog and Addressables managed local asset bundles.
        /// </remarks>
        [Preserve]
        public static string RuntimePath
        {
            get { return m_Addressables.RuntimePath; }
        }


        /// <summary>
        /// Gets the collection of configured <see cref="IResourceLocator"/> objects. Resource Locators are used to find <see cref="IResourceLocation"/> objects from user-defined typed keys.
        /// </summary>
        /// <value>The resource locators collection.</value>
        public static IEnumerable<IResourceLocator> ResourceLocators
        {
            get { return m_Addressables.ResourceLocators; }
        }

        [Conditional(k_AddressablesLogConditional)]
        internal static void InternalSafeSerializationLog(string msg, LogType logType = LogType.Log)
        {
            if (m_AddressablesInstance == null)
                return;
            switch (logType)
            {
                case LogType.Warning:
                    m_AddressablesInstance.LogWarning(msg);
                    break;
                case LogType.Error:
                    m_AddressablesInstance.LogError(msg);
                    break;
                case LogType.Log:
                    m_AddressablesInstance.Log(msg);
                    break;
            }
        }

        [Conditional(k_AddressablesLogConditional)]
        internal static void InternalSafeSerializationLogFormat(string format, LogType logType = LogType.Log, params object[] args)
        {
            if (m_AddressablesInstance == null)
                return;
            switch (logType)
            {
                case LogType.Warning:
                    m_AddressablesInstance.LogWarningFormat(format, args);
                    break;
                case LogType.Error:
                    m_AddressablesInstance.LogErrorFormat(format, args);
                    break;
                case LogType.Log:
                    m_AddressablesInstance.LogFormat(format, args);
                    break;
            }
        }

        /// <summary>
        /// Log can be used to write a Debug level log message.
        /// </summary>
        /// <remarks>
        /// Log works the same as <see cref="Debug.Log(object)">Debug.Log</see>. Addressables only logs warnings and errors so by default this function does not log anything.
        /// </remarks>
        /// <param name="msg">The msg to log</param>
        /// <example>
        /// <code source="../Tests/Editor/DocExampleCode/ScriptReference/UsingLog.cs" region="SAMPLE" />
        /// </example>
        /// <seealso href="xref:addressables-asset-settings">Enable all logging</seealso>
        [Conditional(k_AddressablesLogConditional)]
        public static void Log(string msg)
        {
            m_Addressables.Log(msg);
        }

        /// <summary>
        /// LogFormat can be used to write a formatted log message.
        /// </summary>
        /// <remarks>
        /// LogFormat supports Composite Formatting and works the same way as [Debug.LogFormat](xref:UnityEngine.Debug.LogFormat(System.String,System.Object[])). Addressables logs warnings and errors so by default this function will **not** log.
        /// </remarks>
        /// <seealso href="xref:addressables-asset-settings">Enable all logging</seealso>
        /// <param name="format">The string with format tags.</param>
        /// <param name="args">The args used to fill in the format tags.</param>
        /// <example>
        /// <code source="../Tests/Editor/DocExampleCode/ScriptReference/UsingLogFormat.cs" region="SAMPLE" />
        /// </example>
        [Conditional(k_AddressablesLogConditional)]
        public static void LogFormat(string format, params object[] args)
        {
            m_Addressables.LogFormat(format, args);
        }

        /// <summary>
        /// LogWarning can be used to write a log message.
        /// </summary>
        /// <remarks>
        /// LogWarning works the same way as [Debug.LogWarning](xref:UnityEngine.Debug.LogWarning(System.Object)). Addressables logs warnings and errors so by default this function will log.
        /// </remarks>
        /// <param name="msg">The msg to log</param>
        /// <example>
        /// <code source="../Tests/Editor/DocExampleCode/ScriptReference/UsingLogWarning.cs" region="SAMPLE" />
        /// </example>
        public static void LogWarning(string msg)
        {
            m_Addressables.LogWarning(msg);
        }

        /// <summary>
        /// LogFormat can be used to write a formatted log message.
        /// </summary>
        /// <remarks>
        /// LogWarningFormat supports Composite Formatting and works the same way as [Debug.LogWarningFormat](xref:UnityEngine.Debug.LogWarningFormat(System.String,System.Object[])). Addressables logs warnings and errors so by default this function will log.
        /// </remarks>
        /// <param name="format">The string with format tags.</param>
        /// <param name="args">The args used to fill in the format tags.</param>
        /// <example>
        /// <code source="../Tests/Editor/DocExampleCode/ScriptReference/UsingLogWarningFormat.cs" region="SAMPLE" />
        /// </example>
        public static void LogWarningFormat(string format, params object[] args)
        {
            m_Addressables.LogWarningFormat(format, args);
        }

        /// <summary>
        /// Write an error level log message.
        /// </summary>
        /// <remarks>
        /// LogError can be used to write an Error message. LogError works the same way as <see cref="Debug.LogError(object)" />. Addressables logs warnings and errors so by default this function will log.
        /// </remarks>
        /// <param name="msg">The msg to log</param>
        /// <example>
        /// <code source="../Tests/Editor/DocExampleCode/ScriptReference/UsingLogError.cs" region="SAMPLE"/>
        /// </example>
        /// <seealso href="xref:addressables-asset-settings">Enable all logging</seealso>
        public static void LogError(string msg)
        {
            m_Addressables.LogError(msg);
        }

        /// <summary>
        /// Write an exception as a log message.
        /// </summary>
        /// <remarks>
        /// LogException can be used to convert an exception to a log message. The exception is stringified. If the operation is in a failed state, the exception is logged at an Error logging level. If not the exception is logged at a Debug logging level.
        /// Addressables logs warnings and errors so if the operation is not in a failed state by default this function will not log.
        /// </remarks>
        /// <param name="op">The operation handle.</param>
        /// <param name="ex">The exception.</param>
        /// <example>
        /// <code source="../Tests/Editor/DocExampleCode/ScriptReference/UsingLogException.cs" region="SAMPLE_ASYNC_OP"/>
        /// </example>
        /// <seealso href="xref:addressables-asset-settings">Enable all logging</seealso>
        public static void LogException(AsyncOperationHandle op, Exception ex)
        {
            m_Addressables.LogException(op, ex);
        }

        /// <summary>
        /// Write an exception as a debug log message.
        /// </summary>
        /// <remarks>
        /// LogException can be used to convert an exception to a log message. The exception is stringified and logged at a Debug logging level. Addressables logs warnings and errors so by default this function will not log.
        /// </remarks>
        /// <param name="ex">The exception.</param>
        /// <example>
        /// <code source="../Tests/Editor/DocExampleCode/ScriptReference/UsingLogException.cs" region="SAMPLE"/>
        /// </example>
        public static void LogException(Exception ex)
        {
            m_Addressables.LogException(ex);
        }

        /// <summary>
        /// Write an error level formatted log message.
        /// </summary>
        /// <remarks>
        /// LogErrorFormat can be used to write a formatted Error level log message. LogErrorFormat supports Composite Formatting and works the same way as <see cref="Debug.LogErrorFormat(string, object[])" />. Addressables logs warnings and errors so by default this function will log.
        /// </remarks>
        /// <param name="format">The string with format tags.</param>
        /// <param name="args">The args used to fill in the format tags.</param>
        /// <example>
        /// <code source="../Tests/Editor/DocExampleCode/ScriptReference/UsingLogErrorFormat.cs" region="SAMPLE"/>
        /// </example>
        public static void LogErrorFormat(string format, params object[] args)
        {
            m_Addressables.LogErrorFormat(format, args);
        }

        /// <summary>
        /// Initialize the Addressables system, if needed.
        /// </summary>
        /// <remarks>
        /// The Addressables system initializes itself at runtime the first time you call an Addressables API function.
        /// You can call this function explicitly to initialize Addressables earlier. This function does nothing if
        /// initialization has already occurred.
        ///
        /// Other Addressables API functions such as <see cref="LoadAssetAsync">LoadAssetAsync</see> also automatically initializes the
        /// system if not already initialized. However in some cases you may wish to explicitly initalize Addressables,
        /// for example to check if the initialization process completed successfully before proceeding to load Addressables
        /// assets. Initializing explicitly shortens the execution time of the subsequent Addressables API function call
        /// because the initialization process is already completed.
        ///
        /// The initialization process loads configuration data and the local content catalog. Custom initialization
        /// tasks can also be included in this process, for example loading additional remote catalogs.
        /// See [Customizing Addressables Initialization](xref:addressables-api-initialize-async) for more information.
        ///
        /// The initialization process:
        /// * Sets up the <see cref="ResourceManager"/> and <see cref="ResourceLocators"/>
        /// * Loads the <see cref="Initialization.ResourceManagerRuntimeData"/> object, which is created by the Addressables build
        /// * Executes <see cref="IInitializableObject"/> operations
        /// * Optionally, checks for an updated content catalog (`true` by default)
        /// * Loads the content catalog
        ///
        /// The `Result` object contained in the <see cref="AsyncOperationHandle{TObject}"/> returned by this function
        /// contains a list of Addressable keys and a method that can be used to gather the <see cref="IResourceLocation"/>
        /// instances for a given key and asset type. You must access the `Result` object in a <see cref="AsyncOperationHandle{TObject}.Completed"/>
        /// event handler. To access the handle in a coroutine or Task-based function, pass `false` to the
        /// <see cref="InitializeAsync(bool)"/> overload of this function. Otherwise, the Addressables system
        /// releases the <see cref="AsyncOperationHandle{TObject}"/> object before control returns to your code.
        /// </remarks>
        /// <returns>AsyncOperationHandle that is used to check when the operation has completed. The result of the operation is an [IResourceLocator](xref:UnityEngine.AddressableAssets.ResourceLocators.IResourceLocator)  object.</returns>
        /// <example>
        /// <para>
        /// The following script loads all Addressable assets referenced in the catalog.</para>
        /// <code source="../Tests/Editor/DocExampleCode/ScriptReference/UsingInitializeAsync.cs" region="SAMPLE_LOADALL"/>
        /// </example>
        /// <seealso href="xref:addressables-api-load-content-catalog-async">Managing catalogs at runtime</seealso>
        public static AsyncOperationHandle<IResourceLocator> InitializeAsync()
        {
            return m_Addressables.InitializeAsync();
        }

        /// <summary>
        /// Initialize the Addressables system, if needed.
        /// </summary>
        /// <param name="autoReleaseHandle">Determines if the handle should be released on completion, or manually released.</param>
        /// <returns>AsyncOperationHandle that is used to check when the operation has completed. The result of the operation is an [IResourceLocator](xref:UnityEngine.AddressableAssets.ResourceLocators.IResourceLocator)  object.</returns>
        public static AsyncOperationHandle<IResourceLocator> InitializeAsync(bool autoReleaseHandle)
        {
            return m_Addressables.InitializeAsync(autoReleaseHandle);
        }

        /// <summary>
        /// Additively load catalogs from runtime data.
        /// </summary>
        /// <remarks>
        /// You can cache content catalog by providing the hash file created for the catalog by the Addressables content build
        /// at the same URL as the catalog JSON file. The Addressables system uses this hash file to determine if the cached catalog
        /// needs to be updated. If the value in the hash file has not changed since the last time you loaded the same catalog,
        /// this function loads the cached version instead of downloading the catalog. If the hash value has changed or if no
        /// hash file is provided, Addressables downloads the catalog from the specified path before loading it into memory.
        ///
        /// See also: [Managing catalogs at runtime](xref:addressables-api-load-content-catalog-async)
        /// </remarks>
        /// <param name="catalogPath">The path to the runtime data.</param>
        /// <param name="providerSuffix">This value, if not null or empty, will be appended to all provider ids loaded from this data.</param>
        /// <returns>The operation handle for the request.</returns>
        public static AsyncOperationHandle<IResourceLocator> LoadContentCatalogAsync(string catalogPath, string providerSuffix = null)
        {
            return m_Addressables.LoadContentCatalogAsync(catalogPath, false, providerSuffix);
        }

        /// <summary>
        /// Additively load catalogs from runtime data.
        /// </summary>
        /// <remarks>
        /// You can cache content catalog by providing the hash file created for the catalog by the Addressables content build
        /// at the same URL as the catalog JSON file. The Addressables system uses this hash file to determine if the cached catalog
        /// needs to be updated. If the value in the hash file has not changed since the last time you loaded the same catalog,
        /// this function loads the cached version instead of downloading the catalog. If the hash value has changed or if no
        /// hash file is provided, Addressables downloads the catalog from the specified path before loading it into memory.
        ///
        /// See also: [Managing catalogs at runtime](xref:addressables-api-load-content-catalog-async)
        /// </remarks>
        /// <param name="catalogPath">The path to the runtime data.</param>
        /// <param name="autoReleaseHandle">If true, the async operation handle will be automatically released on completion. Typically,
        /// there is no reason to hold on to the handle for this operation.</param>
        /// <param name="providerSuffix">This value, if not null or empty, will be appended to all provider ids loaded from this data.</param>
        /// <returns>The operation handle for the request.</returns>
        public static AsyncOperationHandle<IResourceLocator> LoadContentCatalogAsync(string catalogPath, bool autoReleaseHandle, string providerSuffix = null)
        {
            return m_Addressables.LoadContentCatalogAsync(catalogPath, autoReleaseHandle, providerSuffix);
        }

        /// <summary>
        /// Loads a single Addressable asset identified by an <see cref="IResourceLocation"/>.
        /// </summary>
        /// <remarks>
        /// Loads an Addressable asset. If a `key` references multiple assets (i.e. a label that is assigned to multiple assets), only the first asset found will be loaded.
        ///
        /// When you load an Addressable asset, the system:
        /// * Gathers the asset's dependencies
        /// * Downloads any remote AssetBundles needed to load the asset or its dependencies
        /// * Loads the AssetBundles into memory
        /// * Populates the `Result` object of the <see cref="AsyncOperationHandle{TObject}"/> instance returned by this function.
        /// </remarks>
        /// <typeparam name="TObject">The type of the asset.</typeparam>
        /// <param name="location">The location of the asset.</param>
        /// <returns>AsyncOperationHandle that is used to check when the operation has completed. The result of the operation is the loaded asset of the type `TObject`.</returns>
        /// <seealso href="xref:addressables-api-load-asset-async">Loading Addressable Assets</seealso>
        /// <seealso href="xref:addressables-async-operation-handling">Operations</seealso>
        /// <example>
        /// <para>
        /// The example below loads a material using an <see cref="IResourceLocation"/>. The loaded material is assigned to a GameObject.
        /// A reference to the operation handle is stored and can be released via <see cref="Addressables.Release(AsyncOperationHandle)"/>.</para>
        /// <code source="../Tests/Editor/DocExampleCode/ScriptReference/UsingLoadAssetAsync.cs" region="SAMPLE_LOCATION"/>
        /// <para>
        /// The example below loads a material using a `key`, specifically an <see cref="AssetReference"/>. The loaded material is assigned to a GameObject. A reference to the
        /// operation handle is stored and can be released via <see cref="Addressables.Release(AsyncOperationHandle)"/>.</para>
        /// <code source="../Tests/Editor/DocExampleCode/ScriptReference/UsingLoadAssetAsync.cs" region="SAMPLE_KEY"/>
        /// </example>
        public static AsyncOperationHandle<TObject> LoadAssetAsync<TObject>(IResourceLocation location)
        {
            return m_Addressables.LoadAssetAsync<TObject>(location);
        }

        /// <summary>
        /// Loads a single Addressable asset identified by an <see cref="IResourceLocation"/>.
        /// </summary>
        /// <typeparam name="TObject">The type of the asset.</typeparam>
        /// <param name="key">The key of the location of the asset.</param>
        /// <returns>AsyncOperationHandle that is used to check when the operation has completed. The result of the operation is the loaded asset of the type `TObject`.</returns>
        public static AsyncOperationHandle<TObject> LoadAssetAsync<TObject>(object key)
        {
            return m_Addressables.LoadAssetAsync<TObject>(key);
        }

        /// <summary>
        /// Loads the resource locations specified by a set of keys.
        /// </summary>
        /// <remarks>
        /// The operation always completes successfully and the operation handle's `Result` object always contains a valid IList instance.
        /// If no assets matched the specified keys, the list in `Result` is empty.
        ///
        /// See [Loading assets by location](xref:addressables-api-load-asset-async) for more information.
        ///
        /// See [Operations](xref:addressables-async-operation-handling) for information on handling the asynchronous operations used
        /// to load Addressable assets.
        /// </remarks>
        /// <param name="keys">The set of keys to use.</param>
        /// <param name="mode">The mode for merging the results of the found locations.</param>
        /// <param name="type">A type restriction for the lookup.  Only locations of the provided type (or derived type) will be returned.</param>
        /// <returns>The operation handle for the request.</returns>
        public static AsyncOperationHandle<IList<IResourceLocation>> LoadResourceLocationsAsync(IEnumerable keys, MergeMode mode, Type type = null)
        {
            return m_Addressables.LoadResourceLocationsAsync(keys, mode, type);
        }

        /// <summary>
        /// Loads the resource locations specified by a key.
        /// </summary>
        /// <remarks>
        /// The operation always completes successfully and the operation handle's `Result` object always contains a valid IList instance.
        /// If no assets matched the specified key, the list in `Result` is empty.
        ///
        /// See [Loading assets by location](xref:addressables-api-load-asset-async) for more information.
        ///
        /// See [Operations](xref:addressables-async-operation-handling) for information on handling the asynchronous operations used
        /// to load Addressable assets.
        /// </remarks>
        /// <param name="key">The key for the locations.</param>
        /// <param name="type">A type restriction for the lookup.  Only locations of the provided type (or derived type) will be returned.</param>
        /// <returns>The operation handle for the request.</returns>
        public static AsyncOperationHandle<IList<IResourceLocation>> LoadResourceLocationsAsync(object key, Type type = null)
        {
            return m_Addressables.LoadResourceLocationsAsync(key, type);
        }

        /// <summary>
        /// Loads multiple assets, based on the list of locations provided.
        /// </summary>
        /// <remarks>
        /// When you load Addressable assets, the system:
        /// * Gathers the dependencies of the asset
        /// * Downloads any remote AssetBundles needed to load the assets or their dependencies
        /// * Loads the AssetBundles into memory
        /// * Populates the `Result` object of the <see cref="AsyncOperationHandle{TObject}"/> instance returned by this function
        ///
        /// Use the `Result` object to access the loaded assets.
        ///
        /// If any assets cannot be loaded, the entire operation fails. The operation releases any assets and dependencies it had already loaded.
        /// The `Status` of the operation handle is set to <see cref="AsyncOperationStatus.Failed"/> and the `Result` is set to null.
        ///
        /// See the [Loading Addressable Assets](xref:addressables-api-load-asset-async) documentation for more details.
        ///
        /// See [Operations](xref:addressables-async-operation-handling) for information on handling the asynchronous operations used
        /// to load Addressable assets.
        /// </remarks>
        /// <typeparam name="TObject">The type of the assets.</typeparam>
        /// <param name="locations">The locations of the assets.</param>
        /// <param name="callback">Callback Action that is called per load operation.</param>
        /// <returns>The operation handle for the request.</returns>
        public static AsyncOperationHandle<IList<TObject>> LoadAssetsAsync<TObject>(IList<IResourceLocation> locations, Action<TObject> callback)
        {
            return m_Addressables.LoadAssetsAsync(locations, callback, true);
        }

        /// <summary>
        /// Loads multiple assets, based on the list of locations provided.
        /// </summary>
        /// <remarks>
        /// When you load Addressable assets, the system:
        /// * Gathers the dependencies of the assets
        /// * Downloads any remote AssetBundles needed to load the assets or their dependencies
        /// * Loads the AssetBundles into memory
        /// * Populates the `Result` object of the <see cref="AsyncOperationHandle{TObject}"/> instance returned by this function
        ///
        /// Use the `Result` object to access the loaded assets.
        ///
        /// If any assets cannot be loaded, the entire operation fails. The operation releases any assets and dependencies it had already loaded.
        /// The `Status` of the operation handle is set to <see cref="AsyncOperationStatus.Failed"/> and the `Result` is set to null.
        ///
        /// See the [Loading Addressable Assets](xref:addressables-api-load-asset-async) documentation for more details.
        ///
        /// See [Operations](xref:addressables-async-operation-handling) for information on handling the asynchronous operations used
        /// to load Addressable assets.
        /// </remarks>
        /// <typeparam name="TObject">The type of the assets.</typeparam>
        /// <param name="locations">The locations of the assets.</param>
        /// <param name="callback">Callback Action that is called per load operation.</param>
        /// <param name="releaseDependenciesOnFailure">
        /// If all matching locations succeed, this parameter is ignored.
        /// When true, if any assets cannot be loaded, the entire operation fails. The operation releases any assets and dependencies it had already loaded.
        /// The `Status` of the operation handle is set to <see cref="AsyncOperationStatus.Failed"/> and the `Result` is set to null.
        ///
        /// When false, if any matching location fails, the `Result` instance in the returned operation handle contains an IList of size equal to the number of
        /// locations that the operation attempted to load. The entry in the result list corresponding to a location that failed to load is null.
        /// The entries for locations that successfully loaded are set to a valid TObject. The `Status` of the operation handle is still <see cref="AsyncOperationStatus.Failed"/>
        /// if any single asset failed to load.
        ///
        /// When <paramref name="releaseDependenciesOnFailure"/> is true, you do not need to release the <see cref="AsyncOperationHandle"/> instance on failure.
        /// When false, you must always release it.
        /// </param>
        /// <returns>The operation handle for the request.</returns>
        public static AsyncOperationHandle<IList<TObject>> LoadAssetsAsync<TObject>(IList<IResourceLocation> locations, Action<TObject> callback, bool releaseDependenciesOnFailure)
        {
            return m_Addressables.LoadAssetsAsync(locations, callback, releaseDependenciesOnFailure);
        }

        /// <summary>
        /// Loads multiple assets identified by a list of keys.
        /// </summary>
        /// <remarks>
        /// The keys in <paramref name="keys"/> are translated into lists of locations, which are merged into a single list based on
        /// the value in <paramref name="mode"/>.
        ///
        /// When you load Addressable assets, the system:
        /// * Gathers the dependencies of the assets
        /// * Downloads any remote AssetBundles needed to load the assets or their dependencies
        /// * Loads the AssetBundles into memory
        /// * Populates the `Result` object of the <see cref="AsyncOperationHandle{TObject}"/> instance returned by this function
        ///
        /// Use the `Result` object to access the loaded assets.
        ///
        /// If any assets cannot be loaded, the entire operation fails. The operation releases any assets and dependencies it had already loaded.
        /// The `Status` of the operation handle is set to <see cref="AsyncOperationStatus.Failed"/> and the `Result` is set to null.
        ///
        /// See the [Loading Addressable Assets](xref:addressables-api-load-asset-async) documentation for more details.
        ///
        /// See [Operations](xref:addressables-async-operation-handling) for information on handling the asynchronous operations used
        /// to load Addressable assets.
        /// </remarks>
        /// <typeparam name="TObject">The type of the assets.</typeparam>
        /// <param name="keys">List of keys for the locations.</param>
        /// <param name="callback">Callback Action that is called per load operation.</param>
        /// <param name="mode">Method for merging the results of key matches.  See <see cref="MergeMode"/> for specifics</param>
        /// <returns>The operation handle for the request.</returns>
        public static AsyncOperationHandle<IList<TObject>> LoadAssetsAsync<TObject>(IEnumerable keys, Action<TObject> callback, MergeMode mode)
        {
            return m_Addressables.LoadAssetsAsync(keys, callback, mode, true);
        }

        /// <summary>
        /// Loads multiple assets identified by a key.
        /// </summary>
        /// <remarks>
        /// The key in <paramref name="key"/> is translated into lists of locations, which are merged into a single list based on
        /// the value in <paramref name="mode"/>.
        ///
        /// When you load Addressable assets, the system:
        /// * Gathers the dependencies of the assets
        /// * Downloads any remote AssetBundles needed to load the assets or their dependencies
        /// * Loads the AssetBundles into memory
        /// * Populates the `Result` object of the <see cref="AsyncOperationHandle{TObject}"/> instance returned by this function
        ///
        /// Use the `Result` object to access the loaded assets.
        ///
        /// If any assets cannot be loaded, the entire operation fails. The operation releases any assets and dependencies it had already loaded.
        /// The `Status` of the operation handle is set to <see cref="AsyncOperationStatus.Failed"/> and the `Result` is set to null.
        ///
        /// See the [Loading Addressable Assets](xref:addressables-api-load-asset-async) documentation for more details.
        ///
        /// See [Operations](xref:addressables-async-operation-handling) for information on handling the asynchronous operations used
        /// to load Addressable assets.
        /// </remarks>
        /// <typeparam name="TObject">The type of the assets.</typeparam>
        /// <param name="key">A key string to identify the locations.</param>
        /// <param name="callback">Callback Action that is called per load operation.</param>
        /// <returns>The operation handle for the request.</returns>
        public static AsyncOperationHandle<IList<TObject>> LoadAssetsAsync<TObject>(string key, Action<TObject> callback = null)
        {
            return m_Addressables.LoadAssetsAsync(new List<string>() { key }, callback, MergeMode.None, true);
        }

        /// <summary>
        /// Loads multiple assets, identified by a set of keys.
        /// </summary>
        /// <remarks>
        /// The keys in <paramref name="keys"/> are translated into lists of locations, which are merged into a single list based on
        /// the value in <paramref name="mode"/>.
        ///
        /// When you load Addressable assets, the system:
        /// * Gathers the dependencies of the assets
        /// * Downloads any remote AssetBundles needed to load the assets or their dependencies
        /// * Loads the AssetBundles into memory
        /// * Populates the `Result` object of the <see cref="AsyncOperationHandle{TObject}"/> instance returned by this function
        ///
        /// Use the `Result` object to access the loaded assets.
        ///
        /// If any assets cannot be loaded, the entire operation fails. The operation releases any assets and dependencies it had already loaded.
        /// The `Status` of the operation handle is set to <see cref="AsyncOperationStatus.Failed"/> and the `Result` is set to null.
        ///
        /// See the [Loading Addressable Assets](xref:addressables-api-load-asset-async) documentation for more details.
        ///
        /// See [Operations](xref:addressables-async-operation-handling) for information on handling the asynchronous operations used
        /// to load Addressable assets.
        /// </remarks>
        /// <typeparam name="TObject">The type of the assets.</typeparam>
        /// <param name="keys">IEnumerable set of keys for the locations.</param>
        /// <param name="callback">Callback Action that is called per load operation.</param>
        /// <param name="mode">Method for merging the results of key matches.  See <see cref="MergeMode"/> for specifics</param>
        /// <param name="releaseDependenciesOnFailure">
        /// If all matching locations succeed, this parameter is ignored.
        ///
        /// When true, if any assets cannot be loaded, the entire operation fails. The operation releases any assets and dependencies it had already loaded.
        /// The `Status` of the operation handle is set to <see cref="AsyncOperationStatus.Failed"/> and the `Result` is set to null.
        ///
        /// When false, if any matching location fails, the `Result` instance in the returned operation handle contains an IList of size equal to the number of
        /// locations that the operation attempted to load. The entry in the result list corresponding to a location that failed to load is null.
        /// The entries for locations that successfully loaded are set to a valid TObject. The `Status` of the operation handle is still <see cref="AsyncOperationStatus.Failed"/>
        /// if any single asset failed to load.
        ///
        /// When <paramref name="releaseDependenciesOnFailure"/> is true, you do not need to release the <see cref="AsyncOperationHandle"/> instance on failure.
        /// When false, you must always release it.
        /// </param>
        /// <returns>The operation handle for the request.</returns>
        public static AsyncOperationHandle<IList<TObject>> LoadAssetsAsync<TObject>(IEnumerable keys, Action<TObject> callback, MergeMode mode, bool releaseDependenciesOnFailure)
        {
            return m_Addressables.LoadAssetsAsync(keys, callback, mode, releaseDependenciesOnFailure);
        }

        /// <summary>
        /// Loads multiple assets, identified by a key.
        /// </summary>
        /// <remarks>
        /// The key in <paramref name="key"/> is translated into lists of locations, which are merged into a single list based on
        ///
        /// When you load Addressable assets, the system:
        /// * Gathers the dependencies of the assets
        /// * Downloads any remote AssetBundles needed to load the assets or their dependencies
        /// * Loads the AssetBundles into memory
        /// * Populates the `Result` object of the <see cref="AsyncOperationHandle{TObject}"/> instance returned by this function
        ///
        /// Use the `Result` object to access the loaded assets.
        ///
        /// If any assets cannot be loaded, the entire operation fails. The operation releases any assets and dependencies it had already loaded.
        /// The `Status` of the operation handle is set to <see cref="AsyncOperationStatus.Failed"/> and the `Result` is set to null.
        ///
        /// See the [Loading Addressable Assets](xref:addressables-api-load-asset-async) documentation for more details.
        ///
        /// See [Operations](xref:addressables-async-operation-handling) for information on handling the asynchronous operations used
        /// to load Addressable assets.
        /// </remarks>
        /// <typeparam name="TObject">The type of the assets.</typeparam>
        /// <param name="key">Key used to gather the resource locations.</param>
        /// <param name="callback">Callback Action that is called per load operation.</param>
        /// <param name="releaseDependenciesOnFailure">
        /// If all matching locations succeed, this parameter is ignored.
        ///
        /// When true, if any assets cannot be loaded, the entire operation fails. The operation releases any assets and dependencies it had already loaded.
        /// The `Status` of the operation handle is set to <see cref="AsyncOperationStatus.Failed"/> and the `Result` is set to null.
        ///
        /// When false, if any matching location fails, the `Result` instance in the returned operation handle contains an IList of size equal to the number of
        /// locations that the operation attempted to load. The entry in the result list corresponding to a location that failed to load is null.
        /// The entries for locations that successfully loaded are set to a valid TObject. The `Status` of the operation handle is still <see cref="AsyncOperationStatus.Failed"/>
        /// if any single asset failed to load.
        ///
        /// When <paramref name="releaseDependenciesOnFailure"/> is true, you do not need to release the <see cref="AsyncOperationHandle"/> instance on failure.
        /// When false, you must always release it.
        /// </param>
        /// <returns>The operation handle for the request.</returns>
        public static AsyncOperationHandle<IList<TObject>> LoadAssetsAsync<TObject>(string key, bool releaseDependenciesOnFailure, Action<TObject> callback = null)
        {
            return m_Addressables.LoadAssetsAsync(new List<string>() { key }, callback, MergeMode.None, releaseDependenciesOnFailure);
        }

        /// <summary>
        /// Loads multiple assets identified by a single key.
        /// </summary>
        /// <remarks>
        /// The key in <paramref name="key"/> is translated into a list of locations that are then loaded.
        ///
        /// When you load Addressable assets, the system:
        /// * Gathers the dependencies of the assets
        /// * Downloads any remote AssetBundles needed to load the assets or their dependencies
        /// * Loads the AssetBundles into memory
        /// * Populates the `Result` object of the <see cref="AsyncOperationHandle{TObject}"/> instance returned by this function
        ///
        /// Use the `Result` object to access the loaded assets.
        ///
        /// If any assets cannot be loaded, the entire operation fails. The operation releases any assets and dependencies it had already loaded.
        /// The `Status` of the operation handle is set to <see cref="AsyncOperationStatus.Failed"/> and the `Result` is set to null.
        ///
        /// See the [Loading Addressable Assets](xref:addressables-api-load-asset-async) documentation for more details.
        ///
        /// See [Operations](xref:addressables-async-operation-handling) for information on handling the asynchronous operations used
        /// to load Addressable assets.
        /// </remarks>
        /// <typeparam name="TObject">The type of the assets.</typeparam>
        /// <param name="key">Key for the locations.</param>
        /// <param name="callback">Callback Action that is called per load operation.</param>
        /// <returns>The operation handle for the request.</returns>
        public static AsyncOperationHandle<IList<TObject>> LoadAssetsAsync<TObject>(object key, Action<TObject> callback)
        {
            return m_Addressables.LoadAssetsAsync(key, callback, true);
        }

        /// <summary>
        /// Loads multiple assets identified by a single key.
        /// </summary>
        /// <remarks>
        /// The key in <paramref name="key"/> is translated into a list of locations that are then loaded.
        ///
        /// When you load Addressable assets, the system:
        /// * Gathers the dependencies of the assets
        /// * Downloads any remote AssetBundles needed to load the assets or their dependencies
        /// * Loads the AssetBundles into memory
        /// * Populates the `Result` object of the <see cref="AsyncOperationHandle{TObject}"/> instance returned by this function
        ///
        /// Use the `Result` object to access the loaded assets.
        ///
        /// See the [Loading Addressable Assets](xref:addressables-api-load-asset-async) documentation for more details.
        ///
        /// See [Operations](xref:addressables-async-operation-handling) for information on handling the asynchronous operations used
        /// to load Addressable assets.
        /// </remarks>
        /// <typeparam name="TObject">The type of the assets.</typeparam>
        /// <param name="key">Key for the locations.</param>
        /// <param name="callback">Callback Action that is called per load operation (per loaded asset).</param>
        /// <param name="releaseDependenciesOnFailure">
        /// If all matching locations succeed, this parameter is ignored.
        ///
        /// When true, if any assets cannot be loaded, the entire operation fails. The operation releases any assets and dependencies it had already loaded.
        /// The `Status` of the operation handle is set to <see cref="AsyncOperationStatus.Failed"/> and the `Result` is set to null.
        ///
        /// When false, if any matching location fails, the `Result` instance in the returned operation handle contains an IList of size equal to the number of
        /// locations that the operation attempted to load. The entry in the result list corresponding to a location that failed to load is null.
        /// The entries for locations that successfully loaded are set to a valid TObject. The `Status` of the operation handle is still <see cref="AsyncOperationStatus.Failed"/>
        /// if any single asset failed to load.
        ///
        /// When <paramref name="releaseDependenciesOnFailure"/> is true, you do not need to release the <see cref="AsyncOperationHandle"/> instance on failure.
        /// When false, you must always release it.
        /// </param>
        /// <returns>The operation handle for the request.</returns>
        public static AsyncOperationHandle<IList<TObject>> LoadAssetsAsync<TObject>(object key, Action<TObject> callback, bool releaseDependenciesOnFailure)
        {
            return m_Addressables.LoadAssetsAsync(key, callback, releaseDependenciesOnFailure);
        }

        /// <summary>
        /// Release asset.
        /// </summary>
        /// <typeparam name="TObject">The type of the object being released</typeparam>
        /// <param name="obj">The asset to release.</param>
        public static void Release<TObject>(TObject obj)
        {
            m_Addressables.Release(obj);
        }

        /// <summary>
        /// Release the operation and its associated resources.
        /// </summary>
        /// <typeparam name="TObject">The type of the AsyncOperationHandle being released</typeparam>
        /// <param name="handle">The operation handle to release.</param>
        public static void Release<TObject>(AsyncOperationHandle<TObject> handle)
        {
            handle.Release();
        }

        /// <summary>
        /// Release the operation and its associated resources.
        /// </summary>
        /// <param name="handle">The operation handle to release.</param>
        public static void Release(AsyncOperationHandle handle)
        {
            handle.Release();
        }

        /// <summary>
        /// Releases and destroys an object that was created via Addressables.InstantiateAsync.
        /// </summary>
        /// <param name="instance">The GameObject instance to be released and destroyed.</param>
        /// <returns>Returns true if the instance was successfully released.</returns>
        public static bool ReleaseInstance(GameObject instance)
        {
            return m_Addressables.ReleaseInstance(instance);
        }

        /// <summary>
        /// Releases and destroys an object that was created via Addressables.InstantiateAsync.
        /// </summary>
        /// <param name="handle">The handle to the game object to destroy, that was returned by InstantiateAsync.</param>
        /// <returns>Returns true if the instance was successfully released.</returns>
        public static bool ReleaseInstance(AsyncOperationHandle handle)
        {
            handle.Release();
            return true;
        }

        /// <summary>
        /// Releases and destroys an object that was created via Addressables.InstantiateAsync.
        /// </summary>
        /// <param name="handle">The handle to the game object to destroy, that was returned by InstantiateAsync.</param>
        /// <returns>Returns true if the instance was successfully released.</returns>
        public static bool ReleaseInstance(AsyncOperationHandle<GameObject> handle)
        {
            handle.Release();
            return true;
        }

        /// <summary>
        /// Determines the required download size, dependencies included, for the specified <paramref name="key"/>.
        /// Cached assets require no download and thus their download size will be 0.  The Result of the operation
        /// is the download size in bytes.
        /// </summary>
        /// <returns>The operation handle for the request.</returns>
        /// <param name="key">The key of the asset(s) to get the download size of.</param>
        public static AsyncOperationHandle<long> GetDownloadSizeAsync(object key)
        {
            return m_Addressables.GetDownloadSizeAsync(key);
        }

        /// <summary>
        /// Determines the required download size, dependencies included, for the specified <paramref name="key"/>.
        /// Cached assets require no download and thus their download size will be 0.  The Result of the operation
        /// is the download size in bytes.
        /// </summary>
        /// <returns>The operation handle for the request.</returns>
        /// <param name="key">The key of the asset(s) to get the download size of.</param>
        public static AsyncOperationHandle<long> GetDownloadSizeAsync(string key)
        {
            return m_Addressables.GetDownloadSizeAsync((object)key);
        }

        /// <summary>
        /// Determines the required download size, dependencies included, for the specified <paramref name="keys"/>.
        /// Cached assets require no download and thus their download size will be 0.  The Result of the operation
        /// is the download size in bytes.
        /// </summary>
        /// <returns>The operation handle for the request.</returns>
        /// <param name="keys">The keys of the asset(s) to get the download size of.</param>
        public static AsyncOperationHandle<long> GetDownloadSizeAsync(IEnumerable keys)
        {
            return m_Addressables.GetDownloadSizeAsync(keys);
        }

        /// <summary>
        /// Downloads dependencies of assets identified with the specified label or address.
        /// </summary>
        /// <remarks>
        /// Call this function to make sure that the dependencies of assets you plan to load in
        /// performance-critical sections of code are downloaded and available beforehand.
        ///
        /// You can use the <see cref="AsyncOperationHandle"/> returned by this function to monitor and
        /// provide feedback on the download progress.
        ///
        /// The returned handle must be released before Assets can be be loaded from the downloaded bundles.
        ///
        /// See [Preloading dependencies](xref:addressables-api-download-dependencies-async) for more details.
        /// </remarks>
        /// <param name="key">The key of the assets to load dependencies for.</param>
        /// <param name="autoReleaseHandle">If true, the Addressables system automatically releases the handle on completion.</param>
        /// <returns>The AsyncOperationHandle for the dependency load operation.</returns>
        public static AsyncOperationHandle DownloadDependenciesAsync(object key, bool autoReleaseHandle = false)
        {
            return m_Addressables.DownloadDependenciesAsync(key, autoReleaseHandle);
        }

        /// <summary>
        /// Downloads dependencies of assets at the specified locations.
        /// </summary>
        /// <remarks>
        /// Call this function to make sure that the dependencies of assets you plan to load in
        /// performance-critical sections of code are downloaded and available beforehand.
        ///
        /// You can use the <see cref="AsyncOperationHandle"/> returned by this function to monitor and
        /// provide feedback on the download progress.
        ///
        /// The returned handle must be released before Assets can be be loaded from the downloaded bundles.
        ///
        /// See [Preloading dependencies](xref:addressables-api-download-dependencies-async) for more details.
        /// </remarks>
        /// <param name="locations">The locations of the assets.</param>
        /// <param name="autoReleaseHandle">If true, the Addressables system automatically releases the handle on completion.</param>
        /// <returns>The AsyncOperationHandle for the dependency load.</returns>
        public static AsyncOperationHandle DownloadDependenciesAsync(IList<IResourceLocation> locations, bool autoReleaseHandle = false)
        {
            return m_Addressables.DownloadDependenciesAsync(locations, autoReleaseHandle);
        }

        /// <summary>
        /// Downloads dependencies of assets identified by a list of keys.
        /// </summary>
        /// <remarks>
        /// The keys in <paramref name="keys"/> are translated into lists of locations, which are merged into a single list based on
        /// the value in <paramref name="mode"/>.
        ///
        /// Call this function to make sure that the dependencies of assets you plan to load in
        /// performance-critical sections of code are downloaded and available beforehand.
        ///
        /// You can use the <see cref="AsyncOperationHandle"/> returned by this function to monitor and
        /// provide feedback on the download progress.
        ///
        /// The returned handle must be released before Assets can be be loaded from the downloaded bundles.
        ///
        /// See [Preloading dependencies](xref:addressables-api-download-dependencies-async) for more details.
        /// </remarks>
        /// <param name="keys">List of keys for the locations.</param>
        /// <param name="mode">Method for merging the results of key matches.  See <see cref="MergeMode"/> for specifics</param>
        /// <param name="autoReleaseHandle">If true, the Addressables system automatically releases the handle on completion.</param>
        /// <returns>The AsyncOperationHandle for the dependency load operation.</returns>
        public static AsyncOperationHandle DownloadDependenciesAsync(IEnumerable keys, MergeMode mode, bool autoReleaseHandle = false)
        {
            return m_Addressables.DownloadDependenciesAsync(keys, mode, autoReleaseHandle);
        }

        /// <summary>
        /// Clear the cached AssetBundles for a given key.  Operation may be performed async if Addressables
        /// is initializing or updating.
        /// </summary>
        /// <remarks>
        /// Clear all cached AssetBundles
        /// WARNING: This will cause all asset bundles represented by the passed-in
        /// parameters to be cleared and require re-downloading.
        /// </remarks>
        /// <param name="key">The key to clear the cache for.</param>
        public static void ClearDependencyCacheAsync(object key)
        {
            m_Addressables.ClearDependencyCacheAsync(key, true);
        }

        /// <summary>
        /// Clear the cached AssetBundles for a list of Addressable locations.  Operation may be performed async if Addressables
        /// is initializing or updating.
        /// </summary>
        /// <remarks>
        /// Clear all cached AssetBundles
        /// WARNING: This will cause all asset bundles represented by the passed-in
        /// parameters to be cleared and require re-downloading.
        /// </remarks>
        /// <param name="locations">The locations to clear the cache for.</param>
        public static void ClearDependencyCacheAsync(IList<IResourceLocation> locations)
        {
            m_Addressables.ClearDependencyCacheAsync(locations, true);
        }

        /// <summary>
        /// Clear the cached AssetBundles for a list of Addressable keys.  Operation may be performed async if Addressables
        /// is initializing or updating.
        /// </summary>
        /// <remarks>
        /// Clear all cached AssetBundles
        /// WARNING: This will cause all asset bundles represented by the passed-in
        /// parameters to be cleared and require re-downloading.
        /// </remarks>
        /// <param name="keys">The keys to clear the cache for.</param>
        public static void ClearDependencyCacheAsync(IEnumerable keys)
        {
            m_Addressables.ClearDependencyCacheAsync(keys, true);
        }

        /// <summary>
        /// Clear the cached AssetBundles for a list of Addressable keys.  Operation may be performed async if Addressables
        /// is initializing or updating.
        /// </summary>
        /// <remarks>
        /// Clear all cached AssetBundles
        /// WARNING: This will cause all asset bundles represented by the passed-in
        /// parameters to be cleared and require re-downloading.
        /// </remarks>
        /// <param name="key">The key to clear the cache for.</param>
        public static void ClearDependencyCacheAsync(string key)
        {
            m_Addressables.ClearDependencyCacheAsync((object)key, true);
        }

        /// <summary>
        /// Clear the cached AssetBundles for a given key.  Operation may be performed async if Addressables
        /// is initializing or updating.
        /// </summary>
        /// <remarks>
        /// Clear all cached AssetBundles
        /// WARNING: This will cause all asset bundles represented by the passed-in
        /// parameters to be cleared and require re-downloading.
        /// </remarks>
        /// <param name="key">The key to clear the cache for.</param>
        /// <param name="autoReleaseHandle">If true, the returned AsyncOperationHandle will be released on completion.</param>
        /// <returns>The operation handle for the request.</returns>
        public static AsyncOperationHandle<bool> ClearDependencyCacheAsync(object key, bool autoReleaseHandle)
        {
            return m_Addressables.ClearDependencyCacheAsync(key, autoReleaseHandle);
        }

        /// <summary>
        /// Clear the cached AssetBundles for a list of Addressable locations.  Operation may be performed async if Addressables
        /// is initializing or updating.
        /// </summary>
        /// <remarks>
        /// Clear all cached AssetBundles
        /// WARNING: This will cause all asset bundles represented by the passed-in
        /// parameters to be cleared and require re-downloading.
        /// </remarks>
        /// <param name="locations">The locations to clear the cache for.</param>
        /// <param name="autoReleaseHandle">If true, the returned AsyncOperationHandle will be released on completion.</param>
        /// <returns>The operation handle for the request.</returns>
        public static AsyncOperationHandle<bool> ClearDependencyCacheAsync(IList<IResourceLocation> locations, bool autoReleaseHandle)
        {
            return m_Addressables.ClearDependencyCacheAsync(locations, autoReleaseHandle);
        }

        /// <summary>
        /// Clear the cached AssetBundles for a list of Addressable keys.  Operation may be performed async if Addressables
        /// is initializing or updating.
        /// </summary>
        /// <remarks>
        /// Clear all cached AssetBundles
        /// WARNING: This will cause all asset bundles represented by the passed-in
        /// parameters to be cleared and require re-downloading.
        /// </remarks>
        /// <param name="keys">The keys to clear the cache for.</param>
        /// <param name="autoReleaseHandle">If true, the returned AsyncOperationHandle will be released on completion.</param>
        /// <returns>The operation handle for the request.</returns>
        public static AsyncOperationHandle<bool> ClearDependencyCacheAsync(IEnumerable keys, bool autoReleaseHandle)
        {
            return m_Addressables.ClearDependencyCacheAsync(keys, autoReleaseHandle);
        }

        /// <summary>
        /// Clear the cached AssetBundles for a list of Addressable keys.  Operation may be performed async if Addressables
        /// is initializing or updating.
        /// </summary>
        /// <remarks>
        /// Clear all cached AssetBundles
        /// WARNING: This will cause all asset bundles represented by the passed-in
        /// parameters to be cleared and require re-downloading.
        /// </remarks>
        /// <param name="key">The keys to clear the cache for.</param>
        /// <param name="autoReleaseHandle">If true, the returned AsyncOperationHandle will be released on completion.</param>
        /// <returns>The operation handle for the request.</returns>
        public static AsyncOperationHandle<bool> ClearDependencyCacheAsync(string key, bool autoReleaseHandle)
        {
            return m_Addressables.ClearDependencyCacheAsync((object)key, autoReleaseHandle);
        }

        /// <summary>
        /// Given the Id of an IResourceLocator, get the relevant content catalog data associated with it.
        /// </summary>
        /// <remarks>
        /// Retrieves the content catalog data associated with the specified [IResourceLocator](xref:UnityEngine.AddressableAssets.ResourceLocators.IResourceLocator) id. The catalog data is stored as a [ResourceLocatorInfo](xref:UnityEngine.AddressableAssets.ResourceLocatorInfo) object.
        /// </remarks>
        /// <param name="locatorId">The identifier of an [IResourceLocator](xref:UnityEngine.AddressableAssets.ResourceLocators.IResourceLocator) used to retrieve content catalog data.</param>
        /// <returns>Container for content catalog data pertaining to the [IResourceLocator](xref:UnityEngine.AddressableAssets.ResourceLocators.IResourceLocator) object.</returns>
        /// <example>
        /// <para>
        /// The example below uses the identifiers of the resource locators loaded during Addressables initialization process to retrieve catalog data.</para>
        /// <code source="../Tests/Editor/DocExampleCode/ScriptReference/UsingGetLocatorInfo.cs" region="SAMPLE_1"/>
        /// </example>
        public static ResourceLocatorInfo GetLocatorInfo(string locatorId)
        {
            return m_Addressables.GetLocatorInfo(locatorId);
        }

        /// <summary>
        /// Given an [IResourceLocator](xref:UnityEngine.AddressableAssets.ResourceLocators.IResourceLocator), get the relevant content catalog data associated with it.
        /// </summary>
        /// <param name="locator">The resource locator you want to get content catalog data for.</param>
        /// <returns>Container for content catalog data pertaining to the [IResourceLocator](xref:UnityEngine.AddressableAssets.ResourceLocators.IResourceLocator) object.</returns>
        public static ResourceLocatorInfo GetLocatorInfo(IResourceLocator locator)
        {
            return m_Addressables.GetLocatorInfo(locator.LocatorId);
        }

        /// <summary>
        /// Instantiate a single object.
        /// </summary>
        /// <remarks>
        /// Loads a Prefab and instantiates a copy of the prefab into the active scene or parent GameObject. The Prefab and any resources associated with it
        /// are loaded asynchronously, whereas the instantiation is executed synchronously. In the situation where the Prefab and resources are already loaded,
        /// the entire operation is completed synchronously.
        ///
        /// Most versions of the function shares the same parameters(position, rotation, etc.) as [Object.Instantiate](xref:UnityEngine.Object.Instantiate*).
        /// You can create an [InstantiationParameters](xref:UnityEngine.ResourceManagement.ResourceProviders.InstantiationParameters) struct to store these
        /// parameters, pass it into the function instead.
        /// </remarks>
        /// <param name="location">The location of the Object to instantiate.</param>
        /// <param name="parent">Parent transform for instantiated object.</param>
        /// <param name="instantiateInWorldSpace">Option to retain world space when instantiated with a parent.</param>
        /// <param name="trackHandle">If true, Addressables will track this request to allow it to be released via the result object.</param>
        /// <returns>AsyncOperationHandle that is used to check when the operation has completed. The result of the operation is a GameObject.</returns>
        /// <example>
        /// <para>
        /// The example below instantiates a GameObject using by a `key`, specifically an <see cref="AssetReference"/>. By default `trackHandle` is set to `true`.
        /// Since the instance is tracked, a reference from the instance to the handle is stored and released via <see cref="Addressables.ReleaseInstance(GameObject)"/>.
        /// Alternatively a reference to the operation handle can be stored and released via <see cref="Release(AsyncOperationHandle)"/>, similar to the second example below.</para>
        /// <code source="../Tests/Editor/DocExampleCode/ScriptReference/UsingInstantiateAsync.cs" region="SAMPLE_OBJECT_TRACKED"/>
        /// <para>
        /// The example below shows how to release a GameObject when `trackHandle` is set to `false`. The instance is not tracked and cannot be used to
        /// release the object, instead a reference to the operation handle is stored and released via <see cref="Release(AsyncOperationHandle)"/>.</para>
        /// <code source="../Tests/Editor/DocExampleCode/ScriptReference/UsingInstantiateAsync.cs" region="SAMPLE_OBJECT_UNTRACKED"/>
        /// <para>
        /// The example below instantiates a GameObject using an <see cref="IResourceLocation"/>.</para>
        /// <code source="../Tests/Editor/DocExampleCode/ScriptReference/UsingInstantiateAsync.cs" region="SAMPLE_LOCATION"/>
        /// </example>
        public static AsyncOperationHandle<GameObject> InstantiateAsync(IResourceLocation location, Transform parent = null, bool instantiateInWorldSpace = false, bool trackHandle = true)
        {
            return m_Addressables.InstantiateAsync(location, new InstantiationParameters(parent, instantiateInWorldSpace), trackHandle);
        }

        /// <summary>
        /// Instantiate a single object.
        /// </summary>
        /// <param name="location">The location of the Object to instantiate.</param>
        /// <param name="position">The position of the instantiated object.</param>
        /// <param name="rotation">The rotation of the instantiated object.</param>
        /// <param name="parent">Parent transform for instantiated object.</param>
        /// <param name="trackHandle">If true, Addressables will track this request to allow it to be released via the result object.</param>
        /// <returns>AsyncOperationHandle that is used to check when the operation has completed. The result of the operation is a GameObject.</returns>
        public static AsyncOperationHandle<GameObject> InstantiateAsync(IResourceLocation location, Vector3 position, Quaternion rotation, Transform parent = null, bool trackHandle = true)
        {
            return m_Addressables.InstantiateAsync(location, position, rotation, parent, trackHandle);
        }

        /// <summary>
        /// Instantiate a single object.
        /// </summary>
        /// <param name="key">The key of the location of the Object to instantiate.</param>
        /// <param name="parent">Parent transform for instantiated object.</param>
        /// <param name="instantiateInWorldSpace">Option to retain world space when instantiated with a parent.</param>
        /// <param name="trackHandle">If true, Addressables will track this request to allow it to be released via the result object.</param>
        /// <returns>AsyncOperationHandle that is used to check when the operation has completed. The result of the operation is a GameObject.</returns>
        public static AsyncOperationHandle<GameObject> InstantiateAsync(object key, Transform parent = null, bool instantiateInWorldSpace = false, bool trackHandle = true)
        {
            return m_Addressables.InstantiateAsync(key, parent, instantiateInWorldSpace, trackHandle);
        }

        /// <summary>
        /// Instantiate a single object.
        /// </summary>
        /// <param name="key">The key of the location of the Object to instantiate.</param>
        /// <param name="position">The position of the instantiated object.</param>
        /// <param name="rotation">The rotation of the instantiated object.</param>
        /// <param name="parent">Parent transform for instantiated object.</param>
        /// <param name="trackHandle">If true, Addressables will track this request to allow it to be released via the result object.</param>
        /// <returns>AsyncOperationHandle that is used to check when the operation has completed. The result of the operation is a GameObject.</returns>
        public static AsyncOperationHandle<GameObject> InstantiateAsync(object key, Vector3 position, Quaternion rotation, Transform parent = null, bool trackHandle = true)
        {
            return m_Addressables.InstantiateAsync(key, position, rotation, parent, trackHandle);
        }

        /// <summary>
        /// Instantiate a single object.
        /// </summary>
        /// <param name="key">The key of the location of the Object to instantiate.</param>
        /// <param name="instantiateParameters">Parameters for instantiation.</param>
        /// <param name="trackHandle">If true, Addressables will track this request to allow it to be released via the result object.</param>
        /// <returns>AsyncOperationHandle that is used to check when the operation has completed. The result of the operation is a GameObject.</returns>
        public static AsyncOperationHandle<GameObject> InstantiateAsync(object key, InstantiationParameters instantiateParameters, bool trackHandle = true)
        {
            return m_Addressables.InstantiateAsync(key, instantiateParameters, trackHandle);
        }

        /// <summary>
        /// Instantiate a single object.
        /// </summary>
        /// <param name="location">The location of the Object to instantiate.</param>
        /// <param name="instantiateParameters">Parameters for instantiation.</param>
        /// <param name="trackHandle">If true, Addressables will track this request to allow it to be released via the result object.</param>
        /// <returns>AsyncOperationHandle that is used to check when the operation has completed. The result of the operation is a GameObject.</returns>
        public static AsyncOperationHandle<GameObject> InstantiateAsync(IResourceLocation location, InstantiationParameters instantiateParameters, bool trackHandle = true)
        {
            return m_Addressables.InstantiateAsync(location, instantiateParameters, trackHandle);
        }

        /// <summary>
        /// Loads an Addressable Scene asset.
        /// </summary>
        /// <remarks>
        /// The <paramref name="loadMode"/>, <paramref name="activateOnLoad"/>, and <paramref name="priority"/> parameters correspond to
        /// the parameters used in the Unity [SceneManager.LoadSceneAsync](https://docs.unity3d.com/ScriptReference/SceneManagement.SceneManager.LoadSceneAsync.html)
        /// method.
        ///
        /// See [Loading Scenes](xref:addressables-api-load-asset-async) for more details.
        /// </remarks>
        /// <param name="key">The key of the location of the scene to load.</param>
        /// <param name="loadMode">Scene load mode.</param>
        /// <param name="activateOnLoad">If false, the scene will load but not activate (for background loading).  The SceneInstance returned has an Activate() method that can be called to do this at a later point.</param>
        /// <param name="priority">Async operation priority for scene loading.</param>
        /// <param name="releaseMode">The behavior for how the scene gets unloaded.</param>
        /// <returns>The operation handle for the request.</returns>
        public static AsyncOperationHandle<SceneInstance> LoadSceneAsync(object key, LoadSceneMode loadMode = LoadSceneMode.Single, bool activateOnLoad = true, int priority = 100, SceneReleaseMode releaseMode = SceneReleaseMode.ReleaseSceneWhenSceneUnloaded)
        {
            return m_Addressables.LoadSceneAsync(key, new LoadSceneParameters(loadMode), releaseMode, activateOnLoad, priority);
        }

        /// <summary>
        /// Loads an Addressable Scene asset.
        /// </summary>
        /// <remarks>
        /// The <paramref name="loadMode"/>, <paramref name="activateOnLoad"/>, and <paramref name="priority"/> parameters correspond to
        /// the parameters used in the Unity [SceneManager.LoadSceneAsync](https://docs.unity3d.com/ScriptReference/SceneManagement.SceneManager.LoadSceneAsync.html)
        /// method.
        ///
        /// See [Loading Scenes](xref:addressables-api-load-asset-async) for more details.
        /// </remarks>
        /// <param name="key">The key of the location of the scene to load.</param>
        /// <param name="loadMode">Scene load mode.</param>
        /// <param name="releaseMode">How the scene is handled if it is unloaded due to another scene loading using single mode.</param>
        /// <param name="activateOnLoad">If false, the scene will load but not activate (for background loading).  The SceneInstance returned has an Activate() method that can be called to do this at a later point.</param>
        /// <param name="priority">Async operation priority for scene loading.</param>
        /// <returns>The operation handle for the request.</returns>
        public static AsyncOperationHandle<SceneInstance> LoadSceneAsync(object key, LoadSceneMode loadMode, SceneReleaseMode releaseMode, bool activateOnLoad = true, int priority = 100)
        {
            return m_Addressables.LoadSceneAsync(key, new LoadSceneParameters(loadMode), releaseMode, activateOnLoad, priority);
        }

        /// <summary>
        /// Loads an Addressable Scene asset.
        /// </summary>
        /// <param name="key">The key of the location of the scene to load.</param>
        /// <param name="loadSceneParameters">Scene load mode.</param>
        /// <param name="activateOnLoad">If false, the scene will load but not activate (for background loading).  The SceneInstance returned has an Activate() method that can be called to do this at a later point.</param>
        /// <param name="priority">Async operation priority for scene loading.</param>
        /// <returns>The operation handle for the request.</returns>
        public static AsyncOperationHandle<SceneInstance> LoadSceneAsync(object key, LoadSceneParameters loadSceneParameters, bool activateOnLoad = true, int priority = 100)
        {
            return m_Addressables.LoadSceneAsync(key, loadSceneParameters, SceneReleaseMode.ReleaseSceneWhenSceneUnloaded, activateOnLoad, priority);
        }

        /// <summary>
        /// Loads an Addressable Scene asset.
        /// </summary>
        /// <param name="key">The key of the location of the scene to load.</param>
        /// <param name="loadSceneParameters">Scene load mode.</param>
        /// <param name="releaseMode">How the scene is handled if it is unloaded due to another scene loading using single mode.</param>
        /// <param name="activateOnLoad">If false, the scene will load but not activate (for background loading).  The SceneInstance returned has an Activate() method that can be called to do this at a later point.</param>
        /// <param name="priority">Async operation priority for scene loading.</param>
        /// <returns>The operation handle for the request.</returns>
        public static AsyncOperationHandle<SceneInstance> LoadSceneAsync(object key, LoadSceneParameters loadSceneParameters, SceneReleaseMode releaseMode, bool activateOnLoad = true, int priority = 100)
        {
            return m_Addressables.LoadSceneAsync(key, loadSceneParameters, releaseMode, activateOnLoad, priority);
        }

        /// <summary>
        /// Loads an Addressable Scene asset.
        /// </summary>
        /// <param name="location">The location of the scene to load.</param>
        /// <param name="loadMode">Scene load mode.</param>
        /// <param name="activateOnLoad">If false, the scene will load but not activate (for background loading).  The SceneInstance returned has an Activate() method that can be called to do this at a later point.</param>
        /// <param name="priority">Async operation priority for scene loading.</param>
        /// <returns>The operation handle for the request.</returns>
        public static AsyncOperationHandle<SceneInstance> LoadSceneAsync(IResourceLocation location, LoadSceneMode loadMode = LoadSceneMode.Single, bool activateOnLoad = true, int priority = 100)
        {
            return m_Addressables.LoadSceneAsync(location, new LoadSceneParameters(loadMode), SceneReleaseMode.ReleaseSceneWhenSceneUnloaded, activateOnLoad, priority);
        }

        /// <summary>
        /// Loads an Addressable Scene asset.
        /// </summary>
        /// <param name="location">The location of the scene to load.</param>
        /// <param name="loadMode">Scene load mode.</param>
        /// <param name="releaseMode">How the scene is handled if it is unloaded due to another scene loading using single mode.</param>
        /// <param name="activateOnLoad">If false, the scene will load but not activate (for background loading).  The SceneInstance returned has an Activate() method that can be called to do this at a later point.</param>
        /// <param name="priority">Async operation priority for scene loading.</param>
        /// <returns>The operation handle for the request.</returns>
        public static AsyncOperationHandle<SceneInstance> LoadSceneAsync(IResourceLocation location, LoadSceneMode loadMode, SceneReleaseMode releaseMode, bool activateOnLoad = true, int priority = 100)
        {
            return m_Addressables.LoadSceneAsync(location, new LoadSceneParameters(loadMode), releaseMode, activateOnLoad, priority);
        }

        /// <summary>
        /// Loads an Addressable Scene asset.
        /// </summary>
        /// <param name="location">The location of the scene to load.</param>
        /// <param name="loadSceneParameters">Scene load parameters.</param>
        /// <param name="activateOnLoad">If false, the scene will load but not activate (for background loading).  The SceneInstance returned has an Activate() method that can be called to do this at a later point.</param>
        /// <param name="priority">Async operation priority for scene loading.</param>
        /// <returns>The operation handle for the request.</returns>
        public static AsyncOperationHandle<SceneInstance> LoadSceneAsync(IResourceLocation location, LoadSceneParameters loadSceneParameters, bool activateOnLoad = true, int priority = 100)
        {
            return m_Addressables.LoadSceneAsync(location, loadSceneParameters, SceneReleaseMode.ReleaseSceneWhenSceneUnloaded, activateOnLoad, priority);
        }

        /// <summary>
        /// Loads an Addressable Scene asset.
        /// </summary>
        /// <param name="location">The location of the scene to load.</param>
        /// <param name="loadSceneParameters">Scene load parameters.</param>
        /// <param name="releaseMode">How the scene is handled if it is unloaded due to another scene loading using single mode.</param>
        /// <param name="activateOnLoad">If false, the scene will load but not activate (for background loading).  The SceneInstance returned has an Activate() method that can be called to do this at a later point.</param>
        /// <param name="priority">Async operation priority for scene loading.</param>
        /// <returns>The operation handle for the request.</returns>
        public static AsyncOperationHandle<SceneInstance> LoadSceneAsync(IResourceLocation location, LoadSceneParameters loadSceneParameters, SceneReleaseMode releaseMode, bool activateOnLoad = true, int priority = 100)
        {
            return m_Addressables.LoadSceneAsync(location, loadSceneParameters, releaseMode, activateOnLoad, priority);
        }

        /// <summary>
        /// Release scene
        /// </summary>
        /// <remarks>
        /// UnloadSceneAsync releases a previously loaded scene. The scene must have been activated to be unloaded.
        ///
        /// Passing UnloadSceneOptions.UnloadAllEmbeddedSceneObjects will unload assets embedded in the scene. The default is UploadSceneOptions.None
        /// which will only unload the scene's GameObjects.
        /// </remarks>
        /// <param name="scene">The SceneInstance to release.</param>
        /// <param name="unloadOptions">Specify behavior for unloading embedded scene objecs</param>
        /// <param name="autoReleaseHandle">If true, the handle will be released automatically when complete.</param>
        /// <returns>The operation handle for the scene unload.</returns>
        /// <example>
        /// <code source="../Tests/Editor/DocExampleCode/ScriptReference/UsingUnloadSceneAsync.cs" region="SAMPLE"/>
        /// </example>
        public static AsyncOperationHandle<SceneInstance> UnloadSceneAsync(SceneInstance scene, UnloadSceneOptions unloadOptions, bool autoReleaseHandle = true)
        {
            return m_Addressables.UnloadSceneAsync(scene, unloadOptions, autoReleaseHandle);
        }

        /// <summary>
        /// Release scene
        /// </summary>
        /// <param name="handle">The handle returned by LoadSceneAsync for the scene to release.</param>
        /// <param name="unloadOptions">Specify behavior for unloading embedded scene objecs</param>
        /// <param name="autoReleaseHandle">If true, the handle will be released automatically when complete.</param>
        /// <returns>The operation handle for the scene unload.</returns>
        public static AsyncOperationHandle<SceneInstance> UnloadSceneAsync(AsyncOperationHandle handle, UnloadSceneOptions unloadOptions, bool autoReleaseHandle = true)
        {
            return m_Addressables.UnloadSceneAsync(handle, unloadOptions, autoReleaseHandle);
        }

        /// <summary>
        /// Release scene
        /// </summary>
        /// <param name="scene">The SceneInstance to release.</param>
        /// <param name="autoReleaseHandle">If true, the handle will be released automatically when complete.</param>
        /// <returns>The operation handle for the scene unload.</returns>
        public static AsyncOperationHandle<SceneInstance> UnloadSceneAsync(SceneInstance scene, bool autoReleaseHandle = true)
        {
            return m_Addressables.UnloadSceneAsync(scene, UnloadSceneOptions.None, autoReleaseHandle);
        }

        /// <summary>
        /// Release scene
        /// </summary>
        /// <param name="handle">The handle returned by LoadSceneAsync for the scene to release.</param>
        /// <param name="autoReleaseHandle">If true, the handle will be released automatically when complete.</param>
        /// <returns>The operation handle for the scene unload.</returns>
        public static AsyncOperationHandle<SceneInstance> UnloadSceneAsync(AsyncOperationHandle handle, bool autoReleaseHandle = true)
        {
            return m_Addressables.UnloadSceneAsync(handle, UnloadSceneOptions.None, autoReleaseHandle);
        }

        /// <summary>
        /// Release scene
        /// </summary>
        /// <param name="handle">The handle returned by LoadSceneAsync for the scene to release.</param>
        /// <param name="autoReleaseHandle">If true, the handle will be released automatically when complete.</param>
        /// <returns>The operation handle for the scene unload.</returns>
        public static AsyncOperationHandle<SceneInstance> UnloadSceneAsync(AsyncOperationHandle<SceneInstance> handle, bool autoReleaseHandle = true)
        {
            return m_Addressables.UnloadSceneAsync(handle, UnloadSceneOptions.None, autoReleaseHandle);
        }

        /// <summary>
        /// Checks all updatable content catalogs for a new version.
        /// </summary>
        /// <param name="autoReleaseHandle">If true, the handle will automatically be released when the operation completes.</param>
        /// <returns>The operation containing the list of catalog ids that have an available update.  This can be used to filter which catalogs to update with the UpdateContent.</returns>
        public static AsyncOperationHandle<List<string>> CheckForCatalogUpdates(bool autoReleaseHandle = true)
        {
            return m_Addressables.CheckForCatalogUpdates(autoReleaseHandle);
        }

        /// <summary>
        /// Update the specified catalogs.
        /// </summary>
        /// <remarks>
        /// When you call the UpdateCatalog function, all other Addressable requests are blocked until the operation is finished.
        /// You can release the operation handle returned by UpdateCatalogs immediately after the operation finishes (or set the
        /// autoRelease parameter to true).
        ///
        /// If you call UpdateCatalog without providing a list of catalogs, the Addressables system checks all of the currently
        /// loaded catalogs for updates.
        ///
        /// If you update a catalog when you have already loaded content from the related AssetBundles, you can encounter conflicts
        /// between the loaded AssetBundles and the updated versions. To avoid conflicts, update the catalog before loading assets or unload
        /// the AssetBundles before the updating the catalog. You can enable the
        /// [Unique Bundle Ids](xref:addressables-content-update-builds)
        /// option in your Addressable settings to avoid conflicts, but that can increase memory consumption since you will still
        /// have the original AssetBundles in memory after loading the updated ones. Enabling this option can also make the download size of content
        /// updates larger because typically more AssetBundles must be rebuilt.
        ///
        /// You can call <see cref="UpdateCatalogs(bool, IEnumerable{string}, bool)"/> with the first value set to true to automtically remove any
        /// nonreferenced bundles from the cache after updating the catalogs.
        ///
        /// </remarks>
        /// <param name="catalogs">The set of catalogs to update.  If null, all catalogs that have an available update will be updated.</param>
        /// <param name="autoReleaseHandle">If true, the handle will automatically be released when the operation completes.</param>
        /// <returns>The operation with the list of updated content catalog data.</returns>
        /// <example>
        /// <para>
        /// In this example all catalogs are checked for updates:</para>
        /// <code source="../Tests/Editor/DocExampleCode/MiscellaneousTopics.cs" region="doc_CheckCatalog"/>
        /// <para>
        /// Alternatively you can directly get a list of catalogs and perform the updates:</para>
        /// <code source="../Tests/Editor/DocExampleCode/MiscellaneousTopics.cs" region="doc_CheckCatalog"/>
        /// </example>
        /// <seealso href="xref:addressables-api-load-content-catalog-async">Updating catalogs</seealso>
        public static AsyncOperationHandle<List<IResourceLocator>> UpdateCatalogs(IEnumerable<string> catalogs = null, bool autoReleaseHandle = true)
        {
            return m_Addressables.UpdateCatalogs(catalogs, autoReleaseHandle, false);
        }

        /// <summary>
        /// Update the specified catalogs.
        /// </summary>
        /// <param name="autoCleanBundleCache">If true, removes any nonreferenced bundles in the cache.</param>
        /// <param name="catalogs">The set of catalogs to update.  If null, all catalogs that have an available update will be updated.</param>
        /// <param name="autoReleaseHandle">If true, the handle will automatically be released when the operation completes.</param>
        /// <returns>The operation with the list of updated content catalog data.</returns>
        public static AsyncOperationHandle<List<IResourceLocator>>
            UpdateCatalogs(bool autoCleanBundleCache, IEnumerable<string> catalogs = null, bool autoReleaseHandle = true) // autoCleanBundleCache must be listed first to avoid breaking API
        {
            return m_Addressables.UpdateCatalogs(catalogs, autoReleaseHandle, autoCleanBundleCache);
        }

        /// <summary>
        /// Adds a ResourceLocator to the Addressables runtime.
        /// </summary>
        /// <remarks>
        /// Adds a ResourceLocator to the Addressables runtime.
        ///
        /// After adding the resource locator to Addressables it can then be used to locate Locations via Addressables loading APIs.
        ///
        /// Adding new resource locators can be used to add locations and manage asset files outside of the Addressables build system.
        ///
        /// In the following example we have a folder in the root folder called "dataFiles" containing some json files.
        ///
        /// These json files are then loaded using TextDataProvider, which is a ResourceProvider used to load text files.
        /// </remarks>
        /// <param name="locator">The locator object.</param>
        /// <param name="localCatalogHash">The hash of the local catalog. This can be null if the catalog cannot be updated.</param>
        /// <param name="remoteCatalogLocation">The location of the remote catalog. This can be null if the catalog cannot be updated.</param>
        /// <example>
        /// <para>
        /// This example code creates ResourceLocationBase and adds it to the locator for each file.</para>
        /// <code source="../Tests/Editor/DocExampleCode/ScriptReference/UsingAddResourceLocator.cs" region="SAMPLE_ADDLOCATOR"/>
        /// <para>
        /// Using Addressables API to load "dataFiles/settings.json" after adding the locator:</para>
        /// <code source="../Tests/Editor/DocExampleCode/ScriptReference/UsingAddResourceLocator.cs" region="SAMPLE_LOADING"/>
        /// </example>
        public static void AddResourceLocator(IResourceLocator locator, string localCatalogHash = null, IResourceLocation remoteCatalogLocation = null)
        {
            m_Addressables.AddResourceLocator(locator, localCatalogHash, remoteCatalogLocation);
        }

        /// <summary>
        /// Removes a ResourceLocator from the Addressables runtime.  This is uncommon but can be used to remove a specific locator that is only needed for a short time.
        /// </summary>
        /// <param name="locator">The locator to remove.</param>
        public static void RemoveResourceLocator(IResourceLocator locator)
        {
            m_Addressables.RemoveResourceLocator(locator);
        }

        /// <summary>
        /// Clears all ResourceLocators from the Addressables runtime.  This can be used to remove the default locators in order to add custom ones.
        /// </summary>
        public static void ClearResourceLocators()
        {
            m_Addressables.ClearResourceLocators();
        }

        /// <summary>
        /// Removes any AssetBundles that are no longer referenced in the bundle cache. This can occur when a new, updated catalog excludes entries present in an older catalog.
        /// </summary>
        /// <remarks>
        /// This is used to reduce the disk usage of the app by removing AssetBundles that are not needed.
        ///
        /// Note, that only AssetBundles loaded through UnityWebRequest are cached. If you want to purge the entire cache, use [Caching.ClearCache](xref:UnityEngine.Cache.ClearCache) instead.
        ///
        /// In the Editor, calling CleanBundleCache when not using the "Use Existing Build (requires built groups)" will clear all bundles. No bundles are used by "Use Asset Database (fastest)" or "Simulate Groups (advanced)" catalogs.
        ///
        /// See [AssetBundle caching](xref:addressables-remote-content-distribution) for more details.
        /// </remarks>
        /// <param name="catalogsIds">The ids of catalogs whose bundle cache entries we want to preserve. If null, entries for all currently loaded catalogs will be preserved.</param>
        /// <returns>The operation handle for the request. Note, that it is user's responsibility to release the returned operation; this can be done before or after the operation completes.</returns>
        /// <example>
        /// <para>
        /// Using CleanBundleCache to remove unused bundles from cache:</para>
        /// <code source="../Tests/Editor/DocExampleCode/ScriptReference/UsingCleanBundleCache.cs" region="SAMPLE_ALL" />
        /// <para>
        /// The catalogIds parameter is used to set which cached bundles are preserved.Any bundles that are used in other catalogs or old bundles from the specified catalogs are removed.If null, all currently loaded catalogs will be preserved.
        /// Note, Catalogs other than the main catalog must be loaded to be used for preserving bundles, see[LoadContentCatalogAsync] for more information.
        /// Using CleanBundleCache to remove all unused bundles from cache that are not used in the current main catalog:</para>
        /// <code source="../Tests/Editor/DocExampleCode/ScriptReference/UsingCleanBundleCache.cs" region="SAMPLE_SPECIFY" />
        /// </example>
        public static AsyncOperationHandle<bool> CleanBundleCache(IEnumerable<string> catalogsIds = null)
        {
            return m_Addressables.CleanBundleCache(catalogsIds, false);
        }

        /// <summary>
        /// Given a location path that points to a remote content catalog, create a location with the assumed dependencies
        /// that point to remote, and local, hash files respectively.  The first dependency, remote, assumes that the .hash file
        /// is located beside the provided location of the .json catalog file.  The second dependency, local, points to a location
        /// inside the Addressables local cache data folder.  The Addressables local cache data folder is meant for content catalogs
        /// and is not the same cache location for AssetBundles.
        /// </summary>
        /// <typeparam name="T">The type of provider you want to load your given catalog.  By default, Addressables uses the ContentCatalogProvider.</typeparam>
        /// <param name="remoteCatalogPath">The path of the remote content catalog.</param>
        /// <returns>A resource location with exactly 2 dependencies.  The first points to the assumed remote hash file location.  The second points to the local hash file location.</returns>
        public static ResourceLocationBase CreateCatalogLocationWithHashDependencies<T>(string remoteCatalogPath) where T : IResourceProvider
        {
            return m_Addressables.CreateCatalogLocationWithHashDependencies<T>(remoteCatalogPath);
        }

        /// <summary>
        /// Given a location path that points to a remote content catalog, create a location with the assumed dependencies
        /// that point to remote, and local, hash files respectively.  The first dependency, remote, assumes that the .hash file
        /// is located beside the provided location of the .json catalog file.  The second dependency, local, points to a location
        /// inside the Addressables local cache data folder.  The Addressables local cache data folder is meant for content catalogs
        /// and is not the same cache location for AssetBundles.
        /// </summary>
        /// <typeparam name="T">The type of provider you want to load your given catalog.  By default, Addressables uses the ContentCatalogProvider.</typeparam>
        /// <param name="remoteCatalogLocation">A resource location that points to the remote content catalog file.</param>
        /// <returns>A resource location with exactly 2 dependencies.  The first points to the assumed remote hash file location.  The second points to the local hash file location.</returns>
        public static ResourceLocationBase CreateCatalogLocationWithHashDependencies<T>(IResourceLocation remoteCatalogLocation) where T : IResourceProvider
        {
            return m_Addressables.CreateCatalogLocationWithHashDependencies<T>(remoteCatalogLocation);
        }

        /// <summary>
        /// Given a location path that points to a remote content catalog and its corresponding remote hash file, create a location with the dependencies
        /// that point to remote, and local, hash files respectively.  The first dependency, remote, uses the provided remote hash location.
        /// The second dependency, local, points to a location inside the Addressables local cache data folder.  The Addressables local cache data folder is meant for content catalogs
        /// and is not the same cache location for AssetBundles.
        /// </summary>
        /// <typeparam name="T">The type of provider you want to load your given catalog.  By default, Addressables uses the ContentCatalogProvider.</typeparam>
        /// <param name="remoteCatalogPath">The path of the remote content catalog.</param>
        /// <param name="remoteHashPath">The path of the remote catalog .hash file.</param>
        /// <returns>A resource location with exactly 2 dependencies.  The first points to the assumed remote hash file location.  The second points to the local hash file location.</returns>
        public static ResourceLocationBase CreateCatalogLocationWithHashDependencies<T>(string remoteCatalogPath, string remoteHashPath) where T : IResourceProvider
        {
            return m_Addressables.CreateCatalogLocationWithHashDependencies<T>(remoteCatalogPath, remoteHashPath);
        }
    }
}
