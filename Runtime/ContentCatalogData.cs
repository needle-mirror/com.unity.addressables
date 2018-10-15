using System;
using System.Collections.Generic;
using UnityEngine.ResourceManagement;
using System.Linq;

namespace UnityEngine.AddressableAssets
{
    /// <summary>
    /// Contains serializable data for an IResourceLocation
    /// </summary>
    public class ContentCatalogDataEntry
    {
        /// <summary>
        /// Internl id.
        /// </summary>
        public string InternalId { get; set; }
        /// <summary>
        /// IResourceProvider identifier.
        /// </summary>
        public string Provider { get; private set; }
        /// <summary>
        /// Keys for this location.
        /// </summary>
        public List<object> Keys { get; private set; }
        /// <summary>
        /// Dependency keys.
        /// </summary>
        public List<object> Dependencies { get; private set; }
        /// <summary>
        /// Serializable data for the provider.
        /// </summary>
        public object Data { get; set; }

        /// <summary>
        /// Construct a new ContentCatalogEntry.
        /// </summary>
        /// <param name="internalId">The internal id.</param>
        /// <param name="provider">The provider id.</param>
        /// <param name="keys">The collection of keys that can be used to retrieve this entry.</param>
        /// <param name="dependencies">Optional collection of keys for dependencies.</param>
        /// <param name="extraData">Optional additional data to be passed to the provider.  For example, AssetBundleProviders use this for cache and crc data.</param>
        public ContentCatalogDataEntry(string internalId, string provider, IEnumerable<object> keys, IEnumerable<object> dependencies = null, object extraData = null)
        {
            InternalId = internalId;
            Provider = provider;
            Keys = new List<object>(keys);
            Dependencies = dependencies == null ? new List<object>() : new List<object>(dependencies);
            Data = extraData;
        }
        /// <summary>
        /// [obsolete] Construct a new ContentCatalogEntry.
        /// </summary>
        /// <param name="address">The primary key for this entry.</param>
        /// <param name="guid">The guid of the asset.</param>
        /// <param name="internalId">The internal id.</param>
        /// <param name="provider">The provider id.</param>
        /// <param name="labels">Optional set of labels that will be used as keys.</param>
        /// <param name="dependencies">Optional collection of keys for dependencies.</param>
        /// <param name="extraData">Optional additional data to be passed to the provider.  For example, AssetBundleProviders use this for cache and crc data.</param>
        [Obsolete("Use the version of ContentCatalogDataEntry that takes a list of keys instead of explicit address and guid.")]
        public ContentCatalogDataEntry(string address, string guid, string internalId, System.Type provider, HashSet<string> labels = null, IEnumerable<object> dependencies = null, object extraData = null)
        {
            InternalId = internalId;
            Provider = provider == null ? "" : provider.FullName;
            Keys = new List<object>(labels == null ? 2 : labels.Count + 2);
            Keys.Add(address);
            if (!string.IsNullOrEmpty(guid))
                Keys.Add(Hash128.Parse(guid));
            if (labels != null)
            {
                foreach (var l in labels)
                    Keys.Add(l);
            }
            Dependencies = dependencies == null ? new List<object>() : new List<object>(dependencies);
            Data = extraData;
        }
    }

    /// <summary>
    /// Container for ContentCatalogEntries.
    /// </summary>
    [Serializable]
    public class ContentCatalogData
    {


        [SerializeField]
        string[] m_providerIds = null;
        [SerializeField]
        string[] m_internalIds = null;
        [SerializeField]
        string m_keyDataString = null;
        [SerializeField]
        string m_bucketDataString = null;
        [SerializeField]
        string m_entryDataString = null;
        [SerializeField]
        string m_extraDataString = null;

        struct Bucket
        {
            public int dataOffset;
            public int[] entries;
        }

        struct CompactLocation : IResourceLocation
        {
            ResourceLocationMap m_locator;
            string m_internalId;
            string m_providerId;
            object m_dependency;
            object m_data;
            public string InternalId { get { return m_internalId; } }
            public string ProviderId { get { return m_providerId; } }
            public IList<IResourceLocation> Dependencies
            {
                get
                {
                    if (m_dependency == null)
                        return null;
                    IList<IResourceLocation> results;
                    m_locator.Locate(m_dependency, out results);
                    return results;
                }
            }
            public bool HasDependencies { get { return m_dependency != null; } }

            public object Data { get { return m_data; } }

            public override string ToString()
            {
                return m_internalId;
            }

            public CompactLocation(ResourceLocationMap locator, string internalId, string providerId, object dependencyKey, object data)
            {
                m_locator = locator;
                m_internalId = internalId;
                m_providerId = providerId;
                m_dependency = dependencyKey;
                m_data = data;
            }
        }

        /// <summary>
        /// Create IResourceLocator object
        /// </summary>
        /// <returns>ResourceLocationMap, which implements the IResourceLocator interface.</returns>
        public ResourceLocationMap CreateLocator()
        {
            var bucketData = Convert.FromBase64String(m_bucketDataString);
            int bucketCount = BitConverter.ToInt32(bucketData, 0);
            var buckets = new Bucket[bucketCount];
            int bi = 4;
            for (int i = 0; i < bucketCount; i++)
            {
                var index = SerializationUtilities.ReadInt32FromByteArray(bucketData, bi);
                bi += 4;
                var entryCount = SerializationUtilities.ReadInt32FromByteArray(bucketData, bi);
                bi += 4;
                var entryArray = new int[entryCount];
                for (int c = 0; c < entryCount; c++)
                {
                    entryArray[c] = SerializationUtilities.ReadInt32FromByteArray(bucketData, bi);
                    bi += 4;
                }
                buckets[i] = new Bucket() { entries = entryArray, dataOffset = index };
            }

            var extraData = Convert.FromBase64String(m_extraDataString);
            var keyData = Convert.FromBase64String(m_keyDataString);
            var keyCount = BitConverter.ToInt32(keyData, 0);
            var keys = new object[keyCount];
            for (int i = 0; i < buckets.Length; i++)
                keys[i] = SerializationUtilities.ReadObjectFromByteArray(keyData, buckets[i].dataOffset);

            var locator = new ResourceLocationMap(buckets.Length);

            var entryData = Convert.FromBase64String(m_entryDataString);
            int count = SerializationUtilities.ReadInt32FromByteArray(entryData, 0);
            List<IResourceLocation> locations = new List<IResourceLocation>(count);
            for (int i = 0; i < count; i++)
            {
                var index = 4 + i * 4 * 4;
                var internalId = SerializationUtilities.ReadInt32FromByteArray(entryData, index);
                var providerIndex = SerializationUtilities.ReadInt32FromByteArray(entryData, index + 4);
                var dependency = SerializationUtilities.ReadInt32FromByteArray(entryData, index + 8);
                var dataIndex = SerializationUtilities.ReadInt32FromByteArray(entryData, index + 12);
                object data = dataIndex < 0 ? null : SerializationUtilities.ReadObjectFromByteArray(extraData, dataIndex);
                locations.Add(new CompactLocation(locator, ResourceManager.ResolveInternalId(m_internalIds[internalId]),
                    m_providerIds[providerIndex], dependency < 0 ? null : keys[dependency], data));
            }

            for (int i = 0; i < buckets.Length; i++)
            {
                var bucket = buckets[i];
                var key = keys[i];
                var locs = new List<IResourceLocation>(bucket.entries.Length);
                foreach (var index in bucket.entries)
                    locs.Add(locations[index]);
                locator.Add(key, locs);
            }
            return locator;
        }
        
        public ContentCatalogData()
        {
        }


#if UNITY_EDITOR
        public ContentCatalogData(IList<ContentCatalogDataEntry> data)
        {
            SetData(data);
        }

        class KeyIndexer<T>
        {
            public List<T> values;
            public Dictionary<T, int> map;
            public KeyIndexer(IEnumerable<T> keyCollection, int capacity)
            {
                values = new List<T>(capacity);
                map = new Dictionary<T, int>(capacity);
                if (keyCollection != null)
                    Add(keyCollection);
            }

            public bool Add(IEnumerable<T> keyCollection)
            {
                bool isNew = false;
                foreach (var key in keyCollection)
                    Add(key, ref isNew);
                return isNew;
            }

            public int Add(T key, ref bool isNew)
            {
                int index;
                if (!map.TryGetValue(key, out index))
                {
                    isNew = true;
                    map.Add(key, index = values.Count);
                    values.Add(key);
                }
                return index;
            }
        }

        class KeyIndexer<TVal, TKey>
        {
            public List<TVal> values;
            public Dictionary<TKey, int> map;

            public KeyIndexer(IEnumerable<TKey> keyCollection, Func<TKey, TVal> func, int capacity)
            {
                values = new List<TVal>(capacity);
                map = new Dictionary<TKey, int>(capacity);
                if (keyCollection != null)
                    Add(keyCollection, func);
            }

            public KeyIndexer(IEnumerable<TVal> valCollection, Func<TVal, TKey> func, int capacity)
            {
                values = new List<TVal>(capacity);
                map = new Dictionary<TKey, int>(capacity);
                if (valCollection != null)
                    Add(valCollection, func);
            }

            public void Add(IEnumerable<TKey> keyCollection, Func<TKey, TVal> func)
            {
                foreach (var key in keyCollection)
                    Add(key, func(key));
            }

            public void Add(IEnumerable<TVal> valCollection, Func<TVal, TKey> func)
            {
                foreach (var val in valCollection)
                    Add(func(val), val);
            }

            public int Add(TKey key, TVal val)
            {
                int index;
                if (!map.TryGetValue(key, out index))
                {
                    map.Add(key, index = values.Count);
                    values.Add(val);
                }
                return index;
            }

            public TVal this[TKey key] { get { return values[map[key]]; } }
        }

        /// <summary>
        /// Set the data before serialization
        /// </summary>
        /// <param name="data">The list of </param>
        public void SetData(IList<ContentCatalogDataEntry> data)
        {
            if (data == null)
                return;
            var providers = new KeyIndexer<string>(data.Select(s => s.Provider), 10);
            var internalIds = new KeyIndexer<string>(data.Select(s => s.InternalId), data.Count);
            var keys = new KeyIndexer<object>(data.SelectMany(s => s.Keys), data.Count * 3);
            keys.Add(data.SelectMany(s => s.Dependencies));
            var keyIndexToEntries = new KeyIndexer<List<ContentCatalogDataEntry>, object>(keys.values, s => new List<ContentCatalogDataEntry>(), keys.values.Count);
            var entryToIndex = new Dictionary<ContentCatalogDataEntry, int>(data.Count);
            var extraDataList = new List<byte>();
            var entryIndexToExtraDataIndex = new Dictionary<int, int>();

            int extraDataIndex = 0;
            //create buckets of key to data entry
            for (int i = 0; i < data.Count; i++)
            {
                var e = data[i];
                int extraDataOffset = -1;
                if (e.Data != null)
                {
                    var len = SerializationUtilities.WriteObjectToByteList(e.Data, extraDataList);
                    if (len > 0)
                    {
                        extraDataOffset = extraDataIndex;
                        extraDataIndex += len;
                    }
                }
                entryIndexToExtraDataIndex.Add(i, extraDataOffset);
                entryToIndex.Add(e, i);
                foreach (var k in e.Keys)
                    keyIndexToEntries[k].Add(e);
            }
            m_extraDataString = Convert.ToBase64String(extraDataList.ToArray());

            //create extra entries for dependency sets
            int originalEntryCount = data.Count;
            for (int i = 0; i < originalEntryCount; i++)
            {
                var entry = data[i];
                if (entry.Dependencies == null || entry.Dependencies.Count < 2)
                    continue;

                //seed and and factor values taken from https://stackoverflow.com/questions/1646807/quick-and-simple-hash-code-combinations
                int hashCode = 1009;
                foreach (var dep in entry.Dependencies)
                    hashCode = hashCode * 9176 + dep.GetHashCode();
                bool isNew = false;
                keys.Add(hashCode, ref isNew);
                if (isNew)
                {
                    //if this combination of dependecies is new, add a new entry and add its key to all contained entries
                    var deps = entry.Dependencies.Select(d => keyIndexToEntries[d][0]).ToList();
                    keyIndexToEntries.Add(hashCode, deps);
                    foreach (var dep in deps)
                        dep.Keys.Add(hashCode);
                }

                //reset the dependency list to only contain the key of the new set
                entry.Dependencies.Clear();
                entry.Dependencies.Add(hashCode);
            }

            //serialize internal ids and providers
            m_internalIds = internalIds.values.ToArray();
            m_providerIds = providers.values.ToArray();

            //serialize entries
            {
                var entryData = new byte[data.Count * 4 * 4 + 4];
                var entryDataOffset = SerializationUtilities.WriteInt32ToByteArray(entryData, data.Count, 0);
                for (int i = 0; i < data.Count; i++)
                {
                    var e = data[i];
                    entryDataOffset = SerializationUtilities.WriteInt32ToByteArray(entryData, internalIds.map[e.InternalId], entryDataOffset);
                    entryDataOffset = SerializationUtilities.WriteInt32ToByteArray(entryData, providers.map[e.Provider], entryDataOffset);
                    entryDataOffset = SerializationUtilities.WriteInt32ToByteArray(entryData, e.Dependencies.Count == 0 ? -1 : keyIndexToEntries.map[e.Dependencies[0]], entryDataOffset);
                    entryDataOffset = SerializationUtilities.WriteInt32ToByteArray(entryData, entryIndexToExtraDataIndex[i], entryDataOffset);
                }
                m_entryDataString = Convert.ToBase64String(entryData);
            }

            //serialize keys and mappings
            {
                var entryCount = keyIndexToEntries.values.Aggregate(0, (a, s) => a += s.Count);
                var bucketData = new byte[4 + keys.values.Count * 8 + entryCount * 4];
                var keyData = new List<byte>(keys.values.Count * 10);
                keyData.AddRange(BitConverter.GetBytes(keys.values.Count));
                int keyDataOffset = 4;
                int bucketDataOffset = SerializationUtilities.WriteInt32ToByteArray(bucketData, keys.values.Count, 0);
                for (int i = 0; i < keys.values.Count; i++)
                {
                    var key = keys.values[i];
                    bucketDataOffset = SerializationUtilities.WriteInt32ToByteArray(bucketData, keyDataOffset, bucketDataOffset);
                    keyDataOffset += SerializationUtilities.WriteObjectToByteList(key, keyData);
                    var entries = keyIndexToEntries[key];
                    bucketDataOffset = SerializationUtilities.WriteInt32ToByteArray(bucketData, entries.Count, bucketDataOffset);
                    foreach (var e in entries)
                        bucketDataOffset = SerializationUtilities.WriteInt32ToByteArray(bucketData, entryToIndex[e], bucketDataOffset);
                }
                m_bucketDataString = Convert.ToBase64String(bucketData);
                m_keyDataString = Convert.ToBase64String(keyData.ToArray());
            }
        }


#if REFERENCE_IMPLEMENTATION
        public void SetDataOld(List<ResourceLocationData> locations, List<string> labels)
        {
            var tmpEntries = new List<Entry>(locations.Count);
            var providers = new List<string>(10);
            var providerIndices = new Dictionary<string, int>(10);
            var countEstimate = locations.Count * 2 + labels.Count;
            var internalIdToEntryIndex = new Dictionary<string, int>(countEstimate);
            var internalIdList = new List<string>(countEstimate);
            List<object> keys = new List<object>(countEstimate);

            var keyToIndex = new Dictionary<object, int>(countEstimate);
            var tmpBuckets = new Dictionary<int, List<int>>(countEstimate);
            
            for (int i = 0; i < locations.Count; i++)
            {
                var rld = locations[i];
                int providerIndex = 0;
                if (!providerIndices.TryGetValue(rld.m_provider, out providerIndex))
                {
                    providerIndices.Add(rld.m_provider, providerIndex = providers.Count);
                    providers.Add(rld.m_provider);
                }

                int internalIdIndex = 0;
                if (!internalIdToEntryIndex.TryGetValue(rld.m_internalId, out internalIdIndex))
                {
                    internalIdToEntryIndex.Add(rld.m_internalId, internalIdIndex = internalIdList.Count);
                    internalIdList.Add(rld.m_internalId);
                }

                var e = new Entry() { internalId = internalIdIndex, providerIndex = (byte)providerIndex, dependency = -1 };
                if (rld.m_type == ResourceLocationData.LocationType.Int)
                    AddToBucket(tmpBuckets, keyToIndex, keys, int.Parse(rld.m_address), tmpEntries.Count, 1);
                else if (rld.m_type == ResourceLocationData.LocationType.String)
                    AddToBucket(tmpBuckets, keyToIndex, keys, rld.m_address, tmpEntries.Count, 1);
                if (!string.IsNullOrEmpty(rld.m_guid))
                    AddToBucket(tmpBuckets, keyToIndex, keys, Hash128.Parse(rld.m_guid), tmpEntries.Count, 1);
                if (rld.m_labelMask != 0)
                {
                    for (int t = 0; t < labels.Count; t++)
                    {
                        if ((rld.m_labelMask & (1 << t)) != 0)
                            AddToBucket(tmpBuckets, keyToIndex, keys, labels[t], tmpEntries.Count, 100);
                    }
                }

                tmpEntries.Add(e);
            }

            for (int i = 0; i < locations.Count; i++)
            {
                var rld = locations[i];
                int dependency = -1;
                if (rld.m_dependencies != null && rld.m_dependencies.Length > 0)
                {
                    if (rld.m_dependencies.Length == 1)
                    {
                        dependency = keyToIndex[rld.m_dependencies[0]];
                    }
                    else
                    {
                        System.Text.StringBuilder sb = new System.Text.StringBuilder();
                        foreach (var d in rld.m_dependencies)
                            sb.Append(d);
                        var key = sb.ToString().GetHashCode();
                        int keyIndex = -1;
                        foreach (var d in rld.m_dependencies)
                        {
                            var ki = keyToIndex[d];
                            var depBucket = tmpBuckets[ki];
                            keyIndex = AddToBucket(tmpBuckets, keyToIndex, keys, key, depBucket[0], 10);
                        }
                        dependency = keyIndex;
                    }
                    var e = tmpEntries[i];
                    e.dependency = dependency;
                    tmpEntries[i] = e;
                }
            }

            m_internalIds = internalIdList.ToArray();
            m_providerIds = providers.ToArray();
            var entryData = new byte[tmpEntries.Count * 4 * 3 + 4];
            var offset = Serialize(entryData, tmpEntries.Count, 0);
            for (int i = 0; i < tmpEntries.Count; i++)
            {
                var e = tmpEntries[i];
                offset = Serialize(entryData, e.internalId, offset);
                offset = Serialize(entryData, e.providerIndex, offset);
                offset = Serialize(entryData, e.dependency, offset);
            }
            m_entryDataString = Convert.ToBase64String(entryData);

            int bucketEntryCount = 0;
            var bucketList = new List<Bucket>(keys.Count);
            for (int i = 0; i < keys.Count; i++)
            {
                var bucketIndex = keyToIndex[keys[i]];
                List<int> entries = tmpBuckets[bucketIndex];
                bucketList.Add(new Bucket() { entries = entries.ToArray() });
                bucketEntryCount += entries.Count;
            }

            var keyData = new List<byte>(bucketList.Count * 10);
            keyData.AddRange(BitConverter.GetBytes(bucketList.Count));
            int dataOffset = 4;
            for (int i = 0; i < bucketList.Count; i++)
            {
                var bucket = bucketList[i];
                bucket.dataOffset = dataOffset;
                bucketList[i] = bucket;
                var key = keys[i];
                var kt = key.GetType();
                if (kt == typeof(string))
                {
                    string str = key as string;
                    byte[] tmp = System.Text.Encoding.Unicode.GetBytes(str);
                    byte[] tmp2 = System.Text.Encoding.ASCII.GetBytes(str);
                    if (System.Text.Encoding.Unicode.GetString(tmp) == System.Text.Encoding.ASCII.GetString(tmp2))
                    {
                        keyData.Add((byte)KeyType.ASCIIString);
                        keyData.AddRange(tmp2);
                        dataOffset += tmp2.Length + 1;
                    }
                    else
                    {
                        keyData.Add((byte)KeyType.UnicodeString);
                        keyData.AddRange(tmp);
                        dataOffset += tmp.Length + 1;
                    }
                }
                else if (kt == typeof(UInt32))
                {
                    byte[] tmp = BitConverter.GetBytes((UInt32)key);
                    keyData.Add((byte)KeyType.UInt32);
                    keyData.AddRange(tmp);
                    dataOffset += tmp.Length + 1;
                }
                else if (kt == typeof(UInt16))
                {
                    byte[] tmp = BitConverter.GetBytes((UInt16)key);
                    keyData.Add((byte)KeyType.UInt16);
                    keyData.AddRange(tmp);
                    dataOffset += tmp.Length + 1;
                }
                else if (kt == typeof(Int32))
                {
                    byte[] tmp = BitConverter.GetBytes((Int32)key);
                    keyData.Add((byte)KeyType.Int32);
                    keyData.AddRange(tmp);
                    dataOffset += tmp.Length + 1;
                }
                else if (kt == typeof(int))
                {
                    byte[] tmp = BitConverter.GetBytes((UInt32)key);
                    keyData.Add((byte)KeyType.UInt32);
                    keyData.AddRange(tmp);
                    dataOffset += tmp.Length + 1;
                }
                else if (kt == typeof(Hash128))
                {
                    var guid = (Hash128)key;
                    byte[] tmp = System.Text.Encoding.ASCII.GetBytes(guid.ToString());
                    keyData.Add((byte)KeyType.Hash128);
                    keyData.AddRange(tmp);
                    dataOffset += tmp.Length + 1;
                }
            }
            m_keyDataString = Convert.ToBase64String(keyData.ToArray());

            var bucketData = new byte[4 + bucketList.Count * 8 + bucketEntryCount * 4];
            offset = Serialize(bucketData, bucketList.Count, 0);
            for (int i = 0; i < bucketList.Count; i++)
            {
                offset = Serialize(bucketData, bucketList[i].dataOffset, offset);
                offset = Serialize(bucketData, bucketList[i].entries.Length, offset);
                foreach (var e in bucketList[i].entries)
                    offset = Serialize(bucketData, e, offset);
            }
            m_bucketDataString = Convert.ToBase64String(bucketData);

#if SERIALIZE_CATALOG_AS_BINARY
            //TODO: investigate saving catalog as binary - roughly 20% size decrease, still needs a provider implementation
            var stream = new System.IO.MemoryStream();
            var bw = new System.IO.BinaryWriter(stream);
            foreach (var i in m_internalIds)
                 bw.Write(i);
             foreach (var p in m_providerIds)
                 bw.Write(p);
            bw.Write(entryData);
            bw.Write(keyData.ToArray());
            bw.Write(bucketData);
                        bw.Flush();
                        bw.Close();
                        stream.Flush();
                        System.IO.File.WriteAllBytes("Library/catalog_binary.bytes", stream.ToArray());
                        System.IO.File.WriteAllText("Library/catalog_binary.txt", Convert.ToBase64String(stream.ToArray()));
                        stream.Close();
#endif
        }

        private int AddToBucket(Dictionary<int, List<int>> buckets, Dictionary<object, int> keyToIndex, List<object> keys, object key, int index, int sizeHint)
        {
            int keyIndex = -1;
            if (!keyToIndex.TryGetValue(key, out keyIndex))
            {
                keyToIndex.Add(key, keyIndex = keys.Count);
                keys.Add(key);
            }

            List<int> bucket;
            if (!buckets.TryGetValue(keyIndex, out bucket))
                buckets.Add(keyIndex, bucket = new List<int>(sizeHint));
            bucket.Add(index);
            return keyIndex;
        }
#endif
#endif
    }
}
