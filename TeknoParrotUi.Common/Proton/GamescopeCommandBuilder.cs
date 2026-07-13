using System;
using System.Diagnostics;
using System.Globalization;

namespace TeknoParrotUi.Common.Proton
{
    /// <summary>
    /// Builds the safe Gamescope wrapper ProcessStartInfo. Only ever produces
    /// the OUTPUT-resolution arguments (`-W`, `-H`, `-f`, `--force-windows-fullscreen`)
    /// - never `-w`/`-h` (fixed virtual game resolution), never `-S stretch`
    /// or `-S integer` (both would either distort or intentionally waste
    /// screen space instead of the required "fit, preserve aspect ratio,
    /// center" behaviour).
    ///
    /// The original <see cref="ProcessStartInfo.Arguments"/> string built by
    /// the existing (unmodified) Wine/Proton/loader command construction is
    /// never re-split or reinterpreted - only Gamescope's own fixed,
    /// validated-integer arguments are newly introduced, followed by a
    /// literal `--` separator and the completely untouched original command.
    /// This avoids the exact "command-string quoting risk" the experimental
    /// implementation was flagged for: re-parsing an already-quoted string
    /// with naive whitespace splitting can silently corrupt paths/arguments
    /// containing spaces, quotes, or non-ASCII characters.
    /// </summary>
    public static class GamescopeCommandBuilder
    {
        /// <summary>The exact, fixed Gamescope output/presentation arguments for a given monitor size.</summary>
        public static string[] BuildOutputArguments(int width, int height) => new[]
        {
            "-W", width.ToString(CultureInfo.InvariantCulture),
            "-H", height.ToString(CultureInfo.InvariantCulture),
            "-f",
            "--force-windows-fullscreen"
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
                WindowStyle = original.WindowStyle
            };
            foreach (var key in original.Environment.Keys)
                wrapped.Environment[key] = original.Environment[key];

            var gamescopeArgs = BuildOutputArguments(outputWidth, outputHeight);
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
