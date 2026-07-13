using System.Text;

namespace TeknoParrotUi.Common.Proton
{
    /// <summary>
    /// Full diagnostic snapshot of one launch's Gamescope PREFLIGHT decision
    /// (policy/discovery/display resolution - built BEFORE any process is
    /// started) - built by <see cref="GamescopeLauncher.BuildLaunchPlan"/> and
    /// logged via <see cref="ProtonLog"/> for every Linux launch. Deliberately
    /// has no game width/height/resolution fields anywhere - the game always
    /// chooses its own resolution and this type only ever describes the
    /// Gamescope OUTPUT (monitor) side of the equation.
    ///
    /// Deliberately does NOT claim actual visual scaling was verified for
    /// THIS launch - "WrappedCommandCreated: true" only means a Gamescope
    /// command was built and (per <see cref="GameProcessLauncher"/>'s own,
    /// separately logged lines) a process was successfully started under it;
    /// whether the image visibly scaled correctly can only be confirmed by
    /// looking at the screen, which is why <see cref="VisualFitStatus"/> is
    /// always the fixed string below rather than a claim of success.
    /// </summary>
    public sealed class GamescopeLaunchConfiguration
    {
        public LinuxFullscreenScalingMode ConfiguredGlobalMode { get; init; }
        public LinuxFullscreenScalingMode ConfiguredGameMode { get; init; }
        public LinuxFullscreenScalingMode EffectiveMode { get; init; }

        public string GamescopeExecutable { get; init; } = string.Empty;
        public string GamescopeVersion { get; init; } = string.Empty;

        public int OutputWidth { get; init; }
        public int OutputHeight { get; init; }
        public DisplayResolutionSource DisplaySource { get; init; } = DisplayResolutionSource.None;

        /// <summary>Monitor identity (may be null/default when unavailable) - see ResolvedDisplayTarget docs on why width/height alone can't identify a monitor.</summary>
        public string TargetMonitorIdentifier { get; init; }
        public int TargetMonitorX { get; init; }
        public int TargetMonitorY { get; init; }
        public DisplaySelectionReason MonitorSelectionReason { get; init; } = DisplaySelectionReason.Unknown;

        /// <summary>Resolved Gamescope nested backend (Auto/Wayland/Sdl) - see GamescopeBackendPolicy.</summary>
        public GamescopeBackendMode BackendResolved { get; init; }

        public bool ForcedByEnvironment { get; init; }
        public bool DisabledByEnvironment { get; init; }
        public bool IsExternalEmulator { get; init; }
        public bool AlreadyInsideGamescope { get; init; }

        /// <summary>A Gamescope-wrapped ProcessStartInfo was built - NOT a claim that a process was started or that scaling was visually verified.</summary>
        public bool Wrapped { get; init; }
        public string Reason { get; init; } = string.Empty;

        private const string UnverifiedVisualFitStatus = "runtime-managed/unverified";

        /// <summary>Exact block format required by the task's logging spec - see class docs on why no field here claims verified visual scaling.</summary>
        public string ToLogBlock()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[FullscreenScaling]");
            sb.AppendLine($"ConfiguredGlobal: {ConfiguredGlobalMode}");
            sb.AppendLine($"ConfiguredGame: {ConfiguredGameMode}");
            sb.AppendLine($"RequestedMode: {EffectiveMode}");
            sb.AppendLine($"EnvironmentForced: {Bool(ForcedByEnvironment)}");
            sb.AppendLine($"EnvironmentDisabled: {Bool(DisabledByEnvironment)}");
            sb.AppendLine($"ExternalEmulator: {Bool(IsExternalEmulator)}");
            sb.AppendLine($"AlreadyInsideGamescope: {Bool(AlreadyInsideGamescope)}");
            sb.AppendLine($"GamescopePath: {(string.IsNullOrEmpty(GamescopeExecutable) ? "(none)" : GamescopeExecutable)}");
            sb.AppendLine($"GamescopeVersion: {(string.IsNullOrEmpty(GamescopeVersion) ? "(unknown)" : GamescopeVersion)}");
            sb.AppendLine($"DisplaySource: {DisplaySource}");
            sb.AppendLine(OutputWidth > 0 && OutputHeight > 0
                ? $"OutputResolution: {OutputWidth}x{OutputHeight}"
                : "OutputResolution: (unresolved)");
            sb.AppendLine($"TargetMonitorIdentifier: {(string.IsNullOrEmpty(TargetMonitorIdentifier) ? "(unknown)" : TargetMonitorIdentifier)}");
            sb.AppendLine($"MonitorSelectionReason: {MonitorSelectionReason}");
            sb.AppendLine("NestedResolutionOverride: none");
            sb.AppendLine($"WrappedCommandCreated: {Bool(Wrapped)}");
            if (Wrapped)
            {
                sb.AppendLine($"Options: {string.Join(' ', GamescopeCommandBuilder.BuildOutputArguments(OutputWidth, OutputHeight, BackendResolved))}");
                sb.Append($"VisualFitStatus: {UnverifiedVisualFitStatus}");
            }
            else
            {
                sb.Append($"Reason: {Reason}");
            }
            return sb.ToString();
        }

        private static string Bool(bool value) => value ? "true" : "false";
    }
}
