using System;
using System.Runtime.Serialization;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace UnityEngine.ResourceManagement.Util
{
    /// <summary>
    /// Base class for all ResourceManager related exceptions.
    /// </summary>
    public class ResourceManagerException : Exception
    {
        /// <summary>
        /// Construct a new ResourceManagerException.
        /// </summary>
        public ResourceManagerException() { }
        /// <summary>
        /// Construct a new ResourceManagerException.
        /// </summary>
        /// <param name="message">Message to describe the exception.</param>
        public ResourceManagerException(string message) : base(message) { }
        /// <summary>
        /// Construct a new ResourceManagerException.
        /// </summary>
        /// <param name="message">Message to describe the exception.</param>
        /// <param name="innerException">Inner exception that caused this exception.</param>
        public ResourceManagerException(string message, Exception innerException) : base(message, innerException) { }
        /// <summary>
        /// Construct a new ResourceManagerException.
        /// </summary>
        /// <param name="message">Message to describe the exception.</param>
        /// <param name="context">Context related to the exception.</param>
        protected ResourceManagerException(SerializationInfo message, StreamingContext context) : base(message, context) { }
    }
    /// <summary>
    /// Exception returned when the IResourceProvider is not found for a location.
    /// </summary>
    public class UnknownResourceProviderException : ResourceManagerException
    {
        /// <summary>
        /// The location that contains the provider id that was not found.
        /// </summary>
        public IResourceLocation Location { get; private set; }
        /// <summary>
        /// Construct a new UnknownResourceProviderException
        /// </summary>
        /// <param name="location">The location that caused the exception to be created.</param>
        public UnknownResourceProviderException(IResourceLocation location)
        {
            Location = location;
        }
        /// <summary>
        ///  Construct a new UnknownResourceProviderException
        /// </summary>
        public UnknownResourceProviderException() { }
        /// <summary>
        /// Construct a new UnknownResourceProviderException
        /// </summary>
        /// <param name="message">Message to describe the exception.</param>
        public UnknownResourceProviderException(string message) : base(message) { }
        /// <summary>
        /// Construct a new UnknownResourceProviderException
        /// </summary>
        /// <param name="message">Message to describe the exception.</param>
        /// <param name="innerException">Inner exception that caused this exception.</param>
        public UnknownResourceProviderException(string message, Exception innerException) : base(message, innerException) { }
        /// <summary>
        /// Construct a new UnknownResourceProviderException
        /// </summary>
        /// <param name="message">Message to describe the exception.</param>
        /// <param name="context">Context related to the exception.</param>
        protected UnknownResourceProviderException(SerializationInfo message, StreamingContext context) : base(message, context) { }

        /// <summary>
        /// Returns a string describing  this exception
        /// </summary>
        public override string Message
        {
            get
            {
                return base.Message + ", ProviderId=" + Location.ProviderId + ", Location=" + Location;
            }
        }
        /// <summary>
        /// Returns string representation of exception.
        /// </summary>
        /// <returns>String representation of exception.</returns>
        public override string ToString()
        {
            return Message;
        }

    }

    /// <summary>
    /// Exception created when an IResourceProvider is unabled to load the specified location.
    /// </summary>
    public class ResourceProviderFailedException : ResourceManagerException
    {
        /// <summary>
        /// The location that is unable to be loaded.
        /// </summary>
        public IResourceLocation Location { get; private set; }
        /// <summary>
        /// The provider that is unable to load the location.
        /// </summary>
        public IResourceProvider Provider { get; private set; }
        /// <summary>
        /// Construct a new ResourceProviderFailedException
        /// </summary>
        /// <param name="provider"></param>
        /// <param name="location"></param>
        public ResourceProviderFailedException(IResourceProvider provider, IResourceLocation location)
        {
            Provider = provider;
            Location = location;
        }
        /// <summary>
        /// Construct a new ResourceProviderFailedException
        /// </summary>
        public ResourceProviderFailedException() { }
        /// <summary>
        /// Construct a new ResourceProviderFailedException
        /// </summary>
        /// <param name="message">Message to describe the exception.</param>
        public ResourceProviderFailedException(string message) : base(message) { }
        /// <summary>
        /// Construct a new ResourceProviderFailedException
        /// </summary>
        /// <param name="message">Message to describe the exception.</param>
        /// <param name="innerException">Inner exception that caused this exception.</param>
        public ResourceProviderFailedException(string message, Exception innerException) : base(message, innerException) { }
        /// <summary>
        /// Construct a new ResourceProviderFailedException
        /// </summary>
        /// <param name="message">Message to describe the exception.</param>
        /// <param name="context">Context related to the exception.</param>
        protected ResourceProviderFailedException(SerializationInfo message, StreamingContext context) : base(message, context) { }

        /// <summary>
        /// Returns a descriptive string for the exception.
        /// </summary>
        public override string Message
        {
            get
            {
                return base.Message + ", Provider=" + Provider + ", Location=" + Location;
            }
        }
    }
}