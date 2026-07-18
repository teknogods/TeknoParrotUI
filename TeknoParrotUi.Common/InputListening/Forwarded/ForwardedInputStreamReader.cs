using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace TeknoParrotUi.Common.InputListening.Forwarded
{
    /// <summary>
    /// Allocation-free steady-state reader for the SOCK_STREAM fallback. The
    /// TPI1 header supplies framing; EOF and malformed frames release all held
    /// controls before control returns to the session owner.
    /// </summary>
    public sealed class ForwardedInputStreamReader
    {
        private readonly Stream _stream;
        private readonly byte[] _packet = new byte[
            ForwardedInputProtocol.HeaderBytes + ForwardedInputProtocol.MaximumPayloadBytes];

        public ForwardedInputStreamReader(Stream stream)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead)
                throw new ArgumentException("The TPI1 stream must be readable.", nameof(stream));
        }

        public ForwardedInputFrameHeader LastHeader { get; private set; }

        public ForwardedInputApplyResult LastResult { get; private set; } =
            ForwardedInputApplyResult.InvalidFrame;

        public bool ReadAndApply(
            WinlatorForwardedInputSource destination,
            out ForwardedInputApplyResult result)
        {
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));

            result = ForwardedInputApplyResult.InvalidFrame;
            LastResult = ForwardedInputApplyResult.InvalidFrame;
            bool hasHeader;
            try
            {
                hasHeader = ReadExactlyOrEof(
                    _packet.AsSpan(0, ForwardedInputProtocol.HeaderBytes));
            }
            catch
            {
                destination.ReleaseAll();
                throw;
            }
            if (!hasHeader)
            {
                destination.ReleaseAll();
                return false;
            }

            if (!ForwardedInputProtocol.TryReadHeaderPrefix(
                    _packet.AsSpan(0, ForwardedInputProtocol.HeaderBytes), out var header))
            {
                destination.ReleaseAll();
                throw new InvalidDataException("The TPI1 stream contained an invalid frame header.");
            }
            LastHeader = header;

            var payloadLength = checked((int)header.PayloadLength);
            try
            {
                ReadExactly(_packet.AsSpan(ForwardedInputProtocol.HeaderBytes, payloadLength));
            }
            catch
            {
                destination.ReleaseAll();
                throw;
            }

            result = destination.ApplyFrame(
                _packet.AsSpan(0, ForwardedInputProtocol.HeaderBytes + payloadLength));
            LastResult = result;
            if (result == ForwardedInputApplyResult.InvalidFrame ||
                result == ForwardedInputApplyResult.UnsupportedFrame)
            {
                destination.ReleaseAll();
                throw new InvalidDataException(
                    result == ForwardedInputApplyResult.UnsupportedFrame
                        ? "The TPI1 stream contained an unsupported frame type."
                        : "The TPI1 stream contained an invalid frame payload.");
            }
            return true;
        }

        /// <summary>
        /// Reads and applies one frame while allowing a long-lived network
        /// session to interrupt an otherwise idle read. The reader keeps the
        /// same reusable packet buffer as the synchronous path.
        /// </summary>
        public async ValueTask<bool> ReadAndApplyAsync(
            WinlatorForwardedInputSource destination,
            CancellationToken cancellationToken = default)
        {
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));

            LastResult = ForwardedInputApplyResult.InvalidFrame;
            bool hasHeader;
            try
            {
                hasHeader = await ReadExactlyOrEofAsync(
                    _packet.AsMemory(0, ForwardedInputProtocol.HeaderBytes),
                    cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                destination.ReleaseAll();
                throw;
            }
            if (!hasHeader)
            {
                destination.ReleaseAll();
                return false;
            }

            if (!ForwardedInputProtocol.TryReadHeaderPrefix(
                    _packet.AsSpan(0, ForwardedInputProtocol.HeaderBytes), out var header))
            {
                destination.ReleaseAll();
                throw new InvalidDataException("The TPI1 stream contained an invalid frame header.");
            }
            LastHeader = header;

            var payloadLength = checked((int)header.PayloadLength);
            try
            {
                await ReadExactlyAsync(
                    _packet.AsMemory(ForwardedInputProtocol.HeaderBytes, payloadLength),
                    cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                destination.ReleaseAll();
                throw;
            }

            LastResult = destination.ApplyFrame(
                _packet.AsSpan(0, ForwardedInputProtocol.HeaderBytes + payloadLength));
            if (LastResult == ForwardedInputApplyResult.InvalidFrame ||
                LastResult == ForwardedInputApplyResult.UnsupportedFrame)
            {
                destination.ReleaseAll();
                throw new InvalidDataException(
                    LastResult == ForwardedInputApplyResult.UnsupportedFrame
                        ? "The TPI1 stream contained an unsupported frame type."
                        : "The TPI1 stream contained an invalid frame payload.");
            }
            return true;
        }

        private bool ReadExactlyOrEof(Span<byte> destination)
        {
            var offset = 0;
            while (offset < destination.Length)
            {
                var count = _stream.Read(destination[offset..]);
                if (count == 0)
                {
                    if (offset == 0)
                        return false;
                    throw new EndOfStreamException(
                        $"The TPI1 stream ended after {offset} of {destination.Length} bytes.");
                }
                offset += count;
            }
            return true;
        }

        private void ReadExactly(Span<byte> destination)
        {
            if (!ReadExactlyOrEof(destination))
                throw new EndOfStreamException("The TPI1 stream ended before its payload.");
        }

        private async ValueTask<bool> ReadExactlyOrEofAsync(
            Memory<byte> destination,
            CancellationToken cancellationToken)
        {
            var offset = 0;
            while (offset < destination.Length)
            {
                var count = await _stream.ReadAsync(
                    destination[offset..], cancellationToken).ConfigureAwait(false);
                if (count == 0)
                {
                    if (offset == 0)
                        return false;
                    throw new EndOfStreamException(
                        $"The TPI1 stream ended after {offset} of {destination.Length} bytes.");
                }
                offset += count;
            }
            return true;
        }

        private async ValueTask ReadExactlyAsync(
            Memory<byte> destination,
            CancellationToken cancellationToken)
        {
            if (!await ReadExactlyOrEofAsync(destination, cancellationToken).ConfigureAwait(false))
                throw new EndOfStreamException("The TPI1 stream ended before its payload.");
        }
    }
}
