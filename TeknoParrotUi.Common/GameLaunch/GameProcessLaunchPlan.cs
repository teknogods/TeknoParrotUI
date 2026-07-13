using System.Diagnostics;

namespace TeknoParrotUi.Common.GameLaunch
{
    /// <summary>
    /// The result of deciding HOW a game should be launched, without actually
    /// starting any process - built by <see cref="Proton.GamescopeLauncher.BuildLaunchPlan"/>
    /// and executed by <see cref="GameProcessLauncher"/>. Separating "decide"
    /// from "execute" is what lets the actual <see cref="System.Diagnostics.Process.Start()"/>
    /// fallback rules (see <see cref="GameProcessLauncher"/>) be unit-tested
    /// with a fake <see cref="IProcessStarter"/> instead of real processes.
    /// </summary>
    public sealed class GameProcessLaunchPlan
    {
        /// <summary>The exact original/unwrapped launch - always present, always what Disabled mode uses.</summary>
        public ProcessStartInfo DirectStartInfo { get; init; }

        /// <summary>Non-null only when Gamescope wrapping should be attempted.</summary>
        public ProcessStartInfo GamescopeStartInfo { get; init; }

        /// <summary>True whenever fullscreen scaling was requested at all (even if it ended up unavailable).</summary>
        public bool ScalingRequested { get; init; }

        /// <summary>
        /// True when scaling was EXPLICITLY requested (TP_GAMESCOPE=1 or an
        /// explicit per-game AutomaticFit setting) rather than merely
        /// inherited from the global default - determines whether a failure
        /// to launch under Gamescope should error out instead of silently
        /// falling back to <see cref="DirectStartInfo"/>.
        /// </summary>
        public bool ScalingForced { get; init; }

        /// <summary>
        /// Non-null when Gamescope preflight (policy/discovery/display
        /// resolution) failed BEFORE any process was started - if
        /// <see cref="ScalingForced"/> is also true, this is a hard error;
        /// otherwise it's just an informational fallback reason.
        /// </summary>
        public string ScalingUnavailableReason { get; init; }
    }
}
