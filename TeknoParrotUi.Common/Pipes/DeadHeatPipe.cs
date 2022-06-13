using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TeknoParrotUi.Common.Jvs;

namespace TeknoParrotUi.Common.Pipes
{
    public class DeadHeatPipe : ControlSender
    {
        public override void Transmit()
        {
            // Test
            if (InputCode.PlayerDigitalButtons[0].Test.HasValue && InputCode.PlayerDigitalButtons[0].Test.Value)
                Control |= 0x0100;
            // Service
            if (InputCode.PlayerDigitalButtons[0].Service.HasValue && InputCode.PlayerDigitalButtons[0].Service.Value)
                Control |= 0x0200;
            // Coin
            if (InputCode.PlayerDigitalButtons[0].Coin.HasValue && InputCode.PlayerDigitalButtons[0].Coin.Value)
                Control |= 0x0400;
            // Sequential Up or 3D/2D
            if (InputCode.PlayerDigitalButtons[1].Up.HasValue && InputCode.PlayerDigitalButtons[1].Up.Value)
                Control |= 0x1000;
            // Sequential Down
            if (InputCode.PlayerDigitalButtons[1].Down.HasValue && InputCode.PlayerDigitalButtons[1].Down.Value)
                Control |= 0x2000;
            // Menu Up
            if (InputCode.PlayerDigitalButtons[0].Up.HasValue && InputCode.PlayerDigitalButtons[0].Up.Value)
                Control |= 0x4000;
            // Menu Down
            if (InputCode.PlayerDigitalButtons[0].Down.HasValue && InputCode.PlayerDigitalButtons[0].Down.Value)
                Control |= 0x01;
            // Enter
            if (InputCode.PlayerDigitalButtons[0].Button1.HasValue && InputCode.PlayerDigitalButtons[0].Button1.Value)
                Control |= 0x02;
            // View
            if (InputCode.PlayerDigitalButtons[0].Button2.HasValue && InputCode.PlayerDigitalButtons[0].Button2.Value)
                Control |= 0x04;
            // Nitrous
            if (InputCode.PlayerDigitalButtons[0].Button3.HasValue && InputCode.PlayerDigitalButtons[0].Button3.Value)
                Control |= 0x08;
            // Shift 1
            if (InputCode.PlayerDigitalButtons[1].Button1.HasValue && InputCode.PlayerDigitalButtons[1].Button1.Value)
                Control |= 0x8000;
            // Shift 2
            if (InputCode.PlayerDigitalButtons[1].Button2.HasValue && InputCode.PlayerDigitalButtons[1].Button2.Value)
                Control |= 0x10000;
            // Shift 3
            if (InputCode.PlayerDigitalButtons[1].Button3.HasValue && InputCode.PlayerDigitalButtons[1].Button3.Value)
                Control |= 0x20000;
            // Shift 4
            if (InputCode.PlayerDigitalButtons[1].Button4.HasValue && InputCode.PlayerDigitalButtons[1].Button4.Value)
                Control |= 0x40000;

            JvsHelper.StateView.Write(8, Control);
            JvsHelper.StateView.Write(12, InputCode.AnalogBytes[0]);
            JvsHelper.StateView.Write(16, InputCode.AnalogBytes[2]);
            JvsHelper.StateView.Write(20, InputCode.AnalogBytes[4]);
            JvsHelper.StateView.Write(28, InputCode.AnalogBytes[6]);
        }
    }
}
