#if DEBUG
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
using Android.Views;
using Android.Widget;
using TeknoParrotUi.AndroidBridge;
using TeknoParrotUi.Common;
using TeknoParrotUi.Common.Android;
using TeknoParrotUi.Common.InputListening.Forwarded;

namespace TeknoParrotUi.Avalonia.Android;

/// <summary>
/// Debug-only entry point for validating the production-direction Winlator
/// service. It is deliberately not exported and is started by the emulator
/// lab through this application's UID.
/// </summary>
[Activity(
    Name = "com.teknoparrot.bridge.WinlatorBridgeProbeActivity",
    Label = "Winlator Bridge Probe",
    Exported = false,
    Theme = "@android:style/Theme.Material.NoActionBar",
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize)]
public sealed class WinlatorBridgeProbeActivity : global::Android.App.Activity
{
    public const string LaunchExtra = "com.teknoparrot.ui.RUN_WINLATOR_BRIDGE_PROBE";
    public const string GuestLaunchExtra = "com.teknoparrot.ui.RUN_WINLATOR_GUEST_BRIDGE_PROBE";
    public const string ProfileLaunchExtra = "com.teknoparrot.ui.RUN_CONFIGURED_PROFILE";
    public const string GuestContainerExtra = "com.teknoparrot.ui.WINLATOR_GUEST_CONTAINER_ID";
    public const string WindowsExecutableExtra = "com.teknoparrot.ui.WINLATOR_WINDOWS_EXECUTABLE";
    public const string WindowsWorkingDirectoryExtra = "com.teknoparrot.ui.WINLATOR_WINDOWS_WORKING_DIRECTORY";
    public const string WindowsArgumentsExtra = "com.teknoparrot.ui.WINLATOR_WINDOWS_ARGUMENTS_JSON";
    public const string WindowsLibraryDirectoryExtra =
        "com.teknoparrot.ui.WINLATOR_WINDOWS_LIBRARY_DIRECTORY";
    private const int NotificationPermissionRequestCode = 0x5450;

    private TextView? _result;
    private Button? _runButton;
    private WinlatorServiceConnection? _connection;
    private bool _bound;
    private bool _runGuestDiagnostic;
    private bool _runProfileLaunch;
    private int _guestContainerId;
    private string? _profileName;
    private string? _windowsExecutable;
    private string? _windowsWorkingDirectory;
    private string[] _windowsArguments = Array.Empty<string>();
    private string? _windowsLibraryDirectory;
    private bool _restoreGameSession;
    private bool _notificationPermissionResolved;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        _runGuestDiagnostic = Intent?.GetBooleanExtra(GuestLaunchExtra, false) == true;
        _runProfileLaunch = Intent?.GetBooleanExtra(ProfileLaunchExtra, false) == true;
        _restoreGameSession = Intent?.GetBooleanExtra(GameSessionService.RestoreExtra, false) == true;
        _guestContainerId = Math.Max(1, Intent?.GetIntExtra(GuestContainerExtra, 1) ?? 1);
        _profileName = Intent?.GetStringExtra(GameSessionService.ProfileNameExtra);
        _windowsExecutable = Intent?.GetStringExtra(WindowsExecutableExtra);
        _windowsWorkingDirectory = Intent?.GetStringExtra(WindowsWorkingDirectoryExtra);
        _windowsLibraryDirectory = Intent?.GetStringExtra(WindowsLibraryDirectoryExtra);
        GameSessionService.StatusChanged += OnGameSessionStatusChanged;
        var windowsArgumentsJson = Intent?.GetStringExtra(WindowsArgumentsExtra);
        if (!string.IsNullOrEmpty(windowsArgumentsJson))
            _windowsArguments = JsonSerializer.Deserialize<string[]>(windowsArgumentsJson) ?? Array.Empty<string>();

        var root = new LinearLayout(this) { Orientation = Orientation.Vertical };
        root.SetGravity(GravityFlags.CenterHorizontal);
        root.SetPadding(32, 48, 32, 32);
        _runButton = new Button(this)
        {
            Text = _restoreGameSession
                ? "Restore foreground game session"
                : _runProfileLaunch
                    ? "Run configured TeknoParrot profile"
                : !string.IsNullOrEmpty(_windowsExecutable)
                ? "Run prepared Windows executable"
                : _runGuestDiagnostic
                    ? "Run Winlator Windows guest test"
                    : "Run Winlator service test"
        };
        _result = new TextView(this)
        {
            TextSize = 14,
            Typeface = global::Android.Graphics.Typeface.Monospace,
            Text = "Install the matching Winlator bridge build, then run the test."
        };
        _runButton.Click += (_, _) => BindAndRun();
        root.AddView(_runButton);
        root.AddView(_result);
        SetContentView(root);
        BindAndRun();
    }

    protected override void OnDestroy()
    {
        GameSessionService.StatusChanged -= OnGameSessionStatusChanged;
        if (_bound && _connection != null)
            UnbindService(_connection);
        _bound = false;
        _connection = null;
        base.OnDestroy();
    }

    private void BindAndRun()
    {
        if (_runButton == null || _result == null)
            return;

        if ((_restoreGameSession || !string.IsNullOrEmpty(_windowsExecutable)) &&
            RequestNotificationPermissionIfNeeded())
            return;

        if (_restoreGameSession)
        {
            _runButton.Enabled = false;
            _result.Text = "Restoring foreground game session...";
            StartForegroundService(new Intent(this, typeof(GameSessionService)));
            return;
        }

        if (_runProfileLaunch)
        {
            StartConfiguredProfileSession();
            return;
        }

        if (!string.IsNullOrEmpty(_windowsExecutable))
        {
            StartForegroundWindowsSession();
            return;
        }

        if (_bound && _connection != null)
        {
            UnbindService(_connection);
            _bound = false;
        }

        _runButton.Enabled = false;
        _result.Text = "Binding to com.teknoparrot.winlator...";
        _connection = new WinlatorServiceConnection(OnServiceConnected, ShowFailure);

        var intent = new Intent(BridgeProtocol.WinlatorServiceAction);
        intent.SetComponent(new ComponentName(
            BridgeProtocol.WinlatorServicePackage,
            BridgeProtocol.WinlatorServiceClass));
        var bindFlags = Bind.AutoCreate;
        if (OperatingSystem.IsAndroidVersionAtLeast(34))
            bindFlags |= Bind.AllowActivityStarts;
        _bound = BindService(intent, _connection, bindFlags);
        if (!_bound)
            ShowFailure("Android refused the Winlator bridge binding. Verify the APK and matching signatures.");
    }

    public override void OnRequestPermissionsResult(
        int requestCode,
        string[] permissions,
        Permission[] grantResults)
    {
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        if (requestCode != NotificationPermissionRequestCode)
            return;

        _notificationPermissionResolved = true;
        BindAndRun();
    }

    private bool RequestNotificationPermissionIfNeeded()
    {
        if (_notificationPermissionResolved)
        {
            return false;
        }

        if (!AndroidNotificationPermission.RequestIfNeeded(
                this,
                NotificationPermissionRequestCode))
        {
            _notificationPermissionResolved = true;
            return false;
        }

        if (_runButton != null)
            _runButton.Enabled = false;
        if (_result != null)
            _result.Text = "Allow notifications to keep the running game session visible.";
        return true;
    }

    private void StartForegroundWindowsSession()
    {
        if (_runButton == null || _result == null || string.IsNullOrEmpty(_windowsExecutable))
            return;

        var workingDirectory = _windowsWorkingDirectory;
        var separator = _windowsExecutable.LastIndexOf('\\');
        if (string.IsNullOrEmpty(workingDirectory) && separator > 2)
            workingDirectory = _windowsExecutable[..separator];
        if (string.IsNullOrEmpty(workingDirectory))
        {
            ShowFailure("A Windows working directory is required.");
            return;
        }

        _runButton.Enabled = false;
        _result.Text = "Starting foreground game session...";
        var intent = AndroidGameSessionLauncherActivity.CreateIntent(
            this,
            _guestContainerId,
            _windowsExecutable,
            workingDirectory,
            _windowsArguments,
            _windowsLibraryDirectory,
            _profileName);
        StartActivity(intent);
        // The launcher and Winlator game activity run in their own tasks. If
        // this debug bridge entry point remains in the back stack, Android
        // reveals it as soon as the one-shot launcher finishes, pausing and
        // SIGSTOPing the active Wine guest behind it.
        Finish();
    }

    private void StartConfiguredProfileSession()
    {
        if (_runButton == null || _result == null || string.IsNullOrWhiteSpace(_profileName))
        {
            ShowFailure("A configured TeknoParrot profile name is required.");
            return;
        }

        try
        {
            var userPath = Path.Combine("UserProfiles", _profileName + ".xml");
            var stockPath = Path.Combine("GameProfiles", _profileName + ".xml");
            var useUserProfile = File.Exists(userPath);
            var profile = JoystickHelper.DeSerializeGameProfile(
                useUserProfile ? userPath : stockPath,
                useUserProfile) ?? throw new InvalidDataException(
                    "The configured TeknoParrot profile could not be loaded.");
            profile.ProfileName = _profileName;
            var downloads = global::Android.OS.Environment
                .GetExternalStoragePublicDirectory(global::Android.OS.Environment.DirectoryDownloads)
                ?.AbsolutePath ?? "/storage/emulated/0/Download";
            var plan = AndroidWinlatorLaunchPlan.Create(
                profile,
                isTest: false,
                emuOnly: false,
                downloads);

            _runButton.Enabled = false;
            _result.Text = "Starting configured profile " + _profileName + "...";
            StartActivity(AndroidGameSessionLauncherActivity.CreateIntent(this, plan));
            Finish();
        }
        catch (Exception error)
        {
            ShowFailure(error.Message);
        }
    }

    private void OnGameSessionStatusChanged(string status) => RunOnUiThread(() =>
    {
        if (_result != null)
            _result.Text = "ANDROID GAME SESSION\n\n" + status;
        if (_runButton != null &&
            (status.StartsWith("state=ended", StringComparison.Ordinal) ||
             status.StartsWith("state=stopped", StringComparison.Ordinal) ||
             status.StartsWith("state=fault", StringComparison.Ordinal)))
            _runButton.Enabled = true;
    });

    private void OnServiceConnected(IBinder binder)
    {
        var service = ITeknoParrotWinlatorServiceStub.AsInterface(binder);
        _ = Task.Run(() => RunProbeAsync(
            service,
            _runGuestDiagnostic,
            _guestContainerId,
            _windowsExecutable,
            _windowsWorkingDirectory,
            _windowsArguments,
            _windowsLibraryDirectory)).ContinueWith(task =>
        {
            if (task.IsFaulted)
                ShowFailure(task.Exception?.GetBaseException().Message ?? "Unknown probe failure.");
            else if (task.IsCanceled)
                ShowFailure("Probe was cancelled.");
            else
                ShowSuccess(task.Result);
        }, TaskScheduler.Default);
    }

    private static async Task<string> RunProbeAsync(
        ITeknoParrotWinlatorService service,
        bool runGuestDiagnostic,
        int guestContainerId,
        string? windowsExecutable,
        string? windowsWorkingDirectory,
        IReadOnlyList<string> windowsArguments,
        string? windowsLibraryDirectory)
    {
        var serviceProtocolVersion = service.GetProtocolVersion();
        if (serviceProtocolVersion != WinlatorSessionContract.ServiceProtocolVersion)
            throw new InvalidOperationException("Winlator bridge protocol version mismatch.");

        if (!string.IsNullOrEmpty(windowsExecutable))
            return await RunWindowsExecutableProbeAsync(
                service,
                guestContainerId,
                windowsExecutable,
                windowsWorkingDirectory,
                windowsArguments,
                windowsLibraryDirectory).ConfigureAwait(false);

        if (runGuestDiagnostic)
            return await RunGuestBridgeProbeAsync(service, guestContainerId).ConfigureAwait(false);

        var sessionIdText = service.PrepareTestSession("teknoparrot-emulator-host");
        if (!Guid.TryParseExact(sessionIdText, "N", out var sessionId))
            throw new InvalidDataException("Winlator returned an invalid session identifier.");

        var report = new List<string>
        {
            $"AIDL: PASS (service v{serviceProtocolVersion}, data v{BridgeProtocol.ProtocolVersion})",
            $"Winlator session: {sessionId:N}"
        };

        try
        {
            using (var page = MappedSharedPage.Map(OpenSharedPage(service, sessionId)))
            {
                if (!page.ReadBytes(BridgeProtocol.MagicOffset, 4).SequenceEqual(BridgeProtocol.SharedPageMagic))
                    throw new InvalidDataException("Winlator shared page magic is not TPJ1.");
                if (page.ReadUInt16(BridgeProtocol.LayoutVersionOffset) != BridgeProtocol.ProtocolVersion ||
                    page.ReadUInt32(BridgeProtocol.TotalSizeOffset) != BridgeProtocol.PageSize)
                    throw new InvalidDataException("Winlator shared page layout metadata is invalid.");

                var firstGuestSequence = page.ReadUInt32(BridgeProtocol.GuestSequenceOffset);
                await Task.Delay(160).ConfigureAwait(false);
                var secondGuestSequence = page.ReadUInt32(BridgeProtocol.GuestSequenceOffset);
                if (secondGuestSequence <= firstGuestSequence)
                    throw new InvalidDataException("Winlator heartbeat is not updating the shared page.");

                var legacyPattern = new byte[BridgeProtocol.LegacySize];
                for (var index = 0; index < legacyPattern.Length; index++)
                    legacyPattern[index] = (byte)(index ^ 0x5A);
                page.WriteBytes(BridgeProtocol.LegacyOffset, legacyPattern);
                if (!page.ReadBytes(BridgeProtocol.LegacyOffset, BridgeProtocol.LegacySize)
                        .SequenceEqual(legacyPattern))
                    throw new InvalidDataException("Shared legacy page did not preserve TeknoParrotUI writes.");

                const uint hostMarker = 0xC0DEC0DE;
                page.WriteUInt64(BridgeProtocol.HostTimestampOffset, BridgeProtocol.MonotonicNanoseconds());
                page.WriteUInt32(BridgeProtocol.HostSequenceOffset, hostMarker);
                page.WriteUInt32(
                    BridgeProtocol.FlagsOffset,
                    page.ReadUInt32(BridgeProtocol.FlagsOffset) | BridgeProtocol.FlagHostReady);
                report.Add(
                    $"Shared page: PASS (Winlator {firstGuestSequence}->{secondGuestSequence}, host 0x{hostMarker:X8})");
            }

            var pipeReport = await RunPipeProbeAsync(service, sessionId, CancellationToken.None)
                .ConfigureAwait(false);
            report.Add("Pipe TPB1 + echo: PASS (" + pipeReport + ")");

            await Task.Delay(100).ConfigureAwait(false);
            var status = service.GetSessionStatus(sessionId.ToString("N"));
            if (!status.Contains("pipeMessages=16", StringComparison.Ordinal) ||
                !status.Contains("hostSeq=3235823838", StringComparison.Ordinal))
                throw new InvalidDataException("Winlator did not observe both data paths: " + status);
            report.Add("Winlator observation: PASS");
            report.Add(status);

            service.StopTestSession(sessionId.ToString("N"));
            for (var iteration = 0; iteration < 100; iteration++)
            {
                var lifecycleId = service.PrepareTestSession($"lifecycle-{iteration:D3}");
                if (!Guid.TryParseExact(lifecycleId, "N", out _))
                    throw new InvalidDataException($"Lifecycle iteration {iteration} returned an invalid session.");
                service.StopTestSession(lifecycleId);
            }
            report.Add("Lifecycle: PASS (100 prepare/stop cycles)");
            return string.Join("\n", report);
        }
        finally
        {
            service.StopTestSession(sessionId.ToString("N"));
        }
    }

    private static async Task<string> RunGuestBridgeProbeAsync(
        ITeknoParrotWinlatorService service,
        int containerId)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start(backlog: 1);
        var sessionId = Guid.Empty;
        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var capabilities = WinlatorSessionContract.ParseCapabilities(
                service.GetCapabilities(WinlatorSessionContract.ServiceProtocolVersion));
            sessionId = Guid.NewGuid();
            var token = RandomNumberGenerator.GetBytes(32);
            var spec = WinlatorSessionContract.CreateDiagnosticSpec(
                sessionId, token, containerId, port);
            var prepared = WinlatorSessionContract.ParsePrepared(
                service.PrepareSession(spec), sessionId, containerId, port);

            using var page = MappedSharedPage.Map(OpenSharedPage(service, sessionId));
            var hostPrefix = new byte[16];
            for (var index = 0; index < hostPrefix.Length; index++)
                hostPrefix[index] = (byte)(0xA0 + index);
            page.WriteBytes(BridgeProtocol.LegacyOffset, hostPrefix);
            page.WriteBytes(32, new byte[17]);
            page.WriteUInt32(
                BridgeProtocol.FlagsOffset,
                page.ReadUInt32(BridgeProtocol.FlagsOffset) | BridgeProtocol.FlagHostReady);

            var report = new List<string>
            {
                $"AIDL guest launch: PASS (service v{capabilities.ProtocolVersion}, data v{BridgeProtocol.ProtocolVersion})",
                $"Capabilities: PASS ({capabilities.Features})",
                "Versioned SessionSpec/PreparedSession: PASS (token not echoed)",
                $"Winlator container: {containerId}",
                $"Guest session: {sessionId:N}"
            };

            using (var inputTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            {
                var inputStatus = await RunPreparedInputDiagnosticAsync(
                    service, listener, sessionId, token, inputTimeout.Token).ConfigureAwait(false);
                report.Add("Winlator Java TPI1 producer: PASS (" + inputStatus + ")");
            }

            using (var activityInputTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(20)))
            {
                var activityInputStatus = await RunPreparedInputActivityDiagnosticAsync(
                    service, listener, prepared, token, activityInputTimeout.Token).ConfigureAwait(false);
                report.Add("Winlator Activity TPI1 observer: PASS (" + activityInputStatus + ")");
            }

            var launchStatus = service.LaunchPreparedGuestDiagnostic(sessionId.ToString("N"));
            if (launchStatus.Contains("state=fault", StringComparison.Ordinal) ||
                launchStatus.Contains("state=unsupported", StringComparison.Ordinal))
                throw new InvalidOperationException("Winlator rejected guest launch: " + launchStatus);
            report.Add("Service-controlled launch: PASS (" + launchStatus + ")");

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
            await RejectWrongTokenGuestAsync(
                listener, sessionId, token, prepared.PipeName64, timeout.Token).ConfigureAwait(false);
            report.Add("Live Windows wrong-token rejection: PASS (no OKAY)");
            await ExchangeGuestPipeVectorAsync(
                listener, 64, sessionId, token, prepared.PipeName64, timeout.Token).ConfigureAwait(false);
            report.Add("x64 Windows named pipe + TPB1 + 1 MiB/dir: PASS");
            await ExchangeGuestPipeVectorAsync(
                listener, 32, sessionId, token, prepared.PipeName32, timeout.Token).ConfigureAwait(false);
            report.Add("x86/WoW64 Windows named pipe + TPB1 + 1 MiB/dir: PASS");
            report.Add("Authenticated reconnect after rejection: PASS");
            report.Add("Same-helper stress reconnect: PASS (x64 + x86)");

            var expectedMarker = new byte[16];
            for (var index = 0; index < expectedMarker.Length; index++)
                expectedMarker[index] = (byte)(0xD0 + index);

            var markerDeadline = DateTime.UtcNow.AddSeconds(10);
            while (!page.ReadBytes(32, expectedMarker.Length).SequenceEqual(expectedMarker) ||
                   page.ReadBytes(48, 1)[0] != 32)
            {
                if (DateTime.UtcNow >= markerDeadline)
                    throw new InvalidDataException("Windows guest writes did not reach the Winlator-owned page.");
                await Task.Delay(50).ConfigureAwait(false);
            }
            report.Add("TPJ1 backing page through Windows mapping: PASS (host->guest->host)");

            string guestStatus;
            var exitDeadline = DateTime.UtcNow.AddSeconds(15);
            do
            {
                guestStatus = service.GetGuestBridgeDiagnosticStatus(sessionId.ToString("N"));
                if (guestStatus.Contains("state=exited", StringComparison.Ordinal))
                    break;
                if (guestStatus.Contains("state=fault", StringComparison.Ordinal) ||
                    DateTime.UtcNow >= exitDeadline)
                    throw new InvalidOperationException("Guest diagnostic did not exit cleanly: " + guestStatus);
                await Task.Delay(100).ConfigureAwait(false);
            } while (true);

            if (!guestStatus.Contains("exit=0", StringComparison.Ordinal))
                throw new InvalidOperationException("Guest diagnostic returned a failure: " + guestStatus);
            report.Add("Guest exit: PASS (" + guestStatus + ")");

            service.StopGuestBridgeDiagnostic(sessionId.ToString("N"));
            service.StopGuestBridgeDiagnostic(sessionId.ToString("N"));
            var stoppedStatus = service.GetGuestBridgeDiagnosticStatus(sessionId.ToString("N"));
            if (!stoppedStatus.Contains("state=stopped", StringComparison.Ordinal))
                throw new InvalidOperationException("Guest stop was not idempotent: " + stoppedStatus);
            report.Add("Idempotent guest stop: PASS");
            report.Add("Session page retained for inspection (no delete requested)");
            return string.Join("\n", report);
        }
        finally
        {
            listener.Stop();
            if (sessionId != Guid.Empty)
                service.StopGuestBridgeDiagnostic(sessionId.ToString("N"));
        }
    }

    private static async Task<string> RunWindowsExecutableProbeAsync(
        ITeknoParrotWinlatorService service,
        int containerId,
        string executable,
        string? workingDirectory,
        IReadOnlyList<string> arguments,
        string? libraryDirectory)
    {
        var separator = executable.LastIndexOf('\\');
        if (string.IsNullOrEmpty(workingDirectory) && separator > 2)
            workingDirectory = executable[..separator];
        if (string.IsNullOrEmpty(workingDirectory))
            throw new ArgumentException("A Windows working directory is required.", nameof(workingDirectory));

        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start(backlog: 1);
        var sessionId = Guid.Empty;
        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var capabilities = WinlatorSessionContract.ParseCapabilities(
                service.GetCapabilities(WinlatorSessionContract.ServiceProtocolVersion));
            sessionId = Guid.NewGuid();
            var token = RandomNumberGenerator.GetBytes(32);
            var prepared = WinlatorSessionContract.ParsePrepared(
                service.PrepareSession(WinlatorSessionContract.CreateDiagnosticSpec(
                    sessionId, token, containerId, port)),
                sessionId,
                containerId,
                port);

            var request = new WinlatorActivityLaunchRequest(
                prepared.SessionId,
                prepared.ContainerId,
                WinlatorSessionContract.WindowsExecutableLaunchKind,
                executable,
                workingDirectory,
                arguments,
                libraryDirectory,
                ProfileConfigIni: "[General]\nWindowed=1\n");
            var launchStatus = service.LaunchPreparedActivity(
                WinlatorSessionContract.CreateActivityLaunch(request));
            WinlatorSessionContract.ValidateActivityLaunchStatus(launchStatus, request);

            using var connectionTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
            using var client = await listener.AcceptTcpClientAsync(connectionTimeout.Token)
                .ConfigureAwait(false);
            client.NoDelay = true;
            using var stream = client.GetStream();

            var fixedHeader = new byte[58];
            await BridgeProtocol.ReadExactlyAsync(
                stream, fixedHeader, connectionTimeout.Token).ConfigureAwait(false);
            var channelNameLength = BinaryPrimitives.ReadUInt16BigEndian(fixedHeader.AsSpan(56, 2));
            if (channelNameLength == 0 || channelNameLength > BridgeProtocol.MaxPipeNameBytes)
                throw new InvalidDataException("Winlator sent an invalid launch input channel.");
            var channelName = new byte[channelNameLength];
            await BridgeProtocol.ReadExactlyAsync(
                stream, channelName, connectionTimeout.Token).ConfigureAwait(false);
            if (!BridgeProtocol.ValidateAuthenticatedHandshake(
                    fixedHeader,
                    channelName,
                    sessionId,
                    token,
                    BridgeProtocol.ForwardedInputChannelKind,
                    BridgeProtocol.ProbeInputChannelName,
                    out var error))
                throw new UnauthorizedAccessException(
                    "Winlator launch input authentication failed: " + error);

            await stream.WriteAsync(BridgeProtocol.PipeAck, connectionTimeout.Token)
                .ConfigureAwait(false);
            await stream.FlushAsync(connectionTimeout.Token).ConfigureAwait(false);

            // Keep the prepared session, authenticated input channel, and shared page
            // alive until the Winlator Activity is closed.  This method intentionally
            // does not expose the executable or token in its status text.
            var source = new WinlatorForwardedInputSource();
            var reader = new ForwardedInputStreamReader(stream);
            var frames = 0;
            while (reader.ReadAndApply(source, out _))
                frames++;

            return $"Windows launch closed normally; inputFrames={frames};" +
                   $"features={capabilities.Features}";
        }
        finally
        {
            listener.Stop();
            if (sessionId != Guid.Empty)
                service.StopTestSession(sessionId.ToString("N"));
        }
    }

    private static async Task<string> RunPreparedInputDiagnosticAsync(
        ITeknoParrotWinlatorService service,
        TcpListener listener,
        Guid sessionId,
        byte[] token,
        CancellationToken cancellationToken)
    {
        var producerTask = Task.Run(
            () => service.RunPreparedInputDiagnostic(sessionId.ToString("N")),
            CancellationToken.None);
        using var client = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
        client.NoDelay = true;
        client.ReceiveTimeout = 5000;
        using var stream = client.GetStream();

        var fixedHeader = new byte[58];
        await BridgeProtocol.ReadExactlyAsync(stream, fixedHeader, cancellationToken).ConfigureAwait(false);
        var channelNameLength = BinaryPrimitives.ReadUInt16BigEndian(fixedHeader.AsSpan(56, 2));
        if (channelNameLength == 0 || channelNameLength > BridgeProtocol.MaxPipeNameBytes)
            throw new InvalidDataException("Winlator sent an invalid TPI1 channel-name length.");
        var channelName = new byte[channelNameLength];
        await BridgeProtocol.ReadExactlyAsync(stream, channelName, cancellationToken).ConfigureAwait(false);
        if (!BridgeProtocol.ValidateAuthenticatedHandshake(
                fixedHeader,
                channelName,
                sessionId,
                token,
                BridgeProtocol.ForwardedInputChannelKind,
                BridgeProtocol.ProbeInputChannelName,
                out var error))
            throw new UnauthorizedAccessException("Winlator TPI1 handshake rejected: " + error);

        await stream.WriteAsync(BridgeProtocol.PipeAck, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);

        const uint deviceStableId = 0xA0B0C0D0;
        var source = new WinlatorForwardedInputSource();
        var reader = new ForwardedInputStreamReader(stream);
        var buttonMasks = new uint[WinlatorForwardedInputSource.MaximumPlayers];
        var axes = new short[
            WinlatorForwardedInputSource.MaximumPlayers * WinlatorForwardedInputSource.MaximumAxes];
        var flats = new ushort[axes.Length];
        var pointers = new ForwardedPointerState[WinlatorForwardedInputSource.MaximumPlayers];
        var frames = 0;
        var gaps = 0;
        var buttonObserved = false;
        var axisObserved = false;
        var pointerObserved = false;
        var released = false;
        var lifecycleResetObserved = false;

        while (reader.ReadAndApply(source, out var result))
        {
            frames++;
            if (result == ForwardedInputApplyResult.SequenceGap)
                gaps++;
            if (frames <= 2)
            {
                if (!source.TryCopyDeviceState(
                        0, buttonMasks, axes, flats, pointers))
                    throw new InvalidDataException("Winlator TPI1 lifecycle state was not created.");
                lifecycleResetObserved = buttonMasks.All(value => value == 0) &&
                                         axes.All(value => value == 0) &&
                                         flats.All(value => value == 0) &&
                                         pointers.All(pointer => pointer.PointerId == 0 &&
                                                                 pointer.X == 0 && pointer.Y == 0 &&
                                                                 pointer.Pressure == 0 &&
                                                                 pointer.Buttons == 0);
                continue;
            }
            if (!source.TryCopyDeviceState(
                    deviceStableId, buttonMasks, axes, flats, pointers))
                throw new InvalidDataException("Winlator TPI1 device state disappeared unexpectedly.");

            switch (frames)
            {
                case 3:
                    buttonObserved = IsForwardedButtonPressed(
                        buttonMasks[0], ForwardedInputButton.Coin);
                    break;
                case 4:
                    axisObserved = axes[2] == -12345 && flats[2] == 256;
                    break;
                case 5:
                    pointerObserved = pointers[0].PointerId == 9 &&
                                      pointers[0].X == 12345 &&
                                      pointers[0].Y == 23456 &&
                                      pointers[0].Pressure == 30000 &&
                                      pointers[0].ToolType == 2 &&
                                      pointers[0].Buttons == 1;
                    break;
                case 6:
                    if (IsForwardedButtonPressed(buttonMasks[0], ForwardedInputButton.Coin))
                        throw new InvalidDataException("Winlator TPI1 coin release was not applied.");
                    break;
                case 7:
                    if (!IsForwardedButtonPressed(buttonMasks[0], ForwardedInputButton.Start))
                        throw new InvalidDataException("Winlator TPI1 start press was not applied.");
                    break;
                case 8:
                    released = buttonMasks.All(value => value == 0) &&
                               axes.All(value => value == 0) &&
                               flats.All(value => value == 0) &&
                               pointers.All(pointer => pointer.PointerId == 0 &&
                                                       pointer.X == 0 && pointer.Y == 0 &&
                                                       pointer.Pressure == 0 &&
                                                       pointer.Buttons == 0);
                    break;
            }
        }

        var producerStatus = await producerTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        if (producerStatus != "frames=6;queueRemaining=0;resync=1;dropped=0" ||
            frames != 8 || gaps != 0 || !lifecycleResetObserved ||
            !buttonObserved || !axisObserved || !pointerObserved || !released)
            throw new InvalidDataException(
                $"Winlator TPI1 diagnostic mismatch: producer={producerStatus};" +
                $"frames={frames};gaps={gaps};button={buttonObserved};axis={axisObserved};" +
                $"pointer={pointerObserved};reset={lifecycleResetObserved};release={released}");

        return producerStatus + ";hostFrames=8;hostGaps=0;release=1";
    }

    private static async Task<string> RunPreparedInputActivityDiagnosticAsync(
        ITeknoParrotWinlatorService service,
        TcpListener listener,
        WinlatorPreparedSession prepared,
        byte[] token,
        CancellationToken cancellationToken)
    {
        var sessionId = prepared.SessionId;
        RequireActivityLaunchRejected(
            service,
            new WinlatorActivityLaunchRequest(
                sessionId,
                checked(prepared.ContainerId + 1),
                WinlatorSessionContract.ForwardedInputDiagnosticLaunchKind),
            "container substitution");
        RequireActivityLaunchRejected(
            service,
            new WinlatorActivityLaunchRequest(
                Guid.NewGuid(),
                prepared.ContainerId,
                WinlatorSessionContract.ForwardedInputDiagnosticLaunchKind),
            "session substitution");
        RequireActivityLaunchEnvelopeRejected(
            service,
            JsonSerializer.SerializeToUtf8Bytes(new
            {
                protocolVersion = WinlatorSessionContract.ServiceProtocolVersion,
                sessionId = sessionId.ToString("N"),
                containerId = prepared.ContainerId,
                launchKind = "arbitrary-executable"
            }),
            "kind substitution");
        RequireActivityLaunchEnvelopeRejected(
            service,
            JsonSerializer.SerializeToUtf8Bytes(new
            {
                protocolVersion = WinlatorSessionContract.ServiceProtocolVersion,
                sessionId = sessionId.ToString("N"),
                containerId = prepared.ContainerId,
                launchKind = WinlatorSessionContract.ForwardedInputDiagnosticLaunchKind,
                executable = "C:\\untrusted.exe"
            }),
            "schema extension");

        var launchRequest = new WinlatorActivityLaunchRequest(
            sessionId,
            prepared.ContainerId,
            WinlatorSessionContract.ForwardedInputDiagnosticLaunchKind);
        var launchStatus = await Task.Run(
            () => service.LaunchPreparedActivity(
                WinlatorSessionContract.CreateActivityLaunch(launchRequest)),
            CancellationToken.None).ConfigureAwait(false);
        WinlatorSessionContract.ValidateActivityLaunchStatus(launchStatus, launchRequest);
        using var client = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
        client.NoDelay = true;
        client.ReceiveTimeout = 10000;
        using var stream = client.GetStream();

        var fixedHeader = new byte[58];
        await BridgeProtocol.ReadExactlyAsync(stream, fixedHeader, cancellationToken).ConfigureAwait(false);
        var channelNameLength = BinaryPrimitives.ReadUInt16BigEndian(fixedHeader.AsSpan(56, 2));
        if (channelNameLength == 0 || channelNameLength > BridgeProtocol.MaxPipeNameBytes)
            throw new InvalidDataException("Winlator Activity sent an invalid TPI1 channel-name length.");
        var channelName = new byte[channelNameLength];
        await BridgeProtocol.ReadExactlyAsync(stream, channelName, cancellationToken).ConfigureAwait(false);
        if (!BridgeProtocol.ValidateAuthenticatedHandshake(
                fixedHeader,
                channelName,
                sessionId,
                token,
                BridgeProtocol.ForwardedInputChannelKind,
                BridgeProtocol.ProbeInputChannelName,
                out var error))
            throw new UnauthorizedAccessException("Winlator Activity TPI1 handshake rejected: " + error);

        await stream.WriteAsync(BridgeProtocol.PipeAck, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);

        var source = new WinlatorForwardedInputSource();
        var reader = new ForwardedInputStreamReader(stream);
        var buttonMasks = new uint[WinlatorForwardedInputSource.MaximumPlayers];
        var axes = new short[
            WinlatorForwardedInputSource.MaximumPlayers * WinlatorForwardedInputSource.MaximumAxes];
        var flats = new ushort[axes.Length];
        var pointers = new ForwardedPointerState[WinlatorForwardedInputSource.MaximumPlayers];
        var frames = 0;
        var gaps = 0;
        var focusFrames = 0;
        var coinPressed = false;
        var coinReleased = false;
        var pointerPressed = false;
        var pointerReleased = false;
        uint buttonDevice = 0;
        uint pointerDevice = 0;

        while (reader.ReadAndApply(source, out var result))
        {
            frames++;
            if (result == ForwardedInputApplyResult.SequenceGap)
                gaps++;
            var header = reader.LastHeader;
            switch (header.Type)
            {
                case ForwardedInputFrameType.Focus:
                    focusFrames++;
                    break;
                case ForwardedInputFrameType.Button:
                    buttonDevice = header.DeviceStableId;
                    if (!source.TryCopyDeviceState(
                            buttonDevice, buttonMasks, axes, flats, pointers))
                        throw new InvalidDataException("Winlator Activity button state disappeared.");
                    if (IsForwardedButtonPressed(buttonMasks[0], ForwardedInputButton.Coin))
                        coinPressed = true;
                    else if (coinPressed)
                        coinReleased = true;
                    break;
                case ForwardedInputFrameType.PointerAbsolute:
                    pointerDevice = header.DeviceStableId;
                    if (!source.TryCopyDeviceState(
                            pointerDevice, buttonMasks, axes, flats, pointers))
                        throw new InvalidDataException("Winlator Activity pointer state disappeared.");
                    if (pointers[0].Buttons != 0 && pointers[0].Pressure != 0)
                        pointerPressed = true;
                    else if (pointerPressed && pointers[0].Buttons == 0 && pointers[0].Pressure == 0)
                        pointerReleased = true;
                    break;
            }
        }

        var releasedOnEof = true;
        if (buttonDevice != 0 && source.TryCopyDeviceState(
                buttonDevice, buttonMasks, axes, flats, pointers))
            releasedOnEof &= buttonMasks.All(value => value == 0);
        if (pointerDevice != 0 && source.TryCopyDeviceState(
                pointerDevice, buttonMasks, axes, flats, pointers))
            releasedOnEof &= pointers.All(pointer => pointer.Buttons == 0 && pointer.Pressure == 0);

        if (frames < 6 || gaps != 0 || focusFrames < 2 ||
            !coinPressed || !coinReleased || !pointerPressed || !pointerReleased || !releasedOnEof)
            throw new InvalidDataException(
                $"Winlator Activity TPI1 mismatch: launch={launchStatus};frames={frames};" +
                $"gaps={gaps};focus={focusFrames};coin={coinPressed}/{coinReleased};" +
                $"pointer={pointerPressed}/{pointerReleased};eofRelease={releasedOnEof}");

        return $"frames={frames};gaps=0;focus={focusFrames};coin=1;pointer=1;" +
               "eofRelease=1;immutableReject=4";
    }

    private static void RequireActivityLaunchRejected(
        ITeknoParrotWinlatorService service,
        WinlatorActivityLaunchRequest request,
        string scenario)
    {
        var rejected = false;
        try
        {
            service.LaunchPreparedActivity(WinlatorSessionContract.CreateActivityLaunch(request));
        }
        catch (Exception)
        {
            rejected = true;
        }

        if (!rejected)
            throw new InvalidDataException(
                "Winlator accepted Activity launch " + scenario + '.');
    }

    private static void RequireActivityLaunchEnvelopeRejected(
        ITeknoParrotWinlatorService service,
        byte[] request,
        string scenario)
    {
        var rejected = false;
        try
        {
            service.LaunchPreparedActivity(request);
        }
        catch (Exception)
        {
            rejected = true;
        }

        if (!rejected)
            throw new InvalidDataException(
                "Winlator accepted Activity launch " + scenario + '.');
    }

    private static bool IsForwardedButtonPressed(
        uint state,
        ForwardedInputButton button) =>
        (state & (1u << (int)button)) != 0;

    private static async Task RejectWrongTokenGuestAsync(
        TcpListener listener,
        Guid sessionId,
        byte[] expectedToken,
        string expectedPipeName,
        CancellationToken cancellationToken)
    {
        using var client = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
        client.NoDelay = true;
        using var stream = client.GetStream();
        var fixedHeader = new byte[58];
        await BridgeProtocol.ReadExactlyAsync(stream, fixedHeader, cancellationToken).ConfigureAwait(false);
        var pipeNameLength = BinaryPrimitives.ReadUInt16BigEndian(fixedHeader.AsSpan(56, 2));
        if (pipeNameLength == 0 || pipeNameLength > BridgeProtocol.MaxPipeNameBytes)
            throw new InvalidDataException("The rejection helper sent an invalid TPB1 pipe name.");
        var pipeName = new byte[pipeNameLength];
        await BridgeProtocol.ReadExactlyAsync(stream, pipeName, cancellationToken).ConfigureAwait(false);

        var presentedToken = fixedHeader.AsSpan(24, 32).ToArray();
        if (CryptographicOperations.FixedTimeEquals(presentedToken, expectedToken))
            throw new InvalidDataException("The wrong-token helper sent the accepted token.");
        if (!BridgeProtocol.ValidatePipeHandshake(
                fixedHeader, pipeName, sessionId, presentedToken, expectedPipeName, out var structuralError))
            throw new InvalidDataException(
                "The rejection fixture changed more than its token: " + structuralError);
        if (BridgeProtocol.ValidatePipeHandshake(
                fixedHeader, pipeName, sessionId, expectedToken, expectedPipeName, out var rejectionError) ||
            rejectionError != "token")
            throw new InvalidDataException(
                "The wrong-token helper was not rejected specifically for its token.");

        // Intentionally close without writing OKAY. The Windows helper must
        // disconnect its named-pipe client and allow the valid retry to start.
    }

    private static async Task ExchangeGuestPipeVectorAsync(
        TcpListener listener,
        byte architecture,
        Guid sessionId,
        byte[] token,
        string expectedPipeName,
        CancellationToken cancellationToken)
    {
        for (var phase = 0; phase < 2; phase++)
        {
            using var client = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
            client.NoDelay = true;
            using var stream = client.GetStream();

            var fixedHeader = new byte[58];
            await BridgeProtocol.ReadExactlyAsync(stream, fixedHeader, cancellationToken).ConfigureAwait(false);
            var pipeNameLength = BinaryPrimitives.ReadUInt16BigEndian(fixedHeader.AsSpan(56, 2));
            if (pipeNameLength == 0 || pipeNameLength > BridgeProtocol.MaxPipeNameBytes)
                throw new InvalidDataException(
                    $"The {architecture}-bit helper sent an invalid TPB1 pipe name.");
            var pipeName = new byte[pipeNameLength];
            await BridgeProtocol.ReadExactlyAsync(stream, pipeName, cancellationToken).ConfigureAwait(false);

            var wrongToken = (byte[])token.Clone();
            wrongToken[0] ^= 0x80;
            if (BridgeProtocol.ValidatePipeHandshake(
                    fixedHeader, pipeName, sessionId, wrongToken, expectedPipeName, out _))
                throw new InvalidDataException("TPB1 accepted a deliberately incorrect token.");
            if (!BridgeProtocol.ValidatePipeHandshake(
                    fixedHeader, pipeName, sessionId, token, expectedPipeName, out var error))
                throw new UnauthorizedAccessException(
                    $"The {architecture}-bit helper TPB1 handshake was rejected: {error}");
            await stream.WriteAsync(BridgeProtocol.PipeAck, cancellationToken).ConfigureAwait(false);

            var request = new byte[16];
            await BridgeProtocol.ReadExactlyAsync(stream, request, cancellationToken).ConfigureAwait(false);
            var expected = new byte[]
            {
                0x54, 0x50, 0x47, 0x31, architecture,
                0x10, 0x11, 0x12, 0x13, 0x14, 0x15, 0x16,
                0x17, 0x18, 0x19, 0x1A
            };
            if (!request.SequenceEqual(expected))
                throw new InvalidDataException(
                    $"The {architecture}-bit guest request vector did not match after phase {phase + 1}.");

            var response = new byte[]
            {
                0x54, 0x50, 0x52, 0x31, architecture,
                0x20, 0x21, 0x22, 0x23, 0x24, 0x25, 0x26,
                0x27, 0x28, 0x29, 0x2A
            };
            if (phase == 0)
            {
                await stream.WriteAsync(response, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                await ReceiveGuestStressAsync(stream, architecture, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await SendHostStressAsync(stream, response, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static async Task ReceiveGuestStressAsync(
        NetworkStream stream,
        byte architecture,
        CancellationToken cancellationToken)
    {
        const int stressBytes = 1024 * 1024;
        var header = new byte[12];
        await BridgeProtocol.ReadExactlyAsync(stream, header, cancellationToken).ConfigureAwait(false);
        if (!header.AsSpan(0, 4).SequenceEqual("TPS1"u8) ||
            BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(4, 4)) != stressBytes)
            throw new InvalidDataException(
                $"The {architecture}-bit guest sent an invalid randomized-stress header.");

        var guestState = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(8, 4));
        if (guestState == 0)
            throw new InvalidDataException("The randomized-stress guest seed cannot be zero.");
        var buffer = new byte[16 * 1024];
        var remaining = stressBytes;
        while (remaining > 0)
        {
            var count = Math.Min(buffer.Length, remaining);
            await BridgeProtocol.ReadExactlyAsync(
                stream, buffer.AsMemory(0, count), cancellationToken).ConfigureAwait(false);
            for (var index = 0; index < count; index++)
            {
                if (buffer[index] != NextStressByte(ref guestState))
                    throw new InvalidDataException(
                        $"The {architecture}-bit guest randomized payload changed.");
            }
            remaining -= count;
        }
    }

    private static async Task SendHostStressAsync(
        NetworkStream stream,
        byte[] response,
        CancellationToken cancellationToken)
    {
        const int stressBytes = 1024 * 1024;
        var packet = new byte[response.Length + 12 + stressBytes];
        response.CopyTo(packet, 0);
        var hostSeedBytes = RandomNumberGenerator.GetBytes(4);
        var hostState = BinaryPrimitives.ReadUInt32LittleEndian(hostSeedBytes);
        if (hostState == 0)
            hostState = 1;
        "TPS2"u8.CopyTo(packet.AsSpan(response.Length, 4));
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(response.Length + 4, 4), stressBytes);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(response.Length + 8, 4), hostState);
        var payloadOffset = response.Length + 12;
        for (var index = 0; index < stressBytes; index++)
            packet[payloadOffset + index] = NextStressByte(ref hostState);
        await stream.WriteAsync(packet, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static byte NextStressByte(ref uint state)
    {
        state ^= state << 13;
        state ^= state >> 17;
        state ^= state << 5;
        return (byte)state;
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

    private static async Task<string> RunPipeProbeAsync(
        ITeknoParrotWinlatorService service,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var token = RandomNumberGenerator.GetBytes(32);
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start(backlog: 1);
        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var winlatorTask = Task.Run(() => service.RunPipeProbe(
                sessionId.ToString("N"),
                port,
                Convert.ToHexString(token)));

            using var acceptTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            acceptTimeout.CancelAfter(TimeSpan.FromSeconds(10));
            using var client = await listener.AcceptTcpClientAsync(acceptTimeout.Token).ConfigureAwait(false);
            client.NoDelay = true;
            using var stream = client.GetStream();

            var fixedHeader = new byte[58];
            await BridgeProtocol.ReadExactlyAsync(stream, fixedHeader, cancellationToken).ConfigureAwait(false);
            var pipeNameLength = BinaryPrimitives.ReadUInt16BigEndian(fixedHeader.AsSpan(56, 2));
            if (pipeNameLength == 0 || pipeNameLength > BridgeProtocol.MaxPipeNameBytes)
                throw new InvalidDataException("Winlator sent an invalid pipe-name length.");
            var pipeName = new byte[pipeNameLength];
            await BridgeProtocol.ReadExactlyAsync(stream, pipeName, cancellationToken).ConfigureAwait(false);
            if (!BridgeProtocol.ValidatePipeHandshake(
                    fixedHeader,
                    pipeName,
                    sessionId,
                    token,
                    BridgeProtocol.WinlatorProbePipeName,
                    out var error))
                throw new UnauthorizedAccessException("Winlator TPB1 handshake rejected: " + error);

            await stream.WriteAsync(BridgeProtocol.PipeAck, cancellationToken).ConfigureAwait(false);
            var frameLength = new byte[4];
            for (var frame = 0; frame < 16; frame++)
            {
                await BridgeProtocol.ReadExactlyAsync(stream, frameLength, cancellationToken).ConfigureAwait(false);
                var length = BinaryPrimitives.ReadUInt32BigEndian(frameLength);
                if (length == 0 || length > BridgeProtocol.MaxFrameBytes)
                    throw new InvalidDataException($"Invalid Winlator frame length {length}.");
                var payload = new byte[length];
                await BridgeProtocol.ReadExactlyAsync(stream, payload, cancellationToken).ConfigureAwait(false);
                await stream.WriteAsync(frameLength, cancellationToken).ConfigureAwait(false);
                await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
            }

            return await winlatorTask.ConfigureAwait(false);
        }
        finally
        {
            listener.Stop();
        }
    }

    private void ShowSuccess(string message) => RunOnUiThread(() =>
    {
        if (_result != null)
            _result.Text = (_runGuestDiagnostic
                ? "WINLATOR WINDOWS GUEST SERVICE TEST PASSED\n\n"
                : "WINLATOR SERVICE SMOKE TEST PASSED\n\n") + message;
        if (_runButton != null)
            _runButton.Enabled = true;
    });

    private void ShowFailure(string message) => RunOnUiThread(() =>
    {
        if (_result != null)
            _result.Text = (_runGuestDiagnostic
                ? "WINLATOR WINDOWS GUEST SERVICE TEST FAILED\n\n"
                : "WINLATOR SERVICE SMOKE TEST FAILED\n\n") + message;
        if (_runButton != null)
            _runButton.Enabled = true;
    });
}

internal sealed class WinlatorServiceConnection : Java.Lang.Object, IServiceConnection
{
    private readonly Action<IBinder> _connected;
    private readonly Action<string> _failed;

    public WinlatorServiceConnection(Action<IBinder> connected, Action<string> failed)
    {
        _connected = connected;
        _failed = failed;
    }

    public void OnServiceConnected(ComponentName? name, IBinder? service)
    {
        if (service == null)
            _failed("Winlator bridge service returned a null Binder.");
        else
            _connected(service);
    }

    public void OnServiceDisconnected(ComponentName? name) =>
        _failed("Winlator bridge service disconnected unexpectedly.");

    public void OnBindingDied(ComponentName? name) =>
        _failed("Winlator bridge service binding died.");

    public void OnNullBinding(ComponentName? name) =>
        _failed("Winlator bridge service returned a null binding.");
}
#endif
