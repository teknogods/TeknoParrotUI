using System;
using System.Collections.Generic;
using System.Linq;

namespace TeknoParrotUi.Common.Android
{
    /// <summary>
    /// Converts Android Storage Access Framework document URIs for shared
    /// storage into the physical path Winlator needs. Avalonia's
    /// TryGetLocalPath intentionally returns null for content:// URIs.
    /// </summary>
    public static class AndroidDocumentPathResolver
    {
        private const string ExternalStorageAuthority =
            "com.android.externalstorage.documents";

        public static bool TryResolve(string value, out string path)
        {
            path = string.Empty;
            if (string.IsNullOrWhiteSpace(value) ||
                !Uri.TryCreate(value, UriKind.Absolute, out var uri))
                return false;

            if (string.Equals(uri.Scheme, Uri.UriSchemeFile,
                    StringComparison.OrdinalIgnoreCase))
                return TryNormalizeSharedPath(uri.LocalPath, out path);
            if (!string.Equals(uri.Scheme, "content",
                    StringComparison.OrdinalIgnoreCase))
                return false;

            var unescapedPath = Uri.UnescapeDataString(uri.AbsolutePath)
                .Replace('\\', '/');
            if (string.Equals(uri.Host, ExternalStorageAuthority,
                    StringComparison.OrdinalIgnoreCase))
            {
                const string documentMarker = "/document/";
                const string treeMarker = "/tree/";
                var marker = unescapedPath.Contains(
                    documentMarker,
                    StringComparison.OrdinalIgnoreCase)
                    ? documentMarker
                    : treeMarker;
                var markerIndex = unescapedPath.IndexOf(
                    marker,
                    StringComparison.OrdinalIgnoreCase);
                if (markerIndex >= 0)
                {
                    var documentId =
                        unescapedPath[(markerIndex + marker.Length)..];
                    if (TryResolveDocumentId(documentId, out path))
                        return true;
                }
            }

            // Some vendor document providers include the physical shared
            // storage path in the URI. Accept only Android shared-storage
            // roots; never reinterpret arbitrary provider paths.
            var storageIndex = unescapedPath.IndexOf(
                "/storage/",
                StringComparison.OrdinalIgnoreCase);
            return storageIndex >= 0 &&
                   TryNormalizeSharedPath(
                       unescapedPath[storageIndex..],
                       out path);
        }

        private static bool TryResolveDocumentId(
            string documentId,
            out string path)
        {
            path = string.Empty;
            if (documentId.StartsWith(
                    "raw:",
                    StringComparison.OrdinalIgnoreCase))
                return TryNormalizeSharedPath(documentId[4..], out path);

            var separator = documentId.IndexOf(':');
            if (separator < 0)
                return false;
            var volume = documentId[..separator];
            var relative = documentId[(separator + 1)..];
            string root;
            if (string.Equals(volume, "primary",
                    StringComparison.OrdinalIgnoreCase))
            {
                root = "/storage/emulated/0";
            }
            else if (string.Equals(volume, "home",
                         StringComparison.OrdinalIgnoreCase))
            {
                root = "/storage/emulated/0/Documents";
            }
            else
            {
                if (volume.Length == 0 ||
                    volume.Any(character =>
                        !char.IsLetterOrDigit(character) &&
                        character is not '-' and not '_'))
                    return false;
                root = "/storage/" + volume;
            }

            return TryCombine(root, relative, out path);
        }

        private static bool TryCombine(
            string root,
            string relative,
            out string path)
        {
            path = string.Empty;
            if (!TryGetSafeSegments(relative, out var segments))
                return false;
            path = segments.Count == 0
                ? root
                : root + "/" + string.Join("/", segments);
            return true;
        }

        public static bool TryNormalizeSharedPath(
            string candidate,
            out string path)
        {
            path = string.Empty;
            var normalized = candidate.Replace('\\', '/');
            if (!normalized.StartsWith("/", StringComparison.Ordinal) ||
                (!normalized.StartsWith(
                     "/storage/",
                     StringComparison.OrdinalIgnoreCase) &&
                 !normalized.StartsWith(
                     "/sdcard/",
                     StringComparison.OrdinalIgnoreCase)))
                return false;
            if (!TryGetSafeSegments(normalized, out var segments) ||
                segments.Count == 0)
                return false;
            path = "/" + string.Join("/", segments);
            return true;
        }

        private static bool TryGetSafeSegments(
            string value,
            out IReadOnlyList<string> segments)
        {
            var result = value
                .Replace('\\', '/')
                .Split('/', StringSplitOptions.RemoveEmptyEntries)
                .ToArray();
            if (result.Any(segment =>
                    segment is "." or ".." ||
                    segment.Any(char.IsControl)))
            {
                segments = Array.Empty<string>();
                return false;
            }
            segments = result;
            return true;
        }
    }
}
