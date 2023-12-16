using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TeknoParrotUi.Common.Pipes
{
    public class ControlSender
    {
        private Thread pipe;

        public bool Running;
        public int Control = 0x00, Control2 = 0x00;
        public byte SleepTime = 15;

        public virtual void Start()
        {
            if (Running)
                return;

            Running = true;
            pipe = new Thread(TransmitThread);
            pipe.Start();
        }

        public virtual void Stop()
        {
            Running = false;
        }

        public virtual void Transmit()
        {

        }

        public void TransmitThread()
        {
            while (Running)
            {
                Control = 0x00;
                Control2 = 0x00;
                Transmit();
                Thread.Sleep(SleepTime);
            }
        }
    }
}
