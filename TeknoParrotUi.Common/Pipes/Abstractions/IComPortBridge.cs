using System;

namespace TeknoParrotUi.Common.Pipes.Abstractions
{
    /// <summary>
    /// Platform-agnostic COM (serial) port bridge for games that talk JVS over a
    /// serial port (Type-X2, Ex-Board). On Windows this maps to the existing
    /// serial/COM handling; on Linux+Proton it will be backed by a bridge that
    /// masquerades as COM1 inside the Proton prefix.
    /// </summary>
    public interface IComPortBridge : IDisposable
    {
        /// <summary>
        /// Port name the game opens (e.g. "COM1").
        /// </summary>
        string PortName { get; }

        /// <summary>
        /// True when the bridge is up and the game side is connected.
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Starts the bridge (creates the port endpoint the game will open).
        /// </summary>
        void Start();

        /// <summary>
        /// Stops the bridge and releases the port endpoint.
        /// </summary>
        void Stop();

        /// <summary>
        /// Reads bytes the game wrote to the port (JVS commands).
        /// </summary>
        int Read(byte[] buffer, int offset, int count);

        /// <summary>
        /// Writes bytes for the game to read from the port (JVS responses).
        /// </summary>
        void Write(byte[] buffer, int offset, int count);
    }
}
