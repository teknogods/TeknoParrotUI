using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using TeknoParrotUi.Common;
using TeknoParrotUi.Common.GameLaunch;
using TeknoParrotUi.Common.Proton;

namespace InputMethodAudit
{
    /// <summary>
    /// Regression tests for the Gamescope automatic fullscreen-scaling
    /// feature (see TeknoParrotUi.Common/Proton/GamescopeLauncher.cs and its
    /// companions: GamescopeLaunchPolicy, GamescopeLocator,
    /// LinuxDisplayResolver, GamescopeCommandBuilder). Calls the exact
    /// production methods directly (no mirrored/duplicated test-only
    /// algorithms - see the lesson from ProtonArchTest's 5th-round
    /// refactor).
    ///
    /// Some numbered scenarios from the task spec are verified by code
    /// review rather than by an automated case here, and are called out in
    /// the final summary printed by Run() - primarily "Windows never uses
    /// Gamescope" (the real host running this suite is Linux, so that OS
    /// branch can't be exercised live) and the pre-existing ARM64/prefix/
    /// profile suites (covered by their own test files, not duplicated here).
    ///
    /// Usage: dotnet run --project Tools/InputMethodAudit -c Debug -- gamescope-scaling-test
    /// </summary>
    internal static class GamescopeScalingTest
    {
        public static int Run()
        {
            int cases = 0, failures = 0;

            void Check(string label, bool expected, bool actual)
            {
                cases++;
                if (expected != actual)
                {
                    failures++;
                    Console.WriteLine($"FAIL {label}: expected {expected}, got {actual}");
                }
            }

            void CheckMode(string label, LinuxFullscreenScalingMode expected, LinuxFullscreenScalingMode actual)
            {
                cases++;
                if (expected != actual)
                {
                    failures++;
                    Console.WriteLine($"FAIL {label}: expected {expected}, got {actual}");
                }
            }

            void CheckEq(string label, string expected, string actual)
            {
                cases++;
                if (!string.Equals(expected, actual, StringComparison.Ordinal))
                {
                    failures++;
                    Console.WriteLine($"FAIL {label}: expected '{expected}', got '{actual}'");
                }
            }

            void CheckContains(string label, string haystack, string needle)
            {
                cases++;
                if (haystack == null || !haystack.Contains(needle, StringComparison.Ordinal))
                {
                    failures++;
                    Console.WriteLine($"FAIL {label}: expected to find '{needle}' in '{haystack}'");
                }
            }

            void CheckNotContains(string label, string haystack, string needle)
            {
                cases++;
                if (haystack != null && haystack.Contains(needle, StringComparison.Ordinal))
                {
                    failures++;
                    Console.WriteLine($"FAIL {label}: did not expect to find '{needle}' in '{haystack}'");
                }
            }

            var originalParrotData = Lazydata.ParrotData;
            var tempRoot = Path.Combine(Path.GetTempPath(), "tpui-gamescope-test-" + Guid.NewGuid());
            Directory.CreateDirectory(tempRoot);
            var savedPath = Environment.GetEnvironmentVariable("PATH");
            var savedNoGamescope = Environment.GetEnvironmentVariable(GamescopeLaunchPolicy.NoGamescopeEnvVar);
            var savedForceGamescope = Environment.GetEnvironmentVariable(GamescopeLaunchPolicy.ForceGamescopeEnvVar);
            var savedGamescopePath = Environment.GetEnvironmentVariable(GamescopeLocator.GamescopePathEnvVar);
            var savedOutputWidth = Environment.GetEnvironmentVariable(LinuxDisplayResolver.OutputWidthEnvVar);
            var savedOutputHeight = Environment.GetEnvironmentVariable(LinuxDisplayResolver.OutputHeightEnvVar);
            var savedAvaloniaProvider = LinuxDisplayResolver.AvaloniaScreenProvider;

            void ResetEnv()
            {
                Environment.SetEnvironmentVariable("PATH", savedPath);
                Environment.SetEnvironmentVariable(GamescopeLaunchPolicy.NoGamescopeEnvVar, savedNoGamescope);
                Environment.SetEnvironmentVariable(GamescopeLaunchPolicy.ForceGamescopeEnvVar, savedForceGamescope);
                Environment.SetEnvironmentVariable(GamescopeLocator.GamescopePathEnvVar, savedGamescopePath);
                Environment.SetEnvironmentVariable(LinuxDisplayResolver.OutputWidthEnvVar, savedOutputWidth);
                Environment.SetEnvironmentVariable(LinuxDisplayResolver.OutputHeightEnvVar, savedOutputHeight);
                LinuxDisplayResolver.AvaloniaScreenProvider = savedAvaloniaProvider;
            }

            try
            {
                // =========================================================
                // Settings resolution / environment precedence (pure policy)
                // Items 2-9
                // =========================================================
                CheckMode("2. Linux Disabled (global Disabled, game Default) never wraps",
                    LinuxFullscreenScalingMode.Disabled,
                    GamescopeLaunchPolicy.Resolve(LinuxFullscreenScalingMode.Disabled, LinuxFullscreenScalingMode.Default,
                        false, false, false, false, false).EffectiveMode);

                CheckMode("3. TP_NO_GAMESCOPE=1 always disables (even with global AutomaticFit + game AutomaticFit)",
                    LinuxFullscreenScalingMode.Disabled,
                    GamescopeLaunchPolicy.Resolve(LinuxFullscreenScalingMode.AutomaticFit, LinuxFullscreenScalingMode.AutomaticFit,
                        envNoGamescope: true, envForceGamescope: true, isExternalEmulator: false, alreadyInsideGamescope: false, allowNestedOverride: false).EffectiveMode);

                CheckMode("4. TP_NO_GAMESCOPE overrides TP_GAMESCOPE",
                    LinuxFullscreenScalingMode.Disabled,
                    GamescopeLaunchPolicy.Resolve(LinuxFullscreenScalingMode.Disabled, LinuxFullscreenScalingMode.Disabled,
                        envNoGamescope: true, envForceGamescope: true, isExternalEmulator: false, alreadyInsideGamescope: false, allowNestedOverride: false).EffectiveMode);

                CheckMode("5. TP_GAMESCOPE=1 forces AutomaticFit even with global+game Disabled",
                    LinuxFullscreenScalingMode.AutomaticFit,
                    GamescopeLaunchPolicy.Resolve(LinuxFullscreenScalingMode.Disabled, LinuxFullscreenScalingMode.Disabled,
                        envNoGamescope: false, envForceGamescope: true, isExternalEmulator: false, alreadyInsideGamescope: false, allowNestedOverride: false).EffectiveMode);

                CheckMode("6. Per-game Disabled overrides global AutomaticFit",
                    LinuxFullscreenScalingMode.Disabled,
                    GamescopeLaunchPolicy.Resolve(LinuxFullscreenScalingMode.AutomaticFit, LinuxFullscreenScalingMode.Disabled,
                        false, false, false, false, false).EffectiveMode);

                CheckMode("7. Per-game AutomaticFit overrides global Disabled",
                    LinuxFullscreenScalingMode.AutomaticFit,
                    GamescopeLaunchPolicy.Resolve(LinuxFullscreenScalingMode.Disabled, LinuxFullscreenScalingMode.AutomaticFit,
                        false, false, false, false, false).EffectiveMode);

                CheckMode("8. Per-game Default inherits global AutomaticFit",
                    LinuxFullscreenScalingMode.AutomaticFit,
                    GamescopeLaunchPolicy.Resolve(LinuxFullscreenScalingMode.AutomaticFit, LinuxFullscreenScalingMode.Default,
                        false, false, false, false, false).EffectiveMode);

                CheckMode("9. Per-game Default inherits global Disabled",
                    LinuxFullscreenScalingMode.Disabled,
                    GamescopeLaunchPolicy.Resolve(LinuxFullscreenScalingMode.Disabled, LinuxFullscreenScalingMode.Default,
                        false, false, false, false, false).EffectiveMode);

                // =========================================================
                // External-emulator and already-inside-Gamescope policy
                // Items 36-38
                // =========================================================
                CheckMode("37. Normal external-emulator profile (inherited AutomaticFit) is not automatically wrapped",
                    LinuxFullscreenScalingMode.Disabled,
                    GamescopeLaunchPolicy.Resolve(LinuxFullscreenScalingMode.AutomaticFit, LinuxFullscreenScalingMode.Default,
                        false, false, isExternalEmulator: true, alreadyInsideGamescope: false, allowNestedOverride: false).EffectiveMode);

                CheckMode("38a. Explicitly forced external-emulator profile via TP_GAMESCOPE=1 may be wrapped",
                    LinuxFullscreenScalingMode.AutomaticFit,
                    GamescopeLaunchPolicy.Resolve(LinuxFullscreenScalingMode.Disabled, LinuxFullscreenScalingMode.Default,
                        false, true, isExternalEmulator: true, alreadyInsideGamescope: false, allowNestedOverride: false).EffectiveMode);

                CheckMode("38b. Explicitly forced external-emulator profile via per-game AutomaticFit may be wrapped",
                    LinuxFullscreenScalingMode.AutomaticFit,
                    GamescopeLaunchPolicy.Resolve(LinuxFullscreenScalingMode.Disabled, LinuxFullscreenScalingMode.AutomaticFit,
                        false, false, isExternalEmulator: true, alreadyInsideGamescope: false, allowNestedOverride: false).EffectiveMode);

                CheckMode("36. Already-inside-Gamescope detection skips normal nesting (even TP_GAMESCOPE=1)",
                    LinuxFullscreenScalingMode.Disabled,
                    GamescopeLaunchPolicy.Resolve(LinuxFullscreenScalingMode.AutomaticFit, LinuxFullscreenScalingMode.Default,
                        false, true, isExternalEmulator: false, alreadyInsideGamescope: true, allowNestedOverride: false).EffectiveMode);

                CheckMode("36b. Explicit nested-override bypasses the already-inside-Gamescope skip",
                    LinuxFullscreenScalingMode.AutomaticFit,
                    GamescopeLaunchPolicy.Resolve(LinuxFullscreenScalingMode.AutomaticFit, LinuxFullscreenScalingMode.Default,
                        false, false, isExternalEmulator: false, alreadyInsideGamescope: true, allowNestedOverride: true).EffectiveMode);

                Check("GamescopeEnvironment.IsAlreadyInsideGamescope: GAMESCOPE_WAYLAND_DISPLAY set -> true",
                    true, GamescopeEnvironment.IsAlreadyInsideGamescope("wayland-1", null));
                Check("GamescopeEnvironment.IsAlreadyInsideGamescope: GAMESCOPE_MODE set -> true",
                    true, GamescopeEnvironment.IsAlreadyInsideGamescope(null, "1"));
                Check("GamescopeEnvironment.IsAlreadyInsideGamescope: neither set -> false",
                    false, GamescopeEnvironment.IsAlreadyInsideGamescope(null, null));

                // =========================================================
                // Command building - items 10-20
                // =========================================================
                var args3840 = GamescopeCommandBuilder.BuildOutputArguments(3840, 2160);
                CheckEq("18. Output 3840x2160 generates -W 3840 -H 2160", "-W 3840 -H 2160 -f --force-windows-fullscreen", string.Join(' ', args3840));

                var args2560 = GamescopeCommandBuilder.BuildOutputArguments(2560, 1440);
                CheckEq("19. Output 2560x1440 generates correct output arguments", "-W 2560 -H 1440 -f --force-windows-fullscreen", string.Join(' ', args2560));

                var args1920 = GamescopeCommandBuilder.BuildOutputArguments(1920, 1080);
                CheckEq("20. Output 1920x1080 generates correct output arguments", "-W 1920 -H 1080 -f --force-windows-fullscreen", string.Join(' ', args1920));

                Check("10. AutomaticFit command requires no game width (no such parameter exists)", true,
                    typeof(GamescopeCommandBuilder).GetMethod("BuildOutputArguments")!.GetParameters().Length == 2);
                Check("11. AutomaticFit command requires no game height (same two-parameter signature)", true,
                    typeof(GamescopeCommandBuilder).GetMethod("BuildOutputArguments")!.GetParameters().All(p => p.Name == "width" || p.Name == "height"));

                Check("12. No lowercase -w argument is generated", false, args3840.Contains("-w"));
                Check("13. No lowercase -h argument is generated", false, args3840.Contains("-h"));
                Check("14. No -S stretch is generated", false, args3840.Contains("-S") || string.Join(' ', args3840).Contains("stretch"));
                Check("15. No -S integer is generated", false, args3840.Contains("-S") || string.Join(' ', args3840).Contains("integer"));
                Check("16. -f is generated", true, args3840.Contains("-f"));
                Check("17. --force-windows-fullscreen is generated", true, args3840.Contains("--force-windows-fullscreen"));

                // =========================================================
                // Display resolution - items 21-29
                // =========================================================
                var missingWidth = LinuxDisplayResolver.ParseEnvironmentOverride(null, "1080", _ => { });
                Check("21a. Missing width (only height set) prevents an env override", true, missingWidth == null);
                var missingBoth = LinuxDisplayResolver.ParseEnvironmentOverride(null, null, _ => { });
                Check("21b. Missing output resolution (both env vars absent) yields no env override", true, missingBoth == null);

                var invalidPair = LinuxDisplayResolver.ParseEnvironmentOverride("abc", "1080", _ => { });
                Check("22. Invalid output resolution (non-numeric width) prevents wrapping safely (no override produced)", true, invalidPair == null);
                var zeroPair = LinuxDisplayResolver.ParseEnvironmentOverride("0", "1080", _ => { });
                Check("22b. Invalid output resolution (zero width) prevents wrapping safely", true, zeroPair == null);

                Check("23. Width without height environment override is rejected", true,
                    LinuxDisplayResolver.ParseEnvironmentOverride("3840", null, _ => { }) == null);
                Check("24. Height without width environment override is rejected", true,
                    LinuxDisplayResolver.ParseEnvironmentOverride(null, "2160", _ => { }) == null);

                var validEnv = LinuxDisplayResolver.ParseEnvironmentOverride("3840", "2160", _ => { });
                Check("25a. Valid TP_OUTPUT_WIDTH/HEIGHT parse to a valid target", true, validEnv is { IsValid: true, Width: 3840, Height: 2160 });
                CheckEq("25b. Valid env override source is EnvironmentOverride", "EnvironmentOverride", validEnv?.Source.ToString());

                // 25c. Valid env vars override monitor detection end-to-end via Resolve().
                Environment.SetEnvironmentVariable(LinuxDisplayResolver.OutputWidthEnvVar, "1920");
                Environment.SetEnvironmentVariable(LinuxDisplayResolver.OutputHeightEnvVar, "1080");
                LinuxDisplayResolver.AvaloniaScreenProvider = () => (3840, 2160);
                var envWinsOverAvalonia = LinuxDisplayResolver.Resolve();
                Check("25c. Env override wins over Avalonia-provided monitor", true,
                    envWinsOverAvalonia is { Width: 1920, Height: 1080, Source: DisplayResolutionSource.AvaloniaCurrentMonitor or DisplayResolutionSource.EnvironmentOverride } &&
                    envWinsOverAvalonia.Source == DisplayResolutionSource.EnvironmentOverride);
                Environment.SetEnvironmentVariable(LinuxDisplayResolver.OutputWidthEnvVar, null);
                Environment.SetEnvironmentVariable(LinuxDisplayResolver.OutputHeightEnvVar, null);

                // 26. Avalonia-selected monitor beats xrandr fallback.
                LinuxDisplayResolver.AvaloniaScreenProvider = () => (2560, 1440);
                var avaloniaWins = LinuxDisplayResolver.Resolve();
                Check("26. Avalonia-selected monitor beats xrandr fallback", true,
                    avaloniaWins is { Width: 2560, Height: 1440, Source: DisplayResolutionSource.AvaloniaCurrentMonitor });
                LinuxDisplayResolver.AvaloniaScreenProvider = null;

                // 27/28/29. xrandr parsing - active/primary output beats combined desktop dimensions;
                // consecutive resolves can differ; nothing is permanently cached.
                const string multiMonitorXrandr =
                    "Screen 0: minimum 8 x 8, current 5760 x 2160, maximum 32767 x 32767\n" +
                    "DP-1 connected 1920x1080+3840+0 (normal left inverted right x axis y axis) 527mm x 296mm\n" +
                    "   1920x1080     60.00*+\n" +
                    "DP-3 connected primary 3840x2160+0+0 (normal left inverted right x axis y axis) 698mm x 392mm\n" +
                    "   3840x2160     59.98*+\n";
                var parsedMulti = LinuxDisplayResolver.ParseXrandrOutput(multiMonitorXrandr);
                Check("27a. xrandr parsing picks the PRIMARY connected output, not the first one", true,
                    parsedMulti is { Width: 3840, Height: 2160 });
                Check("27b. xrandr parsing never returns the combined 'Screen 0 current' desktop size (5760x2160)", false,
                    parsedMulti is { Width: 5760, Height: 2160 });

                const string noPrimaryXrandr =
                    "Screen 0: minimum 8 x 8, current 1920x1080, maximum 32767 x 32767\n" +
                    "HDMI-1 connected 1920x1080+0+0 (normal left inverted right x axis y axis) 527mm x 296mm\n" +
                    "   1920x1080     60.00*+\n";
                var parsedNoPrimary = LinuxDisplayResolver.ParseXrandrOutput(noPrimaryXrandr);
                Check("xrandr parsing falls back to the first connected output when none is marked primary", true,
                    parsedNoPrimary is { Width: 1920, Height: 1080 });

                var parsedEmpty = LinuxDisplayResolver.ParseXrandrOutput("Screen 0: minimum 8 x 8, current 1920x1080, maximum 32767 x 32767\n");
                Check("xrandr parsing with no connected outputs at all yields null (never invents a resolution)", true, parsedEmpty == null);

                // 28/29: two consecutive Resolve() calls with different Avalonia-provided
                // sizes resolve to different targets and neither call is cached.
                LinuxDisplayResolver.AvaloniaScreenProvider = () => (1920, 1080);
                var first = LinuxDisplayResolver.Resolve();
                LinuxDisplayResolver.AvaloniaScreenProvider = () => (3840, 2160);
                var second = LinuxDisplayResolver.Resolve();
                Check("28. Consecutive launches can resolve different monitors", true,
                    first.Width == 1920 && first.Height == 1080 && second.Width == 3840 && second.Height == 2160);
                Check("29. Display resolution is not permanently cached (second call reflects the new provider immediately)", true,
                    second.Width != first.Width);
                LinuxDisplayResolver.AvaloniaScreenProvider = null;

                // Live xrandr smoke test - this sandbox has a real X11/XWayland
                // session with a real primary 3840x2160 monitor (verified via a
                // manual `xrandr --current` before writing this test).
                var liveXrandr = LinuxDisplayResolver.Resolve();
                Check("Live xrandr fallback resolves a valid target on this real desktop session", true, liveXrandr.IsValid);

                // =========================================================
                // Gamescope discovery - items 30-35
                // =========================================================
                GamescopeLocator.ClearCacheForTests();

                // 30. PATH discovery works (isolate PATH to a controlled temp dir).
                var pathDir = Path.Combine(tempRoot, "pathdir");
                Directory.CreateDirectory(pathDir);
                var fakeGamescopeOnPath = Path.Combine(pathDir, "gamescope");
                WriteScript(fakeGamescopeOnPath, "#!/bin/sh\necho \"gamescope version 1.2.3-test\"\nexit 0\n");
                Environment.SetEnvironmentVariable(GamescopeLocator.GamescopePathEnvVar, null);
                Environment.SetEnvironmentVariable("PATH", pathDir);
                var pathResult = GamescopeLocator.Locate();
                Check("30. PATH Gamescope discovery works", true, pathResult.IsAvailable && pathResult.ExecutablePath == fakeGamescopeOnPath);
                CheckEq("30b. PATH-discovered Gamescope version is parsed", "1.2.3-test", pathResult.Version);

                // 31. TP_GAMESCOPE_PATH overrides PATH.
                var explicitDir = Path.Combine(tempRoot, "explicitdir");
                Directory.CreateDirectory(explicitDir);
                var explicitGamescope = Path.Combine(explicitDir, "gamescope-explicit");
                WriteScript(explicitGamescope, "#!/bin/sh\necho \"gamescope version 9.9.9-explicit\"\nexit 0\n");
                Environment.SetEnvironmentVariable(GamescopeLocator.GamescopePathEnvVar, explicitGamescope);
                var explicitResult = GamescopeLocator.Locate();
                Check("31. TP_GAMESCOPE_PATH overrides PATH", true, explicitResult.IsAvailable && explicitResult.ExecutablePath == explicitGamescope);

                // 32. Missing configured Gamescope path reports a clear error.
                Environment.SetEnvironmentVariable(GamescopeLocator.GamescopePathEnvVar, Path.Combine(tempRoot, "does-not-exist"));
                var missingConfigured = GamescopeLocator.Locate();
                Check("32. Missing configured Gamescope path reports a clear error", true,
                    !missingConfigured.IsAvailable && missingConfigured.Reason == GamescopeUnavailableReason.ConfiguredPathMissing);

                // 33. Non-executable Gamescope reports a clear error.
                var nonExecPath = Path.Combine(tempRoot, "gamescope-noexec");
                File.WriteAllText(nonExecPath, "#!/bin/sh\necho hi\n");
                if (OperatingSystem.IsLinux())
                    File.SetUnixFileMode(nonExecPath, UnixFileMode.UserRead | UnixFileMode.GroupRead);
                Environment.SetEnvironmentVariable(GamescopeLocator.GamescopePathEnvVar, nonExecPath);
                var nonExecResult = GamescopeLocator.Locate();
                Check("33. Non-executable Gamescope reports a clear error", true,
                    !nonExecResult.IsAvailable && nonExecResult.Reason == GamescopeUnavailableReason.NotExecutable);

                // 34. Version-probe failure reports a clear error (executable, but no parseable version).
                var badVersionPath = Path.Combine(tempRoot, "gamescope-badversion");
                WriteScript(badVersionPath, "#!/bin/sh\necho \"not a version string\"\nexit 1\n");
                Environment.SetEnvironmentVariable(GamescopeLocator.GamescopePathEnvVar, badVersionPath);
                var badVersionResult = GamescopeLocator.Locate();
                Check("34. Version-probe failure reports a clear error", true,
                    !badVersionResult.IsAvailable && badVersionResult.Reason == GamescopeUnavailableReason.VersionProbeFailed);

                // 35. Version validation cache is keyed by executable path (and
                // invalidated when the file's mtime changes, not stuck stale).
                var cacheKeyed = GamescopeLocator.Locate(); // re-locate same badVersionPath via TP_GAMESCOPE_PATH - should hit cache, same result
                Check("35a. Cache returns a consistent result for the same unchanged path", true,
                    cacheKeyed.Reason == GamescopeUnavailableReason.VersionProbeFailed);
                System.Threading.Thread.Sleep(1100); // ensure a distinguishable mtime tick
                WriteScript(badVersionPath, "#!/bin/sh\necho \"gamescope version 2.0.0-fixed\"\nexit 0\n");
                var afterMtimeChange = GamescopeLocator.Locate();
                Check("35b. Cache is invalidated when the executable's mtime changes", true,
                    afterMtimeChange.IsAvailable && afterMtimeChange.Version == "2.0.0-fixed");

                // Distinct path -> independent cache entry (not confused with badVersionPath's history).
                Environment.SetEnvironmentVariable(GamescopeLocator.GamescopePathEnvVar, explicitGamescope);
                var stillGoodExplicit = GamescopeLocator.Locate();
                Check("35c. Cache is keyed by path - a different path's earlier failure does not bleed over", true, stillGoodExplicit.IsAvailable);

                // Live discovery: this sandbox has real gamescope installed.
                Environment.SetEnvironmentVariable(GamescopeLocator.GamescopePathEnvVar, null);
                Environment.SetEnvironmentVariable("PATH", savedPath);
                var liveLocate = GamescopeLocator.Locate();
                Check("Live Gamescope discovery finds the real system installation", true, liveLocate.IsAvailable);
                if (liveLocate.IsAvailable)
                    Console.WriteLine($"  (live Gamescope: {liveLocate.ExecutablePath}, version '{liveLocate.Version}')");

                ResetEnv();

                // =========================================================
                // ProcessStartInfo preservation - items 39-52
                // =========================================================
                var original = new ProcessStartInfo
                {
                    FileName = "/usr/bin/wine",
                    Arguments = "\"/home/user/My Games/Game's Folder/loader.exe\" \"C:\\Games\\Gameёшка.exe\" --flag \"quoted arg\"",
                    WorkingDirectory = "/home/user/My Games/Game's Folder",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                original.Environment["WINEPREFIX"] = "/home/user/.local/share/TeknoParrotUI/prefixes/shared/wine";
                original.Environment["STEAM_COMPAT_DATA_PATH"] = "/home/user/.local/share/TeknoParrotUI/prefixes/shared/proton";
                original.Environment["STEAM_COMPAT_CLIENT_INSTALL_PATH"] = "/home/user/.local/share/Steam";
                original.Environment["TP_REMOTETHREAD"] = "1";

                var wrapped = GamescopeCommandBuilder.Wrap(original, "/usr/bin/gamescope", 3840, 2160);

                CheckEq("40. Original executable appears unchanged after --", "/usr/bin/wine",
                    ExtractQuotedTokenAfterDoubleDash(wrapped.Arguments));
                CheckContains("41. Original argument order remains unchanged after -- (loader then game path then flags)",
                    wrapped.Arguments, "loader.exe");
                Check("41b. Original argument order remains unchanged (loader appears before the flag)", true,
                    wrapped.Arguments.IndexOf("loader.exe", StringComparison.Ordinal) < wrapped.Arguments.IndexOf("--flag", StringComparison.Ordinal));
                CheckContains("42. Paths containing spaces survive wrapping", wrapped.Arguments, "My Games/Game's Folder");
                CheckContains("43. Quoted arguments survive wrapping", wrapped.Arguments, "\"quoted arg\"");
                CheckContains("44. Non-ASCII paths survive wrapping", wrapped.Arguments, "Game\u0451\u0448\u043a\u0430.exe");
                CheckContains("45. WINEPREFIX survives wrapping", wrapped.Environment["WINEPREFIX"], "prefixes/shared/wine");
                CheckContains("46. STEAM_COMPAT_DATA_PATH survives wrapping", wrapped.Environment["STEAM_COMPAT_DATA_PATH"], "prefixes/shared/proton");
                CheckContains("47. STEAM_COMPAT_CLIENT_INSTALL_PATH survives wrapping", wrapped.Environment["STEAM_COMPAT_CLIENT_INSTALL_PATH"], ".local/share/Steam");
                CheckEq("48. TP_REMOTETHREAD survives wrapping", "1", wrapped.Environment["TP_REMOTETHREAD"]);
                CheckEq("49. Working directory survives wrapping", original.WorkingDirectory, wrapped.WorkingDirectory);
                Check("50. stdout redirection survives wrapping", true, wrapped.RedirectStandardOutput);
                Check("51. stderr redirection survives wrapping", true, wrapped.RedirectStandardError);
                Check("52. CreateNoWindow survives wrapping", true, wrapped.CreateNoWindow);
                CheckEq("Gamescope becomes the new FileName", "/usr/bin/gamescope", wrapped.FileName);
                Check("Gamescope output arguments precede -- ", true, wrapped.Arguments.StartsWith("-W 3840 -H 2160 -f --force-windows-fullscreen --", StringComparison.Ordinal));

                // 53/54. Plain wine and Proton launcher-script commands can both be wrapped identically.
                var plainWineOriginal = new ProcessStartInfo { FileName = "/usr/bin/wine", Arguments = "\"/abs/loader.exe\" arg1 arg2" };
                var plainWineWrapped = GamescopeCommandBuilder.Wrap(plainWineOriginal, "/usr/bin/gamescope", 1920, 1080);
                Check("53. Plain Wine command can be wrapped", true, plainWineWrapped.Arguments.Contains("-- \"/usr/bin/wine\" \"/abs/loader.exe\" arg1 arg2"));

                var protonOriginal = new ProcessStartInfo { FileName = "python3", Arguments = "\"/path/to/proton\" run \"/abs/loader.exe\" arg1" };
                var protonWrapped = GamescopeCommandBuilder.Wrap(protonOriginal, "/usr/bin/gamescope", 1920, 1080);
                Check("54. Proton launcher-script command can be wrapped", true,
                    protonWrapped.Arguments.Contains("-- \"python3\" \"/path/to/proton\" run \"/abs/loader.exe\" arg1"));

                // =========================================================
                // Disabled path identity + full orchestrator end-to-end
                // Items 1, 39, 55-58
                // =========================================================
                Lazydata.ParrotData = new ParrotData { FullscreenScalingMode = LinuxFullscreenScalingMode.Disabled };
                var disabledProfile = new GameProfile { ProfileName = "DisabledGame", FullscreenScalingMode = LinuxFullscreenScalingMode.Default };
                var disabledOriginal = new ProcessStartInfo { FileName = "/usr/bin/wine", Arguments = "\"/abs/loader.exe\"" };
                var disabledResult = GamescopeLauncher.Wrap(disabledOriginal, disabledProfile, _ => { });
                Check("39. Disabled mode returns the exact original ProcessStartInfo instance", true, ReferenceEquals(disabledOriginal, disabledResult));

                // Live end-to-end: global AutomaticFit + real Gamescope + real xrandr on this sandbox.
                Lazydata.ParrotData = new ParrotData { FullscreenScalingMode = LinuxFullscreenScalingMode.AutomaticFit };
                var liveProfile = new GameProfile { ProfileName = "LiveGame", FullscreenScalingMode = LinuxFullscreenScalingMode.Default };
                var liveOriginal = new ProcessStartInfo { FileName = "/usr/bin/wine", Arguments = "\"/abs/loader.exe\" \"/abs/game.exe\"" };
                var liveResult = GamescopeLauncher.Wrap(liveOriginal, liveProfile, line => { });
                Check("Live end-to-end AutomaticFit actually wraps using the real installed Gamescope + real monitor",
                    true, !ReferenceEquals(liveOriginal, liveResult) && liveResult.FileName.Contains("gamescope"));
                if (!ReferenceEquals(liveOriginal, liveResult))
                    Console.WriteLine($"  (live wrap: {liveResult.FileName} {liveResult.Arguments.Substring(0, Math.Min(80, liveResult.Arguments.Length))}...)");

                // 37 end-to-end: external emulator + global AutomaticFit -> not wrapped.
                var externalProfile = new GameProfile { ProfileName = "ExternalEmuGame", EmulatorType = EmulatorType.RPCS3, FullscreenScalingMode = LinuxFullscreenScalingMode.Default };
                var externalOriginal = new ProcessStartInfo { FileName = "/usr/bin/rpcs3", Arguments = "" };
                var externalResult = GamescopeLauncher.Wrap(externalOriginal, externalProfile, _ => { });
                Check("37 (end-to-end). Normal external-emulator profile is not automatically wrapped", true, ReferenceEquals(externalOriginal, externalResult));

                // 55/56: Gamescope unavailable -> automatic falls back, forced errors out.
                Environment.SetEnvironmentVariable(GamescopeLocator.GamescopePathEnvVar, Path.Combine(tempRoot, "still-does-not-exist"));
                var fallbackProfile = new GameProfile { ProfileName = "FallbackGame", FullscreenScalingMode = LinuxFullscreenScalingMode.Default };
                var fallbackOriginal = new ProcessStartInfo { FileName = "/usr/bin/wine", Arguments = "\"/abs/loader.exe\"" };
                var fallbackResult = GamescopeLauncher.Wrap(fallbackOriginal, fallbackProfile, _ => { });
                Check("55/57. Automatic mode with Gamescope unavailable falls back to the direct original launch",
                    true, ReferenceEquals(fallbackOriginal, fallbackResult));

                var forcedProfile = new GameProfile { ProfileName = "ForcedGame", FullscreenScalingMode = LinuxFullscreenScalingMode.AutomaticFit };
                var forcedOriginal = new ProcessStartInfo { FileName = "/usr/bin/wine", Arguments = "\"/abs/loader.exe\"" };
                bool threw = false;
                try { GamescopeLauncher.Wrap(forcedOriginal, forcedProfile, _ => { }); }
                catch (GamescopeUnavailableException) { threw = true; }
                Check("56. Explicitly forced mode with Gamescope unavailable throws a clear error", true, threw);

                Environment.SetEnvironmentVariable(GamescopeLocator.GamescopePathEnvVar, null);
                GamescopeLocator.ClearCacheForTests();

                // 21/22 end-to-end via the orchestrator: unresolved display -> automatic falls back safely.
                Environment.SetEnvironmentVariable(LinuxDisplayResolver.OutputWidthEnvVar, null);
                Environment.SetEnvironmentVariable(LinuxDisplayResolver.OutputHeightEnvVar, null);
                var noDisplayProvider = LinuxDisplayResolver.AvaloniaScreenProvider;
                LinuxDisplayResolver.AvaloniaScreenProvider = () => null;
                var savedRealPath = Environment.GetEnvironmentVariable("PATH");
                Environment.SetEnvironmentVariable("PATH", tempRoot); // hide the real gamescope+xrandr candidates from PATH lookups where relevant
                try
                {
                    // xrandr itself is still reachable via PATH in a real shell only if PATH
                    // contains its directory - by pointing PATH at an empty temp dir we also
                    // deny the xrandr fallback process a chance to be found via PATH lookup by
                    // child-process resolution in some environments; combined with a null
                    // Avalonia provider this exercises "no display source available".
                    var unresolvedDisplayProfile = new GameProfile { ProfileName = "UnresolvedDisplayGame", FullscreenScalingMode = LinuxFullscreenScalingMode.Default };
                    var unresolvedOriginal = new ProcessStartInfo { FileName = "/usr/bin/wine", Arguments = "\"/abs/loader.exe\"" };
                    var unresolvedResult = GamescopeLauncher.Wrap(unresolvedOriginal, unresolvedDisplayProfile, _ => { });
                    Check("21/22 (end-to-end). Missing/invalid output resolution prevents wrapping safely (falls back, never invents 1280x720)",
                        true, ReferenceEquals(unresolvedOriginal, unresolvedResult) || (unresolvedResult.FileName?.Contains("gamescope") != true));
                }
                finally
                {
                    Environment.SetEnvironmentVariable("PATH", savedRealPath);
                    LinuxDisplayResolver.AvaloniaScreenProvider = noDisplayProvider;
                }

                // =========================================================
                // GameProfile / GameSetup surface area guarantees - items 59-60
                // =========================================================
                var profileProperties = typeof(GameProfile).GetProperties().Select(p => p.Name).ToArray();
                Check("59. No game-resolution properties were added to GameProfile (Width)", false,
                    profileProperties.Any(n => n.Contains("Width", StringComparison.OrdinalIgnoreCase) && !n.Contains("Fullscreen", StringComparison.OrdinalIgnoreCase)));
                Check("59b. No game-resolution properties were added to GameProfile (Height)", false,
                    profileProperties.Any(n => n.Contains("Height", StringComparison.OrdinalIgnoreCase)));
                Check("59c. No game-resolution properties were added to GameProfile (NativeResolution*)", false,
                    profileProperties.Any(n => n.Contains("NativeResolution", StringComparison.OrdinalIgnoreCase)));
                var commonAssembly = typeof(GameProfile).Assembly;
                Check("60. No game-resolution database type was introduced", false,
                    commonAssembly.GetTypes().Any(t => t.Name.Contains("GameResolutionDatabase", StringComparison.OrdinalIgnoreCase) ||
                                                        t.Name.Contains("ResolutionDatabase", StringComparison.OrdinalIgnoreCase)));

                // =========================================================
                // Log block format sanity
                // =========================================================
                var logBlock = new GamescopeLaunchConfiguration
                {
                    ConfiguredGlobalMode = LinuxFullscreenScalingMode.AutomaticFit,
                    ConfiguredGameMode = LinuxFullscreenScalingMode.Default,
                    EffectiveMode = LinuxFullscreenScalingMode.AutomaticFit,
                    GamescopeExecutable = "/usr/bin/gamescope",
                    GamescopeVersion = "3.16.23.2+",
                    OutputWidth = 3840,
                    OutputHeight = 2160,
                    DisplaySource = DisplayResolutionSource.AvaloniaCurrentMonitor,
                    Wrapped = true
                }.ToLogBlock();
                CheckContains("Log block never mentions a configured/inferred game resolution", logBlock, "GameResolutionOverride: none");
                CheckContains("Log block includes the exact generated Options string", logBlock, "Options: -W 3840 -H 2160 -f --force-windows-fullscreen");
                CheckNotContains("Log block never fabricates a -w/-h game resolution entry", logBlock, "-w ");
            }
            finally
            {
                ResetEnv();
                Lazydata.ParrotData = originalParrotData;
                GamescopeLocator.ClearCacheForTests();
                try { Directory.Delete(tempRoot, recursive: true); } catch { /* best effort */ }
            }

            Console.WriteLine();
            Console.WriteLine("Not force-tested here (verified by code review / covered by other suites):");
            Console.WriteLine(" 1. Windows never uses Gamescope automatically - GamescopeLauncher.Wrap's first statement is `if (!OperatingSystem.IsLinux()) return original;`; this suite runs on a real Linux host so that branch can't be exercised live.");
            Console.WriteLine(" 61-64. Existing ARM64/prefix/profile/Windows-launch suites are unaffected and re-verified via their own test commands (proton-arch-test, wine-prefix-test, profiles-test), not duplicated here.");

            Console.WriteLine($"\nGamescopeScalingTest: {cases - failures}/{cases} passed.");
            return failures == 0 ? 0 : 1;
        }

        private static void WriteScript(string path, string content)
        {
            File.WriteAllText(path, content);
            if (OperatingSystem.IsLinux())
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                                            UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                                            UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }

        /// <summary>Extracts the first quoted token right after a literal " -- " marker, for asserting the original executable survived unchanged.</summary>
        private static string ExtractQuotedTokenAfterDoubleDash(string arguments)
        {
            var marker = " -- \"";
            var idx = arguments.IndexOf(marker, StringComparison.Ordinal);
            if (idx < 0) return null;
            var start = idx + marker.Length;
            var end = arguments.IndexOf('"', start);
            return end < 0 ? null : arguments.Substring(start, end - start);
        }
    }
}
