using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.IO.Ports;
using System.Threading;
using TeknoParrotUi.Common.Jvs;
using TeknoParrotUi.Common.Pipes;
using TeknoParrotUi.Common.Pipes.Abstractions;
using TeknoParrotUi.Common.Pipes.Implementation;

namespace TeknoParrotUi.Common
{
    public class SerialPortHandler
    {
        private readonly ConcurrentQueue<byte> _recievedData = new ConcurrentQueue<byte>();
        private SerialPort _port;
        // Instance state: each GameSession creates its own handler; static
        // state raced between sessions (KillMe reset by session 2 kept
        // session 1's listener thread alive holding the bridge port).
        private readonly object _sync = new object();
        // Cancellation-aware backoff/stop signal - NEVER a bare Thread.Sleep
        // (a sleeping listener can't observe a stop request and could recreate
        // a pipe server AFTER shutdown, colliding with the next session).
        private readonly ManualResetEventSlim _stopSignal = new ManualResetEventSlim(false);
        private IPipeServer _npServer;
        private Thread _listenerThread;
        private Thread _queueThread;
        private string _pipe;
        private volatile bool _stopRequested;
        private bool KillMe => _stopRequested;
        private const int _targetElapsedMilliseconds = 10;
        private Stopwatch _stopwatchDeque = new Stopwatch();
        private SpinWait _spinWaitDeque = new SpinWait();
        //private readonly List<byte> _lastPackage = new List<byte>(); // This is for TESTING
        /// <summary>
        /// Process the queue, very dirty and hacky. Please improve.
        /// This is the stablest I got it, lot of research and development of different methods.
        /// </summary>
        public void ProcessQueue()
        {
            Thread.CurrentThread.Priority = ThreadPriority.Highest;
            Stopwatch stopwatch = new Stopwatch();
            SpinWait spinWait = new SpinWait();
            // we use a spinlock here instead of Thread.Sleep because otherwise
            // when startMinimzed is used windows will forcibly reduce our timer precision
            // and JVS will run too slow for games like LGJ to run properly
            while (true)
            {
                stopwatch.Restart();
                try
                {
                    if (QueueProcessor()) return;

                    while (stopwatch.ElapsedMilliseconds < _targetElapsedMilliseconds)
                    {
                        spinWait.SpinOnce();
                    }
                }
                catch (Exception)
                {
                    // ignored
                }
            }
        }

        private Stream _stream;
        private bool QueueProcessor()
        {
            if (KillMe)
                return true;
            var queue = new List<byte>();
            if (_recievedData.Count != 0)
            {
                var f = DeQueueByte(); //_recievedData.Dequeue();
                if (f == 0xE0)
                {
                    var count = 0;
                    byte size = 0;
                    while (true)
                    {
                        if (KillMe)
                            return true;
                        if (count == 0)
                        {
                            queue.Add(f);
                            count++;
                        }
                        else if (_recievedData.Count > 2 && count == 1)
                        {
                            queue.Add(DeQueueByte()); // _recievedData.Dequeue());
                            size = DeQueueByte(); //_recievedData.Dequeue();
                            queue.Add(size);
                            count++;
                        }
                        else if (count == 2 && _recievedData.Count >= size)
                        {
                            for (int i = 0; i < size; i++)
                            {
                                queue.Add(DeQueueByte()); //_recievedData.Dequeue());
                            }
                            //_lastPackage.Clear();
                            //_lastPackage.AddRange(queue);
                            var reply = JvsPackageEmulator.GetReply(queue.ToArray());
                            if (reply.Length != 0)
                            {
                                _stream.Write(reply, 0, reply.Length);
                                //Console.WriteLine(reply.Length);
                            }
                            break;
                        }
                    }
                }
            }
            return false;
        }

        public byte DeQueueByte()
        {
            while (true)
            {
                _stopwatchDeque.Restart();
                if (_recievedData.TryDequeue(out byte value))
                {
                    return value;
                }
                while (_stopwatchDeque.ElapsedMilliseconds < _targetElapsedMilliseconds)
                {
                    _spinWaitDeque.SpinOnce();
                }
            }
        }

        /// <summary>
        /// Starts the JVS pipe listener AND queue-processing threads, both
        /// owned by this instance so <see cref="StopAndWait"/> can join them
        /// during shutdown. One handler instance serves one GameSession.
        /// </summary>
        public void StartPipe(string pipeName)
        {
            lock (_sync)
            {
                if (_listenerThread != null)
                    return; // already started - one instance, one session
                _stopRequested = false;
                _stopSignal.Reset();
                _pipe = pipeName;
                _listenerThread = new Thread(() => ListenPipe(pipeName)) { IsBackground = true };
                _queueThread = new Thread(ProcessQueue) { IsBackground = true };
                _listenerThread.Start();
                _queueThread.Start();
            }
        }

        public void ListenPipe(string pipe)
        {
            Thread.CurrentThread.Priority = ThreadPriority.Highest;
            _pipe = pipe;
            // The worker keeps its OWN local server reference; the shared
            // field exists only so StopAndWait can close this exact instance.
            IPipeServer server;
            lock (_sync)
            {
                if (_stopRequested)
                    return;
                try { _npServer?.Close(); } catch { /* ignored */ }
                // On Windows this is a plain NamedPipeServerStream(pipe, InOut, 1,
                // Byte, Asynchronous) - identical to the pre-Proton implementation.
                // The Linux/Proton bridge branch inside the factory never runs on
                // Windows (RuntimeInformation.IsOSPlatform(Linux) is false there).
                server = Pipes.PipeFactory.ControlPipeFactory.CreatePipe(pipe, PipeOptions.Asynchronous);
                _npServer = server;
            }

            while (true)
            {
                try
                {
                    server.WaitForConnection();
                    var stream = new PipeServerStream(server);
                    lock (_sync)
                        _stream = stream;

                    while (true)
                    {
                        if (_stopRequested)
                        {
                            server.Close();
                            return;
                        }
                        var data = new byte[1024];
                        var r = server.Read(data, 0, data.Length);

                        if (r == 0)
                        {
                            server.Close();
                            // Check cancellation BEFORE every recreation - a
                            // stopped listener must never create another server.
                            server = RecreateServer(pipe);
                            if (server == null)
                                return;
                        }
                        else
                        {
                            for (var i = 0; i < r; i++)
                            {
                                if (data[i] == 0xD0 && i + 1 != r)
                                {
                                    if (data[i + 1] == 0xCF || data[i + 1] == 0xDF)
                                    {
                                        i += 1;

                                        if (data[i] == 0xCF)
                                        {
                                            _recievedData.Enqueue(0xD0);
                                        }
                                        else
                                        {
                                            _recievedData.Enqueue(0xE0);
                                        }

                                        continue;
                                    }
                                }

                                _recievedData.Enqueue(data[i]);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    TeknoParrotUi.Common.Proton.ProtonLog.Write($"JVS pipe loop error: {ex.GetType().Name}: {ex.Message}");
                    try { server?.Close(); } catch { /* ignored */ }
                    // Session over: do NOT respawn the pipe (each respawn used
                    // to leak a pipehelper process into the Wine prefix).
                    if (_stopRequested)
                        return;
                    // Backoff so a persistent failure (e.g. port conflict)
                    // cannot spam create/close cycles - cancellation-aware, so
                    // a stop during the backoff exits immediately instead of
                    // sleeping through it.
                    if (_stopSignal.Wait(500))
                        return;
                    server = RecreateServer(pipe);
                    if (server == null)
                        return;
                }
            }
        }

        /// <summary>
        /// Creates a replacement pipe server, registered as this instance's
        /// server, or null when a stop was requested (the caller must exit).
        /// </summary>
        private IPipeServer RecreateServer(string pipe)
        {
            lock (_sync)
            {
                if (_stopRequested)
                    return null;
                _npServer = Pipes.PipeFactory.ControlPipeFactory.CreatePipe(pipe, PipeOptions.Asynchronous);
                return _npServer;
            }
        }

        /// <summary>
        /// Listen the serial port.
        /// </summary>
        /// <param name="port">Port name.</param>
        public void ListenSerial(string port)
        {
            _stopRequested = false;
            _port = new SerialPort(port)
            {
                BaudRate = 115200,
                Parity = Parity.None,
                StopBits = StopBits.One,
                ReadTimeout = 0,
                WriteBufferSize = 516,
                ReadBufferSize = 516,
                Handshake = Handshake.None
            };

            _port.DataReceived += delegate (object sender, SerialDataReceivedEventArgs args)
            {
                var sp = (SerialPort)sender;
                var data = new byte[sp.BytesToRead];
                var r = sp.Read(data, 0, data.Length);
                for (var i = 0; i < r; i++)
                {
                    if (data[i] == 0xD0 && i + 1 != r)
                    {
                        if (data[i + 1] == 0xCF || data[i + 1] == 0xDF)
                        {
                            i += 1;

                            if (data[i] == 0xCF)
                            {
                                _recievedData.Enqueue(0xD0);
                            }
                            else
                            {
                                _recievedData.Enqueue(0xE0);
                            }

                            continue;
                        }
                    }

                    _recievedData.Enqueue(data[i]);
                }
            };

            _port.Open();
            while (_port.IsOpen)
            {
                if (KillMe)
                {
                    _port.Close();
                    break;
                }
                Thread.Sleep(1000);
            }
        }

        /// <summary>
        /// Legacy stop entry point - now a bounded synchronous stop (joins the
        /// owned threads and waits for the in-prefix helper via CloseAndWait).
        /// Prefer <see cref="StopAndWait"/> directly for an honest result.
        /// </summary>
        public void StopListening()
        {
            StopAndWait(TimeSpan.FromSeconds(2));
        }

        /// <summary>
        /// Full verified shutdown:
        ///  1. signal cancellation,
        ///  2. close the exact server instance (unblocks WaitForConnection/Read),
        ///  3. close/dispose the stream,
        ///  4-5. join the listener and queue threads (bounded),
        ///  6. clear queued bytes,
        ///  7-8. confirm thread exit and report honestly. When the server is a
        ///  Proton bridge, also waits for its in-prefix pipehelper via
        ///  <see cref="ProtonBridgePipe.CloseAndWait"/>.
        /// </summary>
        public PipeShutdownResult StopAndWait(TimeSpan timeout)
        {
            IPipeServer server;
            Thread listener, queue;
            lock (_sync)
            {
                server = _npServer;
                listener = _listenerThread;
                queue = _queueThread;
            }

            if (listener == null && queue == null && server == null && _pipe == null)
                return PipeShutdownResult.NothingToStop;

            // 1-3. signal + close the exact server + stream.
            SignalStopAndCloseServer();

            // 4-5. join both owned threads with a bounded timeout.
            var listenerExited = listener == null || !listener.IsAlive || listener.Join(timeout);
            var queueExited = queue == null || !queue.IsAlive || queue.Join(timeout);

            // 6. clear queued bytes so nothing leaks into a later session.
            while (_recievedData.TryDequeue(out _)) { }

            // Proton bridge: wait for the in-prefix pipehelper to actually exit.
            var helperResult = server is ProtonBridgePipe bridge
                ? bridge.CloseAndWait(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2))
                : null;

            var helperExited = helperResult?.HelperExited ?? true;
            return new PipeShutdownResult
            {
                Completed = listenerExited && queueExited && helperExited,
                ListenerThreadExited = listenerExited,
                QueueThreadExited = queueExited,
                HelperExited = helperExited,
                RemainingHelperPids = helperResult?.RemainingHelperPids ?? Array.Empty<int>(),
                Detail = $"listener={(listenerExited ? "exited" : "STILL RUNNING")}, " +
                         $"queue={(queueExited ? "exited" : "STILL RUNNING")}, " +
                         $"helper={(helperExited ? "exited" : "STILL RUNNING")}"
            };
        }

        private void SignalStopAndCloseServer()
        {
            IPipeServer server;
            Stream stream;
            lock (_sync)
            {
                _stopRequested = true;
                _stopSignal.Set();
                server = _npServer;
                stream = _stream;
            }

            if (server == null && _pipe == null)
                return;

            // Windows native pipes: connecting a dummy client is the reliable,
            // exception-free way to unblock a pending WaitForConnection()/Read()
            // (matches the pre-Proton behavior exactly - do this FIRST, then
            // close). Linux/Proton bridge pipes: TRANSPORT-ONLY stop - closes
            // the stream/client/listener to unblock I/O but leaves the helper
            // process, its registry entry and the shm claim untouched;
            // CloseAndWait (called by StopAndWait) is the single owner of
            // final helper shutdown.
            if (server is ProtonBridgePipe bridgeServer)
            {
                bridgeServer.StopTransport();
            }
            else if (!(server is WindowsNamedPipe))
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
            }
            else
            {
                try
                {
                    using (NamedPipeClientStream npcs = new NamedPipeClientStream(_pipe))
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

            try { stream?.Close(); } catch { /* ignored */ }
            try { stream?.Dispose(); } catch { /* ignored */ }
        }
    }
}