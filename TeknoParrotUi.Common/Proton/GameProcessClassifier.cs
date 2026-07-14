using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TeknoParrotUi.Common.Proton
{
    /// <summary>
    /// The executables a launch is EXPECTED to run, taken from existing
    /// profile data only (no game-specific database): the configured primary
    /// game executable, the optional secondary executable (GamePath2), and
    /// the known TeknoParrot loader executable used for this launch.
    /// </summary>
    public sealed class GameExecutableExpectations
    {
        public string PrimaryExecutable { get; init; }

        public string SecondaryExecutable { get; init; }

        /// <summary>Loader/launcher executables (BudgieLoader, OpenParrotLoader, ...) - candidates, never immediately the confirmed game.</summary>
        public IReadOnlyList<string> LauncherExecutables { get; init; } = Array.Empty<string>();
    }

    /// <summary>
    /// Normalized executable-name comparison across the naming schemes that
    /// actually collide here: Windows paths (C:\game\game.exe), Linux paths,
    /// mixed slashes, case-insensitive Windows filenames, and
    /// /proc/&lt;pid&gt;/comm's 15-character truncation.
    /// </summary>
    public static class ExecutableNameMatcher
    {
        /// <summary>Basename with both slash directions handled, lower-cased (invariant).</summary>
        public static string NormalizeBaseName(string pathOrName)
        {
            if (string.IsNullOrEmpty(pathOrName))
                return string.Empty;
            var normalized = pathOrName.Replace('\\', '/');
            var baseName = normalized.Substring(normalized.LastIndexOf('/') + 1);
            return baseName.ToLowerInvariant();
        }

        /// <summary>
        /// Does an observed process name (comm - possibly truncated to 15
        /// chars - or a full path) refer to <paramref name="expected"/>?
        /// </summary>
        public static bool Matches(string observedProcessName, string expected)
        {
            if (string.IsNullOrEmpty(observedProcessName) || string.IsNullOrEmpty(expected))
                return false;

            var observed = NormalizeBaseName(observedProcessName);
            var expectedBase = NormalizeBaseName(expected);
            if (observed.Length == 0 || expectedBase.Length == 0)
                return false;

            if (observed == expectedBase)
                return true;

            // /proc comm truncation: a 15-char observed name matches when it
            // is exactly the first 15 chars of the expected basename (with or
            // without extension).
            if (observed.Length == 15 && expectedBase.Length > 15 && expectedBase.StartsWith(observed, StringComparison.Ordinal))
                return true;

            var expectedNoExt = Path.GetFileNameWithoutExtension(expectedBase);
            if (observed == expectedNoExt)
                return true;
            if (observed.Length == 15 && expectedNoExt.Length > 15 && expectedNoExt.StartsWith(observed, StringComparison.Ordinal))
                return true;

            return false;
        }
    }

    /// <summary>
    /// Static (single-observation) confidence classification. Time-based
    /// promotion (candidate stabilization, replacement confirmation) is the
    /// state machine's job - this classifier alone NEVER returns
    /// ConfirmedMainGame, so an arbitrary .exe can never be instantly
    /// treated as the game.
    /// </summary>
    public static class GameProcessClassifier
    {
        /// <summary>True when the process name matches one of the KNOWN loader/launcher executables.</summary>
        public static bool IsKnownLauncher(SessionProcessInfo process, GameExecutableExpectations expectations)
        {
            if (process == null || expectations?.LauncherExecutables == null)
                return false;
            return expectations.LauncherExecutables.Any(l => ExecutableNameMatcher.Matches(process.ProcessName, l));
        }

        public static GameProcessConfidence Classify(SessionProcessInfo process, GameExecutableExpectations expectations)
        {
            if (process == null)
                return GameProcessConfidence.None;
            if (process.IsWrapper || process.IsInfrastructureProcess)
                return GameProcessConfidence.Infrastructure;

            expectations ??= new GameExecutableExpectations();
            var name = process.ProcessName;

            if (ExecutableNameMatcher.Matches(name, expectations.PrimaryExecutable))
                return GameProcessConfidence.ExpectedPrimaryExecutable;

            if (ExecutableNameMatcher.Matches(name, expectations.SecondaryExecutable))
                return GameProcessConfidence.ExpectedSecondaryExecutable;

            // Known loaders/launchers: legitimate session members, may even
            // end up hosting the game, but never instantly confirmed - they
            // go through candidate stabilization like any other .exe.
            if (expectations.LauncherExecutables != null &&
                expectations.LauncherExecutables.Any(l => ExecutableNameMatcher.Matches(name, l)))
                return GameProcessConfidence.Candidate;

            // Any other .exe: candidate only.
            var normalized = ExecutableNameMatcher.NormalizeBaseName(name);
            if (normalized.EndsWith(".exe", StringComparison.Ordinal))
                return GameProcessConfidence.Candidate;

            // comm truncation can cut off ".exe" - a 15-char name that isn't
            // known infrastructure is still a possible windows exe: candidate.
            if (normalized.Length == 15)
                return GameProcessConfidence.Candidate;

            // Non-exe (wine binaries, shells, ...): plumbing.
            return GameProcessConfidence.Infrastructure;
        }
    }
}
