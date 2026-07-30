using System;
using System.Collections.Generic;
using System.Text.Json;

namespace TeknoParrotUi.AndroidBridge;

[Flags]
internal enum WinlatorBridgeFeatures
{
    SharedPageDescriptor = 1,
    Tpb1 = 1 << 1,
    GuestX64 = 1 << 2,
    GuestX86 = 1 << 3,
    ControlledDiagnostic = 1 << 4,
    VersionedSession = 1 << 5,
    ForwardedInput = 1 << 6,
    PreparedActivityLaunch = 1 << 7,
    ScopedWindowsPath = 1 << 8,
    ManagedEnvironment = 1 << 9,
    ProductionPipeBridge = 1 << 11,
    PreparedDisplayPolicy = 1 << 12,
    PreparedProfileConfig = 1 << 13
}

internal sealed record WinlatorCapabilities(
    int ProtocolVersion,
    int MinimumProtocolVersion,
    WinlatorBridgeFeatures Features,
    int MaximumSharedPageBytes,
    int MaximumPipeNameBytes,
    string Implementation);

internal sealed record WinlatorPreparedSession(
    int ProtocolVersion,
    Guid SessionId,
    int ContainerId,
    int PipePort,
    string PipeName64,
    string PipeName32,
    int SharedPageBytes,
    WinlatorBridgeFeatures Features,
    string State);

internal sealed record WinlatorActivityLaunchRequest(
    Guid SessionId,
    int ContainerId,
    string LaunchKind,
    string? Executable = null,
    string? WorkingDirectory = null,
    IReadOnlyList<string>? Arguments = null,
    string? LibraryDirectory = null,
    int ControlsProfileId = 0,
    int FrameRateLimit = 0,
    int ResolutionWidth = 0,
    int ResolutionHeight = 0,
    bool DebugLoggingEnabled = true,
    string CompatibilityPreset = "",
    string DisplayMode = WinlatorSessionContract.DisplayModeCentered,
    string ProfileConfigIni = "");

internal sealed record WinlatorManagedEnvironment(
    int SchemaVersion,
    string State,
    int ContainerId,
    string ContainerTemplate,
    string RuntimeRoot,
    bool? CxbxrAvailable)
{
    public bool IsReady => State == "ready";
    public bool NeedsStoragePermission => State == "permission-required";
    public bool NeedsRuntimePackages => State == "runtime-required";
}

internal static class WinlatorSessionContract
{
    public const int ServiceProtocolVersion = 13;
    public const int MinimumCompatibleServiceProtocolVersion = 8;
    public const int SessionFlagDiagnostic = 1;
    public const int SessionFlagProduction = 1 << 1;
    public const string DiagnosticPipe64 = "TPWinlatorServicePipe64";
    public const string DiagnosticPipe32 = "TPWinlatorServicePipe32";
    public const string ProductionPipe64 = "TeknoParrotPipe64";
    public const string ProductionPipe32 = "TeknoParrotPipe";
    public const string ProductionJvsPipe64 = "TeknoParrot_JVS64";
    public const string ProductionJvsPipe32 = "TeknoParrot_JVS";
    public const string ForwardedInputDiagnosticLaunchKind = "forwarded-input-diagnostic";
    public const string WindowsExecutableLaunchKind = "windows-executable";
    public const string CompatibilityPresetMediaWmv = "media-wmv";
    public const string CompatibilityPresetWineGStreamer = "wine-gstreamer";
    public const string CompatibilityPresetTaitoLegacySCard = "taito-legacy-scard";
    public const string CompatibilityPresetDirtyDrivingFullscreen = "dirty-driving-fullscreen";
    public const string CompatibilityPresetWmmtTerminal = "wmmt-terminal";
    public const string CompatibilityPresetWmmtNoTerminal = "wmmt-no-terminal";
    public const string CompatibilityPresetWmmt3YaCard = "wmmt3-yacard";
    public const string CompatibilityPresetCxbxrWmmtYaCard = "cxbxr-wmmt-yacard";
    public const string CompatibilityPresetCxbxrPerformance =
        "cxbxr-performance";
    public const string CompatibilityPresetCxbxrChihiroType3 =
        "cxbxr-chihiro-type3";
    public const string CompatibilityPresetWackyRacesNetwork = "wacky-races-network";
    public const string CompatibilityPresetPostStartRemoteThread =
        "post-start-remote-thread";
    public const string CompatibilityPresetInitialD8 = "initial-d8";
    public const string CompatibilityPresetInitialDTheArcade = "initial-d-the-arcade";
    public const string CompatibilityPresetChaseHq2 = "chase-hq2";
    public const string CompatibilityPresetStarWars = "star-wars";
    public const string CompatibilityPresetTaikoCustomResolution = "taiko-custom-resolution";
    public const string CompatibilityPresetLargeAddressAware = "large-address-aware";
    public const string CompatibilityPresetLargeAddressAwareDdraw = "large-address-aware-ddraw";
    public const string CompatibilityPresetGameWorkingDirectory = "game-working-directory";
    public const string CompatibilityPresetBuiltinDdraw = "builtin-ddraw";
    public const string CompatibilityPresetXactLocalRegister = "xact-local-register";
    public const string CompatibilityPresetEadpDualIo = "eadp-dual-io";
    public const string CompatibilityPresetSharedJvsDualIo = "shared-jvs-dual-io";
    public const string CompatibilityPresetDirectTouchJvs = "direct-touch-jvs";
    public const string CompatibilityPresetBox64Interpreter = "box64-interpreter";
    public const string CompatibilityPresetPortraitWindowCounterClockwise =
        "portrait-window-counter-clockwise";
    public const string CompatibilityPresetParkedEntrypoint = "parked-entrypoint";
    public const string CompatibilityPresetWineD3dRemoteThread =
        "wined3d-remote-thread";
    public const string CompatibilityPresetWineD3dParkedEntrypoint =
        "wined3d-parked-entrypoint";
    public const string DisplayModeCentered = "centered";
    public const string DisplayModeAspectFit = "aspect-fit";
    public const string DisplayModeFullscreen = "fullscreen";
    public const string ManagedContainerTemplate = "teknoparrot-x86-v1";
    public const string ManagedRuntimeRoot = "E:\\TeknoParrotRuntime";
    public const int MaximumProfileConfigBytes = 16 * 1024;
    private const int MaximumEnvelopeBytes = 32 * 1024;

    private const string ProtocolVersionKey = "protocolVersion";
    private const string MinimumProtocolVersionKey = "minimumProtocolVersion";
    private const string FeatureFlagsKey = "featureFlags";
    private const string MaximumSharedPageBytesKey = "maximumSharedPageBytes";
    private const string MaximumPipeNameBytesKey = "maximumPipeNameBytes";
    private const string ImplementationKey = "implementation";
    private const string ClientNameKey = "clientName";
    private const string RequestedSessionIdKey = "requestedSessionId";
    private const string TokenHexKey = "tokenHex";
    private const string ContainerIdKey = "containerId";
    private const string PipePortKey = "pipePort";
    private const string PipeName64Key = "pipeName64";
    private const string PipeName32Key = "pipeName32";
    private const string SharedPageBytesKey = "sharedPageBytes";
    private const string SessionFlagsKey = "sessionFlags";
    private const string SessionIdKey = "sessionId";
    private const string StateKey = "state";
    private const string LaunchKindKey = "launchKind";
    private const string SchemaVersionKey = "schemaVersion";
    private const string ContainerTemplateKey = "containerTemplate";
    private const string RuntimeRootKey = "runtimeRoot";
    private const string CxbxrAvailableKey = "cxbxrAvailable";
    private const string ActivatedKey = "activated";
    private const string MessageKey = "message";

    public static bool IsCompatibleServiceProtocolVersion(int protocolVersion) =>
        protocolVersion is >= MinimumCompatibleServiceProtocolVersion and <= ServiceProtocolVersion;

    private static void ValidateCompatibleServiceProtocolVersion(int protocolVersion)
    {
        if (!IsCompatibleServiceProtocolVersion(protocolVersion))
            throw new InvalidOperationException(
                $"Winlator bridge protocol v{protocolVersion} is unsupported; " +
                $"expected v{MinimumCompatibleServiceProtocolVersion}-v{ServiceProtocolVersion}.");
    }

    public static WinlatorManagedEnvironment ParseManagedEnvironment(string? source)
    {
        if (string.IsNullOrWhiteSpace(source) || source.Length > MaximumEnvelopeBytes)
            throw new InvalidOperationException("Winlator returned no managed-environment status.");

        using var document = JsonDocument.Parse(source);
        var root = document.RootElement;
        var schemaVersion = ReadInt(root, SchemaVersionKey);
        var state = ReadString(root, StateKey);
        if (schemaVersion != 1)
            throw new InvalidOperationException("Winlator returned an unsupported environment schema.");

        if (state is "permission-required" or "runtime-required")
            return new WinlatorManagedEnvironment(
                schemaVersion, state, 0, string.Empty, string.Empty, null);
        if (state != "ready")
            throw new InvalidOperationException("Winlator could not provision its managed environment.");

        var result = new WinlatorManagedEnvironment(
            schemaVersion,
            state,
            ReadInt(root, ContainerIdKey),
            ReadString(root, ContainerTemplateKey),
            ReadString(root, RuntimeRootKey),
            ReadOptionalBoolean(root, CxbxrAvailableKey));
        if (result.ContainerId <= 0 ||
            result.ContainerTemplate != ManagedContainerTemplate ||
            result.RuntimeRoot != ManagedRuntimeRoot)
            throw new InvalidOperationException("Winlator changed immutable managed-environment settings.");
        return result;
    }

    private static bool? ReadOptionalBoolean(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value))
            return null;
        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => throw new InvalidOperationException(
                $"Winlator returned an invalid {propertyName} flag.")
        };
    }

    public static byte[] CreateDiagnosticSpec(
        Guid sessionId,
        ReadOnlySpan<byte> token,
        int containerId,
        int pipePort)
    {
        if (token.Length != 32)
            throw new ArgumentException("The session token must be exactly 32 bytes.", nameof(token));
        if (containerId <= 0)
            throw new ArgumentOutOfRangeException(nameof(containerId));
        if (pipePort is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(pipePort));

        return JsonSerializer.SerializeToUtf8Bytes(new
        {
            protocolVersion = ServiceProtocolVersion,
            clientName = "teknoparrot-windows-guest-host",
            requestedSessionId = sessionId.ToString("N"),
            tokenHex = Convert.ToHexString(token),
            containerId,
            pipePort,
            pipeName64 = DiagnosticPipe64,
            pipeName32 = DiagnosticPipe32,
            sharedPageBytes = BridgeProtocol.PageSize,
            sessionFlags = SessionFlagDiagnostic
        });
    }

    public static byte[] CreateProductionSpec(
        Guid sessionId,
        ReadOnlySpan<byte> token,
        int containerId,
        int pipePort,
        string pipeName64 = ProductionPipe64,
        string pipeName32 = ProductionPipe32,
        int protocolVersion = ServiceProtocolVersion)
    {
        ValidateCompatibleServiceProtocolVersion(protocolVersion);
        if (token.Length != 32)
            throw new ArgumentException("The session token must be exactly 32 bytes.", nameof(token));
        if (containerId <= 0)
            throw new ArgumentOutOfRangeException(nameof(containerId));
        if (pipePort is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(pipePort));
        var regularPair = pipeName64 == ProductionPipe64 && pipeName32 == ProductionPipe32;
        var jvsPair = pipeName64 == ProductionJvsPipe64 && pipeName32 == ProductionJvsPipe32;
        if (!regularPair && !jvsPair)
            throw new ArgumentException("The production pipe declaration is unsupported.");

        return JsonSerializer.SerializeToUtf8Bytes(new
        {
            protocolVersion,
            clientName = "teknoparrot-windows-guest-host",
            requestedSessionId = sessionId.ToString("N"),
            tokenHex = Convert.ToHexString(token),
            containerId,
            pipePort,
            pipeName64,
            pipeName32,
            sharedPageBytes = BridgeProtocol.PageSize,
            sessionFlags = SessionFlagProduction
        });
    }

    public static WinlatorCapabilities ParseCapabilities(
        byte[]? source,
        int expectedProtocolVersion = ServiceProtocolVersion)
    {
        ValidateCompatibleServiceProtocolVersion(expectedProtocolVersion);
        using var document = ParseEnvelope(source, "capability");
        var root = document.RootElement;
        var result = new WinlatorCapabilities(
            ReadInt(root, ProtocolVersionKey),
            ReadInt(root, MinimumProtocolVersionKey),
            (WinlatorBridgeFeatures)ReadInt(root, FeatureFlagsKey),
            ReadInt(root, MaximumSharedPageBytesKey),
            ReadInt(root, MaximumPipeNameBytesKey),
            ReadString(root, ImplementationKey));
        if (result.ProtocolVersion != expectedProtocolVersion ||
            result.MinimumProtocolVersion > expectedProtocolVersion ||
            result.MaximumSharedPageBytes < BridgeProtocol.PageSize ||
            result.MaximumPipeNameBytes < BridgeProtocol.MaxPipeNameBytes)
            throw new InvalidOperationException("Winlator returned incompatible bridge capabilities.");

        var required = WinlatorBridgeFeatures.SharedPageDescriptor |
                       WinlatorBridgeFeatures.Tpb1 |
                       WinlatorBridgeFeatures.GuestX64 |
                       WinlatorBridgeFeatures.GuestX86 |
                       WinlatorBridgeFeatures.ControlledDiagnostic |
                       WinlatorBridgeFeatures.VersionedSession |
                       WinlatorBridgeFeatures.ForwardedInput |
                       WinlatorBridgeFeatures.PreparedActivityLaunch |
                       WinlatorBridgeFeatures.ScopedWindowsPath |
                       WinlatorBridgeFeatures.ManagedEnvironment |
                       WinlatorBridgeFeatures.ProductionPipeBridge;
        if (expectedProtocolVersion >= 12)
            required |= WinlatorBridgeFeatures.PreparedDisplayPolicy;
        if (expectedProtocolVersion >= 13)
            required |= WinlatorBridgeFeatures.PreparedProfileConfig;
        if ((result.Features & required) != required)
            throw new InvalidOperationException("Winlator is missing required diagnostic capabilities.");
        return result;
    }

    public static WinlatorPreparedSession ParsePrepared(
        byte[]? source,
        Guid requestedSessionId,
        int requestedContainerId,
        int requestedPort,
        string expectedPipe64 = DiagnosticPipe64,
        string expectedPipe32 = DiagnosticPipe32,
        int expectedProtocolVersion = ServiceProtocolVersion)
    {
        ValidateCompatibleServiceProtocolVersion(expectedProtocolVersion);
        using var document = ParseEnvelope(source, "prepared-session");
        var root = document.RootElement;
        if (root.TryGetProperty(TokenHexKey, out _))
            throw new InvalidOperationException("Winlator echoed the raw session token.");

        if (!Guid.TryParseExact(ReadString(root, SessionIdKey), "N", out var sessionId))
            throw new InvalidOperationException("Winlator returned an invalid prepared session id.");
        var result = new WinlatorPreparedSession(
            ReadInt(root, ProtocolVersionKey),
            sessionId,
            ReadInt(root, ContainerIdKey),
            ReadInt(root, PipePortKey),
            ReadString(root, PipeName64Key),
            ReadString(root, PipeName32Key),
            ReadInt(root, SharedPageBytesKey),
            (WinlatorBridgeFeatures)ReadInt(root, FeatureFlagsKey),
            ReadString(root, StateKey));

        if (result.ProtocolVersion != expectedProtocolVersion ||
            result.SessionId != requestedSessionId ||
            result.ContainerId != requestedContainerId ||
            result.PipePort != requestedPort ||
            result.PipeName64 != expectedPipe64 ||
            result.PipeName32 != expectedPipe32 ||
            result.SharedPageBytes != BridgeProtocol.PageSize ||
            result.State != "ready")
            throw new InvalidOperationException("Winlator changed immutable prepared-session settings.");
        return result;
    }

    public static byte[] CreateActivityLaunch(
        WinlatorActivityLaunchRequest request,
        int protocolVersion = ServiceProtocolVersion)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateCompatibleServiceProtocolVersion(protocolVersion);
        if (request.SessionId == Guid.Empty)
            throw new ArgumentException("A non-empty prepared session id is required.", nameof(request));
        if (request.ContainerId <= 0)
            throw new ArgumentOutOfRangeException(nameof(request));
        byte[] envelope;
        if (string.Equals(
                request.LaunchKind,
                ForwardedInputDiagnosticLaunchKind,
                StringComparison.Ordinal))
        {
            if (request.Executable != null || request.WorkingDirectory != null ||
                request.Arguments is { Count: > 0 } || request.ControlsProfileId != 0 ||
                request.FrameRateLimit != 0 || request.ResolutionWidth != 0 ||
                request.ResolutionHeight != 0 || !request.DebugLoggingEnabled ||
                !string.IsNullOrEmpty(request.CompatibilityPreset) ||
                request.DisplayMode != DisplayModeCentered ||
                !string.IsNullOrEmpty(request.ProfileConfigIni))
                throw new ArgumentException(
                    "The diagnostic Activity launch cannot carry executable settings.", nameof(request));
            envelope = JsonSerializer.SerializeToUtf8Bytes(new
            {
                protocolVersion,
                sessionId = request.SessionId.ToString("N"),
                containerId = request.ContainerId,
                launchKind = request.LaunchKind
            });
        }
        else if (string.Equals(
                     request.LaunchKind,
                     WindowsExecutableLaunchKind,
                     StringComparison.Ordinal))
        {
            ValidateDosPath(request.Executable, directory: false, nameof(request.Executable));
            ValidateDosPath(request.WorkingDirectory, directory: true, nameof(request.WorkingDirectory));
            if (request.LibraryDirectory != null)
                ValidateDosPath(
                    request.LibraryDirectory,
                    directory: true,
                    nameof(request.LibraryDirectory));
            var arguments = request.Arguments ?? Array.Empty<string>();
            ValidateArguments(arguments);
            if (request.ControlsProfileId < 0 || request.ControlsProfileId > 1_000_000)
                throw new ArgumentOutOfRangeException(
                    nameof(request.ControlsProfileId),
                    "The Winlator controls profile id is invalid.");
            if (request.FrameRateLimit < 0 || request.FrameRateLimit > 1_000)
                throw new ArgumentOutOfRangeException(
                    nameof(request.FrameRateLimit),
                    "The Winlator frame-rate limit is invalid.");
            ValidateResolution(request.ResolutionWidth, request.ResolutionHeight);
            ValidateCompatibilityPreset(request.CompatibilityPreset);
            ValidateDisplayMode(request.DisplayMode);
            if (protocolVersion < 13 && !string.IsNullOrEmpty(request.ProfileConfigIni))
                throw new InvalidOperationException(
                    "The installed Winlator companion cannot receive complete game-profile configuration.");
            if (protocolVersion >= 13)
            {
                ValidateProfileConfigIni(request.ProfileConfigIni);
                envelope = JsonSerializer.SerializeToUtf8Bytes(new
                {
                    protocolVersion,
                    sessionId = request.SessionId.ToString("N"),
                    containerId = request.ContainerId,
                    launchKind = request.LaunchKind,
                    executable = request.Executable,
                    workingDirectory = request.WorkingDirectory,
                    arguments,
                    libraryDirectory = request.LibraryDirectory,
                    controlsProfileId = request.ControlsProfileId,
                    frameRateLimit = request.FrameRateLimit,
                    resolutionWidth = request.ResolutionWidth,
                    resolutionHeight = request.ResolutionHeight,
                    debugLoggingEnabled = request.DebugLoggingEnabled,
                    compatibilityPreset = request.CompatibilityPreset,
                    displayMode = request.DisplayMode,
                    profileConfigIni = request.ProfileConfigIni
                });
            }
            else if (protocolVersion >= 12)
            {
                envelope = JsonSerializer.SerializeToUtf8Bytes(new
                {
                    protocolVersion,
                    sessionId = request.SessionId.ToString("N"),
                    containerId = request.ContainerId,
                    launchKind = request.LaunchKind,
                    executable = request.Executable,
                    workingDirectory = request.WorkingDirectory,
                    arguments,
                    libraryDirectory = request.LibraryDirectory,
                    controlsProfileId = request.ControlsProfileId,
                    frameRateLimit = request.FrameRateLimit,
                    resolutionWidth = request.ResolutionWidth,
                    resolutionHeight = request.ResolutionHeight,
                    debugLoggingEnabled = request.DebugLoggingEnabled,
                    compatibilityPreset = request.CompatibilityPreset,
                    displayMode = request.DisplayMode
                });
            }
            else if (protocolVersion >= 11)
            {
                envelope = JsonSerializer.SerializeToUtf8Bytes(new
                {
                    protocolVersion,
                    sessionId = request.SessionId.ToString("N"),
                    containerId = request.ContainerId,
                    launchKind = request.LaunchKind,
                    executable = request.Executable,
                    workingDirectory = request.WorkingDirectory,
                    arguments,
                    libraryDirectory = request.LibraryDirectory,
                    controlsProfileId = request.ControlsProfileId,
                    frameRateLimit = request.FrameRateLimit,
                    resolutionWidth = request.ResolutionWidth,
                    resolutionHeight = request.ResolutionHeight,
                    debugLoggingEnabled = request.DebugLoggingEnabled,
                    compatibilityPreset = request.CompatibilityPreset
                });
            }
            else if (protocolVersion >= 10)
            {
                if (!string.IsNullOrEmpty(request.CompatibilityPreset))
                    throw new InvalidOperationException(
                        "The installed Winlator companion does not support compatibility presets.");
                envelope = JsonSerializer.SerializeToUtf8Bytes(new
                {
                    protocolVersion,
                    sessionId = request.SessionId.ToString("N"),
                    containerId = request.ContainerId,
                    launchKind = request.LaunchKind,
                    executable = request.Executable,
                    workingDirectory = request.WorkingDirectory,
                    arguments,
                    libraryDirectory = request.LibraryDirectory,
                    controlsProfileId = request.ControlsProfileId,
                    frameRateLimit = request.FrameRateLimit,
                    resolutionWidth = request.ResolutionWidth,
                    resolutionHeight = request.ResolutionHeight,
                    debugLoggingEnabled = request.DebugLoggingEnabled
                });
            }
            else if (protocolVersion >= 9)
            {
                envelope = JsonSerializer.SerializeToUtf8Bytes(new
                {
                    protocolVersion,
                    sessionId = request.SessionId.ToString("N"),
                    containerId = request.ContainerId,
                    launchKind = request.LaunchKind,
                    executable = request.Executable,
                    workingDirectory = request.WorkingDirectory,
                    arguments,
                    libraryDirectory = request.LibraryDirectory,
                    controlsProfileId = request.ControlsProfileId,
                    frameRateLimit = request.FrameRateLimit,
                    resolutionWidth = request.ResolutionWidth,
                    resolutionHeight = request.ResolutionHeight
                });
            }
            else
            {
                // Protocol v8 predates resolution and per-game debug options.
                // Omitting them preserves the exact ten-field schema enforced
                // by already-installed tp16 companions.
                envelope = JsonSerializer.SerializeToUtf8Bytes(new
                {
                    protocolVersion,
                    sessionId = request.SessionId.ToString("N"),
                    containerId = request.ContainerId,
                    launchKind = request.LaunchKind,
                    executable = request.Executable,
                    workingDirectory = request.WorkingDirectory,
                    arguments,
                    libraryDirectory = request.LibraryDirectory,
                    controlsProfileId = request.ControlsProfileId,
                    frameRateLimit = request.FrameRateLimit
                });
            }
        }
        else
        {
            throw new ArgumentException("The Activity launch kind is not implemented.", nameof(request));
        }

        if (envelope.Length > MaximumEnvelopeBytes)
            throw new ArgumentException("The Activity launch envelope is too large.", nameof(request));
        return envelope;
    }

    public static void ValidateProfileConfigIni(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException(
                "A complete TeknoParrot profile configuration is required.", nameof(value));
        if (System.Text.Encoding.UTF8.GetByteCount(value) > MaximumProfileConfigBytes)
            throw new ArgumentException(
                $"The TeknoParrot profile configuration exceeds {MaximumProfileConfigBytes} UTF-8 bytes.",
                nameof(value));
        foreach (var character in value)
        {
            if (character == '\0' || (character < 0x20 && character is not ('\r' or '\n' or '\t')))
                throw new ArgumentException(
                    "The TeknoParrot profile configuration contains an invalid control character.",
                    nameof(value));
        }
    }

    private static void ValidateDisplayMode(string? value)
    {
        if (value != DisplayModeCentered &&
            value != DisplayModeAspectFit &&
            value != DisplayModeFullscreen)
            throw new ArgumentException("The Winlator display mode is unsupported.", nameof(value));
    }

    private static void ValidateCompatibilityPreset(string? value)
    {
        if (value is not ("" or CompatibilityPresetMediaWmv or
            CompatibilityPresetWineGStreamer or
            CompatibilityPresetTaitoLegacySCard or CompatibilityPresetDirtyDrivingFullscreen or
            CompatibilityPresetWmmtTerminal or CompatibilityPresetWmmtNoTerminal or
            CompatibilityPresetWmmt3YaCard or
            CompatibilityPresetCxbxrWmmtYaCard or
            CompatibilityPresetCxbxrPerformance or
            CompatibilityPresetCxbxrChihiroType3 or
            CompatibilityPresetWackyRacesNetwork or
            CompatibilityPresetPostStartRemoteThread or
            CompatibilityPresetInitialD8 or
            CompatibilityPresetInitialDTheArcade or
            CompatibilityPresetChaseHq2 or CompatibilityPresetStarWars or
            CompatibilityPresetTaikoCustomResolution or
            CompatibilityPresetLargeAddressAware or
            CompatibilityPresetLargeAddressAwareDdraw or
            CompatibilityPresetGameWorkingDirectory or
            CompatibilityPresetBuiltinDdraw or
            CompatibilityPresetXactLocalRegister or CompatibilityPresetEadpDualIo or
            CompatibilityPresetSharedJvsDualIo or
            CompatibilityPresetDirectTouchJvs or
            CompatibilityPresetBox64Interpreter or
            CompatibilityPresetPortraitWindowCounterClockwise or
            CompatibilityPresetParkedEntrypoint or
            CompatibilityPresetWineD3dRemoteThread or
            CompatibilityPresetWineD3dParkedEntrypoint))
            throw new ArgumentException("The Winlator compatibility preset is unsupported.");
    }

    private static void ValidateResolution(int width, int height)
    {
        if ((width == 0) != (height == 0) || width < 0 || height < 0 ||
            width > 8_192 || height > 8_192 ||
            (width != 0 && (width < 320 || height < 240)))
            throw new ArgumentOutOfRangeException(
                nameof(width),
                "The Winlator resolution must be omitted or between 320x240 and 8192x8192.");
    }

    public static byte[] CreateInputActivityDiagnosticLaunch(WinlatorPreparedSession prepared)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        return CreateActivityLaunch(new WinlatorActivityLaunchRequest(
            prepared.SessionId,
            prepared.ContainerId,
            ForwardedInputDiagnosticLaunchKind), prepared.ProtocolVersion);
    }

    public static void ValidateActivityLaunchStatus(
        string? status,
        WinlatorActivityLaunchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var expected = $"state=launching;session={request.SessionId:N};" +
                       $"container={request.ContainerId};kind={request.LaunchKind}";
        if (!string.Equals(status, expected, StringComparison.Ordinal))
            throw new InvalidOperationException("Winlator returned an invalid Activity launch status.");
    }

    private static void ValidateDosPath(string? value, bool directory, string parameterName)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 512 || value.Length < 4 ||
            (value[0] is not ('C' or 'c' or 'D' or 'd' or 'E' or 'e' or 'G' or 'g')) ||
            value[1] != ':' || value[2] != '\\' || value[^1] == '\\' ||
            value.Contains('/') || value.Contains('"') || value.AsSpan(2).Contains(':') ||
            (!directory && !value.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException(
                "A canonical C:, D:, E:, or G: DOS path is required.",
                parameterName);

        foreach (var character in value)
        {
            if (character < 0x20)
                throw new ArgumentException("DOS paths cannot contain control characters.", parameterName);
        }

        foreach (var segment in value[3..].Split('\\'))
        {
            if (segment.Length == 0 || segment is "." or "..")
                throw new ArgumentException("DOS paths cannot contain empty or traversal segments.", parameterName);
        }
    }

    private static void ValidateArguments(IReadOnlyList<string> arguments)
    {
        if (arguments.Count > 32)
            throw new ArgumentException("At most 32 Windows arguments are supported.", nameof(arguments));
        foreach (var argument in arguments)
        {
            if (argument == null || argument.Length > 512 || argument.Contains('"'))
                throw new ArgumentException("A Windows argument is invalid.", nameof(arguments));
            foreach (var character in argument)
            {
                if (character < 0x20)
                    throw new ArgumentException(
                        "Windows arguments cannot contain control characters.", nameof(arguments));
            }
        }
    }

    private static JsonDocument ParseEnvelope(byte[]? source, string kind)
    {
        if (source is not { Length: > 0 and <= MaximumEnvelopeBytes })
            throw new InvalidOperationException($"Winlator returned an invalid {kind} envelope size.");
        try
        {
            return JsonDocument.Parse(source, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 8
            });
        }
        catch (JsonException error)
        {
            throw new InvalidOperationException($"Winlator returned invalid {kind} JSON.", error);
        }
    }

    private static int ReadInt(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) || !value.TryGetInt32(out var result))
            throw new InvalidOperationException("Winlator omitted integer field " + name + '.');
        return result;
    }

    private static string ReadString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
            throw new InvalidOperationException("Winlator omitted string field " + name + '.');
        return value.GetString() ?? string.Empty;
    }
}
