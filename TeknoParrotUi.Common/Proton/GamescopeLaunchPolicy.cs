namespace TeknoParrotUi.Common.Proton
{
    /// <summary>Why Gamescope wrapping was skipped for a launch - surfaced in diagnostics.</summary>
    public enum GamescopeSkipReason
    {
        None,
        DisabledByPerGameSetting,
        DisabledByGlobalSetting,
        DisabledByEnvironment,
        ExternalEmulatorNotExplicitlyForced,
        AlreadyInsideGamescope
    }

    /// <summary>Pure result of <see cref="GamescopeLaunchPolicy.Resolve"/> - no I/O, no environment reads.</summary>
    public sealed class GamescopePolicyDecision
    {
        /// <summary>Always AutomaticFit or Disabled - Default is only ever a per-game input, never an output.</summary>
        public LinuxFullscreenScalingMode EffectiveMode { get; init; } = LinuxFullscreenScalingMode.Disabled;
        public bool ShouldAttemptWrap => EffectiveMode == LinuxFullscreenScalingMode.AutomaticFit;
        public bool ForcedByEnvironment { get; init; }
        public bool DisabledByEnvironment { get; init; }
        public GamescopeSkipReason SkipReason { get; init; } = GamescopeSkipReason.None;
        public string SkipDescription { get; init; } = string.Empty;
    }

    /// <summary>
    /// Pure decision logic for whether/how a launch should be wrapped with
    /// Gamescope - no filesystem, process, or environment-variable access
    /// happens in this class, so every combination is directly unit-testable.
    ///
    /// Precedence (highest to lowest):
    ///   1. TP_NO_GAMESCOPE=1 - always disables, overrides everything else
    ///      including TP_GAMESCOPE=1.
    ///   2. TP_GAMESCOPE=1 - forces AutomaticFit (backward compatibility).
    ///   3. Explicit per-game mode (AutomaticFit or Disabled; Default doesn't
    ///      count as "explicit" here - it inherits the global mode).
    ///   4. Global mode (already resolved to a concrete AutomaticFit/Disabled
    ///      value by the caller, including any migration/default behaviour).
    ///
    /// After a mode of AutomaticFit is reached by any of the above, two
    /// further gates can still turn it into "skip wrapping" (external
    /// emulator profile that wasn't explicitly requested; already running
    /// inside a Gamescope session) - see the task's external-emulator and
    /// already-inside-Gamescope policy sections.
    /// </summary>
    public static class GamescopeLaunchPolicy
    {
        public const string NoGamescopeEnvVar = "TP_NO_GAMESCOPE";
        public const string ForceGamescopeEnvVar = "TP_GAMESCOPE";
        public const string AllowNestedEnvVar = "TP_GAMESCOPE_ALLOW_NESTED";

        public static GamescopePolicyDecision Resolve(
            LinuxFullscreenScalingMode globalMode,
            LinuxFullscreenScalingMode gameMode,
            bool envNoGamescope,
            bool envForceGamescope,
            bool isExternalEmulator,
            bool alreadyInsideGamescope,
            bool allowNestedOverride)
        {
            // Defensive: the global setting must never actually be stored as
            // Default, but if it somehow were, treat it exactly like
            // Disabled rather than silently upgrading an ambiguous value to
            // AutomaticFit.
            var safeGlobalMode = globalMode == LinuxFullscreenScalingMode.AutomaticFit
                ? LinuxFullscreenScalingMode.AutomaticFit
                : LinuxFullscreenScalingMode.Disabled;

            if (envNoGamescope)
            {
                return new GamescopePolicyDecision
                {
                    EffectiveMode = LinuxFullscreenScalingMode.Disabled,
                    DisabledByEnvironment = true,
                    SkipReason = GamescopeSkipReason.DisabledByEnvironment,
                    SkipDescription = $"Disabled by {NoGamescopeEnvVar}=1."
                };
            }

            bool explicitGameDisabled = gameMode == LinuxFullscreenScalingMode.Disabled;
            bool explicitGameAutomaticFit = gameMode == LinuxFullscreenScalingMode.AutomaticFit;

            LinuxFullscreenScalingMode mode;
            bool forced = false;

            if (envForceGamescope)
            {
                mode = LinuxFullscreenScalingMode.AutomaticFit;
                forced = true;
            }
            else if (explicitGameDisabled)
            {
                mode = LinuxFullscreenScalingMode.Disabled;
            }
            else if (explicitGameAutomaticFit)
            {
                mode = LinuxFullscreenScalingMode.AutomaticFit;
            }
            else
            {
                mode = safeGlobalMode;
            }

            if (mode == LinuxFullscreenScalingMode.Disabled)
            {
                return new GamescopePolicyDecision
                {
                    EffectiveMode = LinuxFullscreenScalingMode.Disabled,
                    SkipReason = explicitGameDisabled ? GamescopeSkipReason.DisabledByPerGameSetting : GamescopeSkipReason.DisabledByGlobalSetting,
                    SkipDescription = explicitGameDisabled ? "Disabled by per-game setting." : "Disabled by global setting."
                };
            }

            // mode == AutomaticFit here (forced by env, explicit per-game, or inherited global).
            bool explicitlyRequested = forced || explicitGameAutomaticFit;

            if (isExternalEmulator && !explicitlyRequested)
            {
                return new GamescopePolicyDecision
                {
                    EffectiveMode = LinuxFullscreenScalingMode.Disabled,
                    ForcedByEnvironment = forced,
                    SkipReason = GamescopeSkipReason.ExternalEmulatorNotExplicitlyForced,
                    SkipDescription = "External emulator profile - automatic wrapping skipped (not explicitly requested via TP_GAMESCOPE=1 or an explicit per-game Automatic fullscreen fit setting)."
                };
            }

            if (alreadyInsideGamescope && !allowNestedOverride)
            {
                return new GamescopePolicyDecision
                {
                    EffectiveMode = LinuxFullscreenScalingMode.Disabled,
                    ForcedByEnvironment = forced,
                    SkipReason = GamescopeSkipReason.AlreadyInsideGamescope,
                    SkipDescription = $"Already running inside a Gamescope session - skipping nested wrapping (set {AllowNestedEnvVar}=1 to force nesting anyway)."
                };
            }

            return new GamescopePolicyDecision
            {
                EffectiveMode = LinuxFullscreenScalingMode.AutomaticFit,
                ForcedByEnvironment = forced
            };
        }
    }
}
