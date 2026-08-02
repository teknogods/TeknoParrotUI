using System;
using System.Linq;

namespace TeknoParrotUi.Common.Android
{
    /// <summary>
    /// Maps only the shared Android directories intentionally exposed to the
    /// managed Winlator container. E: remains companion-private runtime
    /// storage and must never be used as an alias for all shared storage. A
    /// removable card is exposed only through its TeknoParrotGames folder on
    /// H:, never through the card root.
    /// </summary>
    public static class AndroidWinlatorGamePath
    {
        public const string SharedGamesRoot = "/storage/emulated/0/TeknoParrotGames";
        public const string SharedGamesRootAlias = "/sdcard/TeknoParrotGames";

        public static string ToDosPath(string source, string downloadsDirectory)
        {
            if (string.IsNullOrWhiteSpace(source))
                throw new InvalidOperationException("Set the game executable path first.");

            var value = source.Trim();
            if (LooksLikeDosPath(value))
                return ValidateDosGamePath(value);

            var normalized = value.Replace('\\', '/').TrimEnd('/');
            var roots = new[]
            {
                (Path: NormalizeRoot(downloadsDirectory), Drive: "D"),
                (Path: "/storage/emulated/0/Download", Drive: "D"),
                (Path: "/sdcard/Download", Drive: "D"),
                (Path: SharedGamesRoot, Drive: "G"),
                (Path: SharedGamesRootAlias, Drive: "G")
            };
            foreach (var root in roots
                         .Where(root => !string.IsNullOrEmpty(root.Path))
                         .Distinct())
            {
                if (!normalized.StartsWith(root.Path + "/", StringComparison.Ordinal))
                    continue;

                var relative = normalized[(root.Path.Length + 1)..];
                var segments = ValidateSegments(relative, "The selected Android game path");
                return root.Drive + @":\" + string.Join("\\", segments);
            }

            if (TryMapRemovableGamesPath(normalized, out var removablePath))
                return removablePath;

            throw new InvalidOperationException(
                "Choose a game inside Android Downloads or " +
                SharedGamesRoot +
                ", or inside a TeknoParrotGames folder on a removable SD card.");
        }

        public static bool IsAllowedSharedPath(string source, string downloadsDirectory)
        {
            try
            {
                _ = ToDosPath(source, downloadsDirectory);
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        private static bool LooksLikeDosPath(string value) =>
            value.Length >= 4 && value[1] == ':' && value[2] == '\\';

        private static string ValidateDosGamePath(string value)
        {
            var drive = char.ToUpperInvariant(value[0]);
            if (drive is not ('D' or 'G' or 'H'))
                throw new InvalidOperationException(
                    "Android game files must use the shared D:, G:, or H: drive.");
            if (value.Contains('/'))
                throw new InvalidOperationException(
                    "The selected Winlator game path is not canonical.");

            var segments = ValidateSegments(value[3..], "The selected Winlator game path");
            return drive + @":\" + string.Join("\\", segments);
        }

        private static bool TryMapRemovableGamesPath(
            string normalized,
            out string dosPath)
        {
            dosPath = string.Empty;
            const string storagePrefix = "/storage/";
            if (!normalized.StartsWith(storagePrefix, StringComparison.Ordinal))
                return false;

            var volumeEnd = normalized.IndexOf('/', storagePrefix.Length);
            if (volumeEnd < 0)
                return false;
            var volume = normalized[storagePrefix.Length..volumeEnd];
            if (!IsSafeRemovableVolume(volume))
                return false;

            var gamesRoot = storagePrefix + volume + "/TeknoParrotGames";
            if (!normalized.StartsWith(gamesRoot + "/", StringComparison.Ordinal))
                return false;

            var relative = normalized[(gamesRoot.Length + 1)..];
            var segments = ValidateSegments(relative, "The selected SD-card game path");
            dosPath = @"H:\" + string.Join("\\", segments);
            return true;
        }

        private static bool IsSafeRemovableVolume(string volume) =>
            volume.Length > 0 &&
            !volume.Equals("emulated", StringComparison.OrdinalIgnoreCase) &&
            !volume.Equals("self", StringComparison.OrdinalIgnoreCase) &&
            volume.All(character =>
                char.IsLetterOrDigit(character) || character is '-' or '_');

        private static string[] ValidateSegments(string relative, string description)
        {
            var segments = relative.Split('/');
            if (!relative.Contains('/'))
                segments = relative.Split('\\');
            if (segments.Any(segment =>
                    string.IsNullOrEmpty(segment) || segment is "." or ".." ||
                    segment.Contains(':') || segment.Contains('"') ||
                    segment.Any(character => character < 0x20)))
                throw new InvalidOperationException(description + " is not canonical.");
            return segments;
        }

        private static string NormalizeRoot(string root) =>
            string.IsNullOrWhiteSpace(root)
                ? string.Empty
                : root.Replace('\\', '/').TrimEnd('/');
    }
}
