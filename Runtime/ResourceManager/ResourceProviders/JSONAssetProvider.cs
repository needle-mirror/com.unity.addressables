using System;

namespace UnityEngine.ResourceManagement.ResourceProviders
{
    /// <summary>
    /// Converts JSON serialized text into the requested object.
    /// </summary>
    public class JsonAssetProvider : RawDataProvider
    {
        /// <summary>
        /// Converts raw text into requested object type via JSONUtility.FromJson.
        /// </summary>
        /// <typeparam name="TObject">Object type.</typeparam>
        /// <param name="text">The text to convert.</param>
        /// <returns>Converted object of type TObject.</returns>
        public override TObject Convert<TObject>(string text)
        {
            try
            {
                return JsonUtility.FromJson<TObject>(text);
            }
            catch (Exception e)
            {
                if (!IgnoreFailures)
                    Debug.LogException(e);

                return default(TObject);
            }
        }
    }
}