using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Widget;
using TeknoParrotUi.AndroidBridge;
using TeknoParrotUi.Common.InputListening.Forwarded;

namespace TeknoParrotUi.BridgeProbe.Android;

[Activity(
    Label = "TeknoParrot Bridge Probe",
    MainLauncher = true,
    Theme = "@android:style/Theme.Material.NoActionBar",
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize)]
public sealed class MainActivity : global::Android.App.Activity
{
    private TextView? _result;
    private Button? _runButton;
    private ProbeServiceConnection? _connection;
    private bool _bound;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        var root = new LinearLayout(this)
        {
            Orientation = Orientation.Vertical
        };
        root.SetGravity(GravityFlags.CenterHorizontal);
        root.SetPadding(32, 48, 32, 32);

        _runButton = new Button(this) { Text = "Run bridge smoke test" };
        _result = new TextView(this)
        {
            TextSize = 14,
            Typeface = global::Android.Graphics.Typeface.Monospace,
            Text = "Install the TeknoParrot UI host APK, then run the test."
        };
        _runButton.Click += (_, _) => BindAndRun();
        root.AddView(_runButton);
        root.AddView(_result);
        SetContentView(root);
        BindAndRun();
    }

    protected override void OnDestroy()
    {
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

        if (_bound && _connection != null)
        {
            UnbindService(_connection);
            _bound = false;
        }

        _runButton.Enabled = false;
        _result.Text = "Binding to com.teknoparrot.ui...";
        _connection = new ProbeServiceConnection(OnServiceConnected, ShowFailure);

        var intent = new Intent(BridgeProtocol.ServiceAction);
        intent.SetComponent(new ComponentName(BridgeProtocol.ServicePackage, BridgeProtocol.ServiceClass));
        _bound = BindService(intent, _connection, Bind.AutoCreate);
        if (!_bound)
            ShowFailure("Android refused the bridge binding. Verify the host APK and matching signatures.");
    }

    private void OnServiceConnected(IBinder binder)
    {
        var service = ITeknoParrotBridgeServiceStub.AsInterface(binder);
        _ = Task.Run(() => RunProbeAsync(service)).ContinueWith(task =>
        {
            if (task.IsFaulted)
                ShowFailure(task.Exception?.GetBaseException().Message ?? "Unknown probe failure.");
            else if (task.IsCanceled)
                ShowFailure("Probe was cancelled.");
            else
                ShowSuccess(task.Result);
        }, TaskScheduler.Default);
    }

    private static async Task<string> RunProbeAsync(ITeknoParrotBridgeService service)
    {
        if (service.GetProtocolVersion() != BridgeProtocol.ProtocolVersion)
            throw new InvalidOperationException("Bridge protocol version mismatch.");

        var encoded = service.PrepareTestSession("dotnet-emulator-probe");
        if (!BridgeProtocol.TryDecodeSessionResult(encoded, out var session) || session == null)
            throw new InvalidDataException("Host returned an invalid session descriptor.");

        var report = new List<string>
        {
            $"AIDL: PASS (v{BridgeProtocol.ProtocolVersion})",
            $"Session: {session.SessionId:N}",
            $"Pipe port: {session.PipePort}"
        };

        try
        {
            using (var page = MappedSharedPage.Map(OpenSharedPage(service, session.SessionId)))
            {
                if (!page.ReadBytes(BridgeProtocol.MagicOffset, 4).SequenceEqual(BridgeProtocol.SharedPageMagic))
                    throw new InvalidDataException("Shared page magic is not TPJ1.");
                if (page.ReadUInt16(BridgeProtocol.LayoutVersionOffset) != BridgeProtocol.ProtocolVersion ||
                    page.ReadUInt32(BridgeProtocol.TotalSizeOffset) != BridgeProtocol.PageSize)
                    throw new InvalidDataException("Shared page layout metadata is invalid.");

                var legacy = page.ReadBytes(BridgeProtocol.LegacyOffset, BridgeProtocol.LegacySize);
                for (var i = 0; i < legacy.Length; i++)
                {
                    if (legacy[i] != (byte)(i ^ 0xA5))
                        throw new InvalidDataException($"Legacy page mismatch at byte {i}.");
                }

                var firstHostSequence = page.ReadUInt32(BridgeProtocol.HostSequenceOffset);
                await Task.Delay(160).ConfigureAwait(false);
                var secondHostSequence = page.ReadUInt32(BridgeProtocol.HostSequenceOffset);
                if (secondHostSequence <= firstHostSequence)
                    throw new InvalidDataException("Host heartbeat is not updating the shared page.");

                const uint guestMarker = 0xC0DEC0DE;
                page.WriteUInt64(BridgeProtocol.GuestTimestampOffset, BridgeProtocol.MonotonicNanoseconds());
                page.WriteUInt32(BridgeProtocol.GuestSequenceOffset, guestMarker);
                report.Add($"Shared page: PASS (host {firstHostSequence}->{secondHostSequence}, guest 0x{guestMarker:X8})");
            }

            var latency = await RunPipeProbeAsync(session, CancellationToken.None).ConfigureAwait(false);
            report.Add($"Pipe TPB1 + echo: PASS ({latency.Count} frames, avg {latency.Average():F2} ms, max {latency.Max():F2} ms)");
            await RunForwardedInputProbeAsync(session, CancellationToken.None).ConfigureAwait(false);
            report.Add("Forwarded input TPI1: PASS (button + axis + pointer + gap + release)");

            await Task.Delay(200).ConfigureAwait(false);
            var status = service.GetSessionStatus(session.SessionId.ToString("N"));
            if (!status.Contains("pipeMessages=16", StringComparison.Ordinal) ||
                !status.Contains("guestSeq=3235823838", StringComparison.Ordinal) ||
                !status.Contains("inputFrames=6", StringComparison.Ordinal) ||
                !status.Contains("inputGaps=1", StringComparison.Ordinal) ||
                !status.Contains("inputButton=1", StringComparison.Ordinal) ||
                !status.Contains("inputAxis=1", StringComparison.Ordinal) ||
                !status.Contains("inputPointer=1", StringComparison.Ordinal) ||
                !status.Contains("inputRelease=1", StringComparison.Ordinal))
                throw new InvalidDataException("Host status did not observe all three data paths: " + status);
            report.Add("Host observation: PASS");
            report.Add(status);
            return string.Join("\n", report);
        }
        finally
        {
            service.StopTestSession(session.SessionId.ToString("N"));
        }
    }

    private static ParcelFileDescriptor OpenSharedPage(ITeknoParrotBridgeService service, Guid sessionId)
    {
        var data = Parcel.Obtain();
        var reply = Parcel.Obtain();
        try
        {
            data.WriteInterfaceToken(BridgeProtocol.InterfaceDescriptor);
            data.WriteString(sessionId.ToString("N"));
            var remote = service.AsBinder()
                         ?? throw new InvalidOperationException("Host service has no Binder handle.");
            if (!remote.Transact(BridgeProtocol.OpenSharedPageTransaction, data, reply, 0))
                throw new InvalidOperationException("Host rejected the shared-page Binder transaction.");
            reply.ReadException();
            return reply.ReadFileDescriptor()
                   ?? throw new InvalidOperationException("Host returned no shared-page descriptor.");
        }
        finally
        {
            reply.Recycle();
            data.Recycle();
        }
    }

    private static async Task<IReadOnlyList<double>> RunPipeProbeAsync(
        BridgeSessionInfo session,
        CancellationToken cancellationToken)
    {
        using var client = new TcpClient { NoDelay = true };
        await client.ConnectAsync(IPAddress.Loopback, session.PipePort, cancellationToken).ConfigureAwait(false);
        using var stream = client.GetStream();

        var handshake = BridgeProtocol.BuildPipeHandshake(
            session.SessionId,
            session.Token,
            BridgeProtocol.ProbePipeName);
        await stream.WriteAsync(handshake, cancellationToken).ConfigureAwait(false);
        var acknowledgement = new byte[BridgeProtocol.PipeAck.Length];
        await BridgeProtocol.ReadExactlyAsync(stream, acknowledgement, cancellationToken).ConfigureAwait(false);
        if (!acknowledgement.SequenceEqual(BridgeProtocol.PipeAck))
            throw new UnauthorizedAccessException("Host rejected the TPB1 handshake.");

        var latencies = new List<double>();
        for (var i = 0; i < 16; i++)
        {
            var payload = Encoding.UTF8.GetBytes($"probe-frame-{i:D2}-{BridgeProtocol.MonotonicNanoseconds()}");
            var length = new byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(length, (uint)payload.Length);

            var started = Stopwatch.GetTimestamp();
            await stream.WriteAsync(length, cancellationToken).ConfigureAwait(false);
            await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);

            var echoedLength = new byte[4];
            await BridgeProtocol.ReadExactlyAsync(stream, echoedLength, cancellationToken).ConfigureAwait(false);
            var responseLength = BinaryPrimitives.ReadUInt32BigEndian(echoedLength);
            if (responseLength != payload.Length)
                throw new InvalidDataException("Echo frame length mismatch.");
            var echoedPayload = new byte[responseLength];
            await BridgeProtocol.ReadExactlyAsync(stream, echoedPayload, cancellationToken).ConfigureAwait(false);
            if (!echoedPayload.SequenceEqual(payload))
                throw new InvalidDataException("Echo frame payload mismatch.");

            latencies.Add(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
        return latencies;
    }

    private static async Task RunForwardedInputProbeAsync(
        BridgeSessionInfo session,
        CancellationToken cancellationToken)
    {
        using var client = new TcpClient { NoDelay = true };
        await client.ConnectAsync(IPAddress.Loopback, session.PipePort, cancellationToken).ConfigureAwait(false);
        using var stream = client.GetStream();

        var handshake = BridgeProtocol.BuildAuthenticatedHandshake(
            session.SessionId,
            session.Token,
            BridgeProtocol.ForwardedInputChannelKind,
            BridgeProtocol.ProbeInputChannelName);
        await stream.WriteAsync(handshake, cancellationToken).ConfigureAwait(false);
        var acknowledgement = new byte[BridgeProtocol.PipeAck.Length];
        await BridgeProtocol.ReadExactlyAsync(stream, acknowledgement, cancellationToken).ConfigureAwait(false);
        if (!acknowledgement.SequenceEqual(BridgeProtocol.PipeAck))
            throw new UnauthorizedAccessException("Host rejected the forwarded-input handshake.");

        const uint deviceId = 0xC011CAFE;
        var packet = new byte[
            ForwardedInputProtocol.HeaderBytes + ForwardedInputProtocol.MaximumPayloadBytes];
        var length = ForwardedInputProtocol.WriteButtonFrame(
            packet, 1, BridgeProtocol.MonotonicNanoseconds(), deviceId,
            0, ForwardedInputButton.Coin, true);
        await stream.WriteAsync(packet.AsMemory(0, length), cancellationToken).ConfigureAwait(false);

        length = ForwardedInputProtocol.WriteAxisFrame(
            packet, 2, BridgeProtocol.MonotonicNanoseconds(), deviceId,
            1, 3, 12345, 22);
        await stream.WriteAsync(packet.AsMemory(0, length), cancellationToken).ConfigureAwait(false);

        length = ForwardedInputProtocol.WritePointerAbsoluteFrame(
            packet, 3, BridgeProtocol.MonotonicNanoseconds(), deviceId,
            1, 2, 0, ushort.MaxValue, 32768, 0xDEADBEEF, 1);
        await stream.WriteAsync(packet.AsMemory(0, length), cancellationToken).ConfigureAwait(false);

        length = ForwardedInputProtocol.WriteButtonFrame(
            packet, 4, BridgeProtocol.MonotonicNanoseconds(), deviceId,
            0, ForwardedInputButton.Coin, false);
        await stream.WriteAsync(packet.AsMemory(0, length), cancellationToken).ConfigureAwait(false);

        // Sequence 5 is deliberately absent. The host must clear device-owned
        // state before applying the next edge and report one resync gap.
        length = ForwardedInputProtocol.WriteButtonFrame(
            packet, 6, BridgeProtocol.MonotonicNanoseconds(), deviceId,
            0, ForwardedInputButton.Start, true);
        await stream.WriteAsync(packet.AsMemory(0, length), cancellationToken).ConfigureAwait(false);

        length = ForwardedInputProtocol.WriteFocusFrame(
            packet, 7, BridgeProtocol.MonotonicNanoseconds(), deviceId, false);
        await stream.WriteAsync(packet.AsMemory(0, length), cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private void ShowSuccess(string message) => RunOnUiThread(() =>
    {
        if (_result != null)
            _result.Text = "BRIDGE SMOKE TEST PASSED\n\n" + message;
        if (_runButton != null)
            _runButton.Enabled = true;
    });

    private void ShowFailure(string message) => RunOnUiThread(() =>
    {
        if (_result != null)
            _result.Text = "BRIDGE SMOKE TEST FAILED\n\n" + message;
        if (_runButton != null)
            _runButton.Enabled = true;
    });
}

internal sealed class ProbeServiceConnection : Java.Lang.Object, IServiceConnection
{
    private readonly Action<IBinder> _connected;
    private readonly Action<string> _failed;

    public ProbeServiceConnection(Action<IBinder> connected, Action<string> failed)
    {
        _connected = connected;
        _failed = failed;
    }

    public void OnServiceConnected(ComponentName? name, IBinder? service)
    {
        if (service == null)
            _failed("Bridge service returned a null Binder.");
        else
            _connected(service);
    }

    public void OnServiceDisconnected(ComponentName? name) =>
        _failed("Bridge service disconnected unexpectedly.");

    public void OnBindingDied(ComponentName? name) =>
        _failed("Bridge service binding died.");

    public void OnNullBinding(ComponentName? name) =>
        _failed("Bridge service returned a null binding.");
}
