using System.Diagnostics;

namespace TeknoParrotUi.Common.GameLaunch
{
    /// <summary>
    /// Thin abstraction over actually starting a game process - exists purely
    /// so <see cref="GameProcessLauncher"/>'s fallback decision logic can be
    /// unit-tested with a fake/simulated starter instead of ever calling the
    /// real <see cref="Process.Start()"/> in a test.
    /// </summary>
    public interface IProcessStarter
    {
        /// <summary>
        /// Starts <paramref name="startInfo"/> and returns the running
        /// <see cref="Process"/>, or null if none could be created. May also
        /// throw (permission denied, missing executable, backend/driver
        /// initialization failure, etc.) - callers must handle both a thrown
        /// exception AND a null return as "no process was created".
        /// </summary>
        Process Start(ProcessStartInfo startInfo);
    }

    /// <summary>Real implementation - the only one used outside tests.</summary>
    public sealed class RealProcessStarter : IProcessStarter
    {
        public Process Start(ProcessStartInfo startInfo)
        {
            var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            return process.Start() ? process : null;
        }
    }
}
