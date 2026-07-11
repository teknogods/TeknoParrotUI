using System.IO.Pipes;
using System.Runtime.InteropServices;
using TeknoParrotUi.Common.Pipes.Abstractions;
using TeknoParrotUi.Common.Pipes.Implementation;
using TeknoParrotUi.Common.Proton;

namespace TeknoParrotUi.Common.Pipes.PipeFactory
{
    /// <summary>
    /// Creates the correct <see cref="IPipeServer"/> for the current platform.
    ///
    /// Windows: always a real named pipe (existing pre-Proton behavior,
    /// byte-for-byte unchanged - see <paramref name="options"/>). The Linux
    /// branch below is unreachable on Windows: RuntimeInformation.IsOSPlatform
    /// (OSPlatform.Linux) is false, so it is effectively #ifdef'd out at
    /// runtime for Windows builds/installs.
    ///
    /// Linux with a Proton session active: ProtonBridgePipe, which exposes the
    /// real Windows named pipe inside the game's Wine prefix via pipehelper.exe.
    /// Linux without Proton: .NET's local named-pipe emulation (Unix socket),
    /// used by tools/tests.
    /// </summary>
    public static class ControlPipeFactory
    {
        /// <param name="pipeName">Pipe name (e.g. "TeknoParrotPipe", "TeknoParrot_JVS").</param>
        /// <param name="options">
        /// PipeOptions for the Windows named pipe. Callers must pass the same
        /// value the pre-Proton code used directly (e.g. SerialPortHandler's
        /// JVS pipe used PipeOptions.Asynchronous) so Windows I/O behavior is
        /// unaffected by this abstraction. Ignored on the Linux/Proton branch.
        /// </param>
        public static IPipeServer CreatePipe(string pipeName, PipeOptions options = PipeOptions.None)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && ProtonRuntime.IsActive)
            {
                return new ProtonBridgePipe(pipeName, ProtonRuntime.CurrentGame);
            }

            return new WindowsNamedPipe(pipeName, options);
        }
    }
}
