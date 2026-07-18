using System;
using System.Buffers.Binary;

namespace TeknoParrotUi.Common.InputListening.Forwarded
{
    public enum ForwardedInputFrameType : ushort
    {
        DeviceAdded = 1,
        DeviceRemoved = 2,
        Key = 3,
        Axis = 4,
        Button = 5,
        PointerAbsolute = 6,
        PointerRelative = 7,
        GamepadSnapshot = 8,
        Focus = 9,
        Suspend = 10
    }

    public enum ForwardedInputButton : ushort
    {
        Up = 0,
        Down = 1,
        Left = 2,
        Right = 3,
        Start = 4,
        Service = 5,
        Test = 6,
        Coin = 7,
        Button1 = 8,
        Button2 = 9,
        Button3 = 10,
        Button4 = 11,
        Button5 = 12,
        Button6 = 13,
        Button7 = 14,
        Button8 = 15,
        Count = 16
    }

    public readonly struct ForwardedInputFrameHeader
    {
        public ForwardedInputFrameHeader(
            ForwardedInputFrameType type,
            uint payloadLength,
            uint sequence,
            ulong eventTimeNanoseconds,
            uint deviceStableId)
        {
            Type = type;
            PayloadLength = payloadLength;
            Sequence = sequence;
            EventTimeNanoseconds = eventTimeNanoseconds;
            DeviceStableId = deviceStableId;
        }

        public ForwardedInputFrameType Type { get; }
        public uint PayloadLength { get; }
        public uint Sequence { get; }
        public ulong EventTimeNanoseconds { get; }
        public uint DeviceStableId { get; }
    }

    public readonly struct ForwardedPointerState
    {
        public ForwardedPointerState(
            uint pointerId,
            ushort x,
            ushort y,
            ushort pressure,
            byte toolType,
            uint buttons)
        {
            PointerId = pointerId;
            X = x;
            Y = y;
            Pressure = pressure;
            ToolType = toolType;
            Buttons = buttons;
        }

        public uint PointerId { get; }
        public ushort X { get; }
        public ushort Y { get; }
        public ushort Pressure { get; }
        public byte ToolType { get; }
        public uint Buttons { get; }
    }

    /// <summary>
    /// Allocation-free encoder/decoder for the high-rate Winlator input path.
    /// TPI1 integers are little-endian. Transport authentication and session
    /// ownership are established before frames reach this codec.
    /// </summary>
    public static class ForwardedInputProtocol
    {
        public const ushort ProtocolVersion = 1;
        public const int HeaderBytes = 28;
        public const int MaximumPayloadBytes = 1024;
        public const int MaximumPlayers = 4;
        public const int MaximumAxes = 16;
        public const int ButtonPayloadBytes = 4;
        public const int AxisPayloadBytes = 8;
        public const int PointerAbsolutePayloadBytes = 16;
        public const int FocusPayloadBytes = 4;

        private static ReadOnlySpan<byte> Magic => "TPI1"u8;

        public static bool TryReadHeader(
            ReadOnlySpan<byte> packet,
            out ForwardedInputFrameHeader header)
        {
            header = default;
            if (packet.Length < HeaderBytes ||
                !TryReadHeaderPrefix(packet[..HeaderBytes], out header))
                return false;

            return packet.Length == HeaderBytes + (int)header.PayloadLength;
        }

        public static bool TryReadHeaderPrefix(
            ReadOnlySpan<byte> prefix,
            out ForwardedInputFrameHeader header)
        {
            header = default;
            if (prefix.Length != HeaderBytes || !prefix[..4].SequenceEqual(Magic))
                return false;

            var version = BinaryPrimitives.ReadUInt16LittleEndian(prefix.Slice(4, 2));
            var typeValue = BinaryPrimitives.ReadUInt16LittleEndian(prefix.Slice(6, 2));
            var payloadLength = BinaryPrimitives.ReadUInt32LittleEndian(prefix.Slice(8, 4));
            if (version != ProtocolVersion ||
                typeValue < (ushort)ForwardedInputFrameType.DeviceAdded ||
                typeValue > (ushort)ForwardedInputFrameType.Suspend ||
                payloadLength > MaximumPayloadBytes)
                return false;

            header = new ForwardedInputFrameHeader(
                (ForwardedInputFrameType)typeValue,
                payloadLength,
                BinaryPrimitives.ReadUInt32LittleEndian(prefix.Slice(12, 4)),
                BinaryPrimitives.ReadUInt64LittleEndian(prefix.Slice(16, 8)),
                BinaryPrimitives.ReadUInt32LittleEndian(prefix.Slice(24, 4)));
            return true;
        }

        public static int WriteButtonFrame(
            Span<byte> destination,
            uint sequence,
            ulong eventTimeNanoseconds,
            uint deviceStableId,
            byte player,
            ForwardedInputButton button,
            bool pressed)
        {
            if (player >= MaximumPlayers)
                throw new ArgumentOutOfRangeException(nameof(player));
            if (button >= ForwardedInputButton.Count)
                throw new ArgumentOutOfRangeException(nameof(button));

            var length = WriteHeader(
                destination, ForwardedInputFrameType.Button, ButtonPayloadBytes,
                sequence, eventTimeNanoseconds, deviceStableId);
            destination[HeaderBytes] = player;
            destination[HeaderBytes + 1] = pressed ? (byte)1 : (byte)0;
            BinaryPrimitives.WriteUInt16LittleEndian(
                destination.Slice(HeaderBytes + 2, 2), (ushort)button);
            return length;
        }

        public static bool TryReadButton(
            ReadOnlySpan<byte> packet,
            out ForwardedInputFrameHeader header,
            out byte player,
            out ForwardedInputButton button,
            out bool pressed)
        {
            player = 0;
            button = default;
            pressed = false;
            if (!TryReadHeader(packet, out header) ||
                header.Type != ForwardedInputFrameType.Button ||
                header.PayloadLength != ButtonPayloadBytes)
                return false;

            player = packet[HeaderBytes];
            var pressedValue = packet[HeaderBytes + 1];
            button = (ForwardedInputButton)BinaryPrimitives.ReadUInt16LittleEndian(
                packet.Slice(HeaderBytes + 2, 2));
            if (player >= MaximumPlayers ||
                pressedValue > 1 || button >= ForwardedInputButton.Count)
                return false;
            pressed = pressedValue != 0;
            return true;
        }

        public static int WriteAxisFrame(
            Span<byte> destination,
            uint sequence,
            ulong eventTimeNanoseconds,
            uint deviceStableId,
            byte player,
            ushort axisId,
            short valueQ15,
            ushort flatQ15)
        {
            if (player >= MaximumPlayers)
                throw new ArgumentOutOfRangeException(nameof(player));
            if (axisId >= MaximumAxes)
                throw new ArgumentOutOfRangeException(nameof(axisId));
            if (flatQ15 > short.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(flatQ15));

            var length = WriteHeader(
                destination, ForwardedInputFrameType.Axis, AxisPayloadBytes,
                sequence, eventTimeNanoseconds, deviceStableId);
            destination[HeaderBytes] = player;
            destination[HeaderBytes + 1] = 0;
            BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(HeaderBytes + 2, 2), axisId);
            BinaryPrimitives.WriteInt16LittleEndian(destination.Slice(HeaderBytes + 4, 2), valueQ15);
            BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(HeaderBytes + 6, 2), flatQ15);
            return length;
        }

        public static bool TryReadAxis(
            ReadOnlySpan<byte> packet,
            out ForwardedInputFrameHeader header,
            out byte player,
            out ushort axisId,
            out short valueQ15,
            out ushort flatQ15)
        {
            player = 0;
            axisId = 0;
            valueQ15 = 0;
            flatQ15 = 0;
            if (!TryReadHeader(packet, out header) ||
                header.Type != ForwardedInputFrameType.Axis ||
                header.PayloadLength != AxisPayloadBytes ||
                packet[HeaderBytes + 1] != 0)
                return false;

            player = packet[HeaderBytes];
            axisId = BinaryPrimitives.ReadUInt16LittleEndian(packet.Slice(HeaderBytes + 2, 2));
            valueQ15 = BinaryPrimitives.ReadInt16LittleEndian(packet.Slice(HeaderBytes + 4, 2));
            flatQ15 = BinaryPrimitives.ReadUInt16LittleEndian(packet.Slice(HeaderBytes + 6, 2));
            return player < MaximumPlayers &&
                   axisId < MaximumAxes &&
                   flatQ15 <= short.MaxValue;
        }

        public static int WritePointerAbsoluteFrame(
            Span<byte> destination,
            uint sequence,
            ulong eventTimeNanoseconds,
            uint deviceStableId,
            byte player,
            byte toolType,
            ushort x,
            ushort y,
            ushort pressure,
            uint pointerId,
            uint buttons)
        {
            if (player >= MaximumPlayers)
                throw new ArgumentOutOfRangeException(nameof(player));

            var length = WriteHeader(
                destination, ForwardedInputFrameType.PointerAbsolute,
                PointerAbsolutePayloadBytes, sequence, eventTimeNanoseconds, deviceStableId);
            destination[HeaderBytes] = player;
            destination[HeaderBytes + 1] = toolType;
            BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(HeaderBytes + 2, 2), x);
            BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(HeaderBytes + 4, 2), y);
            BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(HeaderBytes + 6, 2), pressure);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(HeaderBytes + 8, 4), pointerId);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(HeaderBytes + 12, 4), buttons);
            return length;
        }

        public static bool TryReadPointerAbsolute(
            ReadOnlySpan<byte> packet,
            out ForwardedInputFrameHeader header,
            out byte player,
            out ForwardedPointerState pointer)
        {
            player = 0;
            pointer = default;
            if (!TryReadHeader(packet, out header) ||
                header.Type != ForwardedInputFrameType.PointerAbsolute ||
                header.PayloadLength != PointerAbsolutePayloadBytes)
                return false;

            player = packet[HeaderBytes];
            if (player >= MaximumPlayers)
                return false;
            pointer = new ForwardedPointerState(
                BinaryPrimitives.ReadUInt32LittleEndian(packet.Slice(HeaderBytes + 8, 4)),
                BinaryPrimitives.ReadUInt16LittleEndian(packet.Slice(HeaderBytes + 2, 2)),
                BinaryPrimitives.ReadUInt16LittleEndian(packet.Slice(HeaderBytes + 4, 2)),
                BinaryPrimitives.ReadUInt16LittleEndian(packet.Slice(HeaderBytes + 6, 2)),
                packet[HeaderBytes + 1],
                BinaryPrimitives.ReadUInt32LittleEndian(packet.Slice(HeaderBytes + 12, 4)));
            return true;
        }

        public static int WriteFocusFrame(
            Span<byte> destination,
            uint sequence,
            ulong eventTimeNanoseconds,
            uint deviceStableId,
            bool focused)
        {
            var length = WriteHeader(
                destination, ForwardedInputFrameType.Focus, FocusPayloadBytes,
                sequence, eventTimeNanoseconds, deviceStableId);
            destination.Slice(HeaderBytes, FocusPayloadBytes).Clear();
            destination[HeaderBytes] = focused ? (byte)1 : (byte)0;
            return length;
        }

        public static bool TryReadFocus(
            ReadOnlySpan<byte> packet,
            out ForwardedInputFrameHeader header,
            out bool focused)
        {
            focused = false;
            if (!TryReadHeader(packet, out header) ||
                header.Type != ForwardedInputFrameType.Focus ||
                header.PayloadLength != FocusPayloadBytes ||
                packet[HeaderBytes] > 1 || packet[HeaderBytes + 1] != 0 ||
                packet[HeaderBytes + 2] != 0 || packet[HeaderBytes + 3] != 0)
                return false;
            focused = packet[HeaderBytes] != 0;
            return true;
        }

        public static int WriteEmptyFrame(
            Span<byte> destination,
            ForwardedInputFrameType type,
            uint sequence,
            ulong eventTimeNanoseconds,
            uint deviceStableId)
        {
            if (type != ForwardedInputFrameType.DeviceRemoved &&
                type != ForwardedInputFrameType.Suspend)
                throw new ArgumentOutOfRangeException(nameof(type));
            return WriteHeader(destination, type, 0, sequence, eventTimeNanoseconds, deviceStableId);
        }

        public static bool IsNewerSequence(uint candidate, uint previous) =>
            unchecked((int)(candidate - previous)) > 0;

        private static int WriteHeader(
            Span<byte> destination,
            ForwardedInputFrameType type,
            int payloadLength,
            uint sequence,
            ulong eventTimeNanoseconds,
            uint deviceStableId)
        {
            var totalLength = HeaderBytes + payloadLength;
            if (payloadLength < 0 || payloadLength > MaximumPayloadBytes)
                throw new ArgumentOutOfRangeException(nameof(payloadLength));
            if (destination.Length < totalLength)
                throw new ArgumentException("The TPI1 destination buffer is too small.", nameof(destination));

            Magic.CopyTo(destination);
            BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(4, 2), ProtocolVersion);
            BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(6, 2), (ushort)type);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(8, 4), (uint)payloadLength);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(12, 4), sequence);
            BinaryPrimitives.WriteUInt64LittleEndian(destination.Slice(16, 8), eventTimeNanoseconds);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(24, 4), deviceStableId);
            return totalLength;
        }
    }
}
