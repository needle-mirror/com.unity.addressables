using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace UnityEditor.AddressableAssets
{
    internal class ProjectConfigData
    {
        [System.Serializable]
        private class ConfigSaveData
        {
            [SerializeField]
            internal bool m_postProfilerEvents = false;
            [SerializeField]
            internal long m_localLoadSpeed = 1024 * 1024 * 10;
            [SerializeField]
            internal long m_remoteLoadSpeed = 1024 * 1024 * 1;
            [SerializeField]
            internal bool m_hierarchicalSearch = false;
        }
        private static ConfigSaveData s_data = null;


        internal static bool postProfilerEvents
        {
            get
            {
                ValidateData();
                return s_data.m_postProfilerEvents;
            }
            set
            {
                ValidateData();
                s_data.m_postProfilerEvents = value;
                SaveData();
            }
        }
        internal static long localLoadSpeed
        {
            get
            {
                ValidateData();
                return s_data.m_localLoadSpeed;
            }
            set
            {
                ValidateData();
                s_data.m_localLoadSpeed = value;
                SaveData();
            }
        }
        internal static long remoteLoadSpeed
        {
            get
            {
                ValidateData();
                return s_data.m_remoteLoadSpeed;
            }
            set
            {
                ValidateData();
                s_data.m_remoteLoadSpeed = value;
                SaveData();
            }
        }
        internal static bool hierarchicalSearch
        {
            get
            {
                ValidateData();
                return s_data.m_hierarchicalSearch;
            }
            set
            {
                ValidateData();
                s_data.m_hierarchicalSearch = value;
                SaveData();
            }
        }

        internal static void SerializeForHash(Stream stream)
        {
            ValidateData();
            BinaryFormatter formatter = new BinaryFormatter();
            formatter.Serialize(stream, s_data);
        }

        private static void ValidateData()
        {
            if (s_data == null)
            {
                var dataPath = System.IO.Path.GetFullPath(".");
                dataPath = dataPath.Replace("\\", "/");
                dataPath += "/Library/AddressablesConfig.dat";

                if (File.Exists(dataPath))
                {
                    BinaryFormatter bf = new BinaryFormatter();
                    try
                    {
                        using (FileStream file = new FileStream(dataPath, FileMode.Open, FileAccess.Read))
                        {
                            var data = bf.Deserialize(file) as ConfigSaveData;
                            if (data != null)
                            {
                                s_data = data;
                            }
                        }
                    }
                    catch
                    {
                        //if the current class doesn't match what's in the file, Deserialize will throw. since this data is non-critical, we just wipe it
                        Addressables.LogWarning("Error reading Addressable Asset project config (play mode, etc.). Resetting to default.");
                        System.IO.File.Delete(dataPath);
                    }
                }

                //check if some step failed.
                if (s_data == null)
                {
                    s_data = new ConfigSaveData();
                }
            }
        }
        private static void SaveData()
        {
            if (s_data == null)
                return;

            var dataPath = Path.GetFullPath(".");
            dataPath = dataPath.Replace("\\", "/");
            dataPath += "/Library/AddressablesConfig.dat";

            BinaryFormatter bf = new BinaryFormatter();
            FileStream file = File.Create(dataPath);

            bf.Serialize(file, s_data);
            file.Close();
        }
    }
}
