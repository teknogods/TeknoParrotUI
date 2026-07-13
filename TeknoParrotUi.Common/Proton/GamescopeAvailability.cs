namespace TeknoParrotUi.Common.Proton
{
    /// <summary>Why Gamescope could not be used - surfaced to logs/UI so failures are actionable.</summary>
    public enum GamescopeUnavailableReason
    {
        None,
        NotInstalled,
        ConfiguredPathMissing,
        NotExecutable,
        VersionProbeFailed,
        VersionProbeTimedOut
    }

    /// <summary>Result of <see cref="GamescopeLocator.Locate"/> - never silently presents Gamescope as usable when it can't actually start.</summary>
    public sealed class GamescopeAvailability
    {
        public bool IsAvailable { get; init; }
        public string ExecutablePath { get; init; } = string.Empty;
        public string Version { get; init; } = string.Empty;
        public GamescopeUnavailableReason Reason { get; init; } = GamescopeUnavailableReason.None;
        public string Message { get; init; } = string.Empty;

        public static GamescopeAvailability Available(string path, string version) =>
            new() { IsAvailable = true, ExecutablePath = path, Version = version ?? string.Empty };

        public static GamescopeAvailability Unavailable(GamescopeUnavailableReason reason, string message, string path = null) =>
            new() { IsAvailable = false, Reason = reason, Message = message ?? string.Empty, ExecutablePath = path ?? string.Empty };
    }
}
