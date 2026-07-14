using System;
using System.Collections.Generic;
using System.Text;

namespace TeknoParrotUi.Common.GameLaunch
{
    /// <summary>
    /// Keeps the FULL log of the most recent game launch (everything the
    /// launch window shows: console output lines, state changes, launch
    /// decisions like [WinePrefix]/[FullscreenScaling]/[GamescopeCompatibility]/
    /// [GameSessionLifecycle]/[PipeSession] blocks, and the exit code) so the
    /// Troubleshooting page can attach a complete, timestamped record of the
    /// last run to a report - the exact information needed when a game does
    /// not boot. Bounded ring buffer; never grows without limit.
    /// </summary>
    public static class GameSessionLogArchive
    {
        private const int MaxLines = 8000;

        private static readonly object Sync = new object();
        private static readonly List<string> Lines = new List<string>(1024);
        private static bool _truncated;

        /// <summary>Profile name of the recorded run (null when no run happened yet).</summary>
        public static string LastRunProfileName { get; private set; }

        /// <summary>Game path of the recorded run.</summary>
        public static string LastRunGamePath { get; private set; }

        public static DateTime? LastRunStartedAt { get; private set; }
        public static DateTime? LastRunEndedAt { get; private set; }
        public static int? LastRunExitCode { get; private set; }

        public static bool HasRun
        {
            get { lock (Sync) return LastRunStartedAt != null; }
        }

        /// <summary>Starts recording a new run - clears the previous run's log.</summary>
        public static void BeginRun(GameProfile profile)
        {
            lock (Sync)
            {
                Lines.Clear();
                _truncated = false;
                LastRunProfileName = profile?.GameNameInternal ?? profile?.ProfileName ?? "(unknown)";
                LastRunGamePath = profile?.GamePath;
                LastRunStartedAt = DateTime.Now;
                LastRunEndedAt = null;
                LastRunExitCode = null;
                AppendLocked($"=== Run started: {LastRunProfileName} ===");
            }
        }

        /// <summary>Appends one log line (timestamped) to the current run's record.</summary>
        public static void Append(string line)
        {
            if (line == null)
                return;
            lock (Sync)
            {
                AppendLocked(line);
            }
        }

        /// <summary>Records the end of the run and its exit code.</summary>
        public static void EndRun(int exitCode)
        {
            lock (Sync)
            {
                LastRunEndedAt = DateTime.Now;
                LastRunExitCode = exitCode;
                AppendLocked($"=== Run ended (exit code {exitCode}) ===");
            }
        }

        private static void AppendLocked(string line)
        {
            if (Lines.Count >= MaxLines)
            {
                // Drop the OLDEST quarter once full - the end of the log (the
                // failure) is what matters for a bug report.
                Lines.RemoveRange(0, MaxLines / 4);
                _truncated = true;
            }
            Lines.Add($"[{DateTime.Now:HH:mm:ss.fff}] {line}");
        }

        /// <summary>The complete recorded log of the last run as one string.</summary>
        public static string GetLastRunLog()
        {
            lock (Sync)
            {
                if (LastRunStartedAt == null)
                    return "No game has been launched in this session yet.";

                var sb = new StringBuilder();
                sb.AppendLine($"Game: {LastRunProfileName}");
                if (!string.IsNullOrEmpty(LastRunGamePath))
                    sb.AppendLine($"Game Path: {LastRunGamePath}");
                sb.AppendLine($"Started: {LastRunStartedAt:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine(LastRunEndedAt != null
                    ? $"Ended: {LastRunEndedAt:yyyy-MM-dd HH:mm:ss} (exit code {LastRunExitCode})"
                    : "Ended: still running (or not cleanly ended)");
                if (_truncated)
                    sb.AppendLine("(log was truncated - oldest lines dropped)");
                sb.AppendLine();
                foreach (var line in Lines)
                    sb.AppendLine(line);
                return sb.ToString();
            }
        }
    }
}
