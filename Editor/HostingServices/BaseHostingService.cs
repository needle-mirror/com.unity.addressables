using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor.AddressableAssets
{
    /// <summary>
    /// Base class for hosting services.
    /// </summary>
    public abstract class BaseHostingService : IHostingService
    {
        const string k_HostingServiceContentRootKey = "ContentRoot";
        const string k_IsHostingServiceRunningKey = "IsEnabled";
        const string k_DescriptiveNameKey = "DescriptiveName";
        const string k_InstanceIdKey = "InstanceId";

        /// <summary>
        /// List of content roots for hosting service.
        /// </summary>
        public abstract List<string> HostingServiceContentRoots { get; }
        /// <summary>
        /// Dictionary of profile variables defined by the hosting service.
        /// </summary>
        public abstract Dictionary<string, string> ProfileVariables { get; }
        /// <summary>
        /// Gets the current running status of the hosting service.
        /// </summary>
        public abstract bool IsHostingServiceRunning { get; }
        /// <summary>
        /// Starts the hosting service.
        /// </summary>
        public abstract void StartHostingService();
        /// <summary>
        /// Stops the hosting service.
        /// </summary>
        public abstract void StopHostingService();
        /// <summary>
        /// Render the hosting service GUI.
        /// </summary>
        public abstract void OnGUI();

        ILogger m_Logger = Debug.unityLogger;

        /// <summary>
        /// Get and set the loger for the hosting service.
        /// </summary>
        public ILogger Logger
        {
            get { return m_Logger; }
            set { m_Logger = value ?? Debug.unityLogger; }
        }

        protected virtual string DisambiguateProfileVar(string key)
        {
            return string.Format("{0}.ID_{1}", key, InstanceId);
        }

        /// <inheritdoc/>
        public virtual string DescriptiveName { get; set; }

        /// <inheritdoc/>
        public virtual int InstanceId { get; set; }

        /// <inheritdoc/>	
        public virtual string EvaluateProfileString(string key)
        {
            string retVal;
            ProfileVariables.TryGetValue(key, out retVal);
            return retVal;
        }

        /// <inheritdoc/>
        public virtual void OnBeforeSerialize(KeyDataStore dataStore)
        {
            dataStore.SetData(k_HostingServiceContentRootKey, string.Join(";", HostingServiceContentRoots.ToArray()));
            dataStore.SetData(k_IsHostingServiceRunningKey, IsHostingServiceRunning);
            dataStore.SetData(k_DescriptiveNameKey, DescriptiveName);
            dataStore.SetData(k_InstanceIdKey, InstanceId);
        }

        /// <inheritdoc/>
        public virtual void OnAfterDeserialize(KeyDataStore dataStore)
        {
            var contentRoots = dataStore.GetData(k_HostingServiceContentRootKey, string.Empty);
            HostingServiceContentRoots.AddRange(contentRoots.Split(';'));

            if (dataStore.GetData(k_IsHostingServiceRunningKey, false))
                StartHostingService();

            DescriptiveName = dataStore.GetDataString(k_DescriptiveNameKey, string.Empty);
            InstanceId = dataStore.GetData(k_InstanceIdKey, -1);
        }

        static T[] ArrayPush<T>(T[] arr, T val)
        {
            var newArr = new T[arr.Length + 1];
            Array.Copy(arr, newArr, arr.Length);
            newArr[newArr.Length - 1] = val;
            return newArr;
        }

        protected void LogFormat(LogType logType, string format, object[] args)
        {
            Logger.LogFormat(logType, format, ArrayPush(args, this));
        }

        protected void Log(string format, params object[] args)
        {
            LogFormat(LogType.Log, format, args);
        }

        protected void LogWarning(string format, params object[] args)
        {
            LogFormat(LogType.Warning, format, args);
        }

        protected void LogError(string format, params object[] args)
        {
            LogFormat(LogType.Error, format, args);
        }
    }
}