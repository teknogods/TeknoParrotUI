using System;
using System.Diagnostics;

namespace TeknoParrotUi.Common.GameLaunch
{
    /// <summary>
    /// Executes a <see cref="GameProcessLaunchPlan"/> against an injectable
    /// <see cref="IProcessStarter"/>, implementing the actual
    /// <see cref="Process.Start()"/>-level fallback rules (not just the
    /// preflight/discovery-level fallback <see cref="Proton.GamescopeLauncher.BuildLaunchPlan"/>
    /// already handles):
    ///
    /// Automatic/default scaling:
    ///   1. Preflight already failed (no GamescopeStartInfo) -&gt; direct.
    ///   2. Gamescope Process.Start throws before returning a process -&gt; direct.
    ///   3. Gamescope Process.Start returns null -&gt; direct.
    ///   Each fallback is logged with its exact reason.
    ///
    /// Explicitly forced scaling (TP_GAMESCOPE=1 / explicit per-game
    /// AutomaticFit treated as forced):
    ///   1. Preflight failure -&gt; clear error (<see cref="Proton.GamescopeUnavailableException"/>).
    ///   2. Process.Start throws or returns null -&gt; clear error, never silently direct.
    ///
    /// Once Gamescope's Process.Start has successfully returned a process,
    /// NO fallback ever happens afterwards - even if that process exits
    /// immediately, to avoid ever accidentally double-launching the game.
    /// </summary>
    public static class GameProcessLauncher
    {
        public static Process Launch(GameProcessLaunchPlan plan, IProcessStarter starter, Action<string> log = null)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (starter == null) throw new ArgumentNullException(nameof(starter));
            log ??= _ => { };

            if (plan.GamescopeStartInfo == null)
            {
                // Preflight (policy/discovery/display resolution) already decided
                // not to wrap, or failed before any process was touched.
                if (plan.ScalingForced && plan.ScalingUnavailableReason != null)
                {
                    log($"[FullscreenScaling] WrappedCommandCreated: false. GamescopeProcessStarted: false. Reason: {plan.ScalingUnavailableReason}");
                    throw new Proton.GamescopeUnavailableException(plan.ScalingUnavailableReason);
                }

                log("[FullscreenScaling] WrappedCommandCreated: false. DirectFallbackUsed: false (direct launch was the plan).");
                return StartOrThrow(starter, plan.DirectStartInfo, "direct launch");
            }

            Process gamescopeProcess;
            try
            {
                gamescopeProcess = starter.Start(plan.GamescopeStartInfo);
            }
            catch (Exception ex)
            {
                log($"[FullscreenScaling] WrappedCommandCreated: true. GamescopeProcessStarted: false. Process.Start threw: {ex.Message}");
                if (plan.ScalingForced)
                    throw new Proton.GamescopeUnavailableException($"Gamescope failed to start: {ex.Message}");

                log("[FullscreenScaling] DirectFallbackUsed: true (Process.Start threw before a process was created).");
                return StartOrThrow(starter, plan.DirectStartInfo, "direct fallback after Gamescope Process.Start exception");
            }

            if (gamescopeProcess == null)
            {
                log("[FullscreenScaling] WrappedCommandCreated: true. GamescopeProcessStarted: false. Process.Start returned null.");
                if (plan.ScalingForced)
                    throw new Proton.GamescopeUnavailableException("Gamescope Process.Start returned null - no process was created.");

                log("[FullscreenScaling] DirectFallbackUsed: true (Process.Start returned null).");
                return StartOrThrow(starter, plan.DirectStartInfo, "direct fallback after null Gamescope process");
            }

            // Gamescope successfully returned a process - this is the point of
            // no return: never attempt the direct command afterwards, even if
            // this process exits immediately, Vulkan initialization fails
            // later, or the game child process never appears. Falling back
            // here would risk launching the game twice.
            log($"[FullscreenScaling] WrappedCommandCreated: true. GamescopeProcessStarted: true. PID: {gamescopeProcess.Id}. DirectFallbackUsed: false.");
            return gamescopeProcess;
        }

        private static Process StartOrThrow(IProcessStarter starter, ProcessStartInfo info, string context)
        {
            return starter.Start(info)
                ?? throw new InvalidOperationException($"Failed to start process ({context}): Process.Start returned null.");
        }
    }
}
