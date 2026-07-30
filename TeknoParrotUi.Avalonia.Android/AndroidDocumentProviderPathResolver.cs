using System;
using Android.Content;
using Android.Provider;
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
