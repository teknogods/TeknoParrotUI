using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TeknoParrotUi.Common.Jvs;

namespace TeknoParrotUi.Common.Pipes
{
    public class HotWheelsPipe : ControlSender
    {
        public override void Transmit()
        {
            // Test
            if (InputCode.PlayerDigitalButtons[0].Test.HasValue && InputCode.PlayerDigitalButtons[0].Test.Value)
                Control |= 0x01;

            // Player 1 Coin
            if (InputCode.PlayerDigitalButtons[0].Coin.HasValue && InputCode.PlayerDigitalButtons[0].Coin.Value)
                Control |= 0x02;

            // Player 2 Coin
            if (InputCode.PlayerDigitalButtons[1].Coin.HasValue && InputCode.PlayerDigitalButtons[1].Coin.Value)
                Control |= 0x04;

            // Player 3 Coin
            if (InputCode.PlayerDigitalButtons[0].Button1.HasValue && InputCode.PlayerDigitalButtons[0].Button1.Value)
                Control |= 0x08;

            // Player 4 Coin
            if (InputCode.PlayerDigitalButtons[1].Button1.HasValue && InputCode.PlayerDigitalButtons[1].Button1.Value)
                Control |= 0x10;

            // Player 5 Coin
            if (InputCode.PlayerDigitalButtons[0].Button2.HasValue && InputCode.PlayerDigitalButtons[0].Button2.Value)
                Control |= 0x20;

            // Player 6 Coin
            if (InputCode.PlayerDigitalButtons[1].Button2.HasValue && InputCode.PlayerDigitalButtons[1].Button2.Value)
                Control |= 0x40;

            JvsHelper.StateView.Write(8, Control); // Buttons
            JvsHelper.StateView.Write(12, InputCode.AnalogBytes[0]); // P1 Wheel
            JvsHelper.StateView.Write(16, InputCode.AnalogBytes[2]); // P1 Gas
            JvsHelper.StateView.Write(20, InputCode.AnalogBytes[4]); // P2 Wheel
            JvsHelper.StateView.Write(24, InputCode.AnalogBytes[6]); // P2 Gas
            JvsHelper.StateView.Write(28, InputCode.AnalogBytes[8]); // P3 Wheel
            JvsHelper.StateView.Write(32, InputCode.AnalogBytes[10]); // P3 Gas
            JvsHelper.StateView.Write(36, InputCode.AnalogBytes[12]); // P4 Wheel
            JvsHelper.StateView.Write(40, InputCode.AnalogBytes[14]); // P4 Gas
            JvsHelper.StateView.Write(44, InputCode.AnalogBytes[16]); // P5 Wheel
            JvsHelper.StateView.Write(48, InputCode.AnalogBytes[18]); // P5 Gas
            JvsHelper.StateView.Write(52, InputCode.AnalogBytes[20]); // P6 Wheel
            JvsHelper.StateView.Write(56, InputCode.AnalogBytes[22]); // P6 Gas
        }
    }
}
