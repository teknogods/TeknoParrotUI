namespace TeknoParrotUi.Common.Proton
{
    /// <summary>Describes what (if anything) was actually attempted to place Gamescope's window on a specific physical monitor.</summary>
    public enum MonitorPlacementMechanism
    {
        /// <summary>Only one monitor was detected - there is nothing to disambiguate, so no placement mechanism is needed.</summary>
        NotNeededSingleMonitor,
        /// <summary>Multiple monitors were detected, but no Gamescope placement mechanism has been validated in this environment - none is used.</summary>
        UnavailableUnverified
    }

    /// <summary>Pure result of <see cref="MonitorPlacementPolicy.Describe"/>.</summary>
    public sealed class MonitorPlacementDecision
    {
        public MonitorPlacementMechanism Mechanism { get; init; }
        public string Description { get; init; } = string.Empty;
        public bool PlacementGuaranteed { get; init; }
    }

    /// <summary>
    /// Honestly describes the (current lack of) monitor PLACEMENT guarantee -
    /// separate from monitor SELECTION (which target monitor's dimensions to
    /// use, see LinuxDisplayResolver/largest-overlap logic in MainWindow) and
    /// separate from the actual `-W`/`-H` output-SIZE arguments (which only
    /// describe the size Gamescope should present at, not which physical
    /// connector/screen it opens on).
    ///
    /// Gamescope 3.16's `--help` lists `--display-index` under "Nested mode
    /// options" ("forces gamescope to use a specific display in nested
    /// mode") and `-O`/`--prefer-output` under "Embedded mode options" (DRM/
    /// standalone-session only, not applicable to TeknoParrotUI's always-
    /// nested use case). `--display-index` is real/version-confirmed, but
    /// this codebase has ONLY ever had a single physical monitor available
    /// for testing, so whether `--display-index` reliably targets the
    /// INTENDED monitor (as opposed to, say, silently doing nothing, or
    /// indexing displays in an order that doesn't match the resolved
    /// target) could not be verified - the task's own instructions
    /// explicitly warn against assuming an option "works correctly for every
    /// backend" merely because it is recognized. Rather than risk shipping
    /// an unverified placement argument that could misbehave on someone's
    /// real multi-monitor rig (regressing the one thing that IS proven to
    /// work: single-monitor `-S fit` scaling), this policy deliberately does
    /// NOT wire any placement mechanism into the real command yet - it only
    /// reports, honestly, whether placement is even a concern (multiple
    /// monitors detected) and that no verified mechanism is used.
    ///
    /// This is intentionally the least risky choice while still satisfying
    /// "do not claim monitor placement is solved" - if/when real mixed-
    /// resolution multi-monitor hardware becomes available, this is the
    /// single place to wire in `--display-index` (nested Wayland/SDL) or
    /// `SDL_VIDEO_FULLSCREEN_DISPLAY` (SDL backend only) once verified.
    /// </summary>
    public static class MonitorPlacementPolicy
    {
        public static MonitorPlacementDecision Describe(int detectedMonitorCount)
        {
            if (detectedMonitorCount <= 1)
            {
                return new MonitorPlacementDecision
                {
                    Mechanism = MonitorPlacementMechanism.NotNeededSingleMonitor,
                    Description = "Only one monitor detected - no placement ambiguity to resolve.",
                    PlacementGuaranteed = true
                };
            }

            return new MonitorPlacementDecision
            {
                Mechanism = MonitorPlacementMechanism.UnavailableUnverified,
                Description = "Multiple monitors detected, but no Gamescope monitor-placement mechanism has been validated on real " +
                              "mixed-resolution multi-monitor hardware in this codebase yet - Gamescope may not open on the intended " +
                              "physical monitor. Only the OUTPUT SIZE (-W/-H) is guaranteed, not the physical screen.",
                PlacementGuaranteed = false
            };
        }
    }
}
