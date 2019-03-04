using System;

namespace UnityEngine.ResourceManagement.ResourceProviders
{
    /// <summary>
    /// Provides text from a local or remote URL
    /// </summary>
    public class TextDataProvider : RawDataProvider
    {
        /// <summary>
        /// Method to convert the text into the object type requested.  Usually the text contains a JSON formatted serialized object.
        /// </summary>
        /// <typeparam name="TObject">The object type to convert the text to.</typeparam>
        /// <param name="text">The text to be converted.</param>
        /// <returns>The text string is returned without conversion.</returns>
        public override TObject Convert<TObject>(string text)
        {
            return text as TObject;
        }
    }
}