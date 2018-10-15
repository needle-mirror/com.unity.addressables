using System;

namespace UnityEditor.AddressableAssets
{
    /// <summary>
    /// Interface for providing configuration data for <see cref="IHostingService"/> implementations
    /// </summary>
    public interface IHostingServiceConfigurationProvider
    {
        /// <summary>
        /// Returns the Hosting Service content root path for the given <see cref="AddressableAssetGroup"/>
        /// </summary>
        string HostingServicesContentRoot { get; }
    }
}