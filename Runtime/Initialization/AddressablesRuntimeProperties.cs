using System;
using System.Collections.Generic;
using System.Reflection;

namespace UnityEngine.AddressableAssets.Initialization
{
    /// <summary>
    /// Supports the evaluation of embedded runtime variables in addressables locations
    /// </summary>
    public static class AddressablesRuntimeProperties
    {
        // cache these to avoid GC allocations
        static Stack<string> s_TokenStack = new Stack<string>(32);
        static Stack<int> s_TokenStartStack = new Stack<int>(32);
        static bool s_StaticStacksAreInUse = false;

#if !UNITY_EDITOR && UNITY_WSA_10_0 && ENABLE_DOTNET
        static Assembly[] GetAssemblies()
        {
            //Not supported on UWP platforms
            return new Assembly[0];
        }

#else
        static Assembly[] GetAssemblies()
        {
            return AppDomain.CurrentDomain.GetAssemblies();
        }

#endif

        static Dictionary<string, string> s_CachedValues = new Dictionary<string, string>();

        internal static int GetCachedValueCount()
        {
            return s_CachedValues.Count;
        }

        /// <summary>
        /// Predefine a runtime property.
        /// </summary>
        /// <param name="name">The property name.</param>
        /// <param name="val">The property value.</param>
        public static void SetPropertyValue(string name, string val)
        {
            s_CachedValues[name] = val;
        }

        /// <summary>
        /// This will clear all PropertyValues that have been cached.  This includes all values set by
        /// <see cref="SetPropertyValue"/> as well as any reflection-evaluated properties.
        /// </summary>
        public static void ClearCachedPropertyValues()
        {
            s_CachedValues.Clear();
        }

        /// <summary>
        /// Evaluates a named property using cached values and static public fields and properties.  Be aware that a field or property may be stripped if not referenced anywhere else.
        /// </summary>
        /// <param name="name">The property name.</param>
        /// <returns>The value of the property.  If not found, the name is returned.</returns>
        public static string EvaluateProperty(string name)
        {
            Debug.Assert(s_CachedValues != null, "ResourceManagerConfig.GetGlobalVar - s_cachedValues == null.");

            if (string.IsNullOrEmpty(name))
                return string.Empty;

            string cachedValue;
            if (s_CachedValues.TryGetValue(name, out cachedValue))
                return cachedValue;

            int i = name.LastIndexOf('.');
            if (i < 0)
                return name;

            var className = name.Substring(0, i);
            var propName = name.Substring(i + 1);
            foreach (var a in GetAssemblies())
            {
                Type t = a.GetType(className, false, false);
                if (t == null)
                    continue;
                try
                {
                    var pi = t.GetProperty(propName, BindingFlags.Static | BindingFlags.FlattenHierarchy | BindingFlags.Public);
                    if (pi != null)
                    {
                        var v = pi.GetValue(null, null);
                        if (v != null)
                        {
                            s_CachedValues.Add(name, v.ToString());
                            return v.ToString();
                        }
                    }

                    var fi = t.GetField(propName, BindingFlags.Static | BindingFlags.FlattenHierarchy | BindingFlags.Public);
                    if (fi != null)
                    {
                        var v = fi.GetValue(null);
                        if (v != null)
                        {
                            s_CachedValues.Add(name, v.ToString());
                            return v.ToString();
                        }
                    }
                }
                catch (Exception)
                {
                    // ignored
                }
            }

            return name;
        }

        /// <summary>
        /// Evaluates all tokens deliminated by '{' and '}' in a string and evaluates them with the EvaluateProperty method.
        /// </summary>
        /// <param name="inputString">The input string.</param>
        /// <returns>The evaluated string after resolving all tokens.</returns>
        public static string EvaluateString(string inputString)
        {
            if (string.IsNullOrEmpty(inputString))
                return string.Empty;

            //only need to check the startDelimiter since the end only makes sense with one
            if (!inputString.Contains('{', StringComparison.Ordinal))
                return inputString;

            return EvaluateStringInternal(inputString, '{', '}', EvaluateProperty);
        }

        /// <summary>
        /// Evaluates all tokens deliminated by the specified delimiters in a string and evaluates them with the supplied method.
        /// </summary>
        /// <param name="inputString">The string to evaluate.</param>
        /// <param name="startDelimiter">The start token delimiter.</param>
        /// <param name="endDelimiter">The end token delimiter.</param>
        /// <param name="varFunc">Func that has a single string parameter and returns a string.</param>
        /// <returns>The evaluated string.</returns>
        public static string EvaluateString(string inputString, char startDelimiter, char endDelimiter, Func<string, string> varFunc)
        {
            if (string.IsNullOrEmpty(inputString))
                return string.Empty;

            //If there is no start delimiter, there is no reason to check any further since there can be no tokens
            if (!inputString.Contains(startDelimiter, StringComparison.Ordinal))
                return inputString;

            return EvaluateStringInternal(inputString, startDelimiter, endDelimiter, varFunc);
        }

        static string EvaluateStringInternal(string inputString, char startDelimiter, char endDelimiter, Func<string, string> varFunc)
        {
            string originalString = inputString;

            Stack<string> tokenStack;
            Stack<int> tokenStartStack;

            if (!s_StaticStacksAreInUse)
            {
                tokenStack = s_TokenStack;
                tokenStartStack = s_TokenStartStack;
                s_StaticStacksAreInUse = true;
            }
            else
            {
                tokenStack = new Stack<string>(32);
                tokenStartStack = new Stack<int>(32);
            }

            tokenStack.Push(inputString);
            int popTokenAt = inputString.Length;
            char[] delimiters = {startDelimiter, endDelimiter};
            bool delimitersMatch = startDelimiter == endDelimiter;

            int i = inputString.IndexOf(startDelimiter);
            int prevIndex = -2;
            while (i >= 0)
            {
                char c = inputString[i];
                if (c == startDelimiter && (!delimitersMatch || tokenStartStack.Count == 0))
                {
                    tokenStartStack.Push(i);
                    i++;
                }
                else if (c == endDelimiter && tokenStartStack.Count > 0)
                {
                    int start = tokenStartStack.Peek();
                    string token = inputString.Substring(start + 1, i - start - 1);
                    string tokenVal;

                    if (popTokenAt <= i)
                    {
                        tokenStack.Pop();
                    }

                    // check if the token is already included
                    if (tokenStack.Contains(token))
                        tokenVal = "#ERROR-CyclicToken#";
                    else
                    {
                        tokenVal = varFunc == null ? string.Empty : varFunc(token);
                        tokenStack.Push(token);
                    }

                    i = tokenStartStack.Pop();
                    popTokenAt = i + tokenVal.Length + 1;

                    if (i > 0)
                    {
                        int rhsStartIndex = i + token.Length + 2;
                        if (rhsStartIndex == inputString.Length)
                            inputString = inputString.Substring(0, i) + tokenVal;
                        else
                            inputString = inputString.Substring(0, i) + tokenVal + inputString.Substring(rhsStartIndex);
                    }
                    else
                        inputString = tokenVal + inputString.Substring(i + token.Length + 2);
                }

                bool infiniteLoopDetected = prevIndex == i;
                if (infiniteLoopDetected)
                    return "#ERROR-" + originalString + " contains unmatched delimiters#";

                prevIndex = i;
                i = inputString.IndexOfAny(delimiters, i);
            }

            tokenStack.Clear();
            tokenStartStack.Clear();
            if (ReferenceEquals(tokenStack, s_TokenStack))
                s_StaticStacksAreInUse = false;
            return inputString;
        }
    }
}
