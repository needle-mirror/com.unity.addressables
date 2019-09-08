using System;
using System.Collections.Generic;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEngine.AddressableAssets.Initialization;
using UnityEngine.AddressableAssets.ResourceLocators;

namespace UnityEditor.AddressableAssets.Build.DataBuilders
{
    
    /// <summary>
    /// Interface for any Addressables specific context objects to be used in the Scriptable Build Pipeline context store  
    /// </summary>
    public interface IAddressableAssetsBuildContext : IContextObject { }

    /// <summary>
    /// Simple context object for passing data through SBP, between different sections of Addressables code. 
    /// </summary>
    public class AddressableAssetsBuildContext : IAddressableAssetsBuildContext
    {
        /// <summary>
        /// The settings object to use.
        /// </summary>
        public AddressableAssetSettings settings;
        /// <summary>
        /// The current runtime data being built.
        /// </summary>
        public ResourceManagerRuntimeData runtimeData;
        /// <summary>
        /// The list of catalog locations.
        /// </summary>
        public List<ContentCatalogDataEntry> locations;
        /// <summary>
        /// Mapping of bundles to asset groups.
        /// </summary>
        public Dictionary<string, string> bundleToAssetGroup;
        /// <summary>
        /// Mapping of asset group to bundles.
        /// </summary>
        public Dictionary<AddressableAssetGroup, List<string>> assetGroupToBundles;
        /// <summary>
        /// Set of provider types needed in this build.
        /// </summary>
        public HashSet<Type> providerTypes;
    }
}
