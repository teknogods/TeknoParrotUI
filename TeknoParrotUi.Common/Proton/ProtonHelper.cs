using System;
using System.Diagnostics;
using System.IO;

namespace TeknoParrotUi.Common.Proton
{
    /// <summary>
    /// Locates and launches pipehelper.exe inside a game's Wine/Proton prefix.
    /// Shared by ProtonBridgePipe (pipe+shm mode) and ProtonSharedMemoryMirror
    /// (shm-only mode).
    /// </summary>
    public static class ProtonHelper
    {
        /// <summary>
        /// Converts a host path to the Wine view of it (Z: drive).
        /// /dev/shm/X -> Z:\dev\shm\X
        /// </summary>
        public static string ToWinePath(string hostPath) =>
            "Z:" + hostPath.Replace('/', '\\');

        /// <summary>
        /// Finds pipehelper.exe: TP_PROTON_PIPEHELPER env var, application
        /// directory, then the TeknoParrot Proton package directory.
        /// </summary>
        public static string ResolveHelperPath()
        {
            var candidates = new[]
            {
                Environment.GetEnvironmentVariable("TP_PROTON_PIPEHELPER"),
                Path.Combine(AppContext.BaseDirectory, "pipehelper.exe"),
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "TeknoParrotUI", "proton", "pipehelper.exe")
            };

            foreach (var candidate in candidates)
            {
                if (!string.IsNullOrEmpty(candidate) && File.Exists(candidate))
                    return candidate;
            }

            return null;
        }

        /// <summary>
        /// Kills any pipehelper processes left over from previous sessions.
        /// A stale helper still owns \\.\pipe\... inside the prefix (single
        /// instance), which makes the next session's pipe creation fail and
        /// the game boot with I/O errors.
        /// </summary>
        public static void KillStaleHelpers()
        {
            if (!System.OperatingSystem.IsLinux() || !Directory.Exists("/proc"))
                return;

            foreach (var procDir in Directory.EnumerateDirectories("/proc"))
            {
                if (!int.TryParse(Path.GetFileName(procDir), out var pid))
                    continue;
                try
                {
                    var comm = File.ReadAllText(Path.Combine(procDir, "comm")).Trim();
                    // wine reports the exe name as comm (truncated to 15 chars)
                    if (comm.StartsWith("pipehelper", StringComparison.OrdinalIgnoreCase))
                    {
                        ProtonLog.Write($"killing stale pipehelper (pid {pid})");
                        Process.GetProcessById(pid).Kill();
                    }
                }
                catch
                {
                    // exited mid-scan / not ours
                }
            }
        }

        /// <summary>
        /// Runs pipehelper.exe inside the given game's Wine prefix using the
        /// same wine binary AND environment the game itself runs under
        /// (Proton dists need their WINEDLLPATH/LD_LIBRARY_PATH or builtin
        /// DLLs fail to load).
        /// </summary>
        public static Process RunHelper(ProtonGameInfo game, params string[] args)
        {
            var helperPath = ResolveHelperPath();
            if (helperPath == null)
                throw new FileNotFoundException(
                    "pipehelper.exe not found. Install the TeknoParrot Proton package or set TP_PROTON_PIPEHELPER.");

            var winePath = game?.WineBinaryPath;
            if (winePath == null || !File.Exists(winePath))
                throw new FileNotFoundException(
                    $"Wine binary for the game process could not be resolved (pid {game?.Pid}).");

            var psi = new ProcessStartInfo
            {
                FileName = winePath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            psi.ArgumentList.Add(helperPath);
            foreach (var arg in args)
                psi.ArgumentList.Add(arg);

            // Inherit the game's full environment so the helper's wine finds
            // the same builtin DLL paths (critical for Proton dists).
            var gameEnv = ProtonProcessDetector.ReadEnvironment(game.Pid);
            foreach (var kv in gameEnv)
                psi.Environment[kv.Key] = kv.Value;

            if (!psi.Environment.ContainsKey("WINEPREFIX"))
                psi.Environment["WINEPREFIX"] = game.WinePrefix;
            if (!psi.Environment.ContainsKey("WINEDEBUG"))
                psi.Environment["WINEDEBUG"] = "-all";

            var proc = Process.Start(psi);
            // Surface pipehelper diagnostics (it logs to stderr) and prevent
            // the redirected pipes from filling up.
            proc.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data) && e.Data.Contains("pipehelper"))
                    ProtonLog.Write($"helper: {e.Data}");
            };
            proc.OutputDataReceived += (_, _) => { };
            proc.BeginErrorReadLine();
            proc.BeginOutputReadLine();
            return proc;
        }
    }
}
