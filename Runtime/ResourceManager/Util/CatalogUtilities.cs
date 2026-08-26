using System;
using System.IO;

namespace UnityEngine.ResourceManagement.Util
{
    /// <summary>
    /// Utility methods for working with catalog and catalog hash file paths.
    /// </summary>
    public class CatalogUtilities
    {
        private static bool TryFindExtension(in string path, out string extension)
        {
            if (path.Length > 0)
            {
                var fileName = Path.GetFileName(path);
                if (fileName.Length > 0)
                {
                    extension = Path.GetExtension(fileName);
                    return true;
                }
            }

            extension = string.Empty;
            return false;
        }

        private static string GetCatalogExtension(Uri uri)
        {
            // first check the path
            var localPath = uri.AbsolutePath;
            var fileName = Path.GetFileName(localPath);
            string extension;
            if (TryFindExtension(fileName, out extension))
                return extension;

            // this is everything after the #, there's no formal structure so
            // we just treat the whole thing like a path;
            var fragment = uri.Fragment.TrimStart('#');
            if (TryFindExtension(fragment, out extension))
                return extension;

            // the path has to be the last element when building
            // URLs so we only check the last query pair.
            var (_, value) = GetLastQueryPair(uri.Query);
            if (TryFindExtension(value, out extension))
                return extension;
            return string.Empty;
        }
        /// <summary>
        /// Gets the file extension of a catalog path.
        /// </summary>
        /// <param name="catalogPath">The catalog path or URL. It should end with the catalog file name.</param>
        /// <returns>The file extension of the catalog path (including the leading dot), or an empty string if it has none.</returns>
        public static string GetCatalogExtension(string catalogPath)
        {
            if (ResourceManagerConfig.IsPathRemote(catalogPath) && Uri.TryCreate(catalogPath, UriKind.Absolute, out Uri uri))
            {
                return GetCatalogExtension(uri);
            }
            // if the path isn't remote we don't deal with query strings
            var filename = Path.GetFileName(catalogPath);
            if (filename.Length > 0)
                return Path.GetExtension(catalogPath);
            return string.Empty;
        }

        /// <summary>
        /// Given a path to a <c>.hash</c> file, returns the corresponding catalog path with
        /// <paramref name="catalogExtension"/> (e.g. <c>.bin</c> or <c>.json</c>).
        /// Only the file extension is replaced, so a <c>.hash</c> that appears elsewhere
        /// in the path (e.g. a folder name) is left untouched.  Any URL query string is
        /// preserved correctly.
        /// </summary>
        /// <param name="hashPath">The path or URL of the <c>.hash</c> file. It should end with the file name.</param>
        /// <param name="catalogExtension">The catalog file extension to apply (e.g. <c>.bin</c> or <c>.json</c>).</param>
        /// <returns>The catalog path corresponding to the given hash path.</returns>
        public static string GetCatalogFilePath(string hashPath, string catalogExtension)
        {
            return ChangeExtensionPreservingQuery(hashPath, catalogExtension);
        }

        /// <summary>
        /// Given a path to a catalog file, returns the corresponding <c>.hash</c> file path.
        /// Only the file extension is replaced; any URL query string is preserved.
        /// </summary>
        /// <param name="catalogPath">The path or URL of the catalog file. It should end with the file name.</param>
        /// <returns>The hash file path corresponding to the given catalog path.</returns>
        public static string GetHashFilePath(string catalogPath)
        {
            return ChangeExtensionPreservingQuery(catalogPath, ".hash");
        }

        static bool TryCreateReplacement(string path, string newExtension, out string replacementPath)
        {
            if (path.Length > 0)
            {

                var fileName = Path.GetFileName(path);
                if (fileName.Length > 0)
                {
                    var newFileName = Path.ChangeExtension(fileName, newExtension);
                    replacementPath = path.Substring(0, path.LastIndexOf(fileName, StringComparison.Ordinal)) + newFileName;
                    return true;
                }
            }
            replacementPath = string.Empty;
            return false;
        }

        static string GetQueryStringWithoutLastPairValue(string queryString)
        {
            if (queryString.Length == 0)
                return string.Empty;
            queryString = queryString.TrimStart('?');
            var lastEqualIndex = queryString.LastIndexOf('=');
            if (lastEqualIndex < 0)
                return queryString;

            return queryString.Substring(0, lastEqualIndex + 1);
        }

        static (string, string) GetLastQueryPair(string queryString)
        {
            if (queryString.Length == 0)
                return (string.Empty, string.Empty);

            var lastQueryPair = queryString;
            var lastQueryPairIndex = queryString.LastIndexOf('&');
            if (lastQueryPairIndex > 0)
                lastQueryPair =  queryString.Substring(lastQueryPairIndex + 1);

            var lastEqualIndex = lastQueryPair.LastIndexOf('=');
            if (lastEqualIndex < 0)
                return (string.Empty, string.Empty);

            var key = lastQueryPair.Substring(0, lastEqualIndex).TrimStart('?');
            var value = lastQueryPair.Substring(lastEqualIndex + 1);
            return (key, value);
        }


        static string ChangeExtensionPreservingQuery(Uri uri, string newExtension)
        {
            // first check the path
            var localPath = uri.AbsolutePath;
            var builder = new UriBuilder(uri);
            if (uri.IsDefaultPort)
                builder.Port = -1;
            string replacementPath;
            if (TryCreateReplacement( localPath, newExtension, out replacementPath))
            {
                builder.Path = replacementPath;
                return builder.ToString();
            }

            var fragment = uri.Fragment.TrimStart('#');
            if (TryCreateReplacement( fragment, newExtension, out replacementPath))
            {
                builder.Fragment = replacementPath;
                return builder.ToString();
            }

            var queryString = uri.Query;
            var (_, value) = GetLastQueryPair(queryString);
            if (TryCreateReplacement( value, newExtension, out replacementPath))
            {
                builder.Query = GetQueryStringWithoutLastPairValue(queryString) + replacementPath;
                return builder.ToString();
            }
            return uri.ToString();
        }

        /// <summary>
        /// Changes the extension of the file name at the end of <paramref name="path"/> to
        /// <paramref name="newExtension"/>, leaving the rest of the path alone.
        /// <para>
        /// A path that does not end in a file name falls back to changing the extension on
        /// the portion before the query string, then re-appending the query. That path also
        /// avoids <see cref="Path.ChangeExtension"/> on a full URL, which fails when the
        /// query contains <c>:</c> (e.g. <c>value2:date=number</c>) because it reads the
        /// <c>:</c> as a volume separator and appends to the whole URL.
        /// </para>
        /// </summary>
        static string ChangeExtensionPreservingQuery(string path, string newExtension)
        {
            if (ResourceManagerConfig.IsPathRemote(path) && Uri.TryCreate(path, UriKind.Absolute, out Uri uri))
            {
                return ChangeExtensionPreservingQuery(uri, newExtension);
            }
            // if the path isn't remote we don't deal with query strings
            var filename = Path.GetFileName(path);
            if (filename.Length > 0)
            {
                var pathPreFilename = path.Substring(0, path.LastIndexOf(filename, StringComparison.Ordinal));
                return pathPreFilename + Path.ChangeExtension(filename, newExtension);
            }
            return path;
        }

        static string GetCacheKeySource(Uri uri)
        {
            // ok so what we want to do is use either the localPath + Uri or the
            // first check the path
            var builder = new UriBuilder(uri);
            if (uri.IsDefaultPort)
                builder.Port = -1;
            builder.Query = "";
            builder.Fragment = "";

            var fileName = Path.GetFileName(uri.AbsolutePath);
            // we have a file name from the base path, a path element without a trailing slash
            // WILL be treated as a file. Use /content/ not /content.
            if (fileName.Length > 0)
            {
                return builder.ToString();
            }

            // this is everything after the #, there's no formal structure so
            // we just treat the whole thing like a path;
            var fragment = uri.Fragment.TrimStart('#');
            fileName = Path.GetFileName(fragment);
            if (fileName.Length > 0)
            {
                builder.Fragment = fragment;
                return builder.ToString();
            }

            // we intentionally drop all but the last query pair, as
            // we only support adding the path to the end of the URL
            var (key, value) = GetLastQueryPair(uri.Query);
            fileName = Path.GetFileName(value);
            if (fileName.Length > 0)
            {
                builder.Query = $"{key}={value}";
                return builder.ToString();
            }

            return builder.ToString();
        }

        internal static string GetCacheKeySource(string path)
        {
            if (ResourceManagerConfig.IsPathRemote(path) && Uri.TryCreate(path, UriKind.Absolute, out Uri uri))
            {
                return GetCacheKeySource(uri);
            }
            return path;
        }
    }
}
