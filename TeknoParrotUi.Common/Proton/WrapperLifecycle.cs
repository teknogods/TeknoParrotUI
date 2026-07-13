using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace TeknoParrotUi.Common.Proton
{
    /// <summary>
    /// Tracks one Gamescope-wrapped launch's process identities - the
    /// session boundary used by <see cref="IGameProcessTreeMonitor"/> and
    /// <see cref="WrapperLifecycleDecider"/> so cleanup/termination only
    /// ever touches THIS session's processes, never an unrelated Gamescope
    /// or Wine process running elsewhere on the system.
    /// </summary>
    public sealed class WrappedGameProcessSession
    {
        public Process WrapperProcess { get; init; }
        public int WrapperPid { get; init; }

        public Process PrimaryGameProcess { get; set; }
        public IReadOnlyCollection<int> KnownSessionProcessIds { get; set; } = Array.Empty<int>();

        public bool IsWrapperLaunch { get; init; }
    }

    /// <summary>Decision returned by <see cref="WrapperLifecycleDecider.Decide"/> each poll tick.</summary>
    public enum WrapperLifecycleAction
    {
        /// <summary>Nothing notable this tick - keep polling.</summary>
        ContinueWaiting,
        /// <summary>The user requested force-quit - kill the wrapper directly.</summary>
        ForceQuitRequested,
        /// <summary>The wrapper process itself exited (normally or crashed) - session is over.</summary>
        WrapperExitedNaturally,
        /// <summary>The game exited and the wrapper has lingered past the grace period - terminate ONLY this wrapper.</summary>
        TerminateLingeringWrapper,
        /// <summary>The wrapper has been running for a while with no game child ever observed - report a possible startup failure (does not terminate anything by itself).</summary>
        ReportWrapperStartupStall
    }

    /// <summary>Tunable timing defaults for the wrapper lifecycle - kept in one place so both production code and tests reference the same values.</summary>
    public static class WrapperLifecycleDefaults
    {
        /// <summary>How long the wrapper may linger after the game process is no longer observed before it is terminated.</summary>
        public static readonly TimeSpan LingerGracePeriod = TimeSpan.FromSeconds(5);

        /// <summary>How long to wait for a game child to appear at all before reporting a possible wrapper startup stall.</summary>
        public static readonly TimeSpan StartupObservationWindow = TimeSpan.FromSeconds(15);

        /// <summary>How long to wait for a graceful (SIGTERM) exit before escalating to SIGKILL.</summary>
        public static readonly TimeSpan TerminationGracefulTimeout = TimeSpan.FromSeconds(3);

        /// <summary>Normal poll interval while a wrapper session is running.</summary>
        public static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);
    }

    /// <summary>
    /// PURE decision logic for the Gamescope wrapper lifecycle - takes every
    /// input explicitly (no process/timer access at all), so every scenario
    /// is directly unit-testable without real Wine/Gamescope processes or
    /// real wall-clock waits. See <see cref="WrapperLifecycleDefaults"/> for
    /// the timing constants production code uses; tests may pass any values.
    /// </summary>
    public static class WrapperLifecycleDecider
    {
        public static WrapperLifecycleAction Decide(
            bool forceQuitRequested,
            bool wrapperHasExited,
            bool hasEverObservedGameChild,
            bool gameCurrentlyAlive,
            TimeSpan? timeSinceGameExited,
            TimeSpan timeSinceLaunch,
            TimeSpan gracePeriod,
            TimeSpan startupObservationWindow,
            bool startupStallAlreadyReported)
        {
            if (forceQuitRequested)
                return WrapperLifecycleAction.ForceQuitRequested;

            if (wrapperHasExited)
                return WrapperLifecycleAction.WrapperExitedNaturally;

            if (hasEverObservedGameChild && !gameCurrentlyAlive && timeSinceGameExited.HasValue && timeSinceGameExited.Value >= gracePeriod)
                return WrapperLifecycleAction.TerminateLingeringWrapper;

            if (!hasEverObservedGameChild && !startupStallAlreadyReported && timeSinceLaunch >= startupObservationWindow)
                return WrapperLifecycleAction.ReportWrapperStartupStall;

            return WrapperLifecycleAction.ContinueWaiting;
        }
    }
}
