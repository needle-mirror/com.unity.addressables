using System;
using System.Collections.Generic;
using UnityEditor.Build.Content;
using UnityEngine;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace UnityEditor.AddressableAssets.Settings
{
    internal class AddressableAssetSettingsLocator : IResourceLocator
    {
        public string LocatorId { get; private set; }
        static string kProviderId = typeof(AssetDatabaseProvider).FullName;
        public IEnumerable<object> Keys => m_keyToEntries.Keys;
        Dictionary<object, List<AddressableAssetEntry>> m_keyToEntries;

        public AddressableAssetSettingsLocator(AddressableAssetSettings settings)
        {
            LocatorId = settings.name;
            m_keyToEntries = GatherEntries(settings);
        }

        static Dictionary<object, List<AddressableAssetEntry>> GatherEntries(AddressableAssetSettings settings)
        {
            var keyToEntries = new Dictionary<object, List<AddressableAssetEntry>>();
            try
            {
                foreach (var g in settings.groups)
                {
                    if (g == null)
                        continue;
                    foreach (var me in g.entries)
                        me.GatherAllAssets(null, true, true, false, e =>
                        {
                            var keys = e.CreateKeyList();
                            foreach (var k in keys)
                            {
                                if (!keyToEntries.TryGetValue(k, out var entries))
                                    keyToEntries.Add(k, entries = new List<AddressableAssetEntry>());
                                entries.Add(e);
                            }

                            return false;
                        });
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            return keyToEntries;
        }

        public bool Locate(object key, Type type, out IList<IResourceLocation> locations)
        {
            if (!m_keyToEntries.TryGetValue(key, out var entries))
            {
                locations = null;
                return false;
            }

            locations = new List<IResourceLocation>(entries.Count);
            foreach (var e in entries)
            {
                if (e.guid.Length > 0 && e.address.Contains("[") && e.address.Contains("]"))
                {
                    Debug.LogErrorFormat("Address '{0}' cannot contain '[ ]'.", e.address);
                    locations = null;
                    return false;
                }
                if (type == null || type.IsAssignableFrom(e.MainAssetType) || (type == typeof(SceneInstance) && e.IsScene))
                {
                    locations.Add(new ResourceLocationBase(e.address, e.AssetPath, kProviderId, e.MainAssetType));
                }
                else
                {
                    ObjectIdentifier[] ids = ContentBuildInterface.GetPlayerObjectIdentifiersInAsset(new GUID(e.guid), EditorUserBuildSettings.activeBuildTarget);
                    if (ids.Length > 1)
                    {
                        Type[] typesForObjs = ContentBuildInterface.GetTypeForObjects(ids);
                        foreach (var objType in typesForObjs)
                        {
                            if (type.IsAssignableFrom(objType))
                            {
                                locations.Add(new ResourceLocationBase(e.address, e.AssetPath, kProviderId, objType));
                                break;
                            }
                        }
                    }
                }
            }

            if (locations.Count == 0)
            {
                locations = null;
                return false;
            }
            return true;
        }
    }
}
