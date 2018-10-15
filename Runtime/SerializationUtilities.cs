using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEngine.AddressableAssets
{
    internal static class SerializationUtilities
    {
        internal enum ObjectType
        {
            ASCIIString,
            UnicodeString,
            UInt16,
            UInt32,
            Int32,
            Hash128,
            JsonObject
        }

        internal static int ReadInt32FromByteArray(byte[] data, int offset)
        {
            return ((int)data[offset]) | (((int)data[offset + 1]) << 8) | (((int)data[offset + 2]) << 16) | (((int)data[offset + 3]) << 24);
        }

        internal static int WriteInt32ToByteArray(byte[] data, int val, int offset)
        {
            data[offset] = (byte)(val & 0xFF);
            data[offset + 1] = (byte)((val >> 8) & 0xFF);
            data[offset + 2] = (byte)((val >> 16) & 0xFF);
            data[offset + 3] = (byte)((val >> 24) & 0xFF);
            return offset + 4;
        }

        /// <summary>
        /// Deserializes an object from an array at a specified index.  Supported types are ASCIIString, UnicodeString, UInt16, UInt32, Int32, Hash128, JsonObject
        /// </summary>
        /// <param name="keyData">The array of bytes for the object. The first byte is the ObjectType. The rest depends on the type.</param>
        /// <param name="dataIndex">The index of the first byte of the data.</param>
        /// <returns>The deserialized object.</returns>
        internal static object ReadObjectFromByteArray(byte[] keyData, int dataIndex)
        {
            try
            {
                ObjectType keyType = (ObjectType)keyData[dataIndex];
                dataIndex++;
                switch (keyType)
                {
                    case ObjectType.UnicodeString:
                        {
                            var dataLength = BitConverter.ToInt32(keyData, dataIndex);
                            return System.Text.Encoding.Unicode.GetString(keyData, dataIndex + 4, dataLength);
                        }
                    case ObjectType.ASCIIString:
                        {
                            var dataLength = BitConverter.ToInt32(keyData, dataIndex);
                            return System.Text.Encoding.ASCII.GetString(keyData, dataIndex + 4, dataLength);
                        }
                    case ObjectType.UInt16: return BitConverter.ToUInt16(keyData, dataIndex);
                    case ObjectType.UInt32: return BitConverter.ToUInt32(keyData, dataIndex);
                    case ObjectType.Int32: return BitConverter.ToInt32(keyData, dataIndex);
                    case ObjectType.Hash128: return Hash128.Parse(System.Text.Encoding.ASCII.GetString(keyData, dataIndex + 1, keyData[dataIndex]));
                    case ObjectType.JsonObject:
                        {
                            int assemblyNameLength = keyData[dataIndex];
                            dataIndex++;
                            string assemblyName = System.Text.Encoding.ASCII.GetString(keyData, dataIndex, assemblyNameLength);
                            dataIndex += assemblyNameLength;

                            int classNameLength = keyData[dataIndex];
                            dataIndex++;
                            string className = System.Text.Encoding.ASCII.GetString(keyData, dataIndex, classNameLength);
                            dataIndex += classNameLength;
                            int jsonLength = BitConverter.ToInt32(keyData, dataIndex);
                            dataIndex += 4;
                            string jsonText = System.Text.Encoding.Unicode.GetString(keyData, dataIndex, jsonLength);
                            var assembly = System.Reflection.Assembly.Load(assemblyName);
                            var t = assembly.GetType(className);
                            return JsonUtility.FromJson(jsonText, t);
                        }
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            return null;
        }

        /// <summary>
        /// Write an object to a byte array
        /// </summary>
        /// <param name="obj">The object to write.</param>
        /// <param name="buffer">The list of bytes to write to.</param>
        /// <returns>The number of bytes written.</returns>
        internal static int WriteObjectToByteList(object obj, List<byte> buffer)
        {
            var objectType = obj.GetType();
            if (objectType == typeof(string))
            {
                string str = obj as string;
                byte[] tmp = System.Text.Encoding.Unicode.GetBytes(str);
                byte[] tmp2 = System.Text.Encoding.ASCII.GetBytes(str);
                if (System.Text.Encoding.Unicode.GetString(tmp) == System.Text.Encoding.ASCII.GetString(tmp2))
                {
                    buffer.Add((byte)ObjectType.ASCIIString);
                    buffer.AddRange(BitConverter.GetBytes(tmp2.Length));
                    buffer.AddRange(tmp2);
                    return tmp2.Length + 5;
                }
                else
                {
                    buffer.Add((byte)ObjectType.UnicodeString);
                    buffer.AddRange(BitConverter.GetBytes(tmp.Length));
                    buffer.AddRange(tmp);
                    return tmp.Length + 5;
                }
            }
            else if (objectType == typeof(UInt32))
            {
                byte[] tmp = BitConverter.GetBytes((UInt32)obj);
                buffer.Add((byte)ObjectType.UInt32);
                buffer.AddRange(tmp);
                return tmp.Length + 1;
            }
            else if (objectType == typeof(UInt16))
            {
                byte[] tmp = BitConverter.GetBytes((UInt16)obj);
                buffer.Add((byte)ObjectType.UInt16);
                buffer.AddRange(tmp);
                return tmp.Length + 1;
            }
            else if (objectType == typeof(Int32))
            {
                byte[] tmp = BitConverter.GetBytes((Int32)obj);
                buffer.Add((byte)ObjectType.Int32);
                buffer.AddRange(tmp);
                return tmp.Length + 1;
            }
            else if (objectType == typeof(int))
            {
                byte[] tmp = BitConverter.GetBytes((UInt32)obj);
                buffer.Add((byte)ObjectType.UInt32);
                buffer.AddRange(tmp);
                return tmp.Length + 1;
            }
            else if (objectType == typeof(Hash128))
            {
                var guid = (Hash128)obj;
                byte[] tmp = System.Text.Encoding.ASCII.GetBytes(guid.ToString());
                buffer.Add((byte)ObjectType.Hash128);
                buffer.Add((byte)tmp.Length);
                buffer.AddRange(tmp);
                return tmp.Length + 2;
            }
            else
            {
                var attrs = objectType.GetCustomAttributes(typeof(System.SerializableAttribute), true);
                if (attrs == null || attrs.Length == 0)
                    return 0;
                int length = 0;
                buffer.Add((byte)ObjectType.JsonObject);
                length++;

                //write assembly name
                byte[] tmpAssemblyName = System.Text.Encoding.ASCII.GetBytes(objectType.Assembly.FullName);
                buffer.Add((byte)tmpAssemblyName.Length);
                length++;
                buffer.AddRange(tmpAssemblyName);
                length += tmpAssemblyName.Length;

                //write class name
                byte[] tmpClassName = System.Text.Encoding.ASCII.GetBytes(objectType.FullName);
                buffer.Add((byte)tmpClassName.Length);
                length++;
                buffer.AddRange(tmpClassName);
                length += tmpClassName.Length;

                //write json data
                byte[] tmpJson = System.Text.Encoding.Unicode.GetBytes(JsonUtility.ToJson(obj));
                buffer.AddRange(BitConverter.GetBytes((Int32)tmpJson.Length));
                length += 4;
                buffer.AddRange(tmpJson);
                length += tmpJson.Length;
                return length;
            }
        }

    }
}