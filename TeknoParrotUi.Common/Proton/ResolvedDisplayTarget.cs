namespace TeknoParrotUi.Common.Proton
{
    /// <summary>Where a resolved monitor resolution came from - surfaced in diagnostics/logging.</summary>
    public enum DisplayResolutionSource
    {
        /// <summary>No source could resolve a usable resolution.</summary>
        None,
        /// <summary>TP_OUTPUT_WIDTH/TP_OUTPUT_HEIGHT environment override (debugging).</summary>
        EnvironmentOverride,
        /// <summary>Avalonia's screen containing the TeknoParrotUI window (physical pixels).</summary>
        AvaloniaCurrentMonitor,
        /// <summary>Parsed from `xrandr --current`'s active/primary connected output mode.</summary>
        X11ActiveOutput
    }

    /// <summary>
    /// Why THIS particular monitor (among possibly several) was selected -
    /// distinct from <see cref="DisplayResolutionSource"/>, which describes
    /// WHERE the dimensions came from (env override / Avalonia / xrandr).
    /// </summary>
    public enum DisplaySelectionReason
    {
        Unknown,
        EnvironmentOverride,
        LargestOverlap,
        WindowCenterFallback,
        PrimaryFallback,
        FirstAvailableFallback,
        XrandrActiveOutput
    }

    /// <summary>
    /// The single real monitor Gamescope should present fullscreen on - NEVER
    /// the combined virtual desktop size of a multi-monitor X11 setup. This
    /// only ever describes the Gamescope OUTPUT (presentation) resolution; it
    /// must never be confused with or used as the game's own render
    /// resolution (which TeknoParrotUI never knows or configures).
    ///
    /// IMPORTANT: Width/Height alone do NOT identify a physical monitor - two
    /// monitors can share the same resolution. <see cref="Identifier"/>/
    /// <see cref="OutputName"/>/<see cref="X"/>/<see cref="Y"/> exist so a
    /// monitor can be told apart from another with identical dimensions, and
    /// so a future placement mechanism has something concrete to target.
    /// Selecting these dimensions does NOT by itself guarantee Gamescope's
    /// window actually opens on this physical monitor - see
    /// <see cref="MonitorPlacementPolicy"/> for the (currently unverified,
    /// not wired into the real command) placement story.
    /// </summary>
    public sealed class ResolvedDisplayTarget
    {
        public int Width { get; init; }
        public int Height { get; init; }
        public DisplayResolutionSource Source { get; init; } = DisplayResolutionSource.None;

        /// <summary>Non-null only when resolution failed - an actionable reason for logs/UI.</summary>
        public string FailureReason { get; init; }

        /// <summary>
        /// Opaque identifier distinguishing this monitor from another with
        /// the same resolution - an X11 output name (e.g. "DP-1"), an
        /// Avalonia screen display name, or another backend-specific
        /// identifier. Null when no identity information was available (the
        /// resolution is still valid/usable, just not distinguishable from
        /// another same-sized monitor).
        /// </summary>
        public string Identifier { get; init; }

        /// <summary>The X11/compositor output name specifically (e.g. "DP-1", "HDMI-A-1"), when known - a more specific subset of <see cref="Identifier"/>.</summary>
        public string OutputName { get; init; }

        /// <summary>Top-left X position of this monitor in the desktop's virtual coordinate space, when known.</summary>
        public int X { get; init; }

        /// <summary>Top-left Y position of this monitor in the desktop's virtual coordinate space, when known.</summary>
        public int Y { get; init; }

        /// <summary>Display scaling factor (e.g. 1.0, 1.5, 2.0), when known. Defaults to 1.0 (unknown/unscaled assumption).</summary>
        public double Scaling { get; init; } = 1.0;

        /// <summary>Why this specific monitor (among possibly several) was chosen.</summary>
        public DisplaySelectionReason SelectionReason { get; init; } = DisplaySelectionReason.Unknown;

        public bool IsValid => Width > 0 && Height > 0;

        public static ResolvedDisplayTarget Invalid(string reason) =>
            new() { Width = 0, Height = 0, Source = DisplayResolutionSource.None, FailureReason = reason };

        public static ResolvedDisplayTarget Valid(
            int width, int height, DisplayResolutionSource source,
            string identifier = null, string outputName = null,
            int x = 0, int y = 0, double scaling = 1.0,
            DisplaySelectionReason selectionReason = DisplaySelectionReason.Unknown) =>
            new()
            {
                Width = width,
                Height = height,
                Source = source,
                Identifier = identifier,
                OutputName = outputName,
                X = x,
                Y = y,
                Scaling = scaling <= 0 ? 1.0 : scaling,
                SelectionReason = selectionReason
            };

        public override string ToString() => IsValid
            ? $"{Width}x{Height}{(string.IsNullOrEmpty(Identifier) ? "" : $" [{Identifier}]")} ({Source})"
            : $"invalid ({FailureReason})";
    }
}

