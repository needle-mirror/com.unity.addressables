namespace UnityEditor.AddressableAssets.Build
{
    /// <summary>
    /// Determines how content directory build artifacts are packed into archive files.
    /// </summary>
    /// <remarks>
    /// The order of these values is serialized as an integer and is also used to index the
    /// settings inspector dropdown. Do not reorder them or insert new values between them.
    /// </remarks>
    public enum ContentDirectoryArchiveMode
    {
        /// <summary>
        /// Artifact files are exported to the output path as individual loose files and are not archived.
        /// </summary>
        None,

        /// <summary>
        /// Artifact files are packed into archive files without compression.
        /// </summary>
        Uncompressed,

        /// <summary>
        /// Artifact files are packed into archive files compressed with LZ4.
        /// </summary>
        Lz4
    }
}
