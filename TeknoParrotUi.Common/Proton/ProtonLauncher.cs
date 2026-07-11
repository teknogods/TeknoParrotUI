using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace TeknoParrotUi.Common.Proton
{
    /// <summary>
    /// Wraps a game's ProcessStartInfo so it runs under Wine/Proton on Linux.
    /// Windows builds never hit this code path.
    ///
    /// Wine binary resolution order:
    ///   1. TP_WINE environment variable
    ///   2. Newest Proton in the TeknoParrot Proton package directory
    ///      (~/.local/share/TeknoParrotUI/proton/&lt;version&gt;/bin/wine)
    ///   3. System wine (/usr/bin/wine)
    ///
    /// Each game gets its own prefix at
    /// ~/.local/share/TeknoParrotUI/prefixes/&lt;profile&gt; unless TP_WINEPREFIX
    /// is set.
    /// </summary>
    public static class ProtonLauncher
    {
        public static bool IsLinux => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

        /// <summary>
        /// True when games should be launched through Wine/Proton
        /// (Linux and a wine binary is available).
        /// </summary>
        public static bool ShouldUseProton => IsLinux && ResolveWineBinary() != null;

        /// <summary>
        /// Rewrites <paramref name="info"/> to run under Wine/Proton and marks
        /// the Proton session active so pipe/COM factories create bridges.
        ///
        /// Proton dists (GE-Proton etc.) are launched through their own
        /// "proton" script - running files/bin/wine directly breaks the DLL
        /// search paths (d3d9/vkd3d imports fail and games render nothing).
        /// Plain wine builds are invoked directly.
        /// </summary>
        /// <param name="info">Original start info (loader exe + arguments).</param>
        /// <param name="profile">Game profile, used for the per-game prefix and process detection.</param>
        public static ProcessStartInfo WrapWithProton(ProcessStartInfo info, GameProfile profile)
        {
            var wine = ResolveWineBinary(profile?.ProtonVersion)
                ?? throw new InvalidOperationException(
                    "No wine binary found. Install the TeknoParrot Proton package or system wine.");

            var workingDirectory = string.IsNullOrEmpty(info.WorkingDirectory)
                ? Environment.CurrentDirectory
                : info.WorkingDirectory;

            // Loader exe: absolute host path (wine opens unix paths fine).
            var loaderExe = Path.GetFullPath(info.FileName, workingDirectory);

            // Arguments: 1) the loader DLL is passed as a relative path
            // ("./OpenParrotWin32/OpenParrot") which the loader can't resolve
            // inside Wine ("Failed to load dll error 3") - make it an absolute
            // Wine path. 2) the game path is a unix path - convert to Z: form.
            var arguments = info.Arguments ?? string.Empty;
            arguments = MakeLoaderDllAbsolute(arguments, workingDirectory);
            if (!string.IsNullOrEmpty(profile?.GamePath))
                arguments = arguments.Replace(profile.GamePath, ProtonHelper.ToWinePath(profile.GamePath));
            if (!string.IsNullOrEmpty(profile?.GamePath2))
                arguments = arguments.Replace(profile.GamePath2, ProtonHelper.ToWinePath(profile.GamePath2));

            var protonScript = FindProtonScript(wine);
            ProcessStartInfo psi;
            if (protonScript != null)
            {
                // Proton dist: python3 <dist>/proton run <loader> <args>
                var compatData = ResolveCompatDataPath(profile);
                psi = new ProcessStartInfo
                {
                    FileName = "python3",
                    Arguments = $"\"{protonScript}\" run \"{loaderExe}\" {arguments}",
                    WorkingDirectory = workingDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = info.CreateNoWindow,
                    RedirectStandardOutput = info.RedirectStandardOutput,
                    RedirectStandardError = info.RedirectStandardError
                };
                foreach (var key in info.Environment.Keys)
                    psi.Environment[key] = info.Environment[key];

                psi.Environment["STEAM_COMPAT_DATA_PATH"] = compatData;
                psi.Environment["STEAM_COMPAT_CLIENT_INSTALL_PATH"] = ResolveSteamClientPath();
            }
            else
            {
                // Plain wine build.
                psi = new ProcessStartInfo
                {
                    FileName = wine,
                    Arguments = $"\"{loaderExe}\" {arguments}",
                    WorkingDirectory = workingDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = info.CreateNoWindow,
                    RedirectStandardOutput = info.RedirectStandardOutput,
                    RedirectStandardError = info.RedirectStandardError
                };
                foreach (var key in info.Environment.Keys)
                    psi.Environment[key] = info.Environment[key];

                psi.Environment["WINEPREFIX"] = ResolvePrefix(profile);
                if (!psi.Environment.ContainsKey("WINEDEBUG"))
                    psi.Environment["WINEDEBUG"] = "-all";
            }

            // Always use the loader's remote-thread injection under Wine: the
            // default thread-context hijack (Get/SetThreadContext run-to-address)
            // corrupts MXCSR in wow64 threads, unmasking SSE exceptions - games
            // then die with STATUS_FLOAT_MULTIPLE_TRAPS (0xc00002b5) on the first
            // inexact float op in DllMain (e.g. OpenParrot's TGM3 aspect-ratio
            // division). Remote-thread injection never touches the main thread
            // context. Verified working for both SR3 and TGM3.
            if (!psi.Environment.ContainsKey("TP_REMOTETHREAD"))
                psi.Environment["TP_REMOTETHREAD"] = "1";

            // Fullscreen scaling: low-res arcade games (640x480) end up in the
            // top-left corner of a 4K screen because Wine can't change the
            // display mode. gamescope (nested compositor) scales the game to
            // the full display. Disable with TP_NO_GAMESCOPE=1.
            WrapWithGamescope(psi);

            ProtonRuntime.Enabled = true;
            ProtonRuntime.ExpectedExecutable = Path.GetFileName(profile.GamePath);

            return psi;
        }

        private static void WrapWithGamescope(ProcessStartInfo psi)
        {
            // Opt-in only (TP_GAMESCOPE=1): gamescope fixes low-res fullscreen
            // scaling on 4K displays but is environment-sensitive (headless
            // backend/X errors outside a clean session), so default is off.
            if (Environment.GetEnvironmentVariable("TP_GAMESCOPE") != "1")
                return;

            string gamescope = null;
            foreach (var candidate in new[] { "/usr/bin/gamescope", "/usr/local/bin/gamescope" })
            {
                if (File.Exists(candidate))
                {
                    gamescope = candidate;
                    break;
                }
            }
            if (gamescope == null)
                return;

            // Without explicit output dimensions nested gamescope opens a
            // 1280x720 window and -f fails ("couldn't change to fullscreen").
            var (width, height) = GetDisplayResolution();
            var size = width > 0 ? $"-W {width} -H {height} " : string.Empty;

            // -f: fullscreen output; gamescope auto-scales the game's
            // resolution to the display.
            psi.Arguments = $"{size}-f -- \"{psi.FileName}\" {psi.Arguments}";
            psi.FileName = gamescope;
        }

        private static (int width, int height) _cachedResolution = (-1, -1);

        /// <summary>
        /// Current display resolution via xrandr (works on X11 and XWayland).
        /// Returns (0,0) when it cannot be determined.
        /// </summary>
        private static (int width, int height) GetDisplayResolution()
        {
            if (_cachedResolution.width >= 0)
                return _cachedResolution;

            try
            {
                var psi = new ProcessStartInfo("xrandr", "--current")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using var proc = Process.Start(psi);
                var output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit(3000);

                // "Screen 0: minimum 320 x 200, current 3840 x 2160, maximum ..."
                var match = System.Text.RegularExpressions.Regex.Match(output, @"current (\d+) x (\d+)");
                if (match.Success)
                {
                    _cachedResolution = (int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value));
                    return _cachedResolution;
                }
            }
            catch
            {
                // xrandr missing or no display
            }

            _cachedResolution = (0, 0);
            return _cachedResolution;
        }

        /// <summary>
        /// Rewrites a leading relative loader-DLL token ("./x/y" or ".\x\y")
        /// to an absolute Wine (Z:) path.
        /// </summary>
        private static string MakeLoaderDllAbsolute(string arguments, string workingDirectory)
        {
            var trimmed = arguments.TrimStart();
            if (!trimmed.StartsWith("./") && !trimmed.StartsWith(".\\"))
                return arguments;

            var spaceIdx = trimmed.IndexOf(' ');
            var token = spaceIdx < 0 ? trimmed : trimmed.Substring(0, spaceIdx);
            var rest = spaceIdx < 0 ? string.Empty : trimmed.Substring(spaceIdx);

            var absolute = Path.GetFullPath(token.Replace('\\', '/'), workingDirectory);
            return $"\"{ProtonHelper.ToWinePath(absolute)}\"{rest}";
        }

        /// <summary>
        /// Locates the "proton" launcher script for a dist wine binary
        /// (…/files/bin/wine → …/proton), or null for plain wine builds.
        /// </summary>
        public static string FindProtonScript(string wineBinary)
        {
            var binDir = Path.GetDirectoryName(wineBinary);          // files/bin
            var filesDir = Path.GetDirectoryName(binDir);            // files
            var distRoot = Path.GetDirectoryName(filesDir);          // dist root
            if (distRoot == null || !string.Equals(Path.GetFileName(filesDir), "files", StringComparison.Ordinal))
                return null;

            var script = Path.Combine(distRoot, "proton");
            return File.Exists(script) ? script : null;
        }

        /// <summary>
        /// Compat-data directory for proton runs; the real WINEPREFIX is
        /// created by proton at &lt;compat&gt;/pfx.
        /// </summary>
        private static string ResolveCompatDataPath(GameProfile profile)
        {
            var profileName = !string.IsNullOrEmpty(profile.ProfileName)
                ? profile.ProfileName
                : Path.GetFileNameWithoutExtension(profile.FileName ?? "default");

            var compat = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TeknoParrotUI", "prefixes", profileName);
            Directory.CreateDirectory(compat);
            return compat;
        }

        private static string ResolveSteamClientPath()
        {
            // proton only needs the directory to exist.
            var steam = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Steam");
            Directory.CreateDirectory(steam);
            return steam;
        }

        /// <summary>
        /// Clears the Proton session state (call on game exit).
        /// </summary>
        public static void EndSession()
        {
            ProtonRuntime.Enabled = false;
            ProtonRuntime.CurrentGame = null;
            ProtonRuntime.ExpectedExecutable = null;
            ProtonRuntime.WineBinary = null;
            ProtonRuntime.WinePrefix = null;
        }

        /// <summary>
        /// Resolves wine + prefix for a game BEFORE launch and initializes the
        /// prefix, so pipe bridges can create their in-prefix endpoints before
        /// the game boots (TGM3 etc. probe JVS immediately and never retry).
        /// Only for the plain-wine flow; proton-script prefixes are created by
        /// proton itself on first run.
        /// </summary>
        public static void PrepareSession(GameProfile profile)
        {
            ProtonRuntime.Enabled = true;
            ProtonRuntime.ExpectedExecutable = Path.GetFileName(profile.GamePath);

            var wine = ResolveWineBinary(profile?.ProtonVersion);
            if (wine == null)
                return;

            if (FindProtonScript(wine) == null)
            {
                // Plain wine: prefix path is deterministic - resolve and boot
                // it now so bridges can start helpers immediately.
                ProtonRuntime.WineBinary = wine;
                ProtonRuntime.WinePrefix = ResolvePrefix(profile);
            }
        }

        public static string ResolveWineBinary(string pinnedVersion = null)
        {
            var fromEnv = Environment.GetEnvironmentVariable("TP_WINE");
            if (!string.IsNullOrEmpty(fromEnv) && File.Exists(fromEnv))
                return fromEnv;

            // Explicit per-game pin: use the packaged Proton version.
            if (!string.IsNullOrEmpty(pinnedVersion))
            {
                var pinned = ProtonPackageManager.GetWineBinary(pinnedVersion);
                if (pinned != null)
                    return pinned;
            }

            // Default: system wine. GE-Proton breaks OpenParrotLoader's
            // thread-context injection ("Failed to Load DLL! (Error 3)"), so
            // packaged Proton is only used when a game pins it explicitly.
            foreach (var systemWine in new[] { "/usr/bin/wine", "/usr/local/bin/wine" })
            {
                if (File.Exists(systemWine))
                    return systemWine;
            }

            // No system wine: fall back to the newest packaged Proton.
            return ProtonPackageManager.ResolveWineBinary();
        }

        private static string ResolvePrefix(GameProfile profile)
        {
            var fromEnv = Environment.GetEnvironmentVariable("TP_WINEPREFIX");
            if (!string.IsNullOrEmpty(fromEnv))
                return fromEnv;

            var profileName = !string.IsNullOrEmpty(profile.ProfileName)
                ? profile.ProfileName
                : Path.GetFileNameWithoutExtension(profile.FileName ?? "default");

            var prefix = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TeknoParrotUI", "prefixes", profileName);
            Directory.CreateDirectory(prefix);

            // A prefix that was never fully booted lacks syswow64 (32-bit
            // support) and games fail with "could not load kernel32.dll".
            // Run wineboot to completion on first use.
            if (!File.Exists(Path.Combine(prefix, "system.reg")))
                InitializePrefix(prefix);

            return prefix;
        }

        private static void InitializePrefix(string prefix)
        {
            var wine = ResolveWineBinary();
            if (wine == null)
                return;

            var psi = new ProcessStartInfo
            {
                FileName = wine,
                Arguments = "wineboot --init",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            psi.Environment["WINEPREFIX"] = prefix;
            psi.Environment["WINEDEBUG"] = "-all";

            using var proc = Process.Start(psi);
            // First boot can take a while (font/registry setup).
            proc?.WaitForExit(180_000);
        }
    }
}
