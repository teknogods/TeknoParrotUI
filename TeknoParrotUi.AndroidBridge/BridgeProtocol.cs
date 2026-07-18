using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TeknoParrotUi.AndroidBridge;

internal sealed record BridgeSessionInfo(Guid SessionId, int PipePort, byte[] Token);

internal static class BridgeProtocol
{
    public const int ProtocolVersion = 1;
    public const int PageSize = 4096;
    public const int MaxPipeNameBytes = 128;
    public const int MaxFrameBytes = 64 * 1024;

    public const string ServiceAction = "com.teknoparrot.bridge.v1.BIND";
    public const string ServicePackage = "com.teknoparrot.ui";
    public const string ServiceClass = "com.teknoparrot.bridge.ArcadeSessionService";
    public const string BindPermission = "com.teknoparrot.permission.BIND_BRIDGE";
    public const string InterfaceDescriptor = "com.teknoparrot.bridge.v1.ITeknoParrotBridgeService";
    public const string ProbePipeName = "TeknoParrot_BridgeProbe";
    public const string ProbeInputChannelName = "TeknoParrot_ForwardedInput";
    public const ushort NamedPipeChannelKind = 1;
    public const ushort ForwardedInputChannelKind = 2;

    public const string WinlatorServiceAction = "com.teknoparrot.bridge.v1.WINLATOR_BIND";
    public const string WinlatorServicePackage = "com.teknoparrot.winlator";
    public const string WinlatorServiceClass = "com.winlator.teknoparrot.TeknoParrotBridgeService";
    public const string WinlatorInterfaceDescriptor =
        "com.teknoparrot.bridge.v1.ITeknoParrotWinlatorService";
    public const string WinlatorProbePipeName = "TeknoParrot_WinlatorProbe";

    // Kept away from generated AIDL transaction slots so ordinary AIDL methods
    // can be appended without changing this file-descriptor exchange.
    public const int OpenSharedPageTransaction = Android.OS.Binder.InterfaceConsts.FirstCallTransaction + 32;
    public const int OpenWinlatorSharedPageTransaction =
        Android.OS.Binder.InterfaceConsts.FirstCallTransaction + 32;
    public const int InstallWinlatorRuntimePackageTransaction =
        Android.OS.Binder.InterfaceConsts.FirstCallTransaction + 33;
    public const int QueryWinlatorRuntimePackagesTransaction =
        Android.OS.Binder.InterfaceConsts.FirstCallTransaction + 34;

    public const int LegacyOffset = 0;
    public const int LegacySize = 64;
    public const int MagicOffset = 64;
    public const int LayoutVersionOffset = 68;
    public const int HeaderSizeOffset = 70;
    public const int TotalSizeOffset = 72;
    public const int HostSequenceOffset = 76;
    public const int GuestSequenceOffset = 80;
    public const int HostTimestampOffset = 84;
    public const int GuestTimestampOffset = 92;
    public const int FlagsOffset = 100;

    public const uint FlagHostReady = 1u << 0;
    public const uint FlagPipeAuthenticated = 1u << 1;
    public const uint FlagGuestTouchedPage = 1u << 2;
    public const uint FlagStopping = 1u << 3;
    public const uint FlagFault = 1u << 4;

    public static readonly byte[] SharedPageMagic = Encoding.ASCII.GetBytes("TPJ1");
    public static readonly byte[] PipeAck = Encoding.ASCII.GetBytes("OKAY");

    public static string EncodeSessionResult(Guid sessionId, int port, byte[] token) =>
        $"{sessionId:N}|{port}|{Convert.ToHexString(token)}";

    public static bool TryDecodeSessionResult(string? value, out BridgeSessionInfo? session)
    {
        session = null;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var fields = value.Split('|');
        if (fields.Length != 3 ||
            !Guid.TryParseExact(fields[0], "N", out var id) ||
            !int.TryParse(fields[1], out var port) || port is < 1 or > 65535)
            return false;

        try
        {
            var token = Convert.FromHexString(fields[2]);
            if (token.Length != 32)
                return false;
            session = new BridgeSessionInfo(id, port, token);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public static byte[] BuildPipeHandshake(Guid sessionId, ReadOnlySpan<byte> token, string pipeName)
        => BuildAuthenticatedHandshake(
            sessionId, token, NamedPipeChannelKind, pipeName);

    public static byte[] BuildAuthenticatedHandshake(
        Guid sessionId,
        ReadOnlySpan<byte> token,
        ushort channelKind,
        string channelName)
    {
        if (token.Length != 32)
            throw new ArgumentException("Bridge token must be exactly 32 bytes.", nameof(token));
        if (channelKind is not (NamedPipeChannelKind or ForwardedInputChannelKind))
            throw new ArgumentOutOfRangeException(nameof(channelKind));

        var channelNameBytes = Encoding.UTF8.GetBytes(channelName);
        if (channelNameBytes.Length == 0 || channelNameBytes.Length > MaxPipeNameBytes)
            throw new ArgumentOutOfRangeException(nameof(channelName));

        var header = new byte[58 + channelNameBytes.Length];
        Encoding.ASCII.GetBytes("TPB1").CopyTo(header, 0);
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(4, 2), ProtocolVersion);
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(6, 2), channelKind);
        Convert.FromHexString(sessionId.ToString("N")).CopyTo(header, 8);
        token.CopyTo(header.AsSpan(24, 32));
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(56, 2), (ushort)channelNameBytes.Length);
        channelNameBytes.CopyTo(header, 58);
        return header;
    }

    public static bool ValidatePipeHandshake(
        ReadOnlySpan<byte> fixedHeader,
        ReadOnlySpan<byte> pipeName,
        Guid expectedSessionId,
        ReadOnlySpan<byte> expectedToken,
        string expectedPipeName,
        out string error)
        => ValidateAuthenticatedHandshake(
            fixedHeader,
            pipeName,
            expectedSessionId,
            expectedToken,
            NamedPipeChannelKind,
            expectedPipeName,
            out error);

    public static bool ValidateAuthenticatedHandshake(
        ReadOnlySpan<byte> fixedHeader,
        ReadOnlySpan<byte> channelName,
        Guid expectedSessionId,
        ReadOnlySpan<byte> expectedToken,
        ushort expectedChannelKind,
        string expectedChannelName,
        out string error)
    {
        error = string.Empty;
        if (fixedHeader.Length != 58)
        {
            error = "header length";
            return false;
        }

        if (!fixedHeader[..4].SequenceEqual("TPB1"u8))
        {
            error = "magic";
            return false;
        }

        if (BinaryPrimitives.ReadUInt16BigEndian(fixedHeader.Slice(4, 2)) != ProtocolVersion)
        {
            error = "protocol version";
            return false;
        }

        if (BinaryPrimitives.ReadUInt16BigEndian(fixedHeader.Slice(6, 2)) != expectedChannelKind)
        {
            error = "channel kind";
            return false;
        }

        var expectedId = Convert.FromHexString(expectedSessionId.ToString("N"));
        if (!CryptographicOperations.FixedTimeEquals(fixedHeader.Slice(8, 16), expectedId))
        {
            error = "session";
            return false;
        }

        if (expectedToken.Length != 32 ||
            !CryptographicOperations.FixedTimeEquals(fixedHeader.Slice(24, 32), expectedToken))
        {
            error = "token";
            return false;
        }

        var declaredNameLength = BinaryPrimitives.ReadUInt16BigEndian(fixedHeader.Slice(56, 2));
        if (declaredNameLength != channelName.Length || channelName.Length > MaxPipeNameBytes)
        {
            error = "channel-name length";
            return false;
        }

        if (!channelName.SequenceEqual(Encoding.UTF8.GetBytes(expectedChannelName)))
        {
            error = "channel name";
            return false;
        }

        return true;
    }

    public static ulong MonotonicNanoseconds()
    {
        var ticks = Stopwatch.GetTimestamp();
        return (ulong)(ticks * (1_000_000_000d / Stopwatch.Frequency));
    }

    public static async Task ReadExactlyAsync(Stream stream, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var read = 0;
        while (read < buffer.Length)
        {
            var count = await stream.ReadAsync(buffer[read..], cancellationToken).ConfigureAwait(false);
            if (count == 0)
                throw new EndOfStreamException($"Stream ended after {read} of {buffer.Length} bytes.");
            read += count;
        }
    }

    public static async Task<bool> ReadExactlyOrEofAsync(
        Stream stream,
        Memory<byte> buffer,
        CancellationToken cancellationToken)
    {
        var read = 0;
        while (read < buffer.Length)
        {
            var count = await stream.ReadAsync(buffer[read..], cancellationToken).ConfigureAwait(false);
            if (count == 0)
            {
                if (read == 0)
                    return false;
                throw new EndOfStreamException($"Stream ended after {read} of {buffer.Length} bytes.");
            }
            read += count;
        }
        return true;
    }
}
