using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor.AddressableAssets
{
    [Serializable]
    class AnalyzeRule
    {
        public const char kDelimiter = ':';
        [Serializable]
        internal class AnalyzeResult
        {
            [SerializeField]
            string m_Name;

            public string name
            {
                get { return m_Name; }
                set { m_Name = value; }
            }

            [SerializeField]
            MessageType m_Severity;
            public MessageType severity
            {
                get { return m_Severity; }
                set { m_Severity = value; }
            }

            public AnalyzeResult(string newName, MessageType sev = MessageType.None)
            {
                name = newName;
                severity = sev;
            }
        }
        internal virtual string name
        {
            get { return GetType().ToString(); }
        }
        internal virtual List<AnalyzeResult> RefreshAnalysis(AddressableAssetSettings settings)
        {
            return new List<AnalyzeResult>();
        }

        internal virtual void FixIssues(AddressableAssetSettings settings)
        {
        }

        internal virtual void ClearAnalysis()
        {
        }
    }

}
