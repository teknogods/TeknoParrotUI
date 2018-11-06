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

        public virtual void Start()
        {
            if (Running) return;
            Running = true;
            pipe = new Thread(Transmit);
            pipe.Start();
        }

        public virtual void Stop()
        {
            Running = false;
        }

        public virtual void Transmit()
        {

        }
    }
}
