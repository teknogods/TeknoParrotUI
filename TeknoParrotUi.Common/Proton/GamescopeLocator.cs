using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace TeknoParrotUi.Common.Proton
{
    /// <summary>
    /// Locates and validates the Gamescope executable to use for a launch.
    ///
    /// Discovery order:
    ///   1. TP_GAMESCOPE_PATH environment variable (explicit - a missing or
    ///      invalid configured path is a hard failure, it never silently
    ///      falls through to auto-discovery)
    ///   2. PATH lookup
    ///   3. /usr/bin/gamescope
    ///   4. /usr/local/bin/gamescope
    ///
    /// Validation confirms the file exists, is executable, and that a short
    /// `gamescope --version` probe actually succeeds - never presents
    /// Gamescope as available when it can't actually start. The probe is
    /// cached per executable path, invalidated automatically when that
    /// file's last-write-time changes or it disappears (so re-installing/
    /// upgrading Gamescope, or switching TP_GAMESCOPE_PATH to a different
    /// file, is picked up without restarting TeknoParrotUI).
    /// </summary>
    public static class GamescopeLocator
    {
        public const string GamescopePathEnvVar = "TP_GAMESCOPE_PATH";

        private sealed class CacheEntry
        {
            public DateTime LastWriteTimeUtc;
            public GamescopeAvailability Availability;
        }

        private static readonly ConcurrentDictionary<string, CacheEntry> _cache = new();

        public static GamescopeAvailability Locate()
        {
            var configured = Environment.GetEnvironmentVariable(GamescopePathEnvVar);
            if (!string.IsNullOrEmpty(configured))
            {
                if (!File.Exists(configured))
                {
                    return GamescopeAvailability.Unavailable(GamescopeUnavailableReason.ConfiguredPathMissing,
                        $"{GamescopePathEnvVar} is set to '{configured}' but that file does not exist.", configured);
                }
                return Validate(configured);
            }

            foreach (var candidate in DiscoveryCandidates())
            {
                if (!string.IsNullOrEmpty(candidate) && File.Exists(candidate))
                    return Validate(candidate);
            }

            return GamescopeAvailability.Unavailable(GamescopeUnavailableReason.NotInstalled,
                "Gamescope was not found on PATH, /usr/bin, or /usr/local/bin. Install it, or set TP_GAMESCOPE_PATH to its location.");
        }

        internal static IEnumerable<string> DiscoveryCandidates()
        {
            var fromPath = FindOnPath("gamescope");
            if (fromPath != null)
                yield return fromPath;
            yield return "/usr/bin/gamescope";
            yield return "/usr/local/bin/gamescope";
        }

        internal static string FindOnPath(string exeName)
        {
            var pathVar = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(pathVar))
                return null;

            foreach (var dir in pathVar.Split(Path.PathSeparator))
            {
                try
                {
                    if (string.IsNullOrEmpty(dir))
                        continue;
                    var candidate = Path.Combine(dir, exeName);
                    if (File.Exists(candidate))
                        return candidate;
                }
                catch
                {
                    // malformed PATH entry - skip it
                }
            }
            return null;
        }

        private static GamescopeAvailability Validate(string path)
        {
            DateTime mtimeUtc;
            try
            {
                mtimeUtc = File.GetLastWriteTimeUtc(path);
            }
            catch
            {
                _cache.TryRemove(path, out _);
                return GamescopeAvailability.Unavailable(GamescopeUnavailableReason.ConfiguredPathMissing,
                    $"Gamescope executable disappeared: {path}", path);
            }

            if (_cache.TryGetValue(path, out var entry) && entry.LastWriteTimeUtc == mtimeUtc)
                return entry.Availability;

            var result = ValidateUncached(path);
            _cache[path] = new CacheEntry { LastWriteTimeUtc = mtimeUtc, Availability = result };
            return result;
        }

        /// <summary>Runs the actual `--version` probe - not cached itself (see <see cref="Validate"/> for the caching wrapper).</summary>
        internal static GamescopeAvailability ValidateUncached(string path)
        {
            if (!IsExecutable(path))
            {
                return GamescopeAvailability.Unavailable(GamescopeUnavailableReason.NotExecutable,
                    $"Gamescope at '{path}' is not executable.", path);
            }

            try
            {
                var psi = new ProcessStartInfo(path, "--version")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using var proc = Process.Start(psi);
                if (proc == null)
                {
                    return GamescopeAvailability.Unavailable(GamescopeUnavailableReason.VersionProbeFailed,
                        $"Could not start '{path} --version'.", path);
                }

                var stdout = proc.StandardOutput.ReadToEnd();
                var stderr = proc.StandardError.ReadToEnd();
                if (!proc.WaitForExit(5000))
                {
                    try { proc.Kill(); } catch { /* best effort */ }
                    return GamescopeAvailability.Unavailable(GamescopeUnavailableReason.VersionProbeTimedOut,
                        $"'{path} --version' timed out.", path);
                }

                var version = ParseVersion(stdout + "\n" + stderr);
                if (version == null)
                {
                    return GamescopeAvailability.Unavailable(GamescopeUnavailableReason.VersionProbeFailed,
                        $"'{path} --version' produced no readable version string.", path);
                }

                return GamescopeAvailability.Available(path, version);
            }
            catch (Exception ex)
            {
                return GamescopeAvailability.Unavailable(GamescopeUnavailableReason.VersionProbeFailed,
                    $"'{path} --version' failed: {ex.Message}", path);
            }
        }

        /// <summary>e.g. "[gamescope] [Info]  console: gamescope version 3.16.23.2+ (gcc 16.1.1)" -> "3.16.23.2+".</summary>
        internal static string ParseVersion(string probeOutput)
        {
            var text = probeOutput ?? string.Empty;
            var match = Regex.Match(text, @"gamescope version\s+(\S+)", RegexOptions.IgnoreCase);
            if (match.Success)
                return match.Groups[1].Value;

            match = Regex.Match(text, @"\b(\d+\.\d+(?:\.\d+){0,2}[\w.+-]*)\b");
            return match.Success ? match.Groups[1].Value : null;
        }

        internal static bool IsExecutable(string path)
        {
            if (!File.Exists(path))
                return false;
            if (!OperatingSystem.IsLinux())
                return true; // best-effort elsewhere - this feature only ever runs on Linux anyway

            try
            {
                var mode = File.GetUnixFileMode(path);
                return (mode & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) != 0;
            }
            catch
            {
                return true; // couldn't read the mode bits - don't block on that alone
            }
        }

        /// <summary>Test-only: clears the validation cache between scenarios that reuse the same path.</summary>
        internal static void ClearCacheForTests() => _cache.Clear();
    }
}
