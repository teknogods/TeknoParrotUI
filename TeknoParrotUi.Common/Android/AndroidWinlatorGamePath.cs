using System;
using System.Linq;

namespace TeknoParrotUi.Common.Android
{
    public sealed record AndroidWinlatorGameLocation(
        string DosPath,
        string ScopedGameDirectory)
    {
        public bool UsesScopedGameDirectory =>
            !string.IsNullOrWhiteSpace(ScopedGameDirectory);
    }

    /// <summary>
    /// Maps shared Android game paths into the managed Winlator container.
    /// Known library roots retain their fixed D/G/H mappings; an executable
    /// elsewhere on shared storage receives a launch-lifetime I: mapping for
    /// only its containing folder. E: remains companion-private runtime
    /// storage and no Android storage root is exposed to Wine.
    /// </summary>
    public static class AndroidWinlatorGamePath
    {
        public const string SharedGamesRoot = "/storage/emulated/0/TeknoParrotGames";
        public const string SharedGamesRootAlias = "/sdcard/TeknoParrotGames";
        public const string ScopedGameDrive = "I";

        public static string ToDosPath(string source, string downloadsDirectory)
            => Resolve(source, downloadsDirectory).DosPath;

        public static AndroidWinlatorGameLocation Resolve(
            string source,
            string downloadsDirectory)
        {
            if (string.IsNullOrWhiteSpace(source))
                throw new InvalidOperationException("Set the game executable path first.");

            var value = source.Trim();
            if (LooksLikeDosPath(value))
                return new AndroidWinlatorGameLocation(
                    ValidateDosGamePath(value),
                    string.Empty);

            var normalized = NormalizeAndroidPath(value);
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
                return new AndroidWinlatorGameLocation(
                    root.Drive + @":\" + string.Join("\\", segments),
                    string.Empty);
            }

            if (TryMapRemovableGamesPath(normalized, out var removablePath))
                return new AndroidWinlatorGameLocation(
                    removablePath,
                    string.Empty);

            if (TryMapScopedGamePath(normalized, out var scopedLocation))
                return scopedLocation;

            throw new InvalidOperationException(
                "Choose a game executable inside a local shared-storage folder. " +
                "TeknoParrot exposes only that executable's containing folder to Winlator.");
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

        private static bool TryMapScopedGamePath(
            string normalized,
            out AndroidWinlatorGameLocation location)
        {
            location = new AndroidWinlatorGameLocation(string.Empty, string.Empty);
            const string primaryRoot = "/storage/emulated/0";
            string volumeRoot;
            if (normalized.StartsWith(primaryRoot + "/", StringComparison.Ordinal))
            {
                volumeRoot = primaryRoot;
                if (normalized.StartsWith(
                        primaryRoot + "/Android/data/",
                        StringComparison.OrdinalIgnoreCase) ||
                    normalized.StartsWith(
                        primaryRoot + "/Android/obb/",
                        StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            else
            {
                const string storagePrefix = "/storage/";
                if (!normalized.StartsWith(storagePrefix, StringComparison.Ordinal))
                    return false;
                var volumeEnd = normalized.IndexOf('/', storagePrefix.Length);
                if (volumeEnd < 0)
                    return false;
                var volume = normalized[storagePrefix.Length..volumeEnd];
                if (!IsSafeRemovableVolume(volume))
                    return false;
                volumeRoot = normalized[..volumeEnd];
            }

            var separator = normalized.LastIndexOf('/');
            if (separator <= volumeRoot.Length)
                return false;
            var directory = normalized[..separator];
            var relativeDirectory = directory[(volumeRoot.Length + 1)..];
            if (relativeDirectory.Equals("Android", StringComparison.OrdinalIgnoreCase) ||
                relativeDirectory.Equals("Android/data", StringComparison.OrdinalIgnoreCase) ||
                relativeDirectory.StartsWith("Android/data/", StringComparison.OrdinalIgnoreCase) ||
                relativeDirectory.Equals("Android/obb", StringComparison.OrdinalIgnoreCase) ||
                relativeDirectory.StartsWith("Android/obb/", StringComparison.OrdinalIgnoreCase))
                return false;
            var fileName = normalized[(separator + 1)..];
            _ = ValidateSegments(
                relativeDirectory,
                "The selected Android game folder");
            var fileSegments = ValidateSegments(
                fileName,
                "The selected Android game executable");
            location = new AndroidWinlatorGameLocation(
                ScopedGameDrive + @":\" + fileSegments[0],
                directory);
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
                : NormalizeAndroidPath(root);

        private static string NormalizeAndroidPath(string value)
        {
            var normalized = value.Replace('\\', '/').TrimEnd('/');
            if (normalized.Equals("/sdcard", StringComparison.OrdinalIgnoreCase))
                return "/storage/emulated/0";
            const string sdcardPrefix = "/sdcard/";
            return normalized.StartsWith(sdcardPrefix, StringComparison.OrdinalIgnoreCase)
                ? "/storage/emulated/0/" + normalized[sdcardPrefix.Length..]
                : normalized;
        }
    }
}
