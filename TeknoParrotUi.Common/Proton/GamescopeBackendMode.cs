using System;

namespace TeknoParrotUi.Common.Proton
{
    /// <summary>Which Gamescope nested backend to request - see <see cref="GamescopeBackendPolicy"/>.</summary>
    public enum GamescopeBackendMode
    {
        /// <summary>Let Gamescope autodetect (no `--backend` argument emitted) - Gamescope's own default.</summary>
        Auto,
        /// <summary>`--backend wayland` - Gamescope's native Wayland nested-client backend.</summary>
        Wayland,
        /// <summary>`--backend sdl` - Gamescope's SDL2 nested backend.</summary>
        Sdl
    }

    /// <summary>Pure result of <see cref="GamescopeBackendPolicy.Resolve"/> - no I/O, directly testable.</summary>
    public sealed class GamescopeBackendDecision
    {
        public GamescopeBackendMode Resolved { get; init; }
        public string Reason { get; init; } = string.Empty;
        public bool InvalidOverrideIgnored { get; init; }
        public string InvalidOverrideValue { get; init; }
    }

    /// <summary>
    /// Decides which Gamescope nested backend to request for a launch.
    ///
    /// IMPORTANT: earlier testing incorrectly concluded `--backend sdl` was
    /// required unconditionally, because Gamescope's `auto` backend chose the
    /// `headless` backend (no visible output at all) in one specific tested
    /// nested-Wayland sandbox. Follow-up testing in the SAME sandbox
    /// confirmed `--backend wayland` ALSO produces a real, correctly-scaled
    /// visible window there (verified via screenshot: a real glxgears
    /// window, fullscreen, aspect-correct on the real monitor) - so forcing
    /// `sdl` for every Linux desktop/GPU combination was an overcorrection,
    /// not a genuine requirement. This policy instead prefers the backend
    /// that matches the DETECTED session type, and only emits an explicit
    /// `--backend` argument at all when there is a concrete reason to
    /// (explicit override, or a detected session type) - "Auto" resolves to
    /// omitting the argument entirely, letting Gamescope's own (version-
    /// specific) autodetection run, which is the correct behavior on
    /// whatever normal desktop session actually works for Gamescope's own
    /// default. This has NOT been validated across AMD Mesa / Intel Mesa /
    /// NVIDIA-proprietary / handheld gaming environments - only the single
    /// NVIDIA + Wayland/XWayland sandbox described above.
    /// </summary>
    public static class GamescopeBackendPolicy
    {
        public const string BackendEnvVar = "TP_GAMESCOPE_BACKEND";

        /// <summary>
        /// Resolves the backend to use. Every input is explicit (no direct
        /// environment/session reads here) so every combination is directly
        /// unit-testable; <see cref="Resolve()"/> below reads the real
        /// environment for production use.
        /// </summary>
        public static GamescopeBackendDecision Resolve(
            string configuredOverride,
            string sessionType,
            string waylandDisplay,
            string x11Display)
        {
            if (!string.IsNullOrWhiteSpace(configuredOverride))
            {
                var normalized = configuredOverride.Trim();
                if (string.Equals(normalized, "wayland", StringComparison.OrdinalIgnoreCase))
                    return new GamescopeBackendDecision { Resolved = GamescopeBackendMode.Wayland, Reason = $"Explicit {BackendEnvVar} override" };
                if (string.Equals(normalized, "sdl", StringComparison.OrdinalIgnoreCase))
                    return new GamescopeBackendDecision { Resolved = GamescopeBackendMode.Sdl, Reason = $"Explicit {BackendEnvVar} override" };

                if (string.Equals(normalized, "auto", StringComparison.OrdinalIgnoreCase))
                {
                    // "auto" explicitly configured behaves exactly like no
                    // override at all - runs the same session-aware
                    // detection (which may still resolve to Wayland/Sdl based
                    // on the session, not a hardcoded value).
                    var autoDetected = ResolveAutoDetected(sessionType, waylandDisplay, x11Display);
                    return new GamescopeBackendDecision
                    {
                        Resolved = autoDetected.Resolved,
                        Reason = $"Explicit {BackendEnvVar}=auto override - {autoDetected.Reason}"
                    };
                }

                // Invalid value - warn (via InvalidOverrideIgnored) and fall through to auto-detection.
                var fallback = ResolveAutoDetected(sessionType, waylandDisplay, x11Display);
                return new GamescopeBackendDecision
                {
                    Resolved = fallback.Resolved,
                    Reason = fallback.Reason,
                    InvalidOverrideIgnored = true,
                    InvalidOverrideValue = configuredOverride
                };
            }

            return ResolveAutoDetected(sessionType, waylandDisplay, x11Display);
        }

        private static GamescopeBackendDecision ResolveAutoDetected(string sessionType, string waylandDisplay, string x11Display)
        {
            bool waylandSession =
                string.Equals(sessionType, "wayland", StringComparison.OrdinalIgnoreCase) ||
                !string.IsNullOrEmpty(waylandDisplay);

            if (waylandSession)
            {
                return new GamescopeBackendDecision
                {
                    Resolved = GamescopeBackendMode.Wayland,
                    Reason = "Wayland session detected"
                };
            }

            bool x11Session = !string.IsNullOrEmpty(x11Display);
            if (x11Session)
            {
                // No pure-X11 (non-XWayland) session was available to test in
                // this environment. SDL is chosen here as the confirmed
                // compatible fallback for this codebase's testing (it was the
                // backend actually verified end-to-end, including real
                // aspect-preserving scaling, in the one real environment
                // available) - not because it is assumed universal.
                return new GamescopeBackendDecision
                {
                    Resolved = GamescopeBackendMode.Sdl,
                    Reason = "X11 session detected - SDL is the confirmed compatible nested backend from this codebase's testing"
                };
            }

            return new GamescopeBackendDecision
            {
                Resolved = GamescopeBackendMode.Auto,
                Reason = "No session display information available - deferring to Gamescope's own autodetection"
            };
        }

        /// <summary>Production convenience overload - reads the real environment. Prefer the explicit overload above in tests.</summary>
        public static GamescopeBackendDecision Resolve() => Resolve(
            Environment.GetEnvironmentVariable(BackendEnvVar),
            Environment.GetEnvironmentVariable("XDG_SESSION_TYPE"),
            Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"),
            Environment.GetEnvironmentVariable("DISPLAY"));

        /// <summary>Maps a resolved mode to the literal Gamescope `--backend` value, or null when no argument should be emitted (Auto).</summary>
        public static string ToBackendArgument(GamescopeBackendMode mode) => mode switch
        {
            GamescopeBackendMode.Wayland => "wayland",
            GamescopeBackendMode.Sdl => "sdl",
            _ => null
        };

        /// <summary>Builds the exact `[GamescopeBackend]` diagnostic block required by the task's logging spec.</summary>
        public static string ToLogBlock(GamescopeBackendDecision decision, string configuredOverride, string sessionType, string waylandDisplay, string x11Display)
        {
            var configured = string.IsNullOrWhiteSpace(configuredOverride) ? "Auto" : configuredOverride;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[GamescopeBackend]");
            sb.AppendLine($"Configured: {configured}");
            sb.AppendLine($"SessionType: {(string.IsNullOrEmpty(sessionType) ? "(unset)" : sessionType)}");
            sb.AppendLine($"WaylandDisplay: {(string.IsNullOrEmpty(waylandDisplay) ? "(unset)" : waylandDisplay)}");
            sb.AppendLine($"X11Display: {(string.IsNullOrEmpty(x11Display) ? "(unset)" : x11Display)}");
            sb.AppendLine($"Resolved: {decision.Resolved}");
            if (decision.InvalidOverrideIgnored)
                sb.AppendLine($"InvalidOverride: '{decision.InvalidOverrideValue}' ignored - fell back to autodetection");
            sb.Append($"Reason: {decision.Reason}");
            return sb.ToString();
        }
    }
}
