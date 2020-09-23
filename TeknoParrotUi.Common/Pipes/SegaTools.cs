using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TeknoParrotUi.Common.Jvs;

namespace TeknoParrotUi.Common.Pipes
{
    public class SegaTools : ControlSender
    {
        public override void Transmit()
        {
            // Start
            if (InputCode.PlayerDigitalButtons[0].Start.HasValue && InputCode.PlayerDigitalButtons[0].Start.Value)
                Control |= 0x01;

            // D-Pad
            // Up
            if (InputCode.PlayerDigitalButtons[0].Up.HasValue && InputCode.PlayerDigitalButtons[0].Up.Value)
                Control |= 0x02;
            // Down
            if (InputCode.PlayerDigitalButtons[0].Down.HasValue && InputCode.PlayerDigitalButtons[0].Down.Value)
                Control |= 0x04;
            // Left
            if (InputCode.PlayerDigitalButtons[0].Left.HasValue && InputCode.PlayerDigitalButtons[0].Left.Value)
                Control |= 0x08;
            // Right
            if (InputCode.PlayerDigitalButtons[0].Right.HasValue && InputCode.PlayerDigitalButtons[0].Right.Value)
                Control |= 0x10;

            // Shifter
            // Shift Up
            if (InputCode.PlayerDigitalButtons[1].Up.HasValue && InputCode.PlayerDigitalButtons[1].Up.Value)
                Control |= 0x20;
            // Shift Down
            if (InputCode.PlayerDigitalButtons[1].Down.HasValue && InputCode.PlayerDigitalButtons[1].Down.Value)
                Control |= 0x0100;

            // View Change
            if (InputCode.PlayerDigitalButtons[0].Button1.HasValue && InputCode.PlayerDigitalButtons[0].Button1.Value)
                Control |= 0x0200;

            // 1st Gear
            if (InputCode.PlayerDigitalButtons[0].ExtensionButton1.HasValue && InputCode.PlayerDigitalButtons[0].ExtensionButton1.Value)
                Control |= 0x0400;
            // 2nd Gear
            if (InputCode.PlayerDigitalButtons[0].ExtensionButton2.HasValue && InputCode.PlayerDigitalButtons[0].ExtensionButton2.Value)
                Control |= 0x0800;
            // 3rd Gear
            if (InputCode.PlayerDigitalButtons[0].ExtensionButton3.HasValue && InputCode.PlayerDigitalButtons[0].ExtensionButton3.Value)
                Control |= 0x1000;
            // 4th Gear
            if (InputCode.PlayerDigitalButtons[0].ExtensionButton4.HasValue && InputCode.PlayerDigitalButtons[0].ExtensionButton4.Value)
                Control |= 0x2000;
            // 5th Gear
            if (InputCode.PlayerDigitalButtons[0].ExtensionButton1_1.HasValue && InputCode.PlayerDigitalButtons[0].ExtensionButton1_1.Value)
                Control |= 0x4000;
            // 6th Gear
            if (InputCode.PlayerDigitalButtons[0].ExtensionButton1_2.HasValue && InputCode.PlayerDigitalButtons[0].ExtensionButton1_2.Value)
                Control |= 0x8000;
            // Test
            if (InputCode.PlayerDigitalButtons[0].Test.HasValue && InputCode.PlayerDigitalButtons[0].Test.Value)
                Control |= 0x010000;
            // Service
            if (InputCode.PlayerDigitalButtons[0].Service.HasValue && InputCode.PlayerDigitalButtons[0].Service.Value)
                Control |= 0x020000;
            // Coin
            if (InputCode.PlayerDigitalButtons[0].Coin.HasValue && InputCode.PlayerDigitalButtons[0].Coin.Value)
                Control |= 0x040000;
            // Insert AIME
            if (InputCode.PlayerDigitalButtons[0].ExtensionButton1_3.HasValue && InputCode.PlayerDigitalButtons[0].ExtensionButton1_3.Value)
                Control |= 0x080000;

   
            JvsHelper.StateView.Write(8, Control);
            JvsHelper.StateView.Write(12, InputCode.AnalogBytes[0]);
            JvsHelper.StateView.Write(16, InputCode.AnalogBytes[2]);
            JvsHelper.StateView.Write(20, InputCode.AnalogBytes[4]);
        }
    }
}
