using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using TeknoParrotUi.Common.GameLaunch;

namespace TeknoParrotUi.Common.Proton
{
    /// <summary>
    /// Per-game Wine/Proton prefix preference. Serialized as nullable on
    /// <see cref="GameProfile.WinePrefixMode"/> so XmlSerializer can tell an
    /// old profile (element absent -> null after deserialize) apart from one
    /// that explicitly saved "use the global default" (element present,
    /// value <see cref="Default"/>) - see <see cref="WinePrefixManager"/>'s
    /// migration handling, which only applies to the null case.
    /// </summary>
    public enum WinePrefixMode
    {
        /// <summary>Inherit <see cref="ParrotData.DefaultWinePrefixMode"/>.</summary>
        Default,
        /// <summary>Explicitly use the shared environment for this game.</summary>
        Shared,
        /// <summary>Explicitly use a dedicated environment for this game.</summary>
        Isolated
    }

    /// <summary>Which launcher will run the game - determines WINEPREFIX vs STEAM_COMPAT_DATA_PATH semantics.</summary>
    public enum WineRunnerKind
    {
        PlainWine,
        Proton
    }

    /// <summary>
    /// Groups games whose prefix-wide registry/font mutations are incompatible
    /// with the standard shared environment (see <see cref="ProtonLauncher.EnsureJapaneseCodepage"/>) -
    /// each group gets its own shared prefix so those mutations can never leak
    /// into games that don't need them.
    /// </summary>
    public enum WinePrefixCompatibilityGroup
    {
        Standard,
        Japanese
    }

    /// <summary>
    /// Structured result of resolving a game's Wine/Proton environment - see
    /// <see cref="WinePrefixManager.Resolve"/>. Deliberately never a single
    /// ambiguous path string: plain Wine and Proton use different environment
    /// variables (WINEPREFIX vs STEAM_COMPAT_DATA_PATH) pointing at different
    /// directory semantics, and callers that need the actual registry/drive_c
    /// (Winetricks, reg commands, reset) must use <see cref="ActualPrefixPath"/>
    /// regardless of which runner is in play.
    /// </summary>
    public sealed class ResolvedWineEnvironment
    {
        /// <summary>
        /// What's effectively configured for display/logging purposes - never
        /// null (a missing/legacy profile value is folded to <see cref="WinePrefixMode.Default"/>
        /// here; the null-vs-explicit-Default distinction that drives the
        /// legacy-migration heuristic is internal to <see cref="WinePrefixManager.Resolve"/>).
        /// </summary>
        public WinePrefixMode ConfiguredMode { get; init; }

        /// <summary>The mode actually used to pick paths below, after resolving <see cref="WinePrefixMode.Default"/>/legacy migration.</summary>
        public WinePrefixMode EffectiveMode { get; init; }

        public WinePrefixCompatibilityGroup CompatibilityGroup { get; init; }

        public WineRunnerKind RunnerKind { get; init; }

        /// <summary>Set only for <see cref="WineRunnerKind.PlainWine"/> - the value to put in WINEPREFIX.</summary>
        public string WinePrefixPath { get; init; }

        /// <summary>Set only for <see cref="WineRunnerKind.Proton"/> - the value to put in STEAM_COMPAT_DATA_PATH.</summary>
        public string SteamCompatDataPath { get; init; }

        /// <summary>
        /// The directory containing drive_c, system.reg, user.reg, etc. - for
        /// Proton this is <c>SteamCompatDataPath/pfx</c>, NOT the compat-data
        /// root itself. Always use this (never <see cref="SteamCompatDataPath"/>)
        /// for Winetricks, `wine reg add`, wineserver control, and readiness markers.
        /// </summary>
        public string ActualPrefixPath { get; init; } = string.Empty;

        /// <summary>
        /// True when this directory is one TeknoParrotUI created/owns (shared
        /// roots, or a legacy per-profile isolated directory) - custom
        /// arbitrary prefix paths aren't supported yet (see class docs), so
        /// this is always true today; kept for forward compatibility so reset
        /// logic never has to guess.
        /// </summary>
        public bool IsManagedByTeknoParrotUi { get; init; } = true;

        /// <summary>True when <see cref="ActualPrefixPath"/> hasn't been booted yet (no system.reg) - the runner-specific first-run flow still needs to run.</summary>
        public bool RequiresInitialization { get; init; }

        /// <summary>
        /// True when this resolution came from the existing-user migration
        /// heuristic (profile had no serialized <see cref="GameProfile.WinePrefixMode"/>
        /// at all, and an already-initialized legacy prefix was found) rather
        /// than an explicit or default-inherited choice.
        /// </summary>
        public bool MigratedFromLegacyIsolated { get; init; }

        /// <summary>
        /// Formats the standard "[WinePrefix] ..." diagnostic block - see
        /// <see cref="WinePrefixManager.Resolve"/>'s callers for when this is logged.
        /// </summary>
        public string ToLogBlock(string profileLabel)
        {
            var log = new System.Text.StringBuilder();
            log.AppendLine("[WinePrefix]");
            log.AppendLine($"Profile: {profileLabel}");
            log.AppendLine($"ConfiguredMode: {ConfiguredMode}");
            log.AppendLine($"EffectiveMode: {EffectiveMode}{(MigratedFromLegacyIsolated ? " (migrated from legacy isolated prefix)" : "")}");
            log.AppendLine($"CompatibilityGroup: {CompatibilityGroup}");
            log.AppendLine($"Runner: {RunnerKind}");
            if (RunnerKind == WineRunnerKind.PlainWine)
                log.AppendLine($"WinePrefix: {WinePrefixPath}");
            else
                log.AppendLine($"SteamCompatDataPath: {SteamCompatDataPath}");
            log.AppendLine($"ActualPrefixPath: {ActualPrefixPath}");
            log.AppendLine($"Managed: {IsManagedByTeknoParrotUi}");
            log.AppendLine($"RequiresInitialization: {RequiresInitialization}");
            return log.ToString();
        }
    }

    /// <summary>Outcome of a <see cref="WinePrefixManager"/> reset operation.</summary>
    public sealed class PrefixResetResult
    {
        public bool Success { get; init; }
        public string Message { get; init; } = "";
    }

    /// <summary>
    /// Single source of truth for every Wine/Proton prefix PATH decision -
    /// shared vs isolated, plain-Wine vs Proton semantics, and the Japanese
    /// compatibility group. Every launcher, helper, Winetricks call, registry
    /// command, setup page and reset action must go through
    /// <see cref="Resolve"/> instead of constructing a prefix path itself, so
    /// they can never disagree about where a game's environment lives.
    ///
    /// Shared layout:
    ///   &lt;data&gt;/prefixes/shared/wine               (plain Wine, standard)
    ///   &lt;data&gt;/prefixes/shared/wine-japanese      (plain Wine, Japanese)
    ///   &lt;data&gt;/prefixes/shared/proton              (Proton compat-data, standard)
    ///   &lt;data&gt;/prefixes/shared/proton-japanese     (Proton compat-data, Japanese)
    ///   &lt;data&gt;/prefixes/shared/proton/pfx           (actual shared Proton Wine prefix)
    ///
    /// Isolated layout (legacy, preserved verbatim - same directory name
    /// format plain Wine and Proton already used per-profile before this
    /// feature existed):
    ///   &lt;data&gt;/prefixes/&lt;profile&gt;                  (plain Wine: the prefix itself)
    ///   &lt;data&gt;/prefixes/&lt;profile&gt;                  (Proton: compat-data root)
    ///   &lt;data&gt;/prefixes/&lt;profile&gt;/pfx              (Proton: actual prefix)
    /// </summary>
    public static class WinePrefixManager
    {
        public static string DefaultDataRoot => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TeknoParrotUI");

        /// <summary>
        /// Resolves the full environment for <paramref name="profile"/> running
        /// under <paramref name="runnerKind"/>. Extracts the profile's stable
        /// identifier, configured mode and compatibility group and delegates
        /// to the profile-agnostic <see cref="Resolve(string,WinePrefixMode?,WinePrefixCompatibilityGroup,WineRunnerKind,string)"/>
        /// overload, which callers that need a PREVIEW (e.g. the per-game
        /// Advanced settings page showing what a not-yet-saved combo
        /// selection would resolve to) should use directly instead of
        /// mutating a live <see cref="GameProfile"/> just to compute one.
        /// </summary>
        public static ResolvedWineEnvironment Resolve(GameProfile profile, WineRunnerKind runnerKind, string dataRoot = null)
        {
            var group = GameLaunchArguments.RequiresJapaneseLocale(profile)
                ? WinePrefixCompatibilityGroup.Japanese
                : WinePrefixCompatibilityGroup.Standard;
            return Resolve(ProfileIdentifier(profile), profile?.WinePrefixMode, group, runnerKind, dataRoot);
        }

        /// <summary>
        /// Profile-agnostic core resolution - pure/read-only (only does
        /// File.Exists checks for the legacy-migration heuristic and the
        /// RequiresInitialization flag) - never creates directories or mutates
        /// anything; callers that need the directories to exist call
        /// <see cref="EnsureDirectories"/> separately.
        /// </summary>
        /// <param name="profileIdentifier">Stable per-game identifier (see <see cref="ProfileIdentifier"/>) - used only for the legacy isolated path.</param>
        /// <param name="configuredMode">The game's raw serialized <see cref="GameProfile.WinePrefixMode"/> - null for a legacy profile with no saved preference at all (triggers the migration heuristic), as opposed to an explicit <see cref="WinePrefixMode.Default"/>.</param>
        public static ResolvedWineEnvironment Resolve(
            string profileIdentifier,
            WinePrefixMode? configuredMode,
            WinePrefixCompatibilityGroup group,
            WineRunnerKind runnerKind,
            string dataRoot = null)
        {
            dataRoot ??= DefaultDataRoot;
            var globalDefault = Lazydata.ParrotData?.DefaultWinePrefixMode ?? WinePrefixMode.Shared;
            var legacyPath = Path.Combine(dataRoot, "prefixes", profileIdentifier);
            var migrated = false;

            WinePrefixMode effective;
            if (configuredMode.HasValue)
            {
                effective = configuredMode.Value == WinePrefixMode.Default ? globalDefault : configuredMode.Value;
            }
            else if (IsLegacyPrefixInitialized(legacyPath))
            {
                // Existing-user migration: an old profile with no serialized
                // preference but an already-booted legacy prefix must keep
                // working exactly as it did - never silently move it to the
                // shared environment. See class docs / GameProfile.WinePrefixMode.
                effective = WinePrefixMode.Isolated;
                migrated = true;
            }
            else
            {
                effective = globalDefault;
            }

            string winePrefixPath = null;
            string compatDataPath = null;
            var basePath = effective == WinePrefixMode.Isolated ? legacyPath : SharedRoot(dataRoot, runnerKind, group);

            string actualPrefix;
            if (runnerKind == WineRunnerKind.PlainWine)
            {
                // TP_WINEPREFIX is a raw debug/advanced escape hatch that
                // overrides path SELECTION entirely (pre-dates prefix modes) -
                // still honored, but centralized here rather than duplicated
                // at every call site.
                var envOverride = Environment.GetEnvironmentVariable("TP_WINEPREFIX");
                winePrefixPath = !string.IsNullOrEmpty(envOverride) ? envOverride : basePath;
                actualPrefix = winePrefixPath;
            }
            else
            {
                compatDataPath = basePath;
                actualPrefix = Path.Combine(basePath, "pfx");
            }

            return new ResolvedWineEnvironment
            {
                ConfiguredMode = configuredMode ?? WinePrefixMode.Default,
                EffectiveMode = effective,
                CompatibilityGroup = group,
                RunnerKind = runnerKind,
                WinePrefixPath = winePrefixPath,
                SteamCompatDataPath = compatDataPath,
                ActualPrefixPath = actualPrefix,
                IsManagedByTeknoParrotUi = true,
                RequiresInitialization = !File.Exists(Path.Combine(actualPrefix, "system.reg")),
                MigratedFromLegacyIsolated = migrated
            };
        }

        /// <summary>
        /// Creates the directory <see cref="ResolvedWineEnvironment.WinePrefixPath"/>/<see cref="ResolvedWineEnvironment.SteamCompatDataPath"/>
        /// needs to exist before launch (never the Proton "pfx" subdirectory -
        /// Proton creates that itself). Gated the same as every other
        /// prefix-mutating entry point (see <see cref="ProtonPackageManager.ThrowIfUnsupportedHost"/>) -
        /// creating either a shared or an isolated prefix directory is still
        /// "initializing a Wine/Proton environment" and must not happen on an
        /// unsupported host, even though callers (<see cref="ProtonLauncher.WrapWithProton"/>/
        /// <see cref="ProtonLauncher.PrepareSession"/>) already gate first too.
        /// The optional parameter exists purely for tests to simulate an
        /// unsupported host without needing to run on one.
        /// </summary>
        public static void EnsureDirectories(ResolvedWineEnvironment env, System.Runtime.InteropServices.Architecture? hostArchitecture = null)
        {
            ProtonPackageManager.ThrowIfUnsupportedHost(hostArchitecture);
            Directory.CreateDirectory(env.RunnerKind == WineRunnerKind.PlainWine ? env.WinePrefixPath : env.SteamCompatDataPath);
        }

        /// <summary>Stable identifier used for both legacy isolated paths and reset targeting - matches the profile-name logic every prefix path has always used.</summary>
        public static string ProfileIdentifier(GameProfile profile) =>
            !string.IsNullOrEmpty(profile?.ProfileName)
                ? profile.ProfileName
                : Path.GetFileNameWithoutExtension(profile?.FileName ?? "default");

        /// <summary>
        /// Legacy per-profile directory - identical format to what
        /// <c>ProtonLauncher.ResolvePrefix</c>/<c>ResolveCompatDataPath</c> always
        /// used, deliberately preserved so existing prefixes keep resolving to
        /// the same physical location regardless of runner kind.
        /// </summary>
        public static string LegacyIsolatedPath(GameProfile profile, string dataRoot = null) =>
            Path.Combine(dataRoot ?? DefaultDataRoot, "prefixes", ProfileIdentifier(profile));

        /// <summary>Shared environment root for a runner kind + compatibility group.</summary>
        public static string SharedRoot(string dataRoot, WineRunnerKind runnerKind, WinePrefixCompatibilityGroup group = WinePrefixCompatibilityGroup.Standard)
        {
            var runnerFolder = runnerKind == WineRunnerKind.PlainWine ? "wine" : "proton";
            var suffix = group == WinePrefixCompatibilityGroup.Japanese ? "-japanese" : "";
            return Path.Combine(dataRoot, "prefixes", "shared", runnerFolder + suffix);
        }

        /// <summary>
        /// True when either legacy marker exists - checked regardless of
        /// runner kind since an old profile might have run under either
        /// (both historically used the exact same directory name), per the
        /// migration rule: "if either &lt;legacy&gt;/system.reg or
        /// &lt;legacy&gt;/pfx/system.reg exists, treat the game as explicitly Isolated".
        /// </summary>
        public static bool IsLegacyPrefixInitialized(string legacyPath) =>
            File.Exists(Path.Combine(legacyPath, "system.reg")) ||
            File.Exists(Path.Combine(legacyPath, "pfx", "system.reg"));

        // ---------------------------------------------------------------
        // Initialization locking - "must not run twice concurrently", but
        // must NOT be held for the duration of normal gameplay (only around
        // the actual mutation: first boot, winetricks, registry init, reset).
        // ---------------------------------------------------------------

        private static readonly ConcurrentDictionary<string, SemaphoreSlim> Locks = new(StringComparer.Ordinal);

        /// <summary>Per-canonical-prefix-path lock - callers must Wait()/Release() (or use <see cref="InitializeOnceIfNeeded"/>) around any operation that mutates the prefix.</summary>
        public static SemaphoreSlim GetLock(string prefixPath) => Locks.GetOrAdd(NormalizeKey(prefixPath), _ => new SemaphoreSlim(1, 1));

        private static string NormalizeKey(string path) =>
            Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).ToLowerInvariant();

        /// <summary>
        /// Runs <paramref name="initialize"/> under the prefix's lock, but only
        /// if <paramref name="isReady"/> still says no after the lock is
        /// acquired (a concurrent caller may have just finished - double-checked
        /// locking). Returns true when initialization actually ran. If
        /// <paramref name="initialize"/> throws, the exception propagates and
        /// nothing is considered "ready" (callers derive readiness from a real
        /// marker file <paramref name="isReady"/> checks, which a half-finished
        /// init never wrote) - so a failed run is safely retried later.
        /// </summary>
        public static bool InitializeOnceIfNeeded(string prefixPath, Func<bool> isReady, Action initialize)
        {
            if (isReady())
                return false;

            var sem = GetLock(prefixPath);
            sem.Wait();
            try
            {
                if (isReady())
                    return false;
                initialize();
                return true;
            }
            finally
            {
                sem.Release();
            }
        }

        // ---------------------------------------------------------------
        // Reset - correctly scoped: only ever deletes the ONE resolved
        // directory (shared root for a specific runner+group, or one game's
        // legacy isolated directory), never anything else.
        // ---------------------------------------------------------------

        public static PrefixResetResult ResetShared(WineRunnerKind runnerKind, WinePrefixCompatibilityGroup group = WinePrefixCompatibilityGroup.Standard, string dataRoot = null) =>
            ResetDirectory(SharedRoot(dataRoot ?? DefaultDataRoot, runnerKind, group), runnerKind);

        public static PrefixResetResult ResetIsolated(GameProfile profile, WineRunnerKind runnerKind, string dataRoot = null) =>
            ResetDirectory(LegacyIsolatedPath(profile, dataRoot ?? DefaultDataRoot), runnerKind);

        private static PrefixResetResult ResetDirectory(string basePath, WineRunnerKind runnerKind)
        {
            var actualPrefix = runnerKind == WineRunnerKind.PlainWine ? basePath : Path.Combine(basePath, "pfx");
            var sem = GetLock(basePath);
            if (!sem.Wait(0))
                return new PrefixResetResult { Success = false, Message = "This prefix is currently being initialized by another operation - try again shortly." };

            try
            {
                if (IsPrefixActive(actualPrefix))
                    return new PrefixResetResult { Success = false, Message = $"This prefix is currently in use by a running game ({actualPrefix}). Close it before resetting." };

                StopWineServer(actualPrefix);

                if (IsPrefixActive(actualPrefix))
                    return new PrefixResetResult { Success = false, Message = "Could not stop all processes using this prefix - reset aborted." };

                if (Directory.Exists(basePath))
                    Directory.Delete(basePath, recursive: true);

                // Recreate only the base directory - the runner-specific first-run
                // flow (wineboot/Proton's own init) runs again at next launch, same
                // as a genuinely new prefix. Do not pre-create "pfx" for Proton -
                // Proton creates that itself.
                Directory.CreateDirectory(basePath);
                return new PrefixResetResult { Success = true, Message = $"Prefix reset: {basePath}" };
            }
            catch (Exception ex)
            {
                return new PrefixResetResult { Success = false, Message = $"Reset failed: {ex.Message}" };
            }
            finally
            {
                sem.Release();
            }
        }

        /// <summary>True when any process on the system currently has WINEPREFIX exactly equal to <paramref name="actualPrefixPath"/> - scoped per-prefix, never "any wine process anywhere".</summary>
        private static bool IsPrefixActive(string actualPrefixPath)
        {
            if (!OperatingSystem.IsLinux() || !Directory.Exists("/proc"))
                return false;

            string target;
            try { target = Path.GetFullPath(actualPrefixPath).TrimEnd('/'); }
            catch { return false; }

            foreach (var procDir in Directory.EnumerateDirectories("/proc"))
            {
                if (!int.TryParse(Path.GetFileName(procDir), out _))
                    continue;
                try
                {
                    var environPath = Path.Combine(procDir, "environ");
                    if (!File.Exists(environPath))
                        continue;
                    var environ = File.ReadAllText(environPath);
                    var prefixEntry = environ.Split('\0').FirstOrDefault(e => e.StartsWith("WINEPREFIX=", StringComparison.Ordinal));
                    if (prefixEntry == null)
                        continue;
                    var prefixValue = prefixEntry.Substring("WINEPREFIX=".Length);
                    if (string.Equals(Path.GetFullPath(prefixValue).TrimEnd('/'), target, StringComparison.Ordinal))
                        return true;
                }
                catch
                {
                    // process exited mid-scan / unreadable - not ours
                }
            }
            return false;
        }

        /// <summary>Best-effort `wineserver -k` scoped to one prefix (never kills every Wine process on the machine).</summary>
        private static void StopWineServer(string actualPrefixPath)
        {
            try
            {
                string wineserver = null;
                foreach (var candidate in new[] { "/usr/bin/wineserver", "/usr/local/bin/wineserver" })
                {
                    if (File.Exists(candidate))
                    {
                        wineserver = candidate;
                        break;
                    }
                }
                if (wineserver == null || !Directory.Exists(actualPrefixPath))
                    return;

                var psi = new ProcessStartInfo(wineserver, "-k")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                psi.Environment["WINEPREFIX"] = actualPrefixPath;
                using var proc = Process.Start(psi);
                proc?.WaitForExit(10_000);
            }
            catch
            {
                // best effort - IsPrefixActive re-check afterward is the real safety net
            }
        }
    }
}
