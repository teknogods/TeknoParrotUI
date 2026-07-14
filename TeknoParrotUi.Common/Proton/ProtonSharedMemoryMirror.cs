using System;
using System.Diagnostics;
using System.Threading;
using TeknoParrotUi.Common.Jvs;
using TeknoParrotUi.Common.Jvs.Implementation;

namespace TeknoParrotUi.Common.Proton
{
    /// <summary>
    /// Mirrors the JVS state shared memory into a Proton game's Windows
    /// namespace for games that do NOT use a named pipe (Type-X2, Ex-Board -
    /// their serial traffic goes through ProtonComPortBridge instead).
    /// Launches pipehelper.exe in shm-only mode inside the game's prefix.
    /// </summary>
    public class ProtonSharedMemoryMirror : IDisposable
    {
        private const int GameDetectTimeoutMs = 120_000;
        private const int GameDetectPollMs = 500;

        private Process _helperProcess;
        private volatile bool _stopped;

        /// <summary>
        /// Waits for the Proton game process, then starts the mirror helper.
        /// Call from a background thread - blocks until the game is detected.
        /// </summary>
        public void Start(ProtonGameInfo gameInfo = null)
        {
            if (JvsHelper.StateSharedMemory is not ProtonSharedMemoryBridge shm)
                return; // Windows (or no /dev/shm region) - nothing to mirror.

            gameInfo ??= WaitForProtonGame();

            _helperProcess = ProtonHelper.RunHelper(gameInfo,
                "shm", shm.Name, shm.Size.ToString(), ProtonHelper.ToWinePath(shm.FilePath));
        }

        public void Stop()
        {
            _stopped = true;
            try
            {
                // Kills only the exact helper Process object THIS mirror
                // started - never a name-wide sweep.
                if (_helperProcess != null && !_helperProcess.HasExited)
                    _helperProcess.Kill();
                if (_helperProcess != null)
                    PipeHelperRegistry.Unregister(_helperProcess.Id);
            }
            catch { /* ignored */ }
        }

        public void Dispose() => Stop();

        private ProtonGameInfo WaitForProtonGame()
        {
            var deadline = Environment.TickCount64 + GameDetectTimeoutMs;
            while (Environment.TickCount64 < deadline)
            {
                var game = ProtonRuntime.CurrentGame
                           ?? ProtonProcessDetector.FindRunningProtonGame(ProtonRuntime.ExpectedExecutable);
                if (game != null)
                    return game;

                if (_stopped)
                    throw new OperationCanceledException("Mirror stopped while waiting for Proton game.");

                Thread.Sleep(GameDetectPollMs);
            }

            throw new InvalidOperationException(
                "No Proton game process detected. Ensure the game is launched in Proton.");
        }
    }
}
