using System;
using System.Buffers.Binary;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.OS;
using TeknoParrotUi.AndroidBridge;
using TeknoParrotUi.Common.InputListening.Forwarded;

namespace TeknoParrotUi.Avalonia.Android;

[Service(
    Name = BridgeProtocol.ServiceClass,
    Exported = true,
    Permission = BridgeProtocol.BindPermission)]
[IntentFilter([BridgeProtocol.ServiceAction])]
public sealed class ArcadeSessionService : Service
{
    private BridgeServiceBinder? _binder;

    public override void OnCreate()
    {
        base.OnCreate();
        _binder = new BridgeServiceBinder(FilesDir!.AbsolutePath);
    }

    public override IBinder? OnBind(Intent? intent) => _binder;

    public override void OnDestroy()
    {
        _binder?.Shutdown();
        _binder?.Dispose();
        _binder = null;
        base.OnDestroy();
    }
}

internal sealed class BridgeServiceBinder : ITeknoParrotBridgeServiceStub
{
    private readonly object _sync = new();
    private readonly string _pagePath;
    private BridgeTestSession? _session;

    public BridgeServiceBinder(string filesDirectory)
    {
        var bridgeDirectory = Path.Combine(filesDirectory, "bridge-lab");
        Directory.CreateDirectory(bridgeDirectory);
        _pagePath = Path.Combine(bridgeDirectory, "TeknoParrot_JvsState.page");
    }

    public override int GetProtocolVersion() => BridgeProtocol.ProtocolVersion;

    public override string PrepareTestSession(string clientName)
    {
        if (string.IsNullOrWhiteSpace(clientName) || clientName.Length > 80)
            throw new ArgumentException("A short client name is required.", nameof(clientName));

        lock (_sync)
        {
            _session?.Dispose();
            _session = new BridgeTestSession(_pagePath);
            return BridgeProtocol.EncodeSessionResult(_session.SessionId, _session.Port, _session.Token);
        }
    }

    public override string GetSessionStatus(string sessionId)
    {
        lock (_sync)
            return TryGetSession(sessionId, out var session) ? session.GetStatus() : "state=missing";
    }

    public override void StopTestSession(string sessionId)
    {
        lock (_sync)
        {
            if (!TryGetSession(sessionId, out var session))
                return;
            session.Dispose();
            _session = null;
        }
    }

    public void Shutdown()
    {
        lock (_sync)
        {
            _session?.Dispose();
            _session = null;
        }
    }

    protected override bool OnTransact(int code, Parcel data, Parcel reply, int flags)
    {
        if (code != BridgeProtocol.OpenSharedPageTransaction)
            return base.OnTransact(code, data, reply, flags);

        data.EnforceInterface(BridgeProtocol.InterfaceDescriptor);
        var sessionId = data.ReadString() ?? string.Empty;
        lock (_sync)
        {
            if (!TryGetSession(sessionId, out var session))
                throw new InvalidOperationException("Bridge session is not active.");

            using var descriptor = session.OpenSharedPage();
            var fileDescriptor = descriptor.FileDescriptor ??
                throw new IOException("Bridge shared-page descriptor is unavailable.");
            reply.WriteNoException();
            reply.WriteFileDescriptor(fileDescriptor);
            return true;
        }
    }

    private bool TryGetSession(string sessionId, out BridgeTestSession session)
    {
        session = _session!;
        return _session != null &&
               Guid.TryParseExact(sessionId, "N", out var requested) &&
               requested == _session.SessionId;
    }
}

internal sealed class BridgeTestSession : IDisposable
{
    private readonly object _statusSync = new();
    private readonly string _pagePath;
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _stop = new();
    private readonly MappedSharedPage _page;
    private readonly Task _acceptLoop;
    private readonly Task _heartbeatLoop;
    private readonly WinlatorForwardedInputSource _inputSource = new();
    private int _pipeMessages;
    private int _inputFrames;
    private int _inputGaps;
    private int _inputButtonObserved;
    private int _inputAxisObserved;
    private int _inputPointerObserved;
    private int _inputReleaseObserved;
    private string _lastError = string.Empty;
    private int _disposed;

    public BridgeTestSession(string pagePath)
    {
        _pagePath = pagePath;
        SessionId = Guid.NewGuid();
        Token = RandomNumberGenerator.GetBytes(32);

        using (var file = new FileStream(pagePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite))
        {
            file.SetLength(BridgeProtocol.PageSize);
            file.Flush(flushToDisk: true);
        }

        var descriptor = ParcelFileDescriptor.Open(new Java.IO.File(pagePath), ParcelFileMode.ReadWrite)
                         ?? throw new InvalidOperationException("Could not open the shared-page descriptor.");
        _page = MappedSharedPage.Map(descriptor);
        InitializePage();

        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start(backlog: 2);
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _acceptLoop = Task.Run(AcceptLoopAsync);
        _heartbeatLoop = Task.Run(HeartbeatLoopAsync);
    }

    public Guid SessionId { get; }
    public byte[] Token { get; }
    public int Port { get; }

    public ParcelFileDescriptor OpenSharedPage() =>
        ParcelFileDescriptor.Open(new Java.IO.File(_pagePath), ParcelFileMode.ReadWrite)
        ?? throw new InvalidOperationException("Could not duplicate the shared-page descriptor.");

    public string GetStatus()
    {
        lock (_statusSync)
        {
            var flags = _page.ReadUInt32(BridgeProtocol.FlagsOffset);
            var guestSequence = _page.ReadUInt32(BridgeProtocol.GuestSequenceOffset);
            if (guestSequence != 0)
            {
                flags |= BridgeProtocol.FlagGuestTouchedPage;
                _page.WriteUInt32(BridgeProtocol.FlagsOffset, flags);
            }

            return $"state=ready;session={SessionId:N};port={Port};" +
                   $"pipeMessages={_pipeMessages};hostSeq={_page.ReadUInt32(BridgeProtocol.HostSequenceOffset)};" +
                   $"guestSeq={guestSequence};flags=0x{flags:X8};" +
                   $"inputFrames={Volatile.Read(ref _inputFrames)};" +
                   $"inputGaps={Volatile.Read(ref _inputGaps)};" +
                   $"inputButton={Volatile.Read(ref _inputButtonObserved)};" +
                   $"inputAxis={Volatile.Read(ref _inputAxisObserved)};" +
                   $"inputPointer={Volatile.Read(ref _inputPointerObserved)};" +
                   $"inputRelease={Volatile.Read(ref _inputReleaseObserved)};" +
                   $"error={_lastError}";
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        try
        {
            var flags = _page.ReadUInt32(BridgeProtocol.FlagsOffset) | BridgeProtocol.FlagStopping;
            _page.WriteUInt32(BridgeProtocol.FlagsOffset, flags);
        }
        catch
        {
            // Best-effort state publication during shutdown.
        }

        _stop.Cancel();
        _listener.Stop();
        try
        {
            Task.WaitAll([_acceptLoop, _heartbeatLoop], millisecondsTimeout: 1500);
        }
        catch
        {
            // Listener cancellation is expected to surface as an exception.
        }
        _page.Dispose();
        _stop.Dispose();
    }

    private void InitializePage()
    {
        _page.Clear();
        var legacyPattern = new byte[BridgeProtocol.LegacySize];
        for (var i = 0; i < legacyPattern.Length; i++)
            legacyPattern[i] = (byte)(i ^ 0xA5);

        _page.WriteBytes(BridgeProtocol.LegacyOffset, legacyPattern);
        _page.WriteBytes(BridgeProtocol.MagicOffset, BridgeProtocol.SharedPageMagic);
        _page.WriteUInt16(BridgeProtocol.LayoutVersionOffset, BridgeProtocol.ProtocolVersion);
        _page.WriteUInt16(BridgeProtocol.HeaderSizeOffset, 128);
        _page.WriteUInt32(BridgeProtocol.TotalSizeOffset, BridgeProtocol.PageSize);
        _page.WriteUInt32(BridgeProtocol.HostSequenceOffset, 1);
        _page.WriteUInt64(BridgeProtocol.HostTimestampOffset, BridgeProtocol.MonotonicNanoseconds());
        _page.WriteUInt32(BridgeProtocol.FlagsOffset, BridgeProtocol.FlagHostReady);
    }

    private async Task HeartbeatLoopAsync()
    {
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                var sequence = _page.ReadUInt32(BridgeProtocol.HostSequenceOffset);
                _page.WriteUInt64(BridgeProtocol.HostTimestampOffset, BridgeProtocol.MonotonicNanoseconds());
                _page.WriteUInt32(BridgeProtocol.HostSequenceOffset, sequence + 1);
                await Task.Delay(50, _stop.Token).ConfigureAwait(false);
            }
        }
        catch (global::System.OperationCanceledException)
        {
        }
    }

    private async Task AcceptLoopAsync()
    {
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                using var client = await _listener.AcceptTcpClientAsync(_stop.Token).ConfigureAwait(false);
                await HandleClientAsync(client, _stop.Token).ConfigureAwait(false);
            }
        }
        catch (global::System.OperationCanceledException)
        {
        }
        catch (ObjectDisposedException) when (_stop.IsCancellationRequested)
        {
        }
        catch (SocketException) when (_stop.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            SetError(ex.GetType().Name + ": " + ex.Message);
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        client.NoDelay = true;
        using var stream = client.GetStream();

        var fixedHeader = new byte[58];
        await BridgeProtocol.ReadExactlyAsync(stream, fixedHeader, cancellationToken).ConfigureAwait(false);
        var channelNameLength = BinaryPrimitives.ReadUInt16BigEndian(fixedHeader.AsSpan(56, 2));
        if (channelNameLength == 0 || channelNameLength > BridgeProtocol.MaxPipeNameBytes)
            throw new InvalidDataException("Invalid channel-name length.");

        var channelName = new byte[channelNameLength];
        await BridgeProtocol.ReadExactlyAsync(stream, channelName, cancellationToken).ConfigureAwait(false);
        var channelKind = BinaryPrimitives.ReadUInt16BigEndian(fixedHeader.AsSpan(6, 2));
        var expectedName = channelKind switch
        {
            BridgeProtocol.NamedPipeChannelKind => BridgeProtocol.ProbePipeName,
            BridgeProtocol.ForwardedInputChannelKind => BridgeProtocol.ProbeInputChannelName,
            _ => throw new InvalidDataException("Unsupported TPB1 channel kind.")
        };
        if (!BridgeProtocol.ValidateAuthenticatedHandshake(
                fixedHeader,
                channelName,
                SessionId,
                Token,
                channelKind,
                expectedName,
                out var error))
            throw new UnauthorizedAccessException("TPB1 handshake rejected: " + error);

        if (channelKind == BridgeProtocol.NamedPipeChannelKind)
        {
            var flags = _page.ReadUInt32(BridgeProtocol.FlagsOffset) | BridgeProtocol.FlagPipeAuthenticated;
            _page.WriteUInt32(BridgeProtocol.FlagsOffset, flags);
        }
        await stream.WriteAsync(BridgeProtocol.PipeAck, cancellationToken).ConfigureAwait(false);

        if (channelKind == BridgeProtocol.NamedPipeChannelKind)
            await HandlePipeFramesAsync(stream, cancellationToken).ConfigureAwait(false);
        else
            await HandleInputFramesAsync(stream, cancellationToken).ConfigureAwait(false);
    }

    private async Task HandlePipeFramesAsync(Stream stream, CancellationToken cancellationToken)
    {
        var frameLength = new byte[4];
        while (await BridgeProtocol.ReadExactlyOrEofAsync(stream, frameLength, cancellationToken).ConfigureAwait(false))
        {
            var length = BinaryPrimitives.ReadUInt32BigEndian(frameLength);
            if (length == 0 || length > BridgeProtocol.MaxFrameBytes)
                throw new InvalidDataException($"Invalid bridge frame length {length}.");

            var payload = new byte[length];
            await BridgeProtocol.ReadExactlyAsync(stream, payload, cancellationToken).ConfigureAwait(false);
            await stream.WriteAsync(frameLength, cancellationToken).ConfigureAwait(false);
            await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
            Interlocked.Increment(ref _pipeMessages);
        }
    }

    private async Task HandleInputFramesAsync(Stream stream, CancellationToken cancellationToken)
    {
        var packet = new byte[
            ForwardedInputProtocol.HeaderBytes + ForwardedInputProtocol.MaximumPayloadBytes];
        try
        {
            while (await BridgeProtocol.ReadExactlyOrEofAsync(
                       stream,
                       packet.AsMemory(0, ForwardedInputProtocol.HeaderBytes),
                       cancellationToken).ConfigureAwait(false))
            {
                if (!ForwardedInputProtocol.TryReadHeaderPrefix(
                        packet.AsSpan(0, ForwardedInputProtocol.HeaderBytes), out var header))
                    throw new InvalidDataException("Invalid TPI1 frame header.");

                var payloadLength = checked((int)header.PayloadLength);
                await BridgeProtocol.ReadExactlyAsync(
                    stream,
                    packet.AsMemory(ForwardedInputProtocol.HeaderBytes, payloadLength),
                    cancellationToken).ConfigureAwait(false);
                var result = _inputSource.ApplyFrame(
                    packet.AsSpan(0, ForwardedInputProtocol.HeaderBytes + payloadLength));
                if (result is ForwardedInputApplyResult.InvalidFrame or
                    ForwardedInputApplyResult.UnsupportedFrame)
                    throw new InvalidDataException("Rejected TPI1 frame: " + result);

                Interlocked.Increment(ref _inputFrames);
                if (result == ForwardedInputApplyResult.SequenceGap)
                    Interlocked.Increment(ref _inputGaps);
                ObserveInputFrame(packet, header);
            }
        }
        finally
        {
            _inputSource.ReleaseAll();
        }
    }

    private void ObserveInputFrame(byte[] packet, ForwardedInputFrameHeader header)
    {
        Span<uint> buttons = stackalloc uint[ForwardedInputProtocol.MaximumPlayers];
        Span<short> axes = stackalloc short[
            ForwardedInputProtocol.MaximumPlayers * ForwardedInputProtocol.MaximumAxes];
        Span<ushort> flats = stackalloc ushort[
            ForwardedInputProtocol.MaximumPlayers * ForwardedInputProtocol.MaximumAxes];
        Span<ForwardedPointerState> pointers = stackalloc ForwardedPointerState[
            ForwardedInputProtocol.MaximumPlayers];
        if (!_inputSource.TryCopyDeviceState(
                header.DeviceStableId, buttons, axes, flats, pointers))
            return;

        switch (header.Type)
        {
            case ForwardedInputFrameType.Button:
                if (ForwardedInputProtocol.TryReadButton(
                        packet.AsSpan(0, ForwardedInputProtocol.HeaderBytes + (int)header.PayloadLength),
                        out _, out var buttonPlayer, out var button, out var pressed) &&
                    pressed && (buttons[buttonPlayer] & (1u << (int)button)) != 0)
                    Interlocked.Exchange(ref _inputButtonObserved, 1);
                break;

            case ForwardedInputFrameType.Axis:
                if (ForwardedInputProtocol.TryReadAxis(
                        packet.AsSpan(0, ForwardedInputProtocol.HeaderBytes + (int)header.PayloadLength),
                        out _, out var axisPlayer, out var axisId,
                        out var valueQ15, out var flatQ15))
                {
                    var index = axisPlayer * ForwardedInputProtocol.MaximumAxes + axisId;
                    if (axes[index] == valueQ15 && flats[index] == flatQ15)
                        Interlocked.Exchange(ref _inputAxisObserved, 1);
                }
                break;

            case ForwardedInputFrameType.PointerAbsolute:
                if (ForwardedInputProtocol.TryReadPointerAbsolute(
                        packet.AsSpan(0, ForwardedInputProtocol.HeaderBytes + (int)header.PayloadLength),
                        out _, out var pointerPlayer, out var pointer) &&
                    pointers[pointerPlayer].PointerId == pointer.PointerId &&
                    pointers[pointerPlayer].X == pointer.X &&
                    pointers[pointerPlayer].Y == pointer.Y)
                    Interlocked.Exchange(ref _inputPointerObserved, 1);
                break;

            case ForwardedInputFrameType.Focus:
                if (ForwardedInputProtocol.TryReadFocus(
                        packet.AsSpan(0, ForwardedInputProtocol.HeaderBytes + (int)header.PayloadLength),
                        out _, out var focused) && !focused && AllReleased(buttons))
                    Interlocked.Exchange(ref _inputReleaseObserved, 1);
                break;
        }
    }

    private static bool AllReleased(ReadOnlySpan<uint> buttons)
    {
        foreach (var value in buttons)
        {
            if (value != 0)
                return false;
        }
        return true;
    }

    private void SetError(string message)
    {
        lock (_statusSync)
        {
            _lastError = message.Replace(';', ',');
            var flags = _page.ReadUInt32(BridgeProtocol.FlagsOffset) | BridgeProtocol.FlagFault;
            _page.WriteUInt32(BridgeProtocol.FlagsOffset, flags);
        }
    }
}
