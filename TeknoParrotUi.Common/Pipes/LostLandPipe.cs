using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TeknoParrotUi.Common.Jvs;

namespace TeknoParrotUi.Common.Pipes
{
    public class LostLandPipe : ControlSender
    {
        public override void Transmit()
        {
            uint gunAxis = 0;

            if (InputCode.PlayerDigitalButtons[0].Button1.HasValue && InputCode.PlayerDigitalButtons[0].Button1.Value)
                Control |= 0x01;
            if (InputCode.PlayerDigitalButtons[1].Button1.HasValue && InputCode.PlayerDigitalButtons[1].Button1.Value)
                Control |= 0x02;

            // Analogs
            // P1 Y
            gunAxis = InputCode.AnalogBytes[0];
            // P1 X
            gunAxis += (uint)InputCode.AnalogBytes[2] * 0x100;
            // P2 Y
            gunAxis += (uint)InputCode.AnalogBytes[4] * 0x10000;
            // P2 X
            gunAxis += (uint)InputCode.AnalogBytes[6] * 0x1000000;

            JvsHelper.StateView.Write(8, Control);
            JvsHelper.StateView.Write(12, gunAxis);
        }
    }
}
