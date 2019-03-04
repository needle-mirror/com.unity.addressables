namespace UnityEngine.ResourceManagement
{
    /// <summary>
    /// Providers that implement this interface will received Update calls from the ResourceManager each frame
    /// </summary>
    public interface IUpdateReceiver
    {
        /// <summary>
        /// This will be called once per frame by the ResourceManager
        /// </summary>
        /// <param name="unscaledDeltaTime">The unscaled delta time since the last invocation of this function</param>
        void Update(float unscaledDeltaTime);

        /// <summary>
        /// Signals to the ResourceManager if the Update method should be invoked. This is only checked when the provider is initially added to the ResourceManager.
        /// </summary>
        /// <value>True if the Update method should be invoked, false otherwise.</value>
        bool NeedsUpdate { get; }
    }
}