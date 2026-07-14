using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
        private int _port;                 // ephemeral - assigned when the listener starts
        private bool _listening;
        private TcpClient _client;
        private NetworkStream _stream;
        private Process _helperProcess;
        private long? _helperStartTicks;   // /proc identity of the launched helper
        private ProtonGameInfo _gameInfo;
        private int _closedFlag;           // Interlocked: quick-close/CloseAndWait run once
        private readonly string _sessionToken;

        private static int _bridgeSeq;

        public string PipeName { get; }
        public bool IsConnected => _client?.Connected ?? false;
        /// <summary>Unique id of this bridge instance (diagnostics).</summary>
        public string BridgeId { get; }
        /// <summary>The ephemeral TCP port actually bound (0 until <see cref="StartListening"/> ran).</summary>
        public int Port => _port;

        private bool Closed => Volatile.Read(ref _closedFlag) != 0;

        public ProtonBridgePipe(string pipeName, ProtonGameInfo gameInfo = null)
        {
            PipeName = pipeName;
            _gameInfo = gameInfo;
            _sessionToken = ProtonRuntime.CurrentSessionToken;
            BridgeId = $"{pipeName}#{Interlocked.Increment(ref _bridgeSeq)}";
            // Ephemeral port: the OS picks a free port per instance. The
            // selected port is passed to pipehelper.exe as an argument, so no
            // deterministic derivation is needed - and a previous session's
            // lingering TCP state can never block a new session's listener.
            _listener = new TcpListener(IPAddress.Loopback, 0);
        }

        /// <summary>
        /// Starts the TCP listener and resolves the actual ephemeral port.
        /// Idempotent. Public so tests can verify port assignment without a
        /// Wine prefix; production callers go through <see cref="WaitForConnection"/>.
        /// </summary>
        public void StartListening()
        {
            if (_listening)
                return;
            _listener.Start();
            _port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _listening = true;
        }

        public void WaitForConnection()
        {
            StartListening();
            ProtonLog.Write($"pipe '{PipeName}': listening on 127.0.0.1:{_port} (bridge {BridgeId})");

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

        // Set exactly once by whichever path performs the FINAL helper
        // shutdown accounting (registry unregister + shm claim release):
        // the mid-session quick Close() or the verified CloseAndWait().
        private int _helperFinalized;

        /// <summary>
        /// Transport-only stop: closes the network stream, TCP client and TCP
        /// listener so a thread blocked in Read/WaitForConnection unblocks.
        /// Deliberately does NOT touch the helper process, does NOT unregister
        /// it from <see cref="PipeHelperRegistry"/> and does NOT release the
        /// shm mirror claim - final helper shutdown is owned exclusively by
        /// <see cref="CloseAndWait"/>. Safe to call repeatedly.
        /// </summary>
        public void StopTransport()
        {
            Interlocked.Exchange(ref _closedFlag, 1);
            try { _stream?.Close(); } catch { /* ignored */ }
            try { _client?.Close(); } catch { /* ignored */ }
            try { _listener.Stop(); } catch { /* ignored */ }
        }

        public void Close()
        {
            if (Interlocked.Exchange(ref _closedFlag, 1) != 0)
                return;
            ProtonLog.Write($"pipe '{PipeName}': closing (game->TPUI {_bytesRead} B, TPUI->game {_bytesWritten} B)");

            try { _stream?.Close(); } catch { /* ignored */ }
            try { _client?.Close(); } catch { /* ignored */ }
            try { _listener.Stop(); } catch { /* ignored */ }

            // Quick close (mid-session pipe recycle path): terminate only the
            // exact helper Process object THIS bridge started, and finalize
            // (unregister + shm release) exactly once so a successor bridge
            // can claim the mirror. The verified session-shutdown path is
            // CloseAndWait; the shared _helperFinalized flag guarantees the
            // finalization never runs twice across both paths.
            if (Interlocked.Exchange(ref _helperFinalized, 1) == 0)
            {
                try
                {
                    if (_helperProcess != null && !_helperProcess.HasExited)
                        _helperProcess.Kill();
                    if (_helperProcess != null)
                        PipeHelperRegistry.Unregister(_helperProcess.Id);
                }
                catch { /* ignored */ }

                if (_ownsShmMirror)
                {
                    _ownsShmMirror = false;
                    Interlocked.Exchange(ref _shmMirrorClaimed, 0);
                }
            }
        }

        /// <summary>
        /// Full verified shutdown for session cleanup - the SINGLE owner of
        /// final helper shutdown: closes the network endpoints (transport
        /// stop), lets the helper exit naturally, then terminates ONLY helpers
        /// verifiably carrying this session's token (direct helper,
        /// descendants, or Wine-reparented helpers), polls /proc until they
        /// actually disappeared, and reports honestly. The registry entry and
        /// the shm mirror claim are released EXACTLY ONCE, and only after
        /// helper shutdown is confirmed. Safe and idempotent when called
        /// repeatedly. Never a process-name-wide kill.
        /// </summary>
        public PipeShutdownResult CloseAndWait(TimeSpan gracefulTimeout, TimeSpan forceTimeout)
        {
            // 1. Atomically mark the bridge closing.
            var firstCloser = Interlocked.Exchange(ref _closedFlag, 1) == 0;
            if (firstCloser)
                ProtonLog.Write($"pipe '{PipeName}': closing (game->TPUI {_bytesRead} B, TPUI->game {_bytesWritten} B)");

            // 2-4. Close the network stream, TCP client, TCP listener.
            try { _stream?.Close(); } catch { /* ignored */ }
            try { _client?.Close(); } catch { /* ignored */ }
            try { _listener.Stop(); } catch { /* ignored */ }

            // 5-6. Allow pipehelper to exit naturally; wait for the returned process.
            var helper = _helperProcess;
            var helperExited = helper == null;
            if (helper != null)
            {
                try { helperExited = helper.WaitForExit((int)gracefulTimeout.TotalMilliseconds); }
                catch { helperExited = true; /* disposed/never started */ }
            }

            // 7. Discover token-carrying helper descendants / reparented helpers.
            var remaining = FindLiveSessionHelpers();

            // 8. If still alive, terminate only helpers carrying this session token.
            if (remaining.Count > 0 && OperatingSystem.IsLinux())
            {
                var proc = new LinuxProcReader();
                var signaler = new LinuxProcessSignaler();
                foreach (var (pid, startTicks) in remaining)
                {
                    // Re-verify identity immediately before the signal (PID-reuse guard).
                    var stat = proc.ReadStat(pid);
                    if (stat == null || stat.StartTimeTicks != startTicks)
                        continue;
                    ProtonLog.Write($"pipe '{PipeName}': terminating session helper pid {pid}");
                    signaler.SignalGraceful(pid);
                }

                // 9. Wait again (bounded), then force-kill still-verified survivors.
                var deadline = Environment.TickCount64 + (long)forceTimeout.TotalMilliseconds;
                while (Environment.TickCount64 < deadline && FindLiveSessionHelpers().Count > 0)
                    Thread.Sleep(50);
                foreach (var (pid, startTicks) in FindLiveSessionHelpers())
                {
                    var stat = proc.ReadStat(pid);
                    if (stat == null || stat.StartTimeTicks != startTicks)
                        continue;
                    signaler.SignalForce(pid);
                }
                // After the final force termination, poll /proc briefly (up to
                // ~2s, 50ms steps) until the helper actually disappeared - the
                // kernel needs a moment to reap it; reporting STILL ALIVE
                // immediately would be dishonest the other way around.
                deadline = Environment.TickCount64 + 2000;
                while (Environment.TickCount64 < deadline && FindLiveSessionHelpers().Count > 0)
                    Thread.Sleep(50);
            }

            // 10. Report any remaining helper PIDs - never assume they exited.
            var stillAlive = FindLiveSessionHelpers();
            if (helper != null && !helperExited)
            {
                try { helperExited = helper.HasExited; } catch { helperExited = true; }
            }
            var allHelpersGone = stillAlive.Count == 0 && helperExited;

            // 11. Registry unregister + shm claim release: EXACTLY ONCE, and
            //     only after helper shutdown is confirmed. When shutdown could
            //     NOT be confirmed the flag stays unset, so a later retry can
            //     still finalize once the helper is really gone.
            if (allHelpersGone && Interlocked.Exchange(ref _helperFinalized, 1) == 0)
            {
                try
                {
                    if (helper != null)
                        PipeHelperRegistry.Unregister(helper.Id);
                    // Confirmed-dead token helpers registered for this session
                    // (e.g. re-registered by a recycle) are stale entries now.
                    if (!string.IsNullOrEmpty(_sessionToken))
                    {
                        foreach (var entry in PipeHelperRegistry.Snapshot())
                        {
                            if (entry.SessionToken == _sessionToken)
                                PipeHelperRegistry.Unregister(entry.Pid);
                        }
                    }
                }
                catch { /* ignored */ }

                if (_ownsShmMirror)
                {
                    _ownsShmMirror = false;
                    Interlocked.Exchange(ref _shmMirrorClaimed, 0);
                }
            }

            var result = new PipeShutdownResult
            {
                Completed = allHelpersGone,
                HelperExited = allHelpersGone,
                RemainingHelperPids = stillAlive.Select(p => p.Pid).ToList(),
                Detail = allHelpersGone ? "helper shutdown confirmed" : "session helper(s) still alive"
            };

            ProtonLog.Write(BuildPipeSessionLogBlock(result));
            return result;
        }

        /// <summary>[PipeSession] diagnostics block for this bridge instance.</summary>
        public string BuildPipeSessionLogBlock(PipeShutdownResult shutdown = null)
        {
            int? helperPid = null;
            try { helperPid = _helperProcess?.Id; } catch { /* disposed */ }
            var remaining = shutdown == null || shutdown.RemainingHelperPids.Count == 0
                ? "none"
                : string.Join(",", shutdown.RemainingHelperPids);
            return "[PipeSession]\n" +
                   $"SessionId: {(_sessionToken ?? "(none)")}\n" +
                   $"PipeName: {PipeName}\n" +
                   $"BridgeId: {BridgeId}\n" +
                   $"TcpPort: {_port}\n" +
                   $"HelperPid: {(helperPid?.ToString() ?? "(none)")}\n" +
                   $"HelperStartTime: {(_helperStartTicks?.ToString() ?? "(unknown)")}\n" +
                   $"HelperSessionTokenMatched: {(!string.IsNullOrEmpty(_sessionToken)).ToString().ToLowerInvariant()}\n" +
                   $"Connected: {IsConnected.ToString().ToLowerInvariant()}\n" +
                   $"ShutdownRequested: {Closed.ToString().ToLowerInvariant()}\n" +
                   $"HelperExited: {(shutdown?.HelperExited ?? false).ToString().ToLowerInvariant()}\n" +
                   $"RemainingHelperPids: {remaining}";
        }

        private IReadOnlyList<(int Pid, long StartTimeTicks)> FindLiveSessionHelpers() =>
            PipeHelperRegistry.FindSessionHelperProcesses(_sessionToken);

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

                if (Closed)
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

            if (_helperProcess != null && OperatingSystem.IsLinux())
                _helperStartTicks = new LinuxProcReader().ReadStat(_helperProcess.Id)?.StartTimeTicks;
        }

        /// <summary>
        /// Legacy deterministic port derivation (FNV-1a into 40000-49999).
        /// NO LONGER used to select the production listener port - each bridge
        /// binds an ephemeral port (see the constructor) and passes the actual
        /// port to pipehelper.exe. Retained only for backward-compatible tests
        /// of the hash function itself.
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
