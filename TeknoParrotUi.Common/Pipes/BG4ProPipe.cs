using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TeknoParrotUi.Common.Jvs;

namespace TeknoParrotUi.Common.Pipes
{
    public class BG4ProPipe : ControlSender
    {
        public override void Transmit()
        {
            // Test
            if (InputCode.PlayerDigitalButtons[0].Test.HasValue && InputCode.PlayerDigitalButtons[0].Test.Value)
                Control |= 0x0100;
            // Service
            if (InputCode.PlayerDigitalButtons[0].Service.HasValue && InputCode.PlayerDigitalButtons[0].Service.Value)
                Control |= 0x0200;
            // Coin Chute 1
            if (InputCode.PlayerDigitalButtons[0].Coin.HasValue && InputCode.PlayerDigitalButtons[0].Coin.Value)
                Control |= 0x0400;
            // Start
            if (InputCode.PlayerDigitalButtons[0].Button4.HasValue && InputCode.PlayerDigitalButtons[0].Button4.Value)
                Control |= 0x1000;
            // Key
            if (InputCode.PlayerDigitalButtons[0].Right.HasValue && InputCode.PlayerDigitalButtons[0].Right.Value)
                Control |= 0x2000;
            // View Change
            if (InputCode.PlayerDigitalButtons[0].Up.HasValue && InputCode.PlayerDigitalButtons[0].Up.Value)
                Control |= 0x8000;
            // Hazard
            if (InputCode.PlayerDigitalButtons[0].Down.HasValue && InputCode.PlayerDigitalButtons[0].Down.Value)
                Control |= 0x01;
            // Overtake
            if (InputCode.PlayerDigitalButtons[0].Left.HasValue && InputCode.PlayerDigitalButtons[0].Left.Value)
                Control |= 0x20;
            // SideBrake
            if (InputCode.PlayerDigitalButtons[0].Button1.HasValue && InputCode.PlayerDigitalButtons[0].Button1.Value)
                Control |= 0x02;
            // Start
            if (InputCode.PlayerDigitalButtons[0].Start.HasValue && InputCode.PlayerDigitalButtons[0].Start.Value)
                Control |= 0x40;
            // Shift Right
            if (InputCode.PlayerDigitalButtons[0].Button5.HasValue && InputCode.PlayerDigitalButtons[0].Button5.Value)
                Control |= 0x800;
            // Shift Left
            if (InputCode.PlayerDigitalButtons[0].Button6.HasValue && InputCode.PlayerDigitalButtons[0].Button6.Value)
                Control |= 0x4000;
            // Timer Start
            if (InputCode.PlayerDigitalButtons[0].ExtensionButton1.HasValue && InputCode.PlayerDigitalButtons[0].ExtensionButton1.Value)
                Control |= 0x10000;
            // Seat Switch 1
            if (InputCode.PlayerDigitalButtons[0].ExtensionButton3.HasValue && InputCode.PlayerDigitalButtons[0].ExtensionButton3.Value)
                Control |= 0x20000;
            // Seat Switch 2
            if (InputCode.PlayerDigitalButtons[0].ExtensionButton2.HasValue && InputCode.PlayerDigitalButtons[0].ExtensionButton2.Value)
                Control |= 0x40000;
            // Gear 1
            if (InputCode.PlayerDigitalButtons[0].ExtensionButton1_1.HasValue && InputCode.PlayerDigitalButtons[0].ExtensionButton1_1.Value)
                Control |= 0x100000;
            // Gear 2
            if (InputCode.PlayerDigitalButtons[0].ExtensionButton1_2.HasValue && InputCode.PlayerDigitalButtons[0].ExtensionButton1_2.Value)
                Control |= 0x200000;
            // Shift Up / Gear 3
            if (InputCode.PlayerDigitalButtons[0].Button3.HasValue && InputCode.PlayerDigitalButtons[0].Button3.Value)
                Control |= 0x04;
            // Shift Down / Gear 4
            if (InputCode.PlayerDigitalButtons[0].Button2.HasValue && InputCode.PlayerDigitalButtons[0].Button2.Value)
                Control |= 0x08;
            // Gear 5
            if (InputCode.PlayerDigitalButtons[0].ExtensionButton1_3.HasValue && InputCode.PlayerDigitalButtons[0].ExtensionButton1_3.Value)
                Control |= 0x1000000;
            // Gear 6/Reverse
            if (InputCode.PlayerDigitalButtons[0].ExtensionButton1_4.HasValue && InputCode.PlayerDigitalButtons[0].ExtensionButton1_4.Value)
                Control |= 0x2000000;

            JvsHelper.StateView.Write(8, Control);
            JvsHelper.StateView.Write(12, InputCode.AnalogBytes[0]);
            JvsHelper.StateView.Write(16, InputCode.AnalogBytes[2]);
            JvsHelper.StateView.Write(20, InputCode.AnalogBytes[4]);
            JvsHelper.StateView.Write(24, InputCode.AnalogBytes[6]);
        }
    }
}
