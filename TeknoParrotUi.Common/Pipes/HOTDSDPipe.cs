using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TeknoParrotUi.Common.Jvs;

namespace TeknoParrotUi.Common.Pipes
{
    public class HOTDSDPipe : ControlSender
    {
        public override void Transmit()
        {
            //P1 Trigger
            if (InputCode.PlayerDigitalButtons[0].Button1.HasValue && InputCode.PlayerDigitalButtons[0].Button1.Value)
                Control |= 0x01;
            //P2 Trigger
            if (InputCode.PlayerDigitalButtons[1].Button1.HasValue && InputCode.PlayerDigitalButtons[1].Button1.Value)
                Control |= 0x02;
            //P1 Screen In
            if (InputCode.PlayerDigitalButtons[0].Button4.HasValue && InputCode.PlayerDigitalButtons[0].Button4.Value)
                Control |= 0x04;
            //P2 Screen In
            if (InputCode.PlayerDigitalButtons[1].Button4.HasValue && InputCode.PlayerDigitalButtons[1].Button4.Value)
                Control |= 0x08;

            JvsHelper.StateView.Write(8, Control);
            JvsHelper.StateView.Write(12, InputCode.AnalogBytes[0]);
            JvsHelper.StateView.Write(16, InputCode.AnalogBytes[2]);
            JvsHelper.StateView.Write(20, InputCode.AnalogBytes[4]);
            JvsHelper.StateView.Write(24, InputCode.AnalogBytes[6]);
        }
    }
}
