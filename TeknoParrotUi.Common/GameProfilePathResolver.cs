using System;
using System.IO;

namespace TeknoParrotUi.Common
{
    /// <summary>
    /// Resolves command-line and online-launch profile names without allowing
    /// rooted paths or traversal outside the selected profile directory.
    /// </summary>
    public static class GameProfilePathResolver
    {
        public static bool TryResolveExisting(
            string profilesDirectory,
            string profileFileName,
            out string profilePath)
        {
            profilePath = string.Empty;
            if (string.IsNullOrWhiteSpace(profilesDirectory) ||
                string.IsNullOrWhiteSpace(profileFileName) ||
                profileFileName.Length > 160 ||
                !profileFileName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(
                    Path.GetFileName(profileFileName),
                    profileFileName,
                    StringComparison.Ordinal) ||
                profileFileName.Contains('/') ||
                profileFileName.Contains('\\') ||
                Path.IsPathRooted(profileFileName))
                return false;

            try
            {
                var root = Path.GetFullPath(profilesDirectory)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var candidate = Path.GetFullPath(Path.Combine(root, profileFileName));
                var comparison = OperatingSystem.IsWindows()
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal;
                if (!candidate.StartsWith(root + Path.DirectorySeparatorChar, comparison) ||
                    !File.Exists(candidate))
                    return false;

                profilePath = candidate;
                return true;
            }
            catch (Exception error) when (
                error is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return false;
            }
        }
    }
}
