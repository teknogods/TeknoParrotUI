using System;
using System.Diagnostics;
using System.Globalization;

namespace TeknoParrotUi.Common.Proton
{
    /// <summary>
    /// Builds the Gamescope wrapper ProcessStartInfo.
    ///
    /// IMPORTANT - evidence-based command, corrected from the original
    /// implementation's UNVERIFIED assumption. Controlled testing (real
    /// wine 11.10 + real gamescope 3.16 + a purpose-built fixed-canvas Win32
    /// probe that renders to a constant-size canvas regardless of its
    /// window's actual size, simulating a real arcade game with a fixed
    /// backbuffer) proved:
    ///
    ///   - `-f --force-windows-fullscreen` (the ORIGINAL command) does NOT
    ///     scale a fixed-resolution client's rendered image at all - it
    ///     forces the WINDOW's dimensions to match the nested/output size,
    ///     and a non-adaptive client's content simply stays pinned at its
    ///     original pixel size in the corner, with the rest of the enlarged
    ///     window left blank. This is NOT the required aspect-preserving
    ///     centered fit.
    ///   - Dropping `--force-windows-fullscreen` and using `-S fit` (explicit
    ///     "fit" scaler, NOT the default "auto") DOES work: Gamescope leaves
    ///     the client window at its own actual/native size (never touched)
    ///     and scales the resulting nested surface up to the output
    ///     resolution, preserving aspect ratio, centering it, and adding
    ///     symmetric pillar/letterboxing - with ZERO resolution configured
    ///     in advance, and confirmed to keep working correctly after a
    ///     runtime resolution change with no relaunch.
    ///   - Gamescope's `auto` BACKEND selection chose the `headless` backend
    ///     (no visible output at all) in the sandboxed nested-Wayland session
    ///     used for this testing; passing `--backend sdl` explicitly was
    ///     required to get a real visible nested window. TeknoParrotUI's use
    ///     case is always "nested" (wrapping one window on an existing
    ///     desktop, never a standalone DRM session), so `sdl` is passed
    ///     explicitly rather than trusting `auto`.
    ///
    /// This has been validated with controlled Win32/Wine test probes, NOT
    /// yet with a real TeknoParrot game launched through the full loader/JVS
    /// pipeline, multiple GPU vendors, a pure X11 (non-XWayland) session, or
    /// lightgun/absolute-pointer input - see the Gamescope feature's status
    /// notes for exactly what remains unverified. AutomaticFit therefore
    /// stays non-default/experimental (see <see cref="LinuxFullscreenScalingMode"/>
    /// and <see cref="ParrotData.FullscreenScalingMode"/>) until that
    /// real-game/lightgun verification happens.
    ///
    /// Never produces `-w`/`-h` (fixed nested/game resolution - would require
    /// knowing the game's resolution in advance, which this feature must
    /// never do), never `-S stretch` or `-S integer`.
    ///
    /// The original <see cref="ProcessStartInfo.Arguments"/> string built by
    /// the existing (unmodified) Wine/Proton/loader command construction is
    /// never re-split or reinterpreted - only Gamescope's own fixed,
    /// validated-integer arguments are newly introduced, followed by a
    /// literal `--` separator and the completely untouched original command.
    /// This avoids the exact "command-string quoting risk" the experimental
    /// implementation was flagged for: re-parsing an already-quoted string
    /// with naive whitespace splitting can silently corrupt paths/arguments
    /// containing spaces, quotes, or non-ASCII characters. A fully structured
    /// (ArgumentList-based, entry-by-entry) launch-command representation was
    /// considered but is a much larger change to the existing Wine/Proton
    /// command construction (which already builds one opaque pre-quoted
    /// string) - deferred; see the feature's status notes "remaining
    /// argument-handling limitation" for the follow-up plan and the explicit
    /// tricky-argument execution tests that cover this decision.
    /// </summary>
    public static class GamescopeCommandBuilder
    {
        /// <summary>
        /// Explicit nested backend - see class docs: `auto` backend detection
        /// chose `headless` (no visible output) in at least one real tested
        /// nested-Wayland session. TeknoParrotUI always wraps a single window
        /// on an existing desktop (X11 or Wayland via XWayland), matching
        /// Gamescope's documented nested-mode backends (`sdl`/`wayland`) -
        /// never the standalone `drm` backend.
        /// </summary>
        public const string BackendEnvVar = "TP_GAMESCOPE_BACKEND";
        public const string DefaultBackend = "sdl";

        /// <summary>The exact, fixed Gamescope output/presentation arguments for a given monitor size.</summary>
        public static string[] BuildOutputArguments(int width, int height, string backend = null) => new[]
        {
            "-W", width.ToString(CultureInfo.InvariantCulture),
            "-H", height.ToString(CultureInfo.InvariantCulture),
            "-S", "fit",
            "-f",
            "--backend", string.IsNullOrEmpty(backend) ? DefaultBackend : backend
        };

        /// <summary>
        /// Returns a NEW ProcessStartInfo that runs <paramref name="original"/>
        /// under Gamescope, preserving every field of the original (working
        /// directory, environment variables, redirection, window style,
        /// executable and arguments) unchanged.
        /// </summary>
        public static ProcessStartInfo Wrap(ProcessStartInfo original, string gamescopeExecutable, int outputWidth, int outputHeight)
        {
            if (original == null)
                throw new ArgumentNullException(nameof(original));
            if (string.IsNullOrEmpty(gamescopeExecutable))
                throw new ArgumentException("A Gamescope executable path is required.", nameof(gamescopeExecutable));

            var wrapped = new ProcessStartInfo
            {
                FileName = gamescopeExecutable,
                WorkingDirectory = original.WorkingDirectory,
                UseShellExecute = original.UseShellExecute,
                CreateNoWindow = original.CreateNoWindow,
                RedirectStandardOutput = original.RedirectStandardOutput,
                RedirectStandardError = original.RedirectStandardError,
                RedirectStandardInput = original.RedirectStandardInput,
                WindowStyle = original.WindowStyle,
                ErrorDialog = original.ErrorDialog
            };
            if (original.RedirectStandardOutput)
                wrapped.StandardOutputEncoding = original.StandardOutputEncoding;
            if (original.RedirectStandardError)
                wrapped.StandardErrorEncoding = original.StandardErrorEncoding;
            foreach (var key in original.Environment.Keys)
                wrapped.Environment[key] = original.Environment[key];

            var backend = Environment.GetEnvironmentVariable(BackendEnvVar);
            var gamescopeArgs = BuildOutputArguments(outputWidth, outputHeight, backend);
            var originalExeToken = QuoteArgument(original.FileName);
            var suffix = string.IsNullOrEmpty(original.Arguments) ? string.Empty : " " + original.Arguments;

            wrapped.Arguments = string.Join(' ', gamescopeArgs) + " -- " + originalExeToken + suffix;

            return wrapped;
        }

        /// <summary>Matches the existing codebase's own convention of always quoting path-like tokens (see ProtonLauncher).</summary>
        internal static string QuoteArgument(string value) =>
            "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
    }
}
