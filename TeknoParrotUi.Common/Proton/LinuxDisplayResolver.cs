using System;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

namespace TeknoParrotUi.Common.Proton
{
    /// <summary>
    /// Resolves the single real monitor Gamescope should present fullscreen
    /// on for THIS launch - never the combined virtual-desktop size of a
    /// multi-monitor X11 setup, and never permanently cached (the user may
    /// move TeknoParrotUI to another monitor, change resolution, or
    /// connect/disconnect displays between launches).
    ///
    /// Preferred order:
    ///   1. TP_OUTPUT_WIDTH/TP_OUTPUT_HEIGHT (debugging override - both or neither)
    ///   2. Avalonia's screen containing the TeknoParrotUI window (physical pixels)
    ///   3. Active/primary xrandr output mode (X11/XWayland)
    ///   4. None - callers must handle an invalid result themselves (never
    ///      invent a resolution like 1280x720).
    /// </summary>
    public static class LinuxDisplayResolver
    {
        public const string OutputWidthEnvVar = "TP_OUTPUT_WIDTH";
        public const string OutputHeightEnvVar = "TP_OUTPUT_HEIGHT";

        /// <summary>
        /// Hook the Avalonia UI layer sets at startup: returns the physical
        /// pixel width/height of the screen currently containing
        /// TeknoParrotUI's main window, or null when unavailable (no window
        /// yet, headless, etc.). Avalonia's Screen.Bounds is already device
        /// pixels - implementations must NOT multiply by scaling again.
        /// Invoked fresh on every <see cref="Resolve"/> call - never cached
        /// by this class.
        /// </summary>
        public static Func<(int Width, int Height)?> AvaloniaScreenProvider { get; set; }

        /// <summary>
        /// Optional companion hook providing MONITOR IDENTITY (not just
        /// dimensions) for whatever screen <see cref="AvaloniaScreenProvider"/>
        /// resolved - kept as a SEPARATE delegate (rather than changing
        /// AvaloniaScreenProvider's signature) so existing callers/tests that
        /// only set the dimensions provider are unaffected. Optional/nullable
        /// by design - Identifier may legitimately be unavailable.
        /// </summary>
        public static Func<AvaloniaScreenIdentity> AvaloniaScreenIdentityProvider { get; set; }

        /// <summary>
        /// Optional hook the UI layer sets: total number of detected
        /// monitors. Used only to decide whether monitor PLACEMENT is even a
        /// concern (see <see cref="MonitorPlacementPolicy"/>) - never for
        /// resolution/selection itself. Falls back to counting connected
        /// `xrandr` outputs, then to assuming a single monitor when neither
        /// source is available.
        /// </summary>
        public static Func<int> AvaloniaScreenCountProvider { get; set; }

        public static int DetectMonitorCount()
        {
            var provider = AvaloniaScreenCountProvider;
            if (provider != null)
            {
                try
                {
                    var count = provider();
                    if (count > 0)
                        return count;
                }
                catch
                {
                    // fall through
                }
            }

            try
            {
                var psi = new ProcessStartInfo("xrandr", "--current")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    var output = proc.StandardOutput.ReadToEnd();
                    proc.WaitForExit(3000);
                    var count = CountConnectedXrandrOutputs(output);
                    if (count > 0)
                        return count;
                }
            }
            catch
            {
                // fall through
            }

            return 1; // conservative default: assume single-monitor when truly unknown
        }

        /// <summary>Pure counter of " connected" output lines in `xrandr --current` text - directly unit-testable.</summary>
        internal static int CountConnectedXrandrOutputs(string xrandrText)
        {
            if (string.IsNullOrWhiteSpace(xrandrText))
                return 0;
            var count = 0;
            foreach (var rawLine in xrandrText.Split('\n'))
            {
                if (Regex.IsMatch(rawLine, @"^\S+\s+connected\b"))
                    count++;
            }
            return count;
        }

        public static ResolvedDisplayTarget Resolve(Action<string> warn = null)
        {
            warn ??= _ => { };

            var envTarget = ParseEnvironmentOverride(
                Environment.GetEnvironmentVariable(OutputWidthEnvVar),
                Environment.GetEnvironmentVariable(OutputHeightEnvVar),
                warn);
            if (envTarget != null)
                return envTarget;

            var provider = AvaloniaScreenProvider;
            if (provider != null)
            {
                try
                {
                    if (provider() is { } size && size.Width > 0 && size.Height > 0)
                    {
                        var identity = TryGetAvaloniaIdentity();
                        return ResolvedDisplayTarget.Valid(
                            size.Width, size.Height, DisplayResolutionSource.AvaloniaCurrentMonitor,
                            identifier: identity?.Identifier,
                            outputName: null,
                            x: identity?.X ?? 0,
                            y: identity?.Y ?? 0,
                            scaling: identity?.Scaling ?? 1.0,
                            selectionReason: identity?.SelectionReason ?? DisplaySelectionReason.Unknown);
                    }
                }
                catch
                {
                    // Fall through to the xrandr fallback.
                }
            }

            var xrandrTarget = TryX11ActiveOutputLive();
            if (xrandrTarget != null)
                return xrandrTarget;

            return ResolvedDisplayTarget.Invalid(
                "Could not determine the target monitor's resolution (no Avalonia screen information, and xrandr parsing failed or is unavailable).");
        }

        private static AvaloniaScreenIdentity TryGetAvaloniaIdentity()
        {
            try { return AvaloniaScreenIdentityProvider?.Invoke(); }
            catch { return null; }
        }

        /// <summary>
        /// Pure parser for TP_OUTPUT_WIDTH/TP_OUTPUT_HEIGHT - both must be
        /// present and valid positive integers, or neither is used (a
        /// partial pair is always rejected with a warning, never guessed).
        /// Directly unit-testable without touching real environment variables.
        /// </summary>
        internal static ResolvedDisplayTarget ParseEnvironmentOverride(string widthText, string heightText, Action<string> warn)
        {
            warn ??= _ => { };
            bool hasWidth = !string.IsNullOrEmpty(widthText);
            bool hasHeight = !string.IsNullOrEmpty(heightText);
            if (!hasWidth && !hasHeight)
                return null;

            if (hasWidth && !hasHeight)
            {
                warn($"{OutputWidthEnvVar} is set without {OutputHeightEnvVar} - ignoring both, falling back to automatic monitor detection.");
                return null;
            }
            if (hasHeight && !hasWidth)
            {
                warn($"{OutputHeightEnvVar} is set without {OutputWidthEnvVar} - ignoring both, falling back to automatic monitor detection.");
                return null;
            }

            bool widthOk = int.TryParse(widthText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var width) && width > 0;
            bool heightOk = int.TryParse(heightText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var height) && height > 0;
            if (!widthOk || !heightOk)
            {
                warn($"{OutputWidthEnvVar}/{OutputHeightEnvVar} contain an invalid value ('{widthText}' x '{heightText}') - ignoring both, falling back to automatic monitor detection.");
                return null;
            }

            return ResolvedDisplayTarget.Valid(width, height, DisplayResolutionSource.EnvironmentOverride,
                identifier: $"{OutputWidthEnvVar}/{OutputHeightEnvVar}", selectionReason: DisplaySelectionReason.EnvironmentOverride);
        }

        /// <summary>
        /// Pure parser for `xrandr --current` output - prefers the "primary"
        /// connected output's geometry, falling back to the first connected
        /// output. Deliberately never reads the "Screen 0: ... current W x H"
        /// line (that's the COMBINED virtual-desktop size across every
        /// monitor - e.g. 5760x2160 for a 3840x2160 + 1920x1080 setup, which
        /// would make Gamescope try to span both monitors as one output).
        /// Captures the output NAME (e.g. "DP-1") and X/Y offset too, so two
        /// same-resolution monitors remain distinguishable. Directly
        /// unit-testable against captured xrandr output text.
        /// </summary>
        internal static ResolvedDisplayTarget ParseXrandrOutput(string xrandrText)
        {
            if (string.IsNullOrWhiteSpace(xrandrText))
                return null;

            ResolvedDisplayTarget firstConnected = null;
            foreach (var rawLine in xrandrText.Split('\n'))
            {
                var line = rawLine.TrimEnd('\r');
                if (!line.Contains(" connected"))
                    continue;

                var match = Regex.Match(line, @"^(\S+)\s+connected.*?(\d+)x(\d+)\+(\d+)\+(\d+)");
                if (!match.Success)
                    continue;

                var outputName = match.Groups[1].Value;
                if (!int.TryParse(match.Groups[2].Value, out var w) || !int.TryParse(match.Groups[3].Value, out var h) || w <= 0 || h <= 0)
                    continue;
                int.TryParse(match.Groups[4].Value, out var x);
                int.TryParse(match.Groups[5].Value, out var y);

                var target = ResolvedDisplayTarget.Valid(w, h, DisplayResolutionSource.X11ActiveOutput,
                    identifier: outputName, outputName: outputName, x: x, y: y,
                    selectionReason: DisplaySelectionReason.XrandrActiveOutput);
                if (line.Contains(" primary"))
                    return target;
                firstConnected ??= target;
            }

            return firstConnected;
        }

        private static ResolvedDisplayTarget TryX11ActiveOutputLive()
        {
            try
            {
                var psi = new ProcessStartInfo("xrandr", "--current")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using var proc = Process.Start(psi);
                if (proc == null)
                    return null;
                var output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit(3000);
                return ParseXrandrOutput(output);
            }
            catch
            {
                return null;
            }
        }
    }
}
