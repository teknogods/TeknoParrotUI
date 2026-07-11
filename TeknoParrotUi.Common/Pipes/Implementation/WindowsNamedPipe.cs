using System.IO.Pipes;
using TeknoParrotUi.Common.Pipes.Abstractions;

namespace TeknoParrotUi.Common.Pipes.Implementation
{
    /// <summary>
    /// Wraps <see cref="NamedPipeServerStream"/> in the <see cref="IPipeServer"/>
    /// abstraction. This is the pre-existing behavior used by all games today.
    /// Note: .NET also emulates named pipes on Linux (Unix domain sockets at
    /// /tmp/CoreFxPipe_*), but games running in Proton cannot see those - that is
    /// what ProtonBridgePipe (Phase 2) addresses.
    /// </summary>
    public class WindowsNamedPipe : IPipeServer
    {
        private readonly NamedPipeServerStream _server;

        public string PipeName { get; }
        public bool IsConnected => _server.IsConnected;

        public WindowsNamedPipe(string pipeName)
        {
            PipeName = pipeName;
            _server = new NamedPipeServerStream(pipeName);
        }

        public void WaitForConnection() => _server.WaitForConnection();

        public int Read(byte[] buffer, int offset, int count) => _server.Read(buffer, offset, count);

        public void Write(byte[] buffer, int offset, int count) => _server.Write(buffer, offset, count);

        public void Flush() => _server.Flush();

        public void Close() => _server.Close();

        public void Dispose() => _server.Dispose();
    }
}
