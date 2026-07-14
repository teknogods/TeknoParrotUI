using System;
using System.IO.Pipes;
using System.Threading;
using TeknoParrotUi.Common.Pipes.Abstractions;
using TeknoParrotUi.Common.Pipes.Implementation;

namespace TeknoParrotUi.Common.Pipes
{
    /// <summary>
    /// Base control pipe. INSTANCE-scoped: one ControlPipe instance belongs to
    /// exactly one GameSession. There is deliberately NO static mutable state
    /// here anymore - the old static _isRunning/_npServer/_pipeThread let a
    /// previous session's late-finishing worker thread close the server a NEW
    /// session had just created (dead controls on immediate relaunch).
    /// </summary>
    public class ControlPipe
    {
        public const string PipeName = "TeknoParrotPipe";

        private readonly object _sync = new object();
        private volatile bool _stopRequested;
        private IPipeServer _server;
        private Thread _thread;
        private bool _started;

        /// <summary>False once a stop was requested - subclass transmit loops poll this.</summary>
        protected bool IsRunning => !_stopRequested;

        /// <summary>The instance's current server (may change when a subclass reconnects).</summary>
        protected IPipeServer Server
        {
            get { lock (_sync) return _server; }
        }

        /// <summary>
        /// Creates a fresh pipe server for a subclass reconnect loop,
        /// registered as THIS instance's server so StopAndWait can close it.
        /// Returns null when a stop was already requested - the caller must
        /// exit its loop instead of recreating (a stopped worker must never
        /// create a server a NEW session could collide with).
        /// </summary>
        protected IPipeServer RecreatePipe()
        {
            lock (_sync)
            {
                if (_stopRequested)
                    return null;
                _server = PipeFactory.ControlPipeFactory.CreatePipe(PipeName);
                return _server;
            }
        }

        /// <summary>
        /// Starts the worker thread. Returns false (and does nothing) when
        /// this instance was already started - Start cannot run twice on the
        /// same instance; a new session must create a new ControlPipe.
        /// </summary>
        public bool Start(bool runEmuOnly)
        {
            lock (_sync)
            {
                if (_started)
                    return false;
                _started = true;
                // Background: must never keep the process alive (a Proton bridge
                // can block in AcceptTcpClient when the game never connects).
                _thread = new Thread(() => TransmitThread(runEmuOnly)) { IsBackground = true };
                _thread.Start();
                return true;
            }
        }

        public virtual void Transmit(bool runEmuOnly)
        {
        }

        private void TransmitThread(bool runEmuOnly)
        {
            // The worker owns its LOCAL server reference; the shared field
            // only exists so StopAndWait can close the exact same instance.
            IPipeServer server = null;
            try
            {
                lock (_sync)
                {
                    if (_stopRequested)
                        return;
                    server = PipeFactory.ControlPipeFactory.CreatePipe(PipeName);
                    _server = server;
                }

                server.WaitForConnection();

                Transmit(runEmuOnly);
            }
            catch (Exception)
            {
                // StopAndWait closing the pipe while this thread is blocked in
                // WaitForConnection()/Read()/Write() is expected - that is the
                // (only) way to unblock a bridge pipe's Accept(). Never let
                // that bubble up and crash the process.
            }
            finally
            {
                // Close and dispose the local server exactly once. A subclass
                // reconnect may have swapped in a newer server - that one also
                // belongs to THIS instance, so close it too. A DIFFERENT
                // ControlPipe instance's server is never touched here.
                IPipeServer current;
                lock (_sync)
                {
                    current = _server;
                    _server = null;
                }
                try { server?.Close(); } catch { /* ignored */ }
                try { server?.Dispose(); } catch { /* ignored */ }
                if (!ReferenceEquals(current, server))
                {
                    try { current?.Close(); } catch { /* ignored */ }
                    try { current?.Dispose(); } catch { /* ignored */ }
                }
            }
        }

        /// <summary>
        /// Idempotent stop signal: marks the instance stopping and unblocks a
        /// pending WaitForConnection/Read on THIS instance's server. Does not
        /// wait - see <see cref="StopAndWait"/>.
        /// </summary>
        public void Stop()
        {
            IPipeServer server;
            lock (_sync)
            {
                _stopRequested = true;
                server = _server;
            }
            UnblockAndClose(server);
        }

        /// <summary>
        /// Stops the instance and joins its worker thread with a bounded
        /// timeout. Only closes the exact server THIS instance created; a new
        /// session's ControlPipe is a different instance and is never touched.
        /// </summary>
        public PipeShutdownResult StopAndWait(TimeSpan timeout)
        {
            Stop();

            Thread thread;
            lock (_sync)
                thread = _thread;

            var exited = thread == null || !thread.IsAlive || thread.Join(timeout);
            return new PipeShutdownResult
            {
                Completed = exited,
                ListenerThreadExited = exited,
                Detail = exited ? "control pipe worker exited" : "control pipe worker did not exit within timeout"
            };
        }

        private static void UnblockAndClose(IPipeServer server)
        {
            // Windows native pipes: connect a dummy client FIRST to unblock a
            // pending WaitForConnection() cleanly (identical to the pre-Proton
            // behavior), then close. Linux/Proton bridge pipes wrap a TCP
            // listener that nothing listens on locally, so the dummy connect
            // throws immediately and used to skip the Close() entirely - close
            // those first instead.
            var isBridge = !(server is WindowsNamedPipe);
            if (isBridge)
            {
                try
                {
                    server?.Close();
                    server?.Dispose();
                }
                catch (Exception)
                {
                    // ignored
                }
                return;
            }

            try
            {
                using (var npcs = new NamedPipeClientStream(PipeName))
                {
                    npcs.Connect(100);
                }
            }
            catch (Exception)
            {
                // ignored
            }

            try
            {
                server?.Close();
                server?.Dispose();
            }
            catch (Exception)
            {
                // ignored
            }
        }
    }
}

