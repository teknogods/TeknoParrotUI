using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TeknoParrotUi.Common.Pipes
{
    public class ControlPipe
    {
        public const string PipeName = "TeknoParrotPipe";

        public static bool _isRunning = false;
        public static NamedPipeServerStream _npServer;
        public static Thread _pipeThread;

        public void Start(bool runEmuOnly)
        {
            if (_isRunning)
                return;
            _isRunning = true;
            _pipeThread = new Thread(() => TransmitThread(runEmuOnly));
            _pipeThread.Start();
        }

        public virtual void Transmit(bool runEmuOnly)
        {

        }

        public void TransmitThread(bool runEmuOnly)
        {
            _npServer?.Close();
            _npServer = new NamedPipeServerStream(PipeName);

            _npServer.WaitForConnection();

            Transmit(runEmuOnly);

            _npServer.Close();
            _npServer?.Dispose();
        }

        public void Stop()
        {
            try
            {
                _isRunning = false;
                using (var npcs = new NamedPipeClientStream(PipeName))
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
