using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using TeknoParrotUi.AndroidBridge;
using TeknoParrotUi.Common;
using TeknoParrotUi.Common.Android;
using TeknoParrotUi.Common.GameLaunch;
using TeknoParrotUi.Common.InputListening.Forwarded;
using TeknoParrotUi.Common.Jvs;

namespace TeknoParrotUi.Avalonia.Android;

/// <summary>
/// Owns the Android half of a prepared Winlator game session.  The service is
/// deliberately independent from the Avalonia Activity: Android may recreate
/// the UI without closing the authenticated input channel or shared page.
/// </summary>
[Service(
    Name = ServiceClass,
    Exported = false,
    ForegroundServiceType = ForegroundService.TypeSpecialUse)]
public sealed class GameSessionService : Service
{
    public const string ServiceClass = "com.teknoparrot.session.GameSessionService";
    public const string StartAction = "com.teknoparrot.ui.action.START_GAME_SESSION";
    public const string StopAction = "com.teknoparrot.ui.action.STOP_GAME_SESSION";
    public const string RestoreExtra = "com.teknoparrot.ui.RESTORE_GAME_SESSION";
    public const string ProfileNameExtra = "com.teknoparrot.ui.extra.PROFILE_NAME";
    public const string ContainerExtra = "com.teknoparrot.ui.extra.CONTAINER_ID";
    public const string ExecutableExtra = "com.teknoparrot.ui.extra.WINDOWS_EXECUTABLE";
    public const string WorkingDirectoryExtra = "com.teknoparrot.ui.extra.WINDOWS_WORKING_DIRECTORY";
    public const string ArgumentsExtra = "com.teknoparrot.ui.extra.WINDOWS_ARGUMENTS_JSON";
    public const string LibraryDirectoryExtra = "com.teknoparrot.ui.extra.WINDOWS_LIBRARY_DIRECTORY";
    public const string InputProtocolExtra = "com.teknoparrot.ui.extra.INPUT_PROTOCOL";
    public const string ControlsProfileIdExtra = "com.teknoparrot.ui.extra.CONTROLS_PROFILE_ID";
    public const string FrameRateLimitExtra = "com.teknoparrot.ui.extra.FRAME_RATE_LIMIT";
    public const string ResolutionWidthExtra = "com.teknoparrot.ui.extra.RESOLUTION_WIDTH";
    public const string ResolutionHeightExtra = "com.teknoparrot.ui.extra.RESOLUTION_HEIGHT";
    public const string DisplayModeExtra = "com.teknoparrot.ui.extra.DISPLAY_MODE";
    public const string DebugLoggingEnabledExtra = "com.teknoparrot.ui.extra.DEBUG_LOGGING_ENABLED";
    public const string CompatibilityPresetExtra = "com.teknoparrot.ui.extra.COMPATIBILITY_PRESET";
    public const string ProfileConfigIniExtra = "com.teknoparrot.ui.extra.PROFILE_CONFIG_INI";
    public const string LaunchErrorExtra = "com.teknoparrot.ui.extra.LAUNCH_ERROR";

    private const string NotificationChannelId = "teknoparrot_game_session";
    private const int NotificationId = 0x5450;
    private const int FailureNotificationId = 0x5451;
    private const string PreferencesName = "teknoparrot-game-session";
    private const string SessionRecordKey = "active-session-v1";
    private static readonly TimeSpan HealthSampleInterval = TimeSpan.FromSeconds(5);
    private static readonly object StatusSync = new();
    private static string _status = "state=idle";
    private static WinlatorForwardedInputSource? _currentInputSource;

    private readonly object _runtimeSync = new();
    private CancellationTokenSource? _sessionStop;
    private Task? _sessionTask;
    private TcpListener? _listener;
    private readonly HashSet<TcpClient> _activeClients = new();
    private ITeknoParrotWinlatorService? _remoteService;
    private string? _remoteSessionId;
    private bool _explicitStopRequested;
    private volatile bool _foregroundStarted;
    private volatile bool _debugLoggingEnabled = true;

    internal static event Action<string>? StatusChanged;

    internal static string CurrentStatus
    {
        get
        {
            lock (StatusSync)
                return _status;
        }
    }

    internal static WinlatorForwardedInputSource? CurrentInputSource
    {
        get
        {
            lock (StatusSync)
                return _currentInputSource;
        }
    }

    /// <summary>
    /// Completes the visible Game Running view when Android rejects the
    /// foreground-service handoff before a service instance can own the
    /// session. Without this terminal status the launcher Activity finishes
    /// successfully but the UI waits forever for an event that cannot arrive.
    /// </summary>
    internal static void ReportLaunchFailure(string? message)
    {
        var status = "state=fault;error=" + Sanitize(message);
        Action<string>? changed;
        lock (StatusSync)
        {
            _status = status;
            changed = StatusChanged;
        }
        changed?.Invoke(status);
    }

    public override void OnCreate()
    {
        base.OnCreate();
        CreateNotificationChannel();
    }

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        if (string.Equals(intent?.Action, StopAction, StringComparison.Ordinal))
        {
            RequestStop();
            return StartCommandResult.NotSticky;
        }

        try
        {
            StartForegroundSession("Preparing Winlator session");
        }
        catch (Exception error)
        {
            ReportLaunchFailure(
                "Android could not start the foreground game service: " + error.Message);
            StopSelf(startId);
            return StartCommandResult.NotSticky;
        }
        var launchError = intent?.GetStringExtra(LaunchErrorExtra);
        if (!string.IsNullOrWhiteSpace(launchError))
        {
            var detail = Sanitize(launchError);
            PublishStatus("state=fault;error=" + detail);
            StopForegroundSession();
            PostFailureNotification(detail);
            StopSelf(startId);
            return StartCommandResult.NotSticky;
        }

        lock (_runtimeSync)
        {
            if (_sessionTask is { IsCompleted: false })
            {
                PublishStatus("state=running;detail=session already active");
                return StartCommandResult.Sticky;
            }

            var saved = LoadRecord();
            var restoring = saved != null;
            SessionRecord record;
            try
            {
                record = saved ?? CreateRecord(intent);
            }
            catch (Exception error)
            {
                var detail = Sanitize(error.Message);
                PublishStatus("state=fault;error=" + detail);
                StopForegroundSession();
                PostFailureNotification(detail);
                StopSelf(startId);
                return StartCommandResult.NotSticky;
            }

            _explicitStopRequested = false;
            _debugLoggingEnabled = record.DebugLoggingEnabled;
            _sessionStop = new CancellationTokenSource();
            _sessionTask = Task.Run(() => RunSessionAsync(record, restoring, _sessionStop.Token));
        }
        return StartCommandResult.Sticky;
    }

    public override IBinder? OnBind(Intent? intent) => null;

    public override void OnDestroy()
    {
        CancellationTokenSource? stop;
        lock (_runtimeSync)
            stop = _sessionStop;
        stop?.Cancel();
        CloseNetworkEndpoints();
        SetCurrentInputSource(null);
        base.OnDestroy();
    }

    internal static Intent CreateStartIntent(
        Context context,
        int containerId,
        string executable,
        string workingDirectory,
        IReadOnlyList<string> arguments,
        string? libraryDirectory,
        string? profileName = null,
        string? inputProtocol = null,
        int controlsProfileId = 0,
        int frameRateLimit = 0,
        int resolutionWidth = 0,
        int resolutionHeight = 0,
        string? displayMode = null,
        bool debugLoggingEnabled = true,
        string? compatibilityPreset = null,
        string? profileConfigIni = null)
    {
        var intent = new Intent(context, typeof(GameSessionService));
        intent.SetAction(StartAction);
        intent.PutExtra(ProfileNameExtra, profileName);
        intent.PutExtra(ContainerExtra, containerId);
        intent.PutExtra(ExecutableExtra, executable);
        intent.PutExtra(WorkingDirectoryExtra, workingDirectory);
        intent.PutExtra(ArgumentsExtra, JsonSerializer.Serialize(arguments));
        intent.PutExtra(LibraryDirectoryExtra, libraryDirectory);
        intent.PutExtra(InputProtocolExtra, inputProtocol);
        intent.PutExtra(ControlsProfileIdExtra, controlsProfileId);
        intent.PutExtra(FrameRateLimitExtra, frameRateLimit);
        intent.PutExtra(ResolutionWidthExtra, resolutionWidth);
        intent.PutExtra(ResolutionHeightExtra, resolutionHeight);
        intent.PutExtra(DisplayModeExtra, displayMode);
        intent.PutExtra(DebugLoggingEnabledExtra, debugLoggingEnabled);
        intent.PutExtra(CompatibilityPresetExtra, compatibilityPreset);
        intent.PutExtra(ProfileConfigIniExtra, profileConfigIni);
        return intent;
    }

    internal static string? TryGetActiveProfileName(Context context)
    {
        try
        {
            var json = context.GetSharedPreferences(PreferencesName, FileCreationMode.Private)
                ?.GetString(SessionRecordKey, null);
            if (string.IsNullOrEmpty(json))
                return null;
            var record = JsonSerializer.Deserialize<SessionRecord>(json);
            if (!IsValidStoredRecord(record) || string.IsNullOrWhiteSpace(record!.ProfileName))
                return null;
            return record.ProfileName;
        }
        catch (Exception error) when (
            error is JsonException or FormatException or InvalidDataException)
        {
            return null;
        }
    }

    /// <summary>
    /// Returns the persisted owner and makes sure Android has a live foreground
    /// service to reconcile it. Package updates and force-stops kill the service
    /// process but deliberately leave the private session record intact; merely
    /// reporting that record to the recreated UI would otherwise advertise a
    /// recovery notification that does not exist.
    /// </summary>
    internal static string? TryRestoreActiveProfileName(Context context)
    {
        var profileName = TryGetActiveProfileName(context);
        if (string.IsNullOrWhiteSpace(profileName))
            return null;

        var status = CurrentStatus;
        if (!string.Equals(status, "state=idle", StringComparison.Ordinal) &&
            !status.StartsWith("state=fault", StringComparison.Ordinal))
            return profileName;

        try
        {
            var applicationContext = context.ApplicationContext ?? context;
            applicationContext.StartForegroundService(
                new Intent(applicationContext, typeof(GameSessionService)));
        }
        catch (Exception error)
        {
            ReportLaunchFailure(
                "Android could not restore the foreground game service: " + error.Message);
        }

        return profileName;
    }

    private async Task RunSessionAsync(
        SessionRecord record,
        bool restoring,
        CancellationToken cancellationToken)
    {
        RetainedWinlatorConnection? connection = null;
        ITeknoParrotWinlatorService? service = null;
        MappedSharedPage? page = null;
        CancellationTokenSource? pageStop = null;
        Task? pageHeartbeat = null;
        var clearRecord = false;
        var stopRemote = false;
        string? failureNotification = null;
        var counters = new SessionCounters();
        var clientTasks = new List<Task>();
        var inputSource = new WinlatorForwardedInputSource(
            RequiresLatchedTestSwitch(record.InputProtocol));

        void PublishTerminalStatus(string status)
        {
            // Android can deliver OnServiceDisconnected after the session has
            // already ended or faulted. Mark the retained binding first so a
            // late callback cannot replace the terminal UI state with an
            // endless "reconnecting" message.
            connection?.MarkTerminal();
            PublishStatus(status);
        }

        try
        {
            PublishStatus(restoring
                ? "state=restoring;detail=reopening authenticated input listener"
                : "state=preparing;detail=opening authenticated input listener");

            var listener = new TcpListener(IPAddress.Loopback, record.Port);
            listener.Start(backlog: 2);
            lock (_runtimeSync)
                _listener = listener;

            if (record.Port == 0)
            {
                record.Port = ((IPEndPoint)listener.LocalEndpoint).Port;
                SaveRecord(record);
            }

            connection = new RetainedWinlatorConnection(this);
            service = await connection.BindAsync(cancellationToken).ConfigureAwait(false);
            lock (_runtimeSync)
            {
                _remoteService = service;
                _remoteSessionId = record.SessionId;
            }
            var serviceProtocolVersion = service.GetProtocolVersion();
            if (!WinlatorSessionContract.IsCompatibleServiceProtocolVersion(serviceProtocolVersion))
                throw new InvalidOperationException(
                    $"Winlator bridge protocol v{serviceProtocolVersion} is unsupported; " +
                    $"expected v{WinlatorSessionContract.MinimumCompatibleServiceProtocolVersion}-" +
                    $"v{WinlatorSessionContract.ServiceProtocolVersion}.");
            _ = WinlatorSessionContract.ParseCapabilities(
                service.GetCapabilities(serviceProtocolVersion),
                serviceProtocolVersion);

            if (restoring)
            {
                var remoteStatus = service.GetSessionStatus(record.SessionId);
                if (!IsRemoteReady(remoteStatus))
                {
                    clearRecord = true;
                    throw new InvalidOperationException(
                        "The saved Winlator game session is no longer active.");
                }
            }
            else
            {
                PublishStatus("state=provisioning;detail=ensuring Winlator and OpenParrot runtime");
                var environment = await EnsureManagedEnvironmentAsync(
                    service,
                    record.ContainerId,
                    cancellationToken).ConfigureAwait(false);
                record.ContainerId = environment.ContainerId;
                SaveRecord(record);
                if (IsCxbxrExecutable(record.Executable) &&
                    environment.CxbxrAvailable == false)
                    throw new InvalidOperationException(
                        "The managed CXBXR runtime is incomplete. Install the matching " +
                        "CXBXR runtime and user-supplied Chihiro firmware before launching this game.");
                PublishStatus("state=preparing;detail=opening authenticated input listener");

                var sessionId = Guid.ParseExact(record.SessionId, "N");
                var token = Convert.FromHexString(record.TokenHex);
                var (pipeName64, pipeName32) = GetProductionPipeNames(record.InputProtocol);
                _ = WinlatorSessionContract.ParsePrepared(
                    service.PrepareSession(WinlatorSessionContract.CreateProductionSpec(
                        sessionId,
                        token,
                        record.ContainerId,
                        record.Port,
                        pipeName64,
                        pipeName32,
                        serviceProtocolVersion)),
                    sessionId,
                    record.ContainerId,
                    record.Port,
                    pipeName64,
                    pipeName32,
                    serviceProtocolVersion);
                record.Prepared = true;
                SaveRecord(record);
            }

            page = MappedSharedPage.Map(OpenSharedPage(service, Guid.ParseExact(record.SessionId, "N")));
            ValidateSharedPage(page);
            if (RequiresJvsBridge(record.InputProtocol))
            {
                var jvsPage = page;
                JvsHelper.ConfigureExternalState(
                    (offset, value) => jvsPage.WriteBytes(checked((int)offset), new[] { value }),
                    () => jvsPage.WriteBytes(0, new byte[BridgeProtocol.LegacySize]));
                var jvsProfile = LoadJvsProfile(record.ProfileName);
                JvsPackageEmulator.Initialize(jvsProfile);
                Array.Clear(JvsPackageEmulator.Coins, 0, JvsPackageEmulator.Coins.Length);
                Array.Clear(JvsPackageEmulator.CoinStates, 0, JvsPackageEmulator.CoinStates.Length);
                JvsSetup.InitializeAnalogBytes(jvsProfile.EmulationProfile);
                JvsSetup.ConfigureJvsPackage(jvsProfile);
                if (record.DebugLoggingEnabled)
                    global::Android.Util.Log.Info(
                        "TeknoParrotJvs",
                        $"profile={record.ProfileName};mode={jvsProfile.EmulationProfile};" +
                        $"version=0x{JvsPackageEmulator.JvsVersion:X2};" +
                        $"switches=0x{JvsPackageEmulator.JvsSwitchCount:X2};" +
                        $"taitoStick={JvsPackageEmulator.TaitoStick}");
            }
            SetCurrentInputSource(inputSource);
            pageStop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            pageHeartbeat = PublishPageHeartbeatAsync(
                page, inputSource, record.InputProtocol, pageStop.Token);

            if (!record.Launched)
            {
                var request = CreateLaunchRequest(record);
                var launchStatus = service.LaunchPreparedActivity(
                    WinlatorSessionContract.CreateActivityLaunch(request, serviceProtocolVersion));
                WinlatorSessionContract.ValidateActivityLaunchStatus(launchStatus, request);
                record.Launched = true;
                SaveRecord(record);
            }

            PublishStatus("state=waiting;detail=Winlator launched, waiting for controls;inputFrames=0");

            var hasConnected = false;
            var criticalPressureSamples = 0;
            while (!cancellationToken.IsCancellationRequested)
            {
                var client = await AcceptWithTimeoutAsync(
                    listener,
                    // Startup is the most allocation-heavy part of Wine/Box64/DXVK.
                    // Sample it at the same cadence as live gameplay so Android can
                    // stop a runaway launch before the whole device enters swap thrash.
                    HealthSampleInterval,
                    cancellationToken).ConfigureAwait(false);
                if (client == null)
                {
                    var hasPressureWarning = false;
                    if (TryReadDevicePressure(out var pressure))
                    {
                        if (pressure.IsCritical)
                        {
                            criticalPressureSamples++;
                            if (criticalPressureSamples == 1)
                            {
                                var reason = pressure.LowMemory
                                    ? "Android reports low memory"
                                    : $"Android reports critical heat (thermal status {pressure.ThermalStatus})";
                                PublishStatus(
                                    $"state=pressure;detail={reason}, close other apps and let the device cool;" +
                                    $"availableMiB={pressure.AvailableMiB};" +
                                    $"thresholdMiB={pressure.ThresholdMiB};" +
                                    $"thermal={pressure.ThermalStatus};inputFrames={counters.Frames}");
                            }
                            if (criticalPressureSamples >= 3)
                            {
                                var reason = pressure.LowMemory
                                    ? "sustained low-memory pressure"
                                    : $"sustained critical heat (thermal status {pressure.ThermalStatus})";
                                failureNotification =
                                    $"Android stopped the game after {reason} " +
                                    $"({pressure.AvailableMiB} MiB available; " +
                                    $"low-memory threshold {pressure.ThresholdMiB} MiB). " +
                                    "Close other apps, let the device cool down, and retry.";
                                stopRemote = true;
                                clearRecord = true;
                                PublishTerminalStatus("state=fault;error=" +
                                    Sanitize(failureNotification) +
                                    $";inputFrames={counters.Frames}");
                                try
                                {
                                    service.StopTestSession(record.SessionId);
                                }
                                catch
                                {
                                    // The normal finally path retries this best-effort stop.
                                }
                                break;
                            }
                        }
                        else
                        {
                            var recoveredFromCriticalPressure = criticalPressureSamples > 0;
                            if (criticalPressureSamples > 0)
                                criticalPressureSamples = 0;
                            if (pressure.ShouldWarn)
                            {
                                hasPressureWarning = true;
                                var reason = pressure.HasLowMemoryHeadroom
                                    ? "Android memory headroom is low"
                                    : $"Android reports severe heat (thermal status {pressure.ThermalStatus})";
                                PublishStatus(
                                    $"state=pressure;detail={reason};availableMiB={pressure.AvailableMiB};" +
                                    $"thresholdMiB={pressure.ThresholdMiB};thermal={pressure.ThermalStatus};" +
                                    $"inputFrames={counters.Frames}");
                            }
                            else if (recoveredFromCriticalPressure)
                            {
                                PublishStatus(hasConnected
                                    ? $"state=connected;detail=device pressure recovered;inputFrames={counters.Frames};activePipes={Volatile.Read(ref counters.ActivePipeChannels)}"
                                    : "state=waiting;detail=device pressure recovered, Winlator is still starting;inputFrames=0");
                            }
                        }
                    }
                    else if (criticalPressureSamples > 0)
                    {
                        // A failed sample breaks the consecutive-critical sequence;
                        // never stop a game using stale resource telemetry.
                        criticalPressureSamples = 0;
                        PublishStatus(hasConnected
                            ? $"state=connected;detail=device pressure telemetry unavailable;inputFrames={counters.Frames};activePipes={Volatile.Read(ref counters.ActivePipeChannels)}"
                            : "state=waiting;detail=device pressure telemetry unavailable, Winlator is still starting;inputFrames=0");
                    }

                    if (!IsRemoteReady(service.GetSessionStatus(record.SessionId)))
                    {
                        clearRecord = true;
                        PublishTerminalStatus(
                            $"state=ended;detail=Winlator game closed;inputFrames={counters.Frames}");
                        break;
                    }
                    if (criticalPressureSamples > 0 || hasPressureWarning)
                        continue;
                    // Accept() timing out only means that no *new* channel
                    // arrived. Existing controls and game-pipe clients run on
                    // their own tasks, so do not misreport a healthy session as
                    // reconnecting every five seconds while they are active.
                    if (Volatile.Read(ref counters.ActiveInputChannels) > 0)
                        continue;
                    PublishStatus(hasConnected
                        ? $"state=reconnecting;detail=waiting for Winlator controls;inputFrames={counters.Frames};activePipes={Volatile.Read(ref counters.ActivePipeChannels)}"
                        : "state=waiting;detail=Winlator is still starting;inputFrames=0");
                    continue;
                }

                hasConnected = true;
                var clientTask = HandleAuthenticatedClientAsync(
                    client, record, inputSource, counters, cancellationToken);
                clientTasks.Add(clientTask);
                for (var index = clientTasks.Count - 1; index >= 0; index--)
                {
                    if (clientTasks[index].IsCompleted)
                        clientTasks.RemoveAt(index);
                }
            }

            // An explicit stop can race a clean peer EOF. In that case the
            // loop condition observes cancellation without any awaited read
            // throwing, so route the normal exit through the same cleanup and
            // stopped-status path as an interrupted read.
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (global::System.OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            stopRemote = _explicitStopRequested;
            if (_explicitStopRequested)
            {
                clearRecord = true;
                PublishTerminalStatus(
                    $"state=stopped;detail=stopped by user;inputFrames={counters.Frames}");
            }
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            // Closing TcpClient/TcpListener is how RequestStop interrupts the
            // synchronous allocation-free TPI1 reader. Depending on where it
            // is blocked, that surfaces as IOException or SocketException.
            stopRemote = _explicitStopRequested;
            if (_explicitStopRequested)
            {
                clearRecord = true;
                PublishTerminalStatus(
                    $"state=stopped;detail=stopped by user;inputFrames={counters.Frames}");
            }
        }
        catch (Exception error)
        {
            stopRemote = !restoring || _explicitStopRequested;
            clearRecord |= stopRemote;
            failureNotification = Sanitize(error.Message);
            PublishTerminalStatus(
                "state=fault;error=" + failureNotification + $";inputFrames={counters.Frames}");
        }
        finally
        {
            pageStop?.Cancel();
            if (pageHeartbeat != null)
            {
                try
                {
                    await pageHeartbeat.ConfigureAwait(false);
                }
                catch (global::System.OperationCanceledException)
                {
                }
            }
            pageStop?.Dispose();
            if (AndroidLaunchRecipe.IsJvsInputProtocol(record.InputProtocol))
                JvsHelper.ConfigureExternalState(null, null);
            page?.Dispose();
            SetCurrentInputSource(null);
            CloseNetworkEndpoints();
            if (clientTasks.Count > 0)
            {
                try
                {
                    await Task.WhenAll(clientTasks).ConfigureAwait(false);
                }
                catch (Exception) when (cancellationToken.IsCancellationRequested)
                {
                }
            }

            if (stopRemote && service != null)
            {
                try
                {
                    service.StopTestSession(record.SessionId);
                }
                catch
                {
                    // Winlator may already have closed its side of the session.
                }
            }

            connection?.Dispose();
            if (clearRecord)
                ClearRecord();

            lock (_runtimeSync)
            {
                _remoteService = null;
                _remoteSessionId = null;
                _sessionStop?.Dispose();
                _sessionStop = null;
                _sessionTask = null;
            }
            StopForegroundSession();
            if (failureNotification != null)
                PostFailureNotification(failureNotification);
            StopSelf();
        }
    }

    private bool TryReadDevicePressure(out DevicePressureSnapshot snapshot)
    {
        snapshot = default;
        try
        {
            var manager = GetSystemService(ActivityService) as ActivityManager;
            if (manager == null)
                return false;
            var info = new ActivityManager.MemoryInfo();
            manager.GetMemoryInfo(info);
            const long bytesPerMiB = 1024L * 1024L;
            var thermalStatus = 0;
            if (OperatingSystem.IsAndroidVersionAtLeast(29))
            {
                var powerManager = GetSystemService(PowerService) as PowerManager;
                thermalStatus = (int)(powerManager?.CurrentThermalStatus ?? ThermalStatus.None);
            }
            snapshot = new DevicePressureSnapshot(
                info.AvailMem / bytesPerMiB,
                info.Threshold / bytesPerMiB,
                info.LowMemory,
                thermalStatus);
            return true;
        }
        catch
        {
            // Resource telemetry must never make an otherwise healthy game fail.
            return false;
        }
    }

    private async Task HandleAuthenticatedClientAsync(
        TcpClient client,
        SessionRecord record,
        WinlatorForwardedInputSource inputSource,
        SessionCounters counters,
        CancellationToken cancellationToken)
    {
        var isInputChannel = false;
        var authenticatedChannelName = string.Empty;
        var registeredActiveChannel = false;
        lock (_runtimeSync)
            _activeClients.Add(client);
        try
        {
            client.NoDelay = true;
            using var stream = client.GetStream();
            var fixedHeader = new byte[58];
            await BridgeProtocol.ReadExactlyAsync(stream, fixedHeader, cancellationToken)
                .ConfigureAwait(false);
            var channelKind = BinaryPrimitives.ReadUInt16BigEndian(fixedHeader.AsSpan(6, 2));
            var channelNameLength = BinaryPrimitives.ReadUInt16BigEndian(fixedHeader.AsSpan(56, 2));
            if (channelNameLength == 0 || channelNameLength > BridgeProtocol.MaxPipeNameBytes)
                throw new InvalidDataException("Winlator sent an invalid bridge channel name.");

            var channelName = new byte[channelNameLength];
            await BridgeProtocol.ReadExactlyAsync(stream, channelName, cancellationToken)
                .ConfigureAwait(false);
            string expectedName;
            if (channelKind == BridgeProtocol.ForwardedInputChannelKind)
            {
                expectedName = BridgeProtocol.ProbeInputChannelName;
                isInputChannel = true;
            }
            else if (channelKind == BridgeProtocol.NamedPipeChannelKind)
            {
                var decodedName = System.Text.Encoding.UTF8.GetString(channelName);
                var (pipeName64, pipeName32) = GetProductionPipeNames(record.InputProtocol);
                var isAdditionalJvsPipe =
                    (record.InputProtocol == AndroidLaunchRecipe.InputProtocolAllsIdta ||
                     IsSharedStateWithJvs(record.InputProtocol)) &&
                    (decodedName == WinlatorSessionContract.ProductionJvsPipe64 ||
                     decodedName == WinlatorSessionContract.ProductionJvsPipe32);
                if (decodedName != pipeName64 && decodedName != pipeName32 &&
                    !isAdditionalJvsPipe)
                    throw new UnauthorizedAccessException(
                        "Winlator requested a pipe outside the prepared game protocol.");
                expectedName = decodedName;
                authenticatedChannelName = decodedName;
            }
            else
            {
                throw new UnauthorizedAccessException("Winlator requested an unsupported bridge channel.");
            }

            if (!BridgeProtocol.ValidateAuthenticatedHandshake(
                    fixedHeader,
                    channelName,
                    Guid.ParseExact(record.SessionId, "N"),
                    Convert.FromHexString(record.TokenHex),
                    channelKind,
                    expectedName,
                    out var error))
                throw new UnauthorizedAccessException("Winlator bridge authentication failed: " + error);

            await stream.WriteAsync(BridgeProtocol.PipeAck, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            if (isInputChannel)
            {
                Interlocked.Increment(ref counters.ActiveInputChannels);
                registeredActiveChannel = true;
                PublishStatus($"state=connected;detail=controls authenticated;inputFrames={counters.Frames}");
                var reader = new ForwardedInputStreamReader(stream);
                while (await reader.ReadAndApplyAsync(inputSource, cancellationToken).ConfigureAwait(false))
                    Interlocked.Increment(ref counters.Frames);
            }
            else
            {
                Interlocked.Increment(ref counters.ActivePipeChannels);
                registeredActiveChannel = true;
                Interlocked.Increment(ref counters.PipeConnections);
                var isJvsChannel = IsJvsChannel(
                    record.InputProtocol, authenticatedChannelName);
                if (record.DebugLoggingEnabled && isJvsChannel)
                    global::Android.Util.Log.Info(
                        "TeknoParrotJvs", $"pipe authenticated: {authenticatedChannelName}");
                PublishStatus($"state=connected;detail=game pipe authenticated;inputFrames={counters.Frames}");
                if (isJvsChannel)
                    await WriteJvsRepliesAsync(
                            stream, inputSource,
                            record.InputProtocol switch
                            {
                                AndroidLaunchRecipe.InputProtocolAllsIdta =>
                                    AndroidLaunchRecipe.InputProtocolJvsInitialD,
                                AndroidLaunchRecipe.InputProtocolSharedEadp =>
                                    AndroidLaunchRecipe.InputProtocolJvs,
                                AndroidLaunchRecipe.InputProtocolSharedWonderlandWars =>
                                    AndroidLaunchRecipe.InputProtocolJvs,
                                AndroidLaunchRecipe.InputProtocolSharedTaitoGun =>
                                    AndroidLaunchRecipe.InputProtocolJvs,
                                _ => record.InputProtocol
                            },
                            record.DebugLoggingEnabled, cancellationToken)
                        .ConfigureAwait(false);
                else if (AndroidLaunchRecipe.IsFastIoInputProtocol(record.InputProtocol))
                    await WriteFastIoReportsAsync(
                            stream, inputSource, record.InputProtocol, cancellationToken)
                        .ConfigureAwait(false);
                else if (record.InputProtocol == AndroidLaunchRecipe.InputProtocolAllsIdta)
                    await WriteAllsIdtaReportsAsync(stream, inputSource, cancellationToken)
                        .ConfigureAwait(false);
                else if (record.InputProtocol == AndroidLaunchRecipe.InputProtocolApm3)
                    await WriteApm3ReportsAsync(stream, inputSource, cancellationToken)
                        .ConfigureAwait(false);
                else if (AndroidLaunchRecipe.IsSharedStateInputProtocol(record.InputProtocol))
                    await HoldSharedStatePipeAsync(stream, cancellationToken).ConfigureAwait(false);
                else
                    await WriteSegaRallyReportsAsync(stream, inputSource, cancellationToken)
                        .ConfigureAwait(false);
            }
        }
        catch (global::System.OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception error) when (!cancellationToken.IsCancellationRequested)
        {
            PublishStatus("state=reconnecting;detail=" + Sanitize(error.Message) +
                          $";inputFrames={counters.Frames}");
        }
        finally
        {
            if (registeredActiveChannel)
            {
                if (isInputChannel)
                    Interlocked.Decrement(ref counters.ActiveInputChannels);
                else
                    Interlocked.Decrement(ref counters.ActivePipeChannels);
            }
            lock (_runtimeSync)
                _activeClients.Remove(client);
            client.Dispose();
            if (isInputChannel)
                inputSource.ReleaseAll();
        }
    }

    private static async Task WriteSegaRallyReportsAsync(
        Stream stream,
        WinlatorForwardedInputSource inputSource,
        CancellationToken cancellationToken)
    {
        var buttons = new uint[WinlatorForwardedInputSource.MaximumPlayers];
        var axes = new short[
            WinlatorForwardedInputSource.MaximumPlayers * WinlatorForwardedInputSource.MaximumAxes];
        var report = new byte[15];
        while (!cancellationToken.IsCancellationRequested)
        {
            inputSource.CopyAggregateState(buttons, axes);
            Array.Clear(report, 0, report.Length);
            var playerButtons = buttons[0];
            var steering = AxisToByte(axes[0]);
            if (IsPressed(playerButtons, ForwardedInputButton.Left))
                steering = 0;
            else if (IsPressed(playerButtons, ForwardedInputButton.Right))
                steering = byte.MaxValue;

            report[1] = steering;
            report[2] = steering;
            // Match SegaRallyPipe's native SR3 report layout: byte 3 is the
            // brake channel and byte 4 is the accelerator channel.
            report[3] = TriggerToByte(axes[4]); // Android left trigger: brake
            report[4] = TriggerToByte(axes[5]); // Android right trigger: gas
            if (IsPressed(playerButtons, ForwardedInputButton.Start)) report[6] |= 0x01;
            if (IsPressed(playerButtons, ForwardedInputButton.Button3)) report[6] |= 0x02;
            if (IsPressed(playerButtons, ForwardedInputButton.Button4)) report[6] |= 0x04;
            if (IsPressed(playerButtons, ForwardedInputButton.Button1)) report[6] |= 0x08;
            if (IsPressed(playerButtons, ForwardedInputButton.Button2)) report[6] |= 0x10;
            report[7] = 0x0C;

            await stream.WriteAsync(report, cancellationToken).ConfigureAwait(false);
            await Task.Delay(15, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task WriteFastIoReportsAsync(
        Stream stream,
        WinlatorForwardedInputSource inputSource,
        string inputProtocol,
        CancellationToken cancellationToken)
    {
        var buttons = new uint[WinlatorForwardedInputSource.MaximumPlayers];
        var axes = new short[
            WinlatorForwardedInputSource.MaximumPlayers * WinlatorForwardedInputSource.MaximumAxes];
        var report = new byte[64];
        while (!cancellationToken.IsCancellationRequested)
        {
            inputSource.CopyAggregateState(buttons, axes);
            if (inputProtocol == AndroidLaunchRecipe.InputProtocolFastIoTheatrhythm)
                AndroidFastIoInputEncoder.BuildReport(
                    inputProtocol, buttons, axes, report);
            else
                BuildFastIoReport(buttons, axes, report);
            await stream.WriteAsync(report, cancellationToken).ConfigureAwait(false);
            await Task.Delay(15, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task WriteApm3ReportsAsync(
        Stream stream,
        WinlatorForwardedInputSource inputSource,
        CancellationToken cancellationToken)
    {
        var buttons = new uint[WinlatorForwardedInputSource.MaximumPlayers];
        var axes = new short[
            WinlatorForwardedInputSource.MaximumPlayers * WinlatorForwardedInputSource.MaximumAxes];
        var report = new byte[AndroidApm3InputEncoder.ReportSize];
        while (!cancellationToken.IsCancellationRequested)
        {
            inputSource.CopyAggregateState(buttons, axes);
            AndroidApm3InputEncoder.BuildReport(buttons, axes, report);
            await stream.WriteAsync(report, cancellationToken).ConfigureAwait(false);
            await Task.Delay(15, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task WriteAllsIdtaReportsAsync(
        Stream stream,
        WinlatorForwardedInputSource inputSource,
        CancellationToken cancellationToken)
    {
        var buttons = new uint[WinlatorForwardedInputSource.MaximumPlayers];
        var axes = new short[
            WinlatorForwardedInputSource.MaximumPlayers * WinlatorForwardedInputSource.MaximumAxes];
        var report = new byte[AndroidAllsIdtaInputEncoder.ReportSize];
        var encoder = new AndroidAllsIdtaInputEncoder();
        while (!cancellationToken.IsCancellationRequested)
        {
            inputSource.CopyAggregateState(buttons, axes);
            encoder.BuildReport(buttons, axes, report);
            await stream.WriteAsync(report, cancellationToken).ConfigureAwait(false);
            await Task.Delay(15, cancellationToken).ConfigureAwait(false);
        }
    }

    private static void BuildFastIoReport(
        ReadOnlySpan<uint> buttons,
        ReadOnlySpan<short> axes,
        Span<byte> report)
    {
        report.Clear();

        if (DirectionPressed(buttons, axes, 0, ForwardedInputButton.Left)) report[1] |= 0x10;
        if (DirectionPressed(buttons, axes, 0, ForwardedInputButton.Right)) report[1] |= 0x40;
        if (DirectionPressed(buttons, axes, 0, ForwardedInputButton.Down)) report[1] |= 0x04;
        if (DirectionPressed(buttons, axes, 0, ForwardedInputButton.Up)) report[1] |= 0x01;
        if (IsPressed(buttons[0], ForwardedInputButton.Start)) report[0] |= 0x10;
        if (IsPressed(buttons[0], ForwardedInputButton.Button1)) report[2] |= 0x01;
        if (IsPressed(buttons[0], ForwardedInputButton.Button2)) report[2] |= 0x04;
        if (IsPressed(buttons[0], ForwardedInputButton.Button3)) report[2] |= 0x10;
        if (IsPressed(buttons[0], ForwardedInputButton.Button4)) report[2] |= 0x40;
        if (IsPressed(buttons[0], ForwardedInputButton.Button5)) report[3] |= 0x01;
        if (IsPressed(buttons[0], ForwardedInputButton.Button6)) report[3] |= 0x04;
        if (IsPressed(buttons[0], ForwardedInputButton.Test)) report[0] |= 0x40;
        if (IsPressed(buttons[0], ForwardedInputButton.Service)) report[0] |= 0x04;

        if (DirectionPressed(buttons, axes, 1, ForwardedInputButton.Left)) report[1] |= 0x20;
        if (DirectionPressed(buttons, axes, 1, ForwardedInputButton.Right)) report[1] |= 0x80;
        if (DirectionPressed(buttons, axes, 1, ForwardedInputButton.Down)) report[1] |= 0x08;
        if (DirectionPressed(buttons, axes, 1, ForwardedInputButton.Up)) report[1] |= 0x02;
        if (IsPressed(buttons[1], ForwardedInputButton.Start)) report[0] |= 0x20;
        if (IsPressed(buttons[1], ForwardedInputButton.Button1)) report[2] |= 0x02;
        if (IsPressed(buttons[1], ForwardedInputButton.Button2)) report[2] |= 0x08;
        if (IsPressed(buttons[1], ForwardedInputButton.Button3)) report[2] |= 0x20;
        if (IsPressed(buttons[1], ForwardedInputButton.Button4)) report[2] |= 0x80;
        if (IsPressed(buttons[1], ForwardedInputButton.Button5)) report[3] |= 0x02;
        if (IsPressed(buttons[1], ForwardedInputButton.Button6)) report[3] |= 0x08;
        if (IsPressed(buttons[1], ForwardedInputButton.Service)) report[0] |= 0x08;
        if (IsPressed(buttons[0], ForwardedInputButton.Coin)) report[4] = 1;

        if (DirectionPressed(buttons, axes, 2, ForwardedInputButton.Left)) report[11] |= 0x10;
        if (DirectionPressed(buttons, axes, 2, ForwardedInputButton.Right)) report[11] |= 0x40;
        if (DirectionPressed(buttons, axes, 2, ForwardedInputButton.Down)) report[11] |= 0x04;
        if (DirectionPressed(buttons, axes, 2, ForwardedInputButton.Up)) report[11] |= 0x01;
        if (IsPressed(buttons[2], ForwardedInputButton.Start)) report[10] |= 0x10;
        if (IsPressed(buttons[2], ForwardedInputButton.Button1)) report[12] |= 0x01;
        if (IsPressed(buttons[2], ForwardedInputButton.Button2)) report[12] |= 0x04;
        if (IsPressed(buttons[2], ForwardedInputButton.Button3)) report[12] |= 0x10;
        if (IsPressed(buttons[2], ForwardedInputButton.Button4)) report[12] |= 0x40;
        if (IsPressed(buttons[2], ForwardedInputButton.Button5)) report[13] |= 0x01;
        if (IsPressed(buttons[2], ForwardedInputButton.Button6)) report[13] |= 0x04;
        if (IsPressed(buttons[2], ForwardedInputButton.Test)) report[10] |= 0x40;
        if (IsPressed(buttons[2], ForwardedInputButton.Service)) report[10] |= 0x04;

        if (DirectionPressed(buttons, axes, 3, ForwardedInputButton.Left)) report[11] |= 0x20;
        if (DirectionPressed(buttons, axes, 3, ForwardedInputButton.Right)) report[11] |= 0x80;
        if (DirectionPressed(buttons, axes, 3, ForwardedInputButton.Down)) report[11] |= 0x08;
        if (DirectionPressed(buttons, axes, 3, ForwardedInputButton.Up)) report[11] |= 0x02;
        if (IsPressed(buttons[3], ForwardedInputButton.Start)) report[10] |= 0x20;
        if (IsPressed(buttons[3], ForwardedInputButton.Button1)) report[12] |= 0x02;
        if (IsPressed(buttons[3], ForwardedInputButton.Button2)) report[12] |= 0x08;
        if (IsPressed(buttons[3], ForwardedInputButton.Button3)) report[12] |= 0x20;
        if (IsPressed(buttons[3], ForwardedInputButton.Button4)) report[12] |= 0x80;
        if (IsPressed(buttons[3], ForwardedInputButton.Button5)) report[13] |= 0x02;
        if (IsPressed(buttons[3], ForwardedInputButton.Button6)) report[13] |= 0x08;
        if (IsPressed(buttons[3], ForwardedInputButton.Service)) report[10] |= 0x08;
        if (IsPressed(buttons[2], ForwardedInputButton.Coin)) report[14] = 1;

        PublishFastIoAxis(axes, 0, report, 8, 9);
        PublishFastIoAxis(axes, 2, report, 15, 16);
        PublishFastIoAxis(axes, 1, report, 17, 18);
        PublishFastIoAxis(axes, 3, report, 19, 20);
    }

    private static void PublishFastIoAxis(
        ReadOnlySpan<short> axes,
        int player,
        Span<byte> report,
        int xOffset,
        int yOffset)
    {
        var source = player * WinlatorForwardedInputSource.MaximumAxes;
        if (axes[source] != 0)
            report[xOffset] = AxisToByte(axes[source]);
        if (axes[source + 1] != 0)
            report[yOffset] = AxisToByte(axes[source + 1]);
    }

    private static async Task WriteJvsRepliesAsync(
        Stream stream,
        WinlatorForwardedInputSource inputSource,
        string inputProtocol,
        bool debugLoggingEnabled,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[1024];
        var decoder = new JvsPacketDecoder();
        var readBatchCount = 0;
        var packetCount = 0;
        var previousSwitchState = string.Empty;
        while (!cancellationToken.IsCancellationRequested)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
                return;

            readBatchCount++;
            if (debugLoggingEnabled && readBatchCount <= 4)
                global::Android.Util.Log.Info(
                    "TeknoParrotJvs",
                    $"raw[{readBatchCount}]={Convert.ToHexString(buffer.AsSpan(0, read))}");

            for (var index = 0; index < read; index++)
            {
                if (!decoder.TryPush(buffer[index], out var request))
                    continue;
                packetCount++;
                inputSource.PublishControlsToJvsInputCode(inputProtocol);
                var reply = JvsPackageEmulator.GetReply(request);
                var switchState =
                    $"{JvsPackageEmulator.GetSpecialBits(0):X2}" +
                    $"{JvsPackageEmulator.GetPlayerControls(0):X2}" +
                    $"{JvsPackageEmulator.GetPlayerControlsExt(0):X2}" +
                    $"{JvsPackageEmulator.GetPlayerControls(1):X2}" +
                    $"{JvsPackageEmulator.GetPlayerControlsExt(1):X2}";
                if (debugLoggingEnabled &&
                    (packetCount <= 12 || switchState != previousSwitchState))
                {
                    global::Android.Util.Log.Info(
                        "TeknoParrotJvs",
                        $"packet={packetCount};request={Convert.ToHexString(request)};" +
                        $"reply={Convert.ToHexString(reply)};switches={switchState};" +
                        $"coins={JvsPackageEmulator.Coins[0]},{JvsPackageEmulator.Coins[1]}");
                    previousSwitchState = switchState;
                }
                if (reply.Length == 0)
                    continue;
                await stream.WriteAsync(reply, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static byte AxisToByte(short value) =>
        (byte)(((long)value - short.MinValue) * byte.MaxValue / ushort.MaxValue);

    private static byte TriggerToByte(short value) =>
        (byte)(Math.Clamp((int)value, 0, short.MaxValue) * byte.MaxValue / short.MaxValue);

    private static bool IsPressed(uint state, ForwardedInputButton button) =>
        (state & (1u << (int)button)) != 0;

    private const int DigitalAxisThreshold = 12_000;

    private static bool DirectionPressed(
        ReadOnlySpan<uint> buttons,
        ReadOnlySpan<short> axes,
        int player,
        ForwardedInputButton button)
    {
        if (IsPressed(buttons[player], button))
            return true;
        var offset = player * WinlatorForwardedInputSource.MaximumAxes;
        return button switch
        {
            ForwardedInputButton.Left =>
                axes[offset] <= -DigitalAxisThreshold || axes[offset + 6] <= -DigitalAxisThreshold,
            ForwardedInputButton.Right =>
                axes[offset] >= DigitalAxisThreshold || axes[offset + 6] >= DigitalAxisThreshold,
            ForwardedInputButton.Up =>
                axes[offset + 1] <= -DigitalAxisThreshold || axes[offset + 7] <= -DigitalAxisThreshold,
            ForwardedInputButton.Down =>
                axes[offset + 1] >= DigitalAxisThreshold || axes[offset + 7] >= DigitalAxisThreshold,
            _ => false
        };
    }

    private async Task<WinlatorManagedEnvironment> EnsureManagedEnvironmentAsync(
        ITeknoParrotWinlatorService service,
        int preferredContainerId,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.AddMinutes(5);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var environmentEnvelope = service.EnsureTeknoParrotEnvironment(preferredContainerId);
            WinlatorManagedEnvironment environment;
            try
            {
                environment = WinlatorSessionContract.ParseManagedEnvironment(environmentEnvelope);
            }
            catch (Exception error) when (error is JsonException or InvalidOperationException)
            {
                throw new InvalidOperationException(
                    "Winlator provisioning returned " + Sanitize(environmentEnvelope), error);
            }
            if (environment.IsReady)
                return environment;
            if (environment.NeedsRuntimePackages)
                throw new InvalidOperationException(
                    "The Winlator runtime is not installed. Open Updates and install " +
                    "the Android OpenParrot/TeknoParrot packages required by this game.");
            if (!environment.NeedsStoragePermission)
                throw new InvalidOperationException("Winlator returned an invalid provisioning state.");
            if (DateTime.UtcNow >= deadline)
                throw new TimeoutException("Android storage permission was not granted to Winlator.");

            PublishStatus(
                "state=permission;detail=allow game-folder access in the Android permission prompt");
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<TcpClient?> AcceptWithTimeoutAsync(
        TcpListener listener,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var timeoutStop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutStop.CancelAfter(timeout);
        try
        {
            return await listener.AcceptTcpClientAsync(timeoutStop.Token).ConfigureAwait(false);
        }
        catch (global::System.OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    private static async Task PublishPageHeartbeatAsync(
        MappedSharedPage page,
        WinlatorForwardedInputSource inputSource,
        string inputProtocol,
        CancellationToken cancellationToken)
    {
        var buttons = new uint[WinlatorForwardedInputSource.MaximumPlayers];
        var axes = new short[
            WinlatorForwardedInputSource.MaximumPlayers * WinlatorForwardedInputSource.MaximumAxes];
        var coin = new byte[1];
        var sharedState = new byte[AndroidSharedStateInputEncoder.ReportSize];
        var pointers = new ForwardedPointerState[
            WinlatorForwardedInputSource.MaximumPlayers];
        var cxbxrWmmtState =
            inputProtocol == AndroidLaunchRecipe.InputProtocolSharedCxbxrWmmt
                ? new AndroidCxbxrWmmtInputState()
                : null;
        while (!cancellationToken.IsCancellationRequested)
        {
            inputSource.CopyAggregateState(buttons, axes, pointers);
            if (inputProtocol == AndroidLaunchRecipe.InputProtocolSegaRally)
            {
                coin[0] = IsPressed(buttons[0], ForwardedInputButton.Coin) ? (byte)1 : (byte)0;
                page.WriteBytes(4, coin);
            }
            else if (inputProtocol == AndroidLaunchRecipe.InputProtocolJvsMkdx)
            {
                // BanapassButton writes this DWORD in the desktop ControlSender
                // while COM3 carries the rest of MKDX's JVS traffic.
                page.WriteBytes(
                    BridgeProtocol.LegacyOffset + 8,
                    BitConverter.GetBytes(
                        IsPressed(buttons[0], ForwardedInputButton.Button4) ? 1 : 0));
            }
            else if (inputProtocol == AndroidLaunchRecipe.InputProtocolAllsIdta)
            {
                // AimeButton publishes the card-insert switch beside the
                // ALLS USB-I/O pipe report in TeknoParrot_JvsState.
                page.WriteBytes(
                    BridgeProtocol.LegacyOffset + 32,
                    BitConverter.GetBytes(
                        IsPressed(buttons[0], ForwardedInputButton.Button4) ? 1 : 0));
            }
            else if (AndroidLaunchRecipe.IsSharedStateInputProtocol(inputProtocol))
            {
                AndroidSharedStateInputEncoder.BuildReport(
                    inputProtocol, buttons, axes, pointers, sharedState,
                    cxbxrWmmtState);
                if (IsSharedStateWithJvs(inputProtocol))
                {
                    // These profiles use this page for title-specific controls
                    // while a second COM port carries JVS. Byte zero is the JVS
                    // sense line and is owned by JvsPackageEmulator; rewriting
                    // the encoder's cleared byte every 15 ms makes the game
                    // enumerate phantom nodes after SETADDR 01.
                    page.WriteBytes(
                        BridgeProtocol.LegacyOffset + 1,
                        sharedState.AsSpan(1));
                }
                else
                {
                    page.WriteBytes(BridgeProtocol.LegacyOffset, sharedState);
                }
            }
            page.WriteUInt64(BridgeProtocol.HostTimestampOffset, BridgeProtocol.MonotonicNanoseconds());
            page.WriteUInt32(
                BridgeProtocol.HostSequenceOffset,
                unchecked(page.ReadUInt32(BridgeProtocol.HostSequenceOffset) + 1));
            page.WriteUInt32(
                BridgeProtocol.FlagsOffset,
                page.ReadUInt32(BridgeProtocol.FlagsOffset) | BridgeProtocol.FlagHostReady);
            await Task.Delay(
                AndroidLaunchRecipe.IsSharedStateInputProtocol(inputProtocol) ? 15 : 50,
                cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task HoldSharedStatePipeAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        // These games read TeknoParrot_JvsState directly. The production pipe
        // remains available because the Winlator helper owns both resources,
        // but there is intentionally no byte-report protocol on this channel.
        var buffer = new byte[256];
        while (!cancellationToken.IsCancellationRequested)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
                return;
        }
    }

    private static void ValidateSharedPage(MappedSharedPage page)
    {
        if (!page.ReadBytes(BridgeProtocol.MagicOffset, 4).SequenceEqual(BridgeProtocol.SharedPageMagic) ||
            page.ReadUInt16(BridgeProtocol.LayoutVersionOffset) != BridgeProtocol.ProtocolVersion ||
            page.ReadUInt32(BridgeProtocol.TotalSizeOffset) != BridgeProtocol.PageSize)
            throw new InvalidDataException("Winlator returned an incompatible TPJ1 shared page.");
    }

    private static ParcelFileDescriptor OpenSharedPage(
        ITeknoParrotWinlatorService service,
        Guid sessionId)
    {
        var data = Parcel.Obtain();
        var reply = Parcel.Obtain();
        try
        {
            data.WriteInterfaceToken(BridgeProtocol.WinlatorInterfaceDescriptor);
            data.WriteString(sessionId.ToString("N"));
            var remote = service.AsBinder()
                         ?? throw new InvalidOperationException("Winlator service has no Binder handle.");
            if (!remote.Transact(BridgeProtocol.OpenWinlatorSharedPageTransaction, data, reply, 0))
                throw new InvalidOperationException("Winlator rejected the shared-page Binder transaction.");
            reply.ReadException();
            return reply.ReadFileDescriptor()
                   ?? throw new InvalidOperationException("Winlator returned no shared-page descriptor.");
        }
        finally
        {
            reply.Recycle();
            data.Recycle();
        }
    }

    private static WinlatorActivityLaunchRequest CreateLaunchRequest(SessionRecord record) =>
        new(
            Guid.ParseExact(record.SessionId, "N"),
            record.ContainerId,
            WinlatorSessionContract.WindowsExecutableLaunchKind,
            record.Executable,
            record.WorkingDirectory,
            record.Arguments,
            record.LibraryDirectory,
            record.ControlsProfileId,
            record.FrameRateLimit,
            record.ResolutionWidth,
            record.ResolutionHeight,
            record.DebugLoggingEnabled,
            record.CompatibilityPreset,
            record.DisplayMode,
            record.ProfileConfigIni);

    private static bool IsRemoteReady(string? status) =>
        status?.StartsWith("state=ready;", StringComparison.Ordinal) == true;

    private static bool IsCxbxrExecutable(string? executable) =>
        executable?.Replace('/', '\\').EndsWith(
            "\\cxbxr-ldr.exe", StringComparison.OrdinalIgnoreCase) == true;

    private static (string Pipe64, string Pipe32) GetProductionPipeNames(string inputProtocol)
    {
        ValidateInputProtocol(inputProtocol);
        return AndroidLaunchRecipe.IsJvsInputProtocol(inputProtocol)
            ? (WinlatorSessionContract.ProductionJvsPipe64,
               WinlatorSessionContract.ProductionJvsPipe32)
            : (WinlatorSessionContract.ProductionPipe64,
               WinlatorSessionContract.ProductionPipe32);
    }

    private static bool RequiresJvsBridge(string inputProtocol) =>
        AndroidLaunchRecipe.IsJvsInputProtocol(inputProtocol) ||
        inputProtocol == AndroidLaunchRecipe.InputProtocolAllsIdta ||
        IsSharedStateWithJvs(inputProtocol);

    private static bool RequiresLatchedTestSwitch(string inputProtocol) =>
        inputProtocol == AndroidLaunchRecipe.InputProtocolJvsWmmt ||
        inputProtocol == AndroidLaunchRecipe.InputProtocolJvsMkdx ||
        inputProtocol == AndroidLaunchRecipe.InputProtocolJvsMachStorm;

    private static bool IsSharedStateWithJvs(string inputProtocol) =>
        inputProtocol == AndroidLaunchRecipe.InputProtocolSharedEadp ||
        inputProtocol == AndroidLaunchRecipe.InputProtocolSharedWonderlandWars ||
        inputProtocol == AndroidLaunchRecipe.InputProtocolSharedTaitoGun;

    private static bool IsJvsChannel(string inputProtocol, string channelName) =>
        AndroidLaunchRecipe.IsJvsInputProtocol(inputProtocol) ||
        (inputProtocol == AndroidLaunchRecipe.InputProtocolAllsIdta ||
         IsSharedStateWithJvs(inputProtocol)) &&
        (channelName == WinlatorSessionContract.ProductionJvsPipe64 ||
         channelName == WinlatorSessionContract.ProductionJvsPipe32);

    private static void ValidateInputProtocol(string? inputProtocol)
    {
        if (!AndroidLaunchRecipe.IsSupportedInputProtocol(inputProtocol))
            throw new InvalidDataException("The Android game input protocol is unsupported.");
    }

    private static bool IsValidResolution(int width, int height) =>
        (width == 0) == (height == 0) &&
        width >= 0 && height >= 0 && width <= 8_192 && height <= 8_192 &&
        (width == 0 || (width >= 320 && height >= 240));

    private static void ValidateResolution(int width, int height)
    {
        if (!IsValidResolution(width, height))
            throw new InvalidOperationException(
                "The Android game resolution must be omitted or between 320x240 and 8192x8192.");
    }

    private static void ValidateCompatibilityPreset(string? value)
    {
        if (!AndroidLaunchRecipe.IsSupportedCompatibilityPreset(value ?? string.Empty))
            throw new InvalidOperationException("The Android compatibility preset is unsupported.");
    }

    private static void ValidateDisplayMode(string? value)
    {
        if (!AndroidLaunchRecipe.IsSupportedDisplayMode(value ?? string.Empty))
            throw new InvalidOperationException("The Android display mode is unsupported.");
    }

    private static GameProfile LoadJvsProfile(string profileName)
    {
        var candidates = new[]
        {
            Path.Combine(global::System.Environment.CurrentDirectory, "UserProfiles", profileName + ".xml"),
            Path.Combine(global::System.Environment.CurrentDirectory, "GameProfiles", profileName + ".xml")
        };
        foreach (var candidate in candidates)
        {
            if (!File.Exists(candidate))
                continue;
            var profile = JoystickHelper.DeSerializeGameProfile(
                candidate,
                candidate.Contains("UserProfiles", StringComparison.Ordinal));
            if (profile == null)
                continue;
            profile.ProfileName = profileName;
            return profile;
        }
        throw new InvalidDataException(
            $"The JVS profile '{profileName}' is unavailable in the Android catalog.");
    }

    private SessionRecord CreateRecord(Intent? intent)
    {
        if (intent == null || !string.Equals(intent.Action, StartAction, StringComparison.Ordinal))
            throw new InvalidOperationException("There is no saved Android game session to restore.");

        var containerId = intent.GetIntExtra(ContainerExtra, 0);
        var profileName = intent.GetStringExtra(ProfileNameExtra)?.Trim() ?? string.Empty;
        var executable = intent.GetStringExtra(ExecutableExtra);
        var workingDirectory = intent.GetStringExtra(WorkingDirectoryExtra);
        var argumentsJson = intent.GetStringExtra(ArgumentsExtra);
        var libraryDirectory = intent.GetStringExtra(LibraryDirectoryExtra);
        var inputProtocol = intent.GetStringExtra(InputProtocolExtra)?.Trim() ?? string.Empty;
        var controlsProfileId = intent.GetIntExtra(ControlsProfileIdExtra, 0);
        var frameRateLimit = intent.GetIntExtra(FrameRateLimitExtra, 0);
        var resolutionWidth = intent.GetIntExtra(ResolutionWidthExtra, 0);
        var resolutionHeight = intent.GetIntExtra(ResolutionHeightExtra, 0);
        // Older launchers do not carry protocol-v12 display policy. Keep them
        // on the physical-device-proven native window path: the aspect-fit
        // renderer transformation can terminate otherwise healthy Wine games.
        var displayMode = intent.GetStringExtra(DisplayModeExtra)?.Trim() ??
            AndroidLaunchRecipe.DisplayModeCentered;
        var debugLoggingEnabled = intent.GetBooleanExtra(DebugLoggingEnabledExtra, true);
        var compatibilityPreset = intent.GetStringExtra(CompatibilityPresetExtra)?.Trim() ?? string.Empty;
        var profileConfigIni = intent.GetStringExtra(ProfileConfigIniExtra);
        var arguments = string.IsNullOrEmpty(argumentsJson)
            ? Array.Empty<string>()
            : JsonSerializer.Deserialize<string[]>(argumentsJson) ?? Array.Empty<string>();
        if (profileName.Length > 128 || profileName.Any(character =>
                character < 0x20 || character is '/' or '\\'))
            throw new InvalidOperationException("The Android game profile name is invalid.");
        ValidateInputProtocol(inputProtocol);
        if (controlsProfileId <= 0 || controlsProfileId > 1_000_000)
            throw new InvalidOperationException("The Android controls profile id is invalid.");
        if (frameRateLimit < 0 || frameRateLimit > 1_000)
            throw new InvalidOperationException("The Android frame-rate limit is invalid.");
        ValidateResolution(resolutionWidth, resolutionHeight);
        ValidateDisplayMode(displayMode);
        ValidateCompatibilityPreset(compatibilityPreset);

        // Reuse the bridge contract's strict DOS-path and argument validation
        // before any persistent state or cross-package call is made.
        var sessionId = Guid.NewGuid();
        var validationRequest = new WinlatorActivityLaunchRequest(
            sessionId,
            containerId,
            WinlatorSessionContract.WindowsExecutableLaunchKind,
            executable,
            workingDirectory,
            arguments,
            libraryDirectory,
            controlsProfileId,
            frameRateLimit,
            resolutionWidth,
            resolutionHeight,
            debugLoggingEnabled,
            compatibilityPreset,
            displayMode,
            profileConfigIni ?? string.Empty);
        _ = WinlatorSessionContract.CreateActivityLaunch(validationRequest);

        return new SessionRecord
        {
            SessionId = sessionId.ToString("N"),
            TokenHex = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),
            ProfileName = profileName,
            ContainerId = containerId,
            Port = 0,
            Executable = executable!,
            WorkingDirectory = workingDirectory!,
            Arguments = arguments,
            LibraryDirectory = libraryDirectory,
            InputProtocol = inputProtocol,
            ControlsProfileId = controlsProfileId,
            FrameRateLimit = frameRateLimit,
            ResolutionWidth = resolutionWidth,
            ResolutionHeight = resolutionHeight,
            DisplayMode = displayMode,
            DebugLoggingEnabled = debugLoggingEnabled,
            CompatibilityPreset = compatibilityPreset,
            ProfileConfigIni = profileConfigIni!
        };
    }

    private void RequestStop()
    {
        CancellationTokenSource? stop;
        ITeknoParrotWinlatorService? remoteService;
        string? remoteSessionId;
        lock (_runtimeSync)
        {
            _explicitStopRequested = true;
            stop = _sessionStop;
            remoteService = _remoteService;
            remoteSessionId = _remoteSessionId;
        }
        PublishStatus("state=stopping;detail=releasing controls and Winlator session");

        // Cancellation-aware TPI1 reads let TPUI establish its stopped state
        // before Winlator closes the peer socket. This avoids a clean-EOF race
        // that could skip the cancellation catch and retain the session record.
        stop?.Cancel();
        CloseNetworkEndpoints();

        // End the matching prepared Winlator Activity and its Wine children.
        if (remoteService != null && !string.IsNullOrEmpty(remoteSessionId))
        {
            try
            {
                remoteService.StopTestSession(remoteSessionId);
            }
            catch
            {
                // The normal finally path retries if the remote process raced
                // this request or was already shutting down.
            }
        }
        if (stop == null)
        {
            ClearRecord();
            StopForegroundSession();
            StopSelf();
            PublishStatus("state=stopped;detail=no active session");
        }
    }

    private void CloseNetworkEndpoints()
    {
        lock (_runtimeSync)
        {
            foreach (var client in _activeClients.ToArray())
            {
                try
                {
                    client.Client.Shutdown(SocketShutdown.Both);
                }
                catch
                {
                    // The peer may already have closed its half of the socket.
                }
                try
                {
                    client.Dispose();
                }
                catch
                {
                }
            }
            _activeClients.Clear();
            try
            {
                _listener?.Stop();
            }
            catch
            {
            }
            _listener = null;
        }
    }

    private void SaveRecord(SessionRecord record)
    {
        var preferences = GetSharedPreferences(PreferencesName, FileCreationMode.Private)
                          ?? throw new InvalidOperationException("Android session preferences are unavailable.");
        var editor = preferences.Edit()
                     ?? throw new InvalidOperationException("Android session preferences are read-only.");
        editor.PutString(SessionRecordKey, JsonSerializer.Serialize(record));
        if (!editor.Commit())
            throw new IOException("Could not persist the Android game session record.");
    }

    private SessionRecord? LoadRecord()
    {
        var json = GetSharedPreferences(PreferencesName, FileCreationMode.Private)
            ?.GetString(SessionRecordKey, null);
        if (string.IsNullOrEmpty(json))
            return null;
        try
        {
            var record = JsonSerializer.Deserialize<SessionRecord>(json);
            if (!IsValidStoredRecord(record))
                throw new InvalidDataException("Saved Android game session record is invalid.");
            return record;
        }
        catch (Exception error) when (error is JsonException or FormatException or InvalidDataException)
        {
            ClearRecord();
            return null;
        }
    }

    private static bool IsValidStoredRecord(SessionRecord? record)
    {
        if (record == null ||
            !Guid.TryParseExact(record.SessionId, "N", out _) ||
            string.IsNullOrEmpty(record.TokenHex) ||
            record.Port is < 1 or > 65535 ||
            !record.Prepared ||
            record.ProfileName == null ||
            record.ProfileName.Length > 128 ||
            record.ProfileName.Any(character => character < 0x20 || character is '/' or '\\'))
            return false;
        try
        {
            ValidateInputProtocol(record.InputProtocol);
            ValidateDisplayMode(record.DisplayMode);
            ValidateCompatibilityPreset(record.CompatibilityPreset);
            WinlatorSessionContract.ValidateProfileConfigIni(record.ProfileConfigIni);
            return Convert.FromHexString(record.TokenHex).Length == 32 &&
                   record.ControlsProfileId is > 0 and <= 1_000_000 &&
                   record.FrameRateLimit is >= 0 and <= 1_000 &&
                   IsValidResolution(record.ResolutionWidth, record.ResolutionHeight);
        }
        catch (Exception error) when (error is FormatException or ArgumentException)
        {
            return false;
        }
    }

    private void ClearRecord()
    {
        var editor = GetSharedPreferences(PreferencesName, FileCreationMode.Private)?.Edit();
        if (editor == null)
            return;
        editor.Remove(SessionRecordKey);
        editor.Commit();
    }

    private void CreateNotificationChannel()
    {
        var manager = (NotificationManager?)GetSystemService(NotificationService);
        manager?.CreateNotificationChannel(new NotificationChannel(
            NotificationChannelId,
            "Running arcade game",
            NotificationImportance.Low)
        {
            Description = "Keeps Winlator controls and the TeknoParrot shared page connected."
        });
    }

    private void StartForegroundSession(string detail)
    {
        ((NotificationManager?)GetSystemService(NotificationService))
            ?.Cancel(FailureNotificationId);
        var notification = BuildNotification(detail);

        if (OperatingSystem.IsAndroidVersionAtLeast(34))
            StartForeground(NotificationId, notification, ForegroundService.TypeSpecialUse);
        else
            StartForeground(NotificationId, notification);
        _foregroundStarted = true;
    }

    private Notification BuildNotification(string detail)
    {
        var openIntent = new Intent(this, typeof(MainActivity));
        openIntent.SetFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);
        var openPending = PendingIntent.GetActivity(
            this,
            0,
            openIntent,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

        var stopIntent = new Intent(this, typeof(GameSessionService));
        stopIntent.SetAction(StopAction);
        var stopPending = PendingIntent.GetService(
            this,
            1,
            stopIntent,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable)
                          ?? throw new InvalidOperationException("Could not create the game-session Stop action.");

        var stopAction = new Notification.Action.Builder(
                global::Android.Graphics.Drawables.Icon.CreateWithResource(
                    this,
                    global::Android.Resource.Drawable.IcMenuCloseClearCancel),
                "Stop",
                stopPending)
            .Build();

        return new Notification.Builder(this, NotificationChannelId)
            .SetSmallIcon(global::Android.Resource.Drawable.IcMediaPlay)
            .SetContentTitle("TeknoParrot game session")
            .SetContentText(detail)
            .SetContentIntent(openPending)
            .SetOngoing(true)
            .SetOnlyAlertOnce(true)
            .SetCategory(Notification.CategoryService)
            .AddAction(stopAction)
            .Build();
    }

    private void PostFailureNotification(string detail)
    {
        var openIntent = new Intent(this, typeof(MainActivity));
        openIntent.SetFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);
        var openPending = PendingIntent.GetActivity(
            this,
            2,
            openIntent,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
        var message = "Open TeknoParrot to review and retry. " + Sanitize(detail);
        if (message.Length > 240)
            message = message[..237] + "...";

        var notification = new Notification.Builder(this, NotificationChannelId)
            .SetSmallIcon(global::Android.Resource.Drawable.IcDialogAlert)
            .SetContentTitle("TeknoParrot game needs attention")
            .SetContentText(message)
            .SetStyle(new Notification.BigTextStyle().BigText(message))
            .SetContentIntent(openPending)
            .SetAutoCancel(true)
            .SetOnlyAlertOnce(true)
            .SetCategory(Notification.CategoryError)
            .Build();
        ((NotificationManager?)GetSystemService(NotificationService))
            ?.Notify(FailureNotificationId, notification);
    }

    private void StopForegroundSession()
    {
        _foregroundStarted = false;
        if (OperatingSystem.IsAndroidVersionAtLeast(24))
            StopForeground(StopForegroundFlags.Remove);
        else
#pragma warning disable CA1422
            StopForeground(true);
#pragma warning restore CA1422
    }

    private void PublishStatus(string status)
    {
        Action<string>? changed;
        lock (StatusSync)
        {
            _status = status;
            changed = StatusChanged;
        }
        if (_debugLoggingEnabled)
            global::Android.Util.Log.Info("TeknoParrotSession", status);
        if (_foregroundStarted)
        {
            var manager = (NotificationManager?)GetSystemService(NotificationService);
            manager?.Notify(NotificationId, BuildNotification(NotificationText(status)));
        }
        changed?.Invoke(status);
    }

    private static string NotificationText(string status)
    {
        if (status.StartsWith("state=preparing", StringComparison.Ordinal))
            return "Preparing Winlator session";
        if (status.StartsWith("state=restoring", StringComparison.Ordinal))
            return "Restoring Winlator session";
        if (status.StartsWith("state=provisioning", StringComparison.Ordinal))
            return "Updating the arcade runtime";
        if (status.StartsWith("state=permission", StringComparison.Ordinal))
            return "Waiting for game-folder permission";
        if (status.StartsWith("state=waiting", StringComparison.Ordinal))
            return "Waiting for game controls";
        if (status.StartsWith("state=connected", StringComparison.Ordinal))
            return "Game controls connected";
        if (status.StartsWith("state=reconnecting", StringComparison.Ordinal))
            return "Reconnecting game controls";
        if (status.StartsWith("state=pressure", StringComparison.Ordinal))
            return "Low memory - close other apps";
        if (status.StartsWith("state=stopping", StringComparison.Ordinal))
            return "Stopping game session";
        if (status.StartsWith("state=fault", StringComparison.Ordinal))
            return "Game session needs attention";
        if (status.StartsWith("state=ended", StringComparison.Ordinal))
            return "Game session ended";
        return "Winlator game session active";
    }

    private static void SetCurrentInputSource(WinlatorForwardedInputSource? source)
    {
        lock (StatusSync)
            _currentInputSource = source;
    }

    private static string Sanitize(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return "unknown";
        var sanitized = message
            .Replace(';', ',')
            .Replace('\r', ' ')
            .Replace('\n', ' ');
        return sanitized.Length <= 512 ? sanitized : sanitized[..509] + "...";
    }

    private sealed class SessionRecord
    {
        public string SessionId { get; set; } = string.Empty;
        public string TokenHex { get; set; } = string.Empty;
        public string ProfileName { get; set; } = string.Empty;
        public int ContainerId { get; set; }
        public int Port { get; set; }
        public string Executable { get; set; } = string.Empty;
        public string WorkingDirectory { get; set; } = string.Empty;
        public string[] Arguments { get; set; } = Array.Empty<string>();
        public string? LibraryDirectory { get; set; }
        public string InputProtocol { get; set; } = string.Empty;
        public int ControlsProfileId { get; set; }
        public int FrameRateLimit { get; set; }
        public int ResolutionWidth { get; set; }
        public int ResolutionHeight { get; set; }
        public string DisplayMode { get; set; } = AndroidLaunchRecipe.DisplayModeCentered;
        public bool DebugLoggingEnabled { get; set; } = true;
        public string CompatibilityPreset { get; set; } = string.Empty;
        public string ProfileConfigIni { get; set; } = string.Empty;
        public bool Prepared { get; set; }
        public bool Launched { get; set; }
    }

    private sealed class SessionCounters
    {
        public long Frames;
        public long PipeConnections;
        public int ActiveInputChannels;
        public int ActivePipeChannels;
    }

    private readonly record struct DevicePressureSnapshot(
        long AvailableMiB,
        long ThresholdMiB,
        bool LowMemory,
        int ThermalStatus)
    {
        public bool HasLowMemoryHeadroom =>
            AvailableMiB <= Math.Max(512, ThresholdMiB * 2);
        public bool ShouldWarn => HasLowMemoryHeadroom || ThermalStatus >= 3;
        // Status 4 (CRITICAL) is common under sustained emulation and Android
        // already throttles it. Warn at that level, but only stop at EMERGENCY
        // (5) or SHUTDOWN (6), where continuing risks a platform shutdown.
        public bool IsCritical => LowMemory || ThermalStatus >= 5;
    }

    private sealed class RetainedWinlatorConnection : Java.Lang.Object, IServiceConnection, IDisposable
    {
        private readonly GameSessionService _owner;
        private readonly TaskCompletionSource<ITeknoParrotWinlatorService> _connected =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool _bound;
        private int _terminal;

        public RetainedWinlatorConnection(GameSessionService owner)
        {
            _owner = owner;
        }

        public async Task<ITeknoParrotWinlatorService> BindAsync(CancellationToken cancellationToken)
        {
            var intent = new Intent(BridgeProtocol.WinlatorServiceAction);
            intent.SetComponent(new ComponentName(
                BridgeProtocol.WinlatorServicePackage,
                BridgeProtocol.WinlatorServiceClass));
            var bindFlags = Bind.AutoCreate;
            if (OperatingSystem.IsAndroidVersionAtLeast(34))
                bindFlags |= Bind.AllowActivityStarts;
            _bound = _owner.BindService(intent, this, bindFlags);
            if (!_bound)
                throw new InvalidOperationException("Android refused the Winlator bridge binding.");

            using var registration = cancellationToken.Register(
                () => _connected.TrySetCanceled(cancellationToken));
            try
            {
                return await _connected.Task
                    .WaitAsync(TimeSpan.FromSeconds(30), cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                throw new TimeoutException(
                    "The Winlator bridge did not connect within 30 seconds. " +
                    "Open TeknoParrot again to retry.");
            }
        }

        public void OnServiceConnected(ComponentName? name, IBinder? service)
        {
            if (service == null)
                _connected.TrySetException(
                    new InvalidOperationException("Winlator returned a null Binder."));
            else
                _connected.TrySetResult(ITeknoParrotWinlatorServiceStub.AsInterface(service));
        }

        public void OnServiceDisconnected(ComponentName? name)
        {
            if (Volatile.Read(ref _terminal) == 0)
                _owner.PublishStatus("state=reconnecting;detail=Winlator bridge disconnected");
        }

        public void OnBindingDied(ComponentName? name)
        {
            if (Interlocked.Exchange(ref _terminal, 1) == 0)
                _owner.PublishStatus("state=fault;error=Winlator bridge binding died");
            _connected.TrySetException(
                new InvalidOperationException("Winlator bridge binding died."));
        }

        public void OnNullBinding(ComponentName? name)
        {
            MarkTerminal();
            _connected.TrySetException(
                new InvalidOperationException("Winlator returned a null service binding."));
        }

        public void MarkTerminal() => Interlocked.Exchange(ref _terminal, 1);

        public new void Dispose()
        {
            MarkTerminal();
            if (!_bound)
                return;
            try
            {
                _owner.UnbindService(this);
            }
            catch (ArgumentException)
            {
            }
            _bound = false;
        }
    }
}
