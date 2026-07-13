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
    /// Orchestrates the automatic Gamescope fullscreen-scaling wrapper: pulls
    /// together <see cref="GamescopeLaunchPolicy"/> (should we wrap at all),
    /// <see cref="GamescopeLocator"/> (can Gamescope actually start),
    /// <see cref="LinuxDisplayResolver"/> (what output resolution to use)
    /// and <see cref="GamescopeCommandBuilder"/> (the actual ProcessStartInfo
    /// rewrite) into the single entry point <see cref="ProtonLauncher"/>
    /// calls at the end of building a game's launch command.
    ///
    /// Kept deliberately thin/orchestration-only - no policy, discovery, or
    /// command-building logic lives here, so each concern stays independently
    /// unit-testable (see the task's "Architecture" requirements).
    /// </summary>
    public static class GamescopeLauncher
    {
        /// <summary>
        /// Wraps <paramref name="original"/> with Gamescope when policy,
        /// Gamescope availability and display resolution all allow it.
        /// Returns the exact SAME <paramref name="original"/> instance
        /// (never a clone) whenever wrapping doesn't happen - the Disabled/
        /// skipped path must be bit-for-bit identical to the pre-existing
        /// direct launch.
        /// </summary>
        /// <exception cref="GamescopeUnavailableException">
        /// Only thrown when Gamescope was explicitly requested (TP_GAMESCOPE=1
        /// or an explicit per-game AutomaticFit setting) and genuinely can't
        /// be used - automatic/inherited requests silently fall back instead.
        /// </exception>
        public static ProcessStartInfo Wrap(ProcessStartInfo original, GameProfile profile, Action<string> log = null)
        {
            log ??= ProtonLog.Write;

            if (!OperatingSystem.IsLinux())
                return original; // Windows never touches this feature at all.

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
                return original;
            }

            var availability = GamescopeLocator.Locate();
            bool explicitlyForced = decision.ForcedByEnvironment || gameMode == LinuxFullscreenScalingMode.AutomaticFit;

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

                if (explicitlyForced)
                    throw new GamescopeUnavailableException(message);
                return original;
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

                if (explicitlyForced)
                    throw new GamescopeUnavailableException(message);
                return original;
            }

            var wrapped = GamescopeCommandBuilder.Wrap(original, availability.ExecutablePath, display.Width, display.Height);

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
                Wrapped = true
            }.ToLogBlock());

            return wrapped;
        }
    }
}
