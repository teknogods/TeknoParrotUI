using System;
using System.Threading;

namespace TeknoParrotUi.Common.Pipes
{
    public class ControlSender
    {
        private readonly object _sync = new object();
        private readonly ManualResetEventSlim _stopSignal = new ManualResetEventSlim(false);
        private Thread _thread;

        public volatile bool Running;
        public int Control = 0x00, Control2 = 0x00;
        public byte SleepTime = 15;

        public virtual void Start()
        {
            lock (_sync)
            {
                if (_thread is { IsAlive: true })
                    return;

                _stopSignal.Reset();
                Running = true;
                _thread = new Thread(TransmitThread)
                {
                    IsBackground = true,
                    Name = GetType().Name + ".ControlSender"
                };
                _thread.Start();
            }
        }

        public virtual void Stop()
        {
            Running = false;
            _stopSignal.Set();
        }

        /// <summary>
        /// Stops the sender and waits for the owned worker to exit. The result
        /// is honest so session cleanup can report a stuck title-specific
        /// sender instead of returning to a launch-ready UI while it is alive.
        /// </summary>
        public bool StopAndWait(TimeSpan timeout)
        {
            Stop();
            Thread thread;
            lock (_sync)
                thread = _thread;

            var exited = thread == null || !thread.IsAlive || thread.Join(timeout);
            if (exited)
            {
                lock (_sync)
                {
                    if (ReferenceEquals(_thread, thread))
                        _thread = null;
                }
            }
            return exited;
        }

        public virtual void Transmit()
        {

        }

        public void TransmitThread()
        {
            try
            {
                while (Running)
                {
                    Control = 0x00;
                    Control2 = 0x00;
                    Transmit();
                    if (_stopSignal.Wait(SleepTime))
                        break;
                }
            }
            finally
            {
                Running = false;
            }
        }
    }
}
