using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Experimental.UIElements;
using UnityEngine.ResourceManagement;

namespace UnityEditor.AddressableAssets
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
        /// <param name="key">The key for the value.</param>
        /// <returns>The value as an object.</returns>        
        object GetValue(string key);
        /// <summary>
        /// Get a context value.
        /// </summary>
        /// <typeparam name="T">The type of the context value to retrieve.</typeparam>
        /// <param name="key">The key for the value.</param>
        /// <returns>The value cast to the type T.</returns>    
        T GetValue<T>(string key);
        /// <summary>
        /// Sets a context value.
        /// </summary>
        /// <param name="key">The key for the value.</param>
        /// <param name="value">The value to set.</param>
        void SetValue(string key, object value);
    }
    /// <summary>
    /// GUI for IDataBuilders.
    /// </summary>
    public interface IDataBuilderGUI
    {
        /// <summary>
        /// Show the gui in the provided container.
        /// </summary>
        /// <param name="container">The parent container.</param>
        void ShowGUI(VisualElement container);
        /// <summary>
        /// Called when the gui needs to update.
        /// </summary>
        /// <param name="rect">The current rect for the gui.</param>
        void UpdateGUI(Rect rect);
        /// <summary>
        /// Hide or destroy the gui.
        /// </summary>
        void HideGUI();
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
        /// Create a gui object.
        /// </summary>
        /// <param name="context">The context to use when creating the gui.</param>
        /// <returns>The gui object.  This may be null for some builders.</returns>
        IDataBuilderGUI CreateGUI(IDataBuilderContext context);

        /// <summary>
        /// Clears all cached data.
        /// </summary>
        void ClearCachedData();
    }
}