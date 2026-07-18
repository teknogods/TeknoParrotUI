using System;
using System.IO;
using System.IO.Compression;

namespace TeknoParrotUi.Common.Updater
{
    /// <summary>
    /// Resolves an archive member beneath a trusted extraction root. Archive
    /// names are untrusted input: ZIP files can use either slash on every host
    /// and may contain absolute paths or parent traversal segments.
    /// </summary>
    public static class SafeArchivePath
    {
        public static string Resolve(string extractionRoot, string entryName)
        {
            if (string.IsNullOrWhiteSpace(extractionRoot))
                throw new ArgumentException("An extraction root is required.", nameof(extractionRoot));
            if (string.IsNullOrWhiteSpace(entryName))
                throw new InvalidDataException("Archive entry has an empty name.");

            var normalizedEntry = entryName
                .Replace('\\', Path.DirectorySeparatorChar)
                .Replace('/', Path.DirectorySeparatorChar);

            if (Path.IsPathRooted(normalizedEntry))
                throw new InvalidDataException($"Archive entry uses an absolute path: {entryName}");

            var fullRoot = Path.GetFullPath(extractionRoot);
            var destination = Path.GetFullPath(Path.Combine(fullRoot, normalizedEntry));
            var root = fullRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var comparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            var rootPrefix = root + Path.DirectorySeparatorChar;

            if (!destination.StartsWith(rootPrefix, comparison))
                throw new InvalidDataException($"Archive entry escapes the extraction directory: {entryName}");

            return destination;
        }

        internal static UnixFileMode? GetStoredUnixFileMode(int externalAttributes)
        {
            var mode = (externalAttributes >> 16) & 0x1FF;
            return mode == 0 ? null : (UnixFileMode)mode;
        }

        /// <summary>
        /// Restores the rwx permission bits stored by Unix ZIP tools. The .NET
        /// ZIP extractor otherwise applies the process umask and can silently
        /// remove the executable bit from Linux apphosts after an update.
        /// </summary>
        public static void RestoreUnixPermissions(ZipArchiveEntry entry, string path)
        {
            if (OperatingSystem.IsWindows())
                return;

            var mode = GetStoredUnixFileMode(entry.ExternalAttributes);
            if (mode.HasValue)
                File.SetUnixFileMode(path, mode.Value);
        }
    }
}
