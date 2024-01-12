using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TeknoParrotUi.Common.Jvs;

namespace TeknoParrotUi.Common.Pipes
{
    public class FrictionPipe : ControlSender
    {
        public override void Transmit()
        {
            // Test
            if (InputCode.PlayerDigitalButtons[0].Test.HasValue && InputCode.PlayerDigitalButtons[0].Test.Value)
                Control |= 0x01;

            // Coin
            if (InputCode.PlayerDigitalButtons[0].Coin.HasValue && InputCode.PlayerDigitalButtons[0].Coin.Value)
                Control |= 0x02;

            // P1 Trigger
            if (InputCode.PlayerDigitalButtons[1].Button1.HasValue && InputCode.PlayerDigitalButtons[1].Button1.Value)
                Control |= 0x04;

            // P2 Trigger
            if (InputCode.PlayerDigitalButtons[1].Button2.HasValue && InputCode.PlayerDigitalButtons[1].Button2.Value)
                Control |= 0x08;

            // Menu Select
            if (InputCode.PlayerDigitalButtons[1].Button3.HasValue && InputCode.PlayerDigitalButtons[1].Button3.Value)
                Control |= 0x10;

            // P1 Start
            if (InputCode.PlayerDigitalButtons[1].Button4.HasValue && InputCode.PlayerDigitalButtons[1].Button4.Value)
                Control |= 0x20;

            // P2 Start
            if (InputCode.PlayerDigitalButtons[1].Button5.HasValue && InputCode.PlayerDigitalButtons[1].Button5.Value)
                Control |= 0x40;

            // Menu Up
            if (InputCode.PlayerDigitalButtons[1].Up.HasValue && InputCode.PlayerDigitalButtons[1].Up.Value)
                Control |= 0x80;

            // Menu Down
            if (InputCode.PlayerDigitalButtons[1].Down.HasValue && InputCode.PlayerDigitalButtons[1].Down.Value)
                Control |= 0x100;

            // P1 Reload Button
            if (InputCode.PlayerDigitalButtons[1].Left.HasValue && InputCode.PlayerDigitalButtons[1].Left.Value)
                Control |= 0x200;

            // P2 Reload Button
            if (InputCode.PlayerDigitalButtons[1].Right.HasValue && InputCode.PlayerDigitalButtons[1].Right.Value)
                Control |= 0x400;

            // P2 Coin
            if (InputCode.PlayerDigitalButtons[1].Button6.HasValue && InputCode.PlayerDigitalButtons[1].Button6.Value)
                Control |= 0x800;

            JvsHelper.StateView.Write(8, Control);
            JvsHelper.StateView.Write(12, InputCode.AnalogBytes[0]);
            JvsHelper.StateView.Write(16, InputCode.AnalogBytes[2]);
            JvsHelper.StateView.Write(20, InputCode.AnalogBytes[4]);
            JvsHelper.StateView.Write(24, InputCode.AnalogBytes[6]);
        }
    }
}