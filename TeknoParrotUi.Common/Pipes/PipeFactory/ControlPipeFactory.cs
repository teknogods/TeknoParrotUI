using System.Runtime.InteropServices;
using TeknoParrotUi.Common.Pipes.Abstractions;
using TeknoParrotUi.Common.Pipes.Implementation;
using TeknoParrotUi.Common.Proton;

namespace TeknoParrotUi.Common.Pipes.PipeFactory
{
    /// <summary>
    /// Creates the correct <see cref="IPipeServer"/> for the current platform.
    /// Windows: named pipe (existing behavior, unchanged).
    /// Linux with a Proton session active: ProtonBridgePipe, which exposes the
    /// real Windows named pipe inside the game's Wine prefix via pipehelper.exe.
    /// Linux without Proton: .NET's local named-pipe emulation (Unix socket),
    /// used by tools/tests.
    /// </summary>
    public static class ControlPipeFactory
    {
        public static IPipeServer CreatePipe(string pipeName)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && ProtonRuntime.IsActive)
            {
                return new ProtonBridgePipe(pipeName, ProtonRuntime.CurrentGame);
            }

            return new WindowsNamedPipe(pipeName);
        }
    }
}
