using System;

namespace TeknoParrotUi.Common.Proton
{
    /// <summary>
    /// Reads the environment-variable overrides for the Gamescope feature
    /// (TP_NO_GAMESCOPE / TP_GAMESCOPE / TP_GAMESCOPE_ALLOW_NESTED) and
    /// detects whether TeknoParrotUI is already running inside a nested
    /// Gamescope session, so a normal launch never adds a second,
    /// unnecessary Gamescope layer.
    /// </summary>
    public static class GamescopeEnvironment
    {
        public static bool NoGamescopeRequested =>
            Environment.GetEnvironmentVariable(GamescopeLaunchPolicy.NoGamescopeEnvVar) == "1";

        public static bool ForceGamescopeRequested =>
            Environment.GetEnvironmentVariable(GamescopeLaunchPolicy.ForceGamescopeEnvVar) == "1";

        public static bool AllowNestedOverrideRequested =>
            Environment.GetEnvironmentVariable(GamescopeLaunchPolicy.AllowNestedEnvVar) == "1";

        /// <summary>
        /// Best-effort detection of "TeknoParrotUI is already running inside
        /// a Gamescope session" - Gamescope sets GAMESCOPE_WAYLAND_DISPLAY
        /// (and, on newer builds, GAMESCOPE_MODE) in the environment of every
        /// process it spawns, the same way an X server sets DISPLAY.
        /// </summary>
        public static bool IsAlreadyInsideGamescope() =>
            IsAlreadyInsideGamescope(
                Environment.GetEnvironmentVariable("GAMESCOPE_WAYLAND_DISPLAY"),
                Environment.GetEnvironmentVariable("GAMESCOPE_MODE"));

        internal static bool IsAlreadyInsideGamescope(string gamescopeWaylandDisplay, string gamescopeMode) =>
            !string.IsNullOrEmpty(gamescopeWaylandDisplay) || !string.IsNullOrEmpty(gamescopeMode);
    }
}
