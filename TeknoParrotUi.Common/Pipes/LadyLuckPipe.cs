using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TeknoParrotUi.Common.Jvs;

namespace TeknoParrotUi.Common.Pipes
{
    public class LadyLuckPipe : ControlSender
    {
        public override void Transmit()
        {
            // Test
            if (InputCode.PlayerDigitalButtons[0].Test.HasValue && InputCode.PlayerDigitalButtons[0].Test.Value)
                Control |= 0x80;
            // Reset
            if (InputCode.PlayerDigitalButtons[0].Service.HasValue && InputCode.PlayerDigitalButtons[0].Service.Value)
                Control |= 0x40;
            // Up
            if (InputCode.PlayerDigitalButtons[0].Up.HasValue && InputCode.PlayerDigitalButtons[0].Up.Value)
                Control |= 0x20;
            // Down
            if (InputCode.PlayerDigitalButtons[0].Down.HasValue && InputCode.PlayerDigitalButtons[0].Down.Value)
                Control |= 0x10;
            // Unknown 1
            if (InputCode.PlayerDigitalButtons[0].Button1.HasValue && InputCode.PlayerDigitalButtons[0].Button1.Value)
                Control |= 0x08;
            // Unknown 2
            if (InputCode.PlayerDigitalButtons[0].Button2.HasValue && InputCode.PlayerDigitalButtons[0].Button2.Value)
                Control |= 0x04;
            // BellyBoxDoorSw
            if (InputCode.PlayerDigitalButtons[0].Button3.HasValue && InputCode.PlayerDigitalButtons[0].Button3.Value)
                Control |= 0x02;
            // MainDoorSw
            if (InputCode.PlayerDigitalButtons[0].Button4.HasValue && InputCode.PlayerDigitalButtons[0].Button4.Value)
                Control |= 0x01;

            JvsHelper.StateView.Write(8, Control);
        }
    }
}
