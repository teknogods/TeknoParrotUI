using System;

namespace TeknoParrotUi.Common.Pipes.Abstractions
{
    /// <summary>
    /// Platform-agnostic pipe server interface.
    /// Abstracts Windows named pipes (existing behavior) and future Linux/Proton bridges.
    /// </summary>
    public interface IPipeServer : IDisposable
    {
        /// <summary>
        /// Name of the pipe (e.g. "TeknoParrotPipe").
        /// </summary>
        string PipeName { get; }

        /// <summary>
        /// True when a client is connected.
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Blocks until a client connects to the pipe.
        /// </summary>
        void WaitForConnection();

        /// <summary>
        /// Reads bytes from the connected client.
        /// </summary>
        int Read(byte[] buffer, int offset, int count);

        /// <summary>
        /// Writes bytes to the connected client.
        /// </summary>
        void Write(byte[] buffer, int offset, int count);

        /// <summary>
        /// Flushes pending writes.
        /// </summary>
        void Flush();

        /// <summary>
        /// Closes the pipe. The server cannot be reused afterwards;
        /// create a new instance via ControlPipeFactory instead.
        /// </summary>
        void Close();
    }
}
