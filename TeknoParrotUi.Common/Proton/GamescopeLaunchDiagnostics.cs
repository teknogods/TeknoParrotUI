using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TeknoParrotUi.Common.Proton
{
    /// <summary>
    /// Consolidated end-of-session diagnostic block spanning the WHOLE
    /// Gamescope-wrapped lifecycle (preflight decision through wrapper/game
    /// exit) - distinct from <see cref="GamescopeLaunchConfiguration"/> (the
    /// preflight-only snapshot logged before any process starts). Built
    /// incrementally by GameSession as facts become known, and logged once
    /// when a wrapped session finishes.
    /// </summary>
    public sealed class GamescopeLaunchDiagnostics
    {
        public LinuxFullscreenScalingMode Mode { get; set; }

        public string BackendConfigured { get; set; } = "Auto";
        public GamescopeBackendMode BackendResolved { get; set; }
        public string BackendReason { get; set; } = string.Empty;

        public string TargetMonitorId { get; set; }
        public int TargetMonitorWidth { get; set; }
        public int TargetMonitorHeight { get; set; }
        public int TargetMonitorX { get; set; }
        public int TargetMonitorY { get; set; }
        public DisplaySelectionReason MonitorSelectionReason { get; set; } = DisplaySelectionReason.Unknown;
        public string MonitorPlacementMechanism { get; set; } = "unavailable";

        public int? WrapperPid { get; set; }
        public int? PrimaryGamePid { get; set; }
        public IReadOnlyCollection<int> KnownSessionPids { get; set; } = System.Array.Empty<int>();

        public bool WrapperStarted { get; set; }
        public bool GameChildObserved { get; set; }
        public bool GameChildExited { get; set; }
        public bool WrapperExitedNaturally { get; set; }
        public bool WrapperTerminationRequested { get; set; }
        public bool WrapperTerminationSucceeded { get; set; }
        public bool DirectFallbackUsed { get; set; }

        public string ToLogBlock()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[GamescopeLaunch]");
            sb.AppendLine($"Mode: {Mode}");
            sb.AppendLine($"BackendConfigured: {BackendConfigured}");
            sb.AppendLine($"BackendResolved: {BackendResolved}");
            sb.AppendLine($"BackendReason: {BackendReason}");
            sb.AppendLine($"TargetMonitorId: {(string.IsNullOrEmpty(TargetMonitorId) ? "(unknown)" : TargetMonitorId)}");
            sb.AppendLine(TargetMonitorWidth > 0 && TargetMonitorHeight > 0
                ? $"TargetMonitorResolution: {TargetMonitorWidth}x{TargetMonitorHeight}"
                : "TargetMonitorResolution: (unresolved)");
            sb.AppendLine($"TargetMonitorPosition: {TargetMonitorX},{TargetMonitorY}");
            sb.AppendLine($"MonitorSelectionReason: {MonitorSelectionReason}");
            sb.AppendLine($"MonitorPlacementMechanism: {MonitorPlacementMechanism}");
            sb.AppendLine($"WrapperPid: {(WrapperPid?.ToString() ?? "(none)")}");
            sb.AppendLine($"PrimaryGamePid: {(PrimaryGamePid?.ToString() ?? "(none)")}");
            sb.AppendLine($"KnownSessionPids: {(KnownSessionPids.Count == 0 ? "(none)" : string.Join(',', KnownSessionPids))}");
            sb.AppendLine($"WrapperStarted: {Bool(WrapperStarted)}");
            sb.AppendLine($"GameChildObserved: {Bool(GameChildObserved)}");
            sb.AppendLine($"GameChildExited: {Bool(GameChildExited)}");
            sb.AppendLine($"WrapperExitedNaturally: {Bool(WrapperExitedNaturally)}");
            sb.AppendLine($"WrapperTerminationRequested: {Bool(WrapperTerminationRequested)}");
            sb.AppendLine($"WrapperTerminationSucceeded: {Bool(WrapperTerminationSucceeded)}");
            sb.Append($"DirectFallbackUsed: {Bool(DirectFallbackUsed)}");
            return sb.ToString();
        }

        private static string Bool(bool value) => value ? "true" : "false";
    }
}
