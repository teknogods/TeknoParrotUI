using System;
using System.Diagnostics;
using TeknoParrotUi.Common.GameLaunch;

namespace TeknoParrotUi.Common.Proton
{
    /// <summary>Thrown when Gamescope was explicitly requested (env-forced or explicit per-game AutomaticFit) but genuinely cannot be used.</summary>
    public sealed class GamescopeUnavailableException : Exception
    {
        public GamescopeUnavailableException(string message) : base(message) { }
    }

    /// <summary>
    /// Orchestrates the automatic Gamescope fullscreen-scaling PREFLIGHT
    /// decision: pulls together <see cref="GamescopeLaunchPolicy"/> (should we
    /// wrap at all), <see cref="GamescopeLocator"/> (can Gamescope actually
    /// start), <see cref="LinuxDisplayResolver"/> (what output resolution to
    /// use) and <see cref="GamescopeCommandBuilder"/> (the actual
    /// ProcessStartInfo rewrite) into a <see cref="GameProcessLaunchPlan"/>
    /// that <see cref="GameProcessLauncher"/> then executes (with its own,
    /// separate Process.Start-level fallback rules).
    ///
    /// Kept deliberately thin/orchestration-only - no policy, discovery, or
    /// command-building logic lives here, so each concern stays independently
    /// unit-testable (see the task's "Architecture" requirements). This class
    /// itself never calls <see cref="Process.Start()"/> - see
    /// <see cref="GameProcessLauncher"/> for that.
    /// </summary>
    public static class GamescopeLauncher
    {
        /// <summary>
        /// Builds the launch plan for <paramref name="original"/> - decides
        /// whether Gamescope should be attempted, and if so, produces the
        /// wrapped ProcessStartInfo, WITHOUT starting any process. See
        /// <see cref="GameProcessLauncher.Launch"/> for how the plan is
        /// executed (including the actual Process.Start-level fallback
        /// rules this preflight decision alone can't cover).
        /// </summary>
        public static GameProcessLaunchPlan BuildLaunchPlan(ProcessStartInfo original, GameProfile profile, Action<string> log = null)
        {
            log ??= ProtonLog.Write;

            if (!OperatingSystem.IsLinux())
                return new GameProcessLaunchPlan { DirectStartInfo = original }; // Windows never touches this feature at all.

            var globalMode = Lazydata.ParrotData?.FullscreenScalingMode ?? LinuxFullscreenScalingMode.Disabled;
            var gameMode = profile?.FullscreenScalingMode ?? LinuxFullscreenScalingMode.Default;
            var envNoGamescope = GamescopeEnvironment.NoGamescopeRequested;
            var envForceGamescope = GamescopeEnvironment.ForceGamescopeRequested;
            var isExternalEmulator = profile != null && ExternalEmulatorLauncher.IsExternalEmulator(profile);
            var alreadyInside = GamescopeEnvironment.IsAlreadyInsideGamescope();
            var allowNested = GamescopeEnvironment.AllowNestedOverrideRequested;

            var decision = GamescopeLaunchPolicy.Resolve(globalMode, gameMode, envNoGamescope, envForceGamescope,
                isExternalEmulator, alreadyInside, allowNested);

            if (!decision.ShouldAttemptWrap)
            {
                log(new GamescopeLaunchConfiguration
                {
                    ConfiguredGlobalMode = globalMode,
                    ConfiguredGameMode = gameMode,
                    EffectiveMode = decision.EffectiveMode,
                    ForcedByEnvironment = decision.ForcedByEnvironment,
                    DisabledByEnvironment = decision.DisabledByEnvironment,
                    IsExternalEmulator = isExternalEmulator,
                    AlreadyInsideGamescope = alreadyInside,
                    Wrapped = false,
                    Reason = decision.SkipDescription
                }.ToLogBlock());
                return new GameProcessLaunchPlan { DirectStartInfo = original };
            }

            bool explicitlyForced = decision.ForcedByEnvironment || gameMode == LinuxFullscreenScalingMode.AutomaticFit;
            var availability = GamescopeLocator.Locate();

            if (!availability.IsAvailable)
            {
                var message = $"Gamescope unavailable ({availability.Reason}): {availability.Message}";
                log(new GamescopeLaunchConfiguration
                {
                    ConfiguredGlobalMode = globalMode,
                    ConfiguredGameMode = gameMode,
                    EffectiveMode = decision.EffectiveMode,
                    ForcedByEnvironment = decision.ForcedByEnvironment,
                    IsExternalEmulator = isExternalEmulator,
                    AlreadyInsideGamescope = alreadyInside,
                    Wrapped = false,
                    Reason = explicitlyForced ? message : $"{message} Using direct-launch fallback."
                }.ToLogBlock());

                return new GameProcessLaunchPlan
                {
                    DirectStartInfo = original,
                    ScalingRequested = true,
                    ScalingForced = explicitlyForced,
                    ScalingUnavailableReason = explicitlyForced ? message : null
                };
            }

            var display = LinuxDisplayResolver.Resolve(w => log($"[FullscreenScaling] {w}"));
            if (!display.IsValid)
            {
                var message = $"Could not resolve a monitor output resolution: {display.FailureReason}";
                log(new GamescopeLaunchConfiguration
                {
                    ConfiguredGlobalMode = globalMode,
                    ConfiguredGameMode = gameMode,
                    EffectiveMode = decision.EffectiveMode,
                    ForcedByEnvironment = decision.ForcedByEnvironment,
                    IsExternalEmulator = isExternalEmulator,
                    AlreadyInsideGamescope = alreadyInside,
                    GamescopeExecutable = availability.ExecutablePath,
                    GamescopeVersion = availability.Version,
                    Wrapped = false,
                    Reason = explicitlyForced ? message : $"{message} Using direct-launch fallback."
                }.ToLogBlock());

                return new GameProcessLaunchPlan
                {
                    DirectStartInfo = original,
                    ScalingRequested = true,
                    ScalingForced = explicitlyForced,
                    ScalingUnavailableReason = explicitlyForced ? message : null
                };
            }

            var backendDecision = GamescopeBackendPolicy.Resolve();
            log(GamescopeBackendPolicy.ToLogBlock(
                backendDecision,
                Environment.GetEnvironmentVariable(GamescopeBackendPolicy.BackendEnvVar),
                Environment.GetEnvironmentVariable("XDG_SESSION_TYPE"),
                Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"),
                Environment.GetEnvironmentVariable("DISPLAY")));

            var monitorCount = LinuxDisplayResolver.DetectMonitorCount();
            var placement = MonitorPlacementPolicy.Describe(monitorCount);
            log($"[MonitorPlacement] Mechanism: {placement.Mechanism}. Guaranteed: {(placement.PlacementGuaranteed ? "true" : "false")}. {placement.Description}");

            var wrapped = GamescopeCommandBuilder.Wrap(original, availability.ExecutablePath, display.Width, display.Height, backendDecision.Resolved);

            log(new GamescopeLaunchConfiguration
            {
                ConfiguredGlobalMode = globalMode,
                ConfiguredGameMode = gameMode,
                EffectiveMode = decision.EffectiveMode,
                ForcedByEnvironment = decision.ForcedByEnvironment,
                IsExternalEmulator = isExternalEmulator,
                AlreadyInsideGamescope = alreadyInside,
                GamescopeExecutable = availability.ExecutablePath,
                GamescopeVersion = availability.Version,
                OutputWidth = display.Width,
                OutputHeight = display.Height,
                DisplaySource = display.Source,
                TargetMonitorIdentifier = display.Identifier,
                TargetMonitorX = display.X,
                TargetMonitorY = display.Y,
                MonitorSelectionReason = display.SelectionReason,
                BackendResolved = backendDecision.Resolved,
                Wrapped = true
            }.ToLogBlock());

            return new GameProcessLaunchPlan
            {
                DirectStartInfo = original,
                GamescopeStartInfo = wrapped,
                ScalingRequested = true,
                ScalingForced = explicitlyForced
            };
        }

        /// <summary>
        /// Compatibility convenience wrapper around <see cref="BuildLaunchPlan"/>
        /// for callers/tests that just want "what ProcessStartInfo should run"
        /// without needing the full Process.Start-level fallback machinery in
        /// <see cref="GameProcessLauncher"/> - returns the exact SAME
        /// <paramref name="original"/> instance (never a clone) whenever
        /// wrapping doesn't happen.
        /// </summary>
        /// <exception cref="GamescopeUnavailableException">
        /// Only thrown when Gamescope was explicitly requested (TP_GAMESCOPE=1
        /// or an explicit per-game AutomaticFit setting) and preflight
        /// (policy/discovery/display) genuinely failed - automatic/inherited
        /// requests silently fall back instead.
        /// </exception>
        public static ProcessStartInfo Wrap(ProcessStartInfo original, GameProfile profile, Action<string> log = null)
        {
            var plan = BuildLaunchPlan(original, profile, log);
            if (plan.GamescopeStartInfo != null)
                return plan.GamescopeStartInfo;
            if (plan.ScalingForced && plan.ScalingUnavailableReason != null)
                throw new GamescopeUnavailableException(plan.ScalingUnavailableReason);
            return plan.DirectStartInfo;
        }
    }
}
