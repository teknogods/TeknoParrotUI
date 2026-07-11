using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using TeknoParrotUi.Common.Jvs;
using TeknoParrotUi.Common.Jvs.Implementation;
using TeknoParrotUi.Common.Pipes.Abstractions;
using TeknoParrotUi.Common.Proton;

namespace TeknoParrotUi.Common.Pipes.Implementation
{
    /// <summary>
    /// Linux+Proton implementation of <see cref="IPipeServer"/>.
    ///
    /// Wine named pipes live inside wineserver and are not visible on the host,
    /// so a small helper (pipehelper.exe, see Tools/ProtonPipeHelper) runs inside
    /// the game's Wine prefix. The helper creates the real Windows named pipe
    /// (\\.\pipe\&lt;name&gt;) for OpenParrot to connect to, and forwards all bytes
    /// over TCP loopback to this class:
    ///
    ///   TPUI (native Linux)  ⇄  TCP 127.0.0.1:port  ⇄  pipehelper.exe (Wine)  ⇄  \\.\pipe\name  ⇄  game
    ///
    /// The helper only opens the TCP connection after the game has connected to
    /// the named pipe, so <see cref="WaitForConnection"/> keeps its existing
    /// "blocks until the game is ready" semantics.
    /// </summary>
    public class ProtonBridgePipe : IPipeServer
    {
        private const int GameDetectTimeoutMs = 120_000;
        private const int GameDetectPollMs = 500;

        private readonly TcpListener _listener;
        private readonly int _port;
        private TcpClient _client;
        private NetworkStream _stream;
        private Process _helperProcess;
        private ProtonGameInfo _gameInfo;
        private bool _closed;

        public string PipeName { get; }
        public bool IsConnected => _client?.Connected ?? false;

        public ProtonBridgePipe(string pipeName, ProtonGameInfo gameInfo = null)
        {
            PipeName = pipeName;
            _gameInfo = gameInfo;
            _port = GetDeterministicPort(pipeName);
            _listener = new TcpListener(IPAddress.Loopback, _port);
        }

        public void WaitForConnection()
        {
            _listener.Start();
            ProtonLog.Write($"pipe '{PipeName}': listening on 127.0.0.1:{_port}");

            // Preferred: wine + prefix already resolved by the launcher - start
            // the helper NOW so the pipe exists before the game boots (games
            // like TGM3 probe JVS once at startup and never retry).
            if (_gameInfo == null && ProtonRuntime.WineBinary != null && ProtonRuntime.WinePrefix != null)
            {
                _gameInfo = new ProtonGameInfo
                {
                    Pid = -1,
                    ExecutableName = ProtonRuntime.ExpectedExecutable,
                    WinePrefix = ProtonRuntime.WinePrefix,
                    WineBinaryPath = ProtonRuntime.WineBinary
                };
                ProtonLog.Write($"pipe '{PipeName}': eager helper start (prefix {_gameInfo.WinePrefix})");
            }
            else if (_gameInfo == null)
            {
                // Fallback: wait for the game process to learn prefix + wine.
                ProtonLog.Write($"pipe '{PipeName}': waiting for Proton game process...");
                _gameInfo = WaitForProtonGame();
                ProtonLog.Write($"pipe '{PipeName}': game detected (pid {_gameInfo.Pid}, exe {_gameInfo.ExecutableName})");
            }

            LaunchHelper();
            ProtonLog.Write($"pipe '{PipeName}': helper launched (pid {_helperProcess?.Id}), waiting for game to open the pipe...");

            _client = _listener.AcceptTcpClient();
            _client.NoDelay = true;
            _stream = _client.GetStream();
            ProtonLog.Write($"pipe '{PipeName}': *** GAME CONNECTED *** (helper attached via TCP)");
        }

        private long _bytesRead, _bytesWritten;
        private long _lastLoggedRead, _lastLoggedWritten;

        public int Read(byte[] buffer, int offset, int count)
        {
            var n = _stream.Read(buffer, offset, count);
            _bytesRead += n;
            // log first bytes and then every 64KB
            if (_lastLoggedRead == 0 || _bytesRead - _lastLoggedRead >= 65536)
            {
                ProtonLog.Write($"pipe '{PipeName}': game->TPUI total {_bytesRead} bytes (last chunk {n})");
                _lastLoggedRead = _bytesRead == 0 ? 1 : _bytesRead;
            }
            return n;
        }

        public void Write(byte[] buffer, int offset, int count)
        {
            _stream.Write(buffer, offset, count);
            _bytesWritten += count;
            if (_lastLoggedWritten == 0 || _bytesWritten - _lastLoggedWritten >= 65536)
            {
                ProtonLog.Write($"pipe '{PipeName}': TPUI->game total {_bytesWritten} bytes (last chunk {count})");
                _lastLoggedWritten = _bytesWritten == 0 ? 1 : _bytesWritten;
            }
        }

        public void Flush() => _stream?.Flush();

        public void Close()
        {
            if (_closed)
                return;
            _closed = true;
            ProtonLog.Write($"pipe '{PipeName}': closing (game->TPUI {_bytesRead} B, TPUI->game {_bytesWritten} B)");

            try { _stream?.Close(); } catch { /* ignored */ }
            try { _client?.Close(); } catch { /* ignored */ }
            try { _listener.Stop(); } catch { /* ignored */ }
            try
            {
                if (_helperProcess != null && !_helperProcess.HasExited)
                    _helperProcess.Kill();
            }
            catch { /* ignored */ }

            // Release the shm mirror claim so a successor bridge can take it.
            if (_ownsShmMirror)
            {
                _ownsShmMirror = false;
                Interlocked.Exchange(ref _shmMirrorClaimed, 0);
            }
        }

        public void Dispose() => Close();

        private ProtonGameInfo WaitForProtonGame()
        {
            var expected = ProtonRuntime.ExpectedExecutable;
            var deadline = Environment.TickCount64 + GameDetectTimeoutMs;

            while (Environment.TickCount64 < deadline)
            {
                var game = ProtonRuntime.CurrentGame
                           ?? ProtonProcessDetector.FindRunningProtonGame(expected);
                if (game != null)
                    return game;

                if (_closed)
                    throw new OperationCanceledException("Pipe closed while waiting for Proton game.");

                Thread.Sleep(GameDetectPollMs);
            }

            throw new InvalidOperationException(
                "No Proton game process detected. Ensure the game is launched in Proton.");
        }

        // Only one bridge per session should run the shm mirror - multiple
        // mirrors of the same region would race each other.
        private static int _shmMirrorClaimed;
        private bool _ownsShmMirror;

        private void LaunchHelper()
        {
            // Pipe bridge args; if the JVS state lives in /dev/shm (Linux), also
            // ask the helper to mirror it into the game's CreateFileMapping
            // namespace (coins, FFB). Claimed by the first bridge only.
            if (JvsHelper.StateSharedMemory is ProtonSharedMemoryBridge shm &&
                Interlocked.CompareExchange(ref _shmMirrorClaimed, 1, 0) == 0)
            {
                _ownsShmMirror = true;
                _helperProcess = ProtonHelper.RunHelper(_gameInfo,
                    PipeName, "127.0.0.1", _port.ToString(),
                    shm.Name, shm.Size.ToString(), ProtonHelper.ToWinePath(shm.FilePath));
            }
            else
            {
                _helperProcess = ProtonHelper.RunHelper(_gameInfo,
                    PipeName, "127.0.0.1", _port.ToString());
            }
        }

        /// <summary>
        /// Stable port in the 40000-49999 range derived from the pipe name
        /// (FNV-1a), so the helper and host always agree without configuration.
        /// </summary>
        public static int GetDeterministicPort(string pipeName)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (var c in pipeName)
                {
                    hash ^= c;
                    hash *= 16777619;
                }
                return 40000 + (int)(hash % 10000);
            }
        }
    }
}
