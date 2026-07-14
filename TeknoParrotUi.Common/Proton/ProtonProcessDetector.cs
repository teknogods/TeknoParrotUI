using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TeknoParrotUi.Common.Proton
{
    /// <summary>
    /// Information about a game process running inside Proton/Wine.
    /// </summary>
    public class ProtonGameInfo
    {
        public int Pid { get; set; }

        /// <summary>Process name as reported by /proc/pid/comm (truncated to 15 chars).</summary>
        public string ExecutableName { get; set; }

        /// <summary>WINEPREFIX the game is running in.</summary>
        public string WinePrefix { get; set; }

        /// <summary>Path to the wine binary running the game (from /proc/pid/exe).</summary>
        public string WineBinaryPath { get; set; }
    }

    /// <summary>
    /// Detects game processes running inside Proton/Wine by scanning /proc.
    /// Linux-only; returns null on other platforms or when nothing is found.
    /// </summary>
    public static class ProtonProcessDetector
    {
        // Wine infrastructure processes that are never the game itself.
        private static readonly string[] InfrastructureProcesses =
        {
            "wineserver", "services.exe", "winedevice.exe", "plugplay.exe",
            "svchost.exe", "explorer.exe", "rpcss.exe", "tabtip.exe",
            "conhost.exe", "start.exe", "steam.exe", "pipehelper.exe"
        };

        /// <summary>
        /// True when a process name is known Wine infrastructure - never the
        /// game itself. Shared with the session-scoped lifecycle
        /// (<see cref="ProcSessionProcessLocator"/>/<see cref="GameProcessClassifier"/>)
        /// so both use the exact same list.
        /// </summary>
        public static bool IsInfrastructureProcessName(string processName)
        {
            if (string.IsNullOrEmpty(processName))
                return false;
            return InfrastructureProcesses.Any(p => processName.Equals(p, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Finds a running Proton/Wine game process.
        /// </summary>
        /// <param name="executableName">
        /// Optional executable to look for (e.g. "Rally.exe"). When null, the first
        /// non-infrastructure .exe process with a WINEPREFIX is returned.
        /// </param>
        public static ProtonGameInfo FindRunningProtonGame(string executableName = null)
        {
            if (!Directory.Exists("/proc"))
                return null;

            foreach (var procDir in Directory.EnumerateDirectories("/proc"))
            {
                if (!int.TryParse(Path.GetFileName(procDir), out var pid))
                    continue;

                var info = DescribeIfGameProcess(pid, executableName);
                if (info != null)
                    return info;
            }

            return null;
        }

        /// <summary>
        /// Finds a running Proton/Wine game process among a SPECIFIC set of
        /// candidate PIDs (e.g. the descendants of a known Gamescope wrapper
        /// process) rather than scanning the whole system - this is the
        /// session-scoped equivalent of <see cref="FindRunningProtonGame"/>,
        /// used so a coincidentally-named game process belonging to a
        /// DIFFERENT, unrelated launch is never mistaken for this session's
        /// game. Shares the exact same per-process matching rules via
        /// <see cref="DescribeIfGameProcess"/>.
        /// </summary>
        public static ProtonGameInfo FindGameAmongProcessIds(System.Collections.Generic.IEnumerable<int> candidatePids, string executableName = null)
        {
            if (candidatePids == null)
                return null;
            foreach (var pid in candidatePids)
            {
                var info = DescribeIfGameProcess(pid, executableName);
                if (info != null)
                    return info;
            }
            return null;
        }

        /// <summary>
        /// Checks whether a SPECIFIC pid looks like a Proton/Wine game
        /// process (has a WINEPREFIX, matches the expected executable name
        /// or the generic "non-infrastructure .exe" heuristic) - extracted
        /// so both the system-wide scan and the process-tree-scoped scan
        /// share one implementation and can never disagree.
        /// </summary>
        public static ProtonGameInfo DescribeIfGameProcess(int pid, string executableName = null)
        {
            var procDir = $"/proc/{pid}";
            if (!Directory.Exists(procDir))
                return null;

            try
            {
                var prefix = ReadEnvironVariable(procDir, "WINEPREFIX");
                if (string.IsNullOrEmpty(prefix))
                    return null;

                var comm = File.ReadAllText(Path.Combine(procDir, "comm")).Trim();

                if (executableName != null)
                {
                    if (!MatchesExecutable(comm, executableName))
                        return null;
                }
                else
                {
                    if (InfrastructureProcesses.Any(p => comm.Equals(p, StringComparison.OrdinalIgnoreCase)))
                        return null;
                    if (!comm.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        return null;
                }

                return new ProtonGameInfo
                {
                    Pid = pid,
                    ExecutableName = comm,
                    WinePrefix = prefix,
                    WineBinaryPath = ResolveWineBinary(procDir)
                };
            }
            catch
            {
                // Process exited mid-scan or belongs to another user - skip.
                return null;
            }
        }

        /// <summary>
        /// Reads the WINEPREFIX of a specific process, or null.
        /// </summary>
        public static string GetWinePrefix(int pid)
        {
            try
            {
                return ReadEnvironVariable($"/proc/{pid}", "WINEPREFIX");
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Full environment of a process (used to launch helpers with the same
        /// Wine/Proton library paths as the game). Empty on failure.
        /// </summary>
        public static Dictionary<string, string> ReadEnvironment(int pid)
        {
            var result = new Dictionary<string, string>();
            try
            {
                var environ = File.ReadAllText($"/proc/{pid}/environ");
                foreach (var entry in environ.Split('\0'))
                {
                    var idx = entry.IndexOf('=');
                    if (idx > 0)
                        result[entry.Substring(0, idx)] = entry.Substring(idx + 1);
                }
            }
            catch
            {
                // process gone or unreadable
            }
            return result;
        }

        private static bool MatchesExecutable(string comm, string executableName)
        {
            // /proc/pid/comm is truncated to 15 characters.
            var truncated = executableName.Length > 15 ? executableName.Substring(0, 15) : executableName;
            if (comm.Equals(truncated, StringComparison.OrdinalIgnoreCase))
                return true;

            var noExt = Path.GetFileNameWithoutExtension(executableName);
            var truncatedNoExt = noExt.Length > 15 ? noExt.Substring(0, 15) : noExt;
            return comm.Equals(truncatedNoExt, StringComparison.OrdinalIgnoreCase);
        }

        private static string ReadEnvironVariable(string procDir, string variable)
        {
            var environ = File.ReadAllText(Path.Combine(procDir, "environ"));
            return environ.Split('\0')
                .FirstOrDefault(v => v.StartsWith(variable + "=", StringComparison.Ordinal))
                ?.Substring(variable.Length + 1);
        }

        private static string ResolveWineBinary(string procDir)
        {
            try
            {
                // /proc/pid/exe points at wine-preloader or wine64 in the Proton dist.
                var target = new FileInfo(Path.Combine(procDir, "exe")).LinkTarget;
                if (string.IsNullOrEmpty(target))
                    return null;

                var dir = Path.GetDirectoryName(target);
                if (dir == null)
                    return null;

                // Prefer plain "wine" in the same bin directory.
                foreach (var candidate in new[] { "wine", "wine64" })
                {
                    var path = Path.Combine(dir, candidate);
                    if (File.Exists(path))
                        return path;
                }

                return target;
            }
            catch
            {
                return null;
            }
        }
    }
}
