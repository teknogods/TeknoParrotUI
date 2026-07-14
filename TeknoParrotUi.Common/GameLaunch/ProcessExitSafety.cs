using System;
using System.Diagnostics;

namespace TeknoParrotUi.Common.GameLaunch
{
    /// <summary>
    /// The ONLY sanctioned way to read <see cref="Process.ExitCode"/> in the
    /// launch pipeline. Process.ExitCode throws when the process has not
    /// exited, and both HasExited and ExitCode can throw when the Process
    /// object was disposed or the underlying process raced away - none of
    /// which may crash a game session or be silently assumed successful.
    /// </summary>
    public static class ProcessExitSafety
    {
        /// <summary>
        /// True (with the exit code) only when process exit is CONFIRMED
        /// (HasExited observed true) and the code was readable. False for a
        /// still-running process, a disposal race, or any access exception -
        /// callers must then treat the exit code as unavailable, never guess.
        /// </summary>
        public static bool TryGetExitCode(Process process, out int exitCode)
        {
            exitCode = 0;
            if (process == null)
                return false;
            try
            {
                if (!process.HasExited)
                    return false;
                exitCode = process.ExitCode;
                return true;
            }
            catch
            {
                // Disposed, access denied, or exit raced with the read.
                return false;
            }
        }
    }
}
