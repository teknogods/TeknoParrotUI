using System;
using System.IO;

namespace TeknoParrotUi.Common.Pipes.Abstractions
{
    /// <summary>
    /// Presents an <see cref="IPipeServer"/> as a <see cref="Stream"/> so code
    /// that writes replies through a Stream (e.g. SerialPortHandler) works with
    /// both native pipes and Proton bridges.
    /// </summary>
    public class PipeServerStream : Stream
    {
        private readonly IPipeServer _server;

        public PipeServerStream(IPipeServer server)
        {
            _server = server ?? throw new ArgumentNullException(nameof(server));
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            _server.Read(buffer, offset, count);

        public override void Write(byte[] buffer, int offset, int count) =>
            _server.Write(buffer, offset, count);

        public override void Flush() => _server.Flush();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
