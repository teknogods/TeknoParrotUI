using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.IO.Ports;
using System.Threading;
using TeknoParrotUi.Common.Jvs;

namespace TeknoParrotUi.Common
{
    public class SerialPortHandler
    {
        private readonly ConcurrentQueue<byte> _recievedData = new ConcurrentQueue<byte>();
        private SerialPort _port;
        private static NamedPipeServerStream _npServer;
        private static string _pipe;
        private static bool KillMe { get; set; }
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

        public void ListenPipe(string pipe)
        {
            Thread.CurrentThread.Priority = ThreadPriority.Highest;
            KillMe = false;
            _pipe = pipe;
            _npServer?.Close();
            _npServer = new NamedPipeServerStream(pipe, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

            while (true)
            {
                try
                {
                    _npServer.WaitForConnection();
                    _stream = _npServer;

                    while (true)
                    {
                        if (KillMe)
                        {
                            _npServer.Close();
                            return;
                        }
                        var data = new byte[1024];
                        var r = _npServer.Read(data, 0, data.Length);

                        if (r == 0)
                        {
                            _npServer.Close();
                            _npServer = new NamedPipeServerStream(pipe, PipeDirection.InOut, 1,
                                PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
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
                catch (Exception)
                {
                    _npServer.Close();
                    _npServer = new NamedPipeServerStream(pipe, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                }
            }
        }

        /// <summary>
        /// Listen the serial port.
        /// </summary>
        /// <param name="port">Port name.</param>
        public void ListenSerial(string port)
        {
            KillMe = false;
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

        public void StopListening()
        {
            try
            {
                if (_pipe == null)
                    return;
                KillMe = true;
                using (NamedPipeClientStream npcs = new NamedPipeClientStream(_pipe))
                {
                    npcs.Connect(100);
                }
                Thread.Sleep(100);
                _npServer?.Close();
                _npServer?.Dispose();
            }
            catch (Exception)
            {
                // ignored
            }
        }
    }
}