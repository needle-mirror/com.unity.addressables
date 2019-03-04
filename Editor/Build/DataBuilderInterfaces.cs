using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor.AddressableAssets.Build
{
    /// <summary>
    /// Used to pass context data into IDataBuilders.
    /// </summary>
    public interface IDataBuilderContext
    {
        /// <summary>
        /// The available keys.
        /// </summary>
        ICollection<string> Keys { get; }

        /// <summary>
        /// Get a context value.
        /// </summary>
        /// <typeparam name="T">The type of the context value to retrieve.</typeparam>
        /// <param name="key">The key for the value.</param>
        /// <param name="defaultVal">The default value.</param>
        /// <returns>The value cast to the type T.</returns>    
        T GetValue<T>(string key, T defaultVal = default(T));

        /// <summary>
        /// Sets a context value.
        /// </summary>
        /// <param name="key">The key for the value.</param>
        /// <param name="value">The value to set.</param>
        void SetValue(string key, object value);
    }

    /// <summary>
    /// The result of IDataBuilder.Build.
    /// </summary>
    public interface IDataBuilderResult
    {
        /// <summary>
        /// Duration of the build in seconds.
        /// </summary>
        double Duration { get; set; }
        /// <summary>
        /// Error string, if any.  If Succeeded is true, this may be null.
        /// </summary>
        string Error { get; set; }
        /// <summary>
        /// Path of runtime settings file
        /// </summary>
        string OutputPath { get; set; }
    }

    /// <summary>
    /// Builds objects of type IDataBuilderResult.
    /// </summary>
    public interface IDataBuilder
    {
        /// <summary>
        /// The name of the builder, used for GUI.
        /// </summary>
        string Name { get; }
        /// <summary>
        /// Can this builder build the type of data requested.
        /// </summary>
        /// <typeparam name="T">The data type.</typeparam>
        /// <returns>True if the build can build it.</returns>
        bool CanBuildData<T>() where T : IDataBuilderResult;
        /// <summary>
        /// Build the data of a specific type.
        /// </summary>
        /// <typeparam name="T">The data type.</typeparam>
        /// <param name="context">The context used to build the data.</param>
        /// <returns>The built data.</returns>
        T BuildData<T>(IDataBuilderContext context) where T : IDataBuilderResult;

        /// <summary>
        /// Clears all cached data.
        /// </summary>
        void ClearCachedData();
    }
}