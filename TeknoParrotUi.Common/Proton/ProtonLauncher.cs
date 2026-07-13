using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using TeknoParrotUi.Common.GameLaunch;

namespace TeknoParrotUi.Common.Proton
{
    /// <summary>
    /// Wraps a game's ProcessStartInfo so it runs under Wine/Proton on Linux.
    /// Windows builds never hit this code path.
    ///
    /// Wine binary resolution order:
    ///   1. TP_WINE environment variable
    ///   2. Per-game override (GameProfile.WineRunnerPath, then .ProtonVersion
    ///      - a packaged version name, or "system" to force system wine)
    ///   3. Global custom path (Linux Setup page / ParrotData.CustomWinePath)
    ///   4. System wine (/usr/bin/wine)
    ///   5. Newest Proton in the TeknoParrot Proton package directory
    ///      (~/.local/share/TeknoParrotUI/proton/&lt;version&gt;/bin/wine)
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
            var wine = ResolveWineBinary(profile)
                ?? throw new InvalidOperationException(
                    "No wine binary found. Install the TeknoParrot Proton package or system wine.");

            // Belt-and-suspenders: every ResolveWineBinary() branch already
            // checks File.Exists internally, but validate explicitly right
            // before use too - a package could be removed/moved between
            // resolution and launch, and a clear error here beats a cryptic
            // Process.Start failure.
            if (!File.Exists(wine))
                throw new FileNotFoundException("The selected Wine executable does not exist.", wine);

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

            var wine = ResolveWineBinary(profile);
            if (wine == null)
                return;

            if (FindProtonScript(wine) == null)
            {
                // Plain wine: prefix path is deterministic - resolve and boot
                // it now so bridges can start helpers immediately.
                ProtonRuntime.WineBinary = wine;
                var prefix = ResolvePrefix(profile);
                ProtonRuntime.WinePrefix = prefix;

                if (GameLaunchArguments.RequiresJapaneseLocale(profile))
                    EnsureJapaneseCodepage(wine, prefix);
            }
        }

        /// <summary>
        /// Resolves the wine/Proton binary for a specific game, honoring its
        /// per-game runner choice (<see cref="GameProfile.WineRunnerPath"/> /
        /// <see cref="GameProfile.ProtonVersion"/>) before falling back to
        /// the global chain (<see cref="ResolveWineBinary(string)"/>). A
        /// per-game choice is more specific than the global default, so it's
        /// checked first.
        /// </summary>
        public static string ResolveWineBinary(GameProfile profile)
        {
            if (!string.IsNullOrEmpty(profile?.WineRunnerPath) && File.Exists(profile.WineRunnerPath))
                return profile.WineRunnerPath;

            return ResolveWineBinary(profile?.ProtonVersion);
        }

        public static string ResolveWineBinary(string pinnedVersion = null)
        {
            var fromEnv = Environment.GetEnvironmentVariable("TP_WINE");
            if (!string.IsNullOrEmpty(fromEnv) && File.Exists(fromEnv))
                return fromEnv;

            // Explicit per-game pin (packaged Proton version name, or
            // "system" to force system wine for just this game) takes
            // priority over the global default - a game-specific choice
            // should win over the general one.
            if (!string.IsNullOrEmpty(pinnedVersion))
            {
                var pinned = ResolvePinnedVersion(pinnedVersion);
                if (pinned != null)
                    return pinned;
                // Pinned version missing/not installed - fall through to the
                // global chain so the game still starts; the UI warns about
                // the mismatch.
            }

            // User-configured explicit path (Linux Setup page) - global
            // default for systems where wine isn't at /usr/bin/wine or the
            // packaged Proton dir.
            var custom = Lazydata.ParrotData?.CustomWinePath;
            if (!string.IsNullOrEmpty(custom) && File.Exists(custom))
                return custom;

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

        /// <summary>"system" forces plain system wine; anything else is a packaged Proton version directory name.</summary>
        private static string ResolvePinnedVersion(string pinnedVersion)
        {
            if (string.Equals(pinnedVersion, "system", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var systemWine in new[] { "/usr/bin/wine", "/usr/local/bin/wine" })
                {
                    if (File.Exists(systemWine))
                        return systemWine;
                }
                return null;
            }

            return ProtonPackageManager.GetWineBinary(pinnedVersion);
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
                InitializePrefix(profile, prefix);

            return prefix;
        }

        private static void InitializePrefix(GameProfile profile, string prefix)
        {
            var wine = ResolveWineBinary(profile);
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

            // Best-effort: install the DirectX 9 compatibility libraries every
            // fresh prefix needs for older D3DX9 effect-framework games (see
            // InstallCompatLibraries). Never blocks game launch - a missing
            // winetricks/network just means this is skipped silently and the
            // Linux Setup page will still show it as outstanding.
            try { InstallCompatLibraries(prefix, wine); }
            catch (Exception ex) { ProtonLog.Write($"Compat library install skipped: {ex.Message}"); }
        }

        private const string CompatMarkerFile = ".tpui-compat-installed";

        /// <summary>
        /// Installs the real Microsoft d3dx9_XX.dll redistributables into a
        /// prefix via winetricks, replacing Wine's built-in D3DX9 effect (.fx)
        /// compiler - which is missing support for some fx_2_0 shader features
        /// (sampler object initializers, pass assignments) that older
        /// DirectX 9 games rely on, causing D3DXCreateEffectEx to fail with
        /// E_NOTIMPL and the game to error out/crash on model or shader load.
        /// No-op (and does not throw) when winetricks isn't installed - callers
        /// should surface that via <see cref="LinuxEnvironmentCheck"/> instead.
        /// Idempotent per-prefix via a marker file; pass force=true (Linux
        /// Setup page "reinstall") to run again anyway.
        /// </summary>
        public static bool InstallCompatLibraries(string prefix, string wine = null, bool force = false, Action<string> onOutput = null)
        {
            var marker = Path.Combine(prefix, CompatMarkerFile);
            if (!force && File.Exists(marker))
                return true;

            var winetricks = LinuxEnvironmentCheck.FindWinetricks();
            if (winetricks == null)
            {
                onOutput?.Invoke("winetricks not found - skipping DirectX 9 compatibility library install.");
                return false;
            }

            wine ??= ResolveWineBinary();
            if (wine == null)
            {
                onOutput?.Invoke("No wine binary resolved - cannot run winetricks.");
                return false;
            }
            if (!File.Exists(wine))
            {
                onOutput?.Invoke($"Selected wine executable does not exist: {wine}");
                return false;
            }

            const string arguments = "-q d3dx9";
            onOutput?.Invoke("Installing DirectX 9 compatibility libraries (winetricks d3dx9)...");
            var psi = new ProcessStartInfo
            {
                FileName = winetricks,
                Arguments = arguments,
                WorkingDirectory = prefix,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            psi.Environment["WINEPREFIX"] = prefix;
            psi.Environment["WINE"] = wine;
            psi.Environment["WINEDEBUG"] = "-all";
            // Batocera and other minimal images sometimes lack `taskset` -
            // winetricks prints a harmless "taskset/cpuset not available on
            // your platform!" warning but doesn't require it for anything we
            // use here; nothing to install/require on our side for that.

            var stdout = new System.Text.StringBuilder();
            var stderr = new System.Text.StringBuilder();

            using var proc = Process.Start(psi);
            if (proc == null)
            {
                onOutput?.Invoke("Failed to start winetricks process.");
                return false;
            }
            proc.OutputDataReceived += (_, e) => { if (e.Data != null) { stdout.AppendLine(e.Data); onOutput?.Invoke(e.Data); } };
            proc.ErrorDataReceived += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            // Downloads a ~30MB redistributable on first run per prefix - give it room.
            proc.WaitForExit(300_000);

            if (proc.ExitCode == 0)
            {
                File.WriteAllText(marker, DateTime.UtcNow.ToString("O"));
                onOutput?.Invoke("DirectX 9 compatibility libraries installed.");
                return true;
            }

            onOutput?.Invoke(BuildWinetricksFailureReport(proc.ExitCode, winetricks, arguments, prefix, wine, stdout.ToString(), stderr.ToString()));
            return false;
        }

        /// <summary>Substrings that plausibly indicate an actual network/download failure - only then do we say so.</summary>
        private static readonly string[] NetworkErrorMarkers =
        {
            "dns", "could not resolve", "name or service not known", "temporary failure in name resolution",
            "tls", "ssl", "certificate", "handshake",
            "http error", "404", "403", "connection refused", "connection reset", "connection timed out",
            "network is unreachable", "no route to host", "failed to connect", "curl:", "wget:",
            "download failed", "unable to download"
        };

        /// <summary>
        /// Builds a full diagnostic report for a failed winetricks run - replaces
        /// the old "(no network access?)" guess (wrong for any non-network
        /// failure, e.g. an architecture-mismatched wine binary) with the actual
        /// process details plus stdout/stderr, only mentioning network issues
        /// when the output actually looks like one.
        /// </summary>
        private static string BuildWinetricksFailureReport(int exitCode, string winetricksPath, string arguments,
            string prefix, string wine, string stdout, string stderr)
        {
            var log = new System.Text.StringBuilder();
            log.AppendLine($"winetricks failed with exit code {exitCode}.");
            log.AppendLine($"Executable: {winetricksPath}");
            log.AppendLine($"Arguments: {arguments}");
            log.AppendLine($"Working directory: {prefix}");
            log.AppendLine($"WINE: {wine}");
            log.AppendLine($"WINEPREFIX: {prefix}");
            log.AppendLine($"WINESERVER: {Environment.GetEnvironmentVariable("WINESERVER") ?? "(default)"}");
            log.AppendLine($"Host architecture: {RuntimeInformation.OSArchitecture}");
            log.AppendLine($"Selected Proton package: {DescribeSelectedPackage(wine)}");

            var mismatch = ProtonPackageManager.DescribeArchitectureMismatch(wine);
            if (mismatch != null)
            {
                log.AppendLine(mismatch);
            }
            else
            {
                var combined = stdout + "\n" + stderr;
                var looksLikeNetworkIssue = NetworkErrorMarkers.Any(marker => combined.Contains(marker, StringComparison.OrdinalIgnoreCase));
                log.AppendLine(looksLikeNetworkIssue
                    ? "This looks like a network/download failure (DNS, TLS or HTTP error detected in the output)."
                    : "Cause unclear from exit code alone - see full output below.");
            }

            log.AppendLine("STDOUT:");
            log.AppendLine(string.IsNullOrWhiteSpace(stdout) ? "(empty)" : stdout.TrimEnd());
            log.AppendLine("STDERR:");
            log.AppendLine(string.IsNullOrWhiteSpace(stderr) ? "(empty)" : stderr.TrimEnd());
            return log.ToString();
        }

        private static string DescribeSelectedPackage(string wine)
        {
            var version = ProtonPackageManager.GetPackageVersionForBinary(wine);
            if (version == null)
                return $"{wine} (not a packaged Proton build - system wine or a custom path)";

            var arch = ProtonPackageManager.DetectPackageArchitecture(Path.Combine(ProtonPackageManager.PackageRoot, version));
            return $"{version} ({wine})" + (arch.HasValue ? $" - {ProtonPackageManager.ArchLabel(arch.Value)}" : "");
        }

        /// <summary>
        /// Nesica/Japan-region titles need CP932 as the ANSI/OEM codepage for
        /// their native (non-Unicode) resource strings - MessageBox text etc.
        /// - to render correctly instead of as mojibake. Setting LANG/LC_ALL
        /// on the game process (<see cref="GameLaunchArguments.ApplyJapaneseLocaleFix"/>)
        /// only fixes HKCU\Control Panel\International (Wine re-derives that
        /// from the Unix locale on every process start); the ACP/OEMCP values
        /// under HKLM\...\Nls\CodePage are baked into the prefix ONCE at
        /// `wineboot --init` using whatever locale was active back then and
        /// are never re-derived afterwards, so they need patching directly.
        /// Idempotent - cheap enough to run at the start of every session.
        /// </summary>
        private static void EnsureJapaneseCodepage(string wine, string prefix)
        {
            const string codepageKey = @"HKLM\System\CurrentControlSet\Control\Nls\CodePage";
            RunRegAdd(wine, prefix, codepageKey, "ACP", "932");
            RunRegAdd(wine, prefix, codepageKey, "OEMCP", "932");

            // With the codepage fixed, ANSI bytes decode to the correct CJK
            // Unicode codepoints - but Windows-only fonts these games ask for
            // by name (MS Gothic/MS UI Gothic/MS PGothic/MS(P)Mincho) and even
            // Wine's default UI fonts (Tahoma, MS Shell Dlg(2), Arial, MS Sans
            // Serif - used for window/dialog captions) don't exist in Wine and
            // have no CJK glyph coverage, so the correctly-decoded text still
            // renders as tofu (□). Substitute them all for an installed CJK
            // font via Wine's font replacement table so every UI element
            // (including window title bars) actually has glyphs to draw.
            const string fontKey = @"HKCU\Software\Wine\Fonts\Replacements";
            const string cjkFont = "Noto Sans CJK JP";
            foreach (var fontName in new[]
                     {
                         "MS Gothic", "MS UI Gothic", "MS PGothic", "MS Mincho", "MS PMincho",
                         "Tahoma", "MS Shell Dlg", "MS Shell Dlg 2", "Arial", "MS Sans Serif"
                     })
            {
                RunRegAdd(wine, prefix, fontKey, fontName, cjkFont);
            }
        }

        private static void RunRegAdd(string wine, string prefix, string key, string valueName, string data)
        {
            var psi = new ProcessStartInfo
            {
                FileName = wine,
                Arguments = $"reg add \"{key}\" /v \"{valueName}\" /d \"{data}\" /f",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            psi.Environment["WINEPREFIX"] = prefix;
            psi.Environment["WINEDEBUG"] = "-all";

            using var proc = Process.Start(psi);
            proc?.WaitForExit(10_000);
        }
    }
}
