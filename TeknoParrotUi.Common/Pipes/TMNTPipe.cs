using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TeknoParrotUi.Common.Jvs;

namespace TeknoParrotUi.Common.Pipes
{
    public class TMNTPipe : ControlSender
    {
        public override void Transmit()
        {
            // Test
            if (InputCode.PlayerDigitalButtons[0].Test.HasValue && InputCode.PlayerDigitalButtons[0].Test.Value)
                Control |= 0x1;
            // Service
            if (InputCode.PlayerDigitalButtons[0].Service.HasValue && InputCode.PlayerDigitalButtons[0].Service.Value)
                Control |= 0x2;
            // Coin 1
            if (InputCode.PlayerDigitalButtons[0].Coin.HasValue && InputCode.PlayerDigitalButtons[0].Coin.Value)
                Control |= 0x4;
            // Coin 2
            if (InputCode.PlayerDigitalButtons[1].Coin.HasValue && InputCode.PlayerDigitalButtons[1].Coin.Value)
                Control |= 0x8;
            // Menu Up
            if (InputCode.PlayerDigitalButtons[0].Button5.HasValue && InputCode.PlayerDigitalButtons[0].Button5.Value)
                Control |= 0x10;
            // Menu Down
            if (InputCode.PlayerDigitalButtons[0].Button6.HasValue && InputCode.PlayerDigitalButtons[0].Button6.Value)
                Control |= 0x20;
            // Player 1 Start
            if (InputCode.PlayerDigitalButtons[0].Start.HasValue && InputCode.PlayerDigitalButtons[0].Start.Value)
                Control |= 0x40;
            // Player 1 Trigger
            if (InputCode.PlayerDigitalButtons[0].Button1.HasValue && InputCode.PlayerDigitalButtons[0].Button1.Value)
                Control |= 0x80;
            // Player 1 Option
            if (InputCode.PlayerDigitalButtons[0].Button2.HasValue && InputCode.PlayerDigitalButtons[0].Button2.Value)
                Control |= 0x100;
            // Player 1 Shoulder
            if (InputCode.PlayerDigitalButtons[0].Button3.HasValue && InputCode.PlayerDigitalButtons[0].Button3.Value)
                Control |= 0x200;
            // Player 1 Sense
            if (InputCode.PlayerDigitalButtons[0].Button4.HasValue && InputCode.PlayerDigitalButtons[0].Button4.Value)
                Control |= 0x400;
            // Player 2 Start
            if (InputCode.PlayerDigitalButtons[1].Start.HasValue && InputCode.PlayerDigitalButtons[1].Start.Value)
                Control |= 0x800;
            // Player 2 Trigger
            if (InputCode.PlayerDigitalButtons[1].Button1.HasValue && InputCode.PlayerDigitalButtons[1].Button1.Value)
                Control |= 0x1000;
            // Player 2 Option
            if (InputCode.PlayerDigitalButtons[1].Button2.HasValue && InputCode.PlayerDigitalButtons[1].Button2.Value)
                Control |= 0x2000;
            // Player 2 Shoulder
            if (InputCode.PlayerDigitalButtons[1].Button3.HasValue && InputCode.PlayerDigitalButtons[1].Button3.Value)
                Control |= 0x4000;
            // Player 2 Sense
            if (InputCode.PlayerDigitalButtons[1].Button4.HasValue && InputCode.PlayerDigitalButtons[1].Button4.Value)
                Control |= 0x8000;
            // Player 3 Start
            if (InputCode.PlayerDigitalButtons[2].Start.HasValue && InputCode.PlayerDigitalButtons[2].Start.Value)
                Control |= 0x10000;
            // Player 3 Trigger
            if (InputCode.PlayerDigitalButtons[2].Button1.HasValue && InputCode.PlayerDigitalButtons[2].Button1.Value)
                Control |= 0x20000;
            // Player 3 Option
            if (InputCode.PlayerDigitalButtons[2].Button2.HasValue && InputCode.PlayerDigitalButtons[2].Button2.Value)
                Control |= 0x40000;
            // Player 3 Shoulder
            if (InputCode.PlayerDigitalButtons[2].Button3.HasValue && InputCode.PlayerDigitalButtons[2].Button3.Value)
                Control |= 0x80000;
            // Player 3 Sense
            if (InputCode.PlayerDigitalButtons[2].Button4.HasValue && InputCode.PlayerDigitalButtons[2].Button4.Value)
                Control |= 0x100000;
            // Player 4 Start
            if (InputCode.PlayerDigitalButtons[3].Start.HasValue && InputCode.PlayerDigitalButtons[3].Start.Value)
                Control |= 0x200000;
            // Player 4 Trigger
            if (InputCode.PlayerDigitalButtons[3].Button1.HasValue && InputCode.PlayerDigitalButtons[3].Button1.Value)
                Control |= 0x400000;
            // Player 4 Option
            if (InputCode.PlayerDigitalButtons[3].Button2.HasValue && InputCode.PlayerDigitalButtons[3].Button2.Value)
                Control |= 0x800000;
            // Player 4 Shoulder
            if (InputCode.PlayerDigitalButtons[3].Button3.HasValue && InputCode.PlayerDigitalButtons[3].Button3.Value)
                Control |= 0x1000000;
            // Player 4 Sense
            if (InputCode.PlayerDigitalButtons[3].Button4.HasValue && InputCode.PlayerDigitalButtons[3].Button4.Value)
                Control |= 0x2000000;

            JvsHelper.StateView.Write(8, Control);
            JvsHelper.StateView.Write(12, InputCode.AnalogBytes[0]);  // P1X
            JvsHelper.StateView.Write(13, InputCode.AnalogBytes[2]);  // P1Y
            JvsHelper.StateView.Write(14, InputCode.AnalogBytes[4]);  // P2X
            JvsHelper.StateView.Write(15, InputCode.AnalogBytes[6]);  // P2Y
            JvsHelper.StateView.Write(16, InputCode.AnalogBytes[8]);  // P3X
            JvsHelper.StateView.Write(17, InputCode.AnalogBytes[10]); // P3Y
            JvsHelper.StateView.Write(18, InputCode.AnalogBytes[12]); // P4X
            JvsHelper.StateView.Write(19, InputCode.AnalogBytes[14]); // P4Y
        }
    }
}
