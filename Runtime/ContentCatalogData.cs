using System;
using System.Collections.Generic;
using UnityEngine.ResourceManagement;
using System.Linq;

namespace UnityEngine.AddressableAssets
{

    [Serializable]
    public class ContentCatalogData
    {
        public enum KeyType
        {
            ASCIIString,
            UnicodeString,
            UInt16,
            UInt32,
            Int32,
            Hash128,
            Object
        }

        [SerializeField]
        string[] m_providerIds;
        [SerializeField]
        string[] m_internalIds;
        [SerializeField]
        string m_keyDataString;
        [SerializeField]
        string m_bucketDataString;
        [SerializeField]
        string m_entryDataString;
        [SerializeField]
        string m_extraDataString;

        struct Entry
        {
            public int dependency;
            public int internalId;
            public int providerIndex;
        }

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
            public override string ToString()
            {
                return m_internalId;
            }

            public CompactLocation(ResourceLocationMap locator, string internalId, string providerId, object dependencyKey)
            {
                m_locator = locator;
                m_internalId = internalId;
                m_providerId = providerId;
                m_dependency = dependencyKey;
            }
        }

        public ResourceLocationMap CreateLocator()
        {
            var bucketData = Convert.FromBase64String(m_bucketDataString);
            int bucketCount = BitConverter.ToInt32(bucketData, 0);
            var buckets = new Bucket[bucketCount];
            int bi = 4;
            for (int i = 0; i < bucketCount; i++)
            {
                var index = Deserialize(bucketData, bi);
                bi += 4;
                var entryCount = Deserialize(bucketData, bi);
                bi += 4;
                var entryArray = new int[entryCount];
                for (int c = 0; c < entryCount; c++)
                {
                    entryArray[c] = Deserialize(bucketData, bi);
                    bi += 4;
                }
                buckets[i] = new Bucket() { entries = entryArray, dataOffset = index };
            }

            var keyData = Convert.FromBase64String(m_keyDataString);
            var keyCount = BitConverter.ToInt32(keyData, 0);
            var keys = new object[keyCount];
            for (int i = 0; i < buckets.Length; i++)
            {
                int dataIndex = buckets[i].dataOffset;
                KeyType keyType = (KeyType)keyData[dataIndex];
                dataIndex++;
                int dataLength = i < (buckets.Length - 1) ? (buckets[i + 1].dataOffset - dataIndex) : keyData.Length - dataIndex;
                switch (keyType)
                {
                    case KeyType.UnicodeString: keys[i] = System.Text.Encoding.Unicode.GetString(keyData, dataIndex, dataLength); break;
                    case KeyType.ASCIIString: keys[i] = System.Text.Encoding.ASCII.GetString(keyData, dataIndex, dataLength); break;
                    case KeyType.UInt16: keys[i] = BitConverter.ToUInt16(keyData, dataIndex); break;
                    case KeyType.UInt32: keys[i] = BitConverter.ToUInt32(keyData, dataIndex); break;
                    case KeyType.Int32: keys[i] = BitConverter.ToInt32(keyData, dataIndex); break;
                    case KeyType.Hash128: keys[i] = Hash128.Parse(System.Text.Encoding.ASCII.GetString(keyData, dataIndex, dataLength)); break;
                    case KeyType.Object:
                        {
                            //TODO: JsonUtility.FromJson...
                        }
                        break;
                }
            }

            var locator = new ResourceLocationMap(buckets.Length);

            var entryData = Convert.FromBase64String(m_entryDataString);
            int count = Deserialize(entryData, 0);
            List<IResourceLocation> locations = new List<IResourceLocation>(count);
            for (int i = 0; i < count; i++)
            {
                var index = 4 + i * 4 * 3;
                var internalId = Deserialize(entryData, index);
                var providerIndex = Deserialize(entryData, index + 4);
                var dependency = Deserialize(entryData, index + 8);
                locations.Add(new CompactLocation(locator, AAConfig.ExpandPathWithGlobalVariables(m_internalIds[internalId]),
                    m_providerIds[providerIndex], dependency < 0 ? null : keys[dependency]));
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


        static int Deserialize(byte[] data, int offset)
        {
            return ((int)data[offset]) | (((int)data[offset + 1]) << 8) | (((int)data[offset + 2]) << 16) | (((int)data[offset + 3]) << 24);
        }

#if UNITY_EDITOR

        public bool Save(string path, bool binary)
        {
            try
            {
                if (binary)
                {
                    return false;
                }
                else
                {
                    var data = JsonUtility.ToJson(this);
                    System.IO.File.WriteAllText(path, data);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return false;
            }
        }

        static int Serialize(byte[] data, int val, int offset)
        {
            data[offset] = (byte)(val & 0xFF);
            data[offset + 1] = (byte)((val >> 8) & 0xFF);
            data[offset + 2] = (byte)((val >> 16) & 0xFF);
            data[offset + 3] = (byte)((val >> 24) & 0xFF);
            return offset + 4;
        }

        public interface ICustomKey
        {
            int Serialize(byte[] data, int offset);
            object Deserialize(byte[] data, int offset, int size);
        }

        public class DataEntry
        {
            public string m_internalId;
            public string m_provider;
            public List<object> m_keys;
            public List<object> m_dependencies;
            public DataEntry(string internalId, string provider, IEnumerable<object> keys, IEnumerable<object> deps)
            {
                m_internalId = internalId;
                m_provider = provider;
                m_keys = new List<object>(keys);
                m_dependencies = deps == null ?  new List<object>() : new List<object>(deps);
            }
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

        public void SetData(IList<DataEntry> data)
        {
            var providers = new KeyIndexer<string>(data.Select(s => s.m_provider), 10);
            var internalIds = new KeyIndexer<string>(data.Select(s => s.m_internalId), data.Count);
            var keys = new KeyIndexer<object>(data.SelectMany(s => s.m_keys), data.Count * 3);
            keys.Add(data.SelectMany(s => s.m_dependencies));
            var keyIndexToEntries = new KeyIndexer<List<DataEntry>, object>(keys.values, s => new List<DataEntry>(), keys.values.Count);
            var entryToIndex = new Dictionary<DataEntry, int>(data.Count);
            //create buckets of key to data entry
            for(int i = 0; i < data.Count; i++)
            {
                var e = data[i];
                entryToIndex.Add(e, i);
                foreach (var k in e.m_keys)
                    keyIndexToEntries[k].Add(e);
            }

            //create extra entries for dependency sets
            int originalEntryCount = data.Count;
            for (int i = 0; i < originalEntryCount; i++)
            {
                var entry = data[i];
                if (entry.m_dependencies == null || entry.m_dependencies.Count < 2)
                    continue;

                //seed and and factor values taken from https://stackoverflow.com/questions/1646807/quick-and-simple-hash-code-combinations
                int hashCode = 1009;
                foreach (var dep in entry.m_dependencies)
                    hashCode = hashCode * 9176 + dep.GetHashCode();
                bool isNew = false;
                keys.Add(hashCode, ref isNew);
                if (isNew)
                {
                    //if this combination of dependecies is new, add a new entry and add its key to all contained entries
                    var deps = entry.m_dependencies.Select(d => keyIndexToEntries[d][0]).ToList();
                    keyIndexToEntries.Add(hashCode, deps);
                    foreach (var dep in deps)
                        dep.m_keys.Add(hashCode);
                }

                //reset the dependency list to only contain the key of the new set
                entry.m_dependencies.Clear();
                entry.m_dependencies.Add(hashCode);
            }

            //serialize internal ids and providers
            m_internalIds = internalIds.values.ToArray();
            m_providerIds = providers.values.ToArray();

            //serialize entries
            {
                var entryData = new byte[data.Count * 4 * 3 + 4];
                var entryDataOffset = Serialize(entryData, data.Count, 0);
                for (int i = 0; i < data.Count; i++)
                {
                    var e = data[i];
                    entryDataOffset = Serialize(entryData, internalIds.map[e.m_internalId], entryDataOffset);
                    entryDataOffset = Serialize(entryData, providers.map[e.m_provider], entryDataOffset);
                    entryDataOffset = Serialize(entryData, e.m_dependencies.Count == 0 ? -1 : keyIndexToEntries.map[e.m_dependencies[0]], entryDataOffset);
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
                int bucketDataOffset = Serialize(bucketData, keys.values.Count, 0);
                for (int i = 0; i < keys.values.Count; i++)
                {
                    var key = keys.values[i];
                    bucketDataOffset = Serialize(bucketData, keyDataOffset, bucketDataOffset);
                    keyDataOffset += WriteKey(key, keyData);
                    var entries = keyIndexToEntries[key];
                    bucketDataOffset = Serialize(bucketData, entries.Count, bucketDataOffset);
                    foreach (var e in entries)
                        bucketDataOffset = Serialize(bucketData, entryToIndex[e], bucketDataOffset);
                }
                m_bucketDataString = Convert.ToBase64String(bucketData);
                m_keyDataString = Convert.ToBase64String(keyData.ToArray());
            }
        }

        int WriteKey(object key, List<byte> keyData)
        {
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
                    return tmp2.Length + 1;
                }
                else
                {
                    keyData.Add((byte)KeyType.UnicodeString);
                    keyData.AddRange(tmp);
                    return tmp.Length + 1;
                }
            }
            else if (kt == typeof(UInt32))
            {
                byte[] tmp = BitConverter.GetBytes((UInt32)key);
                keyData.Add((byte)KeyType.UInt32);
                keyData.AddRange(tmp);
                return tmp.Length + 1;
            }
            else if (kt == typeof(UInt16))
            {
                byte[] tmp = BitConverter.GetBytes((UInt16)key);
                keyData.Add((byte)KeyType.UInt16);
                keyData.AddRange(tmp);
                return tmp.Length + 1;
            }
            else if (kt == typeof(Int32))
            {
                byte[] tmp = BitConverter.GetBytes((Int32)key);
                keyData.Add((byte)KeyType.Int32);
                keyData.AddRange(tmp);
                return tmp.Length + 1;
            }
            else if (kt == typeof(int))
            {
                byte[] tmp = BitConverter.GetBytes((UInt32)key);
                keyData.Add((byte)KeyType.UInt32);
                keyData.AddRange(tmp);
                return tmp.Length + 1;
            }
            else if (kt == typeof(Hash128))
            {
                var guid = (Hash128)key;
                byte[] tmp = System.Text.Encoding.ASCII.GetBytes(guid.ToString());
                keyData.Add((byte)KeyType.Hash128);
                keyData.AddRange(tmp);
                return tmp.Length + 1;
            }
            return 0;
        }

        public void SetData(List<ResourceLocationData> locations, List<string> labels)
        {
            var data = new List<DataEntry>();
            foreach (var rld in locations)
            {
                var keys = new List<object>();
                keys.Add(rld.m_address);
                if (!string.IsNullOrEmpty(rld.m_guid))
                    keys.Add(Hash128.Parse(rld.m_guid));
                for (int t = 0; rld.m_labelMask != 0 && t < labels.Count; t++)
                    if ((rld.m_labelMask & (1 << t)) != 0)
                        keys.Add(labels[t]);
                data.Add(new DataEntry(rld.m_internalId, rld.m_provider, keys, rld.m_dependencies));
            }
            SetData(data);
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
