using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace UnityEditor.AddressableAssets.Build
{
    public class FileRegistry
    {
        private readonly HashSet<string> m_FilePaths;

        public FileRegistry()
        {
            m_FilePaths = new HashSet<string>();
        }

        public IEnumerable<string> GetFilePaths()
        {
            return new HashSet<string>(m_FilePaths);
        }

        public void AddFile(string path)
        {
            m_FilePaths.Add(path);
        }

        public void RemoveFile(string path)
        {
            m_FilePaths.Remove(path);
        }

        /// <summary>
        /// Given a bundle name, determine the file path for the bundle.
        /// </summary>
        /// <param name="bundleName">The name of the bundle.</param>
        /// <returns>The full file path.</returns>
        public string GetFilePathForBundle(string bundleName)
        {
            bundleName = Path.GetFileNameWithoutExtension(bundleName);
            return m_FilePaths.FirstOrDefault((entry) => entry.Contains(bundleName));
        }

        /// <summary>
        /// Replace an entry in the File Registry with a new bundle name.
        /// </summary>
        /// <param name="bundleName">The bundle name to replace.</param>
        /// <param name="newFileRegistryEntry">The new file registry bundle name.</param>
        /// <returns>Returns true if a successful replacement occured.</returns>
        public bool ReplaceBundleEntry(string bundleName, string newFileRegistryEntry)
        {
            if (!m_FilePaths.Contains(newFileRegistryEntry))
            {
                m_FilePaths.RemoveWhere((entry) => entry.Contains(bundleName));
                AddFile(newFileRegistryEntry);
                return true;
            }

            return false;
        }
    }
}
