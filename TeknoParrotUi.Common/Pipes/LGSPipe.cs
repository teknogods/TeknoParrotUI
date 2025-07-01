using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TeknoParrotUi.Common.Jvs;

namespace TeknoParrotUi.Common.Pipes
{
    public class LGSPipe : ControlSender
    {
        public override void Transmit()
        {
            JvsHelper.StateView.Write(12, InputCode.AnalogBytes[0]);
        }
    }
}