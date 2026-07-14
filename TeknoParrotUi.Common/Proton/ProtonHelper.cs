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
        /// Builds the ProcessStartInfo for pipehelper.exe inside the given
        /// game's Wine prefix WITHOUT starting it - separated from
        /// <see cref="RunHelper"/> so token/ownership injection is directly
        /// unit-testable. Every helper explicitly carries:
        ///   TP_LAUNCH_SESSION_ID   - the CURRENT session token (never relies
        ///                            only on inheriting a synthetic game env),
        ///   TP_PIPEHELPER_OWNER_PID / TP_PIPEHELPER_OWNER_START - the exact
        ///                            TeknoParrotUI process identity, so crash
        ///                            recovery can verify orphanhood even after
        ///                            Wine reparents the helper.
        /// </summary>
        public static ProcessStartInfo BuildHelperStartInfo(ProtonGameInfo game, params string[] args)
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

            // Session/ownership identity - ALWAYS explicit, overriding anything
            // inherited (a synthetic Pid=-1 game env has no token at all).
            var token = ProtonRuntime.CurrentSessionToken;
            if (!string.IsNullOrEmpty(token))
                psi.Environment[PipeHelperRegistry.SessionTokenEnvVar] = token;
            psi.Environment[PipeHelperRegistry.OwnerPidEnvVar] = Environment.ProcessId.ToString();
            var ownStart = OperatingSystem.IsLinux()
                ? new LinuxProcReader().ReadStat(Environment.ProcessId)?.StartTimeTicks
                : null;
            if (ownStart.HasValue)
                psi.Environment[PipeHelperRegistry.OwnerStartEnvVar] = ownStart.Value.ToString();

            return psi;
        }

        /// <summary>
        /// Runs pipehelper.exe inside the given game's Wine prefix using the
        /// same wine binary AND environment the game itself runs under
        /// (Proton dists need their WINEDLLPATH/LD_LIBRARY_PATH or builtin
        /// DLLs fail to load). The started helper is registered in
        /// <see cref="PipeHelperRegistry"/> (PID + start time + session token +
        /// prefix) so cleanup only ever touches identity-verified helpers.
        /// </summary>
        public static Process RunHelper(ProtonGameInfo game, params string[] args)
        {
            var psi = BuildHelperStartInfo(game, args);

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

            // Register the helper's exact identity so cleanup/exit hooks only
            // ever terminate verified helpers this process started.
            if (OperatingSystem.IsLinux())
            {
                var stat = new LinuxProcReader().ReadStat(proc.Id);
                PipeHelperRegistry.Register(
                    proc.Id, stat?.StartTimeTicks,
                    psi.Environment.TryGetValue(PipeHelperRegistry.SessionTokenEnvVar, out var tok) ? tok : null,
                    psi.Environment.TryGetValue("WINEPREFIX", out var pfx) ? pfx : game.WinePrefix,
                    bridgeId: null);
            }
            return proc;
        }
    }
}
