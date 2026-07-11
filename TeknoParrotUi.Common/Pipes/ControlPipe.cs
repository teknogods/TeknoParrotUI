using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TeknoParrotUi.Common.Pipes.Abstractions;
using TeknoParrotUi.Common.Pipes.Implementation;

namespace TeknoParrotUi.Common.Pipes
{
    public class ControlPipe
    {
        public const string PipeName = "TeknoParrotPipe";

        public static bool _isRunning = false;
        public static IPipeServer _npServer;
        public static Thread _pipeThread;

        public void Start(bool runEmuOnly)
        {
            if (_isRunning)
                return;
            _isRunning = true;
            // Background: must never keep the process alive (a Proton bridge
            // can block in AcceptTcpClient when the game never connects).
            _pipeThread = new Thread(() => TransmitThread(runEmuOnly)) { IsBackground = true };
            _pipeThread.Start();
        }

        public virtual void Transmit(bool runEmuOnly)
        {

        }

        public void TransmitThread(bool runEmuOnly)
        {
            try
            {
                _npServer?.Close();
                _npServer = PipeFactory.ControlPipeFactory.CreatePipe(PipeName);

                _npServer.WaitForConnection();

                Transmit(runEmuOnly);

                _npServer.Close();
                _npServer?.Dispose();
            }
            catch (Exception)
            {
                // Stop() closing/disposing the pipe while this thread is
                // blocked in WaitForConnection()/Read()/Write() is expected -
                // that is the (only) way to unblock a bridge pipe's Accept().
                // Never let that bubble up and crash the process.
            }
        }

        public void Stop()
        {
            _isRunning = false;

            // Windows native pipes: connect a dummy client FIRST to unblock a
            // pending WaitForConnection() cleanly (identical to the pre-Proton
            // behavior), then close - matches how NamedPipeServerStream is
            // meant to be unblocked and avoids racing a cross-thread Close()
            // against WaitForConnection(). Linux/Proton bridge pipes wrap a TCP
            // listener that nothing listens on locally, so the dummy connect
            // throws immediately and used to skip the Close() entirely - close
            // those first instead.
            var isBridge = !(_npServer is WindowsNamedPipe);
            if (isBridge)
            {
                try
                {
                    _npServer?.Close();
                    _npServer?.Dispose();
                }
                catch (Exception)
                {
                    // ignored
                }
            }

            try
            {
                using (var npcs = new NamedPipeClientStream(PipeName))
                {
                    npcs.Connect(100);
                }
                Thread.Sleep(100);
            }
            catch (Exception)
            {
                // ignored
            }

            if (!isBridge)
            {
                try
                {
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
}
