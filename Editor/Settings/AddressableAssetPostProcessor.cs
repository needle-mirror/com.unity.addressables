
using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor.AddressableAssets
{
    class AddressablesAssetPostProcessor : AssetPostprocessor
    {
        struct ImportSet
        {
            public string[] importedAssets;
            public string[] deletedAssets;
            public string[] movedAssets;
            public string[] movedFromAssetPaths;
        }

        static List<ImportSet> m_buffer;
        static Action<string[], string[], string[], string[]> m_handler;
        public static Action<string[], string[], string[], string[]> OnPostProcess
        {
            get
            {
                return m_handler;
            }
            set
            {
                m_handler = value;
                if (m_handler != null && m_buffer != null)
                {
                    foreach (var b in m_buffer)
                        m_handler(b.importedAssets, b.deletedAssets, b.movedAssets, b.movedFromAssetPaths);
                    m_buffer = null;
                }
            }
        }
        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {

            if (m_handler != null)
            {
                m_handler(importedAssets, deletedAssets, movedAssets, movedFromAssetPaths);
            }
            else
            {
                //only buffer imports if they will be consumed by the settings object
                if (AddressableAssetSettingsDefaultObject.SettingsExists)
                {
                    if (m_buffer == null)
                        m_buffer = new List<ImportSet>();
                    m_buffer.Add(new ImportSet() { importedAssets = importedAssets, deletedAssets = deletedAssets, movedAssets = movedAssets, movedFromAssetPaths = movedFromAssetPaths });
                }
            }
        }
    }
}