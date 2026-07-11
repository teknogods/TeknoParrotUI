using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TeknoParrotUi.Common.Pipes.Abstractions;

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
            _npServer?.Close();
            _npServer = PipeFactory.ControlPipeFactory.CreatePipe(PipeName);

            _npServer.WaitForConnection();

            Transmit(runEmuOnly);

            _npServer.Close();
            _npServer?.Dispose();
        }

        public void Stop()
        {
            _isRunning = false;

            // Close the server FIRST - reliably unblocks bridge pipes blocked
            // in Accept. The legacy client-connect below throws on Linux when
            // nothing listens locally and used to skip the Close entirely.
            try
            {
                _npServer?.Close();
                _npServer?.Dispose();
            }
            catch (Exception)
            {
                // ignored
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
        }
    }
}
