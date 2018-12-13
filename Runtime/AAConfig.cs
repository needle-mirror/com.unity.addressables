using System;

namespace UnityEngine.AddressableAssets
{
    /// <summary>
    /// Obsolete. Use AddressablesRuntimeProperties instead
    /// </summary>
    [Obsolete("Use AddressablesRuntimeProperties instead.")]
    public static class AAConfig
    {
        /// <summary>
        /// Obsolete. Use AddressablesRuntimeProperties instead
        /// </summary>
        [Obsolete("Use AddressablesRuntimeProperties instead. (UnityUpgradable) -> AddressablesRuntimeProperties.SetPropertyValue")]
        public static void AddCachedValue(string key, string val)
        {
            AddressablesRuntimeProperties.SetPropertyValue(key, val);
        }

        /// <summary>
        /// Obsolete. Use AddressablesRuntimeProperties instead
        /// </summary>
        [Obsolete("Use AddressablesRuntimeProperties instead. (UnityUpgradable) -> AddressablesRuntimeProperties.EvaluateProperty")]
        public static string GetGlobalVar(string variableName)
        {
            return AddressablesRuntimeProperties.EvaluateProperty(variableName);
        }

        /// <summary>
        /// Obsolete. Use AddressablesRuntimeProperties instead
        /// </summary>
        [Obsolete("Use AddressablesRuntimeProperties instead. (UnityUpgradable) -> AddressablesRuntimeProperties.EvaluateString")]
        public static string ExpandPathWithGlobalVariables(string inputString)
        {
            return AddressablesRuntimeProperties.EvaluateString(inputString);
        }

        /// <summary>
        /// Obsolete. Use AddressablesRuntimeProperties instead
        /// </summary>
        [Obsolete("Use AddressablesRuntimeProperties instead. (UnityUpgradable) -> AddressablesRuntimeProperties.EvaluateString")]
        public static string ExpandWithVariables(string inputString, char startDelimiter, char endDelimiter, Func<string, string> varFunc)
        {
            return AddressablesRuntimeProperties.EvaluateString(inputString, startDelimiter, endDelimiter, varFunc);
        }
    }
}