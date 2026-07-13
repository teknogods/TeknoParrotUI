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
    /// The single real monitor Gamescope should present fullscreen on - NEVER
    /// the combined virtual desktop size of a multi-monitor X11 setup. This
    /// only ever describes the Gamescope OUTPUT (presentation) resolution; it
    /// must never be confused with or used as the game's own render
    /// resolution (which TeknoParrotUI never knows or configures).
    /// </summary>
    public sealed class ResolvedDisplayTarget
    {
        public int Width { get; init; }
        public int Height { get; init; }
        public DisplayResolutionSource Source { get; init; } = DisplayResolutionSource.None;

        /// <summary>Non-null only when resolution failed - an actionable reason for logs/UI.</summary>
        public string FailureReason { get; init; }

        public bool IsValid => Width > 0 && Height > 0;

        public static ResolvedDisplayTarget Invalid(string reason) =>
            new() { Width = 0, Height = 0, Source = DisplayResolutionSource.None, FailureReason = reason };

        public static ResolvedDisplayTarget Valid(int width, int height, DisplayResolutionSource source) =>
            new() { Width = width, Height = height, Source = source };

        public override string ToString() => IsValid ? $"{Width}x{Height} ({Source})" : $"invalid ({FailureReason})";
    }
}
