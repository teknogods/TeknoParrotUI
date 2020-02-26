using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TeknoParrotUi.Common.Jvs;

namespace TeknoParrotUi.Common.Pipes
{
    public class SpiceTools : ControlSender
    {
        public override void Transmit()
        {
            // Start
            if (InputCode.PlayerDigitalButtons[0].Start.HasValue && InputCode.PlayerDigitalButtons[0].Start.Value)
                Control |= 0x0001;

            JvsHelper.StateView.Write(8, Control);
            //JvsHelper.StateView.Write(12, InputCode.AnalogBytes[0]);

            //JvsHelper.StateView.Write(16, InputCode.AnalogBytes[2]);
            //JvsHelper.StateView.Write(20, InputCode.AnalogBytes[4]);
        }
    }
}
