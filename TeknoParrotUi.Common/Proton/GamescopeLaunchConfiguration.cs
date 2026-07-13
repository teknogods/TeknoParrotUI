using System.Text;

namespace TeknoParrotUi.Common.Proton
{
    /// <summary>
    /// Full diagnostic snapshot of one launch's Gamescope decision - built by
    /// <see cref="GamescopeLauncher.Wrap"/> and logged via
    /// <see cref="ProtonLog"/> for every Linux launch. Deliberately has no
    /// game width/height/resolution fields anywhere - the game always
    /// chooses its own resolution and this type only ever describes the
    /// Gamescope OUTPUT (monitor) side of the equation.
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

        public bool ForcedByEnvironment { get; init; }
        public bool DisabledByEnvironment { get; init; }
        public bool IsExternalEmulator { get; init; }
        public bool AlreadyInsideGamescope { get; init; }

        public bool Wrapped { get; init; }
        public string Reason { get; init; } = string.Empty;

        /// <summary>Exact block format required by the task's logging spec.</summary>
        public string ToLogBlock()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[FullscreenScaling]");
            sb.AppendLine($"ConfiguredGlobal: {ConfiguredGlobalMode}");
            sb.AppendLine($"ConfiguredGame: {ConfiguredGameMode}");
            sb.AppendLine($"Effective: {EffectiveMode}");
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
            sb.AppendLine("GameResolutionOverride: none");
            sb.AppendLine($"Wrapped: {Bool(Wrapped)}");
            sb.Append(Wrapped
                ? $"Options: {string.Join(' ', GamescopeCommandBuilder.BuildOutputArguments(OutputWidth, OutputHeight))}"
                : $"Reason: {Reason}");
            return sb.ToString();
        }

        private static string Bool(bool value) => value ? "true" : "false";
    }
}
