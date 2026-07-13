using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TeknoParrotUi.Common;
using TeknoParrotUi.Common.Proton;

namespace InputMethodAudit
{
    /// <summary>
    /// Regression tests for shared/isolated Wine and Proton prefixes (see
    /// TeknoParrotUi.Common/Proton/WinePrefixManager.cs) - the fix for every
    /// ordinary new game getting its own ~1.5 GB Wine/Proton environment.
    ///
    /// Covers mode resolution (Default/Shared/Isolated + global inheritance),
    /// shared vs isolated path construction for both plain Wine (WINEPREFIX)
    /// and Proton (STEAM_COMPAT_DATA_PATH + &lt;compat&gt;/pfx), the Japanese
    /// compatibility group's separate shared environments, existing-user
    /// migration (never silently move an already-initialized legacy prefix),
    /// reset scoping/safety, and initialization locking.
    ///
    /// Usage: dotnet run --project Tools/InputMethodAudit -- wine-prefix-test
    /// </summary>
    internal static class WinePrefixTest
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

            void CheckEq(string label, string expected, string actual)
            {
                cases++;
                if (!string.Equals(NormalizePath(expected), NormalizePath(actual), StringComparison.Ordinal))
                {
                    failures++;
                    Console.WriteLine($"FAIL {label}: expected '{expected}', got '{actual}'");
                }
            }

            void CheckNotEq(string label, string a, string b)
            {
                cases++;
                if (string.Equals(NormalizePath(a), NormalizePath(b), StringComparison.Ordinal))
                {
                    failures++;
                    Console.WriteLine($"FAIL {label}: expected different paths, both were '{a}'");
                }
            }

            void CheckMode(string label, WinePrefixMode expected, WinePrefixMode actual)
            {
                cases++;
                if (expected != actual)
                {
                    failures++;
                    Console.WriteLine($"FAIL {label}: expected {expected}, got {actual}");
                }
            }

            var originalParrotData = Lazydata.ParrotData;
            var tempRoot = Path.Combine(Path.GetTempPath(), "tpui-wineprefix-test-" + Guid.NewGuid());
            Directory.CreateDirectory(tempRoot);

            try
            {
                // =========================================================
                // Mode resolution
                // =========================================================
                {
                    var root = Path.Combine(tempRoot, "mode-resolution");

                    Lazydata.ParrotData = new ParrotData { DefaultWinePrefixMode = WinePrefixMode.Shared };
                    var r1 = WinePrefixManager.Resolve("game1", WinePrefixMode.Default, WinePrefixCompatibilityGroup.Standard, WineRunnerKind.PlainWine, root);
                    CheckMode("1. Per-game Default inherits global Shared", WinePrefixMode.Shared, r1.EffectiveMode);

                    Lazydata.ParrotData = new ParrotData { DefaultWinePrefixMode = WinePrefixMode.Isolated };
                    var r2 = WinePrefixManager.Resolve("game2", WinePrefixMode.Default, WinePrefixCompatibilityGroup.Standard, WineRunnerKind.PlainWine, root);
                    CheckMode("2. Per-game Default inherits global Isolated", WinePrefixMode.Isolated, r2.EffectiveMode);

                    Lazydata.ParrotData = new ParrotData { DefaultWinePrefixMode = WinePrefixMode.Isolated };
                    var r3 = WinePrefixManager.Resolve("game3", WinePrefixMode.Shared, WinePrefixCompatibilityGroup.Standard, WineRunnerKind.PlainWine, root);
                    CheckMode("3. Per-game Shared overrides global Isolated", WinePrefixMode.Shared, r3.EffectiveMode);

                    Lazydata.ParrotData = new ParrotData { DefaultWinePrefixMode = WinePrefixMode.Shared };
                    var r4 = WinePrefixManager.Resolve("game4", WinePrefixMode.Isolated, WinePrefixCompatibilityGroup.Standard, WineRunnerKind.PlainWine, root);
                    CheckMode("4. Per-game Isolated overrides global Shared", WinePrefixMode.Isolated, r4.EffectiveMode);
                }

                // =========================================================
                // Shared paths
                // =========================================================
                {
                    var root = Path.Combine(tempRoot, "shared-paths");
                    Lazydata.ParrotData = new ParrotData { DefaultWinePrefixMode = WinePrefixMode.Shared };

                    var wineA = WinePrefixManager.Resolve("gameA", WinePrefixMode.Shared, WinePrefixCompatibilityGroup.Standard, WineRunnerKind.PlainWine, root);
                    var wineB = WinePrefixManager.Resolve("gameB", WinePrefixMode.Shared, WinePrefixCompatibilityGroup.Standard, WineRunnerKind.PlainWine, root);
                    CheckEq("1. Two plain-Wine games in shared standard mode share WINEPREFIX", wineA.WinePrefixPath, wineB.WinePrefixPath);

                    var protonA = WinePrefixManager.Resolve("gameA", WinePrefixMode.Shared, WinePrefixCompatibilityGroup.Standard, WineRunnerKind.Proton, root);
                    var protonB = WinePrefixManager.Resolve("gameB", WinePrefixMode.Shared, WinePrefixCompatibilityGroup.Standard, WineRunnerKind.Proton, root);
                    CheckEq("2. Two Proton games in shared standard mode share STEAM_COMPAT_DATA_PATH", protonA.SteamCompatDataPath, protonB.SteamCompatDataPath);

                    CheckEq("3. Proton's actual prefix resolves to <compat-data>/pfx", Path.Combine(protonA.SteamCompatDataPath, "pfx"), protonA.ActualPrefixPath);

                    CheckNotEq("4. Shared plain Wine and shared Proton never use the same physical directory", wineA.WinePrefixPath, protonA.SteamCompatDataPath);

                    var wineJp = WinePrefixManager.Resolve("gameJp", WinePrefixMode.Shared, WinePrefixCompatibilityGroup.Japanese, WineRunnerKind.PlainWine, root);
                    CheckNotEq("5. Japanese shared plain-Wine never uses the standard shared prefix", wineJp.WinePrefixPath, wineA.WinePrefixPath);
                    var protonJp = WinePrefixManager.Resolve("gameJp", WinePrefixMode.Shared, WinePrefixCompatibilityGroup.Japanese, WineRunnerKind.Proton, root);
                    CheckNotEq("5b. Japanese shared Proton never uses the standard shared compat-data", protonJp.SteamCompatDataPath, protonA.SteamCompatDataPath);

                    CheckEq("6. Japanese Proton actual prefix resolves to <japanese compat-data>/pfx", Path.Combine(protonJp.SteamCompatDataPath, "pfx"), protonJp.ActualPrefixPath);
                }

                // =========================================================
                // Isolated paths
                // =========================================================
                {
                    var root = Path.Combine(tempRoot, "isolated-paths");
                    Lazydata.ParrotData = new ParrotData { DefaultWinePrefixMode = WinePrefixMode.Shared };

                    var iso1 = WinePrefixManager.Resolve("Isolated-One", WinePrefixMode.Isolated, WinePrefixCompatibilityGroup.Standard, WineRunnerKind.PlainWine, root);
                    var iso2 = WinePrefixManager.Resolve("Isolated-Two", WinePrefixMode.Isolated, WinePrefixCompatibilityGroup.Standard, WineRunnerKind.PlainWine, root);
                    CheckNotEq("1. Two isolated profiles receive separate paths", iso1.WinePrefixPath, iso2.WinePrefixPath);

                    var isoProton = WinePrefixManager.Resolve("Isolated-Proton", WinePrefixMode.Isolated, WinePrefixCompatibilityGroup.Standard, WineRunnerKind.Proton, root);
                    CheckEq("2. Proton isolated actual prefix resolves to <isolated compat-data>/pfx", Path.Combine(isoProton.SteamCompatDataPath, "pfx"), isoProton.ActualPrefixPath);

                    // 3/4. Reset scoping - create marker files in isolated #1, isolated #2, and shared; reset isolated #1; assert only its own directory was touched.
                    Directory.CreateDirectory(iso1.WinePrefixPath);
                    Directory.CreateDirectory(iso2.WinePrefixPath);
                    var sharedRoot = WinePrefixManager.SharedRoot(root, WineRunnerKind.PlainWine);
                    Directory.CreateDirectory(sharedRoot);
                    File.WriteAllText(Path.Combine(iso1.WinePrefixPath, "marker.txt"), "iso1");
                    File.WriteAllText(Path.Combine(iso2.WinePrefixPath, "marker.txt"), "iso2");
                    File.WriteAllText(Path.Combine(sharedRoot, "marker.txt"), "shared");

                    var profile1 = new GameProfile { ProfileName = "Isolated-One" };
                    var resetResult = WinePrefixManager.ResetIsolated(profile1, WineRunnerKind.PlainWine, root);
                    Check("3. Resetting one isolated prefix succeeds", true, resetResult.Success);
                    Check("3b. Resetting one isolated prefix removes its own marker", false, File.Exists(Path.Combine(iso1.WinePrefixPath, "marker.txt")));
                    Check("3c. Resetting one isolated prefix cannot affect another isolated prefix", true, File.Exists(Path.Combine(iso2.WinePrefixPath, "marker.txt")));
                    Check("4. Resetting isolated cannot affect shared", true, File.Exists(Path.Combine(sharedRoot, "marker.txt")));
                }

                // =========================================================
                // Migration
                // =========================================================
                {
                    var root = Path.Combine(tempRoot, "migration");
                    Lazydata.ParrotData = new ParrotData { DefaultWinePrefixMode = WinePrefixMode.Shared };

                    // 1. <legacy>/system.reg present (plain-wine legacy layout).
                    var legacy1 = Path.Combine(root, "prefixes", "LegacyPlainWine");
                    Directory.CreateDirectory(legacy1);
                    File.WriteAllText(Path.Combine(legacy1, "system.reg"), "");
                    var m1 = WinePrefixManager.Resolve("LegacyPlainWine", null, WinePrefixCompatibilityGroup.Standard, WineRunnerKind.PlainWine, root);
                    CheckMode("1. Old profile with <legacy>/system.reg preserved as Isolated", WinePrefixMode.Isolated, m1.EffectiveMode);
                    Check("1b. Migration flag set", true, m1.MigratedFromLegacyIsolated);

                    // 2. <legacy>/pfx/system.reg present (proton legacy layout).
                    var legacy2 = Path.Combine(root, "prefixes", "LegacyProton");
                    Directory.CreateDirectory(Path.Combine(legacy2, "pfx"));
                    File.WriteAllText(Path.Combine(legacy2, "pfx", "system.reg"), "");
                    var m2 = WinePrefixManager.Resolve("LegacyProton", null, WinePrefixCompatibilityGroup.Standard, WineRunnerKind.Proton, root);
                    CheckMode("2. Old profile with <legacy>/pfx/system.reg preserved as Isolated", WinePrefixMode.Isolated, m2.EffectiveMode);

                    // 3. No initialized legacy prefix at all -> inherits the global default.
                    var m3 = WinePrefixManager.Resolve("BrandNewGame", null, WinePrefixCompatibilityGroup.Standard, WineRunnerKind.PlainWine, root);
                    CheckMode("3. Old profile with no initialized legacy prefix inherits the global default", WinePrefixMode.Shared, m3.EffectiveMode);

                    Lazydata.ParrotData = new ParrotData { DefaultWinePrefixMode = WinePrefixMode.Isolated };
                    var m3b = WinePrefixManager.Resolve("BrandNewGame2", null, WinePrefixCompatibilityGroup.Standard, WineRunnerKind.PlainWine, root);
                    CheckMode("3b. ...and correctly follows an Isolated global default too", WinePrefixMode.Isolated, m3b.EffectiveMode);

                    // 4. Existing legacy directories are never deleted or moved by Resolve() itself (pure/read-only).
                    Check("4. Existing legacy directory untouched by Resolve (still exists)", true, Directory.Exists(legacy1));
                    Check("4b. Existing legacy marker file untouched by Resolve", true, File.Exists(Path.Combine(legacy1, "system.reg")));

                    // 5. Switching an existing isolated game to Shared preserves the old isolated directory.
                    Lazydata.ParrotData = new ParrotData { DefaultWinePrefixMode = WinePrefixMode.Shared };
                    var switched = WinePrefixManager.Resolve("LegacyPlainWine", WinePrefixMode.Shared, WinePrefixCompatibilityGroup.Standard, WineRunnerKind.PlainWine, root);
                    CheckMode("5. Explicit Shared overrides even when a legacy isolated prefix exists", WinePrefixMode.Shared, switched.EffectiveMode);
                    Check("5b. Switching to shared preserves the old isolated directory on disk", true,
                        Directory.Exists(legacy1) && File.Exists(Path.Combine(legacy1, "system.reg")));
                }

                // =========================================================
                // Launch environment
                // =========================================================
                {
                    var root = Path.Combine(tempRoot, "launch-env");
                    Lazydata.ParrotData = new ParrotData { DefaultWinePrefixMode = WinePrefixMode.Shared };

                    var wineEnv = WinePrefixManager.Resolve("LaunchWine", WinePrefixMode.Shared, WinePrefixCompatibilityGroup.Standard, WineRunnerKind.PlainWine, root);
                    Check("1. Plain-Wine launch receives a non-empty WINEPREFIX", true, !string.IsNullOrEmpty(wineEnv.WinePrefixPath));
                    Check("1b. Plain-Wine launch's SteamCompatDataPath is unset (WINEPREFIX/STEAM_COMPAT_DATA_PATH never mixed up)", true, wineEnv.SteamCompatDataPath == null);

                    var protonEnv = WinePrefixManager.Resolve("LaunchProton", WinePrefixMode.Shared, WinePrefixCompatibilityGroup.Standard, WineRunnerKind.Proton, root);
                    Check("2. Proton launch receives a non-empty STEAM_COMPAT_DATA_PATH", true, !string.IsNullOrEmpty(protonEnv.SteamCompatDataPath));
                    Check("2b. Proton launch's WinePrefixPath is unset (never sent as WINEPREFIX by mistake)", true, protonEnv.WinePrefixPath == null);

                    // 3/4. Winetricks/registry setup must target the ACTUAL prefix - for
                    // plain Wine that's the same value as WinePrefixPath (what
                    // ProtonLauncher.InitializePrefix/EnsureJapaneseCodepage/InstallCompatLibraries
                    // all receive), for Proton it's <compat>/pfx, never the compat-data root.
                    CheckEq("3. Plain-Wine: actual prefix path used by Winetricks/registry == WINEPREFIX", wineEnv.WinePrefixPath, wineEnv.ActualPrefixPath);
                    CheckNotEq("4. Proton: actual prefix path used by Winetricks/registry != compat-data root (never sent STEAM_COMPAT_DATA_PATH by mistake)", protonEnv.ActualPrefixPath, protonEnv.SteamCompatDataPath);

                    // 5. Helper processes inherit the same environment: resolving the
                    // same profile+runner twice (as ProtonBridgePipe's eager-start read
                    // of ProtonRuntime.WinePrefix vs. WrapWithProton's own resolution
                    // effectively do) must be perfectly consistent/idempotent.
                    var wineEnvAgain = WinePrefixManager.Resolve("LaunchWine", WinePrefixMode.Shared, WinePrefixCompatibilityGroup.Standard, WineRunnerKind.PlainWine, root);
                    CheckEq("5. Helper processes resolve the identical WINEPREFIX as the main launch", wineEnv.WinePrefixPath, wineEnvAgain.WinePrefixPath);

                    // 6. Japanese registry changes cannot reach the standard shared prefix.
                    var japaneseWineEnv = WinePrefixManager.Resolve("LaunchWineJp", WinePrefixMode.Shared, WinePrefixCompatibilityGroup.Japanese, WineRunnerKind.PlainWine, root);
                    CheckNotEq("6. Japanese shared WINEPREFIX differs from the standard shared WINEPREFIX", japaneseWineEnv.WinePrefixPath, wineEnv.WinePrefixPath);
                }

                // =========================================================
                // Initialization locking
                // =========================================================
                {
                    // 1/2. Shared prefix initializes only once, concurrently-safe.
                    var key = Path.Combine(tempRoot, "init-lock-target");
                    Directory.CreateDirectory(key);
                    var initCount = 0;
                    var ready = false;
                    void SlowInit()
                    {
                        Thread.Sleep(50);
                        Interlocked.Increment(ref initCount);
                        ready = true;
                    }

                    var t1 = Task.Run(() => WinePrefixManager.InitializeOnceIfNeeded(key, () => ready, SlowInit));
                    var t2 = Task.Run(() => WinePrefixManager.InitializeOnceIfNeeded(key, () => ready, SlowInit));
                    Task.WaitAll(t1, t2);
                    Check("1. Shared prefix initializes only once (concurrent callers)", true, initCount == 1);
                    Check("2. Concurrent shared initialization is serialized (exactly one caller ran init)", true,
                        (t1.Result ? 1 : 0) + (t2.Result ? 1 : 0) == 1);

                    var t3 = WinePrefixManager.InitializeOnceIfNeeded(key, () => ready, SlowInit);
                    Check("2b. A third call after readiness is a no-op", false, t3);

                    // 3. Failed initialization does not get marked ready.
                    var failKey = Path.Combine(tempRoot, "init-lock-fail-target");
                    Directory.CreateDirectory(failKey);
                    var failReady = false;
                    var attempts = 0;
                    void FailingInit()
                    {
                        attempts++;
                        throw new InvalidOperationException("simulated init failure");
                    }
                    try { WinePrefixManager.InitializeOnceIfNeeded(failKey, () => failReady, FailingInit); }
                    catch (InvalidOperationException) { /* expected */ }
                    Check("3. Failed initialization does not get marked ready", false, failReady);
                    try { WinePrefixManager.InitializeOnceIfNeeded(failKey, () => failReady, FailingInit); }
                    catch (InvalidOperationException) { /* expected */ }
                    Check("3b. A failed init is retried (not permanently skipped)", true, attempts == 2);
                }

                // =========================================================
                // Reset - runner-specific recreation
                // =========================================================
                {
                    var root = Path.Combine(tempRoot, "reset-recreate");
                    Lazydata.ParrotData = new ParrotData { DefaultWinePrefixMode = WinePrefixMode.Shared };

                    // Plain Wine: reset recreates the (empty) prefix directory itself.
                    var wineShared = WinePrefixManager.SharedRoot(root, WineRunnerKind.PlainWine);
                    Directory.CreateDirectory(wineShared);
                    File.WriteAllText(Path.Combine(wineShared, "system.reg"), "");
                    var wineReset = WinePrefixManager.ResetShared(WineRunnerKind.PlainWine, WinePrefixCompatibilityGroup.Standard, root);
                    Check("5a. Plain-Wine reset succeeds", true, wineReset.Success);
                    Check("5b. Plain-Wine reset recreates the prefix directory (empty, ready for wineboot again)", true,
                        Directory.Exists(wineShared) && !File.Exists(Path.Combine(wineShared, "system.reg")));

                    // Proton: reset recreates the compat-data root but NOT "pfx" -
                    // Proton creates that itself on next launch (see ProtonLauncher docs).
                    var protonShared = WinePrefixManager.SharedRoot(root, WineRunnerKind.Proton);
                    Directory.CreateDirectory(Path.Combine(protonShared, "pfx"));
                    File.WriteAllText(Path.Combine(protonShared, "pfx", "system.reg"), "");
                    var protonReset = WinePrefixManager.ResetShared(WineRunnerKind.Proton, WinePrefixCompatibilityGroup.Standard, root);
                    Check("5c. Proton reset succeeds", true, protonReset.Success);
                    Check("5d. Proton reset recreates the compat-data root", true, Directory.Exists(protonShared));
                    Check("5e. Proton reset does NOT pre-create 'pfx' (Proton's own first-run does that)", false, Directory.Exists(Path.Combine(protonShared, "pfx")));
                }

                // =========================================================
                // Logging
                // =========================================================
                {
                    var root = Path.Combine(tempRoot, "logging");
                    Lazydata.ParrotData = new ParrotData { DefaultWinePrefixMode = WinePrefixMode.Shared };
                    var env = WinePrefixManager.Resolve("LogTest", WinePrefixMode.Default, WinePrefixCompatibilityGroup.Japanese, WineRunnerKind.Proton, root);
                    var block = env.ToLogBlock("LogTest");
                    foreach (var expectedField in new[]
                             {
                                 "[WinePrefix]", "Profile: LogTest", "ConfiguredMode:", "EffectiveMode:",
                                 "CompatibilityGroup: Japanese", "Runner: Proton", "SteamCompatDataPath:",
                                 "ActualPrefixPath:", "Managed:", "RequiresInitialization:"
                             })
                    {
                        Check($"Log block contains '{expectedField}'", true, block.Contains(expectedField, StringComparison.Ordinal));
                    }
                }
            }
            finally
            {
                Lazydata.ParrotData = originalParrotData;
                try { Directory.Delete(tempRoot, true); } catch { /* best effort cleanup */ }
            }

            Console.WriteLine($"\nWine prefix test: {cases} cases, {failures} failures");
            return failures == 0 ? 0 : 1;
        }

        private static string NormalizePath(string path) =>
            string.IsNullOrEmpty(path) ? path : path.TrimEnd('/', '\\');
    }
}
