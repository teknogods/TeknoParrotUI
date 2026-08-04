using System;
using Android.Content;
using Android.Provider;
using Android.Util;
using AndroidUri = Android.Net.Uri;

namespace TeknoParrotUi.Avalonia.Android;

/// <summary>
/// Resolves opaque Storage Access Framework document IDs, including Samsung's
/// Downloads-provider msf IDs, through Android's own content providers.
/// </summary>
internal sealed class AndroidDocumentProviderPathResolver
{
    private const string DownloadsAuthority =
        "com.android.providers.downloads.documents";
    private const string MediaAuthority =
        "com.android.providers.media.documents";
    private readonly Context _context;

    public AndroidDocumentProviderPathResolver(Context context)
    {
        _context = context;
    }

    public string? Resolve(string value)
    {
        var uri = AndroidUri.Parse(value);
        if (uri == null ||
            !string.Equals(uri.Scheme, ContentResolver.SchemeContent,
                StringComparison.OrdinalIgnoreCase))
            return null;

        var direct = QueryDataColumn(uri);
        if (!string.IsNullOrWhiteSpace(direct))
            return direct;

        // A URI returned by ACTION_OPEN_DOCUMENT carries an exact read grant,
        // but modern Downloads/Media providers intentionally hide the legacy
        // _data column and this app does not request broad storage access.
        // Follow the granted descriptor instead: local SAF providers expose
        // the backing FUSE path through /proc/self/fd, while cloud/streaming
        // providers resolve to a pipe or anonymous descriptor and are safely
        // rejected below.
        var descriptorPath = ResolveGrantedDescriptorPath(uri);
        if (!string.IsNullOrWhiteSpace(descriptorPath))
            return descriptorPath;

        var authority = uri.Authority ?? string.Empty;
        if (!string.Equals(authority, DownloadsAuthority,
                StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(authority, MediaAuthority,
                StringComparison.OrdinalIgnoreCase))
            return null;

        string? documentId;
        try
        {
            documentId = DocumentsContract.GetDocumentId(uri);
        }
        catch
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(documentId))
            return null;
        if (documentId.StartsWith("raw:", StringComparison.OrdinalIgnoreCase))
            return documentId[4..];

        var separator = documentId.LastIndexOf(':');
        var numericId = separator >= 0
            ? documentId[(separator + 1)..]
            : documentId;
        if (!long.TryParse(numericId, out var id) || id < 0)
            return null;

        if (string.Equals(authority, MediaAuthority,
                StringComparison.OrdinalIgnoreCase) ||
            separator >= 0)
        {
            var mediaFiles = MediaStore.Files.GetContentUri("external");
            if (mediaFiles == null)
                return null;
            var fromMedia = QueryDataColumn(
                mediaFiles,
                "_id=?",
                new[] { id.ToString(System.Globalization.CultureInfo.InvariantCulture) });
            if (!string.IsNullOrWhiteSpace(fromMedia))
                return fromMedia;
            fromMedia = QueryRelativeMediaPath(mediaFiles, id);
            if (!string.IsNullOrWhiteSpace(fromMedia))
                return fromMedia;
        }

        foreach (var downloadsBase in new[]
                 {
                     "content://downloads/public_downloads",
                     "content://downloads/all_downloads"
                 })
        {
            var candidate = ContentUris.WithAppendedId(
                AndroidUri.Parse(downloadsBase)!,
                id);
            if (candidate == null)
                continue;
            var fromDownloads = QueryDataColumn(candidate);
            if (!string.IsNullOrWhiteSpace(fromDownloads))
                return fromDownloads;
        }

        return null;
    }

    private string? ResolveGrantedDescriptorPath(AndroidUri uri)
    {
        try
        {
            using var descriptor = _context.ContentResolver?.OpenFileDescriptor(uri, "r");
            if (descriptor == null)
                return null;
            var kernelPath = global::Android.Systems.Os.Readlink(
                "/proc/self/fd/" + descriptor.Fd);
            var path = NormalizeKernelStoragePath(kernelPath);
            if (!string.IsNullOrWhiteSpace(path))
                Log.Info("TeknoParrotDocument", "Resolved granted document descriptor");
            else
                Log.Debug("TeknoParrotDocument",
                    "Rejected granted document descriptor mount: " +
                    DescribeKernelMount(kernelPath));
            return path;
        }
        catch (Exception error)
        {
            Log.Debug("TeknoParrotDocument",
                "Granted descriptor did not expose a local path: " +
                error.GetType().Name);
            return null;
        }
    }

    private static string DescribeKernelMount(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "empty";
        var segments = value.Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
            return "root";
        var count = string.Equals(segments[0], "mnt",
            StringComparison.OrdinalIgnoreCase)
            ? Math.Min(5, segments.Length)
            : 1;
        return "/" + string.Join('/', segments[..count]);
    }

    internal static string? NormalizeKernelStoragePath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var path = value.Replace('\\', '/');

        // Scoped-storage providers expose the same emulated volume through
        // different per-process mount namespaces across Android vendors and
        // releases (for example /mnt/user/0/emulated/0 and
        // /mnt/pass_through/0/emulated/0). The selected descriptor is already
        // protected by an exact SAF grant, so canonicalize any emulated-primary
        // mount below /mnt to the public path consumed by RPCS3X6.
        if (path.StartsWith("/mnt/", StringComparison.OrdinalIgnoreCase))
        {
            const string emulatedPrimary = "/emulated/0";
            var emulatedIndex = path.IndexOf(
                emulatedPrimary,
                StringComparison.OrdinalIgnoreCase);
            if (emulatedIndex >= 0)
            {
                var suffixIndex = emulatedIndex + emulatedPrimary.Length;
                if (suffixIndex == path.Length || path[suffixIndex] == '/')
                    return "/storage/emulated/0" + path[suffixIndex..];
            }
        }

        foreach (var mapping in new[]
                 {
                     (Prefix: "/mnt/user/0/primary", Root: "/storage/emulated/0"),
                     (Prefix: "/mnt/runtime/default/emulated/0", Root: "/storage/emulated/0"),
                     (Prefix: "/mnt/runtime/read/emulated/0", Root: "/storage/emulated/0"),
                     (Prefix: "/mnt/runtime/write/emulated/0", Root: "/storage/emulated/0"),
                     (Prefix: "/mnt/runtime/full/emulated/0", Root: "/storage/emulated/0"),
                     (Prefix: "/mnt/media_rw", Root: "/storage")
                 })
        {
            if (path.Equals(mapping.Prefix, StringComparison.OrdinalIgnoreCase))
                return mapping.Root;
            if (path.StartsWith(mapping.Prefix + "/",
                    StringComparison.OrdinalIgnoreCase))
                return mapping.Root + path[mapping.Prefix.Length..];
        }

        return path.StartsWith("/storage/", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith("/sdcard/", StringComparison.OrdinalIgnoreCase)
            ? path
            : null;
    }

    private string? QueryRelativeMediaPath(AndroidUri uri, long id)
    {
        try
        {
            using var cursor = _context.ContentResolver?.Query(
                uri,
                new[] { "relative_path", "_display_name", "volume_name" },
                "_id=?",
                new[] { id.ToString(System.Globalization.CultureInfo.InvariantCulture) },
                null);
            if (cursor == null || !cursor.MoveToFirst())
                return null;
            var relativeIndex = cursor.GetColumnIndex("relative_path");
            var nameIndex = cursor.GetColumnIndex("_display_name");
            if (relativeIndex < 0 || nameIndex < 0 ||
                cursor.IsNull(relativeIndex) || cursor.IsNull(nameIndex))
                return null;

            var volumeIndex = cursor.GetColumnIndex("volume_name");
            var volume = volumeIndex >= 0 && !cursor.IsNull(volumeIndex)
                ? cursor.GetString(volumeIndex)
                : "external_primary";
            var root = string.Equals(
                volume,
                "external_primary",
                StringComparison.OrdinalIgnoreCase)
                ? "/storage/emulated/0"
                : "/storage/" + volume;
            return root.TrimEnd('/') + "/" +
                   cursor.GetString(relativeIndex)!.Trim('/') + "/" +
                   cursor.GetString(nameIndex);
        }
        catch
        {
            return null;
        }
    }

    private string? QueryDataColumn(
        AndroidUri uri,
        string? selection = null,
        string[]? selectionArguments = null)
    {
        try
        {
            using var cursor = _context.ContentResolver?.Query(
                uri,
                new[] { "_data" },
                selection,
                selectionArguments,
                null);
            if (cursor == null || !cursor.MoveToFirst())
                return null;
            var index = cursor.GetColumnIndex("_data");
            return index >= 0 && !cursor.IsNull(index)
                ? cursor.GetString(index)
                : null;
        }
        catch
        {
            return null;
        }
    }
}
