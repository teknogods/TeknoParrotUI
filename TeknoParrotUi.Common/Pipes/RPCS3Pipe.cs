using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TeknoParrotUi.Common.Jvs;

namespace TeknoParrotUi.Common.Pipes
{
    // Copied from Dolphin Pipe
    public class RPCS3Pipe : ControlSender
    {
        public override void Transmit()
        {
            ulong control = 0;
            byte systemButtons = 0;
            // === SHARED/SYSTEM BUTTONS ===
            // Test (shared test button for system diagnostics) - Bit 0x01
            if (InputCode.PlayerDigitalButtons[0].Test.HasValue && InputCode.PlayerDigitalButtons[0].Test.Value)
                systemButtons |= 0x80;

            if (InputCode.PlayerDigitalButtons[0].Start.HasValue && InputCode.PlayerDigitalButtons[0].Start.Value)
                control |= 0x1UL;
            if (InputCode.PlayerDigitalButtons[0].Service.HasValue && InputCode.PlayerDigitalButtons[0].Service.Value)
                control |= 0x2UL;
            if (InputCode.PlayerDigitalButtons[0].Button1.HasValue && InputCode.PlayerDigitalButtons[0].Button1.Value)
                control |= 0x4UL;
            if (InputCode.PlayerDigitalButtons[0].Button2.HasValue && InputCode.PlayerDigitalButtons[0].Button2.Value)
                control |= 0x8UL;
            if (InputCode.PlayerDigitalButtons[0].Button3.HasValue && InputCode.PlayerDigitalButtons[0].Button3.Value)
                control |= 0x10UL;
            if (InputCode.PlayerDigitalButtons[0].Button4.HasValue && InputCode.PlayerDigitalButtons[0].Button4.Value)
                control |= 0x20UL;
            if (InputCode.PlayerDigitalButtons[0].Button5.HasValue && InputCode.PlayerDigitalButtons[0].Button5.Value)
                control |= 0x40UL;
            if (InputCode.PlayerDigitalButtons[0].Button6.HasValue && InputCode.PlayerDigitalButtons[0].Button6.Value)
                control |= 0x80UL;
            if (InputCode.PlayerDigitalButtons[0].Left.HasValue && InputCode.PlayerDigitalButtons[0].Left.Value)
                control |= 0x100UL;
            if (InputCode.PlayerDigitalButtons[0].Up.HasValue && InputCode.PlayerDigitalButtons[0].Up.Value)
                control |= 0x200UL;
            if (InputCode.PlayerDigitalButtons[0].Right.HasValue && InputCode.PlayerDigitalButtons[0].Right.Value)
                control |= 0x400UL;
            if (InputCode.PlayerDigitalButtons[0].Down.HasValue && InputCode.PlayerDigitalButtons[0].Down.Value)
                control |= 0x800UL;
            if (InputCode.PlayerDigitalButtons[0].ExtensionButton1_1.HasValue && InputCode.PlayerDigitalButtons[0].ExtensionButton1_1.Value)
                control |= 0x1000UL;
            if (InputCode.PlayerDigitalButtons[0].ExtensionButton1_2.HasValue && InputCode.PlayerDigitalButtons[0].ExtensionButton1_2.Value)
                control |= 0x2000UL;
            if (InputCode.PlayerDigitalButtons[0].ExtensionButton1_3.HasValue && InputCode.PlayerDigitalButtons[0].ExtensionButton1_3.Value)
                control |= 0x4000UL;
            if (InputCode.PlayerDigitalButtons[0].ExtensionButton1_4.HasValue && InputCode.PlayerDigitalButtons[0].ExtensionButton1_4.Value)
                control |= 0x8000UL;
            if (InputCode.PlayerDigitalButtons[0].ExtensionButton1_5.HasValue && InputCode.PlayerDigitalButtons[0].ExtensionButton1_5.Value)
                control |= 0x10000UL;
            if (InputCode.PlayerDigitalButtons[0].ExtensionButton1_6.HasValue && InputCode.PlayerDigitalButtons[0].ExtensionButton1_6.Value)
                control |= 0x20000UL;
            if (InputCode.PlayerDigitalButtons[0].ExtensionButton1_7.HasValue && InputCode.PlayerDigitalButtons[0].ExtensionButton1_7.Value)
                control |= 0x40000UL;
            if (InputCode.PlayerDigitalButtons[0].ExtensionButton1_8.HasValue && InputCode.PlayerDigitalButtons[0].ExtensionButton1_8.Value)
                control |= 0x80000UL;
            if (InputCode.PlayerDigitalButtons[0].ExtensionButton2.HasValue && InputCode.PlayerDigitalButtons[0].ExtensionButton2.Value)
                control |= 0x100000UL;
            if (InputCode.PlayerDigitalButtons[0].ExtensionButton3.HasValue && InputCode.PlayerDigitalButtons[0].ExtensionButton3.Value)
                control |= 0x200000UL;
            if (InputCode.PlayerDigitalButtons[0].ExtensionButton4.HasValue && InputCode.PlayerDigitalButtons[0].ExtensionButton4.Value)
                control |= 0x400000UL;
            if (InputCode.PlayerDigitalButtons[0].ExtensionButton2_1.HasValue && InputCode.PlayerDigitalButtons[0].ExtensionButton2_1.Value)
                control |= 0x800000UL;
            if (InputCode.PlayerDigitalButtons[0].ExtensionButton2_2.HasValue && InputCode.PlayerDigitalButtons[0].ExtensionButton2_2.Value)
                control |= 0x1000000UL;
            if (InputCode.PlayerDigitalButtons[0].ExtensionButton2_3.HasValue && InputCode.PlayerDigitalButtons[0].ExtensionButton2_3.Value)
                control |= 0x2000000UL;
            if (InputCode.PlayerDigitalButtons[0].ExtensionButton2_4.HasValue && InputCode.PlayerDigitalButtons[0].ExtensionButton2_4.Value)
                control |= 0x4000000UL;
            if (InputCode.PlayerDigitalButtons[0].ExtensionButton2_5.HasValue && InputCode.PlayerDigitalButtons[0].ExtensionButton2_5.Value)
                control |= 0x8000000UL;
            if (InputCode.PlayerDigitalButtons[0].ExtensionButton2_6.HasValue && InputCode.PlayerDigitalButtons[0].ExtensionButton2_6.Value)
                control |= 0x10000000UL;
            if (InputCode.PlayerDigitalButtons[0].ExtensionButton2_7.HasValue && InputCode.PlayerDigitalButtons[0].ExtensionButton2_7.Value)
                control |= 0x20000000UL;
            if (InputCode.PlayerDigitalButtons[0].ExtensionButton2_8.HasValue && InputCode.PlayerDigitalButtons[0].ExtensionButton2_8.Value)
                control |= 0x40000000UL;

            if (InputCode.PlayerDigitalButtons[1].Start.HasValue && InputCode.PlayerDigitalButtons[1].Start.Value)
                control |= 0x80000000UL;
            if (InputCode.PlayerDigitalButtons[1].Service.HasValue && InputCode.PlayerDigitalButtons[1].Service.Value)
                control |= 0x100000000UL;
            if (InputCode.PlayerDigitalButtons[1].Button1.HasValue && InputCode.PlayerDigitalButtons[1].Button1.Value)
                control |= 0x200000000UL;
            if (InputCode.PlayerDigitalButtons[1].Button2.HasValue && InputCode.PlayerDigitalButtons[1].Button2.Value)
                control |= 0x400000000UL;
            if (InputCode.PlayerDigitalButtons[1].Button3.HasValue && InputCode.PlayerDigitalButtons[1].Button3.Value)
                control |= 0x800000000UL;
            if (InputCode.PlayerDigitalButtons[1].Button4.HasValue && InputCode.PlayerDigitalButtons[1].Button4.Value)
                control |= 0x1000000000UL;
            if (InputCode.PlayerDigitalButtons[1].Button5.HasValue && InputCode.PlayerDigitalButtons[1].Button5.Value)
                control |= 0x2000000000UL;
            if (InputCode.PlayerDigitalButtons[1].Button6.HasValue && InputCode.PlayerDigitalButtons[1].Button6.Value)
                control |= 0x4000000000UL;
            if (InputCode.PlayerDigitalButtons[1].Left.HasValue && InputCode.PlayerDigitalButtons[1].Left.Value)
                control |= 0x8000000000UL;
            if (InputCode.PlayerDigitalButtons[1].Up.HasValue && InputCode.PlayerDigitalButtons[1].Up.Value)
                control |= 0x10000000000UL;
            if (InputCode.PlayerDigitalButtons[1].Right.HasValue && InputCode.PlayerDigitalButtons[1].Right.Value)
                control |= 0x20000000000UL;
            if (InputCode.PlayerDigitalButtons[1].Down.HasValue && InputCode.PlayerDigitalButtons[1].Down.Value)
                control |= 0x40000000000UL;
            if (InputCode.PlayerDigitalButtons[1].ExtensionButton1_1.HasValue && InputCode.PlayerDigitalButtons[1].ExtensionButton1_1.Value)
                control |= 0x80000000000UL;
            if (InputCode.PlayerDigitalButtons[1].ExtensionButton1_2.HasValue && InputCode.PlayerDigitalButtons[1].ExtensionButton1_2.Value)
                control |= 0x100000000000UL;
            if (InputCode.PlayerDigitalButtons[1].ExtensionButton1_3.HasValue && InputCode.PlayerDigitalButtons[1].ExtensionButton1_3.Value)
                control |= 0x200000000000UL;
            if (InputCode.PlayerDigitalButtons[1].ExtensionButton1_4.HasValue && InputCode.PlayerDigitalButtons[1].ExtensionButton1_4.Value)
                control |= 0x400000000000UL;
            if (InputCode.PlayerDigitalButtons[1].ExtensionButton1_5.HasValue && InputCode.PlayerDigitalButtons[1].ExtensionButton1_5.Value)
                control |= 0x800000000000UL;
            if (InputCode.PlayerDigitalButtons[1].ExtensionButton1_6.HasValue && InputCode.PlayerDigitalButtons[1].ExtensionButton1_6.Value)
                control |= 0x1000000000000UL;
            if (InputCode.PlayerDigitalButtons[1].ExtensionButton1_7.HasValue && InputCode.PlayerDigitalButtons[1].ExtensionButton1_7.Value)
                control |= 0x2000000000000UL;
            if (InputCode.PlayerDigitalButtons[1].ExtensionButton1_8.HasValue && InputCode.PlayerDigitalButtons[1].ExtensionButton1_8.Value)
                control |= 0x4000000000000UL;
            if (InputCode.PlayerDigitalButtons[1].ExtensionButton2.HasValue && InputCode.PlayerDigitalButtons[1].ExtensionButton2.Value)
                control |= 0x8000000000000UL;
            if (InputCode.PlayerDigitalButtons[1].ExtensionButton3.HasValue && InputCode.PlayerDigitalButtons[1].ExtensionButton3.Value)
                control |= 0x10000000000000UL;
            if (InputCode.PlayerDigitalButtons[1].ExtensionButton4.HasValue && InputCode.PlayerDigitalButtons[1].ExtensionButton4.Value)
                control |= 0x20000000000000UL;
            if (InputCode.PlayerDigitalButtons[1].ExtensionButton2_1.HasValue && InputCode.PlayerDigitalButtons[1].ExtensionButton2_1.Value)
                control |= 0x40000000000000UL;
            if (InputCode.PlayerDigitalButtons[1].ExtensionButton2_2.HasValue && InputCode.PlayerDigitalButtons[1].ExtensionButton2_2.Value)
                control |= 0x80000000000000UL;
            if (InputCode.PlayerDigitalButtons[1].ExtensionButton2_3.HasValue && InputCode.PlayerDigitalButtons[1].ExtensionButton2_3.Value)
                control |= 0x100000000000000UL;
            if (InputCode.PlayerDigitalButtons[1].ExtensionButton2_4.HasValue && InputCode.PlayerDigitalButtons[1].ExtensionButton2_4.Value)
                control |= 0x200000000000000UL;
            if (InputCode.PlayerDigitalButtons[1].ExtensionButton2_5.HasValue && InputCode.PlayerDigitalButtons[1].ExtensionButton2_5.Value)
                control |= 0x400000000000000UL;
            if (InputCode.PlayerDigitalButtons[1].ExtensionButton2_6.HasValue && InputCode.PlayerDigitalButtons[1].ExtensionButton2_6.Value)
                control |= 0x800000000000000UL;
            if (InputCode.PlayerDigitalButtons[1].ExtensionButton2_7.HasValue && InputCode.PlayerDigitalButtons[1].ExtensionButton2_7.Value)
                control |= 0x1000000000000000UL;
            if (InputCode.PlayerDigitalButtons[1].ExtensionButton2_8.HasValue && InputCode.PlayerDigitalButtons[1].ExtensionButton2_8.Value)
                control |= 0x2000000000000000UL;

            JvsHelper.StateView.Write(8, control);
            JvsHelper.StateView.Write(16, InputCode.AnalogBytes[0]);  // P1X
            JvsHelper.StateView.Write(17, InputCode.AnalogBytes[2]);  // P1Y
            JvsHelper.StateView.Write(18, InputCode.AnalogBytes[4]);  // P2X
            JvsHelper.StateView.Write(19, InputCode.AnalogBytes[6]);  // P2Y
            JvsHelper.StateView.Write(20, InputCode.AnalogBytes[8]);  // DSPS Wheel

            // TODO: add support for rotary encoders (DSPS wheel)

            // TODO: add taiko buttons (mapping buttons to analog hits)
            byte coinState = 0;
            if (InputCode.PlayerDigitalButtons[0].Coin.HasValue && InputCode.PlayerDigitalButtons[0].Coin.Value)
                coinState = 1;

            JvsHelper.StateView.Write(32, coinState); // Coin at separate offset
            JvsHelper.StateView.Write(33, systemButtons); // Test basically.
        }
    }
}
