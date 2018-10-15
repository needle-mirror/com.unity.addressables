using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor.AddressableAssets
{
    public abstract class BaseHostingService : IHostingService
    {
        private const string k_hostingServiceContentRootKey = "ContentRoot";
        private const string k_isHostingServiceRunningKey = "IsEnabled";
        private const string k_descriptiveNameKey = "DescriptiveName";
        private const string k_instanceIdKey = "InstanceId";

        public abstract List<string> HostingServiceContentRoots { get; }
        public abstract Dictionary<string, string> ProfileVariables { get; }
        public abstract bool IsHostingServiceRunning { get; }
        public abstract void StartHostingService();
        public abstract void StopHostingService();
        public abstract void OnGUI();

        private ILogger m_logger = Debug.unityLogger;

        public ILogger Logger
        {
            get { return m_logger; }
            set { m_logger = value ?? Debug.unityLogger; }
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
            dataStore.SetData(k_hostingServiceContentRootKey, string.Join(";", HostingServiceContentRoots.ToArray()));
            dataStore.SetData(k_isHostingServiceRunningKey, IsHostingServiceRunning);
            dataStore.SetData(k_descriptiveNameKey, DescriptiveName);
            dataStore.SetData(k_instanceIdKey, InstanceId);
        }

        /// <inheritdoc/>
        public virtual void OnAfterDeserialize(KeyDataStore dataStore)
        {
            var contentRoots = dataStore.GetData(k_hostingServiceContentRootKey, string.Empty);
            HostingServiceContentRoots.AddRange(contentRoots.Split(';'));

            if (dataStore.GetData(k_isHostingServiceRunningKey, false))
                StartHostingService();

            DescriptiveName = dataStore.GetDataString(k_descriptiveNameKey, string.Empty);
            InstanceId = dataStore.GetData(k_instanceIdKey, -1);
        }

        private static T[] ArrayPush<T>(T[] arr, T val)
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